using System;
using System.Collections.Generic;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// A light's or station's own marks, kept in its own record so every player on the pack follows them and the unmodded
    /// server keeps them with the world (the research's 2.3 and 4.2): whether it's fed or left alone (an int, FeedMark),
    /// and its linked chests (a text, LinkText). Written only by the game that looks after it, and sent at once, as Reserve
    /// Stock writes its mark on a chest. Set at one a friend's game looks after: sent to that game if it runs the mod (its
    /// hello says so), which writes it; otherwise kept on this computer and written as soon as this game looks after it
    /// (lost if the game is closed first). Cleared by writing "as its kind" (0) or an empty text, never by removing the
    /// key: a removal reaches no other player. Also the key at a light or station (Left Alt + E).
    /// </summary>
    internal static class Marks
    {
        /// <summary>The fed-or-left-alone mark's name in a light's or station's record. Permanent, once written anywhere.</summary>
        internal const string Key = "modprojects.topperupper.feed";

        /// <summary>A mark sent to a friend's game is shown here meanwhile for this long, in seconds, until its record
        /// comes back.</summary>
        internal const float SentShownFor = 10f;

        private const int WhatFeed = 1;
        private const int WhatLinks = 2;

        // Marks waiting, by the thing's ID and which mark: the fed mark and the links wait apart, so a link sent to a
        // friend's game never drops a fed mark kept here (code review n7).
        private static readonly Dictionary<(ZDOID Id, int What), Pending> Waiting = new Dictionary<(ZDOID, int), Pending>();
        private static ZNet world;

        /// <summary>Its fed-or-left-alone mark as this computer sees it: one waiting to be written first, else its record's.</summary>
        internal static int Feed(Fed fed)
        {
            if (Waiting.TryGetValue((fed.Zdo.m_uid, WhatFeed), out Pending pending))
            {
                return pending.Feed;
            }
            return FeedMark.Clean(fed.Zdo.GetInt(Key, FeedMark.AsKind));
        }

        /// <summary>Its links' text as this computer sees it: one waiting to be written first, else its record's.</summary>
        internal static string LinkText(Fed fed)
        {
            if (Waiting.TryGetValue((fed.Zdo.m_uid, WhatLinks), out Pending pending))
            {
                return pending.Links;
            }
            return fed.Zdo.GetString(Links.Key, string.Empty) ?? string.Empty;
        }

        /// <summary>Sets its fed-or-left-alone mark. Returns how: "" written, or a few words for a mark sent or kept here.</summary>
        internal static string SetFeed(Fed fed, int mark) => Set(fed, WhatFeed, FeedMark.Clean(mark), null);

        /// <summary>Sets its links' text. Returns how, as SetFeed.</summary>
        internal static string SetLinks(Fed fed, string text) => Set(fed, WhatLinks, 0, text ?? string.Empty);

        /// <summary>Every frame: the key at a light or station, and marks waiting to be written once this game looks after
        /// their thing.</summary>
        internal static void Update()
        {
            if (world != ZNet.instance)
            {
                world = ZNet.instance;
                Waiting.Clear();   // IDs are only good within one session
            }
            if (!Module.IsOn)
            {
                return;
            }
            WriteWaiting();
            if (!Keys.TakesKeys())
            {
                return;
            }
            bool lightKey = Keys.Pressed(Settings.LightKey.Value);
            bool stationKey = Keys.Pressed(Settings.StationKey.Value);
            if (!lightKey && !stationKey)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            Fed fed = Registry.Pointed(player != null ? player.GetHoverObject() : null);
            if (fed == null || (fed.IsLight ? !lightKey : !stationKey) || !Toggleable(fed))
            {
                return;
            }
            bool kind = fed.IsLight ? Lights.KindSaysFed(fed) : Stations.KindSaysFed(fed);
            int next = FeedMark.Next(Feed(fed), kind, fed.IsLight ? Settings.LightKeySteps.Value : Settings.StationKeySteps.Value);
            string how = SetFeed(fed, next);
            string state = next == FeedMark.LeftAlone ? "left alone" : next == FeedMark.Fed ? "fed (whatever its kind's switch says)" :
                           kind ? "fed, as its kind" : "not fed, as its kind";
            // Its part switched off: the mark is kept for when it's on, and he's told why nothing is fed now (n4).
            bool partOff = fed.IsLight ? !Settings.FeedLights.Value : !Settings.FeedStations.Value;
            string off = partOff && next != FeedMark.LeftAlone
                ? $" (nothing is fed while \"{(fed.IsLight ? "Feed lights and fires" : "Feed stations")}\" is off)"
                : string.Empty;
            Show($"{Names.Of(fed)}: {state}{how}{off}");
            fed.Due = 0f;   // looked at again at once
            fed.Barren = 0;
        }

        /// <summary>Whether the key acts on this light or station at all (one this mod would feed with its switches on).</summary>
        internal static bool Toggleable(Fed fed)
        {
            if (fed.IsLight)
            {
                return Lights.Read(fed, out _);
            }
            return Stations.Feedable(fed) && !(fed.Component is CookingStation cooking && !cooking.m_useFuel);
        }

        /// <summary>The undo step: marks waiting here are forgotten.</summary>
        internal static void Forget() => Waiting.Clear();

        /// <summary>A friend's game sent a mark for a light or station this game may look after (Hello's message): written
        /// if this game owns it. A plain, short text only; anything else is ignored.</summary>
        internal static void Received(long sender, ZPackage package)
        {
            ZDOID id = package.ReadZDOID();
            int what = package.ReadInt();
            int value = package.ReadInt();
            string text = package.ReadString();
            ZDO zdo = ZDOMan.instance?.GetZDO(id);
            if (zdo == null || !zdo.IsOwner())
            {
                return;   // not ours (any more): the sender keeps showing it a while; nothing is written
            }
            GameObject found = ZNetScene.instance?.FindInstance(id);
            Fed fed = found != null ? Registry.Pointed(found) : null;
            if (fed == null || fed.Zdo != zdo || !Toggleable(fed))
            {
                return;
            }
            if (what == WhatFeed)
            {
                Write(zdo, Key, FeedMark.Clean(value));
            }
            else if (what == WhatLinks)
            {
                List<Link> links = Logic.LinkText.Parse(text, out bool readable);
                if (readable)
                {
                    Write(zdo, Links.Key, Logic.LinkText.Write(links));   // read strictly, written afresh
                }
            }
        }

        private static string Set(Fed fed, int what, int mark, string text)
        {
            ZDO zdo = fed.Zdo;
            if (fed.Mine)
            {
                Waiting.Remove((zdo.m_uid, what));
                if (what == WhatFeed)
                {
                    Write(zdo, Key, mark);
                }
                else
                {
                    Write(zdo, Links.Key, text);
                }
                return string.Empty;
            }
            if (!Waiting.TryGetValue((zdo.m_uid, what), out Pending pending))
            {
                Waiting[(zdo.m_uid, what)] = pending = new Pending();
            }
            if (what == WhatFeed)
            {
                pending.Feed = mark;
            }
            else
            {
                pending.Links = text;
            }
            long owner = zdo.GetOwner();
            if (owner != 0L && Hello.RunsTheMod(owner))
            {
                var package = new ZPackage();
                package.Write(zdo.m_uid);
                package.Write(what);
                package.Write(mark);
                package.Write(text ?? string.Empty);
                Hello.SendMark(owner, package);
                pending.SentAt = Time.time;
                return " (sent to the friend's game that looks after it)";
            }
            pending.SentAt = -1f;
            return " (kept here until your game looks after it)";
        }

        // Marks kept here: written once this game looks after their thing; a sent one shown only until its record is back.
        private static void WriteWaiting()
        {
            if (Waiting.Count == 0 || ZDOMan.instance == null)
            {
                return;
            }
            List<(ZDOID, int)> done = null;
            float now = Time.time;
            foreach (KeyValuePair<(ZDOID Id, int What), Pending> each in Waiting)
            {
                ZDO zdo = ZDOMan.instance.GetZDO(each.Key.Id);
                if (zdo == null)
                {
                    continue;   // not loaded now: kept until it is
                }
                if (each.Value.SentAt >= 0f)
                {
                    if (now - each.Value.SentAt > SentShownFor)
                    {
                        (done = done ?? new List<(ZDOID, int)>()).Add(each.Key);
                    }
                    continue;
                }
                if (!zdo.IsOwner())
                {
                    continue;
                }
                if (each.Key.What == WhatFeed)
                {
                    Write(zdo, Key, each.Value.Feed);
                }
                else
                {
                    Write(zdo, Links.Key, each.Value.Links);
                }
                (done = done ?? new List<(ZDOID, int)>()).Add(each.Key);
            }
            if (done != null)
            {
                foreach ((ZDOID, int) key in done)
                {
                    Waiting.Remove(key);
                }
            }
        }

        private static void Write(ZDO zdo, string key, int value)
        {
            if (zdo.GetInt(key, FeedMark.AsKind) == value)
            {
                return;
            }
            zdo.Set(key, value);
            ZDOMan.instance?.ForceSendZDO(zdo.m_uid);
        }

        private static void Write(ZDO zdo, string key, string text)
        {
            text = text ?? string.Empty;   // a stored null would make the record's next write of the key throw
            if ((zdo.GetString(key, string.Empty) ?? string.Empty) == text)
            {
                return;
            }
            zdo.Set(key, text);
            ZDOMan.instance?.ForceSendZDO(zdo.m_uid);
        }

        /// <summary>A line in the middle of the screen.</summary>
        internal static void Show(string text) => MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, text);

        private sealed class Pending
        {
            internal int Feed;
            internal string Links = string.Empty;

            /// <summary>When it was sent to a friend's game, or -1 for one kept here until this game looks after it.</summary>
            internal float SentAt = -1f;
        }
    }
}
