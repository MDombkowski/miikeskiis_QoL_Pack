using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UseCase
{
    /// <summary>
    /// Which mod added a piece, an item or a recipe, for the setting "Things other mods add". Known for mods built with
    /// Jötunn (its ModRegistry), and for any mod once some mod has switched on Jötunn's ModQuery; anything else counts as
    /// the game's own. Reached only through small methods, each compiled on its own and its failure caught, so a Jötunn
    /// without these leaves every thing counted as the game's own, never the module off.
    /// </summary>
    internal sealed class ModNames
    {
        private readonly Dictionary<string, string> byPrefab = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> byRecipe = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static ModNames Read()
        {
            var names = new ModNames();
            try
            {
                FromRegistry(names.byPrefab, names.byRecipe);
            }
            catch (Exception)
            {
                // No registry: every thing counts as the game's own.
            }
            return names;
        }

        internal string OfPrefab(string prefab)
        {
            if (string.IsNullOrEmpty(prefab))
            {
                return string.Empty;
            }
            if (byPrefab.TryGetValue(prefab, out string mod))
            {
                return mod;
            }
            try
            {
                return FromQuery(prefab) ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        internal string OfRecipe(Recipe recipe)
        {
            if (recipe == null)
            {
                return string.Empty;
            }
            if (byRecipe.TryGetValue(recipe.name, out string mod))
            {
                return mod;
            }
            return recipe.m_item != null ? OfPrefab(recipe.m_item.name) : string.Empty;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void FromRegistry(Dictionary<string, string> byPrefab, Dictionary<string, string> byRecipe)
        {
            foreach (Jotunn.Entities.CustomPiece piece in Jotunn.Utils.ModRegistry.GetPieces())
            {
                if (piece?.PiecePrefab != null && piece.SourceMod != null)
                {
                    byPrefab[piece.PiecePrefab.name] = piece.SourceMod.Name;
                }
            }
            foreach (Jotunn.Entities.CustomItem item in Jotunn.Utils.ModRegistry.GetItems())
            {
                if (item?.ItemPrefab != null && item.SourceMod != null)
                {
                    byPrefab[item.ItemPrefab.name] = item.SourceMod.Name;
                }
            }
            foreach (Jotunn.Entities.CustomRecipe recipe in Jotunn.Utils.ModRegistry.GetRecipes())
            {
                if (recipe?.Recipe != null && recipe.SourceMod != null)
                {
                    byRecipe[recipe.Recipe.name] = recipe.SourceMod.Name;
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string FromQuery(string prefab) => Jotunn.Utils.ModQuery.GetPrefab(prefab)?.SourceMod?.Name;
    }
}
