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

        private static readonly HashSet<Door> DoorsSeen = new HashSet<Door>();
        private static readonly Collider[] Near = new Collider[64];
        private static float[] ringMet = new float[16];

        /// <summary>
        /// With the Hoarstone's guarding (card B26): whether a door or gate of the pen around a point stands open. The pen's
        /// own ring is where its looks meet its walls: along each of <paramref name="directions"/> level directions, at the
        /// pen test's three heights, the nearest piece that fences a pen within <paramref name="reach"/>. A door within 3 m
        /// of a meeting point is one of its pen's (a closed gate is met itself); an open gate lets its look through, so
        /// where a look meets nothing, the ring is taken as far off as its neighbours' walls, and a door within 3 m of that
        /// counts too. Open when its record says so (state 1 or -1, research B section 6). Small queries only, never one
        /// over the whole pen, which a dense base would fill (code review 1, m5; the last look).
        /// </summary>
        internal static bool OpenDoorInRing(Vector3 here, float floor, float reach, int directions)
        {
            if (ringMet.Length < directions)
            {
                ringMet = new float[directions];
            }
            for (int i = 0; i < directions; i++)
            {
                HealRules.Direction(i, directions, out float x, out float z);
                ringMet[i] = FirstPenWall(here, floor, new Vector3(x, 0f, z), reach);
            }
            DoorsSeen.Clear();
            try
            {
                float height = floor + HealRules.LookHeights[HealRules.LookHeights.Length / 2];
                for (int i = 0; i < directions; i++)
                {
                    HealRules.Direction(i, directions, out float x, out float z);
                    float met = ringMet[i];
                    if (met >= 0f && OpenDoorNear(new Vector3(here.x + x * met, height, here.z + z * met)))
                    {
                        return true;   // a gate the look met (closed ones are; an open leaf can be too)
                    }
                    // An opening: a look that met nothing, or a wall much farther than the ring on both sides (through
                    // the gate to a farther wall). The ring there is where the nearest looks on each side, up to two
                    // away, met the walls (the last look: a wide gate lets two looks through).
                    float before = NearestMet(i, -1, directions), after = NearestMet(i, 1, directions);
                    if (before < 0f || after < 0f || (met >= 0f && met <= Mathf.Max(before, after) + 3f))
                    {
                        continue;
                    }
                    float ring = (before + after) / 2f;
                    if (OpenDoorNear(new Vector3(here.x + x * ring, height, here.z + z * ring)))
                    {
                        return true;
                    }
                }
                return false;
            }
            finally
            {
                DoorsSeen.Clear();
            }
        }

        // The distance the nearest look on one side, up to two directions away, met a wall at (-1: none did).
        private static float NearestMet(int index, int step, int directions)
        {
            for (int k = 1; k <= 2; k++)
            {
                float met = ringMet[((index + step * k) % directions + directions) % directions];
                if (met >= 0f)
                {
                    return met;
                }
            }
            return -1f;
        }

        // An open door within 3 m of a point.
        private static bool OpenDoorNear(Vector3 at)
        {
            int count = Physics.OverlapSphereNonAlloc(at, 3f, Near, PieceMask, QueryTriggerInteraction.Ignore);
            for (int j = 0; j < count && j < Near.Length; j++)
            {
                Door door = Near[j] != null ? Near[j].GetComponentInParent<Door>() : null;
                if (door == null || !DoorsSeen.Add(door) || !IsWall(Near[j]))
                {
                    continue;
                }
                ZNetView view = door.GetComponent<ZNetView>();
                ZDO record = view != null && view.IsValid() ? view.GetZDO() : null;
                if (record != null && record.GetInt(ZDOVars.s_state) != 0)
                {
                    return true;
                }
            }
            return false;
        }

        // How far a level look meets a piece that fences a pen, at the pen test's three heights (a rail fence lets one
        // through): the nearest, or -1 for none within reach.
        private static float FirstPenWall(Vector3 here, float floor, Vector3 way, float reach)
        {
            float nearest = float.MaxValue;
            foreach (float height in HealRules.LookHeights)
            {
                Vector3 start = new Vector3(here.x, floor + height, here.z);
                int count = Physics.RaycastNonAlloc(start, way, Hits, reach, PieceMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count && i < Hits.Length; i++)
                {
                    if (Hits[i].distance < nearest && IsPenWall(Hits[i].collider))
                    {
                        nearest = Hits[i].distance;
                    }
                }
            }
            return nearest < float.MaxValue ? nearest : -1f;
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
