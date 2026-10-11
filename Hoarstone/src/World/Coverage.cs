using System.Collections.Generic;
using Hoarstone.Shapes;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// Where the shapes are, for the spawn check and the outlines, and the tiles saved on each zone's controller record
    /// (the plan's "Saved tiles: the rule", as review rounds 1 to 3 left it; the rule itself is Tiles.Accounts,
    /// Tiles.UseSaved and Tiles.WriteFor, which the harness checks):
    /// - **Where the squares came from**: each layer of a saved tile names the settlements its squares came from (their
    ///   boxes), the oldest and newest times its writer held their zones, and its writer's session. This game overrules
    ///   them only where it wrote them itself this session, or has held every zone of the box since the writer's newest
    ///   time, and its records of the zone are whole.
    /// - **As of the round**: wholeness and every time are taken from what this game held when the round of working out
    ///   took its pieces (SeenSnapshot), since its shapes know only the pieces of that moment.
    /// - **The check:** for each layer, this game's own shape, and the saved tile's squares where it can't overrule them:
    ///   a settlement too big for one game to hold, the inside of a wall it hasn't seen, a wall a friend closed while it
    ///   was away, is still kept free.
    /// - **The write:** by the zone controller's owner only (the game that runs the zone's spawning), only where its
    ///   records are whole, only when the squares or their sources' boxes changed, never over a tile of a format it can't
    ///   read; per layer exactly where it can overrule (squares may go), else joined (it only adds). The layer of
    ///   settlements without a Hoarstone is written only by a game set to every settlement.
    /// Vanilla games ignore the saved tile. An empty tile is written as empty, never removed (a removal reaches no other
    /// player). Only the zones that matter are looked at: those with a shape here, those with a saved shape, and those
    /// around the player (a game hosting the world holds every zone's controller, thousands of them).
    /// </summary>
    internal static class Coverage
    {
        /// <summary>The saved tile's key on the zone controller's record. Permanent: it's in players' worlds.</summary>
        internal static readonly int TileHash = "modprojects.qolmods.hoarstone.tile".GetStableHashCode();

        private const float WriteEvery = 2f;
        private const int Around = 3;

        private static Dictionary<ZoneId, Tile> computed = new Dictionary<ZoneId, Tile>();
        private static List<Settlement> settlements = new List<Settlement>();
        private static List<ShapePoint> points = new List<ShapePoint>();
        private static ShapeOptions options = new ShapeOptions();
        private static SeenSnapshot snapshot = new SeenSnapshot();
        private static int appliedRecords = -1, appliedLand = -1, appliedShape = -1;
        private static HashSet<ZoneId> whole = new HashSet<ZoneId>();
        private static readonly Dictionary<ZoneId, SavedTile> saved = new Dictionary<ZoneId, SavedTile>();
        private static readonly HashSet<ZoneId> savedShapes = new HashSet<ZoneId>();
        private static float nextWrite;

        private sealed class SavedTile
        {
            internal ZDO Record;
            internal uint Revision;
            internal Tile Tile;
            internal byte[] Bytes;
            internal bool Unreadable;
        }

        /// <summary>Goes up whenever what the outlines show may have changed.</summary>
        internal static int Version { get; private set; }

        internal static IReadOnlyList<Settlement> Settlements => settlements;

        internal static IReadOnlyList<ShapePoint> Points => points;

        internal static ShapeOptions Options => options;

        /// <summary>Takes a finished round of working out.</summary>
        internal static void Apply(WorkResult result)
        {
            computed = result.Tiles;
            settlements = result.Settlements;
            points = result.Points;
            options = result.Options;
            snapshot = result.Seen ?? new SeenSnapshot();
            appliedRecords = result.RecordsVersion;
            appliedLand = result.LandVersion;
            appliedShape = result.ShapeVersion;
            Judge(null);
            Version++;
            nextWrite = 0f;
        }

        internal static void Tick(float now)
        {
            if (now < nextWrite)
            {
                return;
            }
            nextWrite = now + WriteEvery;
            // While no piece, land or shape setting changed since the round, its shapes are those of the pieces now, so
            // what this game holds now may judge them (review round 4: else held times froze between rounds, and a
            // friend's squares written just before stayed until something changed).
            if (appliedRecords == Records.Version && appliedLand == Land.Version && appliedShape == Settings.ShapeVersion)
            {
                snapshot = SeenZones.Snapshot();
            }
            HashSet<ZoneId> matter = Matter();
            foreach (ZoneId zone in matter)
            {
                SavedOf(zone);   // a saved tile that changed raises Version itself
            }
            Judge(matter);
            Write(matter);
        }

        // The zones that matter: those with a shape here or a saved shape, and those around the player.
        private static HashSet<ZoneId> Matter()
        {
            var zones = new HashSet<ZoneId>(computed.Keys);
            zones.UnionWith(savedShapes);
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                Vector3 at = player.transform.position;
                var here = ZoneId.At(at.x, at.z);
                for (int dz = -Around; dz <= Around; dz++)
                {
                    for (int dx = -Around; dx <= Around; dx++)
                    {
                        zones.Add(new ZoneId(here.X + dx, here.Z + dz));
                    }
                }
            }
            return zones;
        }

        // Which zones' records were whole when the round took its pieces, among the zones that matter. A game hosting
        // the world holds every record: every zone is whole there. The outlines follow (they draw by the same rule).
        private static void Judge(HashSet<ZoneId> matter)
        {
            HashSet<ZoneId> next = snapshot.Everything
                ? new HashSet<ZoneId>()
                : Tiles.Whole(matter ?? Matter(), zone => snapshot.Seen.Contains(zone), settlements, options);
            if (!next.SetEquals(whole))
            {
                Version++;
            }
            whole = next;
        }

        private static bool IsWhole(ZoneId zone) => snapshot.Everything || whole.Contains(zone);

        /// <summary>How well this game knew a box when the round took its pieces: the round's own time everywhere on a
        /// game hosting the world, else the oldest and newest of the times it last held each zone within the join
        /// distance of it.</summary>
        private static Held Knowledge(TileSource box) =>
            snapshot.Everything
                ? new Held(snapshot.At, snapshot.At)
                : Tiles.Knowledge(box, options.Eps, zone => snapshot.Held.TryGetValue(zone, out long at) ? at : long.MinValue);

        private static long Session => ZDOMan.GetSessionID();

        // Whether this game's own shape answers for a layer of a zone alone (it can overrule the saved squares there).
        private static bool Overrules(ZoneId zone, Tile kept, int layer) =>
            !Tiles.UseSaved(IsWhole(zone), Tiles.Accounts(kept, layer, Knowledge, Session));

        /// <summary>Whether a point lies inside a shape of the given layers (bit 1 settlements with a Hoarstone, bit 2
        /// the others): the main thread's one lookup for the spawn checks.</summary>
        internal static bool Inside(Vector3 point, int layers)
        {
            var zone = ZoneId.At(point.x, point.z);
            computed.TryGetValue(zone, out Tile own);
            if (own != null && Tiles.Holds(own, zone, point.x, point.z, layers))
            {
                return true;
            }
            Tile kept = SavedOf(zone);
            if (kept == null)
            {
                return false;
            }
            for (int layer = 0; layer < 2; layer++)
            {
                if ((layers & (1 << layer)) != 0 && Tiles.HoldsLayer(kept, zone, point.x, point.z, layer) && !Overrules(zone, kept, layer))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The tile to draw for a zone, by the same rule as the check: this game's own, and, layer by layer, the
        /// saved one's squares where this game can't overrule them.</summary>
        internal static Tile Drawn(ZoneId zone)
        {
            computed.TryGetValue(zone, out Tile own);
            Tile kept = SavedOf(zone);
            if (kept == null)
            {
                return own;
            }
            var drawn = new Tile(own != null ? own.Side : kept.Side);
            for (int layer = 0; layer < 2; layer++)
            {
                if (own != null)
                {
                    Tiles.CopyLayer(own, layer, drawn);
                }
                if (!Overrules(zone, kept, layer))
                {
                    Tiles.CopyLayer(kept, layer, drawn);
                }
            }
            return drawn;
        }

        /// <summary>Every zone that has a shape here or a saved shape this game has read.</summary>
        internal static IEnumerable<ZoneId> ZonesWithShapes()
        {
            var zones = new HashSet<ZoneId>(computed.Keys);
            zones.UnionWith(savedShapes);
            return zones;
        }

        // The saved tile of a zone, read again only when its record's revision moved; Version goes up only when what it
        // holds changed (the spawn system writes its own timers into the same record every few seconds).
        private static Tile SavedOf(ZoneId zone)
        {
            ZDO record = Records.ControlOf(zone);
            if (record == null)
            {
                if (saved.Remove(zone) && savedShapes.Remove(zone))
                {
                    Version++;
                }
                return null;
            }
            if (saved.TryGetValue(zone, out SavedTile s) && s.Record == record && s.Revision == record.DataRevision)
            {
                return s.Tile;
            }
            byte[] bytes = record.GetByteArray(TileHash);
            Tile tile = Tiles.Decode(bytes);
            // Bytes of an older format may be written over (as nothing); a newer format's are left alone (review round 4).
            var read = new SavedTile
            {
                Record = record,
                Revision = record.DataRevision,
                Tile = tile,
                Bytes = bytes,
                Unreadable = bytes != null && tile == null && !Tiles.WrittenOver(bytes),
            };
            byte[] before = s?.Bytes;
            saved[zone] = read;
            bool any = tile != null && (tile.Any(Tiles.StoneLayer) || tile.Any(Tiles.OtherLayer));
            bool changed = any ? savedShapes.Add(zone) : savedShapes.Remove(zone);
            if (changed || !SameBytes(before, bytes))   // squares or sources: either may change what the outlines show
            {
                Version++;
            }
            return tile;
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null)
            {
                return a == b;
            }
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        // The zones this game owns, that matter, and whose records were whole, get their tile written when it changed.
        private static void Write(HashSet<ZoneId> matter)
        {
            if (!Module.IsOn)
            {
                return;
            }
            bool everySettlement = Settings.Where.Value == WhichSettlements.EverySettlement;
            foreach (ZoneId zone in matter)
            {
                ZDO record = Records.ControlOf(zone);
                if (record == null || !record.IsOwner() || !IsWhole(zone))
                {
                    continue;
                }
                computed.TryGetValue(zone, out Tile own);
                Tile kept = SavedOf(zone);
                bool ownAny = own != null && (own.Any(Tiles.StoneLayer) || own.Any(Tiles.OtherLayer));
                bool keptAny = kept != null && (kept.Any(Tiles.StoneLayer) || kept.Any(Tiles.OtherLayer));
                bool unreadable = saved.TryGetValue(zone, out SavedTile s) && s.Unreadable;
                if (!ownAny && !keptAny && !unreadable)
                {
                    continue;   // nothing here and nothing saved: nothing to write (most zones of a world)
                }
                Tile write = Tiles.WriteFor(own, kept, unreadable, true, Knowledge, Session, everySettlement, options.Cell);
                if (write == null)
                {
                    continue;
                }
                record.Set(TileHash, Tiles.Encode(write));
                SavedOf(zone);
                if (Settings.LogWork.Value)
                {
                    Module.Log.LogInfo($"Saved the settlement shape of zone {zone} on its controller.");
                }
            }
        }

        internal static void Forget()
        {
            computed = new Dictionary<ZoneId, Tile>();
            settlements = new List<Settlement>();
            points = new List<ShapePoint>();
            snapshot = new SeenSnapshot();
            whole = new HashSet<ZoneId>();
            saved.Clear();
            savedShapes.Clear();
            Version++;
        }
    }
}
