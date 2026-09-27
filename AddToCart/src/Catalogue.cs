using System.Collections.Generic;
using System.Linq;
using QoLMods.Catalogue;
using QoLMods.Chests;
using UnityEngine;

namespace AddToCart
{
    /// <summary>What kind of thumbnail an entry is.</summary>
    internal enum EntryKind
    {
        /// <summary>A building piece (Build tab): its materials go in the cart.</summary>
        Piece,

        /// <summary>A crafting recipe (Craft tab): its ingredients go in the cart.</summary>
        Recipe,

        /// <summary>What a processing station makes (Craft tab): what goes into the station goes in the cart.</summary>
        Conversion,

        /// <summary>An item in the chests (Take tab): the item itself goes in the cart.</summary>
        Take,
    }

    /// <summary>One thumbnail: something he can build, craft or process, or an item he can take, with what one costs.</summary>
    internal sealed class Entry
    {
        /// <summary>Unique and stable: "piece:", "recipe:", "convert:" or "take:" and the game's own names.</summary>
        internal string Key;

        internal EntryKind Kind;

        /// <summary>The name he sees, translated.</summary>
        internal string Name;

        internal Sprite Icon;

        /// <summary>How many one makes (arrows come 20 at a time, a fermenter gives 6 meads); 1 for a piece.</summary>
        internal int Makes = 1;

        /// <summary>What one costs, by item key (ChestStock: "$item_wood", or name@quality), in the game's own order. A
        /// Take item costs one of itself.</summary>
        internal readonly List<Cost> Costs = new List<Cost>();

        /// <summary>The piece's prefab name; null for anything else.</summary>
        internal string PrefabName;

        /// <summary>The item made (or, on Take, the item itself), by its shared name.</summary>
        internal string ItemName;

        /// <summary>A piece's own comfort value (Piece.m_comfort), 0 if none.</summary>
        internal int Comfort;

        internal Piece.ComfortGroup ComfortGroup;

        /// <summary>A Take item's quality, when the item has several (gear); 0 otherwise.</summary>
        internal int Quality;

        /// <summary>Where it sits: its categories, family, kind, biome … (the QoL Mods catalogue part).</summary>
        internal Listing Listing;

        /// <summary>Its name in the game's favorites (GameLists): a piece's prefab, or "item:" and the item's prefab, so
        /// Lox Meat Pie starred on Craft is starred on Take too. Null for an item the game doesn't know.</summary>
        internal string FavoriteKey => Listing?.FavoriteKey;

        /// <summary>True on the Take tab: the cart takes exactly this many, whatever he carries.</summary>
        internal bool Literal => Kind == EntryKind.Take;

        internal bool IsBuild => Kind == EntryKind.Piece;
    }

    internal readonly struct Cost
    {
        internal readonly string Item;
        internal readonly int Amount;

        internal Cost(string item, int amount)
        {
            Item = item;
            Amount = amount;
        }
    }

    /// <summary>
    /// Everything he can build, craft or process, read from the game each time the window opens (he may have learned
    /// something since), and the Take tab's items, read from the chests. Each is classified by the QoL Mods catalogue
    /// part (families, kinds, categories, what a piece is made of, biomes), so every mod of the bundle sorts and groups
    /// the same way.
    ///
    /// Building pieces: every piece of every build tool the game has (hammer, hoe …) that his character knows, and the
    /// seasonal ones while in season, as the game's build menu shows them. Recipes: every recipe his character knows, as
    /// a new item (not an upgrade), the seasonal ones too while in season. Processing: every conversion of every
    /// processing station he knows how to build (smelters and kilns, the windmill, the spinning wheel and the eitr
    /// refinery are the game's Smelter; the Fermenter; the cooking stations and the oven), for an input he has held.
    ///
    /// Left out, because the game's build tools list more than buildings (checked against Jötunn's list of every
    /// vanilla piece, from Valheim 1.0.12, on 2026-09-26): the hoe's terrain actions (Raise Ground, Paved Road: a
    /// TerrainOp), the serving tray's food placements (a food item set down as it is: an ItemDrop), the hammer's Repair
    /// and the remove tools, and the cultivator's plantings (a Plant) unless "List plantings" is on. Also anything
    /// that costs nothing, and recipes that take any one of several ingredients (a later version).
    /// </summary>
    internal static class Catalogue
    {
        /// <summary>Every item's data by shared name: icons, weights, stack sizes, for the materials list.</summary>
        internal static readonly Dictionary<string, ItemDrop.ItemData> Items = new Dictionary<string, ItemDrop.ItemData>();

