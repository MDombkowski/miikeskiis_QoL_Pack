using System.Collections.Generic;
using UnityEngine;

namespace Terraformer
{
    /// <summary>What one of a zone's points held: its level and smoothing changes, their flag, its paint and its flag.</summary>
    internal struct PointRecord
    {
        internal float Level;
        internal float Smooth;
        internal bool HeightChanged;
        internal Color Paint;
        internal bool Painted;

        /// <summary>Whether two records hold the same ground, as the game saves it: the changes only where their flag is on,
        /// the colour only where it's painted (a point without the flag is saved without them, and comes back from a
        /// reload without them: review round 1).</summary>
        internal bool SameAs(PointRecord other) =>
            HeightChanged == other.HeightChanged && Painted == other.Painted &&
            (!HeightChanged || (Level == other.Level && Smooth == other.Smooth)) &&
            (!Painted || (Paint.r == other.Paint.r && Paint.g == other.Paint.g && Paint.b == other.Paint.b && Paint.a == other.Paint.a));
    }

    /// <summary>
    /// Undo (his ask, 2026-09-30: "reverts the last terrain modification I made to exactly what that terrain was", up to
    /// five): his last strokes with a hoe or cultivator tool, the game's own included, each kept as the points it changed,
    /// zone by zone, with what they held before and after it.
    /// - **Recorded on his own game,** where his strokes are worked out (each zone's record is claimed first). When the
    ///   stroke reaches its first zone, every zone round it that his game holds is noted, before any is worked out; a zone
    ///   first made during the stroke is noted as it's reached (nothing can have written to it before). Once the click is
    ///   over, every noted zone is compared: the game's own paint writes into the next zone along a border (TerrainComp's
    ///   paint step), so a zone can change after its own turn (review round 1). Only the points that changed are kept.
    /// - **Undone only where nothing has changed since:** every point must still hold what the stroke left, so a friend's
    ///   stroke (or anything else) in between is never wiped; otherwise it's refused, said, and forgotten. A zone too far
    ///   away to be loaded is refused and kept, to try again closer.
    /// - **Put back as the game saves a stroke:** the zone's record claimed, its points given back, its stroke count
    ///   raised, saved, redrawn and its grass remade; the stone the stroke cost is given back.
    /// Kept for the world he's in, until he quits or leaves it. The recording is plain arrays, so the harness checks it.
    /// </summary>
    internal static class Undo
    {
        /// <summary>How far past a stroke's reach its zones' points are noted: the game measures a round stroke from the
        /// ground's point nearest it and paints round a point up to 1.41 m off (Ghost.RoundStrokeSlack), with a margin.</summary>
        internal const float Margin = 2f;

        /// <summary>One zone's part of a stroke: the zone's middle, and each changed point's index with what it held before
        /// and after.</summary>
        internal sealed class ZonePart
        {
            internal Vector3 Centre;
            internal readonly List<int> Indices = new List<int>();
            internal readonly List<PointRecord> Before = new List<PointRecord>();
            internal readonly List<PointRecord> After = new List<PointRecord>();
        }

        internal sealed class Entry
        {
            internal string Name;
            internal Vector3 Position;
            internal float Reach;
            internal bool Placed;
            internal readonly List<ZonePart> Zones = new List<ZonePart>();
            internal readonly List<(string Item, int Amount)> Cost = new List<(string, int)>();
        }

        // His strokes kept for undo, the newest last, for the world they were made in.
        private static readonly List<Entry> Kept = new List<Entry>();
        private static ZNet world;

        // The stroke being made now, and each zone's points round it as they were before it was worked out anywhere.
        private static Entry recording;
        private static readonly Dictionary<TerrainComp, (PointRecord[] Points, int[] Indices)> Noted =
            new Dictionary<TerrainComp, (PointRecord[], int[])>();
        private static readonly List<Heightmap> Around = new List<Heightmap>();
        private static readonly Dictionary<TerrainComp, PointRecord[]> SavedAs = new Dictionary<TerrainComp, PointRecord[]>();

