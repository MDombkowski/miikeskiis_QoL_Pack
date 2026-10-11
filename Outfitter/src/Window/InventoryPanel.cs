using System;
using System.Collections.Generic;
using Jotunn.Managers;
using Outfitter.Loadouts;
using UnityEngine;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// His own inventory beside Outfitter's window (his words, 2026-10-06: "a button above or below the 'Hide items' ...
    /// button that swaps the 'Nearby items' container for the Personal Inventory window when clicked, and vice versa";
    /// job 30, Session 3): in the place of the nearby items drawer, one or the other, his grid as the game has it (the
    /// hotbar on top, his rows, EquipmentAndQuickSlots' slots), live. A cell dragged into the loadout's grid puts that very
    /// item in (as Save takes it: its maker and other mods' data, worn as he wears it); with Highlight cells on, a click
    /// highlights the cell (the pick Right Alt makes in the game's own inventory), for Copy cells or Save new. It shows
    /// and never moves anything. Built once with the window, as its child; the window shows and hides it.
    /// </summary>
    internal static class InventoryPanel
    {
        /// <summary>Its size at Small: the drawer's, so either beside the window takes the same room.</summary>
        internal static float Width => Drawer.Width;

        private const float Top = 70f;
        private const float GridRoom = 600f;
        private const float LookEvery = 0.2f;

        private static GameObject panel;
        private static GridView grid;
        private static Text hint;
        private static Text under;
        private static UnityEngine.UI.Button allButton;
        private static GridShape shape;
        private static string shown;
        private static float nextLook;
        private static Vector2i? hovered;

        /// <summary>A click on one of its cells (x, y as his inventory counts them). Set by the window.</summary>
        internal static Action<Vector2i> Clicked;

        /// <summary>Whether its cells highlight on a click now (the window's Highlight cells, and "Picking the cells to
        /// save" letting the window pick). Set by the window.</summary>
        internal static Func<bool> Highlighting = () => false;

        internal static bool Shown => panel != null && panel.activeSelf;

        /// <summary>Builds it, hidden, as a child of the window, its top-left corner at (x, y) from the window's.</summary>
        internal static void Build(Transform windowRoot, float x, float y, float height)
        {
            if (panel != null)
            {
                UnityEngine.Object.Destroy(panel);
            }
            shown = null;
            hovered = null;
            GUIManager gui = GUIManager.Instance;
            panel = gui.CreateWoodpanel(windowRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Width, height, false);
            panel.name = "Outfitter_MyInventory";
            Ui.Place(panel, x, y, Width, height);
            Transform root = panel.transform;
            Ui.Label(root, "My inventory", 20f, 12f, 300f, 34f, 22, TextAnchor.MiddleLeft, Ui.Orange, bold: true);
            allButton = Ui.Button(root, "Highlight all", Width - 160f, 16f, 140f, 30f, () => Module.Guard("highlighting every cell", HighlightAll));
            Ui.Help(allButton, "Highlights every cell of your inventory that holds an item: then Copy cells, or Save new.");
            hint = Ui.Label(root, string.Empty, 20f, 46f, Width - 40f, 22f, 13, TextAnchor.MiddleLeft, Ui.Quiet);
            grid = new GridView(root, 20f, Top, takesDrops: false);
            grid.Clicked = cell => Clicked?.Invoke(cell);
            grid.Hovered = cell =>
            {
                hovered = cell;
                ShowUnder();
            };
            grid.DragFrom = DragFrom;
            under = Ui.Label(root, string.Empty, 20f, height - 64f, Width - 40f, 44f, 14, TextAnchor.UpperLeft, Ui.Plain);
            panel.SetActive(false);
        }

        internal static void SetShown(bool open)
        {
            if (panel == null)
            {
                return;
            }
            open &= Settings.InventoryPanel.Value;
            if (panel.activeSelf != open)
            {
                panel.SetActive(open);
            }
            hovered = null;
            shown = null;
            nextLook = 0f;
        }

        /// <summary>The module's undo step, or a new scene: hidden, and drawn afresh next time.</summary>
        internal static void Forget()
        {
            if (panel != null)
            {
                panel.SetActive(false);
            }
            shown = null;
            hovered = null;
        }

        /// <summary>Every frame the window shows: drawn again when his inventory, the highlights or the highlighting
        /// change (looked at five times a second at most).</summary>
        internal static void Refresh(Player player, int picksVersion)
        {
            if (panel == null || !panel.activeSelf || player == null)
            {
                return;
            }
            if (!Settings.InventoryPanel.Value)
            {
                SetShown(false);
                return;
            }
            float now = Time.unscaledTime;
            if (now < nextLook)
            {
                return;
            }
            nextLook = now + LookEvery;
            // Faded when it would do nothing: the cells picked only by the key, or nothing to highlight (review round 1, m6).
            Ui.Set(allButton, Settings.Picking.Value == PickingCells.HeldKeyAndClicks || player.GetInventory().GetAllItems().Count == 0
                ? Ui.Look.Off : Ui.Look.On);
            bool highlighting = Highlighting();
            string state = State(player, picksVersion, highlighting);
            if (state == shown)
            {
                return;
            }
            shown = state;
            shape = GameHooks.Shape(player);
            grid.Shape(shape, GridRoom);
            foreach (KeyValuePair<string, SlotCell> slot in shape.Slots)
            {
                grid.Label(slot.Value.X, slot.Value.Y, slot.Key.StartsWith("Quick", StringComparison.Ordinal) ? slot.Key.Substring(5) : slot.Key);
            }
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                Vector2i at = item.m_gridPos;
                if (!shape.Usable(at.x, at.y))
                {
                    continue;
                }
                grid.Set(at.x, at.y, item.GetIcon(), item.m_stack, item.m_shared.m_maxQuality > 1 ? item.m_quality : 0,
                         player.IsItemEquiped(item), false, Picks.Has(at), null);
            }
            foreach (Vector2i cell in Picks.All())
            {
                grid.Pick(cell.x, cell.y, true);
            }
            hint.text = highlighting
                ? "Click cells to highlight them (again to take it off). Then Copy cells, or Save new."
                : "Drag a cell into the loadout: that very item goes in. Highlight cells to copy several.";
            ShowUnder();
        }

        // What it shows, as one string: changes when anything it draws changes.
        private static string State(Player player, int picksVersion, bool highlighting)
        {
            var state = new System.Text.StringBuilder();
            state.Append(picksVersion).Append(highlighting ? 'h' : '-');
            GridShape now = GameHooks.Shape(player);
            state.Append(now.Rows).Append('/').Append(now.Height).Append('/').Append(now.Slots.Count).Append(';');
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                state.Append(item.m_shared.m_name).Append(item.m_gridPos.x).Append(',').Append(item.m_gridPos.y).Append(':')
                     .Append(item.m_stack).Append(player.IsItemEquiped(item) ? '*' : ' ').Append(';');
            }
            return state.ToString();
        }

        private static void ShowUnder()
        {
            Player player = Player.m_localPlayer;
            if (under == null || player == null)
            {
                return;
            }
            ItemDrop.ItemData item = hovered.HasValue ? player.GetInventory().GetItemAt(hovered.Value.x, hovered.Value.y) : null;
            if (item == null)
            {
                under.text = Picks.Count > 0 ? $"{Picks.Count} cell{(Picks.Count == 1 ? "" : "s")} highlighted." : string.Empty;
                return;
            }
            under.text = Equip.Messages.Localize(item.m_shared.m_name) + (item.m_stack > 1 ? " x" + item.m_stack : string.Empty) +
                         (player.IsItemEquiped(item) ? ", worn" : string.Empty) + (Picks.Has(hovered.Value) ? ", highlighted" : string.Empty);
        }

        // Dragging one of his cells into the loadout: that very item, as Save takes it.
        private static DragItem DragFrom(Vector2i cell)
        {
            Player player = Player.m_localPlayer;
            if (player == null || shape == null || !shape.Usable(cell.x, cell.y))
            {
                return null;
            }
            ItemDrop.ItemData item = player.GetInventory().GetItemAt(cell.x, cell.y);
            if (item == null)
            {
                return null;
            }
            Loadout captured = Capture.Cells(player, string.Empty, new[] { cell });
            if (captured.Items.Count == 0)
            {
                return null;
            }
            LoadoutItem exact = captured.Items[0];
            return new DragItem
            {
                Prefab = exact.Prefab,
                Quality = exact.Quality,
                Variant = exact.Variant,
                Count = exact.Count,
                Sample = item,
                Exact = exact,
                Icon = item.GetIcon(),
            };
        }

        private static void HighlightAll()
        {
            Player player = Player.m_localPlayer;
            if (player == null || Settings.Picking.Value == PickingCells.HeldKeyAndClicks)
            {
                return;   // the window picks no cells by that setting (only the key does)
            }
            GridShape now = GameHooks.Shape(player);
            var cells = new List<Vector2i>();
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (now.Usable(item.m_gridPos.x, item.m_gridPos.y))
                {
                    cells.Add(item.m_gridPos);
                }
            }
            Picks.Set(cells);
            nextLook = 0f;
        }
    }
}
