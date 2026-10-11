namespace PowerWheel
{
    // The settings' choices (Settings.cs). Their names are what a settings file and a configuration manager show, so
    // they never change once released: a renamed value would reset that setting to its default in every file.

    /// <summary>Which key opens the wheel.</summary>
    internal enum KeyChoice
    {
        /// <summary>The game's "Forsaken power" key (F, or his rebind), which then also uses the power on a tap.</summary>
        GamePowerKey,

        /// <summary>The key in "Own key"; the game's F then works exactly as in the game.</summary>
        OwnKey,
    }

    /// <summary>What a tap of the key does (a press let go before the hold time).</summary>
    internal enum TapChoice
    {
        UsesPower,
        Nothing,
    }

    /// <summary>What letting go of the key does while the wheel is open.</summary>
    internal enum LetGoChoice
    {
        /// <summary>Picks the power under the cursor.</summary>
        Picks,

        /// <summary>Picks it and uses it at once.</summary>
        PicksAndUses,

        /// <summary>Closes the wheel without a change; a click picks while the key is still held.</summary>
        Closes,
    }

    /// <summary>What a left click on a power does (the gamepad's radial confirm too).</summary>
    internal enum LeftClickChoice
    {
        Picks,
        PicksAndUses,
    }

    /// <summary>What a right click does in the wheel.</summary>
    internal enum RightClickChoice
    {
        /// <summary>Closes the wheel without a change, as in the game's own wheel.</summary>
        Closes,

        /// <summary>Picks the power under the cursor and uses it at once.</summary>
        PicksAndUses,
    }

    /// <summary>Where the powers' names show.</summary>
    internal enum NamesChoice
    {
        /// <summary>In the wheel's centre, for the power under the cursor (the game's own wheel does this).</summary>
        OnHover,

        /// <summary>Under every icon as well.</summary>
        Always,
    }

    /// <summary>Which powers the wheel shows.</summary>
    internal enum ShowChoice
    {
        /// <summary>Only the powers this character has earned (by "What counts as earned").</summary>
        EarnedOnly,

        /// <summary>Every power; those not earned yet are dimmed, carry a padlock, and can't be picked.</summary>
        AllUnearnedLocked,

        /// <summary>Every power, and any can be picked. Picking one never earned earns it for good: the game records
        /// every power picked as the character's own.</summary>
        AllAnyPickable,
    }

    /// <summary>What counts as having earned a power.</summary>
    internal enum EarnedChoice
    {
        /// <summary>This character took it at its stone (the game's own record).</summary>
        TakenAtStone,

        /// <summary>Its boss has been beaten in this world (the world's own record).</summary>
        BossBeatenInWorld,

        /// <summary>Either of the two.</summary>
        Either,

        /// <summary>Its boss's trophy is on its sacrificial stone in this world (card B06, his ask): seen there by this
        /// character's game, whoever placed it, or the power taken at the stone (it can't be without its trophy).</summary>
        TrophyAtStone,
    }

    /// <summary>How a power not earned yet looks in the wheel (card B06).</summary>
    internal enum LockedLookChoice
    {
        /// <summary>A padlock alone: no boss icon and no name, "Locked" in the wheel's centre with why (his ask).</summary>
        PadlockOnly,

        /// <summary>Its icon dimmed, with a small padlock at its corner, and its name (0.1's look).</summary>
        DimmedWithPadlock,
    }

    /// <summary>Where the mouse pointer starts when the wheel opens.</summary>
    internal enum CursorChoice
    {
        /// <summary>On the power you have (letting go without moving then changes nothing).</summary>
        OnCurrentPower,

        /// <summary>In the centre (letting go there changes nothing either).</summary>
        InTheCentre,
    }

    /// <summary>Where the message about a pick shows.</summary>
    internal enum MessageChoice
    {
        Centre,
        TopLeft,
        None,
    }
}
