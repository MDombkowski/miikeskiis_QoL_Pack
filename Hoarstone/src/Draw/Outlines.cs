using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Hoarstone.Shapes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ShapeOutline = Hoarstone.Shapes.Outline;

namespace Hoarstone
{
    /// <summary>One line of an outline, in metres, and how it's drawn.</summary>
    internal sealed class OutlineLine
    {
        internal List<Flat> Points;
        internal bool Kept;
    }

    /// <summary>
    /// The settlements' outlines, on request (A88; the lab draws them the same way, by marching squares): every
    /// settlement's while "Show outlines" (Left Alt + F3) has them on, one settlement's while E on its Hoarstone has it on,
    /// and one's while pointing at its Hoarstone. Kept free in one colour; with "Settlements not kept free too", the
    /// others in their own, sparser on the ground. Drawn on the ground (GroundOutline) and on the maps (MapOutline), as
    /// "Where" says. The lines are worked out again only when the shapes or the settings change.
    /// </summary>
    internal static class Outlines
    {
        private const float PointingHolds = 0.3f;
        private const float SamePlace = 4f;

        private static readonly string[] ManagerGuids = { "_shudnal.ConfigurationManager", "com.bepis.bepinex.configurationmanager" };

        private static bool showAll;
        private static readonly List<Vector3> pinned = new List<Vector3>();
        private static readonly List<ZDOID> pinnedStones = new List<ZDOID>();
        private static Vector3 pointedAt;
        private static float pointedUntil = -1f;

        private static List<OutlineLine> lines = new List<OutlineLine>();
        private static long linesFor = long.MinValue;
        private static bool cleared = true;
        private static ZDOMan pinnedIn;
        private static float nextPinCheck;
        private static readonly List<Settlement> shown = new List<Settlement>();

        private static bool keyFailed;
        private static KeyboardShortcut failedKey;
        private static bool lookedForManager;
        private static BaseUnityPlugin manager;
        private static PropertyInfo managerWindowOpen;

        internal static IReadOnlyList<OutlineLine> Lines => lines;

        internal static void Tick(float now)
        {
            if (!Module.IsOn)
            {
                if (!cleared)
                {
                    Clear();
                }
                return;
            }
            cleared = false;
            if (ZDOMan.instance != pinnedIn)
            {
                pinned.Clear();   // pins belong to one world (review round 1)
                pinnedStones.Clear();
                pinnedIn = ZDOMan.instance;
            }
            if (now >= nextPinCheck)
            {
                nextPinCheck = now + 1f;
                ForgetPinsWithoutStones();
            }
            if (Pressed() && TakesKeys())
            {
                showAll = !showAll;
                QoLMods.Kit.Say.TopLeft(showAll ? "Settlement outlines shown" : "Settlement outlines hidden");
            }
            PointAtPlacing();
            bool pointing = now <= pointedUntil;
            long key = Key(pointing);
            if (key != linesFor)
            {
                linesFor = key;
                lines = Build();
            }
            OutlinePlace where = Settings.DrawWhere.Value;
            GroundOutline.Show(lines, where != OutlinePlace.OnTheMap, now);
            MapOutline.Show(lines, where != OutlinePlace.OnTheGround);
        }

        /// <summary>Pointing at a Hoarstone at this place shows its settlement for a moment (renewed every frame while
        /// pointing).</summary>
        internal static void Point(Vector3 at)
        {
            pointedAt = at;
            pointedUntil = Time.unscaledTime + PointingHolds;
        }

        // While a Hoarstone is being placed, the settlement it would mark shows where it would stand (with "Pointing at it
        // shows its settlement" on): a settlement without a Hoarstone shows in the colour of one not kept free.
        private static void PointAtPlacing()
        {
            Player player = Player.m_localPlayer;
            if (player == null || Stone.Entry == null || GameHooks.PlacementGhost == null || !Settings.PointingShows.Value || !player.InPlaceMode())
            {
                return;
            }
            Piece selected = player.GetSelectedPiece();
            GameObject ghost = GameHooks.PlacementGhost(player);
            if (selected != null && selected.gameObject == Stone.Entry && ghost != null && ghost.activeSelf)
            {
                Point(ghost.transform.position);
            }
        }

