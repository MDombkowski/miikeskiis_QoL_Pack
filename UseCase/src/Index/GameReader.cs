using System;
using System.Collections.Generic;
using System.Linq;
using QoLMods.Catalogue;
using UnityEngine;

namespace UseCase
{
    /// <summary>
    /// Reads one world's recipes, tools' pieces, creatures, fish and traders into plain facts, once per world after
    /// ObjectDB and ZNetScene are ready (and again when their signature changes), so other mods' recipes and pieces count
    /// too. Every member it reads was checked against the decompiled game 1.0.16 (2026-09-30). One odd object (another
    /// mod's) leaves only itself out. The places inside the world's locations come from Places.
    /// </summary>
    internal static class GameReader
    {
        /// <summary>The tools whose tables come first, in this order; the rest follow in the game's item order.</summary>
        private static readonly string[] ToolsFirst = { "Hammer", "Cultivator", "Hoe" };

        internal static GameFactsRead Read(Action<string> warn)
        {
            ObjectDB db = ObjectDB.instance;
            ZNetScene scene = ZNetScene.instance;
            var facts = new GameFactsRead();
            var seasons = Seasons.Read();
            var mods = ModNames.Read();
            int odd = 0;

            Biomes.Build(null);   // nothing when a module (Add to Cart) built them already this world
            TorchesStayLit.Read();

            // Recipes, numbered as the game's crafting list orders them before "craftable first": weight, then $ name.
            List<Recipe> recipes = db.m_recipes.Where(recipe => recipe != null && recipe.m_item != null && recipe.m_item.m_itemData?.m_shared != null && HasDlc(recipe.m_item.m_itemData.m_shared.m_dlc)).ToList();
            var menuOrder = new Dictionary<Recipe, int>();
            foreach (Recipe recipe in recipes.OrderBy(recipe => recipe.m_listSortWeight).ThenBy(recipe => recipe.m_item.m_itemData.m_shared.m_name, StringComparer.Ordinal))
            {
                menuOrder[recipe] = menuOrder.Count;
            }
            string upgrader = UpgraderStation();
            foreach (Recipe recipe in recipes)
            {
                try
                {
                    facts.Recipes.Add(ReadRecipe(recipe, seasons, mods, menuOrder[recipe], upgrader));
                }
                catch (Exception)
                {
                    odd++;
                }
            }

            // Every tool's pieces, each piece once (two tools can share a table, or a piece).
            var tables = new List<(PieceTable Table, string Tool, int Order)>();
            foreach ((GameObject prefab, int index) in db.m_items.Select((prefab, index) => (prefab, index)))
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                PieceTable table = drop?.m_itemData?.m_shared?.m_buildPieces;
                if (table != null && tables.All(each => each.Table != table))
                {
                    int first = Array.IndexOf(ToolsFirst, prefab.name);
                    tables.Add((table, drop.m_itemData.m_shared.m_name, first >= 0 ? first : ToolsFirst.Length + index));
                }
            }
            tables.Sort((a, b) => a.Order.CompareTo(b.Order));
            var piecesSeen = new HashSet<GameObject>();
            int toolOrder = 0;
            int gameOrder = 0;
            foreach ((PieceTable table, string tool, _) in tables)
            {
                foreach (GameObject prefab in table.m_pieces ?? new List<GameObject>())
                {
                    gameOrder++;
                    if (prefab == null || !piecesSeen.Add(prefab))
                    {
                        continue;
                    }
                    try
                    {
                        Piece piece = prefab.GetComponent<Piece>();
                        if (piece != null && HasDlc(piece.m_dlc))
                        {
                            facts.Pieces.Add(ReadPiece(prefab, piece, tool, toolOrder, gameOrder, seasons, mods, facts.Anything));
                        }
                    }
                    catch (Exception)
                    {
                        odd++;
                    }
                }
                toolOrder++;
            }
            ReadCatapultAnything(db, facts.Anything);

