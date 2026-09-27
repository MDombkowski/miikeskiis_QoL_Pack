// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;

namespace QoLMods.Catalogue
{
    /// <summary>
    /// The agreed classification, as data (card A11; the plan and the browsing philosophy in the vault's working\
    /// folder, agreed 2026-09-27). Every family, member, kind, slot, station and order here is ported from the mock-up's
    /// data script (working\2026-09-26_add-to-cart-browsing-mockup-source\build_data.py), which is the agreed rules in
    /// runnable form; a change to a rule is a change to one line here. Things are named by the game's own prefab names,
    /// item types and skills, never by their English names, so the rules hold in every language the game speaks.
    /// Labels follow one style: Title Case with "and" spelled out, as the game writes its own category labels.
    /// </summary>
    internal static class Taxonomy
    {
        /// <summary>The biomes in the order a player meets them (Biomes.cs says why the Ocean comes third).</summary>
        internal static readonly string[] Biomes =
        {
            "Meadows", "Black Forest", "Ocean", "Swamp", "Mountain", "Plains", "Mistlands", "Ashlands", "Deep North",
        };

        /// <summary>The heading for anything the biome derivation couldn't place.</summary>
        internal const string OtherBiome = "Other";

        // ---------------------------------------------------------------- Take: the families, in his order (2026-09-26)

        internal static readonly string[] Families =
        {
            "Gear", "Wood", "Stone and Minerals", "Metals", "Hides and Fabrics", "Creature Parts", "Trophies",
            "Fuels and Resins", "Nature", "Food", "Meads and Potions", "Magic and Rare", "Boss Items", "Valuables", "Other",
        };

        internal static readonly string[] GearOrder = { "Tools", "Weapons", "Armor", "Shields", "Ammo", "Trinkets and Utility" };

        /// <summary>Each family's members, in his order: the headings a family opens to, and its rows in the left list.</summary>
        internal static readonly Dictionary<string, string[]> Members = new Dictionary<string, string[]>
        {
            { "Gear", GearOrder },
            { "Metals", new[] { "Copper", "Tin", "Bronze", "Iron", "Silver", "Black Metal", "Flametal", "Bloodgold", "Nails and Fittings", "Moulds and Casts" } },
            { "Hides and Fabrics", new[] { "Hides and Pelts", "Threads and Fabrics" } },
            { "Nature", new[] { "Gathering", "Farming", "Seeds" } },
            { "Food", new[] { "Ready to Eat", "Cooking Ingredients" } },
            { "Meads and Potions", new[] { "Healing", "Stamina", "Eitr", "Resistance", "Other Brews", "Mead Bases", "Brewing Ingredients" } },
            { "Magic and Rare", new[] { "Cores", "Eitr and Spirits", "Gemstones", "Idols", "Trader Goods" } },
            { "Boss Items", new[] { "Summoning", "Boss Loot" } },
            { "Other", new[] { "Keys and Quest Items", "Fireworks", "Odds and Ends" } },
        };

