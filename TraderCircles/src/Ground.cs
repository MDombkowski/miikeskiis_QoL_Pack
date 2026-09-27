using UnityEngine;

namespace TraderCircles
{
    /// <summary>
    /// The ground where one trader can appear in this world, as far as distance from the world's centre goes: the
    /// distances its camp allows (the game's own data for that camp), and the distances at which its kind of ground can
    /// exist at all (the game's world generator: meadows end about 5000 m out, swamps lie between 2000 m and 6000 m, the
    /// Black Forest starts about 600 m out, and none of them lies in the Ashlands or the Deep North). The kind of ground
    /// itself comes in patches too small and ragged to follow, so only these limits count.
    /// Along any line straight out from the world's centre, this ground is one unbroken stretch: from its inner limit to
    /// its outer one. A circle's shape is cut to it along such lines (Clip).
    /// Positions here are the world's x and z, as a Vector2.
    /// </summary>
    internal sealed class Ground
    {
        /// <summary>Where the sea begins for good (WorldGenerator.waterEdge): no camp lies beyond it.</summary>
        private const float WaterEdge = 10500f;

        /// <summary>How many points along a line are looked at before the ends of the ground on it are narrowed down.</summary>
        private const int LookAt = 24;

        private readonly float minDistance;
        private readonly float maxDistance;
        private readonly bool meadows;
        private readonly bool blackForest;
        private readonly bool swamp;
        private readonly bool kindsKnown;
        private readonly float maxMarshDistance;

        private Ground(ZoneSystem.ZoneLocation camp, WorldGenerator generator)
        {
            minDistance = Mathf.Max(0f, Mathf.Max(camp.m_minDistance, camp.m_minDistanceFromCenter));
            maxDistance = WaterEdge;
            if (camp.m_maxDistance > 0f)
            {
                maxDistance = Mathf.Min(maxDistance, camp.m_maxDistance);
            }
            if (camp.m_maxDistanceFromCenter > 0f)
            {
                maxDistance = Mathf.Min(maxDistance, camp.m_maxDistanceFromCenter);
            }

            Heightmap.Biome kinds = camp.m_biome;
            meadows = (kinds & Heightmap.Biome.Meadows) != 0;
            blackForest = (kinds & Heightmap.Biome.BlackForest) != 0;
            swamp = (kinds & Heightmap.Biome.Swamp) != 0;
            // Only these three kinds are worked out here. A camp that may also stand on another kind (a later game
            // update) is held only to its distances, which never rules out ground where it can be.
            Heightmap.Biome known = Heightmap.Biome.Meadows | Heightmap.Biome.BlackForest | Heightmap.Biome.Swamp;
            kindsKnown = kinds != Heightmap.Biome.None && (kinds & ~known) == 0;
            maxMarshDistance = GameHooks.MaxMarshDistance(generator);
        }

        /// <summary>The ground for a trader in the world being played, or null if the game has no such camp.</summary>
        internal static Ground For(Trader trader)
        {
            if (ZoneSystem.instance == null || WorldGenerator.instance == null)
            {
                return null;
            }
            foreach (ZoneSystem.ZoneLocation camp in ZoneSystem.instance.m_locations)
            {
                // The game sets m_prefabName to the camp's asset name when it starts (ZoneSystem.SetupLocations).
                if (camp != null && camp.m_enable && camp.m_prefabName == trader.LocationName)
                {
                    return new Ground(camp, WorldGenerator.instance);
                }
            }
            return null;
        }

        /// <summary>Whether the trader could appear at this point, by the limits above.</summary>
        internal bool Allows(float x, float z)
        {
            float distance = Mathf.Sqrt(x * x + z * z);
            if (distance < minDistance || distance > maxDistance)
            {
                return false;
            }
            if (!kindsKnown)
            {
                return true;
            }
            // The world generator's own tests, in its order: nothing of these kinds lies in the two far regions.
            if (WorldGenerator.IsAshlands(x, z) || WorldGenerator.IsDeepnorth(x, z))
            {
                return false;
            }
            // The limits of meadows and the Black Forest move in and out by up to 100 m, 20 times around the world.
            float wave = WorldGenerator.WorldAngle(x, z) * 100f;
            return (meadows && distance <= 5000f + wave)
                || (blackForest && distance > 600f + wave)
                || (swamp && distance > 2000f && distance < maxMarshDistance);
        }

        internal bool Allows(Vector2 point) => Allows(point.x, point.y);

        /// <summary>
        /// Along the line straight out from the world's centre in the direction given, the part of the stretch from
        /// distance near to distance far that lies on this ground: from lo to hi. False if none of it does. Its ends are
        /// found to within a few centimetres.
        /// </summary>
        internal bool Clip(Vector2 direction, float near, float far, out float lo, out float hi)
        {
            lo = hi = 0f;
            int first = -1;
            int last = -1;
            for (int i = 0; i <= LookAt; i++)
            {
                if (Allows(direction * Mathf.Lerp(near, far, (float)i / LookAt)))
                {
                    if (first < 0)
                    {
                        first = i;
                    }
                    last = i;
                }
            }
            if (first < 0)
            {
                return false;
            }
            lo = first == 0 ? near : Edge(direction, Mathf.Lerp(near, far, (float)(first - 1) / LookAt), Mathf.Lerp(near, far, (float)first / LookAt));
            hi = last == LookAt ? far : Edge(direction, Mathf.Lerp(near, far, (float)(last + 1) / LookAt), Mathf.Lerp(near, far, (float)last / LookAt));
            return true;
        }

        // Between a distance off this ground and one on it, along a line from the centre: where the ground begins,
        // narrowed to within a few centimetres and given on the ground's side.
        private float Edge(Vector2 direction, float off, float on)
        {
            for (int i = 0; i < 16; i++)
            {
                float middle = (off + on) / 2f;
                if (Allows(direction * middle))
                {
                    on = middle;
                }
                else
                {
                    off = middle;
                }
            }
            return on;
        }
    }
}
