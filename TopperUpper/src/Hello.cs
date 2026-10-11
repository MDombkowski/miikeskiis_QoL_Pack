using System;
using System.Collections.Generic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// Games running Topper-Upper say hello to each other on the game's routed messages (a message a game without the
    /// mod ignores; nothing is written into the world): its protocol and whether it feeds lights, stations, and friends'
    /// things the way a hand would. Said to everyone while the module is on: when it starts, whenever those change,
    /// every 60 s and when a player joins; switched off (or after an error), one last word, then nothing. A hello
    /// not yet answered in the last 60 s is answered directly, once. A game counts as running the mod while its last hello
    /// is under 3 minutes old. Needed for two things only (the research's 2.3): a mark (left alone, links) set at a light
    /// or station a friend's game looks after is sent to that game, and "feed as a hand would" sends to games without the
    /// mod only, one of the games with it choosing to (the lowest network ID among those near). House Trained's hello and
    /// the chest service's are the model: registered again on each new connection.
    /// </summary>
    internal static class Hello
    {
        internal const string Name = "modprojects.topperupper.hello";

        /// <summary>The routed message carrying a mark to the game that looks after a light or station (Marks, Links).</summary>
        internal const string MarkName = "modprojects.topperupper.mark";

        internal const int Protocol = 1;

        /// <summary>A hello to everyone this often, in seconds; a game is answered at most this often.</summary>
        internal const float Every = 60f;

        /// <summary>A hello counts for this long, in seconds.</summary>
        internal const float Lasts = 180f;

        /// <summary>The module runs and is switched on there (a hello without it is a game's last word).</summary>
        internal const int On = 8;
        internal const int FeedsLights = 1;
        internal const int FeedsStations = 2;
        internal const int FeedsByHand = 4;

        private static readonly Dictionary<long, Heard> Peers = new Dictionary<long, Heard>();
        private static readonly Dictionary<long, float> Answered = new Dictionary<long, float>();
        private static ZRoutedRpc registeredOn;
        private static bool listening;
        private static bool toldNotListening;
        private static float lastSaid = float.NegativeInfinity;
        private static int saidFlags = -1;
        private static int playersSeen;

        /// <summary>Every frame, from the module's Update (also while it is switched off): registers on each new
        /// connection, and says hello while the module is on.</summary>
        internal static void Tick()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null)
            {
                return;
            }
            if (rpc != registeredOn)
            {
                Register(rpc);
            }
            if (!listening || Player.m_localPlayer == null || ZNet.instance == null)
            {
                return;
            }
            int flags = Flags();
            int players = ZNet.instance.GetPlayerList()?.Count ?? 0;
            bool joined = players > playersSeen;
            playersSeen = players;
            float now = Time.time;
            if (flags < 0)
            {
                if (saidFlags > 0)
                {
                    Say(ZRoutedRpc.Everybody, 0, reply: false);   // one last word: switched off
                    saidFlags = 0;
                }
                return;
            }
            if (flags != saidFlags || joined || now - lastSaid >= Every)
            {
                saidFlags = flags;
                lastSaid = now;
                Say(ZRoutedRpc.Everybody, flags, reply: false);
            }
        }

        /// <summary>Whether that game runs Topper-Upper, switched on, by its last hello (in the last 3 minutes).</summary>
        internal static bool RunsTheMod(long game) =>
            game == ZNet.GetUID() || (Peers.TryGetValue(game, out Heard heard) && Time.time - heard.At <= Lasts && heard.Protocol >= 1 &&
                                      (heard.Flags & On) != 0);

        /// <summary>Whether that game, by its last hello, feeds what this flag names (FeedsLights, FeedsStations, FeedsByHand).</summary>
        internal static bool Feeds(long game, int flag) =>
            Peers.TryGetValue(game, out Heard heard) && Time.time - heard.At <= Lasts && (heard.Flags & flag) != 0;

        /// <summary>
        /// For "feed as a hand would": whether this game is the one to send, of the games feeding by hand whose player is
        /// within <paramref name="range"/> of the thing (this game's own player must be): the lowest network ID among them.
        /// </summary>
        internal static bool ChosenToFeedByHand(Vector3 at, float range)
        {
            long self = ZNet.GetUID();
            bool selfNear = false;
            long lowest = long.MaxValue;
            foreach (Player player in Player.GetAllPlayers())
            {
                if (player == null || (player.transform.position - at).sqrMagnitude > range * range)
                {
                    continue;
                }
                long game = player.GetOwner();
                if (game == self)
                {
                    selfNear = true;
                }
                else if (!Feeds(game, FeedsByHand))
                {
                    continue;
                }
                lowest = Math.Min(lowest, game);
            }
            return selfNear && lowest == self;
        }

        /// <summary>Sends a mark to the game that looks after a light or station (Marks, Links).</summary>
        internal static void SendMark(long owner, ZPackage package)
        {
            if (listening && ZRoutedRpc.instance != null)
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(owner, MarkName, package);
            }
        }

        /// <summary>After an error (the module's Update, and with it Tick, won't run again): one last word, and everything
        /// known forgotten.</summary>
        internal static void Forget()
        {
            try
            {
                if (saidFlags > 0 && listening && ZRoutedRpc.instance != null && ZRoutedRpc.instance == registeredOn && Player.m_localPlayer != null)
                {
                    Say(ZRoutedRpc.Everybody, 0, reply: false);
                }
            }
            finally
            {
                saidFlags = -1;
                Peers.Clear();
                Answered.Clear();
            }
        }

        // What this game feeds now, or -1 while the module is off.
        private static int Flags()
        {
            if (!Module.IsOn)
            {
                return -1;
            }
            int flags = On;
            if (Settings.FeedLights.Value)
            {
                flags |= FeedsLights;
            }
            if (Settings.FeedStations.Value)
            {
                flags |= FeedsStations;
            }
            if (Settings.Friends.Value == FriendsWithout.FeedThemAsAHandWould)
            {
                flags |= FeedsByHand;
            }
            return flags;
        }

        // A new connection: its message handlers, and everything known of the last one forgotten.
        private static void Register(ZRoutedRpc rpc)
        {
            registeredOn = rpc;
            Peers.Clear();
            Answered.Clear();
            saidFlags = -1;
            playersSeen = 0;
            listening = false;
            try
            {
                rpc.Register<ZPackage>(Name, OnHello);
                rpc.Register<ZPackage>(MarkName, OnMark);
                listening = true;
            }
            catch (ArgumentException)
            {
                // Another plugin carrying Topper-Upper registered them first: it answers for this game.
                if (!toldNotListening)
                {
                    toldNotListening = true;
                    Module.Warn("Another copy of Topper-Upper answers the other games' hellos; this copy sends no marks to friends' games.");
                }
            }
        }

        private static void Say(long target, int flags, bool reply)
        {
            var package = new ZPackage();
            package.Write(Protocol);
            package.Write(flags);
            package.Write(reply);
            ZRoutedRpc.instance.InvokeRoutedRPC(target, Name, package);
        }

        private static void OnHello(long sender, ZPackage package)
        {
            try
            {
                if (sender == ZNet.GetUID())
                {
                    return;
                }
                int protocol = package.ReadInt();
                int flags = package.ReadInt();
                bool reply = package.ReadBool();
                float now = Time.time;
                Peers[sender] = new Heard(now, protocol, flags);
                int mine = Flags();
                if (!reply && mine >= 0 && (!Answered.TryGetValue(sender, out float at) || now - at >= Every))
                {
                    Answered[sender] = now;
                    Say(sender, mine, reply: true);
                }
            }
            catch (Exception error)
            {
                Module.Warn($"A friend's hello couldn't be read: {error.Message}");
            }
        }

        private static void OnMark(long sender, ZPackage package)
        {
            try
            {
                if (!Module.IsOn)
                {
                    return;
                }
                Module.Guard("writing a mark a friend set", () => Marks.Received(sender, package));
            }
            catch (Exception error)
            {
                Module.Warn($"A friend's mark couldn't be read: {error.Message}");
            }
        }

        private readonly struct Heard
        {
            internal readonly float At;
            internal readonly int Protocol;
            internal readonly int Flags;

            internal Heard(float at, int protocol, int flags)
            {
                At = at;
                Protocol = protocol;
                Flags = flags;
            }
        }
    }
}
