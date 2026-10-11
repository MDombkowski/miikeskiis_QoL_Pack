using QoLMods.ItemMarks;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// His favourites, as StoreStash lets him set and see them (card A52): the key held with a right-click marks or unmarks an
    /// item's kind, a gold border shows it, and his HexQuickStackStorage list is taken in once. The list itself is the
    /// pack's (QoLMods\parts\ItemMarks: Favorites), so sorting and trash (card A53) and Loadouts read the same one.
    /// </summary>
    internal static class FavoriteMarks
    {
        /// <summary>StoreStash as the item marks part knows it: favourites, their border along the cell's edge, with its
        /// settings.</summary>
        internal static readonly MarkUser User = new MarkUser(Module.Name, Favorites.List, 0, () => Module.IsOn, Module.Guard,
            () => Settings.FavoriteModifier.Value, () => Settings.Borders.Value, () => Settings.BorderColor.Value,
            () => Settings.InChestWindows.Value, Toggled);

        private static bool importTried;

        /// <summary>Every frame: the first time the module runs switched on with the setting on, his HexQuickStackStorage
        /// favourites are taken in (once ever: the list's file remembers it).</summary>
        internal static void Update()
        {
            if (importTried || !Module.IsOn || !Settings.ImportHex.Value)
            {
                return;
            }
            importTried = true;
            int added = Favorites.ImportFromHex();
            if (added >= 0)
            {
                Module.Log.LogInfo($"Took in {added} favorites from {Favorites.HexFile} (read only; never again). " +
                                   $"Your favorites: {Favorites.List.Count}, in {System.IO.Path.GetFileName(Favorites.List.FilePath)}.");
            }
        }

        /// <summary>The undo step: the borders go at once.</summary>
        internal static void Hide() => ItemMarks.HideBorders(User);

        // Says what a right-click did, top left, as the game's own small notes: "Wood: a favorite now". A change that
        // couldn't be saved changed nothing, and says so.
        private static void Toggled(ItemDrop.ItemData item, bool favorite, bool saved)
        {
            string name = Localization.instance != null ? Localization.instance.Localize(item.m_shared.m_name) : item.m_shared.m_name;
            Messages.TopLeft(ToggledText(name, favorite, saved));
        }

        /// <summary>The words after a right-click that marked or unmarked (the item's name as he reads it).</summary>
        internal static string ToggledText(string name, bool favorite, bool saved)
        {
            if (!saved)
            {
                return $"{name}: unchanged, your favorites file couldn't be saved (see the log)";
            }
            return favorite ? $"{name}: a favorite now, kept when you quick stack" : $"{name}: no longer a favorite";
        }
    }
}
