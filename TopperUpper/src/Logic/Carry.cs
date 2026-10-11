using System;
using System.Collections.Generic;

namespace TopperUpper.Logic
{
    /// <summary>What one feed gave one of its lanes.</summary>
    internal sealed class Given
    {
        internal Grant Grant;
        internal int Amount;
        internal int Products;
    }

    /// <summary>The kinds of thing a feed writes into a station, each with the game's own effect once per feed.</summary>
    internal enum AddKind
    {
        Ore,
        Fuel,
        Ammo,
    }

    /// <summary>
    /// Carrying out a feed's plan (code review M1): each lane's units are taken one at a time, then handed to the station
    /// once for the whole lane: as its owner, written straight into its record as the game's own add writes it (the
    /// queue entry by entry, the fuel as one sum), so a top-up costs no log line per item and one added effect per feed
    /// (WriteBook); for a friend's game, as many of the station's own messages, as a hand's presses would be. What was
    /// taken is handed over in a finally, so a take that throws part-way (another mod's handler of the chest's change)
    /// still delivers what left the chests before it. Plain values, for the harness.
    /// </summary>
    internal static class Carry
    {
        /// <summary>Takes up to each grant's amount one at a time (<paramref name="takeOne"/>: one of an input out of the
        /// chests, false when they have none), then calls <paramref name="add"/> once per grant with what was taken.
        /// Returns what was given.</summary>
        internal static List<Given> Out(IList<Grant> grants, Func<string, bool> takeOne, Action<Grant, int> add)
        {
            var given = new List<Given>();
            foreach (Grant grant in grants)
            {
                int amount = 0;
                try
                {
                    while (amount < grant.Amount && takeOne(grant.Input))
                    {
                        amount++;
                    }
                }
                finally
                {
                    if (amount > 0)
                    {
                        given.Add(new Given { Grant = grant, Amount = amount, Products = grant.ProductsOf(amount) });
                        add(grant, amount);
                    }
                }
            }
            return given;
        }
    }

    /// <summary>
    /// What one feed wrote straight into a station, so the game's added effect plays once per kind and feed, as one press
    /// of a hand plays it, however many units went in.
    /// </summary>
    internal sealed class WriteBook
    {
        private readonly Dictionary<AddKind, int> written = new Dictionary<AddKind, int>();

        internal void Wrote(AddKind kind, int units)
        {
            if (units <= 0)
            {
                return;
            }
            written.TryGetValue(kind, out int sum);
            written[kind] = sum + units;
        }

        internal int Units(AddKind kind) => written.TryGetValue(kind, out int sum) ? sum : 0;

        /// <summary>The effects the feed owes: one for each kind it wrote anything of.</summary>
        internal List<AddKind> EffectsOwed()
        {
            var owed = new List<AddKind>();
            foreach (AddKind kind in new[] { AddKind.Ore, AddKind.Fuel, AddKind.Ammo })
            {
                if (Units(kind) > 0)
                {
                    owed.Add(kind);
                }
            }
            return owed;
        }
    }

    /// <summary>
    /// What every feed has taken and queued since each count was made (code review m6): one ledger for lights and
    /// stations, every count reading it, so takes counted against one count (around him, or around a station) are taken
    /// off every other count too, and no two feeds together go below a reserve or past a maximum within the count's few
    /// seconds. Entries are numbered in the order they happen; a count remembers the number it was made at and reads
    /// only what came after. Plain values, for the harness.
    /// </summary>
    internal sealed class Ledger
    {
        private readonly List<Entry> entries = new List<Entry>();
        private long next = 1;

        /// <summary>A number for a count being made now: what's noted after it isn't in that count.</summary>
        internal long Mark() => next++;

        /// <summary>A feed took <paramref name="taken"/> of an input out of the chests and queued <paramref name="products"/>
        /// of a product.</summary>
        internal void Note(string input, int taken, string product, int products)
        {
            entries.Add(new Entry(next++, input, Math.Max(0, taken), product, Math.Max(0, products)));
        }

        /// <summary>How many of an item were taken after the count numbered <paramref name="since"/> was made.</summary>
        internal int TakenSince(string item, long since)
        {
            int sum = 0;
            foreach (Entry entry in entries)
            {
                if (entry.Number > since && entry.Input == item)
                {
                    sum += entry.Taken;
                }
            }
            return sum;
        }

        /// <summary>How many of a product were queued after the count numbered <paramref name="since"/> was made.</summary>
        internal int QueuedSince(string product, long since)
        {
            int sum = 0;
            foreach (Entry entry in entries)
            {
                if (entry.Number > since && entry.Product == product)
                {
                    sum += entry.Products;
                }
            }
            return sum;
        }

        /// <summary>Forgets what no count still in use can need: every entry at or before the oldest such count.</summary>
        internal void ForgetUpTo(long oldestInUse) => entries.RemoveAll(entry => entry.Number <= oldestInUse);

        internal int Count => entries.Count;

        internal void Clear() => entries.Clear();

        private readonly struct Entry
        {
            internal readonly long Number;
            internal readonly string Input;
            internal readonly int Taken;
            internal readonly string Product;
            internal readonly int Products;

            internal Entry(long number, string input, int taken, string product, int products)
            {
                Number = number;
                Input = input;
                Taken = taken;
                Product = product;
                Products = products;
            }
        }
    }

