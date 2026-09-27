using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QoLMods.Catalogue;
using QoLMods.Chests;
using UnityEngine;
using UnityEngine.UI;

namespace AddToCart
{
    /// <summary>
    /// Favorites and lists, the same everywhere: they are the game's own (the build menu's favorites and its favorite
    /// categories, through the catalogue part's GameLists). A build piece is starred under its prefab's name, as the game
    /// stars it; a recipe, a processing product and a Take item are starred as the item they make or are ("item:LoxPie"),
    /// so one item has one star on every tab. A list made here shows in the build menu's Favorites, and the other way round.
    /// </summary>
    internal static class Favorites
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Set on the character once its 0.2.0 favorites have been moved into the game's.</summary>
        private const string MovedMarker = "modprojects.addtocart.favorites.moved";

        private static FieldInfo pieceButtonPrefab;
        private static FieldInfo favoriteStar;
        private static Sprite star;

        /// <summary>At start: finds the game's favorites and its star. Without them the window has no favorites or lists.</summary>
        internal static void Resolve(List<string> missingOptional)
        {
            GameLists.Resolve(missingOptional);
            pieceButtonPrefab = typeof(BuildUi).GetField("m_pieceButtonPrefab", AnyInstance);
            favoriteStar = typeof(BuildUiPieceButton).GetField("m_favoriteStar", AnyInstance);
        }

        /// <summary>The game's store, or null (before the character is loaded, or if the game changed).</summary>
        internal static FavoritePieceList Store => GameLists.Store;

        internal static bool IsFavorite(FavoritePieceList store, Entry entry) => GameLists.IsStarred(store, entry.FavoriteKey);

        internal static bool InList(FavoritePieceList store, Entry entry, int list) => GameLists.InList(store, entry.FavoriteKey, list);

        /// <summary>The build menu's own favorite star, or null (the window then draws its own mark).</summary>
        internal static Sprite Star()
        {
            if (star != null || pieceButtonPrefab == null || favoriteStar == null || Hud.instance == null || Hud.instance.m_buildUi == null)
            {
                return star;
            }
            var prefab = pieceButtonPrefab.GetValue(Hud.instance.m_buildUi) as GameObject;
            BuildUiPieceButton button = prefab != null ? prefab.GetComponent<BuildUiPieceButton>() : null;
            var image = button != null ? favoriteStar.GetValue(button) as Image : null;
            star = image != null ? image.sprite : null;
            return star;
        }

        /// <summary>
        /// Moves this character's 0.2.0 favorites (recipes, processing and Take items, kept in the character's custom
        /// data; build pieces were the game's already) into the game's store, once. The old line stays where it was.
        /// </summary>
        internal static void MoveOldOnes()
        {
            FavoritePieceList store = Store;
            if (store == null || ObjectDB.instance == null || Memory.Moved(MovedMarker))
            {
                return;
            }
            IList<string> old = Memory.FavoritesOf020();
            int moved = 0;
            var missing = new List<string>();
            foreach (string key in old)
            {
                string favorite = NewKey(key);
                if (favorite == null)
                {
                    missing.Add(key);
                    continue;
                }
                GameLists.Star(store, favorite);
                moved++;
            }
            Memory.MarkMoved(MovedMarker, $"0.3.0: {moved} of {old.Count}");
            if (old.Count > 0)
            {
                Module.Log.LogInfo($"Moved {moved} of this character's {old.Count} Add to Cart 0.2.0 favorites into the game's own favorites" +
                                   (missing.Count > 0 ? $"; not found in this game: {string.Join(", ", missing)}." : "."));
            }
        }

        // 0.2.0's thumbnail keys, to the store's names (OldFavorites), with the game's own answers.
        private static string NewKey(string key) => OldFavorites.NewKey(key, RecipeMakes, StationMakes, GameFacts.ItemKey);

        private static string RecipeMakes(string recipeName)
        {
            Recipe recipe = ObjectDB.instance.m_recipes.FirstOrDefault(each => each != null && each.name == recipeName);
            return recipe != null && recipe.m_item != null ? recipe.m_item.m_itemData.m_shared.m_name : null;
        }

        private static string StationMakes(string stationPrefab, string inputPrefab)
        {
            GameObject station = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(stationPrefab) : null;
            ItemDrop made = station != null ? Output(station, inputPrefab) : null;
            return made != null ? made.m_itemData.m_shared.m_name : null;
        }

        private static ItemDrop Output(GameObject station, string from)
        {
            Smelter smelter = station.GetComponent<Smelter>();
            if (smelter != null)
            {
                Smelter.ItemConversion hit = smelter.m_conversion.FirstOrDefault(each => each?.m_from != null && each.m_from.name == from);
                if (hit != null)
                {
                    return hit.m_to;
                }
            }
            Fermenter fermenter = station.GetComponent<Fermenter>();
            if (fermenter != null)
            {
                Fermenter.ItemConversion hit = fermenter.m_conversion.FirstOrDefault(each => each?.m_from != null && each.m_from.name == from);
                if (hit != null)
                {
                    return hit.m_to;
                }
            }
            CookingStation cooking = station.GetComponent<CookingStation>();
            if (cooking != null)
            {
                CookingStation.ItemConversion hit = cooking.m_conversion.FirstOrDefault(each => each?.m_from != null && each.m_from.name == from);
                if (hit != null)
                {
                    return hit.m_to;
                }
            }
            return null;
        }
    }
}
