using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// The trash can (card A53): a square button above the game's armour box, at the top of the column of square buttons
    /// just outside the inventory's right edge (StoreStash's quick stack between the two boxes, sorting's button below the
    /// weight box): the most out-of-the-way place of the three, well apart from quick stack. Anchored where the armour box
    /// is, so it stays 4 above the box's picture when bought rows make the panel taller; clear of EquipmentAndQuickSlots'
    /// panel (read from the game's IngameGui_Inventory prefab and that mod, 2026-09-30). Its picture is white while marked
    /// items would go, reddish while he carries something it would delete, grey while a click would delete nothing.
    /// </summary>
    internal static class TrashCan
    {
        /// <summary>Its width and height, as the pack's other square buttons.</summary>
        internal const float Size = 36f;

        /// <summary>Its middle, from the panel's right edge at its middle (the armour box's own anchor): the column's
        /// middle, and 4 above the armour box's picture (the box's middle at 72, its picture's top 30 + 16 above that).</summary>
        internal static readonly Vector2 Place = new Vector2(32f, 140f);

        internal static readonly Color Ready = Color.white;

        internal static readonly Color Carrying = new Color(1f, 0.5f, 0.42f, 1f);

        internal static readonly Color Idle = new Color(0.55f, 0.55f, 0.55f, 1f);

        private static readonly SortTrashButton Can = new SortTrashButton("StoreStash_TrashCan", () => SortTrashPictures.Trash, 26f,
            () => Module.Guard("using the trash can", Trash.Clicked));

        private static int tipFor = -1;
        private static string tip;

        /// <summary>Every frame: shown while the inventory is open, the mod, the trash and its own setting on.</summary>
        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            Player player = Player.m_localPlayer;
            if (!Module.IsOn || !TrashSettings.On.Value || !TrashSettings.Button.Value || gui == null || gui.m_player == null || player == null ||
                !InventoryGui.IsVisible())
            {
                Can.Hide();
                return;
            }
            Color colour;
            int marked = 0;
            if (SortHooks.Carried(gui, out _, out _, out _))
            {
                colour = TrashSettings.Carried.Value && Trash.JudgeCarried(player, gui, out _, out _, out _) == CarriedVerdict.Deletable ? Carrying : Idle;
            }
            else
            {
                marked = Trash.CountMarked(player);
                colour = marked > 0 ? Ready : Idle;
            }
            WriteTip(marked);
            Can.Show(gui, gui.m_player, new Vector2(1f, 0.5f), Place + TrashSettings.ButtonOffset.Value, Size, tip, colour, true);
        }

        /// <summary>The undo step after an error: the can goes.</summary>
        internal static void Remove() => Can.Remove();

        // The tooltip names the key, how many marked stacks would go, and whether a carried item may: written again only
        // when one of those changed.
        private static void WriteTip(int marked)
        {
            KeyCode modifier = TrashSettings.Modifier.Value;
            int state = (marked & 0xFFFF) | ((int)modifier << 16) | (TrashSettings.Carried.Value ? 1 << 30 : 0);
            if (state == tipFor && tip != null)
            {
                return;
            }
            tipFor = state;
            string mark = modifier == KeyCode.None ? "marked as trash" : $"marked as trash ({Settings.Spaced(modifier)} + right-click)";
            string now = marked == 0 ? "Nothing marked would go now." : $"{marked} marked {(marked == 1 ? "stack" : "stacks")} would go now.";
            string carried = TrashSettings.Carried.Value ? " Carrying an item? Click here to delete just that." : string.Empty;
            tip = $"Trash: deletes every item {mark} from your inventory, but never what's on the hotbar, what you wear or " +
                  $"hold, favorites or quest items. {now}{carried}";
        }
    }
}
