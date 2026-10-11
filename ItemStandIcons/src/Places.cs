using System.Collections;
using System.Collections.Generic;
using QoLMods.ItemStands;
using UnityEngine;
using UnityEngine.UI;

namespace ItemStandIcons
{
    /// <summary>
    /// Every place an item's icon is drawn, and its mark. The places the game draws every frame (the inventory grids, the
    /// hotbar, the chosen recipe, the ingredients) are marked right after the game draws them, from the patches; the lists
    /// the game fills once (the crafting list, the trader's) are noted as they're filled and marked every frame from the
    /// module's Update, so a setting or the "hold a key" key shows at once. Each mark hangs on the icon's host: an inventory
    /// cell's or a hotbar slot's root (MyLittleUI scales the icon inside it; EquipmentAndQuickSlots moves whole cells), an
    /// ingredient's slot, or the picture itself where it stands alone (the crafting list's, the chosen recipe's, the
    /// trader's). An icon carries two marks: the item stands' arrows and the armor stand's mannequin, each at its place;
    /// with "Only while aiming at a stand" on, the places it covers show them only while he aims at one (Aim).
    /// </summary>
    internal static class Places
    {
        private const string Mark = Module.MarkName;

        private const string ArmorMark = Module.ArmorMarkName;

        /// <summary>A full inventory cell or hotbar slot, in its own units.</summary>
        private const float Cell = 64f;

        private sealed class Row
        {
            internal Transform Icon;
            internal GameObject Prefab;
        }

        private static readonly List<Row> RecipeRows = new List<Row>();
        private static readonly List<Row> TraderRows = new List<Row>();
        private static readonly Dictionary<int, ItemDrop.ItemData> At = new Dictionary<int, ItemDrop.ItemData>();

        /// <summary>True once a mark may be showing, until every mark is hidden: while it's false and the marks are off,
        /// the patches skip their places altogether.</summary>
        private static bool shown;

        /// <summary>True while the marks show, or one may still show: the per-frame patches work only then.</summary>
        internal static bool Busy => shown || Settings.Showing;

        /// <summary>Every frame, from the module's Update (also while it's switched off): the lists' marks, and every mark
        /// taken back at once when the marks go off.</summary>
        internal static void Tick()
        {
            // Rows the game destroyed as it filled a list again are forgotten whatever the switches say (review 1: with
            // the marks off, the crafting list's notes would otherwise pile up all session).
            RecipeRows.RemoveAll(row => row.Icon == null);
            TraderRows.RemoveAll(row => row.Icon == null);
            if (!Settings.Showing)
            {
                if (shown)
                {
                    HideAll();
                }
                return;
            }
            StandLook look = Settings.CurrentLook;
            StandRule rule = Settings.CurrentRule;
            if (RecipeRows.Count > 0 && InventoryGui.IsVisible())
            {
                DressRows(RecipeRows, Settings.InCraftingList.Value, look, rule);
            }
            if (TraderRows.Count > 0 && StoreGui.IsVisible())
            {
                DressRows(TraderRows, Settings.AtTrader.Value, look, rule);
            }
        }

        /// <summary>Hides every mark at once: the module switched off, or its undo step after an error.</summary>
        internal static void HideAll()
        {
            StandBadge.HideAll(Mark);
            StandBadge.HideAll(ArmorMark);
            Aim.Forget();
            shown = false;
        }

        // Both marks of one icon: the arrows for the item stands' part of the fit, the mannequin for the armor stand's.
        private static void Put(Transform host, Fit fit, StandLook look, float icon)
        {
            StandBadge.Show(host, Mark, fit & Fit.Both, look, icon);
            StandBadge.Show(host, ArmorMark, fit & Fit.Armor, look, icon);
        }

        private static void Hide(Transform host)
        {
            StandBadge.Hide(host, Mark);
            StandBadge.Hide(host, ArmorMark);
        }

        // An item's fit, and what aiming lets it show (his own items' places are yourItems).
        private static Fit FitOf(GameObject prefab, StandRule rule, bool yourItems) =>
            prefab != null ? Aim.Filter(StandFit.Of(prefab, rule), prefab, yourItems) : Fit.None;

        private static void DressRows(List<Row> rows, bool on, StandLook look, StandRule rule)
        {
            foreach (Row row in rows)
            {
                Fit fit = on ? FitOf(row.Prefab, rule, yourItems: false) : Fit.None;
                Put(row.Icon, fit, look, IconSize(row.Icon, 32f));
            }
            if (on)
            {
                shown = true;
            }
        }

        // The picture's own height, where the mark hangs on the picture itself.
        private static float IconSize(Transform icon, float fallback) =>
            icon is RectTransform rect && rect.rect.height > 1f ? rect.rect.height : fallback;