        /// <summary>The items placed by hand, by family, each list in the order its items show (the order within a heading
        /// after the biome). His calls and the plan's "Homes for the items he couldn't place".</summary>
        internal static readonly Dictionary<string, string[]> Explicit = new Dictionary<string, string[]>
        {
            { "Wood", new[] { "Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood", "Frostwood", "BarkaBranch" } },
            { "Stone and Minerals", new[] { "Stone", "StoneRock", "Flint", "Obsidian", "Crystal", "SharpeningStone", "BlackMarble", "CeramicPlate", "Grausten", "SulfurStone", "ProustitePowder", "Pot_Shard_Green", "Ice" } },
            { "Hides and Fabrics", new[] { "LeatherScraps", "DeerHide", "TrollHide", "BjornHide", "WolfPelt", "LoxPelt", "ScaleHide", "AskHide", "MooseHide", "SealHide", "Leatherstraps", "JuteRed", "LinenThread", "JuteBlue", "NornThread" } },
            { "Fuels and Resins", new[] { "Resin", "Coal", "Guck", "Tar", "Sap", "CharcoalResin", "MemorialCoal", "FrozenFuel" } },
            { "Nature", new[] { "Dandelion", "Thistle", "QueenBee", "Turnip", "Barley", "Flax", "Acorn", "BeechSeeds", "BirchSeeds", "FirCone", "PineCone", "CarrotSeeds", "TurnipSeeds", "VineGreenSeeds", "OnionSeeds", "VineberrySeeds", "KaleSeeds", "OatSeeds", "PoteitrSeeds", "FirConeFrost" } },
            { "Magic and Rare", new[] { "SurtlingCore", "BlackCore", "ShieldCore", "MoltenCore", "FrostCore", "Eitr", "Wisp", "FaderEmber", "OrbFrostFire", "OrbThunderBlood", "GemstoneRed", "GemstoneBlue", "GemstoneGreen", "Thunderstone", "YmirRemains" } },
            { "Boss Items", new[] { "AncientSeed", "WitheredBone", "DragonEgg", "GoblinTotem", "DvergrKeyFragment", "DvergrKey", "BellFragment", "Bell", "HardAntler", "CryptKey", "DragonTear", "YagluthDrop", "QueenDrop", "FaderDrop", "FrozenKingDrop", "CrownJewel" } },
            { "Metals", new[] { "CopperOre", "CopperScrap", "Copper", "TinOre", "Tin", "BronzeScrap", "Bronze", "IronScrap", "IronOre", "Iron", "SilverOre", "Silver", "BlackMetalScrap", "BlackMetal", "FlametalOreNew", "FlametalNew", "GoldOre", "Gold", "BronzeNails", "IronNails", "Chain", "BarrelRings", "Hook", "MechanicalSpring", "CharredCogwheel", "DvergrNeedle", "Ironpit" } },
            { "Meads and Potions", new[] { "CuredSquirrelHamstring", "FragrantBundle", "FreshSeaweed", "PowderedDragonEgg", "PungentPebbles", "BlobVial" } },
            { "Other", new[] { "BarberKit", "ScytheHandle", "CandleWick", "HatefulBlood", "AxeHead1", "AxeHead2", "DyrnwynBladeFragment", "DyrnwynHiltFragment", "DyrnwynTipFragment", "HildirKey_forestcrypt", "HildirKey_mountaincave", "HildirKey_plainsfortress", "chest_hildir1", "chest_hildir2", "chest_hildir3", "BloodGoldKey" } },
        };

        /// <summary>Which metal an ore, scrap or bar belongs to; the rest of Metals are Nails and Fittings.</summary>
        internal static readonly Dictionary<string, string> MetalOf = new Dictionary<string, string>
        {
            { "CopperOre", "Copper" }, { "CopperScrap", "Copper" }, { "Copper", "Copper" }, { "TinOre", "Tin" }, { "Tin", "Tin" },
            { "BronzeScrap", "Bronze" }, { "Bronze", "Bronze" }, { "IronScrap", "Iron" }, { "IronOre", "Iron" }, { "Iron", "Iron" },
            { "SilverOre", "Silver" }, { "Silver", "Silver" }, { "BlackMetalScrap", "Black Metal" }, { "BlackMetal", "Black Metal" },
            { "FlametalOreNew", "Flametal" }, { "FlametalNew", "Flametal" }, { "GoldOre", "Bloodgold" }, { "Gold", "Bloodgold" },
        };

        internal static readonly Dictionary<string, string> MagicSection = new Dictionary<string, string>
        {
            { "SurtlingCore", "Cores" }, { "BlackCore", "Cores" }, { "ShieldCore", "Cores" }, { "MoltenCore", "Cores" },
            { "FrostCore", "Cores" }, { "Eitr", "Eitr and Spirits" }, { "Wisp", "Eitr and Spirits" },
            { "FaderEmber", "Eitr and Spirits" }, { "OrbFrostFire", "Eitr and Spirits" }, { "OrbThunderBlood", "Eitr and Spirits" },
            { "GemstoneRed", "Gemstones" }, { "GemstoneBlue", "Gemstones" }, { "GemstoneGreen", "Gemstones" },
            { "Thunderstone", "Trader Goods" }, { "YmirRemains", "Trader Goods" },
        };

