using System;
using System.Collections.Generic;

namespace Terraformer
{
    /// <summary>
    /// Which of the ground's points a square tool changes. The ground is a grid of points one metre apart, on whole-metre
    /// world coordinates, lined up north–south and east–west. A square tool's area is centred on one of those points and
    /// has an odd number of points along its facing (its length) and across it (its width): equal, it's a square (5 × 5
    /// by default), unequal a rectangle (B02, "like PlantEasily's rows and columns"). It turns with the game's own build
    /// turn, in the game's sense: clockwise seen from above, as Quaternion.Euler(0, turn, 0) turns a building, so its
    /// facing is (sin t, cos t) and its right (cos t, −sin t) in (x, z), north at 0° and east at 90°. Turned by 45°, its
    /// edges run along the grid's diagonals with its points spaced along them, and it takes in every point inside it (13
    /// for the 3-point square), so no point in it is left behind. At other angles (22.5°) its edges are one metre per
    /// point, as unturned, and it takes the points inside them, which can only give a stepped edge: no line of points runs
    /// at that angle. A square looks the same turned by 90°, so its turn is kept from 0 up to 90°; a rectangle's from 0 up
    /// to 180°. Pure arithmetic, no game calls, so the harness can check it.
    /// </summary>
    internal readonly struct Footprint
    {
        /// <summary>How far outside its edge a point may lie and still count, so a point exactly on an edge is always in,
        /// whatever the rounding of sines and cosines.</summary>
        internal const double Slack = 1e-4;

        internal const int SmallestSize = 1;

        internal const int LargestSize = 31;

        /// <summary>The steps a square turns in: the game's own build turn.</summary>
        internal const double TurnStep = 22.5;

        /// <summary>A square: as many points along its facing as across it.</summary>
        internal Footprint(int centreX, int centreZ, int pointsPerEdge, double turnDegrees)
            : this(centreX, centreZ, pointsPerEdge, pointsPerEdge, turnDegrees)
        {
        }

        /// <param name="length">Its points along its facing (odd, 1 to 31).</param>
        /// <param name="width">Its points across its facing (odd, 1 to 31).</param>
        internal Footprint(int centreX, int centreZ, int length, int width, double turnDegrees)
        {
            CentreX = centreX;
            CentreZ = centreZ;
            Length = OddWithin(length);
            Width = OddWithin(width);
            TurnDegrees = Steps(turnDegrees, Length != Width);
            double turn = TurnDegrees * Math.PI / 180.0;
            Sin = Math.Sin(turn);
            Cos = Math.Cos(turn);
            double spacing = Spacing(TurnDegrees);
            HalfLength = (Length - 1) / 2.0 * spacing;
            HalfWidth = (Width - 1) / 2.0 * spacing;
        }

        /// <summary>The world x of the point it's centred on (a whole metre).</summary>
        internal int CentreX { get; }

        internal int CentreZ { get; }

        /// <summary>The number of points along its facing: odd, 1 to 31.</summary>
        internal int Length { get; }

        /// <summary>The number of points across its facing: odd, 1 to 31.</summary>
        internal int Width { get; }

        /// <summary>Whether it's a square (as many points each way).</summary>
        internal bool IsSquare => Length == Width;

        /// <summary>Its turn, one of the game's 22.5° steps: 0 up to (not including) 90° for a square, which looks the same
        /// turned by 90°, and 0 up to 180° for a rectangle.</summary>
        internal double TurnDegrees { get; }

        /// <summary>The sine and cosine of its turn: its facing is (sin, cos) in (x, z), its right (cos, −sin).</summary>
        internal double Sin { get; }

        internal double Cos { get; }

        /// <summary>Half its length, in metres, from the centre to the edge it faces.</summary>
        internal double HalfLength { get; }

        /// <summary>Half its width, in metres, from the centre to a side.</summary>
        internal double HalfWidth { get; }

        /// <summary>The distance between neighbouring points along an edge at its turn.</summary>
        internal double PointSpacing => Spacing(TurnDegrees);

        /// <summary>The farthest any of its points can be from its centre, in metres: its corner.</summary>
        internal double Reach => Math.Sqrt(HalfLength * HalfLength + HalfWidth * HalfWidth);

        /// <summary>Whether it's turned to a diagonal of the grid (45° or 135°), where its edges run through points.</summary>
        internal bool OnDiagonal => Math.Abs(Normalise(TurnDegrees, 90.0) - 45.0) < 1e-6;

        /// <summary>A world point's coordinates in the area: <c>Along</c> its facing, <c>Across</c> to its right, in metres
        /// from its centre.</summary>
        internal (double Along, double Across) Local(double worldX, double worldZ)
        {
            double dx = worldX - CentreX;
            double dz = worldZ - CentreZ;
            return (dx * Sin + dz * Cos, dx * Cos - dz * Sin);
        }

        /// <summary>Whether the point at these world coordinates is one it changes: both its coordinates in the area lie
        /// within half the length and half the width.</summary>
        internal bool Contains(double worldX, double worldZ)
        {
            (double along, double across) = Local(worldX, worldZ);
            return Math.Abs(along) <= HalfLength + Slack && Math.Abs(across) <= HalfWidth + Slack;
        }

        /// <summary>Every point it changes, as world coordinates, row by row from the south-west.</summary>
        internal IEnumerable<(int X, int Z)> Points()
        {
            int reach = (int)Math.Ceiling(Reach + Slack);
            for (int z = CentreZ - reach; z <= CentreZ + reach; z++)
            {
                for (int x = CentreX - reach; x <= CentreX + reach; x++)
                {
                    if (Contains(x, z))
                    {
                        yield return (x, z);
                    }
                }
            }
        }

        /// <summary>The corners of its outline, for drawing it: its edges lie half a spacing outside its outer points, so
        /// the outline shows the ground it changes (at 0°, the 3-point square is drawn 3 metres across).</summary>
        internal (double X, double Z)[] Outline()
        {
            double spacing = Spacing(TurnDegrees) / 2.0;
            double along = HalfLength + spacing;
            double across = HalfWidth + spacing;
            var corners = new (double X, double Z)[4];
            int i = 0;
            foreach ((double a, double c) in new[] { (-along, -across), (along, -across), (along, across), (-along, across) })
            {
                corners[i++] = (CentreX + a * Sin + c * Cos, CentreZ + a * Cos - c * Sin);
            }
            return corners;
        }

        /// <summary>The distance between neighbouring points along an edge at this turn: one metre along the grid, the
        /// diagonal of a metre square (1.41 m) along a diagonal (45° and 135°), and one metre at any other angle.</summary>
        internal static double Spacing(double turnDegrees) =>
            Math.Abs(Steps(turnDegrees) - 45.0) < 1e-6 ? Math.Sqrt(2.0) : 1.0;

        /// <summary>A square's turn as it takes it: the nearest of the game's 22.5° steps, as 0 up to (not including) 90°.
        /// A square's turn is always one of those steps, but the game that applies a stroke gets it back from the stroke's
        /// facing, a few millionths of a degree off (45° arrives as 45.0000038°), and at exactly 45° its points are spaced
        /// along the ground's diagonals: anything else would change a smaller set of points than the outline shows
        /// (review round 1).</summary>
        internal static double Steps(double degrees) => Steps(degrees, false);

        /// <summary>The nearest of the game's 22.5° steps, as 0 up to 90° for a square or 180° for a rectangle. A half step
        /// rounds up, as the Slope mock-up's JavaScript rounds (Math.round), so the two always agree.</summary>
        internal static double Steps(double degrees, bool rectangle) =>
            Normalise(RoundHalfUp(degrees / TurnStep) * TurnStep, rectangle ? 180.0 : 90.0);

        /// <summary>JavaScript's Math.round exactly: the nearest whole number, a half rounding up (toward +infinity). Not
        /// Math.Floor(v + 0.5), which adds before it rounds: 0.49999999999999994 + 0.5 comes to exactly 1 in doubles, where
        /// JavaScript gives 0 (found by b02's harness); C#'s own Math.Round rounds a half to even.</summary>
        internal static double RoundHalfUp(double v)
        {
            double whole = Math.Floor(v);
            return v - whole >= 0.5 ? whole + 1.0 : whole;   // v - whole is exact for every double
        }

        /// <summary>Any angle, as 0 up to (not including) 90°.</summary>
        internal static double Normalise(double degrees) => Normalise(degrees, 90.0);

        /// <summary>Any angle, as 0 up to (not including) <paramref name="period"/>.</summary>
        internal static double Normalise(double degrees, double period)
        {
            double turn = degrees % period;
            if (turn < 0)
            {
                turn += period;
            }
            return turn >= period - 1e-9 ? 0.0 : turn;
        }

        /// <summary>The nearest odd size between the smallest and the largest.</summary>
        internal static int OddWithin(int size)
        {
            int clamped = Math.Max(SmallestSize, Math.Min(LargestSize, size));
            return clamped % 2 == 1 ? clamped : clamped - 1;
        }
    }
}
