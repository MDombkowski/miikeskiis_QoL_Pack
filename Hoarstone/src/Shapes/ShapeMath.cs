using System;
using System.Collections.Generic;

namespace Hoarstone.Shapes
{
    /// <summary>One piece as the shape sees it: where it stands on the ground (x and z, metres), and whether it's a
    /// Hoarstone.</summary>
    internal struct ShapePoint
    {
        internal double X;
        internal double Z;
        internal bool Stone;

        internal ShapePoint(double x, double z, bool stone)
        {
            X = x;
            Z = z;
            Stone = stone;
        }
    }

    /// <summary>The settings a shape is worked out with, in metres and pieces: the lab's sliders.</summary>
    internal sealed class ShapeOptions
    {
        /// <summary>Pieces this close belong together (DBSCAN's Eps).</summary>
        internal double Eps = 24;

        /// <summary>Neighbours a piece needs to pass that on (DBSCAN's MinPts, the piece itself included).</summary>
        internal int MinPts = 4;

        /// <summary>Fewer pieces than this, and it isn't a settlement.</summary>
        internal int MinSize = 8;

        /// <summary>Gaps narrower than this are closed; every piece grows by half of it, then shrinks back.</summary>
        internal double Gap = 30;

        internal double Margin = 8;

        internal double Shore = 20;

        /// <summary>The square size: 1, 2 or 4 m (each divides 32, so squares line up with the game's zones).</summary>
        internal double Cell = 2;

        /// <summary>The Hoarstone's own rules (decision 3 of the plan): a Hoarstone is always a core piece, and a
        /// settlement holding one has no smallest size. Off, the rules are the lab's exactly.</summary>
        internal bool StoneRules = true;

        internal ShapeOptions Copy() => (ShapeOptions)MemberwiseClone();

        /// <summary>Every value as text, so two runs can tell whether the options changed.</summary>
        internal string Signature() =>
            $"{Eps:R}|{MinPts}|{MinSize}|{Gap:R}|{Margin:R}|{Shore:R}|{Cell:R}|{StoneRules}";
    }

    /// <summary>Settlements found among the pieces: each piece's settlement number (-1 for none) and how many.</summary>
    internal sealed class Clusters
    {
        internal int[] Label;
        internal int Count;
    }

    /// <summary>A shape on its own grid of squares: square (i, j) of the grid is the world's square (X0 + i, Z0 + j),
    /// which covers x from (X0 + i) × cell to (X0 + i + 1) × cell, and z the same way.</summary>
    internal sealed class ShapeGrid
    {
        internal int X0;
        internal int Z0;
        internal int W;
        internal int H;
        internal double Cell;

        /// <summary>The finished shape: 1 where spawns are kept out.</summary>
        internal byte[] Final;

        /// <summary>The steps before it (grown, filled, shrunk, margin), kept only when asked for (the harness compares
        /// each with the lab's).</summary>
        internal byte[][] Steps;

        internal bool At(int worldI, int worldJ)
        {
            int i = worldI - X0, j = worldJ - Z0;
            return i >= 0 && j >= 0 && i < W && j < H && Final[j * W + i] != 0;
        }
    }

    /// <summary>
    /// The agreed shape method, ported line for line from the Settlement Shape Lab's own script
    /// (Mod Projects Vault\working\2026-10-10_settlement-shape-lab.html: its cluster, edt and shapeOf), so the harness can
    /// check it square for square against that script. Pure arithmetic in doubles, as the lab's JavaScript does it, in
    /// the same order: no Unity, safe on a worker thread.
    /// - Settlements: DBSCAN, on a grid of buckets the size of Eps (keyed exactly, where the lab's hash merged four
    ///   buckets around the world's centre: A88's decision 12).
    /// - The shape of one settlement, on its own grid: grow every piece by half the gap (a distance map), fill what the
    ///   spread from the grid's edge over land can't reach (the sea stops it), shrink back away from open land only,
    ///   add the margin, then the shore margin over water next to the shape's land.
    /// </summary>
    internal static class ShapeMath
    {
        /// <summary>The game's water line (ZoneSystem.m_waterLevel): ground at or above it is land.</summary>
        internal const double WaterLine = 30;

