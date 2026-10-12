using System;
using System.Collections.Generic;

namespace ZFighter
{
    /// <summary>The setting "Which one shows on top": which of two pieces laid into each other is drawn over the other
    /// where their surfaces meet.</summary>
    internal enum OnTop
    {
        /// <summary>The smaller piece (a 1x1 floor laid into a 2x2 shows), by the size of its drawing.</summary>
        SmallerPiece,

        /// <summary>The bigger piece.</summary>
        BiggerPiece,

        /// <summary>By kind: beams and poles over roofs, roofs over walls, walls over floors (Kinds.Rank).</summary>
        ByKind,

        /// <summary>No preference: each overlap is still kept apart, the one on top picked by the pieces' places.</summary>
        NoPreference,
    }

    /// <summary>
    /// One piece as the level rule sees it. Plain data, built by the game's side from a piece (Tracker) and by the
    /// scratch harness from numbers.
    /// </summary>
    internal sealed class LevelNode
    {
        /// <summary>Its level is fixed (0: drawn as the game draws it): a piece left alone (by a list, or built by someone
        /// else with "only mine"), or one with nothing Z-Fighter may move. It still counts for its neighbours.</summary>
        internal bool Fixed;

        /// <summary>The volume of its drawing's box, in cubic metres.</summary>
        internal double Volume;

        /// <summary>Its kind's rank (Kinds.Rank): higher shows on top with "by kind".</summary>
        internal int Kind;

        /// <summary>Where it stands, for an order that is the same at every load: its saved place, in millimetres, then
        /// its prefab's name and its turn.</summary>
        internal PlaceKey Key;

        /// <summary>The indices (in the list given to Levels.Assign) of the pieces laid into this one.</summary>
        internal readonly List<int> Neighbours = new List<int>();
    }

    /// <summary>Where a piece stands, compared field by field: an order for pieces that is the same at every load and
    /// on every computer, since a piece's saved place and turn never change.</summary>
    internal readonly struct PlaceKey : IComparable<PlaceKey>
    {
        internal readonly long X;
        internal readonly long Y;
        internal readonly long Z;
        internal readonly string Name;
        internal readonly long Turn;

        internal PlaceKey(long x, long y, long z, string name, long turn)
        {
            X = x;
            Y = y;
            Z = z;
            Name = name ?? string.Empty;
            Turn = turn;
        }

        public int CompareTo(PlaceKey other)
        {
            int c = X.CompareTo(other.X);
            if (c != 0) return c;
            c = Y.CompareTo(other.Y);
            if (c != 0) return c;
            c = Z.CompareTo(other.Z);
            if (c != 0) return c;
            c = string.CompareOrdinal(Name, other.Name);
            if (c != 0) return c;
            return Turn.CompareTo(other.Turn);
        }
    }

    /// <summary>
    /// Gives each piece of a group laid into each other a level: 0 is drawn as the game draws it, and each level up is
    /// drawn one nudge bigger. Two pieces laid into each other never share a level, so their surfaces never meet at one
    /// depth; and where the rule says one should show over the other, it gets the higher level. Plain code: the scratch
    /// harness runs it.
    /// </summary>
    internal static class Levels
    {
        /// <summary>The highest level given. A piece whose neighbours fill every level up to it shares the top one with
        /// one of them (logged); it takes about a dozen pieces laid into one to get there.</summary>
        internal const int Highest = 10;

        /// <summary>How much smaller a piece's drawing must be to count as the smaller of two (a quarter), so pieces
        /// of nearly one size are only kept apart, not ordered.</summary>
        internal const double SizeStep = 1.25;

        /// <summary>
        /// The levels for a group of pieces, the same for the same pieces however they loaded. Fixed pieces keep 0.
        /// The others are taken in the rule's order (the ones that should be under first, ties by place), each at the
        /// lowest level that is above every neighbour it should show over and that no neighbour already has.
        /// <paramref name="cappedCount"/> counts the pieces that had to share the highest level.
        /// </summary>
        internal static int[] Assign(IList<LevelNode> nodes, OnTop rule, out int cappedCount)
        {
            cappedCount = 0;
            int n = nodes.Count;
            var levels = new int[n];
            var placed = new bool[n];
            for (int i = 0; i < n; i++)
            {
                if (nodes[i].Fixed)
                {
                    placed[i] = true;   // level 0, there from the start for everyone's rules
                }
            }

            var order = new List<int>();
            for (int i = 0; i < n; i++)
            {
                if (!nodes[i].Fixed)
                {
                    order.Add(i);
                }
            }
            order.Sort((a, b) => CompareForOrder(nodes[a], nodes[b], rule));

            var used = new HashSet<int>();
            foreach (int p in order)
            {
                used.Clear();
                int lowest = 0;
                foreach (int q in nodes[p].Neighbours)
                {
                    if (q == p || q < 0 || q >= n || !placed[q])
                    {
                        continue;
                    }
                    used.Add(levels[q]);
                    if (ShowsOver(nodes[p], nodes[q], rule))
                    {
                        lowest = Math.Max(lowest, levels[q] + 1);
                    }
                }
                int level = lowest;
                while (used.Contains(level))
                {
                    level++;
                }
                if (level > Highest)
                {
                    level = Highest;
                    cappedCount++;
                }
                levels[p] = level;
                placed[p] = true;
            }
            return levels;
        }

        /// <summary>Whether, by the rule, <paramref name="p"/> should show over <paramref name="q"/> where they meet.
        /// Never both ways round, and never in a circle, so the order below always takes the one under first.</summary>
        internal static bool ShowsOver(LevelNode p, LevelNode q, OnTop rule)
        {
            switch (rule)
            {
                case OnTop.SmallerPiece:
                    return p.Volume * SizeStep < q.Volume;
                case OnTop.BiggerPiece:
                    return q.Volume * SizeStep < p.Volume;
                case OnTop.ByKind:
                    return p.Kind > q.Kind;
                default:
                    return false;
            }
        }

        /// <summary>The order pieces are taken in: the ones that should be under come first (the biggest first with
        /// "smaller piece", the smallest first with "bigger piece", the lowest kind first with "by kind"), then by place.
        /// So when a piece is taken, every neighbour it should show over already has its level.</summary>
        private static int CompareForOrder(LevelNode a, LevelNode b, OnTop rule)
        {
            int c = 0;
            switch (rule)
            {
                case OnTop.SmallerPiece:
                    c = b.Volume.CompareTo(a.Volume);
                    break;
                case OnTop.BiggerPiece:
                    c = a.Volume.CompareTo(b.Volume);
                    break;
                case OnTop.ByKind:
                    c = a.Kind.CompareTo(b.Kind);
                    break;
            }
            return c != 0 ? c : a.Key.CompareTo(b.Key);
        }
    }
}