        /// <summary>E on a Hoarstone: shows its settlement's outline, or hides it again; while every outline shows, hides
        /// them all (the hover text says "Hide" then). True when it did either.</summary>
        internal static bool ToggleAt(PrivateArea stone)
        {
            Vector3 at = stone.transform.position;
            if (showAll)
            {
                showAll = false;
                pinned.Clear();
                pinnedStones.Clear();
                linesFor = long.MinValue;
                return true;
            }
            int i = PinnedAt(at);
            if (i >= 0)
            {
                pinned.RemoveAt(i);
                pinnedStones.RemoveAt(i);
            }
            else
            {
                ZNetView view = GameHooks.WardView(stone);
                if (view == null || !view.IsValid())
                {
                    return false;   // a stone not yet whole in this game: nothing to keep the outline by
                }
                pinned.Add(at);
                pinnedStones.Add(view.GetZDO().m_uid);
            }
            linesFor = long.MinValue;
            return true;
        }

        // A pin whose Hoarstone is gone (its record destroyed, or no longer marked) is dropped (review round 2: by its
        // record, not by whether its object is loaded yet, which it may not be for a moment after its place loads).
        private static void ForgetPinsWithoutStones()
        {
            if (pinned.Count == 0 || ZDOMan.instance == null)
            {
                return;
            }
            for (int i = pinned.Count - 1; i >= 0; i--)
            {
                ZDOID id = pinnedStones[i];
                ZDO zdo = id.IsNone() ? null : ZDOMan.instance.GetZDO(id);
                if (zdo == null || !zdo.IsValid() || !zdo.GetBool(Records.MarkHash))
                {
                    pinned.RemoveAt(i);
                    pinnedStones.RemoveAt(i);
                    linesFor = long.MinValue;
                }
            }
        }

        internal static bool IsShownAt(Vector3 at) => showAll || PinnedAt(at) >= 0;

