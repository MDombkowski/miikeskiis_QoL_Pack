namespace Outfitter.Equip
{
    /// <summary>
    /// The settings one equip follows, read once as it begins, so the plan made before the claim and the plan made again
    /// once the chests are his follow the same rules even if a setting changes in between.
    /// </summary>
    internal sealed class EquipRules
    {
        internal EquipMode Mode;
        internal GearQuality Quality;
        internal bool MatchStyle;
        internal bool ExactItem;
        internal PreferCopy Prefer;
        internal bool AmmoIsGear;
        internal bool ToolsAreGear;
        internal bool TorchesAreGear;
        internal TopUp TopUp;
        internal MoreThanCount Extra;
        internal bool LeaveFavourites;

        /// <summary>"My favourite gear in an equip": Swaps (job 30, Session 3): his favourite gear goes like any other gear,
        /// while "Leave my favourites alone" keeps the rest of his favourites.</summary>
        internal bool FavouriteGearSwaps;

        /// <summary>"Equip modes put away my favourites" (0.5.0): a loadout's own switch when it has none set
        /// (Loadout.FavouritesGo); on, favourites go like any other item, whatever the two settings above say.</summary>
        internal bool FavouritesGo;

        /// <summary>"Food slots by stat: at least" (0.5.0): how many of its preferred food a slot putting its stat first
        /// needs nearby (FoodPick.Assign).</summary>
        internal int FoodStatAtLeast = 1;
        internal bool KeepWornWhenMissing;

        /// <summary>"What I wear that the loadout doesn't": ComesOff (card B18): what he wears that the loadout doesn't
        /// wear, and that goes away by the mode, comes off whether or not a chest takes it (what no chest takes stays with
        /// him); false: 0.5's way, only into a chest or for something in its place (card A34, decisions 37 and 47).</summary>
        internal bool OffWithoutChest;

        /// <summary>"A belt or trinket the loadout doesn't name": ComesOff (card B18, the review's major finding: a
        /// Megingjord coming off away from chests takes 150 off his carry weight mid-fight); false (KeepWearing, the
        /// default): 0.5's way for utility-slot pieces.</summary>
        internal bool UtilityOffWithoutChest;

        /// <summary>Whether this worn piece comes off when no chest takes it and nothing takes its place: a belt or a
        /// trinket by its own setting, anything else by "What I wear that the loadout doesn't".</summary>
        internal bool OffWithoutChestFor(ItemDrop.ItemData item) =>
            item != null && IsUtility(item.m_shared.m_itemType) ? UtilityOffWithoutChest : OffWithoutChest;

        /// <summary>A utility-slot piece: a belt (Utility) or a trinket.</summary>
        internal static bool IsUtility(ItemDrop.ItemData.ItemType type) =>
            type == ItemDrop.ItemData.ItemType.Utility || type == ItemDrop.ItemData.ItemType.Trinket;
        internal bool QuickSlotsInFullyReplace;
        internal PlacesKept Places;
        internal CellTaken Taken;
        internal QuickSlotItemsWithout QuickSlotsWithout;
        internal FetchFrom FetchFrom;
        internal bool LeaveOne;
        internal bool Instant;
        internal bool Effects;

        /// <summary>The game's world level now (Game.m_worldLevel): chests' items below it aren't counted or taken, as the
        /// chest service never counts them.</summary>
        internal int WorldLevel;

        /// <summary>The rules as the settings say now, in this mode.</summary>
        internal static EquipRules FromSettings(EquipMode mode) => new EquipRules
        {
            Mode = mode,
            Quality = Settings.Quality.Value,
            MatchStyle = Settings.MatchStyle.Value,
            ExactItem = Settings.ExactItem.Value,
            Prefer = Settings.Prefer.Value,
            AmmoIsGear = Settings.AmmoIsGear.Value,
            ToolsAreGear = Settings.ToolsAreGear.Value,
            TorchesAreGear = Settings.TorchesAreGear.Value,
            TopUp = Settings.TopUps.Value,
            Extra = Settings.Extra.Value,
            LeaveFavourites = Settings.LeaveFavourites.Value,
            FavouriteGearSwaps = Settings.GearFavourites.Value == FavouriteGear.Swaps,
            FavouritesGo = Settings.FavouritesGo.Value,
            FoodStatAtLeast = Settings.FoodStatAtLeast.Value,
            KeepWornWhenMissing = Settings.KeepWornWhenMissing.Value,
            OffWithoutChest = Settings.NotWorn.Value == WornNotInLoadout.ComesOff,
            UtilityOffWithoutChest = Settings.UtilityNotWorn.Value == WornUtilityNotInLoadout.ComesOff,
            QuickSlotsInFullyReplace = Settings.QuickSlotsInFullyReplace.Value,
            Places = Settings.Places.Value,
            Taken = Settings.Taken.Value,
            QuickSlotsWithout = Settings.QuickSlotsWithout.Value,
            FetchFrom = Settings.Fetch.Value,
            LeaveOne = Settings.ChestRules().LeaveOne,
            Instant = Settings.Timing.Value == EquipTime.Instant,
            Effects = Settings.Effects.Value,
            WorldLevel = Game.m_worldLevel,
        };

        /// <summary>Whether putting this loadout on puts his favourites away like any other item: its own switch, else the
        /// setting's (his words, 2026-10-07).</summary>
        internal bool FavouritesGoFor(Outfitter.Loadouts.Loadout loadout) => loadout?.FavouritesGo ?? FavouritesGo;

        /// <summary>Whether the extra beyond a loadout's count goes away in this mode.</summary>
        internal bool ExtraGoesAway =>
            Extra == MoreThanCount.PutAway || (Extra == MoreThanCount.PutAwayInFullyReplace && Mode == EquipMode.FullyReplace);
    }
}