        // ---------- clustering (DBSCAN) ----------

        // A bucket's key: its two numbers, exactly. (The lab's key, i * 73856093 ^ j * 19349663 in 32 bits, gives the
        // buckets (-1, -1) and (1, 1) one key, and (-1, 1) and (1, -1) another, so a piece in bucket (0, 0), at the
        // world's centre, counted some neighbours twice: an accident of its hashing, found by the harness, not part of the
        // method. A88's decision 12.)
        private static long Key(long i, long j) => (i << 32) ^ (j & 0xFFFFFFFFL);

        /// <summary>Sorts pieces into settlements. A piece with at least <paramref name="minPts"/> pieces within
        /// <paramref name="eps"/> (itself included) is a core piece; a settlement grows only through its core pieces;
        /// the rest join the first settlement that reaches them, or none. With <paramref name="stoneCore"/>, a
        /// Hoarstone is a core piece whatever its neighbours. The lab's labels exactly (the order neighbours are met in
        /// never changes them), but no piece's neighbours are kept: a core piece's are found again when it spreads, so a
        /// dense settlement of tens of thousands of pieces needs no more memory than the pieces themselves.</summary>
        internal static Clusters Cluster(IList<ShapePoint> pts, double eps, int minPts, bool stoneCore)
        {
            double cell = eps;
            int count = pts.Count;
            var bi = new long[count];
            var bj = new long[count];
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < count; i++)
            {
                bi[i] = (long)Math.Floor(pts[i].X / cell);
                bj[i] = (long)Math.Floor(pts[i].Z / cell);
                long k = Key(bi[i], bj[i]);
                if (!grid.TryGetValue(k, out List<int> b))
                {
                    b = new List<int>();
                    grid[k] = b;
                }
                b.Add(i);
            }
            double e2 = eps * eps;
            var core = new bool[count];
            for (int p = 0; p < count; p++)
            {
                core[p] = (stoneCore && pts[p].Stone) || HasNeighbours(pts, grid, bi[p], bj[p], p, e2, minPts);
            }
            var label = new int[count];
            for (int p = 0; p < count; p++)
            {
                label[p] = -1;
            }
            int id = 0;
            var queue = new List<int>();
            for (int i = 0; i < count; i++)
            {
                if (!core[i] || label[i] >= 0)
                {
                    continue;
                }
                queue.Clear();
                queue.Add(i);
                label[i] = id;
                while (queue.Count > 0)
                {
                    int q = queue[queue.Count - 1];
                    queue.RemoveAt(queue.Count - 1);
                    for (int di = -1; di <= 1; di++)
                    {
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            if (!grid.TryGetValue(Key(bi[q] + di, bj[q] + dj), out List<int> b))
                            {
                                continue;
                            }
                            // A piece once labelled is taken out of its bucket (the core counts are done, and the order
                            // pieces are met in never changes a label), so no piece is read again and again.
                            for (int t = b.Count - 1; t >= 0; t--)
                            {
                                int n = b[t];
                                if (label[n] < 0)
                                {
                                    double dx = pts[n].X - pts[q].X, dz = pts[n].Z - pts[q].Z;
                                    if (dx * dx + dz * dz > e2)
                                    {
                                        continue;
                                    }
                                    label[n] = id;
                                    if (core[n])
                                    {
                                        queue.Add(n);
                                    }
                                }
                                b[t] = b[b.Count - 1];
                                b.RemoveAt(b.Count - 1);
                            }
                        }
                    }
                }
                id++;
            }
            return new Clusters { Label = label, Count = id };
        }

        // Whether a piece has at least minPts pieces within eps (itself included), counting no further than needed.
        private static bool HasNeighbours(IList<ShapePoint> pts, Dictionary<long, List<int>> grid, long ci, long cj, int p, double e2, int minPts)
        {
            if (minPts <= 0)
            {
                return true;
            }
            int found = 0;
            for (int di = -1; di <= 1; di++)
            {
                for (int dj = -1; dj <= 1; dj++)
                {
                    if (!grid.TryGetValue(Key(ci + di, cj + dj), out List<int> b))
                    {
                        continue;
                    }
                    foreach (int j in b)
                    {
                        double dx = pts[j].X - pts[p].X, dz = pts[j].Z - pts[p].Z;
                        if (dx * dx + dz * dz <= e2 && ++found >= minPts)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // ---------- squared Euclidean distance transform (Felzenszwalb & Huttenlocher) ----------

        private sealed class EdtBuffers
        {
            internal double[] F = new double[0];
            internal double[] D = new double[0];
            internal int[] V = new int[0];
            internal double[] Z = new double[1];

            internal void Ensure(int n)
            {
                if (n > V.Length)
                {
                    F = new double[n];
                    D = new double[n];
                    V = new int[n];
                    Z = new double[n + 1];
                }
            }
        }

        private static void Dt1(EdtBuffers b, int n)
        {
            double[] F = b.F, D = b.D, Z = b.Z;
            int[] V = b.V;
            int k = 0;
            V[0] = 0;
            Z[0] = double.NegativeInfinity;
            Z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s = ((F[q] + q * q) - (F[V[k]] + V[k] * V[k])) / (2 * q - 2 * V[k]);
                while (s <= Z[k])
                {
                    k--;
                    s = ((F[q] + q * q) - (F[V[k]] + V[k] * V[k])) / (2 * q - 2 * V[k]);
                }
                k++;
                V[k] = q;
                Z[k] = s;
                Z[k + 1] = double.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (Z[k + 1] < q)
                {
                    k++;
                }
                int d = q - V[k];
                D[q] = d * d + F[V[k]];
            }
        }

        /// <summary>For each square, the squared distance (in squares) to the nearest marked square of the mask;
        /// about 1e20 where nothing is marked.</summary>
        internal static double[] Edt(byte[] mask, int w, int h)
        {
            const double Inf = 1e20;
            var b = new EdtBuffers();
            b.Ensure(Math.Max(w, h));
            var outD = new double[w * h];
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    b.F[y] = mask[y * w + x] != 0 ? 0 : Inf;
                }
                Dt1(b, h);
                for (int y = 0; y < h; y++)
                {
                    outD[y * w + x] = b.D[y];
                }
            }
            for (int y = 0; y < h; y++)
            {
                int r = y * w;
                for (int x = 0; x < w; x++)
                {
                    b.F[x] = outD[r + x];
                }
                Dt1(b, w);
                for (int x = 0; x < w; x++)
                {
                    outD[r + x] = b.D[x];
                }
            }
            return outD;
        }

        // ---------- the shape of one settlement, on its own small grid ----------

        /// <summary>
        /// The shape of one settlement's pieces. <paramref name="ground"/> gives the ground's height at a square's
        /// middle (x, z in metres); at or above <see cref="WaterLine"/> it's land. With <paramref name="keepSteps"/>
        /// the steps before the finished shape are kept too.
        /// </summary>
        internal static ShapeGrid ShapeOf(IList<ShapePoint> pts, ShapeOptions o, Func<double, double, double> ground, bool keepSteps = false)
        {
            double cs = o.Cell, R = o.Gap / 2, B = o.Margin, C = o.Shore;
            double pad = R + B + C + 3 * cs;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (ShapePoint p in pts)
            {
                if (p.X < minX) minX = p.X;
                if (p.Z < minY) minY = p.Z;
                if (p.X > maxX) maxX = p.X;
                if (p.Z > maxY) maxY = p.Z;
            }
            int gx0 = (int)Math.Floor((minX - pad) / cs), gy0 = (int)Math.Floor((minY - pad) / cs);
            int w = (int)Math.Ceiling((maxX + pad) / cs) - gx0 + 1, h = (int)Math.Ceiling((maxY + pad) / cs) - gy0 + 1, n = w * h;
            var piece = new byte[n];
            var land = new byte[n];
            foreach (ShapePoint p in pts)
            {
                int i = (int)Math.Floor(p.X / cs) - gx0, j = (int)Math.Floor(p.Z / cs) - gy0;
                piece[j * w + i] = 1;
            }
            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    land[j * w + i] = ground((gx0 + i + 0.5) * cs, (gy0 + j + 0.5) * cs) >= WaterLine ? (byte)1 : (byte)0;
                }
            }
            double r2 = (R / cs) * (R / cs);
            // 2: grow
            double[] dP = Edt(piece, w, h);
            var grown = new byte[n];
            for (int k = 0; k < n; k++)
            {
                grown[k] = dP[k] <= r2 ? (byte)1 : (byte)0;
            }
            // 3: fill what the spread from the edge, over land, can't reach
            var reached = new byte[n];
            var queue = new int[n];
            int qh = 0, qt = 0;
            void Seed(int k)
            {
                if (reached[k] == 0 && grown[k] == 0 && land[k] != 0)
                {
                    reached[k] = 1;
                    queue[qt++] = k;
                }
            }
            for (int i = 0; i < w; i++)
            {
                Seed(i);
                Seed((h - 1) * w + i);
            }
            for (int j = 0; j < h; j++)
            {
                Seed(j * w);
                Seed(j * w + w - 1);
            }
            while (qh < qt)
            {
                int k = queue[qh++], i = k % w, j = (k - i) / w;
                if (i > 0) Seed(k - 1);
                if (i < w - 1) Seed(k + 1);
                if (j > 0) Seed(k - w);
                if (j < h - 1) Seed(k + w);
            }
            var filled = new byte[n];
            for (int k = 0; k < n; k++)
            {
                filled[k] = grown[k] != 0 || (land[k] != 0 && reached[k] == 0) ? (byte)1 : (byte)0;
            }
            // 4: shrink back, away from open land only (the sea never pulls the shape back)
            var outside = new byte[n];
            for (int k = 0; k < n; k++)
            {
                outside[k] = filled[k] == 0 && land[k] != 0 ? (byte)1 : (byte)0;
            }
            double[] dO = Edt(outside, w, h);
            var shrunk = new byte[n];
            for (int k = 0; k < n; k++)
            {
                shrunk[k] = (filled[k] != 0 && land[k] != 0 && dO[k] > r2) || piece[k] != 0 ? (byte)1 : (byte)0;
            }
            // 5: margin
            double b2 = (B / cs) * (B / cs);
            double[] dS = Edt(shrunk, w, h);
            var margin = new byte[n];
            for (int k = 0; k < n; k++)
            {
                margin[k] = dS[k] <= b2 ? (byte)1 : (byte)0;
            }
            // 6: shore margin, over water next to the shape's land
            double c2 = (C / cs) * (C / cs);
            var onLand = new byte[n];
            for (int k = 0; k < n; k++)
            {
                onLand[k] = margin[k] != 0 && land[k] != 0 ? (byte)1 : (byte)0;
            }
            double[] dL = Edt(onLand, w, h);
            var final = new byte[n];
            for (int k = 0; k < n; k++)
            {
                final[k] = margin[k] != 0 || (land[k] == 0 && dL[k] <= c2) ? (byte)1 : (byte)0;
            }
            return new ShapeGrid
            {
                X0 = gx0,
                Z0 = gy0,
                W = w,
                H = h,
                Cell = cs,
                Final = final,
                Steps = keepSteps ? new[] { grown, filled, shrunk, margin } : null,
            };
        }
    }
}
