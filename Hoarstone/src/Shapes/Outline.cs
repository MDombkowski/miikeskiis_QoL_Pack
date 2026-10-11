using System;
using System.Collections.Generic;

namespace Hoarstone.Shapes
{
    /// <summary>A point on the ground, in metres (x and z).</summary>
    internal struct Flat
    {
        internal double X;
        internal double Z;

        internal Flat(double x, double z)
        {
            X = x;
            Z = z;
        }
    }

    /// <summary>
    /// A shape's edge, by marching squares as the lab draws it: each 2 × 2 block of squares gives the segments between
    /// the middles of its sides where in and out meet, the corners of the line at the squares' middles; the two saddle
    /// blocks give two segments each, as the lab's table does. Then the segments are joined into lines. Pure arithmetic.
    /// </summary>
    internal static class Outline
    {
        // The lab's table: for each block (bits: lower-left 8, lower-right 4, upper-right 2, upper-left 1, where "lower"
        // is the lower square number), the pairs of side middles to join. T, R, B, L are the block's sides.
        private const int T = 0, Rt = 1, Bt = 2, L = 3;

        private static readonly int[][] Pairs =
        {
            null,
            new[] { L, Bt },
            new[] { Bt, Rt },
            new[] { L, Rt },
            new[] { T, Rt },
            new[] { L, T, Bt, Rt },
            new[] { T, Bt },
            new[] { L, T },
            new[] { L, T },
            new[] { T, Bt },
            new[] { L, Bt, T, Rt },
            new[] { T, Rt },
            new[] { L, Rt },
            new[] { Bt, Rt },
            new[] { L, Bt },
            null,
        };

        /// <summary>Adds the segments of every block whose lower-left square (i, j) lies in [i0, i1] × [j0, j1], in
        /// half-square units (the world point is (a / 2 + 0.5) × cell): squares are read through
        /// <paramref name="at"/>, by world square numbers.</summary>
        internal static void Segments(Func<int, int, bool> at, int i0, int j0, int i1, int j1, List<(long a, long b, long c, long d)> segments)
        {
            for (int j = j0; j <= j1; j++)
            {
                for (int i = i0; i <= i1; i++)
                {
                    int idx = (at(i, j) ? 8 : 0) | (at(i + 1, j) ? 4 : 0) | (at(i + 1, j + 1) ? 2 : 0) | (at(i, j + 1) ? 1 : 0);
                    int[] pairs = Pairs[idx];
                    if (pairs == null)
                    {
                        continue;
                    }
                    for (int p = 0; p < pairs.Length; p += 2)
                    {
                        Side(i, j, pairs[p], out long ax, out long az);
                        Side(i, j, pairs[p + 1], out long bx, out long bz);
                        segments.Add((ax, az, bx, bz));
                    }
                }
            }
        }

        // A side's middle, in half squares: T (i + 0.5, j), R (i + 1, j + 0.5), B (i + 0.5, j + 1), L (i, j + 0.5).
        private static void Side(int i, int j, int side, out long x, out long z)
        {
            switch (side)
            {
                case T: x = 2L * i + 1; z = 2L * j; break;
                case Rt: x = 2L * i + 2; z = 2L * j + 1; break;
                case Bt: x = 2L * i + 1; z = 2L * j + 2; break;
                default: x = 2L * i; z = 2L * j + 1; break;
            }
        }

        /// <summary>Joins segments that share ends into lines, in metres. A closed line ends where it began.</summary>
        internal static List<List<Flat>> Join(List<(long a, long b, long c, long d)> segments, double cell)
        {
            var ends = new Dictionary<(long, long), List<int>>();
            void AddEnd(long x, long z, int s)
            {
                if (!ends.TryGetValue((x, z), out List<int> list))
                {
                    list = new List<int>(2);
                    ends[(x, z)] = list;
                }
                list.Add(s);
            }
            for (int s = 0; s < segments.Count; s++)
            {
                AddEnd(segments[s].a, segments[s].b, s);
                AddEnd(segments[s].c, segments[s].d, s);
            }
            var used = new bool[segments.Count];
            var lines = new List<List<Flat>>();
            Flat ToWorld(long x, long z) => new Flat((x / 2.0 + 0.5) * cell, (z / 2.0 + 0.5) * cell);

            for (int start = 0; start < segments.Count; start++)
            {
                if (used[start])
                {
                    continue;
                }
                used[start] = true;
                var chain = new LinkedList<(long, long)>();
                chain.AddLast((segments[start].a, segments[start].b));
                chain.AddLast((segments[start].c, segments[start].d));
                // Walk on from the tail, then from the head.
                for (int dir = 0; dir < 2; dir++)
                {
                    while (true)
                    {
                        (long x, long z) tip = dir == 0 ? chain.Last.Value : chain.First.Value;
                        int next = -1;
                        foreach (int s in ends[tip])
                        {
                            if (!used[s])
                            {
                                next = s;
                                break;
                            }
                        }
                        if (next < 0)
                        {
                            break;
                        }
                        used[next] = true;
                        (long, long) other = segments[next].a == tip.x && segments[next].b == tip.z
                            ? (segments[next].c, segments[next].d)
                            : (segments[next].a, segments[next].b);
                        if (dir == 0)
                        {
                            chain.AddLast(other);
                        }
                        else
                        {
                            chain.AddFirst(other);
                        }
                    }
                }
                var line = new List<Flat>(chain.Count);
                foreach ((long x, long z) in chain)
                {
                    line.Add(ToWorld(x, z));
                }
                lines.Add(line);
            }
            return lines;
        }
    }
}
