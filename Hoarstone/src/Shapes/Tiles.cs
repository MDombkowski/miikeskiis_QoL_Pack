using System;
using System.Collections.Generic;

namespace Hoarstone.Shapes
{
    /// <summary>A zone of the game's 64 m grid, by its numbers (ZoneSystem.GetZone: the zone (x, z) spans 32 m each way
    /// from (64x, 64z)).</summary>
    internal readonly struct ZoneId : IEquatable<ZoneId>
    {
        internal readonly int X;
        internal readonly int Z;

        internal ZoneId(int x, int z)
        {
            X = x;
            Z = z;
        }

        internal static ZoneId At(double x, double z) => new ZoneId(Tiles.ZoneOf(x), Tiles.ZoneOf(z));

        public bool Equals(ZoneId other) => X == other.X && Z == other.Z;

        public override bool Equals(object obj) => obj is ZoneId other && Equals(other);

        public override int GetHashCode() => unchecked(X * 73856093 ^ Z * 19349663);

        public override string ToString() => $"({X}, {Z})";
    }

    /// <summary>
    /// Where a tile's squares of one layer came from (review rounds 2 and 3): the box round the pieces of a settlement
    /// whose shape put them there, in whole metres; the oldest and the newest of the times (the server's, in seconds)
    /// its writer last held each zone within the join distance of the box, as of the round of working out it wrote from;
    /// and the writer's session (ZDOMan.GetSessionID, new at every join of a world). A game may overrule those squares
    /// when it wrote them itself this session and knows the box at least as well as then, or when it has held every zone
    /// of the box since the writer's newest time: only then can it know every piece the writer knew (a piece taken down
    /// reaches every game at once; a piece built reaches only the games holding its zone).
    /// </summary>
    internal struct TileSource
    {
        internal int X0;
        internal int Z0;
        internal int X1;
        internal int Z1;
        internal long Oldest;
        internal long Newest;
        internal long Writer;

        internal TileSource(int x0, int z0, int x1, int z1, long oldest, long newest, long writer)
        {
            X0 = x0;
            Z0 = z0;
            X1 = x1;
            Z1 = z1;
            Oldest = oldest;
            Newest = newest;
            Writer = writer;
        }

        /// <summary>The box round a settlement's pieces, in whole metres (outward); no times yet.</summary>
        internal static TileSource Of(Settlement s) =>
            new TileSource(Clamp(Math.Floor(s.MinX)), Clamp(Math.Floor(s.MinZ)), Clamp(Math.Ceiling(s.MaxX)), Clamp(Math.Ceiling(s.MaxZ)), 0, 0, 0);

        private static int Clamp(double metres) => (int)Math.Max(short.MinValue, Math.Min(short.MaxValue, metres));

        internal bool SameBox(TileSource other) => X0 == other.X0 && Z0 == other.Z0 && X1 == other.X1 && Z1 == other.Z1;

        internal long Area => (long)(X1 - X0 + 1) * (Z1 - Z0 + 1);

        /// <summary>One source for two: the box round both, the older oldest and the newer newest time, and the writer
        /// only where both had the same.</summary>
        internal static TileSource Union(TileSource a, TileSource b) =>
            new TileSource(Math.Min(a.X0, b.X0), Math.Min(a.Z0, b.Z0), Math.Max(a.X1, b.X1), Math.Max(a.Z1, b.Z1),
                           Math.Min(a.Oldest, b.Oldest), Math.Max(a.Newest, b.Newest), a.Writer == b.Writer ? a.Writer : 0);

        public override string ToString() => $"[{X0},{Z0}..{X1},{Z1} {Oldest}..{Newest} by {Writer}]";
    }

    /// <summary>How well a game knows a box: the oldest and the newest of the times it last held each zone within the
    /// join distance of it (long.MinValue as the oldest where it never held one of them).</summary>
    internal struct Held
    {
        internal long Oldest;
        internal long Newest;

        internal Held(long oldest, long newest)
        {
            Oldest = oldest;
            Newest = newest;
        }
    }

