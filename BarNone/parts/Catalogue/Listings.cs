// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;
using UnityEngine;

namespace QoLMods.Catalogue
{
    /// <summary>
    /// The things a browsing window lists, made from the game's own objects the one way every mod makes them: where each
    /// sits, its biome, its order, its name in the game's favorites and what the filter box searches. A module gives each
    /// its own key (Add to Cart's "piece:…", "recipe:…") and keeps whatever else it needs beside it.
    /// </summary>
    internal static class Listings
    {
        /// <summary>
        /// A building piece (Build). <paramref name="gameOrder"/> is its place in its tool's piece table, the hammer's
        /// first: progression's tie-break. Its biome is the latest of what it costs and what the station it needs costs
        /// (a stone wall costs stone, but the stonecutter needs iron).
        /// </summary>
        internal static Listing Piece(string key, GameObject prefab, Piece piece, int gameOrder)
        {
            PieceHome home = Pieces.Home(GameFacts.PieceOf(prefab, piece));
            var from = new List<string>();
            var costNames = new List<string>();
            AddCosts(piece.m_resources, from, costNames);
            Piece station = piece.m_craftingStation != null ? piece.m_craftingStation.GetComponent<Piece>() : null;
            if (station != null)
            {
                AddCosts(station.m_resources, from, null);
            }
            var listing = new Listing
            {
                Shelf = Shelf.Build,
                Key = key,
                FavoriteKey = GameLists.PieceKey(prefab.name),
                Name = GameFacts.Localize(piece.m_name),
                Biome = Biomes.English(Biomes.Latest(from)),
                GameOrder = gameOrder,
                Station = piece.m_craftingStation != null ? GameFacts.StationLabel(piece.m_craftingStation.gameObject) : string.Empty,
                Piece = home,
            };
            listing.Search = Browse.SearchText(listing, costNames, null);
            return listing;
        }

        /// <summary>
        /// An item: what a recipe or a station makes (Craft), or an item itself (Take). <paramref name="name"/> is as the
        /// module shows it ("Iron Sword, level 3", "Coal, from Wood"); <paramref name="costs"/> are the shared names of
        /// what one costs (Craft), for the filter box.
        /// </summary>
        internal static Listing Item(Shelf shelf, string key, string name, string sharedName, IEnumerable<string> costs, string station, int level)
        {
            ItemFacts facts = GameFacts.FactsOf(sharedName);
            var listing = new Listing
            {
                Shelf = shelf,
                Key = key,
                FavoriteKey = GameFacts.ItemKey(sharedName),
                Name = name ?? string.Empty,
                Biome = Biomes.LabelOf(sharedName),
                Station = station ?? string.Empty,
                Level = level,
                Item = GameFacts.HomeOf(sharedName) ?? ItemHome.Unknown,
                Health = facts?.Health ?? 0f,
                Stamina = facts?.Stamina ?? 0f,
                Eitr = facts?.Eitr ?? 0f,
                Duration = facts?.Duration ?? 0f,
            };
            var costNames = new List<string>();
            if (costs != null)
            {
                foreach (string cost in costs)
                {
                    costNames.Add(GameFacts.Localize(cost));
                }
            }
            listing.Search = Browse.SearchText(listing, costNames, facts?.Type);
            return listing;
        }

        private static void AddCosts(Piece.Requirement[] resources, List<string> sharedNames, List<string> localized)
        {
            if (resources == null)
            {
                return;
            }
            foreach (Piece.Requirement need in resources)
            {
                if (need?.m_resItem != null && need.m_amount > 0)
                {
                    string name = need.m_resItem.m_itemData.m_shared.m_name;
                    sharedNames.Add(name);
                    localized?.Add(GameFacts.Localize(name));
                }
            }
        }
    }
}
