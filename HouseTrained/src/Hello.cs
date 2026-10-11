using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// "Only when every player nearby runs the pack" (decision 21): games running House Trained with friendly fire on say
    /// hello to each other on the game's routed messages (a message a game without the pack ignores; nothing is written
    /// into the world). Said to everyone while this game's friendly fire is on: once it's switched on, every 60 s and
    /// whenever a player joins; switched off (or the module, or after an error), one last "off", then nothing, since no
    /// hello already counts as "off" (code review 1, finding 7 and nit 17). A game with friendly fire on that hears a hello
    /// it hasn't answered in the last 60 s answers it directly, once. A game counts while its last hello is under 3
    /// minutes old and said its friendly fire is on (Peers). The chest service's hello (ChestShare) is the model:
    /// registered again on each new connection.
    /// </summary>
    internal static class Hello
    {
        internal const string Name = "modprojects.housetrained.hello";

        /// <summary>The protocol this copy speaks.</summary>
        internal const int Protocol = 1;

        /// <summary>A hello to everyone this often, in seconds; and a game is answered at most this often.</summary>
        internal const float Every = 60f;

        internal static readonly Peers Peers = new Peers();

        /// <summary>Sends a routed message to one game (or ZRoutedRpc.Everybody); a seam only the harness changes.</summary>
        internal static Action<long, string, ZPackage> Send = (target, name, package) => ZRoutedRpc.instance.InvokeRoutedRPC(target, name, package);

        private static readonly Dictionary<long, float> Answered = new Dictionary<long, float>();
        private static ZRoutedRpc registeredOn;
        private static bool listening;
        private static bool toldNotListening;
        private static float lastSaid = float.NegativeInfinity;
        private static bool saidOn;
        private static bool onNow;
        private static int playersSeen;

        /// <summary>Every frame, from the module's Update (also while it is switched off): registers on each new connection,
        /// and says hello while this game's friendly fire is on. Switched off, it says a last "off" and then nothing, and
        /// answers nothing (a registration can't be taken back: review F13).</summary>
        internal static void Tick(float now, bool moduleOn, bool friendlyFireOn)
        {
            onNow = moduleOn && friendlyFireOn;
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
            int players = ZNet.instance.GetPlayerList()?.Count ?? 0;
            bool joined = players > playersSeen;
            playersSeen = players;
            bool? say = ToSay(onNow, saidOn, joined, now, lastSaid);
            if (say.HasValue)
            {
                saidOn = say.Value;
                lastSaid = now;
                Say(ZRoutedRpc.Everybody, say.Value, reply: false);
            }
        }

        /// <summary>What to say to everyone now: "on" (true) while this game's friendly fire is on, when it was switched
        /// on, a player joined or a minute has passed; one last "off" (false) once it's off; otherwise nothing (null).</summary>
        internal static bool? ToSay(bool onNow, bool saidOn, bool joined, float now, float lastSaid)
        {
            if (!onNow)
            {
                return saidOn ? false : (bool?)null;
            }
            return !saidOn || joined || now - lastSaid >= Every ? true : (bool?)null;
        }

        /// <summary>Whether every player within <paramref name="distance"/> of the point deals friendly fire too (this
        /// game's own player always does: it asks only while its own is on).</summary>
        internal static bool EveryoneNearbyDeals(Vector3 point, float distance, float now)
        {
            long self = ZNet.GetUID();
            Nearby.Clear();
            Player.GetPlayersInRange(point, distance, Nearby);
            foreach (Player player in Nearby)
            {
                // A player's character is owned by that player's game (review F14).
                long game = player != null ? player.GetOwner() : 0L;
                if (game == 0L || !Peers.Deals(game, self, now))
                {
                    Nearby.Clear();
                    return false;
                }
            }
            Nearby.Clear();
            return true;
        }

        private static readonly List<Player> Nearby = new List<Player>();

        /// <summary>The module switched off after an error (its Update, and with it Tick, won't run again): the last "off"
        /// said here (code review 1, finding 7), and everything known forgotten.</summary>
        internal static void Forget()
        {
            onNow = false;
            try
            {
                if (saidOn && listening && ZRoutedRpc.instance != null && ZRoutedRpc.instance == registeredOn && Player.m_localPlayer != null)
                {
                    Say(ZRoutedRpc.Everybody, false, reply: false);
                }
            }
            finally
            {
                saidOn = false;
                Peers.Clear();
                Answered.Clear();
            }
        }

        // A new connection: its message handler, and everything known of the last one forgotten.
        private static void Register(ZRoutedRpc rpc)
        {
            registeredOn = rpc;
            Peers.Clear();
            Answered.Clear();
            saidOn = false;
            playersSeen = 0;
            listening = false;
            try
            {
                rpc.Register<ZPackage>(Name, OnHello);
                listening = true;
            }
            catch (ArgumentException)
            {
                // Another plugin carrying House Trained registered it first: it says hello for this game.
                if (!toldNotListening)
                {
                    toldNotListening = true;
                    Module.Warn("Another copy of House Trained answers the other games' hellos; this copy counts no other game as dealing friendly fire.");
                }
            }
        }

        private static void Say(long target, bool friendlyFireOn, bool reply)
        {
            var package = new ZPackage();
            package.Write(Protocol);
            package.Write(friendlyFireOn);
            package.Write(reply);
            Send(target, Name, package);
        }

        // A hello: that game's state remembered, and answered directly while this game's friendly fire is on, unless this
        // was its answer (once a minute at most).
        private static void OnHello(long sender, ZPackage package)
        {
            try
            {
                if (sender == ZNet.GetUID())
                {
                    return;   // this game's own hello, to everybody
                }
                int protocol = package.ReadInt();
                bool on = package.ReadBool();
                bool reply = package.ReadBool();
                float now = Time.time;
                Peers.Hear(sender, now, protocol >= Protocol && on);
                if (!reply && onNow && Module.IsOn && (!Answered.TryGetValue(sender, out float at) || now - at >= Every))
                {
                    Answered[sender] = now;
                    Say(sender, true, reply: true);
                }
            }
            catch (Exception error)
            {
                Module.Warn($"A friend's hello couldn't be read: {error.Message}");
            }
        }
    }
}