        private static int PinnedAt(Vector3 at)
        {
            for (int i = 0; i < pinned.Count; i++)
            {
                if ((pinned[i] - at).sqrMagnitude <= SamePlace * SamePlace)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>What a Hoarstone's hover text says of its settlement.</summary>
        internal static string Describe(Vector3 at)
        {
            Settlement s = SettlementAt(at);
            if (s == null)
            {
                return "Working out its settlement…";
            }
            int pieces = s.Members.Count;
            int across = (int)Math.Round(s.MaxX - s.MinX), deep = (int)Math.Round(s.MaxZ - s.MinZ);
            string size = $"{pieces} piece{(pieces == 1 ? "" : "s")} over about {across} × {deep} m";
            return Settings.KeepSpawnsOut.Value ? $"Settlement: {size}; no monsters spawn in it" : $"Settlement: {size} (keeping monsters out is switched off)";
        }

        /// <summary>The settlement worked out here whose shape holds this place, the one with a Hoarstone first.</summary>
        internal static Settlement SettlementAt(Vector3 at)
        {
            Settlement best = null;
            foreach (Settlement s in Coverage.Settlements)
            {
                if (!s.Kept || s.Shape == null)
                {
                    continue;
                }
                int i = (int)Math.Floor(at.x / s.Shape.Cell), j = (int)Math.Floor(at.z / s.Shape.Cell);
                if (!s.Shape.At(i, j))
                {
                    continue;
                }
                if (s.HasStone)
                {
                    return s;
                }
                best ??= s;
            }
            return best;
        }

        private static bool KeptFree(Settlement s) => Settings.KeepSpawnsOut.Value && (s.HasStone || Settings.Where.Value == WhichSettlements.EverySettlement);

        // What the lines depend on, so they're worked out again only when it changes: for the settlements shown one by
        // one, which settlements they are (not where the pointer is, which moves every frame while a Hoarstone is placed).
        // A number, made without allocating, every frame.
        private static long Key(bool pointing)
        {
            long key = 17;
            unchecked
            {
                key = key * 31 + Coverage.Version;
                key = key * 31 + (showAll ? 1 : 0);
                key = key * 31 + Settings.KeptLayers;
                key = key * 31 + (Settings.KeepSpawnsOut.Value ? 1 : 0);
                key = key * 31 + (Settings.ShowOthers.Value ? 1 : 0);
                key = key * 31 + (long)Settings.Cell;
                Shown(pointing);
                foreach (Settlement s in shown)
                {
                    key = key * 31 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(s);
                }
            }
            return key == long.MinValue ? 0 : key;
        }

        // The settlements shown one by one: E on their Hoarstones, and the one pointed at (into the list "shown").
        private static void Shown(bool pointing)
        {
            shown.Clear();
            if (showAll)
            {
                return;
            }
            foreach (Vector3 at in pinned)
            {
                Settlement s = SettlementAt(at);
                if (s != null && !shown.Contains(s))
                {
                    shown.Add(s);
                }
            }
            if (pointing)
            {
                Settlement s = SettlementAt(pointedAt);
                if (s != null && !shown.Contains(s))
                {
                    shown.Add(s);
                }
            }
        }

        // The lines for what shows now (the settlements in "shown", as the key just found them).
        private static List<OutlineLine> Build()
        {
            var built = new List<OutlineLine>();
            if (showAll)
            {
                AllFromTiles(built);
                return built;
            }
            foreach (Settlement s in shown)
            {
                bool kept = KeptFree(s);
                if (!kept && !Settings.ShowOthers.Value)
                {
                    continue;
                }
                ShapeGrid g = s.Shape;
                var segments = new List<(long, long, long, long)>();
                ShapeOutline.Segments(g.At, g.X0 - 1, g.Z0 - 1, g.X0 + g.W - 1, g.Z0 + g.H - 1, segments);
                foreach (List<Flat> points in ShapeOutline.Join(segments, g.Cell))
                {
                    built.Add(new OutlineLine { Points = points, Kept = kept });
                }
            }
            return built;
        }

        // Every settlement, from the zone tiles (this game's own, joined with the saved ones where its records aren't
        // whole): the layers kept free, then (with "Settlements not kept free too") the others.
        private static void AllFromTiles(List<OutlineLine> built)
        {
            double cell = Settings.Cell;
            int side = Tiles.Side(cell);
            var tiles = new Dictionary<ZoneId, Tile>();
            foreach (ZoneId zone in Coverage.ZonesWithShapes())
            {
                Tile tile = Coverage.Drawn(zone);
                if (tile == null)
                {
                    continue;
                }
                if (tile.Side != side)
                {
                    var redrawn = new Tile(side);
                    Tiles.CopyLayer(tile, Tiles.StoneLayer, redrawn);
                    Tiles.CopyLayer(tile, Tiles.OtherLayer, redrawn);
                    tile = redrawn;
                }
                tiles[zone] = tile;
            }
            int keptLayers = Settings.KeepSpawnsOut.Value ? Settings.KeptLayers : 0;
            AddLayers(built, tiles, cell, keptLayers, true);
            if (Settings.ShowOthers.Value)
            {
                AddLayers(built, tiles, cell, 3 & ~keptLayers, false);
            }
        }

        private static void AddLayers(List<OutlineLine> built, Dictionary<ZoneId, Tile> tiles, double cell, int layers, bool kept)
        {
            if (layers == 0 || tiles.Count == 0)
            {
                return;
            }
            bool At(int i, int j)
            {
                var zone = new ZoneId(Tiles.ZoneOfSquare(i, cell), Tiles.ZoneOfSquare(j, cell));
                if (!tiles.TryGetValue(zone, out Tile tile))
                {
                    return false;
                }
                int li = i - Tiles.FirstSquare(zone.X, cell), lj = j - Tiles.FirstSquare(zone.Z, cell);
                return ((layers & 1) != 0 && tile.Get(Tiles.StoneLayer, li, lj)) || ((layers & 2) != 0 && tile.Get(Tiles.OtherLayer, li, lj));
            }
            // Each block goes to the zone holding its upper-right square, so every block is read once; the zones next to
            // a tile are read too, for the edge that runs along it.
            var zones = new HashSet<ZoneId>();
            foreach (ZoneId zone in tiles.Keys)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        zones.Add(new ZoneId(zone.X + dx, zone.Z + dz));
                    }
                }
            }
            int side = Tiles.Side(cell);
            var segments = new List<(long, long, long, long)>();
            foreach (ZoneId zone in zones)
            {
                int i0 = Tiles.FirstSquare(zone.X, cell) - 1, j0 = Tiles.FirstSquare(zone.Z, cell) - 1;
                ShapeOutline.Segments(At, i0, j0, i0 + side - 1, j0 + side - 1, segments);
            }
            foreach (List<Flat> points in ShapeOutline.Join(segments, cell))
            {
                built.Add(new OutlineLine { Points = points, Kept = kept });
            }
        }

