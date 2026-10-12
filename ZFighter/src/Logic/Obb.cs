using System;

namespace ZFighter
{
    /// <summary>
    /// A piece's drawing as a box turned with the piece: its centre, its three axes (the piece's right, up and forward,
    /// unit length) and its half sizes along them, in metres. Plain code: the scratch harness runs it.
    /// </summary>
    internal readonly struct Obb
    {
        internal readonly Vec Center;
        internal readonly Vec AxisX;
        internal readonly Vec AxisY;
        internal readonly Vec AxisZ;
        internal readonly Vec Half;

        internal Obb(Vec center, Vec axisX, Vec axisY, Vec axisZ, Vec half)
        {
            Center = center;
            AxisX = axisX;
            AxisY = axisY;
            AxisZ = axisZ;
            Half = half;
        }

        internal Vec Axis(int i) => i == 0 ? AxisX : i == 1 ? AxisY : AxisZ;

        /// <summary>The smallest box along the world's axes that holds this one: its lowest and highest corner.</summary>
        internal void WorldBox(out Vec min, out Vec max)
        {
            double ex = Math.Abs(AxisX.X) * Half.X + Math.Abs(AxisY.X) * Half.Y + Math.Abs(AxisZ.X) * Half.Z;
            double ey = Math.Abs(AxisX.Y) * Half.X + Math.Abs(AxisY.Y) * Half.Y + Math.Abs(AxisZ.Y) * Half.Z;
            double ez = Math.Abs(AxisX.Z) * Half.X + Math.Abs(AxisY.Z) * Half.Y + Math.Abs(AxisZ.Z) * Half.Z;
            var extent = new Vec(ex, ey, ez);
            min = Center - extent;
            max = Center + extent;
        }

        /// <summary>
        /// Whether two drawings are laid into each other: their boxes still overlap once each is shrunk by
        /// <paramref name="margin"/> on every side, so two pieces that only touch (floors side by side, a wall standing
        /// on a floor) don't count, and two that share a slab of space at least about twice the margin thick do. The
        /// separating axis test: two boxes are apart exactly when some axis (one of either box's three, or the cross of
        /// one of each) shows a gap between their shadows on it.
        /// </summary>
        internal static bool Overlap(Obb a, Obb b, double margin)
        {
            Vec ha = Shrunk(a.Half, margin);
            Vec hb = Shrunk(b.Half, margin);
            Vec between = b.Center - a.Center;
            for (int i = 0; i < 3; i++)
            {
                if (Apart(a.Axis(i), between, a, ha, b, hb))
                {
                    return false;
                }
                if (Apart(b.Axis(i), between, a, ha, b, hb))
                {
                    return false;
                }
            }
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Vec axis = Vec.Cross(a.Axis(i), b.Axis(j));
                    if (axis.Length < 1e-6)
                    {
                        continue;   // two parallel axes: already tested above
                    }
                    if (Apart(axis, between, a, ha, b, hb))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Whether a face of one box lies on the plane of a face of the other, both facing the same way, within
        /// <paramref name="tolerance"/> metres: the only way two box-like drawings (floors, walls, beams, poles) can
        /// flicker against each other, since surfaces fight only where they lie on one plane. A wall standing in a floor
        /// shares no plane with it unless it stands at the floor's edge (its outer face and the floor's edge on one
        /// plane: that strip does flicker); two floors at one height share their tops. Boxes turned against each other
        /// by anything but whole quarter turns share no face plane. With <paramref name="thinnest"/>, an axis along which
        /// either box is thinner than that (its half size) is skipped: for the box of a piece's snap points, which is flat
        /// where the snap points all lie on one plane (a floor's), so its two faces there would match anything on it.
        /// </summary>
        internal static bool SharePlane(Obb a, Obb b, double tolerance, double thinnest = 0)
        {
            for (int i = 0; i < 3; i++)
            {
                if (a.Half[i] < thinnest)
                {
                    continue;   // flat along this axis: its two faces are one plane, which says nothing
                }
                Vec axis = a.Axis(i);
                double aMid = Vec.Dot(a.Center, axis);
                double bMid = Vec.Dot(b.Center, axis);
                for (int j = 0; j < 3; j++)
                {
                    double dot = Vec.Dot(axis, b.Axis(j));
                    if (Math.Abs(dot) < 0.9999)
                    {
                        continue;   // not parallel
                    }
                    double bHalf = b.Half[j];
                    if (bHalf < thinnest)
                    {
                        continue;
                    }
                    // a's faces along this axis sit at aMid ± a.Half[i], facing ±axis; b's at bMid ± bHalf, facing ±axis
                    // too (its axis j runs along this one, one way or the other, and its faces lie at both ends).
                    if (Math.Abs((aMid + a.Half[i]) - (bMid + bHalf)) <= tolerance
                        || Math.Abs((aMid - a.Half[i]) - (bMid - bHalf)) <= tolerance)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static Vec Shrunk(Vec half, double margin) =>
            new Vec(Math.Max(0, half.X - margin), Math.Max(0, half.Y - margin), Math.Max(0, half.Z - margin));

        private static bool Apart(Vec axis, Vec between, Obb a, Vec ha, Obb b, Vec hb)
        {
            double ra = ha.X * Math.Abs(Vec.Dot(a.AxisX, axis)) + ha.Y * Math.Abs(Vec.Dot(a.AxisY, axis)) + ha.Z * Math.Abs(Vec.Dot(a.AxisZ, axis));
            double rb = hb.X * Math.Abs(Vec.Dot(b.AxisX, axis)) + hb.Y * Math.Abs(Vec.Dot(b.AxisY, axis)) + hb.Z * Math.Abs(Vec.Dot(b.AxisZ, axis));
            // Strictly greater: shadows that only meet at one point (shrunk boxes that touch) count as overlapping, which
            // only happens when the original boxes overlap by exactly twice the margin.
            return Math.Abs(Vec.Dot(between, axis)) > ra + rb;
        }
    }
}
