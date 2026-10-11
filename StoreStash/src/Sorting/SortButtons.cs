using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// Sorting's two buttons (card A53). His inventory's: below the game's weight box, in the column of square buttons just
    /// outside the inventory's right edge (the trash can above the armour box, StoreStash's quick stack between the two
    /// boxes): anchored to the panel's bottom right, so it stays with the weight box when bought rows make the panel
    /// taller, 4 clear of it, and clear of Reserve Stock's padlock and EquipmentAndQuickSlots' panels (read from the game's
    /// IngameGui_Inventory prefab and those mods, 2026-09-30). The chest's: in the chest window's top row, just left of
    /// Place stacks and level with it, worked out from Place stacks every frame as Reserve Stock places its padlock on the
    /// other side; it goes with the chest window.
    /// </summary>
    internal static class SortButtons
    {
        /// <summary>The buttons' width and height, as the pack's other square buttons.</summary>
        internal const float Size = 36f;

        /// <summary>The inventory's Sort button's middle, from the panel's bottom right corner: the column's middle, 32
        /// right of the edge, and 4 below the weight box (its middle at 60, 64 tall).</summary>
        internal static readonly Vector2 Place = new Vector2(32f, 6f);

        /// <summary>The space between Place stacks and the chest's Sort button.</summary>
        internal const float Gap = 4f;

        private static readonly SortTrashButton Mine = new SortTrashButton("StoreStash_SortButton", () => SortTrashPictures.Sort, 26f,
            () => Module.Guard("sorting from the button", () => { if (Module.IsOn) Sorting.SortMine(fromButton: true); }));

        private static readonly SortTrashButton Chest = new SortTrashButton("StoreStash_ChestSortButton", () => SortTrashPictures.Sort, 26f,
            () => Module.Guard("sorting the chest from its button", () =>
            {
                Container chest = Module.IsOn ? GameHooks.OpenChest(InventoryGui.instance) : null;
                if (chest != null)
                {
                    Sorting.SortChest(chest, fromButton: true);
                }
            }));

        private static bool keyChanged = true;
        private static int tipsFor = -1;
        private static string mineTip;
        private static string chestTip;

        /// <summary>The sort key's setting changed: the tooltip, which names it, is written again.</summary>
        internal static void KeyChanged() => keyChanged = true;

        /// <summary>Every frame: each button shown while the inventory is open, the mod, sorting and its own setting on
        /// (the chest's while a chest it may sort is showing); hidden otherwise.</summary>
        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            bool on = Module.IsOn && SortSettings.On.Value && SortHooks.CanSort && gui != null && InventoryGui.IsVisible();
            if (on)
            {
                WriteTips();
            }

            if (on && SortSettings.Button.Value && gui.m_player != null)
            {
                Mine.Show(gui, gui.m_player, new Vector2(1f, 0f), Place + SortSettings.ButtonOffset.Value, Size, mineTip, Color.white, true);
            }
            else
            {
                Mine.Hide();
            }

            Container chest = on && SortSettings.ChestButton.Value ? GameHooks.OpenChest(gui) : null;
            RectTransform stack = gui != null && gui.m_stackAllButton != null ? (RectTransform)gui.m_stackAllButton.transform : null;
            if (chest != null && stack != null && stack.parent != null && Sorting.Shown(chest) && Sorting.Sortable(chest))
            {
                Vector2 at = LeftOf(stack.anchoredPosition, stack.rect.size, stack.pivot) + SortSettings.ChestButtonOffset.Value;
                Chest.Show(gui, stack.parent, stack.anchorMin, at, Size, chestTip, Color.white, true);
            }
            else
            {
                Chest.Hide();
            }
        }

        // The tooltips name the key, the order and what stays in place: written again only when one of those changed.
        private static void WriteTips()
        {
            int state = (int)SortSettings.Order.Value | (SortSettings.KeepHotbar.Value ? 16 : 0) | (SortSettings.KeepWorn.Value ? 32 : 0) |
                        (SortSettings.KeepFavorites.Value ? 64 : 0);
            if (!keyChanged && state == tipsFor)
            {
                return;
            }
            keyChanged = false;
            tipsFor = state;
            mineTip = $"Sort ({Settings.KeyText(SortSettings.Key.Value)}): your inventory, {OrderWords()}. {KeptWords()}";
            chestTip = $"Sort this chest, {OrderWords()}.";
        }

        /// <summary>The undo step after an error: both buttons go.</summary>
        internal static void Remove()
        {
            Mine.Remove();
            Chest.Remove();
        }

        /// <summary>Where the chest's Sort button's middle goes, from Place stacks' position, size and pivot: its left edge,
        /// the gap, half the button; level with its middle.</summary>
        internal static Vector2 LeftOf(Vector2 stackPosition, Vector2 stackSize, Vector2 stackPivot) =>
            new Vector2(stackPosition.x - stackSize.x * stackPivot.x - Gap - Size / 2f,
                        stackPosition.y + stackSize.y * (0.5f - stackPivot.y));

        private static string OrderWords()
        {
            switch (SortSettings.Order.Value)
            {
                case SortOrderChoice.ByType:
                    return "by type";
                case SortOrderChoice.ByName:
                    return "by name";
                case SortOrderChoice.ByWeight:
                    return "heaviest first";
                case SortOrderChoice.ByValue:
                    return "most valuable first";
                default:
                    return "in the pack's order (families, then progression)";
            }
        }

        private static string KeptWords()
        {
            var kept = new System.Collections.Generic.List<string>();
            if (SortSettings.KeepHotbar.Value)
            {
                kept.Add("the hotbar");
            }
            if (SortSettings.KeepWorn.Value)
            {
                kept.Add("what you wear");
            }
            if (SortSettings.KeepFavorites.Value)
            {
                kept.Add("your favorites");
            }
            if (kept.Count == 0)
            {
                return "Everything is sorted.";
            }
            string list = kept.Count == 1 ? kept[0] : string.Join(", ", kept.GetRange(0, kept.Count - 1)) + " and " + kept[kept.Count - 1];
            bool plural = kept.Count > 1 || !SortSettings.KeepHotbar.Value && !SortSettings.KeepWorn.Value;   // "your favorites"
            return char.ToUpperInvariant(list[0]) + list.Substring(1) + (plural ? " stay where they are." : " stays where it is.");
        }
    }
}
