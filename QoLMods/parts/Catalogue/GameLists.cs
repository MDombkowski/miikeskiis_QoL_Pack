// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace QoLMods.Catalogue
{
    /// <summary>
    /// His favorites and his lists are the game's own: the build menu's favorites (BuildUi.m_favoritePieceList, a
    /// FavoritePieceList saved with the character). A list is one of its favorite categories, so a list made in either
    /// window shows in both, and a piece starred in either is starred in both. Items and recipes live in the same store
    /// under a prefix ("item:LoxPie"): the game keeps names it doesn't know, never shows them, and writes them back on
    /// every save, so a friend without the mod loses nothing (the build-menu research in the vault, section 4; checked
    /// against FavoritePieceList of Valheim 1.0.16).
    ///
    /// The three cautions the research found are kept here, and only here: AddPiece throws on a name already there, so
    /// IsFavorite is asked first; only valid category numbers are ever used; and the save's bytes are never touched, only
    /// the store's own methods.
    /// </summary>
    internal static class GameLists
    {
        /// <summary>The prefix of an item's name in the store: the serving tray's pieces are named after items, so an
        /// item's bare name would star that food on the tray too.</summary>
        internal const string ItemPrefix = "item:";

        /// <summary>The game's rules for a category name (FavoritePieceList.ValidateTagName).</summary>
        internal const int MaxNameLength = 20;

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static bool resolved;
        private static FieldInfo storeField;
        private static FieldInfo piecesField;
        private static FieldInfo tagsField;
        private static FieldInfo tagsChangedField;

        /// <summary>
        /// Called from each using module's Resolve. Without the store, a module offers no favorites or lists: it adds a
        /// note to <paramref name="missingOptional"/> rather than switching off.
        /// </summary>
        internal static void Resolve(List<string> missingOptional)
        {
            if (!resolved)
            {
                resolved = true;
                storeField = typeof(BuildUi).GetField("m_favoritePieceList", AnyInstance);
                if (storeField != null && storeField.FieldType != typeof(FavoritePieceList))
                {
                    storeField = null;
                }
                piecesField = typeof(FavoritePieceList).GetField("m_favoriteBuildPieces", AnyInstance);
                if (piecesField != null && piecesField.FieldType != typeof(Dictionary<string, HashSet<int>>))
                {
                    piecesField = null;
                }
                tagsField = typeof(FavoritePieceList).GetField("m_tags", AnyInstance);
                if (tagsField != null && tagsField.FieldType != typeof(List<string>))
                {
                    tagsField = null;
                }
                // The event's own field, to tell the build menu a list was renamed (it redraws its lists on this).
                tagsChangedField = typeof(FavoritePieceList).GetField(nameof(FavoritePieceList.TagsChanged), AnyInstance);
                if (tagsChangedField != null && tagsChangedField.FieldType != typeof(Action))
                {
                    tagsChangedField = null;
                }
            }
            if (storeField == null || piecesField == null)
            {
                missingOptional.Add("BuildUi.m_favoritePieceList (no favorites or lists)");
            }
            else if (tagsField == null)
            {
                missingOptional.Add("FavoritePieceList.m_tags (lists can't be renamed)");
            }
        }

        /// <summary>The store, once the game's build menu exists (in a world, with the character loaded); else null.</summary>
        internal static FavoritePieceList Store
        {
            get
            {
                if (storeField == null || piecesField == null || Hud.instance == null || Hud.instance.m_buildUi == null)
                {
                    return null;
                }
                return storeField.GetValue(Hud.instance.m_buildUi) as FavoritePieceList;
            }
        }

        internal static bool CanRename => tagsField != null;

        /// <summary>A piece's name in the store: its prefab's name, as the game keys it.</summary>
        internal static string PieceKey(string prefab) => prefab;

        /// <summary>An item's name in the store: the prefix and its prefab's name.</summary>
        internal static string ItemKey(string prefab) => ItemPrefix + prefab;

        internal static bool IsStarred(FavoritePieceList store, string key) => store != null && key != null && store.IsFavorite(key);

        /// <summary>Stars it (with no list), unless it is starred already.</summary>
        internal static void Star(FavoritePieceList store, string key)
        {
            if (store != null && key != null && !store.IsFavorite(key))
            {
                store.AddPiece(key);
            }
        }

        /// <summary>Takes its star away, and with it every list it was in: the game's "Remove from Favorites".</summary>
        internal static void Unstar(FavoritePieceList store, string key)
        {
            if (store != null && key != null && store.IsFavorite(key))
            {
                store.RemovePiece(key);
            }
        }

        internal static int ListCount(FavoritePieceList store) => store?.TagCount ?? 0;

        internal static string ListName(FavoritePieceList store, int list) =>
            store != null && list >= 0 && list < store.TagCount ? store.GetTagDisplayName(list) : string.Empty;

        /// <summary>Whether it is in that list.</summary>
        internal static bool InList(FavoritePieceList store, string key, int list)
        {
            Dictionary<string, HashSet<int>> pieces = Pieces(store);
            return pieces != null && key != null && pieces.TryGetValue(key, out HashSet<int> lists) && lists.Contains(list);
        }

        /// <summary>Puts it in the list or takes it out; putting it in stars it first, as the game does.</summary>
        internal static void SetInList(FavoritePieceList store, string key, int list, bool member)
        {
            if (store == null || key == null || list < 0 || list >= store.TagCount)
            {
                return;   // only a valid category number, ever
            }
            if (member)
            {
                Star(store, key);
                if (!InList(store, key, list))
                {
                    store.AddPieceTag(key, list);
                }
            }
            else if (InList(store, key, list))
            {
                store.RemovePieceTag(key, list);
            }
        }

        /// <summary>Why the game wouldn't take this name for a list, or null if it would.</summary>
        internal static string Refuse(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Give the list a name.";
            }
            if (name.Trim().Length > MaxNameLength)
            {
                return $"Up to {MaxNameLength} characters, as the game allows.";
            }
            if (name.Contains("$"))
            {
                return "The game doesn't allow $ in a list's name.";
            }
            return null;
        }

        /// <summary>Makes a new list (the game's "Add Category..."), last; returns its number, or -1.</summary>
        internal static int AddList(FavoritePieceList store, string name)
        {
            if (store == null || Refuse(name) != null)
            {
                return -1;
            }
            store.AddTag(name.Trim());
            return store.TagCount - 1;
        }

        /// <summary>Renames a list. The game has no rename of its own, but its list of names allows it: nothing else
        /// refers to a list by its name.</summary>
        internal static bool Rename(FavoritePieceList store, int list, string name)
        {
            if (store == null || tagsField == null || list < 0 || list >= store.TagCount || Refuse(name) != null)
            {
                return false;
            }
            var names = (List<string>)tagsField.GetValue(store);
            names[list] = name.Trim();
            try
            {
                // Tells the build menu, which then shows the new name in its lists.
                (tagsChangedField?.GetValue(store) as Action)?.Invoke();
            }
            catch (Exception)
            {
                // The game's own handler failed: the name is changed all the same, and the build menu shows it when it
                // next draws its lists. Not the module's error, so it doesn't switch the module off.
            }
            return true;
        }

        private static Dictionary<string, HashSet<int>> Pieces(FavoritePieceList store) =>
            store != null ? piecesField.GetValue(store) as Dictionary<string, HashSet<int>> : null;
    }
}
