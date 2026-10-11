using System;
using System.Collections.Generic;
using Hoarstone.Shapes;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>One zone's land and water, square by square, read from its loaded heightmap (hoe work included). Never
    /// changed once made: a new reading is a new tile, so a worker may hold one safely.</summary>
    internal sealed class LandTile
    {
        private static int made;

        internal readonly int Side;
        internal readonly bool[] IsLand;

        /// <summary>A number of its own, different for every tile made (a settlement's signature names the tiles it read).</summary>
        internal readonly int Serial;

        internal LandTile(int side, bool[] isLand)
        {
            Side = side;
            IsLand = isLand;
            Serial = ++made;
        }

        internal bool SameAs(LandTile other)
        {
            if (other == null || other.Side != Side)
            {
                return false;
            }
            for (int k = 0; k < IsLand.Length; k++)
            {
                if (IsLand[k] != other.IsLand[k])
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Land or water for each square, against the game's water line (30 m), as the shape needs it (the plan's "Land"):
    /// - **Exact** where this game has loaded the zone's heightmap this session: read from it (Heightmap.GetHeight), whose
    ///   heights hold every terrain edit, so ground raised or dug with the hoe counts. Kept for the session after the zone
    ///   unloads, and read again every few seconds while it's loaded, so new hoe work shows.
    /// - **The world's own height** (WorldGenerator.GetHeight, without terrain edits) anywhere else, worked out on the worker
    ///   thread as the game's own heightmap builder does.
    /// One zone is read a frame, at most, in turn among those loaded around the player.
    /// </summary>
    internal static class Land
    {
        private const float ReadEvery = 0.2f;
        private const int Around = 2;   // without a game: the zones the game is sent records for, by default

        private static Dictionary<ZoneId, LandTile> tiles = new Dictionary<ZoneId, LandTile>();
        private static double forCell;
        private static float nextRead;
        private static int turn;

        /// <summary>Goes up whenever a zone's land changes.</summary>
        internal static int Version { get; private set; }

        /// <summary>A copy of the exact tiles known, for the worker (the tiles themselves never change).</summary>
        internal static Dictionary<ZoneId, LandTile> Snapshot() => new Dictionary<ZoneId, LandTile>(tiles);

        internal static void Tick(float now)
        {
            double cell = Settings.Cell;
            if (cell != forCell)
            {
                forCell = cell;
                tiles = new Dictionary<ZoneId, LandTile>();
                Version++;
            }
            if (now < nextRead)
            {
                return;
            }
            nextRead = now + ReadEvery;
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            Vector3 at = player.transform.position;
            var here = ZoneId.At(at.x, at.z);
            int around = ZNet.instance != null ? Math.Max(0, ZNet.instance.GetSyncedSimulationDistance().NearSimulationDistance) : Around;
            int width = 2 * around + 1;
            int k = turn++ % (width * width);
            var zone = new ZoneId(here.X + k % width - around, here.Z + k / width - around);
            LandTile read = Read(zone, cell);
            if (read == null)
            {
                return;
            }
            if (!tiles.TryGetValue(zone, out LandTile before) || !before.SameAs(read))
            {
                tiles[zone] = read;
                Version++;
            }
        }

        // One zone from the loaded heightmaps, or null while any square can't be read (not loaded, or not built yet).
        private static LandTile Read(ZoneId zone, double cell)
        {
            int side = Tiles.Side(cell);
            var isLand = new bool[side * side];
            double x0 = zone.X * Tiles.ZoneSize - 32.0, z0 = zone.Z * Tiles.ZoneSize - 32.0;
            Heightmap last = null;
            try
            {
                for (int j = 0; j < side; j++)
                {
                    for (int i = 0; i < side; i++)
                    {
                        var point = new Vector3((float)(x0 + (i + 0.5) * cell), 0f, (float)(z0 + (j + 0.5) * cell));
                        float height;
                        if (last == null || !last.GetWorldHeight(point, out height))
                        {
                            last = Heightmap.FindHeightmap(point);
                            if (last == null || !last.GetWorldHeight(point, out height))
                            {
                                return null;
                            }
                        }
                        isLand[j * side + i] = height >= ShapeMath.WaterLine;
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;   // a heightmap whose heights aren't built yet: read it again later
            }
            return new LandTile(side, isLand);
        }

        /// <summary>The ground's height the shape compares with the water line at a square's middle: from an exact tile
        /// as just above or below the line, else the world's own height. Safe on the worker thread.</summary>
        internal static Func<double, double, double> GroundFor(Dictionary<ZoneId, LandTile> exact, WorldGenerator world, double cell)
        {
            return (x, z) =>
            {
                var zone = ZoneId.At(x, z);
                if (exact.TryGetValue(zone, out LandTile tile) && tile.Side == Tiles.Side(cell))
                {
                    int i = (int)Math.Floor((x - (zone.X * Tiles.ZoneSize - 32.0)) / cell);
                    int j = (int)Math.Floor((z - (zone.Z * Tiles.ZoneSize - 32.0)) / cell);
                    if (i >= 0 && j >= 0 && i < tile.Side && j < tile.Side)
                    {
                        return tile.IsLand[j * tile.Side + i] ? ShapeMath.WaterLine + 1 : ShapeMath.WaterLine - 1;
                    }
                }
                return world != null ? world.GetHeight((float)x, (float)z) : ShapeMath.WaterLine + 1;
            };
        }

        /// <summary>A number that changes when any exact tile in the box changes or appears, for a settlement's
        /// signature.</summary>
        internal static long Stamp(Dictionary<ZoneId, LandTile> exact, double x0, double z0, double x1, double z1)
        {
            long stamp = 17;
            Tiles.ForZones(x0, z0, x1, z1, zone =>
            {
                if (exact.TryGetValue(zone, out LandTile tile))
                {
                    stamp = unchecked(stamp * 31 + zone.GetHashCode() * 7 + tile.Serial);
                }
            });
            return stamp;
        }

        internal static void Forget()
        {
            tiles = new Dictionary<ZoneId, LandTile>();
            Version++;
        }
    }
}
