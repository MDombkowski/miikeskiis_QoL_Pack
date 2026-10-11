using System;
using System.Collections.Generic;

namespace Hoarstone.Shapes
{
    /// <summary>One settlement found among the pieces: its pieces, whether a Hoarstone stands in it, whether it counts,
    /// and once worked out, its shape.</summary>
    internal sealed class Settlement
    {
        /// <summary>Its pieces, as places in the list the settlements were found in.</summary>
        internal readonly List<int> Members = new List<int>();

        internal bool HasStone;

        /// <summary>Whether it counts: big enough, or holding a Hoarstone (with the Hoarstone's own rules).</summary>
        internal bool Kept;

        /// <summary>Its tile layer: <see cref="Tiles.StoneLayer"/> with a Hoarstone, <see cref="Tiles.OtherLayer"/> without.</summary>
        internal int Layer => HasStone ? Tiles.StoneLayer : Tiles.OtherLayer;

        /// <summary>The box around its pieces, in metres.</summary>
        internal double MinX = double.PositiveInfinity, MinZ = double.PositiveInfinity, MaxX = double.NegativeInfinity, MaxZ = double.NegativeInfinity;

        internal ShapeGrid Shape;

        /// <summary>What its shape was worked out from (its pieces, the options, the land), so an unchanged settlement
        /// keeps its shape.</summary>
        internal string Signature;

        internal void Include(ShapePoint p)
        {
            if (p.X < MinX) MinX = p.X;
            if (p.Z < MinZ) MinZ = p.Z;
            if (p.X > MaxX) MaxX = p.X;
            if (p.Z > MaxZ) MaxZ = p.Z;
        }
    }

    /// <summary>
    /// Settlements from pieces, and what a shape reaches: the lab's compute(), split so the mod can work out only the
    /// settlements that changed. Pure arithmetic, safe on a worker thread.
    /// </summary>
    internal static class Settlements
    {
        /// <summary>Sorts the pieces into settlements and says which count. With the lab's rules
        /// (<see cref="ShapeOptions.StoneRules"/> off) a settlement counts when it has at least
        /// <see cref="ShapeOptions.MinSize"/> pieces; with the Hoarstone's, one holding a Hoarstone counts whatever its
        /// size.</summary>
        internal static List<Settlement> Find(IList<ShapePoint> pts, ShapeOptions o)
        {
            Clusters groups = ShapeMath.Cluster(pts, o.Eps, o.MinPts, o.StoneRules);
            var found = new List<Settlement>(groups.Count);
            for (int g = 0; g < groups.Count; g++)
            {
                found.Add(new Settlement());
            }
            for (int i = 0; i < pts.Count; i++)
            {
                int g = groups.Label[i];
                if (g < 0)
                {
                    continue;
                }
                Settlement s = found[g];
                s.Members.Add(i);
                s.Include(pts[i]);
                if (pts[i].Stone)
                {
                    s.HasStone = true;
                }
            }
            foreach (Settlement s in found)
            {
                s.Kept = s.Members.Count >= o.MinSize || (o.StoneRules && s.HasStone);
            }
            return found;
        }

        /// <summary>How far a shape reaches past its pieces' box: half the gap, the margin, the shore margin and three
        /// squares (the lab's pad).</summary>
        internal static double Pad(ShapeOptions o) => o.Gap / 2 + o.Margin + o.Shore + 3 * o.Cell;

        /// <summary>The pieces of one settlement, in the order they were found.</summary>
        internal static List<ShapePoint> PointsOf(Settlement s, IList<ShapePoint> pts)
        {
            var list = new List<ShapePoint>(s.Members.Count);
            foreach (int i in s.Members)
            {
                list.Add(pts[i]);
            }
            return list;
        }

        /// <summary>What a settlement's shape depends on, as text: its pieces in order, and the options. The land is
        /// added by the caller, who knows which zones it read.</summary>
        internal static string PiecesSignature(Settlement s, IList<ShapePoint> pts, ShapeOptions o)
        {
            unchecked
            {
                long hash = 1469598103934665603L;
                foreach (int i in s.Members)
                {
                    ShapePoint p = pts[i];
                    hash = (hash ^ BitConverter.DoubleToInt64Bits(p.X)) * 1099511628211L;
                    hash = (hash ^ BitConverter.DoubleToInt64Bits(p.Z)) * 1099511628211L;
                    hash = (hash ^ (p.Stone ? 1 : 0)) * 1099511628211L;
                }
                return $"{s.Members.Count}:{hash:X16}:{o.Signature()}";
            }
        }
    }
}
