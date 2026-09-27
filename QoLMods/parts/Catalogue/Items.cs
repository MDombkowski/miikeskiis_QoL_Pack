// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;

namespace QoLMods.Catalogue
{
    /// <summary>
    /// What the rules need to know about one item: plain values, read from the game (GameFacts) or, in a test, from the
    /// Valheim data library, so the same rules run in both.
    /// </summary>
    internal sealed class ItemFacts
    {
        /// <summary>The item's prefab name. Prefabs sharing one $ name are one item to the game: the canonical one
        /// (GameFacts.Canonical) stands for all of them.</summary>
        internal string Prefab;

        /// <summary>The game's item type (ItemDrop.ItemData.ItemType), by name.</summary>
        internal string Type;

        /// <summary>The skill it trains (Skills.SkillType), by name, for things that are wielded (weapons, shields, bows,
        /// torches, tools); empty for anything else, whose skill field is only the game's default.</summary>
        internal string Skill = string.Empty;

        internal float Health;
        internal float Stamina;
        internal float Eitr;

        /// <summary>How long the food lasts, in seconds.</summary>
        internal float Duration;

        /// <summary>What a trader pays for it (m_value): only valuables have one.</summary>
        internal int Value;

        /// <summary>Its biome of origin (Biomes), as a label; "Other" if unplaced.</summary>
        internal string Biome = Taxonomy.OtherBiome;

        /// <summary>How the biome was reached: the kind of source ("creatures", "homes", "vegetation", "recipes" …).</summary>
        internal string BiomeHow = string.Empty;

        /// <summary>The stations (labels) that make it, the Obliterator left out: for food's preparation.</summary>
        internal readonly HashSet<string> MadeAt = new HashSet<string>();
    }

    /// <summary>
    /// Where an item lives in the Take tab's families, and what else the rules say about it. Read-only: GameFacts keeps
    /// one per item for the whole world, shared by every module of the plugin.
    /// </summary>
    internal sealed class ItemHome
    {
        internal readonly string Family;

        /// <summary>Its member of the family (Gear's Armor, Metals' Iron …); for Trophies and Creature Parts, its biome;
        /// empty for a family without members.</summary>
        internal readonly string Section;

        /// <summary>For Gear: the tool's use, the weapon's type or the ammunition's type; empty otherwise.</summary>
        internal readonly string Sub;

        /// <summary>For Gear: its equipment slot; empty otherwise.</summary>
        internal readonly string Slot;

        /// <summary>For Food: Simple Foods, Prepared Dishes or Cooking Ingredients; empty otherwise.</summary>
        internal readonly string Preparation;

        /// <summary>Its kind on the Craft tab (what a recipe making it counts as), and the kind's sub-heading.</summary>
        internal readonly string Kind;

        internal readonly string KindSub;

        /// <summary>Its place in its hand-placed list (Taxonomy.Explicit), for the order within a heading; 999 if none.</summary>
        internal readonly int ExplicitOrder;

        internal ItemHome(string family, string section, string sub, string slot, string preparation, string kind, string kindSub, int explicitOrder)
        {
            Family = family ?? "Other";
            Section = section ?? string.Empty;
            Sub = sub ?? string.Empty;
            Slot = slot ?? string.Empty;
            Preparation = preparation ?? string.Empty;
            Kind = kind ?? "Other";
            KindSub = kindSub ?? string.Empty;
            ExplicitOrder = explicitOrder;
        }

        /// <summary>For a name no item has: Other, Odds and Ends.</summary>
        internal static readonly ItemHome Unknown = new ItemHome("Other", "Odds and Ends", string.Empty, string.Empty, string.Empty, "Other", "Other", 999);
    }

