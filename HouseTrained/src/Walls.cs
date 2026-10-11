using System.Collections.Generic;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// Tames in pens: what counts as a wall ("What counts as a wall": pieces players built, or any building piece), and
    /// the physics tests every point of the part uses against it: the line between a tame and an enemy, the cast ahead of
    /// a head, the room around a body. Pieces are on the game's `piece` layer (report C); a collider's answer is kept,
    /// since the tests run often, and forgotten when the setting changes. Every query is a NonAlloc one into the module's
    /// own buffers (review F15).
    /// </summary>
    internal static class Walls
    {
        private const int MostKept = 8192;

        private static readonly Dictionary<Collider, bool> WallColliders = new Dictionary<Collider, bool>();
        private static readonly Dictionary<Rigidbody, bool> CreatureBodies = new Dictionary<Rigidbody, bool>();
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly Collider[] Overlaps = new Collider[32];
        private static int keptVersion = int.MinValue;

        /// <summary>The game's piece layer (resolved at the start, Module.Resolve).</summary>
        internal static int PieceMask { get; set; }

        /// <summary>Whether a collider belongs to a piece that counts as a wall.</summary>
        internal static bool IsWall(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }
            Forget();
            if (!WallColliders.TryGetValue(collider, out bool wall))
            {
                Piece piece = collider.GetComponentInParent<Piece>();
                wall = piece != null && (Settings.Walls.Value == WallKind.AnyBuildingPiece || piece.IsPlacedByPlayer());
                WallColliders[collider] = wall;
            }
            return wall;
        }

        /// <summary>Whether a collider is another creature's or a player's: its body carries a Character (not told by
        /// layer, since a saddled moose's saddle sphere is on Default: review N1).</summary>
        internal static bool IsCreature(Collider collider)
        {
            Rigidbody body = collider != null ? collider.attachedRigidbody : null;
            if (body == null)
            {
                return false;
            }
            if (!CreatureBodies.TryGetValue(body, out bool creature))
            {
                if (CreatureBodies.Count > MostKept)
                {
                    CreatureBodies.Clear();
                }
                creature = body.GetComponent<Character>() != null;
                CreatureBodies[body] = creature;
            }
            return creature;
        }

        /// <summary>"Behind a wall": the straight line between the two points meets a wall piece.</summary>
        internal static bool Between(Vector3 from, Vector3 to)
        {
            Vector3 line = to - from;
            float length = line.magnitude;
            if (length < 0.01f)
            {
                return false;
            }
            int count = Physics.RaycastNonAlloc(from, line / length, Hits, length, PieceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && i < Hits.Length; i++)
            {
                if (IsWall(Hits[i].collider))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a wall stands between two spots on the ground, along level lines at three heights above it
        /// (0.3, 0.9 and 1.5 m, none higher than <paramref name="tallest"/>): a fence's rails or gaps may miss one line
        /// (code review 1, finding 10). Both spots are taken at <paramref name="ground"/>'s height.</summary>
        internal static bool BetweenSpots(Vector3 from, Vector3 to, float ground, float tallest)
        {
            from.y = ground;
            to.y = ground;
            foreach (float height in Heights)
            {
                float at = Mathf.Min(height, Mathf.Max(0.1f, tallest));
                if (Between(from + Vector3.up * at, to + Vector3.up * at))
                {
                    return true;
                }
            }
            return false;
        }

        private static readonly float[] Heights = { 0.3f, 0.9f, 1.5f };

        /// <summary>Healing in pens: whether a piece that fences a pen in stands between two spots, along lines at the
        /// pen test's heights above each spot's own ground (HealRules.LookHeights, the same for every tame), so a look can
        /// follow a slope.</summary>
        internal static bool PenWallBetween(Vector3 from, float fromGround, Vector3 to, float toGround)
        {
            foreach (float height in HealRules.LookHeights)
            {
                Vector3 start = new Vector3(from.x, fromGround + height, from.z);
                Vector3 line = new Vector3(to.x, toGround + height, to.z) - start;
                float length = line.magnitude;
                if (length < 0.01f)
                {
                    continue;
                }
                int count = Physics.RaycastNonAlloc(start, line / length, Hits, length, PieceMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count && i < Hits.Length; i++)
                {
                    if (IsPenWall(Hits[i].collider))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static readonly Dictionary<Collider, bool> PenColliders = new Dictionary<Collider, bool>();
        private static int penVersion = int.MinValue;

        /// <summary>Whether a collider belongs to a piece that fences a pen in: a wall by "What counts as a wall", and of a
        /// fencing kind (HealRules.Fences: never a floor, roof, chest, bed or torch).</summary>
        internal static bool IsPenWall(Collider collider)
        {
            if (!IsWall(collider))
            {
                return false;
            }
            if (penVersion != Settings.Version || PenColliders.Count > MostKept)
            {
                penVersion = Settings.Version;
                PenColliders.Clear();
            }
            if (!PenColliders.TryGetValue(collider, out bool fences))
            {
                Piece piece = collider.GetComponentInParent<Piece>();
                fences = piece != null && HealRules.Fences((int)piece.m_usage);
                PenColliders[collider] = fences;
            }
            return fences;
        }

        /// <summary>The terrain's height under a point (the game's own ground height: the terrain alone, never a piece),
        /// or <paramref name="otherwise"/> where there is none.</summary>
        internal static float TerrainAt(Vector3 point, float otherwise) =>
            ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(point, out float height) ? height : otherwise;

        /// <summary>Whether a wall piece lies within <paramref name="distance"/> ahead of a head: the cast HeadCast
        /// describes, from the creature's root (its body's lowest point <paramref name="foot"/> above it), along the way it
        /// faces, its front ending at that distance. A wall already inside the start counts.</summary>
        internal static bool AheadOfHead(Vector3 root, Vector3 facing, Reach reach, float distance, float foot)
        {
            facing.y = 0f;
            if (facing.sqrMagnitude < 1e-6f || distance <= 0f)
            {
                return false;
            }
            facing.Normalize();
            HeadCast.Band(reach, foot, out float low, out float high, out float radius);
            Vector3 bottom = root + Vector3.up * low;
            Vector3 top = root + Vector3.up * high;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, radius, facing, Hits, HeadCast.Sweep(distance, radius), PieceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && i < Hits.Length; i++)
            {
                if (IsWall(Hits[i].collider))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether a body standing at the point keeps the margin from every wall piece: a box its width plus the
        /// margin on each side, from just above its feet (a floor at its feet doesn't count) to its height (review F8).</summary>
        internal static bool Room(Vector3 feet, Vector3 facing, float bodyRadius, float height, float margin)
        {
            facing.y = 0f;
            Quaternion turn = facing.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(facing.normalized) : Quaternion.identity;
            float half = bodyRadius + margin;
            float bottom = 0.25f;
            float tall = Mathf.Max(0.5f, height - bottom);
            Vector3 center = feet + Vector3.up * (bottom + tall / 2f);
            int count = Physics.OverlapBoxNonAlloc(center, new Vector3(half, tall / 2f, half), Overlaps, turn, PieceMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count && i < Overlaps.Length; i++)
            {
                if (IsWall(Overlaps[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The face of a piece just under a point on it, by a short ray down (false when it misses, as it can at
        /// an edge): tilted on a ramp or a stair's slope, up on a rail's top. Only that piece is asked.</summary>
        internal static bool FaceUnder(Collider piece, Vector3 point, out Vector3 normal)
        {
            bool met = piece.Raycast(new Ray(point + Vector3.up * 0.1f, Vector3.down), out RaycastHit hit, 0.4f);
            normal = met ? hit.normal : Vector3.up;
            return met;
        }

        /// <summary>The wall pieces a capsule overlaps, into the module's buffer: how many.</summary>
        internal static int Overlapping(Vector3 bottom, Vector3 top, float radius, out Collider[] found)
        {
            found = Overlaps;
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, Overlaps, PieceMask, QueryTriggerInteraction.Ignore);
            int walls = 0;
            for (int i = 0; i < count && i < Overlaps.Length; i++)
            {
                if (IsWall(Overlaps[i]))
                {
                    Overlaps[walls++] = Overlaps[i];
                }
            }
            return walls;
        }

        /// <summary>Everything kept forgotten (the setting changed, or too much kept).</summary>
        private static void Forget()
        {
            if (keptVersion != Settings.Version || WallColliders.Count > MostKept)
            {
                keptVersion = Settings.Version;
                WallColliders.Clear();
            }
        }
    }
}
