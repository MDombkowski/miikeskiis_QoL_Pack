using System;
using System.Collections.Generic;

namespace TopperUpper.Logic
{
    /// <summary>
    /// A take, and what left the chest by world level (the catch-up's takes, review F3 and round two's minor). When the
    /// take returns, its own count is how many left (ChestStock.Remove's, exact whatever another mod did to the same item
    /// in that chest meanwhile); only when it throws part way (another mod's handler of the chest's change) is what the
    /// chest held before and after measured instead, as ChestPut.Transfer measures a put with CountSame, and never more
    /// than was asked for nor less than none. Either way the world levels come from the measure, trimmed to that count.
    /// Plain values, for the harness.
    /// </summary>
    internal static class Measure
    {
        /// <summary>Runs <paramref name="take"/> (at most <paramref name="want"/>, returning how many it took); then,
        /// whatever happened, tells <paramref name="left"/> how many of each world level left, <paramref name="count"/>
        /// counting what the chest holds before and after (world level to amount). Returns how many left.</summary>
        internal static int Take(int want, Func<IDictionary<int, int>> count, Func<int> take, Action<int, int> left)
        {
            want = Math.Max(0, want);
            IDictionary<int, int> before = count() ?? new Dictionary<int, int>();
            int said = 0;
            bool returned = false;
            int total = 0;
            try
            {
                said = take();
                returned = true;
            }
            finally
            {
                IDictionary<int, int> after = count();
                total = Math.Max(0, Math.Min(want, returned ? said : Gone(before, after)));
                foreach (KeyValuePair<int, int> each in Split(before, after, total))
                {
                    left(each.Key, each.Value);
                }
            }
            return total;
        }

        // What the measure saw leave the chest: each world level's fall, a level that grew counting nothing.
        private static int Gone(IDictionary<int, int> before, IDictionary<int, int> after)
        {
            int gone = 0;
            foreach (KeyValuePair<int, int> each in before)
            {
                gone += Math.Max(0, each.Value - Held(after, each.Key));
            }
            return gone;
        }

        /// <summary>
        /// <paramref name="total"/> split by world level: what the measure saw leave each level first (in the order the
        /// chest held them), then anything it missed (the same item put in meanwhile hid the fall) at the levels that fell,
        /// then at the others the chest held, each up to what it held, and any rest at the first level it held (or 0). Sums
        /// to <paramref name="total"/>.
        /// </summary>
        internal static List<KeyValuePair<int, int>> Split(IDictionary<int, int> before, IDictionary<int, int> after, int total)
        {
            var amounts = new Dictionary<int, int>();
            var order = new List<int>();
            int rest = Math.Max(0, total);
            void Add(int level, int amount)
            {
                if (!amounts.ContainsKey(level))
                {
                    amounts[level] = 0;
                    order.Add(level);
                }
                amounts[level] += amount;
                rest -= amount;
            }
            foreach (KeyValuePair<int, int> each in before)
            {
                int fell = Math.Min(rest, each.Value - Held(after, each.Key));
                if (fell > 0)
                {
                    Add(each.Key, fell);
                }
            }
            foreach (int level in order.ToArray())
            {
                int more = Math.Min(rest, Held(before, level) - amounts[level]);
                if (more > 0)
                {
                    Add(level, more);
                }
            }
            foreach (KeyValuePair<int, int> each in before)
            {
                int more = Math.Min(rest, each.Value - Held(amounts, each.Key));
                if (more > 0)
                {
                    Add(each.Key, more);
                }
            }
            if (rest > 0)
            {
                int first = 0;
                foreach (KeyValuePair<int, int> each in before)
                {
                    first = each.Key;
                    break;
                }
                Add(first, rest);
            }
            var list = new List<KeyValuePair<int, int>>();
            foreach (int level in order)
            {
                list.Add(new KeyValuePair<int, int>(level, amounts[level]));
            }
            return list;
        }

        private static int Held(IDictionary<int, int> levels, int level) =>
            levels != null && levels.TryGetValue(level, out int value) ? value : 0;
    }

    /// <summary>
    /// At most one catch-up a frame (review F4): stations loaded together all reach their first update in the same frame,
    /// and a day's catch-up costs a few milliseconds each; the rest wait their turn, one frame each. Plain values.
    /// </summary>
    internal sealed class OnePerFrame
    {
        private int last = int.MinValue;

        /// <summary>True once per frame: the first asker in a frame runs.</summary>
        internal bool Try(int frame)
        {
            if (frame == last)
            {
                return false;
            }
            last = frame;
            return true;
        }
    }

    /// <summary>
    /// When a station's catch-up may still change its mind (review F2): the server pack's "everyone" or "nobody" decides
    /// over his own settings, but its answer comes only once he's in the world and has said hello. So, while he follows
    /// the server pack's settings, on a server, and its word may still come (his character not in the world yet, or the
    /// first hello said under AnswerMayCome seconds ago and no answer yet), a station the rules would leave out waits
    /// rather than settles, and Topper-Upper's own feeding leaves it alone meanwhile (Pending). Plain values.
    /// </summary>
    internal static class Hearing
    {
        /// <summary>Whether the server pack's word on the catch-up may still come.</summary>
        internal static bool MayStillCome(bool follow, bool onServer, bool inWorld, bool answered, bool helloSaid, float sinceFirstHello, float answerMayCome) =>
            follow && onServer && !answered && (!inWorld || !helloSaid || sinceFirstHello < answerMayCome);

        /// <summary>
        /// Whether a station's catch-up may still come, so Topper-Upper's own feeding, tapping and put-away leave it alone:
        /// not settled, this game's, loaded under <paramref name="pendingAtMost"/> seconds ago, and either the rules have it
        /// catch up or the server pack's word may still come.
        /// </summary>
        internal static bool Pending(bool settled, bool mine, float sinceLoaded, float pendingAtMost, bool rulesHaveIt, bool mayStillCome) =>
            !settled && mine && sinceLoaded <= pendingAtMost && (rulesHaveIt || mayStillCome);
    }
}
