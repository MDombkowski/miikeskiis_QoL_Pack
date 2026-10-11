using System.IO;
using QoLMods.ItemMarks;

namespace StoreStash
{
    /// <summary>
    /// His trash marks (card A53): the item kinds the trash can deletes from his inventory. A second kind of mark in the
    /// pack's item marks part beside his favourites, in its own file (BepInEx\config\modprojects.marks.trash.txt), set with
    /// the trash key held and a right-click (Left Alt, his HexQuickStackStorage setting) and shown as a red border just
    /// inside a favourite's gold one (the part's place 1). His HexQuickStackStorage trash list is taken in once, only read.
    /// </summary>
    internal static class TrashMarks
    {
        /// <summary>HexQuickStackStorage's own list of trash marks, in the same folder: one name per line.</summary>
        internal const string HexFile = "HexQuickStackStorage.junk.txt";

        /// <summary>The list: modprojects.marks.trash.txt in BepInEx\config.</summary>
        internal static readonly MarkList List = new MarkList("trash",
            "trash marks, the items StoreStash's trash can deletes from your inventory (never from the hotbar, never what you " +
            "wear, never a favorite).");

        /// <summary>StoreStash's trash marks as the item marks part knows them: their border just inside a favourite's, with
        /// the Trash part's settings.</summary>
        internal static readonly MarkUser User = new MarkUser(Module.Name, List, 1, () => Module.IsOn && TrashSettings.On.Value, Module.Guard,
            () => TrashSettings.Modifier.Value, () => TrashSettings.Borders.Value, () => TrashSettings.BorderColor.Value,
            () => TrashSettings.InChestWindows.Value, Toggled);

        private static bool importTried;

        /// <summary>Every frame: the first time the module runs with the trash and the setting on, his HexQuickStackStorage
        /// trash marks are taken in (once ever: the list's file remembers it).</summary>
        internal static void Update()
        {
            if (importTried || !Module.IsOn || !TrashSettings.On.Value || !TrashSettings.ImportHex.Value)
            {
                return;
            }
            importTried = true;
            int added = List.Import(Path.Combine(MarkList.Folder(), HexFile), HexFile);
            if (added >= 0)
            {
                Module.Log.LogInfo($"Took in {added} trash marks from {HexFile} (read only; never again). " +
                                   $"Your trash marks: {List.Count}, in {Path.GetFileName(List.FilePath)}.");
            }
        }

        /// <summary>The undo step: the borders go at once.</summary>
        internal static void Hide() => ItemMarks.HideBorders(User);

        // Says what a right-click did, top left, as the game's own small notes: "Wood: marked as trash". A change that
        // couldn't be saved changed nothing, and says so.
        private static void Toggled(ItemDrop.ItemData item, bool marked, bool saved)
        {
            string name = Localization.instance != null ? Localization.instance.Localize(item.m_shared.m_name) : item.m_shared.m_name;
            Messages.TopLeft(ToggledText(name, marked, saved, Favorites.IsFavorite(item)));
        }

        /// <summary>The words after a right-click that marked or unmarked (the item's name as he reads it), and whether it's
        /// a favorite, which the can never deletes.</summary>
        internal static string ToggledText(string name, bool marked, bool saved, bool favorite)
        {
            if (!saved)
            {
                return $"{name}: unchanged, your trash marks file couldn't be saved (see the log)";
            }
            if (!marked)
            {
                return $"{name}: no longer trash";
            }
            return favorite ? $"{name}: marked as trash, but it's a favorite, so the trash can keeps it" : $"{name}: marked as trash";
        }
    }
}