        // The zones the stroke had a turn in (the others only took the game's paint across a border).
        private static readonly HashSet<TerrainComp> Turned = new HashSet<TerrainComp>();

        /// <summary>How many strokes are kept now.</summary>
        internal static int Count => Kept.Count;

        /// <summary>His click begins a stroke of his: its name and reach (the zones' points to note), and its cost unless
        /// the world builds for free. The game takes the cost at the click even when building costs nothing to him, from
        /// what he carries (Player.UpdatePlacement), so it's given back whenever it was asked for (review round 1).</summary>
        internal static void Start(string name, float reach, Piece piece)
        {
            Noted.Clear();
            SavedAs.Clear();
            Turned.Clear();
            recording = new Entry { Name = name, Reach = reach };
            if (piece?.m_resources == null || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey())))
            {
                return;
            }
            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                int amount = requirement?.m_resItem != null ? requirement.GetAmount(0) : 0;
                if (amount > 0)
                {
                    recording.Cost.Add((requirement.m_resItem.gameObject.name, amount));
                }
            }
        }

        /// <summary>Just before his stroke is worked out in a zone. The first time: its middle is where the stroke really
        /// is (the placed stroke's own position, not the ghost's of a frame before: review round 1), its reach the one it
        /// really carries (a square's at the turn its facing sends: review round 2), and every zone round it his game holds
        /// is noted. After that, a zone made during the stroke is noted as it's reached.</summary>
        internal static void Before(TerrainComp compiler, Vector3 position, float reach)
        {
            Entry entry = recording;
            if (entry == null || compiler == null)
            {
                return;
            }
            if (!entry.Placed)
            {
                entry.Placed = true;
                entry.Position = position;
                entry.Reach = Mathf.Max(entry.Reach, reach);
                Around.Clear();
                Heightmap.FindHeightmap(position, entry.Reach + Margin, Around);
                foreach (Heightmap map in Around)
                {
                    if (map != null && !map.IsDistantLod)
                    {
                        Note(TerrainComp.FindTerrainCompiler(map.transform.position), entry);
                    }
                }
            }
            Note(compiler, entry);
            Turned.Add(compiler);
        }

        /// <summary>Just after the game saved a zone at the end of its turn (TerrainComp.DoOperation): the zone's noted
        /// points as saved. The next zone's paint can still write across the border after that (review round 2); Finish
        /// saves the zone again then, so what's saved is what's shown, and what undo compares with.</summary>
        internal static void Saved(TerrainComp compiler, bool wrote)
        {
            if (recording == null || compiler == null || !Noted.TryGetValue(compiler, out (PointRecord[] Points, int[] Indices) noted))
            {
                return;
            }
            if (!wrote)
            {
                SavedAs.Remove(compiler);   // the game skipped the save: Finish saves it if it changed
                return;
            }
            PointRecord[] now = Snapshot(compiler, noted.Indices);
            if (now != null)
            {
                SavedAs[compiler] = now;
            }
        }

        // A zone's points at these indices as they are now; null where the zone has no arrays.
        private static PointRecord[] Snapshot(TerrainComp compiler, int[] indices)
        {
            float[] level = GameHooks.LevelDelta(compiler);
            float[] smooth = GameHooks.SmoothDelta(compiler);
            bool[] heightChanged = GameHooks.ModifiedHeight(compiler);
            Color[] paint = GameHooks.PaintMask(compiler);
            bool[] painted = GameHooks.ModifiedPaint(compiler);
            if (level == null || smooth == null || heightChanged == null || paint == null || painted == null)
            {
                return null;
            }
            var points = new PointRecord[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                points[i] = Read(indices[i], level, smooth, heightChanged, paint, painted);
            }
            return points;
        }

        /// <summary>Whether two snapshots of the same points hold the same ground.</summary>
        internal static bool Same(PointRecord[] a, PointRecord[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].SameAs(b[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static void Note(TerrainComp compiler, Entry entry)
        {
            if (compiler == null || Noted.ContainsKey(compiler))
            {
                return;
            }
            float[] level = GameHooks.LevelDelta(compiler);
            float[] smooth = GameHooks.SmoothDelta(compiler);
            bool[] heightChanged = GameHooks.ModifiedHeight(compiler);
            Color[] paint = GameHooks.PaintMask(compiler);
            bool[] painted = GameHooks.ModifiedPaint(compiler);
            int width = GameHooks.Width(compiler);
            if (level == null || smooth == null || heightChanged == null || paint == null || painted == null || width <= 0)
            {
                return;
            }
            int[] indices = Box(compiler.transform.position, width, entry.Position, entry.Reach);
            var points = new PointRecord[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                points[i] = Read(indices[i], level, smooth, heightChanged, paint, painted);
            }
            Noted[compiler] = (points, indices);
        }

        /// <summary>
        /// His click is over, every zone worked out: each noted zone compared, and a stroke that changed anything kept (the
        /// oldest dropped past "Strokes to undo"). Of the zones it changed (review round 2):
        /// - **One his game doesn't own** only took the game's paint across a border into memory, which the game never
        ///   saves there and a reload undoes: nothing of it is kept.
        /// - **One his game owns, whose points changed after the game saved it** (the next zone's paint across the border), or
        ///   that wasn't saved in the stroke at all, is saved now: what's saved is what's shown, and what undo later compares.
        /// </summary>
        internal static void Finish()
        {
            Entry entry = recording;
            recording = null;
            if (entry != null)
            {
                foreach (KeyValuePair<TerrainComp, (PointRecord[] Points, int[] Indices)> pair in Noted)
                {
                    TerrainComp compiler = pair.Key;
                    if (compiler == null)
                    {
                        continue;
                    }
                    float[] level = GameHooks.LevelDelta(compiler);
                    float[] smooth = GameHooks.SmoothDelta(compiler);
                    bool[] heightChanged = GameHooks.ModifiedHeight(compiler);
                    Color[] paint = GameHooks.PaintMask(compiler);
                    bool[] painted = GameHooks.ModifiedPaint(compiler);
                    if (level == null || smooth == null || heightChanged == null || paint == null || painted == null)
                    {
                        continue;
                    }
                    ZonePart part = Diff(compiler.transform.position, pair.Value.Indices, pair.Value.Points,
                                         index => Read(index, level, smooth, heightChanged, paint, painted));
                    if (part == null)
                    {
                        continue;
                    }
                    ZNetView view = compiler.GetComponent<ZNetView>();
                    if (view == null || !view.IsValid() || !view.IsOwner())
                    {
                        continue;
                    }
                    if (!SavedAs.TryGetValue(compiler, out PointRecord[] saved) || !Same(saved, Snapshot(compiler, pair.Value.Indices)))
                    {
                        if (!Turned.Contains(compiler))
                        {
                            // Counted as a stroke there, as the game counts one, so a friend's game redraws only this
                            // stroke's grass, not the whole zone's (TerrainComp.CheckLoad; the last look).
                            GameHooks.Operations(compiler)++;
                            GameHooks.LastOpPoint(compiler) = entry.Position;
                            GameHooks.LastOpRadius(compiler) = entry.Reach + 1f;
                        }
                        GameHooks.Save(compiler);
                    }
                    entry.Zones.Add(part);
                }
            }
            Noted.Clear();
            Around.Clear();
            SavedAs.Clear();
            Turned.Clear();
            if (entry == null || entry.Zones.Count == 0)
            {
                return;
            }
            if (world != ZNet.instance)
            {
                Kept.Clear();
                world = ZNet.instance;
            }
            Kept.Add(entry);
            Trim();
        }

        /// <summary>Drops the oldest strokes past "Strokes to undo" (a lower setting takes effect at once).</summary>
        internal static void Trim()
        {
            int keep = Mathf.Clamp(Settings.UndoSteps.Value, 1, 5);
            while (Kept.Count > keep)
            {
                Kept.RemoveAt(0);
            }
        }

        /// <summary>Forgets every stroke (after an error).</summary>
        internal static void Clear()
        {
            Kept.Clear();
            recording = null;
            Noted.Clear();
            Around.Clear();
            SavedAs.Clear();
            Turned.Clear();
        }

        /// <summary>The undo key: puts back his last stroke, or says why it can't. What to say.</summary>
        internal static string UndoLast(Player player)
        {
            if (world != ZNet.instance)
            {
                Kept.Clear();
                world = ZNet.instance;
            }
            if (Kept.Count == 0)
            {
                return "Nothing to undo";
            }
            Entry entry = Kept[Kept.Count - 1];
            var compilers = new List<TerrainComp>();
            foreach (ZonePart part in entry.Zones)
            {
                TerrainComp compiler = TerrainComp.FindTerrainCompiler(part.Centre);
                if (compiler == null || GameHooks.LevelDelta(compiler) == null)
                {
                    return "That ground is too far away to undo; go closer";
                }
                compilers.Add(compiler);
            }
            for (int z = 0; z < entry.Zones.Count; z++)
            {
                if (!StillAsLeft(entry.Zones[z], compilers[z]))
                {
                    Kept.RemoveAt(Kept.Count - 1);
                    return $"The ground there has changed since, so your {entry.Name} stroke can't be undone";
                }
            }
            for (int z = 0; z < entry.Zones.Count; z++)
            {
                PutBack(entry.Zones[z], compilers[z], entry);
            }
            if (ClutterSystem.instance != null)
            {
                ClutterSystem.instance.ResetGrass(entry.Position, entry.Reach + 1f);
            }
            Refund(player, entry);
            Kept.RemoveAt(Kept.Count - 1);
            return Kept.Count > 0 ? $"Undone: {entry.Name} ({Kept.Count} more to undo)" : $"Undone: {entry.Name}";
        }

        /// <summary>The indices of a zone's points within a stroke's reach (and a margin), row by row.</summary>
        internal static int[] Box(Vector3 zoneCentre, int width, Vector3 position, float reach)
        {
            int originX = Mathf.RoundToInt(zoneCentre.x) - width / 2;
            int originZ = Mathf.RoundToInt(zoneCentre.z) - width / 2;
            float margin = reach + Margin;
            int left = Mathf.Max(0, Mathf.FloorToInt(position.x - margin) - originX);
            int right = Mathf.Min(width, Mathf.CeilToInt(position.x + margin) - originX);
            int bottom = Mathf.Max(0, Mathf.FloorToInt(position.z - margin) - originZ);
            int top = Mathf.Min(width, Mathf.CeilToInt(position.z + margin) - originZ);
            if (left > right || bottom > top)
            {
                return new int[0];
            }
            var indices = new int[(right - left + 1) * (top - bottom + 1)];
            int i = 0;
            for (int row = bottom; row <= top; row++)
            {
                for (int column = left; column <= right; column++)
                {
                    indices[i++] = row * (width + 1) + column;
                }
            }
            return indices;
        }

        /// <summary>The points of a zone that a stroke changed: each index whose record differs after, with both.</summary>
        internal static ZonePart Diff(Vector3 zoneCentre, int[] indices, PointRecord[] before, System.Func<int, PointRecord> after)
        {
            ZonePart part = null;
            for (int i = 0; i < indices.Length; i++)
            {
                PointRecord now = after(indices[i]);
                if (now.SameAs(before[i]))
                {
                    continue;
                }
                part ??= new ZonePart { Centre = zoneCentre };
                part.Indices.Add(indices[i]);
                part.Before.Add(before[i]);
                part.After.Add(now);
            }
            return part;
        }

        /// <summary>Whether every point a stroke changed in this zone still holds what the stroke left.</summary>
        internal static bool StillAsLeft(ZonePart part, float[] level, float[] smooth, bool[] heightChanged, Color[] paint, bool[] painted)
        {
            for (int i = 0; i < part.Indices.Count; i++)
            {
                int index = part.Indices[i];
                if (index < 0 || index >= level.Length || index >= paint.Length ||
                    !Read(index, level, smooth, heightChanged, paint, painted).SameAs(part.After[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Gives every point a stroke changed in this zone back what it held before.</summary>
        internal static void Restore(ZonePart part, float[] level, float[] smooth, bool[] heightChanged, Color[] paint, bool[] painted)
        {
            for (int i = 0; i < part.Indices.Count; i++)
            {
                int index = part.Indices[i];
                PointRecord was = part.Before[i];
                level[index] = was.Level;
                smooth[index] = was.Smooth;
                heightChanged[index] = was.HeightChanged;
                paint[index] = was.Paint;
                painted[index] = was.Painted;
            }
        }

        internal static PointRecord Read(int index, float[] level, float[] smooth, bool[] heightChanged, Color[] paint, bool[] painted) =>
            new PointRecord
            {
                Level = level[index],
                Smooth = smooth[index],
                HeightChanged = heightChanged[index],
                Paint = paint[index],
                Painted = painted[index],
            };

        private static bool StillAsLeft(ZonePart part, TerrainComp compiler) =>
            StillAsLeft(part, GameHooks.LevelDelta(compiler), GameHooks.SmoothDelta(compiler), GameHooks.ModifiedHeight(compiler),
                        GameHooks.PaintMask(compiler), GameHooks.ModifiedPaint(compiler));

        // As the game ends a stroke (TerrainComp.DoOperation): the record his, the points back, the stroke counted (1.0.16
        // keeps the copy of a record with more strokes), saved, and the ground redrawn.
        private static void PutBack(ZonePart part, TerrainComp compiler, Entry entry)
        {
            ZNetView view = compiler.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && !view.IsOwner())
            {
                view.ClaimOwnership();
            }
            Restore(part, GameHooks.LevelDelta(compiler), GameHooks.SmoothDelta(compiler), GameHooks.ModifiedHeight(compiler),
                    GameHooks.PaintMask(compiler), GameHooks.ModifiedPaint(compiler));
            GameHooks.Operations(compiler)++;
            GameHooks.LastOpPoint(compiler) = entry.Position;
            GameHooks.LastOpRadius(compiler) = entry.Reach;
            GameHooks.Save(compiler);
            Heightmap map = GameHooks.HeightmapOf(compiler);
            if (map != null)
            {
                map.Poke(1, false);
            }
        }

        // The stone a stroke cost goes back into his inventory, or, when it's full, at his feet as one stack (the game's own
        // drop from a full inventory is a single item: review round 1).
        private static void Refund(Player player, Entry entry)
        {
            Inventory inventory = player != null ? player.GetInventory() : null;
            if (inventory == null || ObjectDB.instance == null)
            {
                return;
            }
            foreach ((string item, int amount) in entry.Cost)
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(item);
                if (prefab == null)
                {
                    continue;
                }
                // Room is measured as the game will stack it: at the world's level (Inventory.AddItem(GameObject, int)).
                ItemDrop drop = prefab.GetComponent<ItemDrop>();
                ItemDrop.ItemData stack = drop != null ? drop.m_itemData.Clone() : null;
                if (stack != null)
                {
                    stack.m_dropPrefab = prefab;
                    stack.m_worldLevel = (byte)Game.m_worldLevel;
                }
                if (stack != null && inventory.CanAddItem(stack, amount))
                {
                    inventory.AddItem(prefab, amount);
                    continue;
                }
                if (stack != null)
                {
                    // Dropped as the game drops an item: from the clone, which carries the world's level (the last look).
                    ItemDrop.DropItem(stack, amount, player.transform.position + Vector3.up, Quaternion.identity);
                }
            }
        }
    }
}
