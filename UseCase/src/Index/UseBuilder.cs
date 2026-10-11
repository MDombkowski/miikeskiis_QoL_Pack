using System;
using System.Collections.Generic;
using System.Linq;

namespace UseCase
{
    /// <summary>
    /// Turns the facts read from one world into every use of every item, keyed by the item's $ name. The research's traps
    /// are kept out here, where the harness tests them (A45, research section 1): disabled recipes and pieces (seasonal
    /// ones are kept, marked), upgrade-only recipes (upgrading only), per-level amounts on items that can't be upgraded,
    /// amounts of 0, and self-uses (marked, left out by a setting). Plain logic, no game.
    /// </summary>
    internal static class UseBuilder
    {
        internal static Dictionary<string, List<Use>> Build(GameFactsRead facts)
        {
            var byResource = new Uses();
            foreach (RecipeFact recipe in facts.Recipes)
            {
                AddRecipe(byResource, recipe);
            }
            foreach (PieceFact piece in facts.Pieces)
            {
                AddPiece(byResource, piece);
            }
            foreach (CreatureFact creature in facts.Creatures)
            {
                foreach (string food in creature.Eats)
                {
                    byResource.Add(new Use { Kind = UseKind.Taming, Resource = food, Token = creature.Token, Prefab = creature.Prefab, Mod = creature.Mod,
                        KnownBy = KnownBy.Material, KnownKey = creature.KnownItem, Sort = creature.Sort });
                }
                if (creature.Saddle.Length > 0)
                {
                    byResource.Add(new Use { Kind = UseKind.Taming, Resource = creature.Saddle, Token = creature.Token, Prefab = creature.Prefab, Note = "saddle",
                        Mod = creature.Mod, KnownBy = KnownBy.Material, KnownKey = creature.KnownItem, Sort = creature.Sort });
                }
            }
            foreach (FishFact fish in facts.Fish)
            {
                foreach (string bait in fish.Baits)
                {
                    byResource.Add(new Use { Kind = UseKind.FishingBait, Resource = bait, Token = fish.Token, Prefab = fish.Prefab, Mod = fish.Mod,
                        KnownBy = KnownBy.Material, KnownKey = fish.Token, Sort = fish.Sort });
                }
            }
            foreach (TraderFact trader in facts.Traders)
            {
                int order = 0;
                foreach (Ware ware in trader.Sells)
                {
                    if (ware.Item.Length == 0 || ware.Price <= 0)
                    {
                        continue;
                    }
                    byResource.Add(new Use { Kind = UseKind.Buying, Resource = facts.Coins, Token = ware.Item, Prefab = ware.ItemPrefab, Amount = ware.Price,
                        Makes = Math.Max(1, ware.Stack), Note = trader.Token, KnownBy = KnownBy.GlobalKey, KnownKey = ware.RequiredKey,
                        Sort = ware.Sort, GameOrder = order++ });
                }
                foreach (TakeBack back in trader.TakesBack)
                {
                    if (back.Item.Length > 0)
                    {
                        byResource.Add(new Use { Kind = UseKind.Traders, Resource = back.Item, Token = trader.Token, Prefab = trader.Prefab,
                            Note = back.Unlocks > 0 ? $"unlocks {back.Unlocks} of her wares" : string.Empty, KnownBy = KnownBy.Always });
                    }
                }
            }
            int placeOrder = 0;
            foreach (PlaceFact place in facts.Places)
            {
                if (place.Item.Length == 0)
                {
                    continue;
                }
                byResource.Add(new Use { Kind = place.Kind, Resource = place.Item, Token = place.Token, Prefab = place.Prefab,
                    Amount = place.Count > 0 ? place.Count : -1, Note = place.Note,
                    KnownBy = place.AlwaysKnown ? KnownBy.Always : KnownBy.Seen, KnownKey = place.SeenKey, BeatenKey = place.BeatenKey,
                    Sort = place.Sort, GameOrder = placeOrder++ });
            }
            return byResource.ByResource;
        }