    /// <summary>
    /// The Take families and the Craft kinds: every item gets exactly one home, by these rules in this order (the mock-up's
    /// classify, gear_sub and preparation, ported). Items from other mods fall back by their game type: weapons by skill,
    /// armour by slot, the rest to Other; nothing is ever hidden.
    /// </summary>
    internal static class Items
    {
        internal static ItemHome Home(ItemFacts it)
        {
            (string family, string section) = Classify(it);
            string sub = string.Empty;
            string slot = string.Empty;
            if (family == "Gear")
            {
                sub = GearSub(it, section);
                slot = Taxonomy.Slot.TryGetValue(it.Type ?? string.Empty, out string found) ? found : string.Empty;
            }
            string preparation = Preparation(it, family, section);
            (string kind, string kindSub) = CraftKind(family, section, sub);
            int order = Taxonomy.ExplicitOrder.TryGetValue(it.Prefab ?? string.Empty, out int at) ? at : 999;
            return new ItemHome(family, section, sub, slot, preparation, kind, kindSub, order);
        }

        private static (string, string) Classify(ItemFacts it)
        {
            string p = it.Prefab ?? string.Empty;
            string t = it.Type ?? string.Empty;
            if (Taxonomy.ToolPrefabs.ContainsKey(p) || p.StartsWith("FishingBait") || it.Skill == "Pickaxes")
            {
                return ("Gear", "Tools");
            }
            if (Taxonomy.SiegeAmmo.Contains(p))
            {
                return ("Gear", "Ammo");
            }
            if (p.StartsWith("FireworksRocket"))
            {
                return ("Other", "Fireworks");
            }
            if (p.StartsWith("Upgrader"))
            {
                return ("Magic and Rare", "Idols");
            }
            if (p.StartsWith("Mold") || (p.EndsWith("Uncooked") && (p.Contains("Gold") || p.Contains("Staff"))))
            {
                return ("Metals", "Moulds and Casts");
            }
            if (t == "Trophy")
            {
                return ("Trophies", it.Biome);
            }
            if (Taxonomy.GearKind.TryGetValue(t, out string gear))
            {
                return ("Gear", gear);
            }
            if (it.Value > 0)
            {
                return ("Valuables", string.Empty);
            }
            if (Taxonomy.ExplicitFamily.TryGetValue(p, out string family))
            {
                switch (family)
                {
                    case "Metals":
                        return (family, Taxonomy.MetalOf.TryGetValue(p, out string metal) ? metal : "Nails and Fittings");
                    case "Magic and Rare":
                        return (family, Taxonomy.MagicSection[p]);
                    case "Boss Items":
                        return (family, Taxonomy.BossSummon.Contains(p) ? "Summoning" : "Boss Loot");
                    case "Nature":
                        return (family, Taxonomy.NatureSection.TryGetValue(p, out string nature) ? nature : "Seeds");
                    case "Hides and Fabrics":
                        return (family, Taxonomy.Threads.Contains(p) ? "Threads and Fabrics" : "Hides and Pelts");
                    case "Meads and Potions":
                        return (family, "Brewing Ingredients");
                    case "Other":
                        return (family, p.Contains("Key") || p.Contains("hildir") || p.Contains("Dyrnwyn") || p.Contains("AxeHead") ? "Keys and Quest Items" : "Odds and Ends");
                    default:
                        return (family, string.Empty);
                }
            }
            if (t == "Consumable")
            {
                if (it.Health > 0f || it.Stamina > 0f || it.Eitr > 0f)
                {
                    return ("Food", "Ready to Eat");
                }
                return ("Meads and Potions", MeadEffect(p));
            }
            if (IsMeadBase(p))
            {
                return ("Meads and Potions", "Mead Bases");
            }
            if (t == "Fish" || p.Contains("Meat") || CookingIngredients.Contains(p) || p.EndsWith("Uncooked") || p.StartsWith("Spice"))
            {
                return ("Food", "Cooking Ingredients");
            }
            if (it.BiomeHow == "creatures" || it.BiomeHow == "homes" || CreatureParts.Contains(p))
            {
                return ("Creature Parts", it.Biome);
            }
            return ("Other", "Odds and Ends");
        }

        private static readonly HashSet<string> CookingIngredients = new HashSet<string>
        {
            "NeckTail", "SealBlubber", "FishRaw", "FishAnglerRaw", "ChickenEgg", "VoltureEgg", "BarleyFlour", "OatFlour", "BreadDough",
        };

