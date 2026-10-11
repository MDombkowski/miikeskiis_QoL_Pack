// The QoL Mods item marks, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemMarks\. Every mod that uses it
// carries a copy in its own parts\ItemMarks\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.IO;

namespace QoLMods.ItemMarks
{
    /// <summary>
    /// His favourites (card A52): item types he keeps with him. Quick stack never puts them away (StoreStash), sorting and
    /// trash will leave them where they are (card A53), and Loadouts can know them. Every module reads this one list;
    /// StoreStash lets him mark them (Left Shift + right-click by default, as HexQuickStackStorage did) and draws their gold
    /// border, along the cell's edge (MarkUser's place 0). His HexQuickStackStorage list can be taken in once
    /// (ImportFromHex). A module that acts on an item not being a favourite checks List.Readable first.
    /// </summary>
    internal static class Favorites
    {
        /// <summary>HexQuickStackStorage's own list of favourites, in the same folder: one name per line.</summary>
        internal const string HexFile = "HexQuickStackStorage.favorites.txt";

        /// <summary>The list: modprojects.marks.favorites.txt in BepInEx\config.</summary>
        internal static readonly MarkList List = new MarkList("favorites",
            "favorites, the items quick stack leaves with you (and sorting and trash leave where they are).");

        internal static bool IsFavorite(ItemDrop.ItemData item) => List.Has(item);

        /// <summary>Takes in his HexQuickStackStorage favourites, if that file is in BepInEx\config, once ever (the list's
        /// file remembers it); the file is only read. Returns how many were new, or -1 when nothing was taken in.</summary>
        internal static int ImportFromHex() => List.Import(Path.Combine(MarkList.Folder(), HexFile), HexFile);
    }
}
