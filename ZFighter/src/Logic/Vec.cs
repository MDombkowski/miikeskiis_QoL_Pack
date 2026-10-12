using System;

namespace ZFighter
{
    /// <summary>
    /// A point or direction for the plain-code rules (Obb, Nudge), in doubles. The game's side converts from and to
    /// Unity's Vector3; the scratch harness runs these rules without Unity.
    /// </summary>
    internal readonly struct Vec
    {
        internal readonly double X;
        internal readonly double Y;
        internal readonly double Z;

        internal Vec(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal double this[int axis] => axis == 0 ? X : axis == 1 ? Y : Z;

        public static Vec operator +(Vec a, Vec b) => new Vec(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vec operator -(Vec a, Vec b) => new Vec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec operator *(Vec a, double k) => new Vec(a.X * k, a.Y * k, a.Z * k);

        internal static double Dot(Vec a, Vec b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        internal static Vec Cross(Vec a, Vec b) =>
            new Vec(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        /// <summary>Component by component.</summary>
        internal static Vec Scale(Vec a, Vec b) => new Vec(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

        internal double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        public override string ToString() => $"({X:0.####}, {Y:0.####}, {Z:0.####})";
    }
}