        internal static readonly HashSet<string> BossSummon = new HashSet<string>
        {
            "AncientSeed", "WitheredBone", "DragonEgg", "GoblinTotem", "DvergrKeyFragment", "DvergrKey", "BellFragment", "Bell",
        };

        internal static readonly Dictionary<string, string> NatureSection = new Dictionary<string, string>
        {
            { "Dandelion", "Gathering" }, { "Thistle", "Gathering" }, { "QueenBee", "Farming" }, { "Turnip", "Farming" },
            { "Barley", "Farming" }, { "Flax", "Farming" },
        };

        internal static readonly HashSet<string> Threads = new HashSet<string> { "JuteRed", "LinenThread", "JuteBlue", "NornThread" };

        /// <summary>Gear's member, by the game's item type (ItemDrop.ItemData.ItemType, by name).</summary>
        internal static readonly Dictionary<string, string> GearKind = new Dictionary<string, string>
        {
            { "OneHandedWeapon", "Weapons" }, { "TwoHandedWeapon", "Weapons" }, { "TwoHandedWeaponLeft", "Weapons" },
            { "Bow", "Weapons" }, { "Shield", "Shields" }, { "Helmet", "Armor" }, { "Chest", "Armor" }, { "Legs", "Armor" },
            { "Shoulder", "Armor" }, { "Hands", "Armor" }, { "Ammo", "Ammo" }, { "AmmoNonEquipable", "Ammo" }, { "Tool", "Tools" },
            { "Torch", "Tools" }, { "Trinket", "Trinkets and Utility" }, { "Utility", "Trinkets and Utility" },
        };

        /// <summary>Things the game types as weapons that players use as tools (the wiki's tools list): his fishing-rod call,
        /// 2026-09-26, and its kin. Each with its use.</summary>
        internal static readonly Dictionary<string, string> ToolPrefabs = new Dictionary<string, string>
        {
            { "FishingRod", "Fishing" }, { "GrapplingHook", "Travel" }, { "KnifeButcher", "Farming" }, { "Scythe", "Farming" },
            { "Shovel", "Farming" }, { "Hammer", "Building" }, { "Hoe", "Building" }, { "Cultivator", "Farming" },
            { "Feaster", "Serving" }, { "SaddleLox", "Travel" }, { "SaddleAsksvin", "Travel" }, { "SaddleMoose", "Travel" },
        };

        /// <summary>The siege and ballista ammunition the game types as materials.</summary>
        internal static readonly HashSet<string> SiegeAmmo = new HashSet<string> { "Catapult_ammo", "BombSiege", "Catapult_Ammo_BloodGold" };

        internal static readonly string[] ToolUseOrder = { "Building", "Farming", "Mining", "Fishing", "Lighting", "Travel", "Serving", "Other Tools" };

        /// <summary>The equipment slot, by item type: his slots for armour, completed for the rest of the gear.</summary>
        internal static readonly Dictionary<string, string> Slot = new Dictionary<string, string>
        {
            { "Helmet", "Head" }, { "Shoulder", "Shoulders" }, { "Chest", "Chest" }, { "Legs", "Legs" }, { "Hands", "Hands" },
            { "Utility", "Utility" }, { "Trinket", "Trinket" }, { "OneHandedWeapon", "One Hand" }, { "Torch", "One Hand" },
            { "Tool", "One Hand" }, { "TwoHandedWeapon", "Both Hands" }, { "TwoHandedWeaponLeft", "Both Hands" }, { "Bow", "Both Hands" },
            { "Shield", "Off Hand" }, { "Ammo", "Ammo" }, { "AmmoNonEquipable", "Ammo" },
        };

        internal static readonly string[] SlotOrder = { "Head", "Shoulders", "Chest", "Legs", "Hands", "Utility", "Trinket", "One Hand", "Both Hands", "Off Hand", "Ammo" };

        /// <summary>Food made at these stations (and feasts) is a Prepared Dish; the rest ready to eat is a Simple Food (his
        /// "cauldron versus picked up or cooked on a spit").</summary>
        internal static readonly HashSet<string> PreparedAt = new HashSet<string> { "Cauldron", "Stone Oven", "Mead Ketill", "Food Preparation Table" };

        internal static readonly string[] PreparationOrder = { "Simple Foods", "Prepared Dishes", "Cooking Ingredients" };

