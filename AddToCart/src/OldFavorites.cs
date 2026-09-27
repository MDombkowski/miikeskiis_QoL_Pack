using System;
using QoLMods.Catalogue;
using QoLMods.Chests;

namespace AddToCart
{
    /// <summary>
    /// 0.2.0 kept the favorites of recipes, processing and Take items itself, by thumbnail key; 0.3.0 keeps every favorite
    /// in the game's own store (GameLists). This turns an old key into the store's name for the same thing, given the
    /// game's answers as lookups, so the mapping can be checked outside the game.
    /// </summary>
    internal static class OldFavorites
    {
        /// <summary>
        /// The store's name for a 0.2.0 favorite, or null if the thing is gone from the game (a removed mod's).
        /// <paramref name="recipeMakes"/>: a recipe's name to the shared name of what it makes;
        /// <paramref name="stationMakes"/>: a station's prefab and an input's prefab to the shared name of what it makes;
        /// <paramref name="itemKey"/>: an item's shared name to its name in the store.
        /// </summary>
        internal static string NewKey(string key, Func<string, string> recipeMakes, Func<string, string, string> stationMakes,
                                      Func<string, string> itemKey)
        {
            int colon = key != null ? key.IndexOf(':') : -1;
            if (colon <= 0 || colon == key.Length - 1)
            {
                return null;
            }
            string kind = key.Substring(0, colon);
            string rest = key.Substring(colon + 1);
            switch (kind)
            {
                case "piece":
                    return GameLists.PieceKey(rest);
                case "take":
                    // "take:$item_sword" or "take:$item_sword@3": one star for the item, whatever its level.
                    ChestStock.Parse(rest, out string name, out _);
                    return string.IsNullOrEmpty(name) ? null : itemKey(name);
                case "recipe":
                    string made = recipeMakes(rest);
                    return made != null ? itemKey(made) : null;
                case "convert":
                    // "convert:<station prefab>:<input prefab>"
                    int split = rest.IndexOf(':');
                    if (split <= 0 || split == rest.Length - 1)
                    {
                        return null;
                    }
                    string output = stationMakes(rest.Substring(0, split), rest.Substring(split + 1));
                    return output != null ? itemKey(output) : null;
                default:
                    return null;
            }
        }
    }
}
