using System;
using System.Collections.Generic;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// The loop (the research's 2.6 and 5): each frame a cursor passes over a few of the lights and stations loaded (at
    /// most 64), looks at those whose turn has come (a setting, 4; lights every 10 s, stations every 3 s: settings) and
    /// lines up those wanting a feed; then feeds at most one a frame (a setting), the emptiest light first when the round
    /// that found them has ended. A second cursor looks for what stations made, about once a second each. Reading a record
    /// is cheap; a feed touches chests, so it's the one thing kept to one a frame. Nothing is written to the log per feed
    /// unless "Write what's fed to the log" is on. Switched off, nothing runs and everything waiting is forgotten.
    /// </summary>
    internal static class Loop
    {
        /// <summary>The most entries passed over in one frame.</summary>
        internal const int ScanCap = 64;

        /// <summary>A want waits at most this long for its round to end, in seconds.</summary>
        internal const float MaxWait = 2f;

        /// <summary>How often each station is looked at for what it made, in seconds.</summary>
        internal const float ProductsEvery = 1f;

        /// <summary>The registry is pruned of what's gone this often, in frames.</summary>
        internal const int PruneEvery = 30;

        private static readonly Turns Looks = new Turns();
        private static readonly Turns ProductLooks = new Turns();
        private static readonly Turns OwnerLooks = new Turns();
        private static readonly Wants<Fed> Waiting = new Wants<Fed>();
        private static readonly List<int> Picked = new List<int>();
        private static readonly List<int> PickedPasses = new List<int>();
        private static readonly Func<int, float> DueOf = i => Registry.All[i].Due;
        private static readonly Func<int, float> ProductsDueOf = i => Registry.All[i].ProductsDue;
        private static readonly Func<int, float> Always = i => float.NegativeInfinity;

        /// <summary>How many stations' owners are looked at each frame, for "stand aside while the server feeds".</summary>
        internal const int OwnersPerFrame = 16;
        private static int prunedAt;
        private static ZNet world;
        private static bool wasOn;

        internal static void Tick()
        {
            bool on = Module.IsOn;
            if (!on)
            {
                if (wasOn)
                {
                    Forget();
                }
                wasOn = false;
                return;
            }
            Player player = Player.m_localPlayer;
            if (player == null || ZNet.instance == null || ZNetScene.instance == null || Game.instance == null || ZDOMan.instance == null)
            {
                return;
            }
            if (world != ZNet.instance)
            {
                world = ZNet.instance;
                Forget();
            }
            wasOn = true;
            float now = Time.time;
            if (Time.frameCount - prunedAt >= PruneEvery || Time.frameCount < prunedAt)
            {
                prunedAt = Time.frameCount;
                Registry.Prune(Looks, ProductLooks, OwnerLooks);
                Waiting.RemoveWhere(fed => !fed.Alive);
            }
            Vector3 him = player.transform.position;

            // Every station's owner, often (a dozen frames round a base of two hundred), so the server taking one over for
            // a moment is seen (Owners.Watch).
            OwnerLooks.Next(Registry.All.Count, Always, now, OwnersPerFrame, OwnersPerFrame, Picked);
            foreach (int index in Picked)
            {
                Fed fed = Registry.All[index];
                if (fed.Alive && !fed.IsLight)
                {
                    Owners.Watch(fed, now);
                }
            }

            Looks.Next(Registry.All.Count, DueOf, now, ScanCap, Settings.LookPerFrame.Value, Picked, PickedPasses);
            for (int k = 0; k < Picked.Count; k++)
            {
                Fed fed = Registry.All[Picked[k]];
                if (!fed.Alive)
                {
                    continue;
                }
                // One whose last feeds found nothing to give is looked at less often (up to five times the interval), until
                // something changes (a feed gives, its key is pressed).
                float every = fed.IsLight ? Settings.LightsEvery.Value : Settings.StationsEvery.Value;
                fed.Due = now + every * (1 + Math.Min(fed.Barren, 4));
                float key;
                bool wants = fed.IsLight ? Lights.Look(fed, him, now, out key) : Stations.Look(fed, him, now, out key);
                if (wants)
                {
                    Waiting.Add(fed, PickedPasses[k], now, key);   // the round it was found in (Turns.Next)
                }
                else
                {
                    Waiting.Remove(fed);
                }
            }

            ProductLooks.Next(Registry.All.Count, ProductsDueOf, now, ScanCap, Settings.LookPerFrame.Value, Picked);
            foreach (int index in Picked)
            {
                Fed fed = Registry.All[index];
                if (!fed.Alive || fed.IsLight)
                {
                    fed.ProductsDue = now + ProductsEvery * 10f;
                    continue;
                }
                fed.ProductsDue = now + ProductsEvery;
                Products.Look(fed, now);
            }

            int feeds = Settings.FeedsPerFrame.Value;
            for (int i = 0; i < feeds; i++)
            {
                Fed fed = Waiting.Take(Looks.Pass, now, MaxWait);
                if (fed == null)
                {
                    break;
                }
                if (!fed.Alive)
                {
                    i--;   // gone meanwhile: costs nothing, so the next one gets this frame's feed
                    continue;
                }
                if (fed.IsLight)
                {
                    Lights.Feed(fed);
                }
                else
                {
                    Stations.Feed(fed);
                }
            }
        }

        /// <summary>The undo step, and switching off or a new world: everything waiting and counted forgotten (the
        /// registry stays: it only notes what's loaded).</summary>
        internal static void Forget()
        {
            Waiting.Clear();
            Counts.Forget();
            Looks.Reset();
            ProductLooks.Reset();
            OwnerLooks.Reset();
            foreach (Fed fed in Registry.All)
            {
                fed.Why = null;
            }
        }
    }
}