    /// <summary>
    /// How long a count is used (the setting "Count the chests at most every"): from when it was made until that many
    /// seconds have passed, and never one made "after" now (a new session's clock). Counts.Stale asks it before a count is
    /// used, and the ledger's trimming before it keeps a count's entries (code review n18), so the two never disagree.
    /// Plain values, for the harness.
    /// </summary>
    internal static class CountAge
    {
        internal static bool InUse(float madeAt, float now, float every) => now >= madeAt && now - madeAt < every;

        /// <summary>The oldest mark among the counts that can still be used: a count too old is made afresh before its next
        /// use, so its old mark mustn't keep every entry after it (code review n18). long.MaxValue when none can.</summary>
        internal static long OldestInUse(IEnumerable<(long Mark, float At)> counts, float now, float every)
        {
            long oldest = long.MaxValue;
            foreach ((long mark, float at) in counts)
            {
                if (InUse(at, now, every))
                {
                    oldest = Math.Min(oldest, mark);
                }
            }
            return oldest;
        }
    }

    /// <summary>
    /// Whether a light's or station's owner has already caught up the time it was away (code review n1 and n17). A fire's
    /// owner burns, at its first tick, the whole time since its record's s_lastTime (Fireplace.UpdateFireplace, every 2 s);
    /// an oven's or a Frost Foundry's burns its fuel and cooks every slot for the whole time since its s_startTime
    /// (CookingStation.UpdateCooking and GetDeltaTime, every second, uncapped). Fuel added before that tick would burn for
    /// the time nobody was there, so a feed waits until the record was written a moment ago. An unset time counts no time
    /// at the game's first tick: a cooking station may be fed then; a fire waits for its first tick (as built for n1).
    /// Ticks as ZNet's time writes them. Plain values, for the harness.
    /// </summary>
    internal static class CatchUp
    {
        internal static bool Done(long lastTicks, long nowTicks, double withinSeconds, bool unsetCountsNoTime)
        {
            if (lastTicks == 0L)
            {
                return unsetCountsNoTime;
            }
            double seconds = (nowTicks - lastTicks) / (double)TimeSpan.TicksPerSecond;
            return Math.Abs(seconds) <= withinSeconds;
        }
    }

    /// <summary>
    /// Meads on their way out of a tapped fermenter (code review m2): its content clears at the tap and its meads land at
    /// its output a moment later (Fermenter.RPC_Tap, DelayedTap 1.5 s), so for that moment they are in no count. Each tap is
    /// noted, and counted toward the maximum until it can have landed and been put away. Plain values, for the harness.
    /// </summary>
    internal sealed class Taps
    {
        private readonly List<(string Product, int Count, float At)> taps = new List<(string, int, float)>();

        internal void Note(string product, int count, float at) => taps.Add((product, count, at));

        /// <summary>The meads of a product tapped less than <paramref name="window"/> seconds ago.</summary>
        internal int Pending(string product, float now, float window)
        {
            int sum = 0;
            foreach ((string each, int count, float at) in taps)
            {
                if (each == product && now - at < window && now >= at)
                {
                    sum += count;
                }
            }
            return sum;
        }

        /// <summary>Forgets taps older than the window.</summary>
        internal void Forget(float now, float window) => taps.RemoveAll(tap => now - tap.At >= window || now < tap.At);

        internal void Clear() => taps.Clear();
    }

    /// <summary>
    /// Whether a chest may be taken from or put into by this mod, beside the chest service's own rules (code review M2 and
    /// m4): never the Obliterator's container (whatever goes in is destroyed at its next pull), never a chest kept for a
    /// craft whose bar is running (Craft from Chests), never one this game may not change now, and a reserved one only as
    /// the settings say. Plain values, for the harness.
    /// </summary>
    internal static class ChestUse
    {
        /// <summary>Why not, in words after "the chest is"; null when it may be used.</summary>
        internal static string WhyNot(bool mayChange, bool obliterator, bool keptForCraft, bool reserved, bool useReserved)
        {
            if (obliterator)
            {
                return "the Obliterator";
            }
            if (!mayChange)
            {
                return "not your game's to change now";
            }
            if (keptForCraft)
            {
                return "kept for a craft";
            }
            if (reserved && !useReserved)
            {
                return "reserved";
            }
            return null;
        }
    }

    /// <summary>What a press of the game's Use on a chest does while a station is being linked.</summary>
    internal enum LinkPress
    {
        None,
        Input,
        Output,
    }

    internal static class LinkPresses
    {
        /// <summary>
        /// What a press does: nothing without the Use press, nothing while Alt or Ctrl is held (Reserve Stock's Left Alt + E
        /// on a chest, or another mod's key, is never also a link: code review m3), an output with the game's own "alt"
        /// (Left Shift, or the gamepad's), else an input.
        /// </summary>
        internal static LinkPress Of(bool useDown, bool altHeld, bool ctrlHeld, bool gameAlt)
        {
            if (!useDown || altHeld || ctrlHeld)
            {
                return LinkPress.None;
            }
            return gameAlt ? LinkPress.Output : LinkPress.Input;
        }
    }
}