            // Creatures that can be tamed, fish and traders, from the scene's prefabs; fish also from the items.
            var fishSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameObject prefab in scene.m_prefabs.Concat(db.m_items))
            {
                if (prefab == null)
                {
                    continue;
                }
                try
                {
                    ReadCreature(prefab, facts, mods);
                    ReadFish(prefab, facts, mods, fishSeen);
                    ReadTrader(prefab, facts);
                    ReadGuardianStone(prefab, facts);
                }
                catch (Exception)
                {
                    odd++;
                }
            }
            facts.Places.AddRange(Places.Facts());
            if (odd > 0)
            {
                warn?.Invoke($"{odd} of the game's objects (another mod's?) couldn't be read, so their uses aren't listed.");
            }
            return facts;
        }

        /// <summary>A cheap fingerprint of the recipes and the tools' tables: when it changes (a server's recipe editor
        /// after joining, a mod adding pieces late), the index is read again.</summary>
        internal static int Signature()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null)
            {
                return 0;
            }
            unchecked
            {
                int hash = db.m_recipes.Count * 31 + db.m_items.Count;
                foreach (Recipe recipe in db.m_recipes)
                {
                    if (recipe == null)
                    {
                        continue;
                    }
                    hash = hash * 31 + recipe.m_amount + (recipe.m_enabled ? 7 : 3);
                    Piece.Requirement[] needs = recipe.m_resources;
                    if (needs == null)
                    {
                        continue;
                    }
                    foreach (Piece.Requirement need in needs)
                    {
                        hash = hash * 31 + (need == null ? 0 : need.m_amount * 17 + need.m_amountPerLevel + (need.m_resItem == null ? 0 : need.m_resItem.GetInstanceID()));
                    }
                }
                foreach (GameObject prefab in db.m_items)
                {
                    PieceTable table = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
                    if (table?.m_pieces == null)
                    {
                        continue;
                    }
                    hash = hash * 31 + table.m_pieces.Count;
                    // Each piece's costs too, so a mod that changes them after the world loaded is followed.
                    foreach (GameObject pieceObject in table.m_pieces)
                    {
                        Piece piece = pieceObject != null ? pieceObject.GetComponent<Piece>() : null;
                        Piece.Requirement[] costs = piece != null ? piece.m_resources : null;
                        if (costs == null)
                        {
                            continue;
                        }
                        foreach (Piece.Requirement cost in costs)
                        {
                            hash = hash * 31 + (cost == null ? 0 : cost.m_amount * 17 + (cost.m_resItem == null ? 0 : cost.m_resItem.GetInstanceID()));
                        }
                    }
                }
                return hash;
            }
        }

        private static RecipeFact ReadRecipe(Recipe recipe, Seasons seasons, ModNames mods, int menuOrder, string upgrader)
        {
            ItemDrop.ItemData.SharedData shared = recipe.m_item.m_itemData.m_shared;
            string season = recipe.m_enabled ? null : seasons.Of(recipe);
            var fact = new RecipeFact
            {
                Name = recipe.name,
                Item = shared.m_name,
                ItemPrefab = recipe.m_item.name,
                Makes = Math.Max(1, recipe.m_amount),
                Enabled = recipe.m_enabled || seasons.OnNow(recipe),
                Season = season ?? string.Empty,
                InSeason = season == null || seasons.OnNow(recipe),
                UpgradeOnly = recipe.m_noCraftOnlyUpgrade,
                MaxQuality = shared.m_maxQuality,
                Station = recipe.m_craftingStation != null ? GameFacts.StationLabel(recipe.m_craftingStation.gameObject) : string.Empty,
                UpgraderStation = upgrader,
                OnlyOne = recipe.m_requireOnlyOneIngredient,
                Mod = mods.OfRecipe(recipe),
                GameOrder = menuOrder,
            };
            var costNames = new List<string>();
            foreach (Piece.Requirement need in recipe.m_resources ?? new Piece.Requirement[0])
            {
                if (need?.m_resItem == null)
                {
                    continue;
                }
                var cost = new Cost
                {
                    Item = need.m_resItem.m_itemData.m_shared.m_name,
                    Amount = need.m_amount,
                    PerLevel = need.m_amountPerLevel,
                    Upgrader = need.m_upgraderResource,
                };
                if (shared.m_maxQuality > 1)
                {
                    // The game's own cost of each upgrade (Piece.Requirement.GetAmount), from quality 2 up.
                    cost.Levels = Enumerable.Range(2, shared.m_maxQuality - 1).Select(need.GetAmount).ToArray();
                }
                fact.Costs.Add(cost);
                costNames.Add(cost.Item);
            }
            fact.Sort = Listings.Item(Shelf.Craft, "recipe:" + recipe.name, GameFacts.Localize(shared.m_name), shared.m_name, costNames,
                fact.Station.Length > 0 ? fact.Station : Taxonomy.ByHand, Math.Max(1, recipe.m_minStationLevel));
            return fact;
        }

        private static PieceFact ReadPiece(GameObject prefab, Piece piece, string tool, int toolOrder, int gameOrder, Seasons seasons, ModNames mods,
                                           AnythingFacts anything)
        {
            string season = piece.m_enabled ? null : seasons.Of(prefab);
            var fact = new PieceFact
            {
                Prefab = prefab.name,
                Token = piece.m_name ?? string.Empty,
                Tool = tool,
                ToolOrder = toolOrder,
                GameOrder = gameOrder,
                Enabled = piece.m_enabled || seasons.OnNow(prefab),
                Season = season ?? string.Empty,
                InSeason = season == null || seasons.OnNow(prefab),
                Station = piece.m_craftingStation != null ? GameFacts.StationLabel(piece.m_craftingStation.gameObject) : string.Empty,
                KeptLit = TorchesStayLit.Keeps(prefab.name),
                Mod = mods.OfPrefab(prefab.name),
            };
            foreach (Piece.Requirement need in piece.m_resources ?? new Piece.Requirement[0])
            {
                if (need?.m_resItem != null)
                {
                    fact.Costs.Add(new Cost { Item = need.m_resItem.m_itemData.m_shared.m_name, Amount = need.m_amount });
                }
            }

            // What it takes once built, from its components, on the root or a child (the battering ram's engine is a
            // child "Smelter" that burns wood into nothing).
            Fireplace fire = prefab.GetComponentInChildren<Fireplace>(true);
            if (fire != null && fire.m_canRefill)   // one that can't be refilled takes neither fuel nor fireworks (UseItem)
            {
                if (fire.m_fuelItem != null && !fire.m_infiniteFuel)
                {
                    fact.Fuel.Add(new FuelFact { Item = NameOf(fire.m_fuelItem), Holds = (int)fire.m_maxFuel });
                }
                foreach (Fireplace.FireworkItem firework in fire.m_fireworkItemList ?? new Fireplace.FireworkItem[0])
                {
                    if (firework.m_fireworkItem != null)
                    {
                        fact.Fireworks.Add(NameOf(firework.m_fireworkItem));
                    }
                }
            }
            foreach (Smelter smelter in prefab.GetComponentsInChildren<Smelter>(true))
            {
                if (smelter.m_fuelItem != null && smelter.m_maxFuel > 0)
                {
                    fact.Fuel.Add(new FuelFact { Item = NameOf(smelter.m_fuelItem), Holds = smelter.m_maxFuel });
                }
                foreach (Smelter.ItemConversion conversion in smelter.m_conversion ?? new List<Smelter.ItemConversion>())
                {
                    if (conversion?.m_from == null)
                    {
                        continue;   // the Frigid Kiln's makes Liquid Frost from its fuel alone
                    }
                    if (conversion.m_to == null)
                    {
                        fact.Fuel.Add(new FuelFact { Item = NameOf(conversion.m_from), Holds = smelter.m_maxOre });   // burnt for power
                        continue;
                    }
                    fact.Conversions.Add(new ConversionFact { From = NameOf(conversion.m_from), To = NameOf(conversion.m_to), Holds = smelter.m_maxOre, PerProduct = 1 });
                }
            }
            CookingStation cooking = prefab.GetComponentInChildren<CookingStation>(true);
            if (cooking != null)
            {
                if (cooking.m_useFuel && cooking.m_fuelItem != null)
                {
                    fact.Fuel.Add(new FuelFact { Item = NameOf(cooking.m_fuelItem), Holds = cooking.m_maxFuel });
                }
                int slots = cooking.m_slots != null ? cooking.m_slots.Length : 1;
                foreach (CookingStation.ItemConversion conversion in cooking.m_conversion ?? new List<CookingStation.ItemConversion>())
                {
                    if (conversion?.m_from != null && conversion.m_to != null)
                    {
                        fact.Conversions.Add(new ConversionFact { From = NameOf(conversion.m_from), To = NameOf(conversion.m_to), Holds = slots, PerProduct = 1 });
                    }
                }
            }
            Fermenter fermenter = prefab.GetComponentInChildren<Fermenter>(true);
            if (fermenter != null)
            {
                foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion ?? new List<Fermenter.ItemConversion>())
                {
                    if (conversion?.m_from != null && conversion.m_to != null)
                    {
                        fact.Conversions.Add(new ConversionFact { From = NameOf(conversion.m_from), To = NameOf(conversion.m_to), Holds = 1, PerProduct = 1,
                            Makes = conversion.m_producedItems });
                    }
                }
            }
            Incinerator obliterator = prefab.GetComponentInChildren<Incinerator>(true);
            if (obliterator != null)
            {
                foreach (Incinerator.IncineratorConversion conversion in obliterator.m_conversions ?? new List<Incinerator.IncineratorConversion>())
                {
                    if (conversion?.m_result == null || conversion.m_requirements == null)
                    {
                        continue;
                    }
                    foreach (Incinerator.Requirement need in conversion.m_requirements)
                    {
                        if (need?.m_resItem != null && need.m_amount > 0)
                        {
                            string from = NameOf(need.m_resItem);
                            fact.Conversions.Add(new ConversionFact { From = from, To = NameOf(conversion.m_result), Holds = need.m_amount, PerProduct = need.m_amount,
                                Makes = conversion.m_resultAmount });
                            anything.ObliteratorListed.Add(from);
                        }
                    }
                }
                if (obliterator.m_defaultResult != null && anything.Obliterator == null)
                {
                    anything.Obliterator = fact;
                    anything.ObliteratorCost = Math.Max(1, obliterator.m_defaultCost);
                    anything.ObliteratorResult = NameOf(obliterator.m_defaultResult);
                }
            }
            ShieldGenerator shield = prefab.GetComponentInChildren<ShieldGenerator>(true);
            if (shield != null)
            {
                foreach (ItemDrop fuel in shield.m_fuelItems ?? new List<ItemDrop>())
                {
                    if (fuel != null)
                    {
                        fact.Fuel.Add(new FuelFact { Item = NameOf(fuel), Holds = shield.m_maxFuel });
                    }
                }
            }
            Turret turret = prefab.GetComponentInChildren<Turret>(true);
            if (turret != null)
            {
                foreach (Turret.AmmoType ammo in turret.m_allowedAmmo ?? new List<Turret.AmmoType>())
                {
                    if (ammo.m_ammo != null)
                    {
                        fact.Ammo.Add(new FuelFact { Item = NameOf(ammo.m_ammo), Holds = turret.m_maxAmmo });
                    }
                }
                foreach (Turret.TrophyTarget target in turret.m_configTargets ?? new List<Turret.TrophyTarget>())
                {
                    if (target.m_item != null)
                    {
                        fact.Targets.Add(NameOf(target.m_item));
                    }
                }
            }
            Catapult catapult = prefab.GetComponentInChildren<Catapult>(true);
            if (catapult != null)
            {
                foreach (ItemDrop payload in catapult.m_includeItemsOverride ?? new List<ItemDrop>())
                {
                    if (payload != null)
                    {
                        fact.Ammo.Add(new FuelFact { Item = NameOf(payload), Holds = catapult.m_maxLoadStack });
                    }
                }
                if (anything.Catapult == null)
                {
                    anything.Catapult = fact;
                    anything.CatapultHolds = Math.Max(1, catapult.m_maxLoadStack);
                }
            }
            fact.Sort = Listings.Piece("piece:" + prefab.name, prefab, piece, gameOrder);
            return fact;
        }

        // Catapult.CanItemBeLoaded, for every item (it is private and wants an item in the world): its own payloads
        // first (Ammo already), then only a model with an "attach" point, then its type rule and its exclusions.
        private static void ReadCatapultAnything(ObjectDB db, AnythingFacts anything)
        {
            GameObject piece = anything.Catapult != null ? ZNetScene.instance.GetPrefab(anything.Catapult.Prefab) : null;
            Catapult catapult = piece != null ? piece.GetComponentInChildren<Catapult>(true) : null;
            if (catapult == null)
            {
                return;
            }
            var included = new HashSet<string>((catapult.m_includeItemsOverride ?? new List<ItemDrop>()).Where(each => each != null).Select(NameOf));
            var excluded = new HashSet<string>((catapult.m_excludeItemsOverride ?? new List<ItemDrop>()).Where(each => each != null).Select(NameOf));
            List<ItemDrop.ItemData.ItemType> types = catapult.m_includeExcludeTypesList ?? new List<ItemDrop.ItemData.ItemType>();
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop?.m_itemData?.m_shared == null)
                {
                    continue;
                }
                string name = drop.m_itemData.m_shared.m_name;
                if (included.Contains(name))
                {
                    continue;
                }
                if (catapult.m_onlyUseIncludedProjectiles && prefab.transform.Find("attach") == null)
                {
                    continue;
                }
                bool listed = types.Contains(drop.m_itemData.m_shared.m_itemType);
                if (catapult.m_defaultIncludeAndListExclude ? listed : !listed)
                {
                    continue;
                }
                if (!excluded.Contains(name))
                {
                    anything.CatapultTakes.Add(name);
                }
            }
        }

        private static void ReadCreature(GameObject prefab, GameFactsRead facts, ModNames mods)
        {
            Tameable tameable = prefab.GetComponent<Tameable>();
            MonsterAI ai = prefab.GetComponent<MonsterAI>();
            Character character = prefab.GetComponent<Character>();
            if (tameable == null || ai == null || character == null)
            {
                return;
            }
            var fact = new CreatureFact { Prefab = prefab.name, Token = character.m_name ?? string.Empty, Mod = mods.OfPrefab(prefab.name) };
            foreach (ItemDrop food in ai.m_consumeItems ?? new List<ItemDrop>())
            {
                if (food != null)
                {
                    fact.Eats.Add(NameOf(food));
                }
            }
            if (tameable.m_saddleItem != null)
            {
                fact.Saddle = NameOf(tameable.m_saddleItem);
            }
            // What proves he has met it: its trophy, or else the first thing it drops.
            CharacterDrop drops = prefab.GetComponent<CharacterDrop>();
            string trophy = null;
            string first = null;
            foreach (CharacterDrop.Drop drop in drops != null && drops.m_drops != null ? drops.m_drops : new List<CharacterDrop.Drop>())
            {
                ItemDrop item = drop?.m_prefab != null ? drop.m_prefab.GetComponent<ItemDrop>() : null;
                if (item == null)
                {
                    continue;
                }
                first ??= NameOf(item);
                if (item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy)
                {
                    trophy ??= NameOf(item);
                }
            }
            fact.KnownItem = trophy ?? first ?? string.Empty;
            fact.Sort = new Listing
            {
                Shelf = Shelf.Take,
                Key = "creature:" + prefab.name,
                Name = GameFacts.Localize(fact.Token),
                Biome = fact.KnownItem.Length > 0 ? Biomes.LabelOf(fact.KnownItem) : Taxonomy.OtherBiome,
            };
            if (fact.Eats.Count > 0 || fact.Saddle.Length > 0)
            {
                facts.Creatures.Add(fact);
            }
        }

        private static void ReadFish(GameObject prefab, GameFactsRead facts, ModNames mods, HashSet<string> seen)
        {
            Fish fish = prefab.GetComponent<Fish>();
            ItemDrop item = prefab.GetComponent<ItemDrop>();
            if (fish == null || item == null || !seen.Add(prefab.name))
            {
                return;
            }
            string name = NameOf(item);
            var fact = new FishFact { Prefab = prefab.name, Token = name, Mod = mods.OfPrefab(prefab.name) };
            foreach (Fish.BaitSetting bait in fish.m_baits ?? new List<Fish.BaitSetting>())
            {
                if (bait?.m_bait != null && bait.m_chance > 0f)
                {
                    fact.Baits.Add(NameOf(bait.m_bait));
                }
            }
            fact.Sort = Listings.Item(Shelf.Take, "fish:" + prefab.name, GameFacts.Localize(name), name, null, string.Empty, 1);
            if (fact.Baits.Count > 0)
            {
                facts.Fish.Add(fact);
            }
        }

        private static void ReadTrader(GameObject prefab, GameFactsRead facts)
        {
            Trader trader = prefab.GetComponent<Trader>();
            if (trader == null || facts.Traders.Any(each => each.Prefab == prefab.name))
            {
                return;
            }
            // The Bog Witch's name field is empty in 1.0.16; her name is still in the game's words, as "$npc_bogwitch".
            string token = string.IsNullOrEmpty(trader.m_name) ? "$npc_" + prefab.name.ToLowerInvariant() : trader.m_name;
            var fact = new TraderFact { Prefab = prefab.name, Token = token };
            foreach (Trader.TradeItem ware in trader.m_items ?? new List<Trader.TradeItem>())
            {
                if (ware?.m_prefab == null)
                {
                    continue;   // a "player key item" (a map, a service) has no item
                }
                string name = NameOf(ware.m_prefab);
                fact.Sells.Add(new Ware
                {
                    Item = name,
                    ItemPrefab = ware.m_prefab.name,
                    Stack = ware.m_stack,
                    Price = ware.m_price,
                    RequiredKey = ware.m_requiredGlobalKey ?? string.Empty,
                    Sort = Listings.Item(Shelf.Take, "ware:" + prefab.name + ":" + ware.m_prefab.name, GameFacts.Localize(name), name, null, string.Empty, 1),
                });
            }
            foreach (Trader.TraderUseItem use in trader.m_useItems ?? new List<Trader.TraderUseItem>())
            {
                if (use?.m_prefab != null)
                {
                    // Handing it over sets a key her wares wait for (Hildir's chests: 11, 9 and 10 of them).
                    int unlocks = string.IsNullOrEmpty(use.m_setsGlobalKey) ? 0
                        : (trader.m_items ?? new List<Trader.TradeItem>()).Count(ware => ware != null && string.Equals(ware.m_requiredGlobalKey, use.m_setsGlobalKey, StringComparison.OrdinalIgnoreCase));
                    fact.TakesBack.Add(new TakeBack { Item = NameOf(use.m_prefab), Unlocks = unlocks });
                }
            }
            facts.Traders.Add(fact);
        }

        // The guardian stones: every stone (the start temple's seven, the Queen's and Fader's spawned into it later) and
        // the Deep North's platform are prefabs of the scene, each with the item stand that takes its trophy or blood.
        private static void ReadGuardianStone(GameObject prefab, GameFactsRead facts)
        {
            BossStone stone = prefab.GetComponent<BossStone>();
            ItemStand stand = stone != null ? stone.m_itemStand : null;
            if (stand == null || stand.m_supportedItems == null || facts.Places.Any(place => place.Prefab == prefab.name))
            {
                return;
            }
            // Named by the power it gives ("Eikthyr"), else by the stand's own name ("Chiselled Platform").
            string token = stand.m_guardianPower != null && !string.IsNullOrEmpty(stand.m_guardianPower.m_name) ? stand.m_guardianPower.m_name : stand.m_name ?? prefab.name;
            foreach (ItemDrop item in stand.m_supportedItems)
            {
                if (item == null)
                {
                    continue;
                }
                string name = NameOf(item);
                facts.Places.Add(new PlaceFact
                {
                    Kind = UseKind.GuardianStones,
                    Token = token,
                    Prefab = prefab.name,
                    Item = name,
                    AlwaysKnown = true,
                    Sort = new Listing { Shelf = Shelf.Take, Key = "stone:" + prefab.name, Name = GameFacts.Localize(token), Biome = Biomes.LabelOf(name) },
                });
            }
        }

        // The station that upgrades with idols (the Forge of Potential), as its label; empty when none.
        private static string UpgraderStation()
        {
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                CraftingStation station = prefab != null ? prefab.GetComponent<CraftingStation>() : null;
                if (station != null && station.m_upgrader)
                {
                    return GameFacts.StationLabel(prefab);
                }
            }
            return string.Empty;
        }

        internal static string NameOf(ItemDrop item) => item.m_itemData.m_shared.m_name;

        // A recipe or piece of a DLC not installed is one the game never teaches or offers (Player.HaveRequirements).
        private static bool HasDlc(string dlc) => string.IsNullOrEmpty(dlc) || DLCMan.instance == null || DLCMan.instance.IsDLCInstalled(dlc);
    }
}
