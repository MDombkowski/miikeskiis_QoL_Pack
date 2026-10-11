using System.Collections.Generic;

namespace ComfortBuild
{
    /// <summary>One comfort piece as the count needs it: plain values, so the rule can be tested without the game.</summary>
    internal readonly struct Thing
    {
        /// <summary>The piece's name as the game compares it (Piece.m_name, a "$" token): a second piece of the same
        /// name next to the first in the count adds nothing.</summary>
        internal readonly string Name;

        /// <summary>Its comfort group (Piece.m_comfortGroup as a number): 0 is None, no group.</summary>
        internal readonly int Group;

        /// <summary>What it gives: Piece.GetComfort() for a piece standing here (0 while a fire is out), its own
        /// Piece.m_comfort for a piece in the build menu.</summary>
        internal readonly int Comfort;

        internal Thing(string name, int group, int comfort)
        {
            Name = name ?? string.Empty;
            Group = group;
            Comfort = comfort;
        }
    }

    /// <summary>
    /// The game's own comfort rule, as plain code (SE_Rested.CalculateComfortLevel of Valheim 1.0.16, read from the
    /// installed game on 2026-09-30). Sheltered, comfort is 2 plus what the comfort pieces within 10 m add: they are
    /// sorted by group, then by what they give (highest first), then by name (the game's string order, reversed), and
    /// each adds what it gives unless the one before it in that order is in the same group (for every group but None) or
    /// has the same name. So only the best piece of each group counts, and a piece with no group counts once per name.
    /// Unsheltered, comfort is 1 whatever stands around. Nothing here reads the game.
    /// </summary>
    internal static class ComfortMath
    {
        /// <summary>Comfort Piece.ComfortGroup.None: no group.</summary>
        internal const int NoGroup = 0;

        /// <summary>The base when sheltered: 1, and 1 more for the shelter.</summary>
        internal const int ShelteredBase = 2;

        /// <summary>Comfort unsheltered: always 1.</summary>
        internal const int UnshelteredComfort = 1;

        /// <summary>The game's order (SE_Rested.PieceComfortSort).</summary>
        internal static int GameOrder(Thing x, Thing y)
        {
            if (x.Group != y.Group)
            {
                return x.Group.CompareTo(y.Group);
            }
            if (x.Comfort != y.Comfort)
            {
                return y.Comfort.CompareTo(x.Comfort);
            }
            return y.Name.CompareTo(x.Name);
        }

        /// <summary>What these pieces add together, above the sheltered base, by the game's rule.</summary>
        internal static int Sum(IEnumerable<Thing> things) => Walk(things, null);

        /// <summary>What these pieces add, and which of them count (their places in <paramref name="things"/>'s own
        /// order), by the game's rule.</summary>
        internal static int Sum(IList<Thing> things, List<int> counting) => Walk(things, counting);

        /// <summary>Comfort where he stands, sheltered or not.</summary>
        internal static int Level(IEnumerable<Thing> near, bool sheltered) => sheltered ? ShelteredBase + Sum(near) : UnshelteredComfort;

        /// <summary>What one more piece would add to these, by the game's rule (never less than 0).</summary>
        internal static int Gain(IList<Thing> near, Thing candidate)
        {
            var with = new List<Thing>(near.Count + 1);
            with.AddRange(near);
            with.Add(candidate);
            int gain = Sum(with) - Sum(near);
            return gain > 0 ? gain : 0;
        }

        /// <summary>What the pieces of one group add (for group None, every piece without a group).</summary>
        internal static int SumOfGroup(IEnumerable<Thing> things, int group)
        {
            var of = new List<Thing>();
            foreach (Thing thing in things)
            {
                if (thing.Group == group)
                {
                    of.Add(thing);
                }
            }
            return Sum(of);
        }

        /// <summary>
        /// The piece that keeps <paramref name="candidate"/> from adding its whole value here: in its group, the one that
        /// counts; with no group, one of the same name that counts. Its place in <paramref name="near"/>, or -1 when
        /// nothing stands in its way.
        /// </summary>
        internal static int InTheWay(IList<Thing> near, Thing candidate)
        {
            var counting = new List<int>();
            Sum(near, counting);
            foreach (int at in counting)
            {
                Thing thing = near[at];
                if (candidate.Group != NoGroup ? thing.Group == candidate.Group : thing.Group == NoGroup && thing.Name == candidate.Name)
                {
                    return at;
                }
            }
            return -1;
        }

        // The walk itself, on a sorted copy (the game sorts its own list in place; the caller's list is left alone).
        private static int Walk(IEnumerable<Thing> things, List<int> counting)
        {
            var order = new List<KeyValuePair<Thing, int>>();
            int place = 0;
            foreach (Thing thing in things)
            {
                order.Add(new KeyValuePair<Thing, int>(thing, place++));
            }
            // The game's sort is not stable, but pieces it calls equal (same group, value and name) count the same.
            order.Sort((a, b) => GameOrder(a.Key, b.Key));
            int sum = 0;
            for (int i = 0; i < order.Count; i++)
            {
                Thing thing = order[i].Key;
                if (i > 0)
                {
                    Thing before = order[i - 1].Key;
                    if ((thing.Group != NoGroup && thing.Group == before.Group) || thing.Name == before.Name)
                    {
                        continue;
                    }
                }
                sum += thing.Comfort;
                counting?.Add(order[i].Value);
            }
            return sum;
        }
    }
}