        private static readonly HashSet<string> CreatureParts = new HashSet<string> { "Chitin", "Feathers", "Charredskull" };

        // The mock-up read these from the English names ("Mead Base: …", "Barley Wine Base: …"); the prefab names say the
        // same in every language, and the harness checks they agree on every item of the game.
        private static bool IsMeadBase(string prefab) => prefab.StartsWith("MeadBase") || prefab.Contains("WineBase");

        /// <summary>A mead or potion's effect, from its prefab name (the mock-up used the English name: "Healing",
        /// "Stamina" or "Tasty", "Eitr", "Resistance" or "Barley Wine"; the harness checks the two agree).</summary>
        private static string MeadEffect(string prefab)
        {
            if (IsMeadBase(prefab))
            {
                return "Mead Bases";
            }
            if (prefab.Contains("Health") || prefab.Contains("Heal"))
            {
                return "Healing";
            }
            if (prefab.Contains("Stamina") || prefab.Contains("Tasty"))
            {
                return "Stamina";
            }
            if (prefab.Contains("Eitr"))
            {
                return "Eitr";
            }
            if (prefab.Contains("Resist") || prefab.Contains("BarleyWine"))
            {
                return "Resistance";
            }
            return "Other Brews";
        }

        private static string ToolUse(ItemFacts it)
        {
            string p = it.Prefab ?? string.Empty;
            if (Taxonomy.ToolPrefabs.TryGetValue(p, out string use))
            {
                return use;
            }
            if (p.StartsWith("FishingBait"))
            {
                return "Fishing";
            }
            if (it.Skill == "Pickaxes")
            {
                return "Mining";
            }
            if (it.Type == "Torch")
            {
                return "Lighting";
            }
            return "Other Tools";
        }

        private static string GearSub(ItemFacts it, string section)
        {
            switch (section)
            {
                case "Tools":
                    return ToolUse(it);
                case "Weapons":
                    if (Taxonomy.WeaponType.TryGetValue(it.Skill ?? string.Empty, out string type))
                    {
                        return type;
                    }
                    return it.Type == "OneHandedWeapon" ? "Thrown Weapons" : "Other Weapons";
                case "Ammo":
                    // The mock-up read "arrow", "bolt", "payload" and "missile" from the English names; the game's own
                    // data says the same: the ballista's missiles and the payloads can't be equipped.
                    string p = it.Prefab ?? string.Empty;
                    if (it.Type == "AmmoNonEquipable" || Taxonomy.SiegeAmmo.Contains(p))
                    {
                        return "Siege and Ballista";
                    }
                    if (p.Contains("Arrow"))
                    {
                        return "Arrows";
                    }
                    if (p.Contains("Bolt"))
                    {
                        return "Bolts";
                    }
                    return "Other Ammo";
                default:
                    return string.Empty;
            }
        }

        private static string Preparation(ItemFacts it, string family, string section)
        {
            if (family != "Food")
            {
                return string.Empty;
            }
            if (section == "Cooking Ingredients")
            {
                return "Cooking Ingredients";
            }
            if ((it.Prefab ?? string.Empty).Contains("Feast"))
            {
                return "Prepared Dishes";
            }
            foreach (string station in it.MadeAt)
            {
                if (Taxonomy.PreparedAt.Contains(station))
                {
                    return "Prepared Dishes";
                }
            }
            return "Simple Foods";
        }

        /// <summary>A thing's kind on the Craft tab comes from what it makes: a smelter's iron is a Material, a cooking
        /// station's meat is Food, a fermenter's mead a Mead.</summary>
        private static (string, string) CraftKind(string family, string section, string sub)
        {
            switch (family)
            {
                case "Gear":
                    return (section, sub);
                case "Food":
                case "Meads and Potions":
                    return (family, section);
                case "Other" when section == "Fireworks":
                    return ("Other", "Fireworks");
                case "Other":
                case "Valuables":
                case "Boss Items":
                case "Trophies":
                    return ("Other", family);
                default:
                    return ("Materials", family);
            }
        }
    }
}
