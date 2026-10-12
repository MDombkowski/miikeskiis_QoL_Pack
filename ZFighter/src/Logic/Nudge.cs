using System;

namespace ZFighter
{
    /// <summary>
    /// An object's turn against the piece root: where its own three axes point, in the root's frame (unit vectors).
    /// Plain code.
    /// </summary>
    internal readonly struct Turn
    {
        internal static readonly Turn None = new Turn(new Vec(1, 0, 0), new Vec(0, 1, 0), new Vec(0, 0, 1));

        internal readonly Vec X;
        internal readonly Vec Y;
        internal readonly Vec Z;

        internal Turn(Vec x, Vec y, Vec z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal Vec Column(int k) => k == 0 ? X : k == 1 ? Y : Z;

        /// <summary>Whether it turns by whole quarter turns only (each axis along one of the root's, either way), as the
        /// game's drawing objects are (a pole's drawing is turned 90 degrees).</summary>
        internal bool QuarterTurns
        {
            get
            {
                for (int k = 0; k < 3; k++)
                {
                    Vec c = Column(k);
                    int along = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        double v = Math.Abs(c[i]);
                        if (Math.Abs(v - 1) < 0.001)
                        {
                            along++;
                        }
                        else if (v > 0.001)
                        {
                            return false;
                        }
                    }
                    if (along != 1)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    /// <summary>
    /// How a piece's drawing is nudged at a level. A box-like piece (floors, walls, beams, poles: their faces lie along
    /// the piece's own axes) is scaled about its drawing's centre a little more on its short sides than its long ones,
    /// so every outer face moves out by the level's distance, and lifted by half that. A slanted piece (a roof, a 26,
    /// 45 or 67 degree beam or wall, stairs, or any piece with a drawing object turned other than by quarter turns) is
    /// scaled by one factor on every side, which never tilts a face (scaling one side more than another would turn a
    /// slanted face, and two overlapping roofs would cross), and lifted by the whole distance, which moves every face
    /// that isn't upright off its neighbour's. Only the drawing objects' own position and scale change, worked out from
    /// what they were, so going back is exact. Plain code: the scratch harness runs it.
    /// </summary>
    internal static class Nudge
    {
        /// <summary>The part of the distance a box-like piece is lifted by, besides growing by it.</summary>
        internal const double Lift = 0.5;

        /// <summary>The part of the distance a slanted piece is lifted by.</summary>
        internal const double LiftSlanted = 1.0;

        /// <summary>A side of the drawing thinner than this (metres) doesn't grow: a flat sheet can't be scaled
        /// outward, and a tiny side would need a huge factor.</summary>
        internal const double ThinnestSide = 0.02;

        /// <summary>The distance, in metres, for a level and the setting's nudge in millimetres.</summary>
        internal static double Distance(int level, double nudgeMillimetres) =>
            level <= 0 ? 0 : level * nudgeMillimetres / 1000.0;

        /// <summary>
        /// The factor for each of the drawing's sides, along the root's axes. Box-like: a side <c>size</c> metres long
        /// becomes <c>size + 2 × distance</c>, so each of its two faces moves out by the distance; a side thinner than
        /// ThinnestSide keeps its size. Slanted: one factor for the two axes its slope lies along, the one that grows
        /// the longer of them by twice the distance, so no slanted face tilts and nothing grows by more; the axis
        /// across the slope (<paramref name="across"/>: 0 for x, 2 for z, -1 when it isn't known) takes a factor of its
        /// own, as a box-like side does, which tilts no face either (every slanted face runs along it) and moves the
        /// faces at its ends, the upright sides, by the whole distance. Without a known axis across, one factor for all
        /// three.
        /// </summary>
        internal static Vec Factors(Vec worldSize, double distance, bool slanted, int across = -1)
        {
            if (!slanted)
            {
                return new Vec(Factor(worldSize.X, distance), Factor(worldSize.Y, distance), Factor(worldSize.Z, distance));
            }
            double longest = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i != across)
                {
                    longest = Math.Max(longest, worldSize[i]);
                }
            }
            double f = Factor(longest, distance);
            return new Vec(
                across == 0 ? Factor(worldSize.X, distance) : f,
                f,
                across == 2 ? Factor(worldSize.Z, distance) : f);
        }

        private static double Factor(double size, double distance) =>
            size >= ThinnestSide && distance > 0 ? 1 + 2 * distance / size : 1;

        /// <summary>The upward move, in metres, for a distance.</summary>
        internal static double LiftFor(double distance, bool slanted) => (slanted ? LiftSlanted : Lift) * distance;

        /// <summary>
        /// Where one drawing object goes, and its new scale. <paramref name="center"/> (the drawing's centre),
        /// <paramref name="factors"/> (from Factors) and <paramref name="lift"/> (the upward move) are in the piece
        /// root's frame. The object's parent sits at <paramref name="parentOffset"/> in the root's frame, turned by
        /// <paramref name="parentTurn"/>, with scale <paramref name="parentScale"/> along its own axes; the object
        /// itself is turned by <paramref name="objectTurn"/> (both against the root). <paramref name="position"/> and
        /// <paramref name="scale"/> are the object's own, as the game set them. Every point of the object ends where
        /// scaling about the centre and lifting puts it: exactly when the factors are all one, or when the turns are
        /// whole quarter turns (Tracker uses one factor whenever they aren't).
        /// </summary>
        internal static void Place(Vec center, Vec factors, Vec lift, Vec parentOffset, Vec parentScale, Turn parentTurn,
            Turn objectTurn, Vec position, Vec scale, out Vec newPosition, out Vec newScale)
        {
            // In the root's frame a point x goes to c + F(x - c) + t. The object's points are parentOffset +
            // R(parentScale·(position + …)); with R a quarter turn (or F one factor), F·R = R·Fp, Fp being F's factors
            // read along the parent's axes, so the object's position becomes Fp·position + Rᵀ((1 - F)(c - offset) + t)
            // / parentScale, and its scale takes F's factors read along its own axes.
            Vec shift = new Vec(
                (1 - factors.X) * (center.X - parentOffset.X) + lift.X,
                (1 - factors.Y) * (center.Y - parentOffset.Y) + lift.Y,
                (1 - factors.Z) * (center.Z - parentOffset.Z) + lift.Z);
            var p = new double[3];
            var s = new double[3];
            for (int k = 0; k < 3; k++)
            {
                Vec axis = parentTurn.Column(k);
                p[k] = Along(factors, axis) * position[k] + Vec.Dot(axis, shift) / parentScale[k];
                s[k] = scale[k] * Along(factors, objectTurn.Column(k));
            }
            newPosition = new Vec(p[0], p[1], p[2]);
            newScale = new Vec(s[0], s[1], s[2]);
        }

        /// <summary>The factor along a direction: F's factors weighted by how much of the direction lies along each of
        /// the root's axes (exactly one of them for a quarter turn).</summary>
        private static double Along(Vec factors, Vec axis) =>
            factors.X * axis.X * axis.X + factors.Y * axis.Y * axis.Y + factors.Z * axis.Z * axis.Z;
    }
}
