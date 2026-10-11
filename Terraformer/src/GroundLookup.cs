using System.Collections.Generic;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// His game's copy of the ground, read point by point for the preview: each zone's heights as drawn (its Heightmap)
    /// and the changes it keeps (its terrain compiler's level and smoothing changes and paint flags). Zones are 64 m, with
    /// a point every metre on whole-metre world coordinates and the border points in both (research D, section 1); a zone
    /// is looked up once a frame, and one that isn't loaded, or isn't on the usual one-metre grid, reads as nothing.
    /// </summary>
    internal static class GroundLookup
    {
        private const int ZoneSize = 64;
        private const int Half = ZoneSize / 2;

        private sealed class Zone
        {
            internal Heightmap Map;
            internal List<float> Heights;
            internal float[] Level;
            internal float[] Smooth;
            internal bool[] Painted;
            internal int OriginX;
            internal int OriginZ;
            internal float Y;
        }

        private static readonly Dictionary<long, Zone> Zones = new Dictionary<long, Zone>();
        private static int frame = -1;

        /// <summary>The reader the preview asks.</summary>
        internal static readonly GroundReader Reader = Read;

        private static bool Read(int x, int z, out PointState state)
        {
            state = default;
            if (frame != Time.frameCount)
            {
                Zones.Clear();
                frame = Time.frameCount;
            }
            int zoneX = FloorDiv(x + Half, ZoneSize);
            int zoneZ = FloorDiv(z + Half, ZoneSize);
            long key = ((long)zoneX << 32) ^ (uint)zoneZ;
            if (!Zones.TryGetValue(key, out Zone zone))
            {
                zone = Find(zoneX, zoneZ);
                Zones[key] = zone;
            }
            if (zone == null)
            {
                return false;
            }
            int column = x - zone.OriginX;
            int row = z - zone.OriginZ;
            int pitch = ZoneSize + 1;
            int index = row * pitch + column;
            if (column < 0 || row < 0 || column > ZoneSize || row > ZoneSize || index >= zone.Heights.Count)
            {
                return false;
            }
            state.Height = zone.Heights[index] + zone.Y;
            if (zone.Level != null && index < zone.Level.Length && index < zone.Smooth.Length)
            {
                state.Level = zone.Level[index];
                state.Smooth = zone.Smooth[index];
            }
            if (zone.Painted != null && index < zone.Painted.Length)
            {
                state.Painted = zone.Painted[index];
            }
            return true;
        }

        private static Zone Find(int zoneX, int zoneZ)
        {
            var centre = new Vector3(zoneX * ZoneSize, 0f, zoneZ * ZoneSize);
            List<Heightmap> all = Heightmap.GetAllHeightmaps();
            if (all == null)
            {
                return null;
            }
            foreach (Heightmap map in all)
            {
                if (map == null || map.IsDistantLod || map.m_width != ZoneSize || Mathf.Abs(map.m_scale - 1f) > 1e-4f)
                {
                    continue;
                }
                Vector3 position = map.transform.position;
                if (Mathf.Abs(position.x - centre.x) > 0.01f || Mathf.Abs(position.z - centre.z) > 0.01f)
                {
                    continue;
                }
                List<float> heights = GameHooks.Heights(map);
                if (heights == null || heights.Count < (ZoneSize + 1) * (ZoneSize + 1))
                {
                    return null;   // not drawn yet
                }
                var zone = new Zone
                {
                    Map = map,
                    Heights = heights,
                    OriginX = zoneX * ZoneSize - Half,
                    OriginZ = zoneZ * ZoneSize - Half,
                    Y = position.y,
                };
                TerrainComp compiler = TerrainComp.FindTerrainCompiler(centre);
                if (compiler != null)
                {
                    zone.Level = GameHooks.LevelDelta(compiler);
                    zone.Smooth = GameHooks.SmoothDelta(compiler);
                    zone.Painted = GameHooks.ModifiedPaint(compiler);
                    if (zone.Level == null || zone.Smooth == null)
                    {
                        zone.Level = null;
                        zone.Smooth = null;
                    }
                }
                return zone;
            }
            return null;
        }

        private static int FloorDiv(int value, int by) => value >= 0 ? value / by : -((-value + by - 1) / by);
    }
}
