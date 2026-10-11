using System.ComponentModel;

// The choices of AFK Raid Blocker's settings. Their names are written into the settings file, so they never change
// once released; the descriptions are what a configuration manager (F1) shows.
namespace AFKRaidBlocker
{
    /// <summary>What counts as being at the keyboard for the minutes rule.</summary>
    internal enum InputCounts
    {
        [Description("Movement only")]
        MovementOnly,

        [Description("Movement, keys and the mouse")]
        MovementKeysAndMouse,
    }

    /// <summary>When sitting or lying down (an emote, a chair, a bed) makes a player away.</summary>
    internal enum SeatTiming
    {
        [Description("At once")]
        AtOnce,

        [Description("After the same minutes")]
        AfterTheMinutes,
    }

    /// <summary>What a player who is away no longer gets.</summary>
    internal enum HeldOff
    {
        [Description("Raids that need a base")]
        RaidsThatNeedABase,

        [Description("Every random event")]
        EveryRandomEvent,
    }

    /// <summary>Where the line going away and coming back shows.</summary>
    internal enum MessagePlace
    {
        [Description("In the middle of the screen")]
        Centre,

        [Description("Top left")]
        TopLeft,

        [Description("None")]
        None,
    }

    /// <summary>What a player is sitting or lying on, as far as the away rules care. Read from the game each frame.</summary>
    internal enum Seat
    {
        None,
        Emote,
        Chair,
        Bed,
    }

    /// <summary>Why a player is away (for the message and the log line).</summary>
    internal enum Why
    {
        Present,
        NoMovement,
        Emote,
        Chair,
        Bed,
    }
}