        /// <summary>After InventoryGrid.UpdateGui: his inventory (with EquipmentAndQuickSlots' slots, which are its cells),
        /// a chest's, or another mod's grid.</summary>
        internal static void AfterGrid(InventoryGrid grid)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || grid == null)
            {
                return;
            }
            bool mine = grid == gui.m_playerGrid;
            bool show = Settings.Showing && (mine ? Settings.InInventory.Value : Settings.InChests.Value);
            if (!show && !shown)
            {
                return;   // no mark of ours to hide
            }
            Inventory inventory = GameHooks.InventoryOf(grid);
            List<InventoryElement> elements = GameHooks.ElementsOf(grid);
            if (inventory == null || elements == null)
            {
                return;
            }
            StandLook look = show ? Settings.CurrentLook : null;
            StandRule rule = Settings.CurrentRule;
            At.Clear();
            if (show)
            {
                shown = true;
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    At[Key(item.m_gridPos.x, item.m_gridPos.y)] = item;
                }
            }
            foreach (InventoryElement element in elements)
            {
                if (element == null)
                {
                    continue;
                }
                Fit fit = Fit.None;
                if (show && At.TryGetValue(Key(element.Position.x, element.Position.y), out ItemDrop.ItemData item))
                {
                    fit = FitOf(StandFit.PrefabOf(item), rule, yourItems: mine);
                }
                Put(element.transform, fit, look, Cell);
            }
            At.Clear();
        }

        private static int Key(int x, int y) => y * 1024 + x;

        /// <summary>After HotkeyBar.UpdateIcons: the hotbar, and EquipmentAndQuickSlots' quick-slot bar (a copy of it).
        /// Its cells are its slots in order, as the game fills them (an item's slot is its column).</summary>
        internal static void AfterHotbar(HotkeyBar bar)
        {
            bool show = Settings.Showing && Settings.OnHotbar.Value;
            if (bar == null || (!show && !shown))
            {
                return;
            }
            IList elements = GameHooks.HotbarElements(bar);
            if (elements == null)
            {
                return;
            }
            StandLook look = show ? Settings.CurrentLook : null;
            StandRule rule = Settings.CurrentRule;
            At.Clear();
            List<ItemDrop.ItemData> items = show ? GameHooks.HotbarItems(bar) : null;
            if (items != null)
            {
                shown = true;
                foreach (ItemDrop.ItemData item in items)
                {
                    if (item != null)
                    {
                        At[item.m_gridPos.x] = item;
                    }
                }
            }
            for (int slot = 0; slot < elements.Count; slot++)
            {
                GameObject cell = GameHooks.ObjectOf(elements[slot]);
                if (cell == null)
                {
                    continue;
                }
                Fit fit = At.TryGetValue(slot, out ItemDrop.ItemData item) ? FitOf(StandFit.PrefabOf(item), rule, yourItems: true) : Fit.None;
                Put(cell.transform, fit, look, Cell);
            }
            At.Clear();
        }

        /// <summary>After InventoryGui.AddRecipeToList: the row it has just made is noted with its recipe (both the last of
        /// the game's list), and marked.</summary>
        internal static void AfterAddRecipe(InventoryGui gui)
        {
            IList pairs = GameHooks.AvailableRecipes(gui);
            if (pairs == null || pairs.Count == 0)
            {
                return;
            }
            if (pairs.Count == 1)
            {
                RecipeRows.Clear();   // the game empties its list before filling it again: a new list
            }
            object last = pairs[pairs.Count - 1];
            GameObject row = GameHooks.RowOf(last);
            Transform icon = row != null ? row.transform.Find("icon") : null;
            if (icon == null || (RecipeRows.Count > 0 && RecipeRows[RecipeRows.Count - 1].Icon == icon))
            {
                return;   // no row, or one already noted (another mod skipped the game's own add: review 1)
            }
            Recipe recipe = GameHooks.RecipeOf(last);
            var noted = new Row { Icon = icon, Prefab = recipe != null && recipe.m_item != null ? recipe.m_item.gameObject : null };
            RecipeRows.Add(noted);
            if (Settings.Showing)
            {
                DressRows(new List<Row> { noted }, Settings.InCraftingList.Value, Settings.CurrentLook, Settings.CurrentRule);
            }
        }

        /// <summary>After InventoryGui.UpdateRecipe: the chosen recipe's large picture, and the small one of the item it
        /// upgrades or makes.</summary>
        internal static void AfterUpdateRecipe(InventoryGui gui)
        {
            bool show = Settings.Showing && Settings.OnChosenRecipe.Value;
            if (gui == null || (!show && !shown))
            {
                return;
            }
            Fit fit = Fit.None;
            if (show)
            {
                shown = true;
                Recipe recipe = GameHooks.SelectedRecipe(gui);
                fit = recipe != null && recipe.m_item != null ? FitOf(recipe.m_item.gameObject, Settings.CurrentRule, yourItems: false) : Fit.None;
            }
            StandLook look = show ? Settings.CurrentLook : null;
            Image large = gui.m_recipeIcon;
            if (large != null)
            {
                Put(large.transform, large.enabled ? fit : Fit.None, look, IconSize(large.transform, Cell));
            }
            Image small = gui.m_upgradeItemIcon;
            if (small != null)
            {
                Put(small.transform, small.enabled && small.gameObject.activeInHierarchy ? fit : Fit.None, look, IconSize(small.transform, 32f));
            }
        }

        /// <summary>After InventoryGui.SetupRequirement: an ingredient in the crafting menu, or a material of the piece in
        /// hand in the build panel (the game fills both with it). Another mod's rows are left alone.</summary>
        internal static void AfterSetupRequirement(Transform root, Piece.Requirement requirement, bool filled)
        {
            if (root == null)
            {
                return;
            }
            bool build = Contains(Hud.instance != null ? Hud.instance.m_requirementItems : null, root);
            bool craft = !build && Contains(InventoryGui.instance != null ? InventoryGui.instance.m_recipeRequirementList : null, root);
            if (!build && !craft)
            {
                return;
            }
            bool show = Settings.Showing && (build ? Settings.OnBuildCosts.Value && GameHooks.SetupPieceInfo != null : Settings.OnIngredients.Value);
            if (!show && !shown)
            {
                return;
            }
            Fit fit = Fit.None;
            if (show)
            {
                shown = true;
                if (filled && requirement?.m_resItem != null)
                {
                    fit = FitOf(requirement.m_resItem.gameObject, Settings.CurrentRule, yourItems: false);
                }
            }
            Put(root, fit, show ? Settings.CurrentLook : null, Cell);
        }

        /// <summary>After InventoryGui.HideRequirement: an emptied slot loses its marks.</summary>
        internal static void AfterHideRequirement(Transform root) => Hide(root);

        /// <summary>After Hud.SetupPieceInfo: the build panel's rows after the piece's last material (its station, or
        /// nothing) are written without SetupRequirement, so they lose any mark here.</summary>
        internal static void AfterSetupPieceInfo(Hud hud, Piece piece)
        {
            GameObject[] rows = hud != null ? hud.m_requirementItems : null;
            if (rows == null)
            {
                return;
            }
            int materials = piece != null && piece.m_resources != null ? piece.m_resources.Length : 0;
            for (int index = materials; index < rows.Length; index++)
            {
                if (rows[index] != null)
                {
                    Hide(rows[index].transform);
                }
            }
        }

        private static bool Contains(GameObject[] rows, Transform root)
        {
            if (rows == null)
            {
                return false;
            }
            foreach (GameObject row in rows)
            {
                if (row != null && row.transform == root)
                {
                    return true;
                }
            }
            return false;
        }

        // The picture the game gives a trader's row (StoreGui.FillList): the trade's own, else its item's first.
        private static Sprite PictureOf(Trader.TradeItem trade)
        {
            if (trade.m_icon != null)
            {
                return trade.m_icon;
            }
            Sprite[] icons = trade.m_prefab?.m_itemData?.m_shared?.m_icons;
            return icons != null && icons.Length > 0 ? icons[0] : null;
        }

        /// <summary>After StoreGui.FillList: the trader's rows, made again, are noted with what each sells, and marked.</summary>
        internal static void AfterFillStore(StoreGui store)
        {
            TraderRows.Clear();
            List<GameObject> rows = GameHooks.StoreRows(store);
            Trader trader = GameHooks.StoreTrader(store);
            List<Trader.TradeItem> items = rows != null && trader != null ? trader.GetAvailableItems() : null;
            if (items == null || items.Count != rows.Count)
            {
                return;   // the game's own list, made from the same call: never out of step, unless a mod changes it
            }
            for (int index = 0; index < rows.Count; index++)
            {
                Transform icon = rows[index] != null ? rows[index].transform.Find("icon") : null;
                ItemDrop sold = items[index]?.m_prefab;
                // Only a row showing the picture of what it's paired with (review 1: a mod could hand the second call
                // another list of the same length).
                if (icon != null && sold != null && icon.GetComponent<Image>() is Image picture && picture.sprite == PictureOf(items[index]))
                {
                    TraderRows.Add(new Row { Icon = icon, Prefab = sold.gameObject });
                }
            }
            if (Settings.Showing)
            {
                DressRows(TraderRows, Settings.AtTrader.Value, Settings.CurrentLook, Settings.CurrentRule);
            }
        }
    }
}