        /// <summary>What the last Read left out, by reason, with the names: for the log, so a check is one look.</summary>
        internal static readonly Dictionary<string, List<string>> LeftOut = new Dictionary<string, List<string>>();

        /// <summary>How many seasonal pieces and recipes the last Read listed because they are in season.</summary>
        internal static int InSeason;

        internal static List<Entry> Read(Player player, bool listPlantings)
        {
            var entries = new List<Entry>();
            LeftOut.Clear();
            InSeason = 0;
            ObjectDB db = ObjectDB.instance;
            if (db == null || player == null)
            {
                return entries;
            }
            GameFacts.Prepare();
            Items.Clear();
            var seenPieces = new HashSet<string>();
            var stations = new List<Piece>();
            int tool = 0;
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    continue;
                }
                if (!Items.ContainsKey(drop.m_itemData.m_shared.m_name))
                {
                    Items[drop.m_itemData.m_shared.m_name] = drop.m_itemData;
                }
                PieceTable table = drop.m_itemData.m_shared.m_buildPieces;
                if (table != null)
                {
                    // Progression's ties go by each tool's own order, the hammer's first.
                    int order = prefab.name == "Hammer" ? 0 : ++tool * 10000;
                    AddPieces(entries, seenPieces, stations, player, table, Localization.instance.Localize(drop.m_itemData.m_shared.m_name),
                              listPlantings, order);
                }
            }
            foreach (Recipe recipe in db.m_recipes)
            {
                AddRecipe(entries, player, recipe);
            }
            AddConversions(entries, player, stations);
            // Other mods can add a recipe under a name already taken: the first one listed wins.
            var keys = new HashSet<string>();
            entries.RemoveAll(entry => !keys.Add(entry.Key));
            return entries;
        }

        /// <summary>The Take tab: one entry per kind of item the chests may give, from a survey of them.</summary>
        internal static List<Entry> Take(ChestSurvey survey)
        {
            GameFacts.Prepare();
            var entries = new List<Entry>();
            foreach (KeyValuePair<string, ItemDrop.ItemData> each in survey.Samples)
            {
                if (survey.StockOf(each.Key) <= 0)
                {
                    continue;
                }
                ItemDrop.ItemData item = each.Value;
                int quality = item.m_shared.m_maxQuality > 1 ? item.m_quality : 0;
                string shared = item.m_shared.m_name;
                var entry = new Entry
                {
                    Key = "take:" + each.Key,
                    Kind = EntryKind.Take,
                    Name = Localization.instance.Localize(shared) + (quality > 0 ? $", level {quality}" : string.Empty),
                    Icon = item.GetIcon(),
                    ItemName = shared,
                    Quality = quality,
                };
                entry.Costs.Add(new Cost(each.Key, 1));
                entry.Listing = Listings.Item(Shelf.Take, entry.Key, entry.Name, shared, null, string.Empty, 1);
                entries.Add(entry);
            }
            return entries;
        }

        private static void AddPieces(List<Entry> entries, HashSet<string> seen, List<Piece> stations, Player player, PieceTable table,
                                      string tool, bool listPlantings, int order)
        {
            for (int index = 0; index < table.m_pieces.Count; index++)
            {
                GameObject prefab = table.m_pieces[index];
                Piece piece = prefab != null ? prefab.GetComponent<Piece>() : null;
                // As the game's build menu decides (PieceTable.UpdateAvailable): enabled, or a seasonal piece in season.
                bool inSeason = piece != null && !piece.m_enabled && player.CurrentSeason != null && player.CurrentSeason.Pieces.Contains(prefab);
                if (piece == null || !(piece.m_enabled || inSeason) || !player.IsRecipeKnown(piece.m_name) || !seen.Add(prefab.name))
                {
                    continue;
                }
                if (prefab.GetComponent<Smelter>() != null || prefab.GetComponent<Fermenter>() != null || prefab.GetComponent<CookingStation>() != null)
                {
                    stations.Add(piece);
                }
                string notABuilding = piece.m_repairPiece || piece.m_removePiece ? "tool actions (repair, remove)"
                    : prefab.GetComponent<TerrainOp>() != null ? "terrain actions (the hoe's)"
                    : prefab.GetComponent<ItemDrop>() != null ? "items set down as they are (food on a table)"
                    : !listPlantings && prefab.GetComponent<Plant>() != null ? "plantings (the setting \"List plantings\" is off)"
                    : null;
                if (notABuilding != null)
                {
                    if (!LeftOut.TryGetValue(notABuilding, out List<string> names))
                    {
                        names = LeftOut[notABuilding] = new List<string>();
                    }
                    names.Add($"{Localization.instance.Localize(piece.m_name)} ({tool})");
                    continue;
                }
                var entry = new Entry
                {
                    Key = "piece:" + prefab.name,
                    Kind = EntryKind.Piece,
                    Name = Localization.instance.Localize(piece.m_name),
                    Icon = piece.m_icon,
                    PrefabName = prefab.name,
                    Comfort = piece.m_comfort,
                    ComfortGroup = piece.m_comfortGroup,
                };
                foreach (Piece.Requirement need in piece.m_resources)
                {
                    if (need.m_resItem != null && need.m_amount > 0)
                    {
                        entry.Costs.Add(new Cost(need.m_resItem.m_itemData.m_shared.m_name, need.m_amount));
                    }
                }
                if (entry.Costs.Count == 0)
                {
                    continue;
                }
                entry.Listing = Listings.Piece(entry.Key, prefab, piece, order + index);
                if (inSeason)
                {
                    InSeason++;
                }
                entries.Add(entry);
            }
        }

        private static void AddRecipe(List<Entry> entries, Player player, Recipe recipe)
        {
            // Upgrade-only recipes (the Nord gold gear) never make a new item: the game's Craft tab hides them too.
            if (recipe == null || recipe.m_item == null || recipe.m_requireOnlyOneIngredient || recipe.m_noCraftOnlyUpgrade)
            {
                return;
            }
            // As the game decides (Player.GetAvailableRecipes): enabled, or a seasonal recipe in season.
            bool inSeason = !recipe.m_enabled && player.CurrentSeason != null && player.CurrentSeason.Recipes.Contains(recipe);
            if (!recipe.m_enabled && !inSeason)
            {
                return;
            }
            ItemDrop.ItemData item = recipe.m_item.m_itemData;
            if (!player.IsRecipeKnown(item.m_shared.m_name))
            {
                return;
            }
            var entry = new Entry
            {
                Key = "recipe:" + recipe.name,
                Kind = EntryKind.Recipe,
                Name = Localization.instance.Localize(item.m_shared.m_name),
                Icon = item.GetIcon(),
                Makes = Mathf.Max(1, recipe.m_amount),
                ItemName = item.m_shared.m_name,
            };
            // A new item at quality 1, as the game takes it at a station (Player.ConsumeResources): the materials an
            // upgrade alone needs are left out.
            foreach (Piece.Requirement need in recipe.m_resources)
            {
                int amount = need.m_resItem != null && !need.m_upgraderResource ? need.GetAmount(1) : 0;
                if (amount > 0)
                {
                    entry.Costs.Add(new Cost(need.m_resItem.m_itemData.m_shared.m_name, amount));
                }
            }
            if (entry.Costs.Count > 0)
            {
                string station = recipe.m_craftingStation != null ? GameFacts.StationLabel(recipe.m_craftingStation.gameObject) : Taxonomy.ByHand;
                entry.Listing = Listings.Item(Shelf.Craft, entry.Key, entry.Name, item.m_shared.m_name, entry.Costs.Select(cost => cost.Item), station,
                                              Mathf.Max(1, recipe.m_minStationLevel));
                if (inSeason)
                {
                    InSeason++;
                }
                entries.Add(entry);
            }
        }

        // What each processing station he can build makes, for inputs he has held (the game has no "known" for these, so
        // an input he has never had in his hands stays unlisted). The first station listed wins when two make the same
        // thing from the same input (the cooking station and the iron cooking station).
        private static void AddConversions(List<Entry> entries, Player player, List<Piece> stations)
        {
            var made = new List<Entry>();
            var seen = new HashSet<string>();
            foreach (Piece station in stations)
            {
                Smelter smelter = station.GetComponent<Smelter>();
                if (smelter != null)
                {
                    foreach (Smelter.ItemConversion each in smelter.m_conversion)
                    {
                        // A smelter burns m_fuelPerProduct of its fuel for every product (coal, or sap in the eitr
                        // refinery), but only one that holds fuel at all (m_maxFuel > 0; Smelter.UpdateSmelter): a kiln,
                        // the windmill and the spinning wheel burn none.
                        ItemDrop fuel = smelter.m_maxFuel > 0 ? smelter.m_fuelItem : null;
                        AddConversion(made, seen, player, station, each.m_from, each.m_to, 1, fuel, fuel != null ? smelter.m_fuelPerProduct : 0);
                    }
                }
                Fermenter fermenter = station.GetComponent<Fermenter>();
                if (fermenter != null)
                {
                    foreach (Fermenter.ItemConversion each in fermenter.m_conversion)
                    {
                        AddConversion(made, seen, player, station, each.m_from, each.m_to, each.m_producedItems, null, 0);
                    }
                }
                CookingStation cooking = station.GetComponent<CookingStation>();
                if (cooking != null)
                {
                    foreach (CookingStation.ItemConversion each in cooking.m_conversion)
                    {
                        AddConversion(made, seen, player, station, each.m_from, each.m_to, 1, null, 0);
                    }
                }
            }
            // Where two conversions make the same thing (the kiln's coal from wood, fine wood or core wood), each name
            // says what it's made from.
            var outputs = new Dictionary<string, int>();
            foreach (Entry entry in made)
            {
                outputs.TryGetValue(entry.Name, out int count);
                outputs[entry.Name] = count + 1;
            }
            foreach (Entry entry in made)
            {
                if (outputs[entry.Name] > 1)
                {
                    entry.Name += ", from " + Localization.instance.Localize(entry.Costs[0].Item);
                }
                string station = entry.Listing.Station;
                entry.Listing = Listings.Item(Shelf.Craft, entry.Key, entry.Name, entry.ItemName, entry.Costs.Select(cost => cost.Item), station, 1);
            }
            entries.AddRange(made);
        }

        private static void AddConversion(List<Entry> made, HashSet<string> seen, Player player, Piece station, ItemDrop from,
                                          ItemDrop to, int makes, ItemDrop fuel, int fuelPerProduct)
        {
            if (from == null || to == null || !player.IsMaterialKnown(from.m_itemData.m_shared.m_name) || !seen.Add(to.name + "<" + from.name))
            {
                return;
            }
            var entry = new Entry
            {
                Key = $"convert:{station.gameObject.name}:{from.name}",
                Kind = EntryKind.Conversion,
                Name = Localization.instance.Localize(to.m_itemData.m_shared.m_name),
                Icon = to.m_itemData.GetIcon(),
                Makes = Mathf.Max(1, makes),
                ItemName = to.m_itemData.m_shared.m_name,
            };
            entry.Costs.Add(new Cost(from.m_itemData.m_shared.m_name, 1));
            if (fuel != null && fuelPerProduct > 0)
            {
                entry.Costs.Add(new Cost(fuel.m_itemData.m_shared.m_name, fuelPerProduct));
            }
            // The station's label for now; AddConversions fills in the rest once the names are final.
            entry.Listing = new Listing { Shelf = Shelf.Craft, Station = GameFacts.StationLabel(station.gameObject) };
            made.Add(entry);
        }

        /// <summary>A line of what the thumbnail is, for the hover text: where it sits and what it's made of or from.</summary>
        internal static string Describe(Entry entry)
        {
            Listing listing = entry.Listing;
            if (listing == null)
            {
                return string.Empty;
            }
            switch (listing.Shelf)
            {
                case Shelf.Build:
                    string made = listing.Piece.Material.Length > 0 ? $" · made of {listing.Piece.Material}" : string.Empty;
                    string at = listing.Station.Length > 0 ? $" · {listing.Station}" : string.Empty;
                    return $"{string.Join(", ", listing.Piece.Categories)}{made}{at} · {listing.Biome}";
                case Shelf.Craft:
                    string level = listing.Level > 1 ? $" level {listing.Level}" : string.Empty;
                    string sub = listing.Sub.Length > 0 ? $" › {listing.Sub}" : string.Empty;
                    return $"{listing.Station}{level} · {listing.Kind}{sub} · {listing.Biome}";
                default:
                    string section = listing.Section.Length > 0 && listing.Section != listing.Biome ? $" › {listing.Section}" : string.Empty;
                    string gear = listing.Sub.Length > 0 ? $" › {listing.Sub}" : string.Empty;
                    return $"{listing.Family}{section}{gear} · {listing.Biome}";
            }
        }
    }
}
