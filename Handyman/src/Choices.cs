namespace Handyman
{
    // The choices of the settings that aren't plain on/off (Settings.cs binds them; card A44 has each one's reason).
    // Their names are what the settings file and a configuration manager show, so they never change once released.

    /// <summary>Which key picks Repair.</summary>
    internal enum KeyChoice
    {
        /// <summary>The game's "Hide/show weapon" key (R), following a rebind in the game's controls.</summary>
        GameHideKey,

        /// <summary>The key in "Own key"; the game's Hide key then works as in the game.</summary>
        OwnKey,
    }

    /// <summary>What a tap does while Repair is already picked.</summary>
    internal enum AgainChoice
    {
        BackToMyPiece,
        PutHammerAway,
        Nothing,
    }

    /// <summary>What a tap does when it should go back to a piece but the tool has had none picked yet this session.</summary>
    internal enum NoPieceChoice
    {
        Nothing,
        PutHammerAway,
        OpenBuildMenu,
    }

    /// <summary>How the hammer is put away, now that a tap of the game's Hide key picks Repair.</summary>
    internal enum PutAwayChoice
    {
        /// <summary>Hold the key: let go sooner and it's a tap.</summary>
        HoldTheKey,

        /// <summary>Shift and the key together put it away at once; the key alone picks Repair at once.</summary>
        ShiftAndTheKey,

        /// <summary>The key never puts a build tool with a Repair away; its hotbar key does, as in the game.</summary>
        HotbarKeyOnly,
    }

    /// <summary>Which build tools the key works with.</summary>
    internal enum ToolsChoice
    {
        EveryToolWithRepair,
        HammerOnly,
    }

    /// <summary>What the key does while the build menu is open.</summary>
    internal enum MenuChoice
    {
        /// <summary>Pick Repair and close the menu, as the menu's own hammer button does.</summary>
        PickRepairAndClose,

        /// <summary>As in the game: the game's Hide key puts the hammer away (an own key does nothing).</summary>
        AsInTheGame,

        Nothing,
    }
}
