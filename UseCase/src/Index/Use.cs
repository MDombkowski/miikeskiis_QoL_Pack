using QoLMods.Catalogue;

namespace UseCase
{
    /// <summary>
    /// The ways the game uses an item, each a section of the panel and a switch of its own. The first four are his
    /// (Building, Crafting, Upgrading, Refueling); the rest are the game's other uses the research found (A45).
    /// Their order here is the default order of the sections.
    /// </summary>
    internal enum UseKind
    {
        Building,
        Crafting,
        Upgrading,
        Refueling,
        Processing,
        Summoning,
        LockedDoors,
        Taming,
        FishingBait,
        Ammo,
        Fireworks,
        Buying,
        Selling,
        Traders,
        GuardianStones,
        BallistaTargets,
    }

    /// <summary>How the panel knows whether the player has met the thing a line names (the spoiler rule).</summary>
    internal enum KnownBy
    {
        /// <summary>Nothing to know: always shown.</summary>
        Always,

        /// <summary>A recipe: known when the game's known list holds the $ name of what it makes.</summary>
        Recipe,

        /// <summary>A piece: known when the game's known list holds the piece's $ name.</summary>
        Piece,

        /// <summary>An altar, a locked door: known once it has loaded near him (the mod's own seen list), or once its
        /// boss is beaten, by a setting.</summary>
        Seen,

        /// <summary>A creature or a fish (taming, bait): known once he has held the item named (the game's known
        /// materials), when the setting hides them at all.</summary>
        Material,

        /// <summary>A trader's wares: known once the world has the global key the trader waits for.</summary>
        GlobalKey,
    }

    /// <summary>
    /// One use of one resource: a line of the panel. Made once per world by UseBuilder from the plain facts GameReader
    /// reads, and never changed after; the panel's words are made from it, per the settings, by Lines. Plain data, so
    /// the index can be tested without the game and could move into the catalogue part later (the card's decision 9).
    /// </summary>
    internal sealed class Use
    {
        internal UseKind Kind;

        /// <summary>The resource used, by the $ name the game stacks and counts it by ("$item_resin").</summary>
        internal string Resource = string.Empty;

        /// <summary>What uses it, by the $ token its name is shown from ("$piece_walltorch", "$enemy_boar").</summary>
        internal string Token = string.Empty;

        /// <summary>What uses it, by prefab name ("piece_walltorch"), for the ignore lists; empty when it has none.</summary>
        internal string Prefab = string.Empty;

        /// <summary>A third name the ignore lists accept (a recipe's own name, "Recipe_Torch"); empty when none.</summary>
        internal string OtherName = string.Empty;

        /// <summary>Building: the $ name of the tool whose table holds the piece ("$item_hammer").</summary>
        internal string Tool = string.Empty;

        /// <summary>Building: the tool's place among the tools (the hammer's table first, as the game's list has them).</summary>
        internal int ToolOrder;

        /// <summary>The "· X": for one build or craft, the most a piece holds, a summon's count; -1 for none.</summary>
        internal int Amount = -1;

        /// <summary>Upgrading: the amount at each quality from 2 to the item's highest, the game's own GetAmount.</summary>
        internal int[] Levels;

        /// <summary>Upgrading: an idol, which only a station that upgrades past the recipe's levels takes (the Forge of
        /// Potential), at each upgrade on top of its per-level amount; its number is the first upgrade's.</summary>
        internal bool Upgrader;

        /// <summary>Processing: how many one product takes; -1 when it is the same as Amount.</summary>
        internal int PerProduct = -1;

        /// <summary>Crafting and processing: how many one craft or batch makes (shown as "makes 20" by a setting).</summary>
        internal int Makes = 1;

        /// <summary>Crafting and upgrading: the station it needs, as its label ("Forge"); empty for none or by hand.</summary>
        internal string Station = string.Empty;

        /// <summary>A few more words the line always carries ("saddle", "Haldor"), or empty.</summary>
        internal string Note = string.Empty;

        /// <summary>The season a seasonal recipe or piece belongs to ("Halloween"), or empty for a year-round one.</summary>
        internal string Season = string.Empty;

        /// <summary>False for a seasonal recipe or piece while its season isn't on.</summary>
        internal bool InSeason = true;

        /// <summary>The name of the mod that added the thing, when it is known; empty for the game's own.</summary>
        internal string Mod = string.Empty;

        /// <summary>A use of a thing by itself (the serving tray's food costing that food): left out by a setting.</summary>
        internal bool SelfUse;

        /// <summary>A piece whose fuel another mod makes needless (TimedTorchesStayLit keeps it lit): left out of
        /// Refueling by a setting.</summary>
        internal bool KeptLit;

        internal KnownBy KnownBy;

        /// <summary>What KnownBy looks up: a $ name, a seen key or a global key.</summary>
        internal string KnownKey = string.Empty;

        /// <summary>Summoning and locked doors: the global key the boss's defeat sets, for "unknown until the boss is
        /// beaten"; empty when none.</summary>
        internal string BeatenKey = string.Empty;

        /// <summary>The catalogue's listing of the thing, so every order inside a section is the pack's (rule 7).</summary>
        internal Listing Sort;

        /// <summary>Its place in the game's own menus (a piece's in its table, a recipe's in the crafting list).</summary>
        internal int GameOrder;

        /// <summary>Two uses that would read the same (the same kind, thing, number and words) are one line.</summary>
        internal string SameAs() =>
            $"{(int)Kind}|{Token}|{Tool}|{Amount}|{(Levels == null ? string.Empty : string.Join(",", Levels))}|{PerProduct}|{Makes}|{Station}|{Note}";
    }
}
