using System;

namespace TopperUpper.Loose
{
    /// <summary>A place in the world, in metres: the loose-item pickup's plain logic works without the game or Unity, so
    /// its scratch harness can run it.</summary>
    internal readonly struct Spot
    {
        internal readonly float X;
        internal readonly float Y;
        internal readonly float Z;

        internal Spot(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        internal float DistanceSquared(Spot other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            float dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        internal float Distance(Spot other) => (float)Math.Sqrt(DistanceSquared(other));

        /// <summary>Within <paramref name="range"/> metres of the other place (the edge counts as within).</summary>
        internal bool Within(Spot other, float range) => range >= 0f && DistanceSquared(other) <= range * range;
    }

    /// <summary>An item on the ground by its record's number (the game's ZDOID: the game that made it, and its number
    /// there), which stays the same while it lies there; for remembering an item that found no chest.</summary>
    internal readonly struct ItemKey : IEquatable<ItemKey>
    {
        internal readonly long User;
        internal readonly uint Id;

        internal ItemKey(long user, uint id)
        {
            User = user;
            Id = id;
        }

        public bool Equals(ItemKey other) => User == other.User && Id == other.Id;

        public override bool Equals(object other) => other is ItemKey key && Equals(key);

        public override int GetHashCode() => unchecked((User.GetHashCode() * 397) ^ (int)Id);

        public override string ToString() => $"{User}:{Id}";
    }
}