        /// <summary>A weapon's type, by its skill (Skills.SkillType, by name).</summary>
        internal static readonly Dictionary<string, string> WeaponType = new Dictionary<string, string>
        {
            { "Swords", "Swords" }, { "Axes", "Axes" }, { "Clubs", "Clubs" }, { "Spears", "Spears" }, { "Polearms", "Polearms" },
            { "Knives", "Knives" }, { "Unarmed", "Fists" }, { "Bows", "Bows" }, { "Crossbows", "Crossbows" },
            { "ElementalMagic", "Elemental Magic" }, { "BloodMagic", "Blood Magic" },
        };

        internal static readonly string[] WeaponOrder =
        {
            "Swords", "Axes", "Clubs", "Spears", "Polearms", "Knives", "Fists", "Bows", "Crossbows", "Elemental Magic", "Blood Magic",
            "Thrown Weapons", "Other Weapons",
        };

        internal static readonly string[] AmmoOrder = { "Arrows", "Bolts", "Siege and Ballista", "Other Ammo" };

        // ---------------------------------------------------------------- Build: pieces

        /// <summary>The wood, stone and metal a piece can be made of (the game's prefab names), each with its label and
        /// family, in unlock order within each family: what a piece's "made of" and the Materials list are built from.</summary>
        internal static readonly (string Prefab, string Label, string Family)[] StructuralMaterials =
        {
            ("Wood", "Wood", "Wood"), ("FineWood", "Finewood", "Wood"), ("RoundLog", "Corewood", "Wood"),
            ("ElderBark", "Ancient Bark", "Wood"), ("YggdrasilWood", "Yggdrasil Wood", "Wood"), ("Blackwood", "Ashwood", "Wood"),
            ("Frostwood", "Timberwood", "Wood"),
            ("Stone", "Stone", "Stone"), ("BlackMarble", "Black Marble", "Stone"), ("Grausten", "Grausten", "Stone"),
            ("Copper", "Copper", "Metal"), ("Tin", "Tin", "Metal"), ("Bronze", "Bronze", "Metal"), ("Iron", "Iron", "Metal"),
            ("Silver", "Silver", "Metal"), ("BlackMetal", "Black Metal", "Metal"), ("FlametalNew", "Flametal", "Metal"),
            ("Gold", "Bloodgold", "Metal"),
        };

        /// <summary>The three families of structural materials: a piece costing one of them is "made of" that family.</summary>
        internal static readonly string[] StructuralFamilies = { "Wood", "Stone", "Metal" };

        internal static readonly string[] MaterialFamilies = { "Wood", "Stone", "Metal", "Hides and Fabrics", "Other" };

        /// <summary>The heading for pieces costing no wood, stone or metal, grouped by material.</summary>
        internal const string OtherMaterials = "Other Materials";

        /// <summary>The game's build-menu category flags (Piece.UsageTagFlags, by name) and their labels: the game's own,
        /// except "Building", which is the parent "Building Structures" (his answer, 2026-09-27).</summary>
        internal static readonly Dictionary<string, string> CategoryOf = new Dictionary<string, string>
        {
            { "Misc", "Misc." }, { "Crafting", "Crafting" }, { "Building", BuildingParent }, { "Floor", "Flooring" }, { "Wall", "Walls" },
            { "Roof", "Roofing" }, { "Architecture", "Architecture" }, { "Furniture", "Furniture" }, { "Lighting", "Lighting" },
            { "Decor", "Decor" }, { "Storage", "Storage" }, { "Transport", "Transportation" }, { "Food", "Food" }, { "Meads", "Mead" },
            { "Feasts", "Feasts" }, { "Defense", "Defence" }, { "Stacks", "Piles and Stacks" }, { "Stairs", "Stairs" },
            { "Doors", "Doors and Windows" }, { "Seasonal", "Seasonal Items" },
        };

        internal const string BuildingParent = "Building Structures";

        /// <summary>The heading for the one piece whose most specific category is the Building tag itself.</summary>
        internal const string OtherStructures = "Other Structures";

