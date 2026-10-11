using System;
using System.Collections.Generic;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// A plane over the ground: target(x, z) = clamp(H0 + Gx (x − X0) + Gz (z − Z0), Lo, Hi), in world metres, in doubles
    /// while it's worked out. Lo and Hi are ±infinity when it keeps sloping past its high and low points.
    /// </summary>
    internal readonly struct Plane
    {
        internal Plane(double x0, double z0, double h0, double gx, double gz, double lo, double hi)
        {
            X0 = x0;
            Z0 = z0;
            H0 = h0;
            Gx = gx;
            Gz = gz;
            Lo = lo;
            Hi = hi;
        }

        internal double X0 { get; }
        internal double Z0 { get; }
        internal double H0 { get; }
        internal double Gx { get; }
        internal double Gz { get; }
        internal double Lo { get; }
        internal double Hi { get; }

        internal static Plane Level(double h, double x0, double z0) =>
            new Plane(x0, z0, h, 0.0, 0.0, double.NegativeInfinity, double.PositiveInfinity);

        internal double At(double x, double z) => SlopeMath.Clamp(H0 + Gx * (x - X0) + Gz * (z - Z0), Lo, Hi);

        /// <summary>The same plane measured from another point (what a stroke sends: its height at the stroke's centre).</summary>
        internal Plane Rebase(double x0, double z0) => new Plane(x0, z0, H0 + Gx * (x0 - X0) + Gz * (z0 - Z0), Gx, Gz, Lo, Hi);

        internal Plane Clamped(double lo, double hi) => new Plane(X0, Z0, H0, Gx, Gz, lo, hi);
    }

    /// <summary>
    /// Slope's maths (card B02), as plain code with no game calls: the port of the Slope mock-up's own script,
    /// scratchpad\j35\mockup\slope.js (56-B, 2026-10-10), which the harness runs under Node on the same inputs and
    /// compares, function by function. JavaScript's Math.round rounds a half up; it's written out exactly here
    /// (RoundHalfUp) so the two agree (C#'s Math.Round rounds a half to even).
    /// </summary>
    internal static class SlopeMath
    {
        /// <summary>A height difference smaller than this is level.</summary>
        internal const double Flat = 1e-4;

        /// <summary>The longest strip two clicks make, in points from its middle to an end.</summary>
        internal const int LongestHalfStrip = (Footprint.LargestSize - 1) / 2;

        internal static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>JavaScript's Math.round: the nearest whole number, a half rounding up (Footprint.RoundHalfUp).</summary>
        internal static double RoundHalfUp(double v) => Footprint.RoundHalfUp(v);

        /// <summary>
        /// High to low (his description, the default): through the highest point and the lowest, falling the way the ground
        /// there falls (the best fit's direction), so a slope that's already even is left as it is. Where the ground has no
        /// fall to speak of, or it runs across the line between the two points, it falls along that line instead. The
        /// first point in the area's order wins a tie. <paramref name="levelBeyond"/>: it never goes above the highest or
        /// below the lowest point.
        /// </summary>
        internal static Plane HighToLow(IReadOnlyList<(double X, double Z, double H)> points, bool levelBeyond)
        {
            (double X, double Z, double H) hi = points[0];
            (double X, double Z, double H) lo = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i].H > hi.H)
                {
                    hi = points[i];
                }
                if (points[i].H < lo.H)
                {
                    lo = points[i];
                }
            }
            if (hi.H - lo.H < Flat)
            {
                return Plane.Level(hi.H, hi.X, hi.Z);
            }
            double dx = lo.X - hi.X;
            double dz = lo.Z - hi.Z;
            Plane fit = BestFit(points, false);
            double ux = fit.Gx;
            double uz = fit.Gz;
            double along = dx * ux + dz * uz;
            double size = Math.Sqrt(dx * dx + dz * dz) * Math.Sqrt(ux * ux + uz * uz);
            if (!(size > 1e-9) || Math.Abs(along) < 0.1 * size)
            {
                ux = dx;
                uz = dz;
                along = dx * dx + dz * dz;
            }
            double k = (lo.H - hi.H) / along;
            var plane = new Plane(hi.X, hi.Z, hi.H, k * ux, k * uz, double.NegativeInfinity, double.PositiveInfinity);
            return levelBeyond ? plane.Clamped(lo.H, hi.H) : plane;
        }

        /// <summary>Closest to the ground: the least-squares plane through the heights there (a tiny ridge keeps a
        /// one-point-wide area level across its line), the slope that moves the least earth.</summary>
        internal static Plane BestFit(IReadOnlyList<(double X, double Z, double H)> points, bool levelBeyond)
        {
            int n = points.Count;
            double cx = 0, cz = 0, hm = 0;
            for (int i = 0; i < n; i++)
            {
                cx += points[i].X;
                cz += points[i].Z;
                hm += points[i].H;
            }
            cx /= n;
            cz /= n;
            hm /= n;
            double sxx = 0, szz = 0, sxz = 0, sxh = 0, szh = 0;
            double least = double.PositiveInfinity, most = double.NegativeInfinity;
            for (int i = 0; i < n; i++)
            {
                double x = points[i].X - cx;
                double z = points[i].Z - cz;
                double h = points[i].H - hm;
                sxx += x * x;
                szz += z * z;
                sxz += x * z;
                sxh += x * h;
                szh += z * h;
                least = Math.Min(least, points[i].H);
                most = Math.Max(most, points[i].H);
            }
            double lambda = 1e-6 * n;
            double det = (sxx + lambda) * (szz + lambda) - sxz * sxz;
            double gx = ((szz + lambda) * sxh - sxz * szh) / det;
            double gz = ((sxx + lambda) * szh - sxz * sxh) / det;
            var plane = new Plane(cx, cz, hm, gx, gz, double.NegativeInfinity, double.PositiveInfinity);
            return levelBeyond ? plane.Clamped(least, most) : plane;
        }

        /// <summary>Two clicks: from the first point to the second, level across.</summary>
        internal static Plane TwoPoints((double X, double Z, double H) a, (double X, double Z, double H) b, bool levelBeyond)
        {
            double dx = b.X - a.X;
            double dz = b.Z - a.Z;
            double d2 = dx * dx + dz * dz;
            if (d2 < 1e-9)
            {
                return Plane.Level(a.H, a.X, a.Z);
            }
            double k = (b.H - a.H) / d2;
            var plane = new Plane(a.X, a.Z, a.H, k * dx, k * dz, double.NegativeInfinity, double.PositiveInfinity);
            return levelBeyond ? plane.Clamped(Math.Min(a.H, b.H), Math.Max(a.H, b.H)) : plane;
        }

        /// <summary>How far a point lies inside a square's or rectangle's outline (half a spacing outside its outer points),
        /// and half the area's smaller extent, both in metres.</summary>
        internal static (double E, double Half) Inside(Footprint area, double x, double z)
        {
            (double along, double across) = area.Local(x, z);
            double spacing = area.PointSpacing / 2.0;
            double lo = area.HalfLength + spacing;
            double wo = area.HalfWidth + spacing;
            return (Math.Min(lo - Math.Abs(along), wo - Math.Abs(across)), Math.Min(lo, wo));
        }

        /// <summary>The same for a round area: its outline half a metre past its radius.</summary>
        internal static (double E, double Half) InsideRound(int centreX, int centreZ, double radius, double x, double z)
        {
            double d = Math.Sqrt((x - centreX) * (x - centreX) + (z - centreZ) * (z - centreZ));
            return (radius + 0.5 - d, radius + 0.5);
        }

        /// <summary>The edge's weight: 1 where the point lies on the plane, falling smoothly to near 0 at the outline over a
        /// band as wide as (1 − sharpness) of half the area's smaller extent. At 100 % the whole area lies on the plane.</summary>
        internal static double EdgeWeight(double e, double half, double sharpness)
        {
            double band = (1.0 - Clamp(sharpness, 0.0, 100.0) / 100.0) * half;
            if (band < 1e-6)
            {
                return 1.0;
            }
            double t = Clamp(e / band, 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        /// <summary>A round area's points: every point within <paramref name="radius"/> of its centre point, row by row from
        /// the south-west.</summary>
        internal static IEnumerable<(int X, int Z)> RoundPoints(int centreX, int centreZ, double radius)
        {
            int span = (int)Math.Ceiling(radius);
            for (int z = centreZ - span; z <= centreZ + span; z++)
            {
                for (int x = centreX - span; x <= centreX + span; x++)
                {
                    double d = Math.Sqrt((double)(x - centreX) * (x - centreX) + (double)(z - centreZ) * (z - centreZ));
                    if (d <= radius)
                    {
                        yield return (x, z);
                    }
                }
            }
        }

        /// <summary>
        /// Two clicks' area: a strip from the first click's point (<paramref name="ax"/>, <paramref name="az"/>) toward the
        /// second's, <paramref name="width"/> points wide, turned to the nearest of <paramref name="stepDegrees"/> (22.5, 45
        /// or 90, as "Square tools turn"), its length a whole, odd number of points (up to 31) so its middle is a ground
        /// point. <paramref name="endX"/> and <paramref name="endZ"/>: where the slope ends, the far end's middle.
        /// </summary>
        internal static Footprint Strip(int ax, int az, double bx, double bz, int width, double stepDegrees, out double endX, out double endZ)
        {
            double dx = bx - ax;
            double dz = bz - az;
            if (dx == 0.0 && dz == 0.0)
            {
                endX = ax;
                endZ = az;
                return new Footprint(ax, az, 1, width, 0.0);
            }
            double turn = RoundHalfUp(Math.Atan2(dx, dz) * 180.0 / Math.PI / stepDegrees) * stepDegrees;
            turn = Footprint.Normalise(turn, 360.0);
            double r = turn * Math.PI / 180.0;
            double fx = Math.Sin(r);
            double fz = Math.Cos(r);
            double s = Footprint.Spacing(turn);
            double half = RoundHalfUp((dx * fx + dz * fz) / s / 2.0);
            half = Math.Max(0.0, Math.Min(LongestHalfStrip, half));
            int cx = (int)RoundHalfUp(ax + half * s * fx);
            int cz = (int)RoundHalfUp(az + half * s * fz);
            endX = ax + 2.0 * half * s * fx;
            endZ = az + 2.0 * half * s * fz;
            return new Footprint(cx, cz, 2 * (int)half + 1, width, turn);
        }
    }

    /// <summary>How a Slope stroke moves its points, as it travels (one byte of the stroke's block).</summary>
    [Flags]
    internal enum SlopeFlags : byte
    {
        None = 0,

        /// <summary>It makes the points it moves dirt ("Its ground": dirt).</summary>
        Dirt = 1,

        /// <summary>It only lowers: a point whose target is above it is left alone.</summary>
        OnlyLower = 2,

        /// <summary>It only raises: a point whose target is below it is left alone.</summary>
        OnlyRaise = 4,

        /// <summary>Its area is round (its radius is the stroke's radius), not a square or rectangle.</summary>
        Round = 8,
    }

    /// <summary>A Slope stroke's plane as it travels: its height at the stroke's centre point and its fall, and its low and
    /// high clamp (±infinity when it keeps sloping), as floats.</summary>
    internal struct SlopePlane
    {
        internal float H0;
        internal float Gx;
        internal float Gz;
        internal float Lo;
        internal float Hi;

        internal static SlopePlane Of(Plane plane, int centreX, int centreZ)
        {
            Plane at = plane.Rebase(centreX, centreZ);
            return new SlopePlane { H0 = (float)at.H0, Gx = (float)at.Gx, Gz = (float)at.Gz, Lo = (float)at.Lo, Hi = (float)at.Hi };
        }

        /// <summary>The target height at a point, in world metres, the stroke's centre point given.</summary>
        internal float At(int centreX, int centreZ, int x, int z) =>
            (float)SlopeMath.Clamp(H0 + (double)Gx * (x - centreX) + (double)Gz * (z - centreZ), Lo, Hi);
    }

    /// <summary>
    /// One Slope stroke, as both his game's preview and the game that applies it work it out: its area (a square or
    /// rectangle, or round), its plane, its edge and how it moves its points. The applying side builds it from the stroke
    /// alone (the plane travels), so the result is the preview exactly.
    /// </summary>
    internal sealed class SlopeStroke
    {
        internal bool Round;

        /// <summary>A square or rectangle's area (not round).</summary>
        internal Footprint Area;

        internal int CentreX;
        internal int CentreZ;

        /// <summary>A round area's radius: the points within it of its centre point.</summary>
        internal float Radius;

        internal SlopePlane Plane;

        /// <summary>Whether it has a plane (two clicks' first click has none yet: it only shows where it measures).</summary>
        internal bool HasPlane;

        internal SlopeFlags Flags;

        /// <summary>Its edge, 0 (soft) to 100 % (sharp).</summary>
        internal float Sharpness;

        internal static SlopeStroke Square(Footprint area, float sharpness, SlopeFlags flags) => new SlopeStroke
        {
            Area = area,
            CentreX = area.CentreX,
            CentreZ = area.CentreZ,
            Sharpness = sharpness,
            Flags = flags & ~SlopeFlags.Round,
        };

        internal static SlopeStroke Circle(int centreX, int centreZ, float radius, float sharpness, SlopeFlags flags) => new SlopeStroke
        {
            Round = true,
            CentreX = centreX,
            CentreZ = centreZ,
            Radius = radius,
            Sharpness = sharpness,
            Flags = flags | SlopeFlags.Round,
        };

        /// <summary>How far from its centre it can change anything, in metres.</summary>
        internal float Reach => Round ? Radius : (float)Area.Reach;

        internal IEnumerable<(int X, int Z)> Points() => Round ? SlopeMath.RoundPoints(CentreX, CentreZ, Radius) : Area.Points();

        /// <summary>The edge's weight at a point of it.</summary>
        internal float WeightAt(int x, int z)
        {
            (double e, double half) = Round ? SlopeMath.InsideRound(CentreX, CentreZ, Radius, x, z) : SlopeMath.Inside(Area, x, z);
            return (float)SlopeMath.EdgeWeight(e, half, Sharpness);
        }

        /// <summary>The plane's height at a point, in world metres.</summary>
        internal float TargetAt(int x, int z) => Plane.At(CentreX, CentreZ, x, z);

        /// <summary>One point's change, Terraformer's own level step with the edge's weight (within the game's 8 m), unless
        /// "only lower" or "only raise" leaves it alone. Whether it acted on the point.</summary>
        internal bool Step(ref float level, ref float smooth, float height, float target, float weight)
        {
            if (!HasPlane || ((Flags & SlopeFlags.OnlyLower) != 0 && target > height) || ((Flags & SlopeFlags.OnlyRaise) != 0 && target < height))
            {
                return false;
            }
            Squares.LevelPoint(ref level, ref smooth, height, target, weight);
            return true;
        }

        /// <summary>The stroke on one zone's points (the game that applies it): each point it acts on moved, and made dirt
        /// for "Its ground: dirt". <paramref name="zoneHeight"/> turns the plane's world heights into the zone's own. How
        /// many points it changed.</summary>
        internal int ApplyTo(Ground ground, float zoneHeight, Color paint)
        {
            int changed = 0;
            int pitch = ground.Width + 1;
            foreach ((int x, int z) in Points())
            {
                int column = x - ground.OriginX;
                int row = z - ground.OriginZ;
                if (column < 0 || row < 0 || column > ground.Width || row > ground.Width)
                {
                    continue;
                }
                int index = row * pitch + column;
                float target = TargetAt(x, z) - zoneHeight;
                if (!Step(ref ground.LevelDelta[index], ref ground.SmoothDelta[index], ground.HeightAt(column, row), target, WeightAt(x, z)))
                {
                    continue;
                }
                ground.ModifiedHeight[index] = true;
                if ((Flags & SlopeFlags.Dirt) != 0)
                {
                    Squares.Paint(ground, index, paint, false);
                }
                changed++;
            }
            return changed;
        }
    }
}
