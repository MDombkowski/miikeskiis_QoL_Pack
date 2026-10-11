using System.Collections.Generic;
using UnityEngine;

namespace AFKRaidBlocker
{
    /// <summary>
    /// The player's away state, live: each frame it reads his game (his movement, the keys and the mouse, what he sits
    /// or lies on, who is near), runs the away rules (AwayRules) and the presence rule (Nearby), says so when it
    /// changes, and sets what the patches read (HoldsOff). Runs every frame from the module's Update, also while the mod
    /// is switched off, so that switching off takes everything back at once.
    /// </summary>
    internal static class Away
    {
        private static readonly AwayRules rules = new AwayRules();
        private static readonly EmoteList emotes = new EmoteList();
        private static readonly List<Other> others = new List<Other>();
        private static readonly HashSet<ZDOID> loaded = new HashSet<ZDOID>();

        private static Transform chairPoint;
        private static bool chairPointIsChair;

        private static ZNet world;
        private static bool holding;
        private static HeldOff heldMode;
        private static bool someoneThere;

        /// <summary>True while raids are held off for him: the mod is on, he is away, and nobody near him is still
        /// playing. What both patches read.</summary>
        internal static bool HoldsOff => holding;

        internal static void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            Player player = Player.m_localPlayer;
            ZNet net = ZNet.instance;
            if (!Module.IsOn || net == null)
            {
                rules.Reset(now);
                world = null;
                someoneThere = false;
                SetHolding(false);
                Mark.WriteOwn(player, false, false);
                return;
            }
            if (!ReferenceEquals(net, world))
            {
                // A new world (or the mod switched on): he starts out there, the minutes counting from now. A death and
                // respawn in the same world doesn't count: the game respawns him by itself, at his bed.
                world = net;
                rules.Reset(now);
                someoneThere = false;
            }

            emotes.Read(Settings.Emotes.Value);
            var sample = new Sample
            {
                Now = now,
                Moved = player != null && player.GetMoveDir() != Vector3.zero,
                Input = InputSeen.ThisFrame(now),
                Seat = player != null ? SeatOf(player) : Seat.None,
            };
            if (rules.Step(sample, Settings.Rules))
            {
                Tell(rules.Away, rules.Reason);
            }

            bool there = rules.Away && SomeoneThere(player, someoneThere);
            if (rules.Away && there != someoneThere && Settings.Log.Value)
            {
                Module.Log.LogInfo(there
                    ? "A player still playing is near you: raids can start on you while they are."
                    : $"Nobody near you is playing any more: {Held(Settings.HeldOff.Value)} held off again.");
            }
            someoneThere = there;
            SetHolding(rules.Away && !there);
            Mark.WriteOwn(player, rules.Away, rules.Away && Settings.ShowMyMark.Value);
        }

        /// <summary>Takes back everything: nothing held off, his flags off. An undo step of the module.</summary>
        internal static void Stop()
        {
            SetHolding(false);
            Mark.WriteOwn(Player.m_localPlayer, false, false);
        }

        /// <summary>What he sits or lies on, as the game says: in a bed, in a chair (a bench, a throne), or holding the
        /// pose of a listed emote. The game keeps the emote's name on his character until he moves, but a hit can knock
        /// him out of the pose meanwhile: the emote counts only while the game still holds it (InEmote: sit keeps its
        /// state; rest's and relax's lying loops carry the animator's "emote" tag).</summary>
        private static Seat SeatOf(Player player)
        {
            if (player.InBed())
            {
                return Seat.Bed;
            }
            if (player.IsAttached())
            {
                return Settings.ChairsCount.Value && IsChair(player.GetAttachPoint()) ? Seat.Chair : Seat.None;
            }
            ZNetView view = player.GetComponent<ZNetView>();
            ZDO record = view != null && view.IsValid() ? view.GetZDO() : null;
            string emote = record?.GetString(ZDOVars.s_emote) ?? "";
            return emotes.Has(emote) && player.InEmote() ? Seat.Emote : Seat.None;
        }

