using System.Collections.Generic;
using QoLMods.Catalogue;

namespace UseCase
{
    // The plain facts GameReader reads from the live game, and UseBuilder turns into uses. Nothing here touches the game,
    // so the rules of what counts as a use (the research's traps) are tested without it. Every item is named by its $
    // name, as the game stacks and counts items.

    /// <summary>What one recipe or piece costs of one item (the game's Piece.Requirement).</summary>
    internal sealed class Cost
    {
        internal string Item = string.Empty;

        /// <summary>For one craft or build (m_amount).</summary>
        internal int Amount;

        /// <summary>For each upgrade level (m_amountPerLevel).</summary>
        internal int PerLevel;

        /// <summary>The amount at each quality from 2 up to the made item's highest, from the game's own
        /// Piece.Requirement.GetAmount; empty when the item can't be upgraded.</summary>
        internal int[] Levels = new int[0];

        /// <summary>An idol the Forge of Potential takes at each upgrade (m_upgraderResource).</summary>
        internal bool Upgrader;
    }

    /// <summary>One entry of ObjectDB.m_recipes.</summary>
    internal sealed class RecipeFact
    {
        /// <summary>The recipe's own name ("Recipe_Torch").</summary>
        internal string Name = string.Empty;

        /// <summary>What it makes, by $ name, and that item's prefab.</summary>
        internal string Item = string.Empty;

        internal string ItemPrefab = string.Empty;

        /// <summary>How many one craft makes.</summary>
        internal int Makes = 1;

        /// <summary>m_enabled, or on in the season now on.</summary>
        internal bool Enabled;

        /// <summary>The season it belongs to when it is seasonal, else empty; InSeason says whether that season is on.</summary>
        internal string Season = string.Empty;

        internal bool InSeason = true;

        /// <summary>m_noCraftOnlyUpgrade: never crafted new, only upgraded.</summary>
        internal bool UpgradeOnly;

        /// <summary>The made item's highest quality (m_maxQuality): above 1 means it can be upgraded.</summary>
        internal int MaxQuality = 1;

        internal string Station = string.Empty;

        /// <summary>The label of the station that upgrades with idols (a CraftingStation with m_upgrader, the Forge of
        /// Potential), for its costs' lines; empty when none was found.</summary>
        internal string UpgraderStation = string.Empty;

        /// <summary>m_requireOnlyOneIngredient: any one of the costs makes it.</summary>
        internal bool OnlyOne;

        internal readonly List<Cost> Costs = new List<Cost>();

        internal string Mod = string.Empty;

        internal int GameOrder;

        internal Listing Sort;
    }

    /// <summary>What a piece takes as fuel, and the most it holds.</summary>
    internal sealed class FuelFact
    {
        internal string Item = string.Empty;

        internal int Holds;
    }

    /// <summary>What a station turns an item into, and how many the station holds or one product takes.</summary>
    internal sealed class ConversionFact
    {
        internal string From = string.Empty;

        internal string To = string.Empty;

        /// <summary>How many the station holds at once (a smelter's 10, a cooking station's slots).</summary>
        internal int Holds;

        /// <summary>How many one product takes (1, or an Obliterator conversion's amount).</summary>
        internal int PerProduct = 1;

        /// <summary>How many one batch makes (a fermenter's 6 meads).</summary>
        internal int Makes = 1;
    }

    /// <summary>One piece in a tool's table, and what it takes once built.</summary>
    internal sealed class PieceFact
    {
        internal string Prefab = string.Empty;

        /// <summary>The piece's $ name ("$piece_walltorch").</summary>
        internal string Token = string.Empty;

        /// <summary>The tool whose table holds it, by $ name, and that table's place among the tools.</summary>
        internal string Tool = string.Empty;

        internal int ToolOrder;

        /// <summary>Its place in its tool's table.</summary>
        internal int GameOrder;

        internal bool Enabled;

        internal string Season = string.Empty;

        internal bool InSeason = true;

        /// <summary>The station it needs nearby to be built, as its label; empty for none.</summary>
        internal string Station = string.Empty;

        internal readonly List<Cost> Costs = new List<Cost>();

        internal readonly List<FuelFact> Fuel = new List<FuelFact>();

        internal readonly List<ConversionFact> Conversions = new List<ConversionFact>();

        /// <summary>The items it launches as fireworks (a campfire's).</summary>
        internal readonly List<string> Fireworks = new List<string>();

        /// <summary>The ammo it takes, and the most it holds (a ballista's missiles, a catapult's payloads).</summary>
        internal readonly List<FuelFact> Ammo = new List<FuelFact>();

        /// <summary>The trophies that set its target (the ballista's).</summary>
        internal readonly List<string> Targets = new List<string>();

        /// <summary>TimedTorchesStayLit keeps it lit, so its fuel is never needed on this computer.</summary>
        internal bool KeptLit;

        internal string Mod = string.Empty;

        internal Listing Sort;
    }

