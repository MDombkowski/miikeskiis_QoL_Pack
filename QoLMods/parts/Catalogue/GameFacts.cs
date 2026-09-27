// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace QoLMods.Catalogue
{
    /// <summary>
    /// Reads the game's own objects into the plain facts the rules take (ItemFacts, PieceFacts), once per world, and
    /// keeps each item's home. Everything is keyed by an item's shared name ("$item_wood"), as the game stacks and
    /// counts items. Call Biomes.Build first: an item's biome decides some homes.
    /// </summary>
    internal static class GameFacts
    {
        /// <summary>The item types that are wielded: only these carry a meaningful skill (the data library's rule).</summary>
        private static readonly HashSet<ItemDrop.ItemData.ItemType> Wielded = new HashSet<ItemDrop.ItemData.ItemType>
        {
            ItemDrop.ItemData.ItemType.OneHandedWeapon, ItemDrop.ItemData.ItemType.Bow, ItemDrop.ItemData.ItemType.Shield,
            ItemDrop.ItemData.ItemType.TwoHandedWeapon, ItemDrop.ItemData.ItemType.Torch, ItemDrop.ItemData.ItemType.Tool,
            ItemDrop.ItemData.ItemType.Attach_Atgeir, ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft,
        };

        private static ObjectDB readFor;
        private static readonly Dictionary<string, GameObject> CanonicalByName = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, HashSet<string>> MadeAtByName = new Dictionary<string, HashSet<string>>();
        private static readonly Dictionary<string, ItemHome> Homes = new Dictionary<string, ItemHome>();
        private static readonly Dictionary<string, ItemFacts> Facts = new Dictionary<string, ItemFacts>();
        private static bool homesHaveBiomes;
        private static bool readHadScene;

        /// <summary>
        /// Reads ObjectDB's items and recipes and the scene's stations, once per world (again if the game's item list is
        /// a new one). Cheap: one pass over each, and nothing at all once done. Returns false while the game has no item
        /// list yet. Every lookup below calls it, so a module needn't; it works out the biomes first if no module has
        /// (silently: a module that wants the biomes' log line calls Biomes.Build with its log before anything else).
        /// </summary>
        internal static bool Prepare()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null)
            {
                return false;
            }
            if (db == readFor && readHadScene && homesHaveBiomes)
            {
                return true;
            }
            Biomes.Build(null);   // nothing when built already, or while it can't be (no world yet)
            // The same item list, read with the world's scene, or with none to read yet (the main menu has an item list and
            // no world): nothing to read again (review 3).
            if (db == readFor && (readHadScene || ZNetScene.instance == null))
            {
                if (!homesHaveBiomes && Biomes.Built)
                {
                    // The homes worked out before the biomes were: Trophies and Creature Parts need them.
                    Homes.Clear();
                    Facts.Clear();
                    homesHaveBiomes = true;
                }
                return true;
            }
            readFor = db;
            readHadScene = ZNetScene.instance != null;   // what the stations make comes from the scene: read again once it exists
            homesHaveBiomes = Biomes.Built;
            CanonicalByName.Clear();
            MadeAtByName.Clear();
            Homes.Clear();
            Facts.Clear();
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null || drop.m_itemData?.m_shared == null || string.IsNullOrEmpty(drop.m_itemData.m_shared.m_name))
                {
                    continue;
                }
                string name = drop.m_itemData.m_shared.m_name;
                if (!CanonicalByName.TryGetValue(name, out GameObject best) || Better(prefab.name, best.name))
                {
                    CanonicalByName[name] = prefab;
                }
            }
            // What makes each item: every enabled recipe's station (or By Hand), and every processing station's
            // conversions, the Obliterator left out. Food made at a cauldron, oven, ketill or preparation table is a
            // Prepared Dish.
            foreach (Recipe recipe in db.m_recipes)
            {
                if (recipe == null || !recipe.m_enabled || recipe.m_item == null)
                {
                    continue;
                }
                AddMadeAt(recipe.m_item.m_itemData.m_shared.m_name,
                          recipe.m_craftingStation != null ? StationLabel(recipe.m_craftingStation.gameObject) : Taxonomy.ByHand);
            }
            ZNetScene scene = ZNetScene.instance;
            if (scene != null)
            {
                foreach (GameObject prefab in scene.m_prefabs)
                {
                    if (prefab == null || prefab.name == Taxonomy.Obliterator)
                    {
                        continue;
                    }
                    try
                    {
                        foreach (ItemDrop made in Outputs(prefab))
                        {
                            AddMadeAt(made.m_itemData.m_shared.m_name, StationLabel(prefab));
                        }
                    }
                    catch (Exception)
                    {
                        // One odd prefab (another mod's) leaves only itself out.
                    }
                }
            }
            return true;
        }

        // Prefabs sharing one $ name are one item: the one that stands for them all is the first by name (as the data
        // library orders them), skipping the game's copies (FW_ and SP_ gear, a feast's _Material) when there is another.
        internal static bool Better(string candidate, string current)
        {
            bool candidateCopy = IsCopy(candidate);
            bool currentCopy = IsCopy(current);
            if (candidateCopy != currentCopy)
            {
                return currentCopy;
            }
            return string.CompareOrdinal(candidate, current) < 0;
        }

        internal static bool IsCopy(string prefab) => prefab.StartsWith("FW_") || prefab.StartsWith("SP_") || prefab.EndsWith("_Material");

        private static IEnumerable<ItemDrop> Outputs(GameObject prefab)
        {
            Smelter smelter = prefab.GetComponent<Smelter>();
            if (smelter != null && smelter.m_conversion != null)
            {
                foreach (Smelter.ItemConversion each in smelter.m_conversion)
                {
                    if (each?.m_to != null)
                    {
                        yield return each.m_to;
                    }
                }
            }
            Fermenter fermenter = prefab.GetComponent<Fermenter>();
            if (fermenter != null && fermenter.m_conversion != null)
            {
                foreach (Fermenter.ItemConversion each in fermenter.m_conversion)
                {
                    if (each?.m_to != null)
                    {
                        yield return each.m_to;
                    }
                }
            }
            CookingStation cooking = prefab.GetComponent<CookingStation>();
            if (cooking != null && cooking.m_conversion != null)
            {
                foreach (CookingStation.ItemConversion each in cooking.m_conversion)
                {
                    if (each?.m_to != null)
                    {
                        yield return each.m_to;
                    }
                }
            }
        }

        private static void AddMadeAt(string name, string station)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            if (!MadeAtByName.TryGetValue(name, out HashSet<string> stations))
            {
                MadeAtByName[name] = stations = new HashSet<string>();
            }
            stations.Add(station);
        }

        /// <summary>A station's label, by its prefab's name (its own for the game's, the game's name for another mod's).</summary>
        internal static string StationLabel(GameObject station)
        {
            if (station == null)
            {
                return Taxonomy.ByHand;
            }
            if (Taxonomy.StationLabel.TryGetValue(station.name, out string label))
            {
                return label;
            }
            CraftingStation crafting = station.GetComponent<CraftingStation>();
            Piece piece = station.GetComponent<Piece>();
            string token = crafting != null ? crafting.m_name : piece != null ? piece.m_name : station.name;
            return Localize(token);
        }

        /// <summary>The prefab that stands for every prefab with this shared name, or null.</summary>
        internal static GameObject Canonical(string sharedName) =>
            sharedName != null && Prepare() && CanonicalByName.TryGetValue(sharedName, out GameObject prefab) ? prefab : null;

        /// <summary>An item's name in the lists (GameLists.ItemKey): its canonical prefab's, so one item has one star
        /// whichever tab shows it.</summary>
        internal static string ItemKey(string sharedName)
        {
            GameObject prefab = Canonical(sharedName);
            return prefab != null ? GameLists.ItemKey(prefab.name) : null;
        }

        /// <summary>The facts the rules need about an item, by its shared name; null for a name no item has.</summary>
        internal static ItemFacts FactsOf(string sharedName)
        {
            if (sharedName == null || !Prepare())
            {
                return null;
            }
            if (Facts.TryGetValue(sharedName, out ItemFacts known))
            {
                return known;
            }
            GameObject prefab = Canonical(sharedName);
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return null;
            }
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            bool eats = shared.m_food != 0f || shared.m_foodStamina != 0f || shared.m_foodEitr != 0f;
            var facts = new ItemFacts
            {
                Prefab = prefab.name,
                Type = shared.m_itemType.ToString(),
                Skill = Wielded.Contains(shared.m_itemType) ? shared.m_skillType.ToString() : string.Empty,
                Health = eats ? shared.m_food : 0f,
                Stamina = eats ? shared.m_foodStamina : 0f,
                Eitr = eats ? shared.m_foodEitr : 0f,
                Duration = eats ? shared.m_foodBurnTime : 0f,
                Value = shared.m_value,
                Biome = Biomes.LabelOf(sharedName),
                BiomeHow = Biomes.HowOf(sharedName),
            };
            if (MadeAtByName.TryGetValue(sharedName, out HashSet<string> stations))
            {
                facts.MadeAt.UnionWith(stations);
            }
            Facts[sharedName] = facts;
            return facts;
        }

        /// <summary>Where an item lives (its family, member, kind …), by its shared name; worked out once per world.</summary>
        internal static ItemHome HomeOf(string sharedName)
        {
            if (sharedName == null || !Prepare())
            {
                return null;
            }
            if (Homes.TryGetValue(sharedName, out ItemHome home))
            {
                return home;
            }
            ItemFacts facts = FactsOf(sharedName);
            home = facts != null ? Items.Home(facts) : ItemHome.Unknown;
            Homes[sharedName] = home;
            return home;
        }

        /// <summary>The facts the rules need about a piece.</summary>
        internal static PieceFacts PieceOf(GameObject prefab, Piece piece)
        {
            var facts = new PieceFacts { Prefab = prefab.name };
            foreach (Piece.UsageTagFlags flag in Enum.GetValues(typeof(Piece.UsageTagFlags)))
            {
                if (flag != 0 && (piece.m_usage & flag) == flag)
                {
                    facts.Flags.Add(flag.ToString());
                }
            }
            if (piece.m_resources != null)
            {
                foreach (Piece.Requirement need in piece.m_resources)
                {
                    if (need?.m_resItem == null || need.m_amount <= 0)
                    {
                        continue;
                    }
                    string name = need.m_resItem.m_itemData.m_shared.m_name;
                    GameObject canonical = Canonical(name);
                    facts.Costs.Add(new PieceCost
                    {
                        Prefab = canonical != null ? canonical.name : need.m_resItem.name,
                        Name = Localize(name),
                        Amount = need.m_amount,
                        Biome = Biomes.LabelOf(name),
                        Family = HomeOf(name)?.Family ?? string.Empty,
                    });
                }
            }
            return facts;
        }

        /// <summary>A $ token in the player's language; the token itself when there is no text for it.</summary>
        internal static string Localize(string token)
        {
            if (string.IsNullOrEmpty(token) || Localization.instance == null)
            {
                return token ?? string.Empty;
            }
            return Localization.instance.Localize(token);
        }
    }
}