        /// <summary>Whether an attach point is a chair's (a chair, a bench, a throne, a ship's bench): under a Chair, or
        /// named as one's by a Chair elsewhere in the same piece. Worked out once per attach point.</summary>
        private static bool IsChair(Transform point)
        {
            if (point == null)
            {
                return false;
            }
            if (ReferenceEquals(point, chairPoint))
            {
                return chairPointIsChair;
            }
            chairPoint = point;
            chairPointIsChair = point.GetComponentInParent<Chair>() != null;
            if (!chairPointIsChair)
            {
                foreach (Chair chair in point.root.GetComponentsInChildren<Chair>())
                {
                    if (chair.m_attachPoint == point)
                    {
                        chairPointIsChair = true;
                        break;
                    }
                }
            }
            return chairPointIsChair;
        }

        /// <summary>Whether a player who isn't away stands within the setting's distance of him: every player his game
        /// has loaded, by his own game's word on whether he's away; and every other player who shares his position on
        /// the map (the server's player list), counted as there, since nothing says he's away.</summary>
        private static bool SomeoneThere(Player player, bool wasThere)
        {
            int metres = Settings.NearbyMetres.Value;
            if (player == null || metres <= 0)
            {
                return false;
            }
            others.Clear();
            loaded.Clear();
            Vector3 here = player.transform.position;
            foreach (Player other in Player.GetAllPlayers())
            {
                if (other == null || other == player)
                {
                    continue;
                }
                loaded.Add(other.GetZDOID());
                Vector3 there = other.transform.position;
                others.Add(new Other { Dx = there.x - here.x, Dz = there.z - here.z, Away = Mark.IsAway(other) });
            }
            ZDOID self = player.GetZDOID();
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                if (!info.m_publicPosition || info.m_characterID.IsNone() || info.m_characterID == self || loaded.Contains(info.m_characterID))
                {
                    continue;
                }
                others.Add(new Other { Dx = info.m_position.x - here.x, Dz = info.m_position.z - here.z, Away = false });
            }
            return Nearby.AnyoneThere(others, metres, wasThere);
        }

        /// <summary>Changes what the patches read. Whenever that changes (held off or not, or what's held off), the host
        /// reads every report again before its next raid roll: in single player, or a world he hosts, his game is the
        /// server. A client's next report reaches the server within 2 seconds by itself.</summary>
        private static void SetHolding(bool hold)
        {
            HeldOff mode = Settings.HeldOff.Value;
            if (hold == holding && (!hold || mode == heldMode))
            {
                return;
            }
            holding = hold;
            heldMode = mode;
            RandEventSystem.SetRandomEventsNeedsRefresh();
        }

        private static void Tell(bool away, Why reason)
        {
            HeldOff what = Settings.HeldOff.Value;
            MessagePlace place = Settings.Message.Value;
            if (place != MessagePlace.None && MessageHud.instance != null)
            {
                MessageHud.instance.ShowMessage(place == MessagePlace.TopLeft ? MessageHud.MessageType.TopLeft : MessageHud.MessageType.Center,
                                                Message(away, what, Settings.NearbyMetres.Value > 0));
            }
            if (Settings.Log.Value)
            {
                Module.Log.LogInfo(away
                    ? $"Away ({Because(reason)}): {Held(what)} held off" + (Settings.NearbyMetres.Value > 0 ? " while nobody near you is playing." : ".")
                    : $"Back: {Held(what)} can start on you again.");
            }
        }

        internal static string Message(bool away, HeldOff what, bool unlessSomeoneNear)
        {
            if (!away)
            {
                return what == HeldOff.EveryRandomEvent ? "Back: raids and events can start on you again" : "Back: raids can start on you again";
            }
            string held = what == HeldOff.EveryRandomEvent ? "Away: no raid or event will start on you" : "Away: raids won't start on you";
            return unlessSomeoneNear ? held + " while nobody near you is playing" : held;
        }

        internal static string Because(Why reason)
        {
            switch (reason)
            {
                case Why.Emote:
                    return "sitting or lying down by an emote";
                case Why.Chair:
                    return "sitting in a chair";
                case Why.Bed:
                    return "lying in a bed";
                case Why.NoMovement:
                    return Settings.InputCounts.Value == InputCounts.MovementOnly
                        ? $"no movement for {Settings.AwayAfterMinutes.Value} minutes"
                        : $"no movement or input for {Settings.AwayAfterMinutes.Value} minutes";
                default:
                    return "there";
            }
        }

        private static string Held(HeldOff what) =>
            what == HeldOff.EveryRandomEvent ? "every random event" : "raids that need a base";
    }
}
