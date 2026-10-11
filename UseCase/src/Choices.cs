namespace UseCase
{
    // The choices of the settings that aren't on or off. Their names are what a configuration manager shows, and what the
    // settings file keeps, so they never change once released.

    /// <summary>When the panel shows beside a tooltip.</summary>
    internal enum ShowWhen
    {
        OnHover,
        WhileKeyHeld,
        ToggledByKey,
    }

    /// <summary>What "· X" says for an upgrade.</summary>
    internal enum UpgradeNumber
    {
        /// <summary>"· 2 per level": the recipe's amount per level.</summary>
        PerLevel,

        /// <summary>"· 2 / 4": what each level takes, from level 2 up.</summary>
        EachLevel,
    }

    /// <summary>What "· X" says for a station that processes.</summary>
    internal enum ProcessingNumber
    {
        /// <summary>How many the station holds at once (a smelter's 10), as his rule for fuel.</summary>
        StationHolds,

        /// <summary>How many one product takes (1).</summary>
        PerProduct,
    }

    /// <summary>The order inside a section.</summary>
    internal enum InsideOrder
    {
        /// <summary>The pack's order: Meadows things first, then the game's own order (PROJECT.md rule 7).</summary>
        Progression,

        /// <summary>As the game's menus list them: the tools' tables, the crafting list.</summary>
        GameMenus,

        AToZ,
    }

    /// <summary>How a line reads while its thing is unknown.</summary>
    internal enum UnknownLine
    {
        /// <summary>"??? · 6": his words keep the amount.</summary>
        HiddenWithAmount,

        /// <summary>"???" alone.</summary>
        Hidden,

        /// <summary>Not listed at all.</summary>
        LeftOut,

        /// <summary>One line at a section's end: "3 more you haven't found".</summary>
        CountedAtTheEnd,
    }

    /// <summary>Until when an altar or a locked door is unknown.</summary>
    internal enum PlacesUnknownUntil
    {
        /// <summary>Until it has loaded near him (the mod learns it then).</summary>
        Seen,

        /// <summary>Until its boss is beaten (the world's defeat key).</summary>
        BossBeaten,

        NeverHidden,
    }

    /// <summary>Seasonal recipes and pieces out of their season.</summary>
    internal enum OutOfSeason
    {
        /// <summary>Listed, marked with their season ("Jack-o-turnip (Halloween)").</summary>
        ListedMarked,

        Hidden,
    }

    /// <summary>Things another mod adds (known only for Jötunn mods' own).</summary>
    internal enum OtherMods
    {
        Shown,
        MarkedWithTheirMod,
        Hidden,
    }

    /// <summary>Where the panel sits.</summary>
    internal enum PanelSide
    {
        /// <summary>Right of the tooltip, or left when there's no room on the right.</summary>
        Beside,
        AlwaysRight,
        AlwaysLeft,
        Below,
    }

    /// <summary>What a list too long for the screen does.</summary>
    internal enum LongLists
    {
        /// <summary>Runs on in columns beside each other.</summary>
        Columns,

        /// <summary>Each section shows its first lines, then "and 57 more".</summary>
        CapPerSection,

        /// <summary>Each section shows as its heading and a count until the key is held.</summary>
        FoldedUntilKeyHeld,
    }

    /// <summary>Pieces TimedTorchesStayLit keeps lit with no fuel.</summary>
    internal enum KeptLitPieces
    {
        /// <summary>Listed under Refueling as the game has them (his friends may not run that mod).</summary>
        ShownAsTheGameHasThem,

        LeftOutOfRefueling,
    }
}
