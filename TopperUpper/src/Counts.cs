using System;
using System.Collections.Generic;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// What's near him (or near a station), counted for his reserves and maximums (the research's 3.1 and 3.2): what the
    /// usable chests within the count range hold, whole (leave-one is for taking, not counting), his personal chests
    /// whether or not they're used, reserved chests too when "Count reserved chests" is on, what he carries when "Count
    /// what you carry" is on; and, for maximums, what stations loaded nearby will make from what's queued in them, a
    /// windmill's held flour, a fermenter's meads (and those just tapped), the Frigid Kiln's Ice and what lies at their
    /// outputs. From this computer's copies of the chests, whether or not his game looks after them: right for counting,
    /// never for taking. Made at most every few seconds (a setting); in between, every feed's takes and queued products
    /// are read from the one ledger every count shares (Counts.Ledger; code review m6).
    /// </summary>
    internal sealed class Tally
    {
        /// <summary>Held near him, by shared name, as counted.</summary>
        internal readonly Dictionary<string, int> Held = new Dictionary<string, int>();

        /// <summary>Products on their way, by shared name, as counted: queued, held, fermenting, lying at an output.</summary>
        internal readonly Dictionary<string, int> Made = new Dictionary<string, int>();

        internal float At;
        internal Vector3 Centre;
        internal float Range;
        internal int SettingsVersion;

        /// <summary>The ledger's number when it was made: what's noted after it isn't in it.</summary>
        internal long Mark;

        /// <summary>What's held now: as counted, less what every feed took since.</summary>
        internal int HeldOf(string name)
        {
            int counted = name != null && Held.TryGetValue(name, out int count) ? count : 0;
            return Math.Max(0, counted - Counts.Ledger.TakenSince(name, Mark));
        }

        /// <summary>What counts toward a product's maximum: held, and (with "Count what stations hold") on its way, what
        /// every feed queued since, and meads just tapped.</summary>
        internal int CountedOf(string name)
        {
            int made = name != null && Made.TryGetValue(name, out int counted) ? counted : 0;
            return HeldOf(name) + made + Counts.Ledger.QueuedSince(name, Mark) + Counts.TappedNow(name);
        }

        internal static void Add(Dictionary<string, int> into, string name, int amount)
        {
            if (name == null || amount <= 0)
            {
                return;
            }
            into.TryGetValue(name, out int sum);
            into[name] = sum + amount;
        }
    }

    /// <summary>The counts, made and kept: one around him, or one around each station (CountWhere); the ledger they all
    /// read; the meads just tapped; and the chests near him a light's feed takes from, found with the count.</summary>
    internal static class Counts
    {
        /// <summary>A count around him is made again sooner only when he has moved this far (a portal), in metres.</summary>
        internal const float MovedFar = 64f;

        /// <summary>How long a tap's meads count before they can have landed and been put away, beyond the wait setting,
        /// in seconds (the fermenter's own tap delay is 1.5 s).</summary>
        internal const float TapLands = 3f;

        /// <summary>Every feed's takes and queued products, for every count (m6).</summary>
        internal static readonly Ledger Ledger = new Ledger();

        private static readonly Taps Tapped = new Taps();
        private static Tally aroundHim;
        private static readonly Dictionary<Fed, Tally> AroundStations = new Dictionary<Fed, Tally>();
        private static List<Container> chestsNearHim;
        private static float chestsAt;
        private static float chestsRange;
        private static int chestsVersion;
        private static Vector3 chestsCentre;

        /// <summary>The count a feed of <paramref name="fed"/> uses: around him, or around the station, as the setting
        /// says (lights always around him), made again once it's older than "Count the chests at most every" (around him,
        /// also once he has moved far: a portal).</summary>
        internal static Tally For(Fed fed)
        {
            Player player = Player.m_localPlayer;
            float now = Time.time;
            float range = Settings.CountingRange;
            if (fed != null && !fed.IsLight && Settings.CountWhere.Value == CountAround.AroundEachStation)
            {
                AroundStations.TryGetValue(fed, out Tally tally);
                if (Stale(tally, fed.Position, range, now))
                {
                    tally = Make(fed.Position, range, now);
                    if (AroundStations.Count > 64)
                    {
                        AroundStations.Clear();
                    }
                    AroundStations[fed] = tally;
                    ForgetOld();
                }
                return tally;
            }
            Vector3 centre = player != null ? player.transform.position : Vector3.zero;
            if (Stale(aroundHim, centre, range, now))
            {
                aroundHim = Make(centre, range, now);
                ForgetOld();
            }
            return aroundHim;
        }

        /// <summary>Whether a station's own queue and output are inside the count its feed uses (code review m1): around
        /// each station always; around him only within the count range of where it was made. A station outside it gets
        /// nothing for a product with a maximum.</summary>
        internal static bool Covers(Tally tally, Fed fed) =>
            tally != null && (Settings.CountWhere.Value == CountAround.AroundEachStation ||
                              (fed.Position - tally.Centre).sqrMagnitude <= tally.Range * tally.Range);

        /// <summary>The chests within <paramref name="range"/> of him a light's feed may take from, nearest first, found
        /// with the count and kept as long (m5): each feed then checks only whether its game may change them now.</summary>
        internal static List<Container> ChestsNearHim(float range)
        {
            Player player = Player.m_localPlayer;
            Vector3 centre = player != null ? player.transform.position : Vector3.zero;
            float now = Time.time;
            if (chestsNearHim == null || now - chestsAt >= Settings.CountEvery.Value || now < chestsAt || chestsRange != range ||
                chestsVersion != Settings.Version || (chestsCentre - centre).sqrMagnitude > MovedFar * MovedFar)
            {
                chestsNearHim = ChestFinder.Find(centre, Settings.Rules(range));
                chestsAt = now;
                chestsRange = range;
                chestsVersion = Settings.Version;
                chestsCentre = centre;
            }
            return chestsNearHim;
        }

        /// <summary>A feed took and queued (Feeding.Count).</summary>
        internal static void Took(string input, int taken, string product, int products) => Ledger.Note(input, taken, product, products);

        /// <summary>A fermenter was tapped: its meads count until they can have landed and been put away (m2).</summary>
        internal static void NoteTap(string product, int count) => Tapped.Note(product, count, Time.time);

        /// <summary>Meads of a product tapped a moment ago, on their way to the ground.</summary>
        internal static int TappedNow(string product) =>
            Tapped.Pending(product, Time.time, TapLands + Settings.WaitBeforePutting.Value);

        /// <summary>Everything counted is forgotten (switched off, an error, a new world).</summary>
        internal static void Forget()
        {
            aroundHim = null;
            AroundStations.Clear();
            chestsNearHim = null;
            Ledger.Clear();
            Tapped.Clear();
        }

        private static readonly List<(long Mark, float At)> CountMarks = new List<(long, float)>();
        private static readonly List<Fed> Dropped = new List<Fed>();

        // The ledger keeps only what a count still in use may read, and taps only while they count. A count too old to be
        // used again is made afresh before its next use, so it's dropped first: an old count around a station no longer
        // fed (left alone, out of range, gone), or around him with every light off, would otherwise keep every later entry
        // all session (code review n18).
        private static void ForgetOld()
        {
            float now = Time.time;
            float every = Settings.CountEvery.Value;
            CountMarks.Clear();
            if (aroundHim != null)
            {
                CountMarks.Add((aroundHim.Mark, aroundHim.At));
                if (!CountAge.InUse(aroundHim.At, now, every))
                {
                    aroundHim = null;
                }
            }
            Dropped.Clear();
            foreach (KeyValuePair<Fed, Tally> pair in AroundStations)
            {
                CountMarks.Add((pair.Value.Mark, pair.Value.At));
                if (!CountAge.InUse(pair.Value.At, now, every))
                {
                    Dropped.Add(pair.Key);
                }
            }
            foreach (Fed fed in Dropped)
            {
                AroundStations.Remove(fed);
            }
            Dropped.Clear();
            long oldest = CountAge.OldestInUse(CountMarks, now, every);
            CountMarks.Clear();
            if (oldest != long.MaxValue)
            {
                Ledger.ForgetUpTo(oldest);
            }
            Tapped.Forget(Time.time, TapLands + Settings.WaitBeforePutting.Value);
        }

        // Older than the setting, made with other settings or another range, or (around him) made far from here.
        private static bool Stale(Tally tally, Vector3 centre, float range, float now) =>
            tally == null || !CountAge.InUse(tally.At, now, Settings.CountEvery.Value) || tally.SettingsVersion != Settings.Version ||
            tally.Range != range || (tally.Centre - centre).sqrMagnitude > MovedFar * MovedFar;

        private static Tally Make(Vector3 centre, float range, float now)
        {
            var tally = new Tally { At = now, Centre = centre, Range = range, SettingsVersion = Settings.Version, Mark = Ledger.Mark() };
            int worldLevel = Game.m_worldLevel;
            foreach (Container chest in ChestFinder.Find(centre, Settings.CountRules(range), withReserved: Settings.CountReserved.Value))
            {
                Inventory inventory = chest.GetInventory();
                if (inventory == null)
                {
                    continue;
                }
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (item?.m_shared != null && item.m_worldLevel >= worldLevel)
                    {
                        Tally.Add(tally.Held, item.m_shared.m_name, item.m_stack);
                    }
                }
            }
            Player player = Player.m_localPlayer;
            if (Settings.CountCarried.Value && player != null)
            {
                foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                {
                    if (item?.m_shared != null && item.m_worldLevel >= worldLevel)
                    {
                        Tally.Add(tally.Held, item.m_shared.m_name, item.m_stack);
                    }
                }
            }
            if (Settings.CountStations.Value)
            {
                float most = range * range;
                foreach (Fed fed in Registry.All)
                {
                    if (fed.Alive && !fed.IsLight && (fed.Position - centre).sqrMagnitude <= most)
                    {
                        Stations.CountMade(fed, tally.Made);
                    }
                }
            }
            return tally;
        }
    }
}