        private static void AddRecipe(Uses byResource, RecipeFact recipe)
        {
            // A recipe the game has switched off is no use, unless it is a seasonal one, kept and marked with its season.
            if (recipe.Item.Length == 0 || (!recipe.Enabled && recipe.Season.Length == 0))
            {
                return;
            }
            bool inSeason = recipe.Season.Length == 0 || recipe.InSeason;
            foreach (Cost cost in recipe.Costs)
            {
                if (cost.Item.Length == 0)
                {
                    continue;
                }
                bool self = cost.Item == recipe.Item;
                // Crafting: what one craft takes. An upgrade-only recipe's first cost is never paid (the Deep North's cast
                // weapons and their moulds), and an idol is taken only when upgrading.
                if (!recipe.UpgradeOnly && cost.Amount > 0 && !cost.Upgrader)
                {
                    byResource.Add(new Use { Kind = UseKind.Crafting, Resource = cost.Item, Token = recipe.Item, Prefab = recipe.ItemPrefab, OtherName = recipe.Name,
                        Amount = cost.Amount, Makes = Math.Max(1, recipe.Makes), Station = recipe.Station, Season = recipe.Season, InSeason = inSeason,
                        Mod = recipe.Mod, SelfUse = self, KnownBy = KnownBy.Recipe, KnownKey = recipe.Item, Sort = recipe.Sort, GameOrder = recipe.GameOrder });
                }
                // Upgrading: only an item that can be upgraded, whatever per-level amounts the data carries (the Torch, the
                // bombs and arrows carry some), and only what some level really takes.
                if (recipe.MaxQuality > 1 && cost.Levels != null && cost.Levels.Any(level => level > 0))
                {
                    int perLevel = cost.PerLevel > 0 && !cost.Upgrader ? cost.PerLevel : cost.Levels.First(level => level > 0);
                    byResource.Add(new Use { Kind = UseKind.Upgrading, Resource = cost.Item, Token = recipe.Item, Prefab = recipe.ItemPrefab, OtherName = recipe.Name,
                        Amount = perLevel, Levels = cost.Levels.ToArray(), Upgrader = cost.Upgrader, Note = cost.Upgrader ? recipe.UpgraderStation : string.Empty,
                        Station = recipe.Station, Season = recipe.Season, InSeason = inSeason,
                        Mod = recipe.Mod, SelfUse = self, KnownBy = KnownBy.Recipe, KnownKey = recipe.Item, Sort = recipe.Sort, GameOrder = recipe.GameOrder });
                }
            }
        }

        private static void AddPiece(Uses byResource, PieceFact piece)
        {
            if (piece.Token.Length == 0 || (!piece.Enabled && piece.Season.Length == 0))
            {
                return;
            }
            bool inSeason = piece.Season.Length == 0 || piece.InSeason;
            Use Make(UseKind kind, string resource, int amount) => new Use
            {
                Kind = kind, Resource = resource, Token = piece.Token, Prefab = piece.Prefab, Amount = amount, Tool = piece.Tool, ToolOrder = piece.ToolOrder,
                Station = piece.Station, Season = piece.Season, InSeason = inSeason, Mod = piece.Mod, KnownBy = KnownBy.Piece, KnownKey = piece.Token,
                Sort = piece.Sort, GameOrder = piece.GameOrder,
            };
            foreach (Cost cost in piece.Costs)
            {
                if (cost.Item.Length > 0 && cost.Amount > 0)
                {
                    Use use = Make(UseKind.Building, cost.Item, cost.Amount);
                    // The serving tray's food and feasts cost themselves (a tray piece shares its $ name with its food), as
                    // does the hoe's placeable stone.
                    use.SelfUse = cost.Item == piece.Token;
                    byResource.Add(use);
                }
            }
            foreach (FuelFact fuel in piece.Fuel)
            {
                if (fuel.Item.Length > 0)
                {
                    Use use = Make(UseKind.Refueling, fuel.Item, fuel.Holds > 0 ? fuel.Holds : -1);
                    use.KeptLit = piece.KeptLit;
                    byResource.Add(use);
                }
            }
            foreach (ConversionFact conversion in piece.Conversions)
            {
                if (conversion.From.Length > 0 && conversion.To.Length > 0)
                {
                    Use use = Make(UseKind.Processing, conversion.From, conversion.Holds > 0 ? conversion.Holds : -1);
                    use.PerProduct = conversion.PerProduct;
                    use.Makes = Math.Max(1, conversion.Makes);
                    use.SelfUse = conversion.From == conversion.To;
                    byResource.Add(use);
                }
            }
            foreach (string firework in piece.Fireworks)
            {
                if (firework.Length > 0)
                {
                    byResource.Add(Make(UseKind.Fireworks, firework, -1));
                }
            }
            foreach (FuelFact ammo in piece.Ammo)
            {
                if (ammo.Item.Length > 0)
                {
                    byResource.Add(Make(UseKind.Ammo, ammo.Item, ammo.Holds > 0 ? ammo.Holds : -1));
                }
            }
            foreach (string trophy in piece.Targets)
            {
                if (trophy.Length > 0)
                {
                    byResource.Add(Make(UseKind.BallistaTargets, trophy, -1));
                }
            }
        }

        // Two uses that read the same are kept once (a piece in two tools' tables, a recipe listed twice).
        private sealed class Uses
        {
            internal readonly Dictionary<string, List<Use>> ByResource = new Dictionary<string, List<Use>>(StringComparer.Ordinal);
            private readonly Dictionary<string, HashSet<string>> said = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            internal void Add(Use use)
            {
                if (!ByResource.TryGetValue(use.Resource, out List<Use> uses))
                {
                    ByResource[use.Resource] = uses = new List<Use>();
                    said[use.Resource] = new HashSet<string>(StringComparer.Ordinal);
                }
                if (said[use.Resource].Add(use.SameAs()))
                {
                    uses.Add(use);
                }
            }
        }
    }
}
