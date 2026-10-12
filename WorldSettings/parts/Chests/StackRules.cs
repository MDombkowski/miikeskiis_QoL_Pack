// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;

namespace QoLMods.Chests
{
    /// <summary>What a quick stack does with the hotbar, the inventory's top row (card A18).</summary>
    internal enum HotbarRule
    {
        /// <summary>The hotbar is left as it is.</summary>
        Off,

        /// <summary>What isn't a favourite goes, as from any other row.</summary>
        UnfavoritedOnly,

        /// <summary>Everything on it goes except what's worn, favourites too.</summary>
        EverythingButWorn,
    }

    /// <summary>Where a quick stack sends trophies.</summary>
    internal enum TrophyRule
    {
        /// <summary>To any chest that holds a trophy of any kind (HexQuickStackStorage's trophy storage).</summary>
        AnyTrophyChest,

        /// <summary>Like any other item: only to a chest that holds that trophy.</summary>
        LikeAnyItem,

        /// <summary>Never: trophies stay with him.</summary>
        Never,

        /// <summary>To a chest holding trophies of creatures from the trophy's own biome (StackRules.TrophyBiome), else to
        /// any chest holding a trophy, as AnyTrophyChest (card A82). A chest holding that very trophy comes first, as
        /// always.</summary>
        SameBiomeTrophyChest,

        /// <summary>Only to a chest holding trophies from the trophy's own biome (or that very trophy); never to another
        /// chest, a trophy chest or not, even with OnlyWhereHeld off: else it stays with him (card A82).</summary>
        SameBiomeTrophyChestOnly,
    }

    /// <summary>
    /// A rule of which chest an item goes to first, when more than one may take it (card A41's second round, decision 28:
    /// StoreStash's "Which chest first"). The caller lists the rules that are on, first to last (StackRules.Order): the
    /// first rule that tells two chests apart decides which comes first, and the chest it puts first gets all it can take
    /// before the next one is touched (StackRouting.Order). Chests no rule tells apart go by rank, then the nearest.
    /// </summary>
    internal enum ChestOrder
    {
        /// <summary>A set-up chest whose setup takes the item, before every chest that isn't one (StoreStash 0.4.0's
        /// "Set-up chests first"). Only with StackRules.Setups.</summary>
        SetUp,

        /// <summary>Among set-up chests taking the item, the more specific setup first (a single item, then a Gear kind, a
        /// member, a family, biomes alone). It tells set-up chests apart only, so it counts only below SetUp.</summary>
        Specific,

        /// <summary>Among set-up chests taking the item, the one holding the most of it first. Only below SetUp, as
        /// Specific.</summary>
        Most,

        /// <summary>His personal chests (StackRules.Personal) before every other chest.</summary>
        Personal,
    }

    /// <summary>
    /// How a quick stack chooses his items and the chests they go to (card A52). The service has no settings of its own:
    /// each module makes these from its own settings, each time it asks, so a changed setting counts at once. The routing
    /// (StackRouting) reads them in one place, so a module that sends items only to some chests passes a Filter, and one
    /// that puts away particular items passes Pick.
    /// </summary>
    internal sealed class StackRules
    {
        /// <summary>Which chests near him may be used, and how far away: the same rules takes use (ChestRules; its LeaveOne
        /// doesn't apply to a put). Its UseReserved lets a put into a reserved chest (Place stacks by hand, when his setting
        /// allows it).</summary>
        internal ChestRules Chests = new ChestRules();

        internal HotbarRule Hotbar = HotbarRule.UnfavoritedOnly;

        internal TrophyRule Trophies = TrophyRule.AnyTrophyChest;

        /// <summary>An item goes only to a chest that already holds one by name (the game's own Place stacks rule).
        /// Off: any chest the rest of the rules allow, chests that hold it first.</summary>
        internal bool OnlyWhereHeld = true;

        /// <summary>The part-stacks of every chest chosen are topped up before any empty cell is used, nearest chest first.
        /// Off: chest by chest, nearest first, each topped up and then its empty cells.</summary>
        internal bool TopUpFirst = true;

        // Set only by the modules that stack (StoreStash, with its personal key; Loadouts later): a plugin that carries the
        // service without stacking never sets them, so the compiler's warning is silenced, as the kit's hints' are.
#pragma warning disable CS0649
        /// <summary>A chest someone else marked as theirs (ChestMine, card A43) takes nothing. Off unless the caller asks, from
        /// its own settings (StoreStash: "Quick stack leaves chests others marked alone", on; never for Place stacks and
        /// holding E, a choice made by hand at that chest), so a module that doesn't know the mark, or StoreStash switched
        /// off, never follows it.</summary>
        internal bool LeaveOthersMarked;