        // ---------- the key ----------

        private static bool Pressed()
        {
            KeyboardShortcut key = Settings.ShowKey.Value;
            if (key.MainKey == KeyCode.None || (keyFailed && key.Equals(failedKey)))
            {
                return false;
            }
            keyFailed = false;
            try
            {
                if (!ZInput.GetKeyDown(key.MainKey, false))
                {
                    return false;
                }
                foreach (KeyCode modifier in key.Modifiers)
                {
                    if (!ZInput.GetKey(modifier, false))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception error)
            {
                // Some keys (F13 to F15, Help, SysReq, the shifted characters …) aren't in the game's own key map, and its
                // input throws for them (LESSONS_LEARNED).
                keyFailed = true;
                failedKey = key;
                Module.Log.LogWarning($"The key {key} of \"Show outlines\" can't be read ({error.Message}); choose another.");
                return false;
            }
        }

        // Whether a key press is meant for the game (StoreStash's rule): not while typing in chat, the console, a sign, a
        // text box or the build menu's search, in a menu, or with a configuration manager's window open. The big map may
        // be open: the outlines show there too.
        private static bool TakesKeys()
        {
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.InCutscene() || player.IsTeleporting())
            {
                return false;
            }
            if ((Chat.instance != null && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible() || StoreGui.IsVisible() ||
                global::Menu.IsVisible() || (TextViewer.instance != null && TextViewer.instance.IsVisible()) || GameCamera.InFreeFly())
            {
                return false;
            }
            if (Hud.instance != null && Hud.instance.m_buildUi != null && Hud.instance.m_buildUi.SearchFieldFocused)
            {
                return false;
            }
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null)
            {
                TMP_InputField tmp = selected.GetComponent<TMP_InputField>();
                InputField plain = selected.GetComponent<InputField>();
                if ((tmp != null && tmp.isFocused) || (plain != null && plain.isFocused))
                {
                    return false;
                }
            }
            return !ManagerWindowOpen();
        }

        private static bool ManagerWindowOpen()
        {
            if (!lookedForManager)
            {
                lookedForManager = true;
                foreach (string guid in ManagerGuids)
                {
                    if (!Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo info) || info.Instance == null)
                    {
                        continue;
                    }
                    PropertyInfo open = info.Instance.GetType().GetProperty("DisplayingWindow", BindingFlags.Instance | BindingFlags.Public);
                    if (open != null && open.PropertyType == typeof(bool) && open.GetIndexParameters().Length == 0)
                    {
                        manager = info.Instance;
                        managerWindowOpen = open;
                        break;
                    }
                }
            }
            if (manager == null)
            {
                return false;
            }
            try
            {
                return (bool)managerWindowOpen.GetValue(manager, null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Hides and forgets every outline (switching off, or after an error).</summary>
        internal static void Clear()
        {
            showAll = false;
            pinned.Clear();
            pinnedStones.Clear();
            shown.Clear();
            pointedUntil = -1f;
            lines = new List<OutlineLine>();
            linesFor = long.MinValue;
            GroundOutline.Clear();
            MapOutline.Clear();
            cleared = true;
        }
    }
}
