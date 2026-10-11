using System;

namespace Grounded
{
    /// <summary>
    /// The box around what is drawn of a piece, measured from its centre point in metres, unturned (x across, y up, z
    /// along, as the piece stands at no turn). The rug's footprint on the floor, and how far its lowest drawn part is
    /// above its centre point (MinY). Also PickTop: the height above its centre point the hammer's ray is sure to meet
    /// (Heights.PickTop); NaN for none. Plain numbers, so the scratch harness runs the turning.
    /// </summary>
    internal readonly struct Footprint
    {
        internal readonly float MinX;
        internal readonly float MaxX;
        internal readonly float MinY;
        internal readonly float MaxY;
        internal readonly float MinZ;
        internal readonly float MaxZ;
        internal readonly float PickTop;

        internal Footprint(float minX, float maxX, float minY, float maxY, float minZ, float maxZ, float pickTop)
        {
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            MinZ = minZ;
            MaxZ = maxZ;
            PickTop = pickTop;
        }

        internal static Footprint Empty => new Footprint(0f, -1f, 0f, -1f, 0f, -1f, float.NaN);

        /// <summary>True when nothing drawn was found.</summary>
        internal bool IsEmpty => MaxX < MinX || MaxY < MinY || MaxZ < MinZ;

        /// <summary>Half its width and length, each made smaller by <paramref name="inset"/> but never below 1 cm.</summary>
        internal float HalfX(float inset) => Math.Max(0.01f, (MaxX - MinX) / 2f - inset);

        internal float HalfZ(float inset) => Math.Max(0.01f, (MaxZ - MinZ) / 2f - inset);

        /// <summary>Where the middle of its footprint lies from its centre point, turned by <paramref name="yawDegrees"/>
        /// about the vertical as the game turns pieces (Quaternion.Euler(0, yaw, 0)): x across and z along.</summary>
        internal void Middle(float yawDegrees, out float x, out float z) =>
            Turn((MinX + MaxX) / 2f, (MinZ + MaxZ) / 2f, yawDegrees, out x, out z);

        /// <summary>The four corners of its footprint, each moved in by <paramref name="inset"/> (never past the
        /// middle), turned by <paramref name="yawDegrees"/>, as x and z pairs from its centre point.</summary>
        internal float[] Corners(float yawDegrees, float inset)
        {
            float midX = (MinX + MaxX) / 2f;
            float midZ = (MinZ + MaxZ) / 2f;
            float halfX = Math.Max(0f, (MaxX - MinX) / 2f - inset);
            float halfZ = Math.Max(0f, (MaxZ - MinZ) / 2f - inset);
            var corners = new float[8];
            int i = 0;
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    Turn(midX + sx * halfX, midZ + sz * halfZ, yawDegrees, out corners[i], out corners[i + 1]);
                    i += 2;
                }
            }
            return corners;
        }

        /// <summary>A turn about the vertical as Unity's Quaternion.Euler(0, yaw, 0) turns a point: (1, 0, 0) goes to
        /// (cos, 0, -sin), (0, 0, 1) to (sin, 0, cos).</summary>
        internal static void Turn(float x, float z, float yawDegrees, out float turnedX, out float turnedZ)
        {
            double radians = yawDegrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            turnedX = (float)(x * cos + z * sin);
            turnedZ = (float)(-x * sin + z * cos);
        }
    }
}