    /// <summary>One zone's squares: two layers of bits, one bit a square, row by row from the zone's lowest x and z, and
    /// for each layer the settlements its squares came from.</summary>
    internal sealed class Tile
    {
        /// <summary>Squares along each side of the zone: 64 at 1 m, 32 at 2 m, 16 at 4 m.</summary>
        internal readonly int Side;

        internal readonly ulong[][] Layers;

        internal readonly List<TileSource>[] Sources = { new List<TileSource>(), new List<TileSource>() };

        internal Tile(int side)
        {
            Side = side;
            int words = (side * side + 63) / 64;
            Layers = new[] { new ulong[words], new ulong[words] };
        }

        internal bool Get(int layer, int i, int j)
        {
            int k = j * Side + i;
            return (Layers[layer][k >> 6] & (1UL << (k & 63))) != 0;
        }

        internal void Set(int layer, int i, int j)
        {
            int k = j * Side + i;
            Layers[layer][k >> 6] |= 1UL << (k & 63);
        }

        internal bool Any(int layer)
        {
            foreach (ulong word in Layers[layer])
            {
                if (word != 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether two tiles hold the same squares (their sources aside).</summary>
        internal bool SameAs(Tile other)
        {
            if (other == null || other.Side != Side)
            {
                return false;
            }
            for (int l = 0; l < 2; l++)
            {
                if (!SameLayer(other, l))
                {
                    return false;
                }
            }
            return true;
        }

        internal bool SameLayer(Tile other, int layer)
        {
            for (int w = 0; w < Layers[layer].Length; w++)
            {
                if (Layers[layer][w] != other.Layers[layer][w])
                {
                    return false;
                }
            }
            return true;
        }

        internal Tile Copy()
        {
            var copy = new Tile(Side);
            for (int l = 0; l < 2; l++)
            {
                Array.Copy(Layers[l], copy.Layers[l], Layers[l].Length);
                copy.Sources[l].AddRange(Sources[l]);
            }
            return copy;
        }
    }

    /// <summary>
    /// Zones, squares and the tile saved on each zone's controller record (the plan's "Saved tiles: the rule", as review
    /// rounds 1 to 4 left it; A88's decision 13). Pure arithmetic: the harness checks every rule here.
    /// </summary>
    internal static class Tiles
    {
        internal const double ZoneSize = 64;

        /// <summary>The layer of settlements holding a Hoarstone.</summary>
        internal const int StoneLayer = 0;

        /// <summary>The layer of settlements without one.</summary>
        internal const int OtherLayer = 1;

        /// <summary>The saved tile's first byte. A tile starting with any other is left alone and never read.</summary>
        internal const byte FormatVersion = 2;

        /// <summary>The most sources a layer keeps; past it, the two whose joined box is smallest are joined, again and
        /// again (which only makes their squares harder to overrule).</summary>
        internal const int MostSources = 8;

        /// <summary>The bytes of one saved source: four 16-bit numbers and three 64-bit ones.</summary>
        internal const int SourceBytes = 32;

        internal static int ZoneOf(double coordinate) => (int)Math.Floor((coordinate + 32.0) / 64.0);

        internal static int Side(double cell) => (int)Math.Round(ZoneSize / cell);

        /// <summary>The world's square number of a zone's first square along one axis.</summary>
        internal static int FirstSquare(int zone, double cell) => (int)Math.Round((zone * ZoneSize - 32.0) / cell);

        /// <summary>The zone a world square lies in, along one axis.</summary>
        internal static int ZoneOfSquare(int square, double cell) => (int)Math.Floor((square * cell + 32.0) / ZoneSize);

        /// <summary>Spreads finished shapes into zone tiles, each settlement into its layer, with its box as a source of
        /// every zone and layer it reaches (its time left at 0: the writer sets it). Only settlements that count are
        /// spread.</summary>
        internal static Dictionary<ZoneId, Tile> Spread(IEnumerable<Settlement> settlements, double cell)
        {
            var tiles = new Dictionary<ZoneId, Tile>();
            int side = Side(cell);
            foreach (Settlement s in settlements)
            {
                if (!s.Kept || s.Shape == null)
                {
                    continue;
                }
                ShapeGrid g = s.Shape;
                TileSource source = TileSource.Of(s);
                var reached = new HashSet<ZoneId>();
                for (int j = 0; j < g.H; j++)
                {
                    int wz = g.Z0 + j;
                    int zz = ZoneOfSquare(wz, cell);
                    int lj = wz - FirstSquare(zz, cell);
                    for (int i = 0; i < g.W; i++)
                    {
                        if (g.Final[j * g.W + i] == 0)
                        {
                            continue;
                        }
                        int wx = g.X0 + i;
                        int zx = ZoneOfSquare(wx, cell);
                        var id = new ZoneId(zx, zz);
                        if (!tiles.TryGetValue(id, out Tile tile))
                        {
                            tile = new Tile(side);
                            tiles[id] = tile;
                        }
                        tile.Set(s.Layer, wx - FirstSquare(zx, cell), lj);
                        if (reached.Add(id))
                        {
                            tile.Sources[s.Layer].Add(source);
                        }
                    }
                }
            }
            return tiles;
        }

        /// <summary>Whether a point lies in a tile's square of one layer.</summary>
        internal static bool HoldsLayer(Tile tile, ZoneId zone, double x, double z, int layer)
        {
            if (tile == null)
            {
                return false;
            }
            double cell = ZoneSize / tile.Side;
            int i = (int)Math.Floor((x - (zone.X * ZoneSize - 32.0)) / cell);
            int j = (int)Math.Floor((z - (zone.Z * ZoneSize - 32.0)) / cell);
            if (i < 0 || j < 0 || i >= tile.Side || j >= tile.Side)
            {
                return false;
            }
            return tile.Get(layer, i, j);
        }

        /// <summary>Whether a point lies in a tile's square of the given layers (bit 1 the Hoarstone layer, bit 2 the
        /// other).</summary>
        internal static bool Holds(Tile tile, ZoneId zone, double x, double z, int layers) =>
            ((layers & 1) != 0 && HoldsLayer(tile, zone, x, z, StoneLayer)) || ((layers & 2) != 0 && HoldsLayer(tile, zone, x, z, OtherLayer));

        // ---------- the saved form ----------

        /// <summary>
        /// A tile as saved on the zone controller's record: the version, the squares per side, which layers hold
        /// anything, then for each layer its number of sources and the sources (each its box as four 16-bit numbers, then
        /// its oldest and newest times and its writer as 64-bit numbers, lowest byte first), then the bits of each layer
        /// that holds anything, eight squares a byte. An empty tile is five bytes.
        /// </summary>
        internal static byte[] Encode(Tile tile)
        {
            bool a = tile.Any(StoneLayer), b = tile.Any(OtherLayer);
            int bytesPerLayer = (tile.Side * tile.Side + 7) / 8;
            var data = new List<byte>(5 + 2 * bytesPerLayer) { FormatVersion, (byte)tile.Side, (byte)((a ? 1 : 0) | (b ? 2 : 0)) };
            for (int l = 0; l < 2; l++)
            {
                List<TileSource> sources = !tile.Any(l) ? new List<TileSource>() : tile.Sources[l].Count > MostSources ? Merged(tile.Sources[l]) : tile.Sources[l];
                data.Add((byte)Math.Min(sources.Count, MostSources));
                for (int k = 0; k < sources.Count && k < MostSources; k++)
                {
                    TileSource s = sources[k];
                    Short(data, s.X0);
                    Short(data, s.Z0);
                    Short(data, s.X1);
                    Short(data, s.Z1);
                    Long(data, s.Oldest);
                    Long(data, s.Newest);
                    Long(data, s.Writer);
                }
            }
            for (int l = 0; l < 2; l++)
            {
                if (!tile.Any(l))
                {
                    continue;
                }
                var bits = new byte[bytesPerLayer];
                for (int k = 0; k < tile.Side * tile.Side; k++)
                {
                    if ((tile.Layers[l][k >> 6] & (1UL << (k & 63))) != 0)
                    {
                        bits[k >> 3] |= (byte)(1 << (k & 7));
                    }
                }
                data.AddRange(bits);
            }
            return data.ToArray();
        }

        private static void Long(List<byte> data, long value)
        {
            for (int bit = 0; bit < 64; bit += 8)
            {
                data.Add((byte)(value >> bit));
            }
        }

        private static void Short(List<byte> data, int value)
        {
            short v = (short)value;
            data.Add((byte)v);
            data.Add((byte)(v >> 8));
        }

        /// <summary>A saved tile read back, or null when it isn't one this version can read (another version, a size
        /// that isn't a zone's, too many sources, or not exactly as long as it says).</summary>
        /// <remarks>A tile of an older version may be written over (<see cref="WrittenOver"/>); one of a newer version
        /// is left alone.</remarks>
        internal static Tile Decode(byte[] data)
        {
            if (data == null || data.Length < 5 || data[0] != FormatVersion)
            {
                return null;
            }
            int side = data[1];
            if (side != 16 && side != 32 && side != 64)
            {
                return null;
            }
            int flags = data[2];
            if ((flags & ~3) != 0)
            {
                return null;
            }
            var tile = new Tile(side);
            int at = 3;
            for (int l = 0; l < 2; l++)
            {
                if (at >= data.Length)
                {
                    return null;
                }
                int count = data[at++];
                if (count > MostSources || at + count * SourceBytes > data.Length)
                {
                    return null;
                }
                for (int k = 0; k < count; k++)
                {
                    int x0 = ReadShort(data, at), z0 = ReadShort(data, at + 2), x1 = ReadShort(data, at + 4), z1 = ReadShort(data, at + 6);
                    tile.Sources[l].Add(new TileSource(x0, z0, x1, z1, ReadLong(data, at + 8), ReadLong(data, at + 16), ReadLong(data, at + 24)));
                    at += SourceBytes;
                }
            }
            int bytesPerLayer = (side * side + 7) / 8;
            int expected = at + ((flags & 1) != 0 ? bytesPerLayer : 0) + ((flags & 2) != 0 ? bytesPerLayer : 0);
            if (data.Length != expected)
            {
                return null;
            }
            for (int l = 0; l < 2; l++)
            {
                if ((flags & (1 << l)) == 0)
                {
                    tile.Sources[l].Clear();
                    continue;
                }
                for (int k = 0; k < side * side; k++)
                {
                    if ((data[at + (k >> 3)] & (1 << (k & 7))) != 0)
                    {
                        tile.Layers[l][k >> 6] |= 1UL << (k & 63);
                    }
                }
                at += bytesPerLayer;
            }
            return tile;
        }

        /// <summary>Whether saved bytes this version can't read may be written over: only those of an older format
        /// (review round 4), never a newer one's.</summary>
        internal static bool WrittenOver(byte[] data) => data == null || data.Length == 0 || data[0] < FormatVersion;

        private static int ReadShort(byte[] data, int at) => (short)(data[at] | (data[at + 1] << 8));

        private static long ReadLong(byte[] data, int at)
        {
            long value = 0;
            for (int bit = 0; bit < 64; bit += 8)
            {
                value |= (long)data[at + bit / 8] << bit;
            }
            return value;
        }

        // ---------- redrawing at another square size ----------

        /// <summary>Copies one layer's squares into a tile of any square size, adding to what it holds. To a coarser
        /// size a square is set where any square under it is (review round 2: the "only adds" rule never loses a square
        /// at an edge); to a finer or the same size, where the square under its middle is.</summary>
        internal static void CopyLayer(Tile from, int layer, Tile to)
        {
            if (from.Side == to.Side)
            {
                for (int w = 0; w < to.Layers[layer].Length; w++)
                {
                    to.Layers[layer][w] |= from.Layers[layer][w];
                }
                return;
            }
            if (from.Side > to.Side)
            {
                int per = from.Side / to.Side;
                for (int fj = 0; fj < from.Side; fj++)
                {
                    for (int fi = 0; fi < from.Side; fi++)
                    {
                        if (from.Get(layer, fi, fj))
                        {
                            to.Set(layer, fi / per, fj / per);
                        }
                    }
                }
                return;
            }
            for (int j = 0; j < to.Side; j++)
            {
                int fj = (int)((j + 0.5) * from.Side / to.Side);
                for (int i = 0; i < to.Side; i++)
                {
                    int fi = (int)((i + 0.5) * from.Side / to.Side);
                    if (from.Get(layer, fi, fj))
                    {
                        to.Set(layer, i, j);
                    }
                }
            }
        }

        /// <summary>One layer of a tile redrawn at another square size (a copy at the same size), with its sources.</summary>
        internal static Tile Redrawn(Tile from, int side)
        {
            var to = new Tile(side);
            for (int l = 0; l < 2; l++)
            {
                CopyLayer(from, l, to);
                to.Sources[l].AddRange(from.Sources[l]);
            }
            return to;
        }

        /// <summary>Sources with the same box and the same writer kept once (the older oldest time, the newer newest);
        /// past <see cref="MostSources"/>, two are joined, again and again: two of the same writer first, else the two
        /// whose joined box is smallest (review round 4: sources of different writers kept apart, so each game needs only
        /// the other's newest time).</summary>
        internal static List<TileSource> Merged(List<TileSource> sources)
        {
            var kept = new List<TileSource>();
            foreach (TileSource s in sources)
            {
                int at = kept.FindIndex(k => k.SameBox(s) && k.Writer == s.Writer);
                if (at < 0)
                {
                    kept.Add(s);
                }
                else
                {
                    kept[at] = TileSource.Union(kept[at], s);
                }
            }
            while (kept.Count > MostSources)
            {
                int bestA = 0, bestB = 1;
                bool bestSame = false;
                long bestArea = long.MaxValue;
                for (int a = 0; a < kept.Count; a++)
                {
                    for (int b = a + 1; b < kept.Count; b++)
                    {
                        bool same = kept[a].Writer == kept[b].Writer;
                        long area = TileSource.Union(kept[a], kept[b]).Area;
                        if ((same && !bestSame) || (same == bestSame && area < bestArea))
                        {
                            bestSame = same;
                            bestArea = area;
                            bestA = a;
                            bestB = b;
                        }
                    }
                }
                kept[bestA] = TileSource.Union(kept[bestA], kept[bestB]);
                kept.RemoveAt(bestB);
            }
            return kept;
        }

        // ---------- what this game may do with a saved tile ----------

        /// <summary>A few seconds of slack on a writer's newest time: a record may reach a game a moment after the
        /// server's time it was held at.</summary>
        internal const long Slack = 10;

        /// <summary>
        /// Whether this game can account for a saved tile's squares of one layer (review rounds 3 and 4): for every
        /// settlement they came from, it holds every zone of the box, and either it wrote them itself this session
        /// (<paramref name="session"/>), or it has held every zone of the box since the writer's newest time (with a few
        /// seconds' slack). <paramref name="held"/> gives how well this game knows a box. An empty layer, or no saved
        /// tile, is accounted for; squares without sources never are.
        /// </summary>
        internal static bool Accounts(Tile saved, int layer, Func<TileSource, Held> held, long session)
        {
            if (saved == null || !saved.Any(layer))
            {
                return true;
            }
            if (saved.Sources[layer].Count == 0)
            {
                return false;   // squares from nowhere: nothing can overrule them
            }
            foreach (TileSource s in saved.Sources[layer])
            {
                long oldest = held(s).Oldest;
                if (oldest == long.MinValue)
                {
                    return false;
                }
                // Its own squares of this session: its records have only grown since it wrote them (a piece taken down
                // reaches it, a zone it lost reads as never held above), whatever the clock says (review round 4).
                bool own = s.Writer != 0 && s.Writer == session;
                if (!own && (s.Newest > long.MaxValue - Slack || oldest < s.Newest + Slack))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Whether the spawn check (and the drawing) must look at a layer of the saved tile too, not this game's own shape
        /// alone: unless this game's records are whole for the zone and it can account for the saved squares of that
        /// layer. A game hosting the world too, through the round's time (a client's squares written after its round are
        /// kept until its next).
        /// </summary>
        internal static bool UseSaved(bool whole, bool accounts) => !(whole && accounts);

        /// <summary>
        /// What this game writes on a zone's controller (it owns the controller), or null for nothing:
        /// - **nothing** where its records aren't whole, the saved tile is of a format it can't read (left alone), or it
        ///   doesn't know one of its own settlements' boxes (it never writes a time it doesn't have);
        /// - **per layer, its own squares exactly** where it can account for the saved squares of that layer: then squares
        ///   may go (a wall or a settlement taken down, a Hoarstone removed, a margin lowered), and the layer's sources are
        ///   its own settlements' boxes, each with how well it knows it and its session;
        /// - **else its own squares joined with the saved ones**, with both sets of sources: it only adds;
        /// - the layer of settlements without a Hoarstone is kept exactly as saved by a game set to Hoarstones only.
        /// Nothing is written when the squares and the sources' boxes are as saved (times alone never rewrite a tile),
        /// nor an empty tile where nothing was saved.
        /// </summary>
        internal static Tile WriteFor(Tile own, Tile saved, bool savedUnreadable, bool whole, Func<TileSource, Held> held,
                                      long session, bool writeOtherLayer, double cell)
        {
            if (savedUnreadable || !whole)
            {
                return null;
            }
            int side = Side(cell);
            Tile ownHere = own == null ? null : own.Side == side ? own : Redrawn(own, side);
            Tile savedHere = saved == null ? null : saved.Side == side ? saved : Redrawn(saved, side);
            var tile = new Tile(side);
            for (int l = 0; l < 2; l++)
            {
                if (l == OtherLayer && !writeOtherLayer)
                {
                    if (savedHere != null)
                    {
                        CopyLayer(savedHere, l, tile);
                        if (savedHere.Any(l))
                        {
                            tile.Sources[l].AddRange(savedHere.Sources[l]);
                        }
                    }
                    continue;
                }
                var ownSources = new List<TileSource>();
                if (ownHere != null && ownHere.Any(l))
                {
                    CopyLayer(ownHere, l, tile);
                    foreach (TileSource s in ownHere.Sources[l])
                    {
                        Held h = held(s);
                        if (h.Oldest == long.MinValue)
                        {
                            return null;
                        }
                        ownSources.Add(new TileSource(s.X0, s.Z0, s.X1, s.Z1, h.Oldest, h.Newest, session));
                    }
                }
                if (Accounts(saved, l, held, session) || savedHere == null)
                {
                    tile.Sources[l].AddRange(Merged(ownSources));
                    continue;
                }
                CopyLayer(savedHere, l, tile);
                var both = new List<TileSource>(savedHere.Any(l) ? savedHere.Sources[l] : new List<TileSource>());
                both.AddRange(ownSources);
                tile.Sources[l].AddRange(Merged(both));
            }
            for (int l = 0; l < 2; l++)
            {
                if (!tile.Any(l))
                {
                    tile.Sources[l].Clear();
                }
            }
            if (saved == null)
            {
                return tile.Any(StoneLayer) || tile.Any(OtherLayer) ? tile : null;
            }
            return SameSquaresAndBoxes(tile, savedHere) ? null : tile;
        }

        // Whether a tile to write holds what the saved one does: the same squares (at this size) and, per layer, the same
        // set of source boxes. Times aside: a newer knowledge of the same squares rewrites nothing.
        private static bool SameSquaresAndBoxes(Tile tile, Tile savedHere)
        {
            if (!tile.SameAs(savedHere))
            {
                return false;
            }
            for (int l = 0; l < 2; l++)
            {
                // The same set of boxes, both ways (the harness, round 3): a box may stand twice, once per writer, so a
                // count would make two games that agree on every square rewrite each other's tile for ever.
                List<TileSource> a = tile.Sources[l], b = savedHere.Any(l) ? savedHere.Sources[l] : new List<TileSource>();
                if (!a.TrueForAll(s => b.Exists(o => o.SameBox(s))) || !b.TrueForAll(o => a.Exists(s => s.SameBox(o))))
                {
                    return false;
                }
            }
            return true;
        }

        // ---------- where this game's records are whole ----------

        /// <summary>
        /// The zones where this game knows everything its shape there depends on: every zone within <c>pad + eps</c> of
        /// the zone has been seen, and every settlement whose shape could reach the zone (its box grown by the pad) has
        /// every zone within eps of its box seen. Settlements that don't count are checked too: a few pieces seen may be a
        /// large settlement not yet seen. <paramref name="candidates"/> are the zones to judge.
        /// </summary>
        internal static HashSet<ZoneId> Whole(IEnumerable<ZoneId> candidates, Func<ZoneId, bool> seen, IList<Settlement> settlements, ShapeOptions o)
        {
            double pad = Settlements.Pad(o);
            var notWhole = new HashSet<ZoneId>();
            foreach (Settlement s in settlements)
            {
                // The same rounded box a source of it would carry (review round 3), so a game that writes a settlement
                // always knows every zone its source's time is taken over.
                TileSource box = TileSource.Of(s);
                if (AllSeen(box.X0 - o.Eps, box.Z0 - o.Eps, box.X1 + o.Eps, box.Z1 + o.Eps, seen))
                {
                    continue;
                }
                ForZones(box.X0 - pad, box.Z0 - pad, box.X1 + pad, box.Z1 + pad, id => notWhole.Add(id));
            }
            var whole = new HashSet<ZoneId>();
            double reach = pad + o.Eps;
            foreach (ZoneId id in candidates)
            {
                if (notWhole.Contains(id))
                {
                    continue;
                }
                double x0 = id.X * ZoneSize - 32.0, z0 = id.Z * ZoneSize - 32.0;
                if (AllSeen(x0 - reach, z0 - reach, x0 + ZoneSize + reach, z0 + ZoneSize + reach, seen))
                {
                    whole.Add(id);
                }
            }
            return whole;
        }

        /// <summary>How well this game knows a box: the oldest and the newest of the times it last held each zone within
        /// <paramref name="eps"/> of it (<paramref name="heldAt"/>: long.MinValue for a zone never held).</summary>
        internal static Held Knowledge(TileSource box, double eps, Func<ZoneId, long> heldAt)
        {
            long oldest = long.MaxValue, newest = long.MinValue;
            ForZones(box.X0 - eps, box.Z0 - eps, box.X1 + eps, box.Z1 + eps, id =>
            {
                long at = heldAt(id);
                if (at < oldest)
                {
                    oldest = at;
                }
                if (at > newest)
                {
                    newest = at;
                }
            });
            return new Held(oldest, newest);
        }

        private static bool AllSeen(double x0, double z0, double x1, double z1, Func<ZoneId, bool> seen)
        {
            bool all = true;
            ForZones(x0, z0, x1, z1, id =>
            {
                if (all && !seen(id))
                {
                    all = false;
                }
            });
            return all;
        }

        /// <summary>Every zone a box touches (its edges included).</summary>
        internal static void ForZones(double x0, double z0, double x1, double z1, Action<ZoneId> each)
        {
            int ax = ZoneOf(x0), az = ZoneOf(z0), bx = ZoneOf(x1), bz = ZoneOf(z1);
            for (int z = az; z <= bz; z++)
            {
                for (int x = ax; x <= bx; x++)
                {
                    each(new ZoneId(x, z));
                }
            }
        }
    }
}