        /// <summary>The categories in the order he liked: building parts first, Misc. last.</summary>
        internal static readonly string[] CategoryOrder =
        {
            BuildingParent, "Flooring", "Walls", "Roofing", "Stairs", "Doors and Windows", "Architecture", "Furniture", "Storage",
            "Lighting", "Decor", "Crafting", "Transportation", "Defence", "Piles and Stacks", "Seasonal Items", "Food", "Mead",
            "Feasts", "Misc.",
        };

        /// <summary>The six structural categories, the members of Building Structures.</summary>
        internal static readonly string[] BuildingSubs = { "Flooring", "Walls", "Roofing", "Stairs", "Doors and Windows", "Architecture" };

        /// <summary>A piece's most specific category: the first of these it carries.</summary>
        internal static readonly string[] PrimaryPriority =
        {
            "Seasonal Items", "Piles and Stacks", "Doors and Windows", "Stairs", "Roofing", "Flooring", "Walls", "Architecture",
            "Storage", "Lighting", "Furniture", "Decor", "Crafting", "Transportation", "Defence", BuildingParent, "Misc.",
        };

        // ---------------------------------------------------------------- Craft: recipes and what the stations make

        internal static readonly string[] KindOrder =
        {
            "Materials", "Tools", "Weapons", "Armor", "Shields", "Ammo", "Trinkets and Utility", "Food", "Meads and Potions", "Other",
        };

        internal const string ByHand = "By Hand";

        /// <summary>Every station, by its prefab name, with its label, in the order a player can first build it (his call:
        /// "in order of unlock by the player"): by biome, and within a biome after the station whose output builds it (the
        /// smelter's copper builds the forge, the forge's bronze builds the fermenter).</summary>
        internal static readonly (string Prefab, string Label)[] Stations =
        {
            ("piece_workbench", "Workbench"), ("piece_cookingstation", "Cooking Station"), ("charcoal_kiln", "Charcoal Kiln"),
            ("smelter", "Smelter"), ("forge", "Forge"), ("piece_cauldron", "Cauldron"), ("fermenter", "Fermenter"),
            ("piece_MeadCauldron", "Mead Ketill"), ("piece_stonecutter", "Stonecutter"), ("piece_cookingstation_iron", "Iron Cooking Station"),
            ("piece_preptable", "Food Preparation Table"), ("piece_artisanstation", "Artisan Table"), ("windmill", "Windmill"),
            ("piece_spinningwheel", "Spinning Wheel"), ("piece_oven", "Stone Oven"), ("blastfurnace", "Blast Furnace"),
            ("blackforge", "Black Forge"), ("piece_magetable", "Galdr Table"), ("eitrrefinery", "Eitr Refinery"),
            ("piece_FrostFoundry", "Frost Foundry"), ("piece_FrostKiln", "Frigid Kiln"),
        };

        /// <summary>The Obliterator turns any trophy or arrow into coal: not a way anyone makes coal, so it is left out (his
        /// call, 2026-09-26).</summary>
        internal const string Obliterator = "incinerator";

        // ---------------------------------------------------------------- Lookups built once from the tables above

        internal static readonly Dictionary<string, string> ExplicitFamily = new Dictionary<string, string>();
        internal static readonly Dictionary<string, int> ExplicitOrder = new Dictionary<string, int>();
        internal static readonly Dictionary<string, (string Label, string Family)> StructuralByPrefab = new Dictionary<string, (string, string)>();
        internal static readonly Dictionary<string, string> StationLabel = new Dictionary<string, string>();

        static Taxonomy()
        {
            foreach (KeyValuePair<string, string[]> family in Explicit)
            {
                for (int i = 0; i < family.Value.Length; i++)
                {
                    ExplicitFamily[family.Value[i]] = family.Key;
                    ExplicitOrder[family.Value[i]] = i;
                }
            }
            foreach ((string prefab, string label, string family) in StructuralMaterials)
            {
                StructuralByPrefab[prefab] = (label, family);
            }
            foreach ((string prefab, string label) in Stations)
            {
                StationLabel[prefab] = label;
            }
        }

        /// <summary>A biome's place in progression; Other after them all.</summary>
        internal static int BiomeRank(string biome)
        {
            int rank = System.Array.IndexOf(Biomes, biome);
            return rank < 0 ? Biomes.Length : rank;
        }
    }
}
