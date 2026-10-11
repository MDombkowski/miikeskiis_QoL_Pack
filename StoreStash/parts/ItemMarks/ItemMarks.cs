// The QoL Mods item marks, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemMarks\. Every mod that uses it
// carries a copy in its own parts\ItemMarks\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace QoLMods.ItemMarks
{
    /// <summary>
    /// Marks on item types, set by hand and shown on the item's cell (card A52): a module registers what it marks
    /// (MarkUser: StoreStash, favourites; job 32, trash marks), and the part does the rest with two patches, applied once for
    /// the whole plugin however many modules use it. A right-click in his inventory with a marking module's modifier held
    /// marks or unmarks that item's type, instead of the game's own right-click (using or equipping it); and every marked
    /// cell gets a border, drawn over the cell and never catching a click. Both do nothing while no marking module is on.
    /// Checked against the game's InventoryGui (OnRightClickItem, m_playerGrid, ContainerGrid) and InventoryGrid (UpdateGui,
    /// m_inventory, m_elements) of 2026-09-25 (Valheim 1.0.16).
    /// </summary>
    internal static class ItemMarks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly List<MarkUser> Users = new List<MarkUser>();
        private static readonly List<string> Missing = new List<string>();
        private static bool resolved;
        private static FieldInfo gridInventory;
        private static FieldInfo gridElements;
        private static Harmony harmony;

        /// <summary>The names of the Unity objects drawing a kind's border: this plugin's, so two plugins carrying the part
        /// never take each other's.</summary>
        private static readonly string Prefix = typeof(ItemMarks).Assembly.GetName().Name + "_Mark_";

        /// <summary>
        /// Called from each using module's Resolve, at the game's start, with what it marks (null for a module that only
        /// reads a list, such as sorting reading the favourites). The first call looks up the game's members and applies the
        /// part's patches; every call adds to <paramref name="missing"/> what the part can't work without.
        /// </summary>
        internal static void Resolve(List<string> missing, MarkUser user = null)
        {
            if (user != null && !Users.Contains(user))
            {
                Users.Add(user);
            }
            if (!resolved)
            {
                resolved = true;
                Find();
            }
            missing.AddRange(Missing);
        }

        private static void Find()
        {
            MethodInfo rightClick = typeof(InventoryGui).GetMethod("OnRightClickItem", AnyInstance, null,
                new[] { typeof(InventoryGrid), typeof(ItemDrop.ItemData), typeof(Vector2i) }, null);
            if (rightClick == null)
            {
                Missing.Add("InventoryGui.OnRightClickItem");
            }
            MethodInfo updateGui = typeof(InventoryGrid).GetMethod("UpdateGui", AnyInstance, null, new[] { typeof(Player), typeof(ItemDrop.ItemData) }, null);
            if (updateGui == null)
            {
                Missing.Add("InventoryGrid.UpdateGui");
            }
            gridInventory = typeof(InventoryGrid).GetField("m_inventory", AnyInstance);
            if (gridInventory == null || gridInventory.FieldType != typeof(Inventory))
            {
                Missing.Add("InventoryGrid.m_inventory");
            }
            gridElements = typeof(InventoryGrid).GetField("m_elements", AnyInstance);
            if (gridElements == null || gridElements.FieldType != typeof(List<InventoryElement>))
            {
                Missing.Add("InventoryGrid.m_elements");
            }
            if (Missing.Count > 0)
            {
                return;
            }
            try
            {
                // Its own Harmony ID, one per plugin: modprojects.qolmods.itemmarks in the bundle, modprojects.<mod>.itemmarks
                // in a single release. A module's host never removes it (MODULES.md, "Parts").
                string plugin = typeof(ItemMarks).Assembly.GetName().Name.ToLowerInvariant();
                harmony = new Harmony($"modprojects.{plugin}.itemmarks");
                // First: a marking click then skips the game's right-click and every later prefix that honours
                // __runOriginal (another mod's prefix that doesn't look at it still runs).
                harmony.Patch(rightClick, prefix: new HarmonyMethod(AccessTools.Method(typeof(ItemMarks), nameof(RightClickPrefix))) { priority = Priority.First });
                // Last, so the border is drawn over whatever the game and other mods have drawn in the cell.
                harmony.Patch(updateGui, postfix: new HarmonyMethod(AccessTools.Method(typeof(ItemMarks), nameof(UpdateGuiPostfix))) { priority = Priority.Last });
            }
            catch (Exception error)
            {
                Missing.Add($"the item marks' patches on the inventory ({error.Message})");
            }
        }

        /// <summary>
        /// Prefix on InventoryGui.OnRightClickItem (the grid, the item, its cell): with a marking module on and its modifier
        /// held, the click marks or unmarks the item's type and the game's own right-click is skipped; every other click goes
        /// to the game as usual. Positional arguments, so a renamed parameter can't stop the patch from applying.
        /// </summary>
        internal static bool RightClickPrefix(InventoryGrid __0, ItemDrop.ItemData __1, bool __runOriginal)
        {
            if (!__runOriginal || __1?.m_shared == null)
            {
                return true;
            }
            MarkUser user = Marker(__0);
            if (user == null)
            {
                return true;
            }
            bool took = false;
            user.Guard("marking an item", () =>
            {
                bool now = user.List.Toggle(__1.m_shared.m_name, out bool saved);
                took = true;
                user.Toggled?.Invoke(__1, now, saved);
            });
            return !took;
        }

        // The marking module this click is for: on, its modifier held, and the grid one it marks in; the first registered wins.
        private static MarkUser Marker(InventoryGrid grid)
        {
            foreach (MarkUser user in Users)
            {
                if (!user.IsOn)
                {
                    continue;
                }
                KeyCode modifier = user.Modifier();
                if (modifier != KeyCode.None && ZInput.GetKey(modifier, false) && Allowed(grid, user))
                {
                    return user;
                }
            }
            return null;
        }

        // His own inventory always; a chest's window when the module says so; no other grid (another mod's).
        private static bool Allowed(InventoryGrid grid, MarkUser user)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || grid == null)
            {
                return false;
            }
            return grid == gui.m_playerGrid || (grid == gui.ContainerGrid && user.InChests());
        }

        /// <summary>Postfix on InventoryGrid.UpdateGui, which draws a grid's cells every frame it shows: each registered
        /// module's border on the cells whose item carries its mark, hidden on the others and while the module is off.</summary>
        internal static void UpdateGuiPostfix(InventoryGrid __instance)
        {
            if (Users.Count == 0)
            {
                return;
            }
            try
            {
                Draw(__instance);
            }
            catch (Exception error)
            {
                if (!toldDrawFailed)
                {
                    toldDrawFailed = true;
                    ZLog.LogWarning($"[QoL Mods item marks] The marks couldn't be drawn on the inventory: {error}");
                }
            }
        }

        private static bool toldDrawFailed;

        // The grids each module may have borders showing in: one showing none in a grid is passed over there, so drawing
        // an inventory costs nothing while every marking module is off or shows no borders.
        private static readonly Dictionary<MarkUser, HashSet<InventoryGrid>> Showing = new Dictionary<MarkUser, HashSet<InventoryGrid>>();

        // The grid's items by cell, filled again at each drawing that shows borders (one dictionary, kept).
        private static readonly Dictionary<int, ItemDrop.ItemData> At = new Dictionary<int, ItemDrop.ItemData>();

        private static HashSet<InventoryGrid> ShowingIn(MarkUser user)
        {
            if (!Showing.TryGetValue(user, out HashSet<InventoryGrid> grids))
            {
                grids = new HashSet<InventoryGrid>();
                Showing[user] = grids;
            }
            return grids;
        }

        private static void Draw(InventoryGrid grid)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || grid == null)
            {
                return;
            }
            bool mine = grid == gui.m_playerGrid;
            if (!mine && grid != gui.ContainerGrid)
            {
                return;
            }
            var inventory = (Inventory)gridInventory.GetValue(grid);
            var elements = (List<InventoryElement>)gridElements.GetValue(grid);
            if (inventory == null || elements == null)
            {
                return;
            }
            bool filled = false;
            foreach (MarkUser user in Users)
            {
                bool show = user.IsOn && user.Borders() && (mine || user.InChests());
                HashSet<InventoryGrid> showingIn = ShowingIn(user);
                if (!show && !showingIn.Contains(grid))
                {
                    continue;   // none of its borders here to hide
                }
                if (show)
                {
                    // Noted before drawing, so a drawing that fails part way still has its borders hidden later.
                    showingIn.RemoveWhere(each => each == null);   // grids gone with a world
                    showingIn.Add(grid);
                }
                if (show && !filled)
                {
                    filled = true;
                    At.Clear();
                    foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                    {
                        At[Cell(item.m_gridPos.x, item.m_gridPos.y)] = item;
                    }
                }
                Color colour = show ? user.Colour() : Color.clear;
                string name = Prefix + user.List.Kind;
                foreach (InventoryElement element in elements)
                {
                    if (element == null)
                    {
                        continue;
                    }
                    ItemDrop.ItemData item = null;
                    if (show)
                    {
                        At.TryGetValue(Cell(element.Position.x, element.Position.y), out item);
                    }
                    MarkBorder.Show(element.transform, name, user.Place, show && item != null && user.List.Has(item), colour);
                }
                if (!show)
                {
                    showingIn.Remove(grid);   // every one of its borders here is hidden now
                }
            }
            At.Clear();
        }

        private static int Cell(int x, int y) => y * 1024 + x;

        /// <summary>The undo step of a marking module: hides its borders at once in both grids (they would go at the grids'
        /// next drawing anyway, since the module is off).</summary>
        internal static void HideBorders(MarkUser user)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || user == null || gridElements == null)
            {
                return;
            }
            string name = Prefix + user.List.Kind;
            foreach (InventoryGrid grid in new[] { gui.m_playerGrid, gui.ContainerGrid })
            {
                if (grid != null && gridElements.GetValue(grid) is List<InventoryElement> elements)
                {
                    foreach (InventoryElement element in elements)
                    {
                        if (element != null)
                        {
                            MarkBorder.Show(element.transform, name, 0, false, Color.clear);
                        }
                    }
                }
            }
            ShowingIn(user).Clear();   // after: if hiding failed part way, the next drawing hides the rest
        }
    }
}
