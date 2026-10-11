using System.ComponentModel;

// The choices of Grounded's settings. Their names are written into the settings file, so they never change once
// released; the descriptions are what a configuration manager (F1) shows.
namespace Grounded
{
    /// <summary>How the key works.</summary>
    internal enum KeyMode
    {
        [Description("Hold it")]
        Hold,

        [Description("Press once on, once off")]
        Toggle,

        [Description("Always on (no key)")]
        Always,
    }

    /// <summary>Which pieces in your hand land on the floor.</summary>
    internal enum WhichPieces
    {
        [Description("The rugs")]
        Rugs,

        [Description("Any piece")]
        AnyPiece,
    }

    /// <summary>What happens when no listed floor is behind the rugs.</summary>
    internal enum NoFloor
    {
        [Description("The game's own placement")]
        GameOwn,

        [Description("Land on whatever is behind the rugs")]
        BehindRugs,
    }

    /// <summary>How low a rug lies on the floor.</summary>
    internal enum HowLow
    {
        [Description("The floor's surface as the game sees it")]
        GameSurface,

        [Description("Down to what you see")]
        WhatYouSee,
    }

    /// <summary>Where a rug goes when it overlaps rugs already lying on the floor.</summary>
    internal enum Overlap
    {
        [Description("A step above the highest of them")]
        OnTop,

        [Description("As low as it can without touching another")]
        LowestFree,
    }

    /// <summary>Where Grounded looks for the floor.</summary>
    internal enum LookUnder
    {
        [Description("Under the cursor")]
        Cursor,

        [Description("Under the whole rug (highest floor wins)")]
        WholeRug,
    }
}