        /// <summary>Items that stay with him (his favourites): never moved, except from the hotbar with
        /// HotbarRule.EverythingButWorn. May be null.</summary>
        internal Func<ItemDrop.ItemData, bool> Keep;

        /// <summary>The caller's own say on which chest may take which item (StoreStash's personal key: only his personal
        /// chests), on top of the rules above. May be null.</summary>
        internal Func<Container, ItemDrop.ItemData, bool> Filter;

        /// <summary>Only these of his items (Loadouts: the gear it puts away); null for every item the rules allow.</summary>
        internal Func<ItemDrop.ItemData, bool> Pick;

        /// <summary>Every row of his inventory counts, not only the game's own: for a caller that means the rows below them
        /// too (Loadouts, putting away gear from EquipmentAndQuickSlots' equipment slots). Off for every quick stack.</summary>
        internal bool AllRows;

        /// <summary>Chests' setups (card A41: StoreStash's "Chest setups"), and how the routing treats set-up chests;
        /// null: setups are ignored, and every chest is routed as before them.</summary>
        internal SetupRules Setups;

        /// <summary>Which chest first (card A41's second round, decision 28): the rules that are on, first to last, each
        /// once. Null: the default, SetUp, Specific, Most (StoreStash 0.4.0's order with its three switches on). SetUp,
        /// Specific and Most count only with Setups, and Specific and Most only below SetUp (StackRouting.Effective);
        /// Personal only with Personal. Every rule is a tier: the chests a rule puts first get all they can take before the
        /// next chest is touched.</summary>
        internal IList<ChestOrder> Order;

        /// <summary>Which chests are his personal ones, for ChestOrder.Personal (StoreStash: the chests its Personal Stacker
        /// counts as his). Null: none. Asked once per chest for each plan, and only while Order has Personal.</summary>
        internal Func<Container, bool> Personal;

        /// <summary>A trophy's biome, by its shared name ("$item_trophy_troll"), for TrophyRule.SameBiomeTrophyChest and
        /// SameBiomeTrophyChestOnly (card A82): the caller's reading, as setups pass theirs, since the service never needs
        /// the catalogue (StoreStash: its biome of origin, Biomes.OfItem, the creature's). Heightmap.Biome.None for one it
        /// can't place, which counts as a biome of its own, so such trophies gather together. Null: every trophy is of that
        /// one unknown biome, so any trophy chest counts as of its biome.</summary>
        internal Func<string, Heightmap.Biome> TrophyBiome;
#pragma warning restore CS0649
    }

    /// <summary>
    /// How a quick stack follows chests' setups (card A41; StackRouting says where each rule acts). A set-up chest takes
    /// what its setup takes, even when it holds none of it yet, and nothing else unless KeepHeld. The caller reads the
    /// setups (the text is the catalogue's, ItemPicks, which the chest service doesn't carry) and passes its reading here,
    /// with the choices its settings make, each time it asks. Which set-up chest comes first is StackRules.Order's.
    /// </summary>
    internal sealed class SetupRules
    {
        // Set only by the modules that stack (StoreStash): a plugin that carries the service without stacking never sets
        // them, so the compiler's warning is silenced, as StackRules' are.
#pragma warning disable CS0649
        /// <summary>A chest's setup, as the caller reads it: null for a chest without one (or one whose setup the caller
        /// leaves aside: a cart's, when carts' setups are off), else how strongly it takes an item, 0 for not at all
        /// (higher: more specific; ItemPicks.Fit). Asked once per chest for each plan.</summary>
        internal Func<Container, Func<ItemDrop.ItemData, int>> Of;

        /// <summary>A set-up chest also takes what it already holds, like a chest without a setup (with ChestOrder.SetUp,
        /// after the set-up chests that take it). Off: strict, only what its setup takes.</summary>
        internal bool KeepHeld;

        /// <summary>What the set-up chests taking an item can't fit goes on to the other chests that take it. Off: it stays
        /// with him.</summary>
        internal bool Overflow = true;

        /// <summary>What no set-up chest nearby takes goes to chests without a setup, by the rules above them. Off: only
        /// set-up chests are stacked into.</summary>
        internal bool Others = true;
#pragma warning restore CS0649
    }
}