    /// <summary>A creature that can be tamed: what it eats, and its saddle.</summary>
    internal sealed class CreatureFact
    {
        internal string Prefab = string.Empty;

        internal string Token = string.Empty;

        internal readonly List<string> Eats = new List<string>();

        internal string Saddle = string.Empty;

        /// <summary>An item he has held once he has met it (its trophy, or else what it drops), for the spoiler rule.</summary>
        internal string KnownItem = string.Empty;

        internal string Mod = string.Empty;

        internal Listing Sort;
    }

    /// <summary>A fish, and the bait it bites on.</summary>
    internal sealed class FishFact
    {
        internal string Prefab = string.Empty;

        /// <summary>The fish item's $ name.</summary>
        internal string Token = string.Empty;

        internal readonly List<string> Baits = new List<string>();

        internal string Mod = string.Empty;

        internal Listing Sort;
    }

    /// <summary>What a trader sells for coins, and what she takes back (Hildir's chests).</summary>
    internal sealed class TraderFact
    {
        internal string Prefab = string.Empty;

        internal string Token = string.Empty;

        internal readonly List<Ware> Sells = new List<Ware>();

        internal readonly List<TakeBack> TakesBack = new List<TakeBack>();
    }

    /// <summary>An item a trader takes back (Hildir's chests), and how many of her wares it unlocks.</summary>
    internal sealed class TakeBack
    {
        internal string Item = string.Empty;

        internal int Unlocks;
    }

    /// <summary>One of a trader's wares.</summary>
    internal sealed class Ware
    {
        internal string Item = string.Empty;

        internal string ItemPrefab = string.Empty;

        internal int Stack = 1;

        internal int Price;

        /// <summary>The global key the trader waits for before selling it; empty when always sold.</summary>
        internal string RequiredKey = string.Empty;

        internal Listing Sort;
    }

    /// <summary>A place in the world that takes an item: a boss altar, a locked door, a guardian stone. From the fixed
    /// list for the game's own (GameReader.Fixed), and learned as they load near him (others').</summary>
    internal sealed class PlaceFact
    {
        internal UseKind Kind;

        /// <summary>What the line names (a boss's $enemy_ token; a place's token or English name).</summary>
        internal string Token = string.Empty;

        /// <summary>The location or piece prefab, for the ignore lists.</summary>
        internal string Prefab = string.Empty;

        internal string Item = string.Empty;

        /// <summary>How many it takes; -1 for a key, which it only checks for.</summary>
        internal int Count = -1;

        /// <summary>Words the line always carries ("used up" for a key the door keeps).</summary>
        internal string Note = string.Empty;

        /// <summary>The key of the mod's own seen list, set once it has loaded near him.</summary>
        internal string SeenKey = string.Empty;

        /// <summary>The boss's defeat key, for "unknown until the boss is beaten"; empty for none.</summary>
        internal string BeatenKey = string.Empty;

        /// <summary>Guardian stones: always known (the world starts there).</summary>
        internal bool AlwaysKnown;

        internal Listing Sort;
    }

    /// <summary>
    /// The two rules of the game that take almost anything (research section 1), kept apart from the index and listed only
    /// by a setting: the Obliterator turns any item it has no conversion for into its default result, so many to one; the
    /// catapult loads any item whose model has an "attach" point (the one item stands use) unless its type or name is
    /// excluded (Catapult.CanItemBeLoaded).
    /// </summary>
    internal sealed class AnythingFacts
    {
        internal PieceFact Obliterator;

        /// <summary>How many of any item make one of its default result (Incinerator.m_defaultCost).</summary>
        internal int ObliteratorCost;

        /// <summary>Its default result's $ name (coal), which it doesn't turn into itself.</summary>
        internal string ObliteratorResult = string.Empty;

        /// <summary>The items its own conversions take, which the default rule doesn't reach.</summary>
        internal readonly HashSet<string> ObliteratorListed = new HashSet<string>();

        internal PieceFact Catapult;

        /// <summary>How many it loads at once (m_maxLoadStack).</summary>
        internal int CatapultHolds = 1;

        /// <summary>The items its "anything" rule lets it load, its own payloads left out (those are Ammo).</summary>
        internal readonly HashSet<string> CatapultTakes = new HashSet<string>();
    }

    /// <summary>Everything GameReader read from one world, for UseBuilder.</summary>
    internal sealed class GameFactsRead
    {
        internal readonly AnythingFacts Anything = new AnythingFacts();

        internal readonly List<RecipeFact> Recipes = new List<RecipeFact>();

        internal readonly List<PieceFact> Pieces = new List<PieceFact>();

        internal readonly List<CreatureFact> Creatures = new List<CreatureFact>();

        internal readonly List<FishFact> Fish = new List<FishFact>();

        internal readonly List<TraderFact> Traders = new List<TraderFact>();

        internal readonly List<PlaceFact> Places = new List<PlaceFact>();

        /// <summary>The $ name of the game's coins, which the traders take.</summary>
        internal string Coins = "$item_coins";
    }
}
