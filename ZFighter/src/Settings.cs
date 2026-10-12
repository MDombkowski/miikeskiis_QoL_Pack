using BepInEx.Configuration;
using QoLMods.Kit;
using QoLMods.ListPicker;

namespace ZFighter
{
    /// <summary>The setting "Which pieces".</summary>
    internal enum WhichPieces
    {
        /// <summary>Every piece, except those on "Pieces left alone".</summary>
        EveryPiece,

        /// <summary>Only the pieces on "Listed pieces".</summary>
        OnlyListedPieces,
    }

    /// <summary>The setting "Pieces built by others".</summary>
    internal enum OthersPieces
    {
        /// <summary>Fixed too.</summary>
        FixedToo,

        /// <summary>Only the pieces you built are nudged; others' stay as the game draws them (yours still move off
        /// theirs).</summary>
        OnlyMine,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.zfighter.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "Z-Fighter". Every one takes effect at
    /// once, also when changed in game with a configuration manager: the Tracker compares them each frame and draws
    /// every piece again when one changes.
    /// </summary>
    internal static class Settings
    {
        /// <summary>The rugs, left to the game and to Grounded, which lays them at heights of their own (Grounded's
        /// own list of rugs, card A74).</summary>
        internal const string RugsDefault =
            "rug_straw,rug_deer,rug_bjorn,rug_wolf,rug_fur,rug_hare,rug_asksvin,rug_seal,rug_moose,jute_carpet,jute_carpet_blue";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<OnTop> WhichOnTop { get; private set; }

        internal static ConfigEntry<float> NudgeMm { get; private set; }

        internal static ConfigEntry<WhichPieces> Pieces { get; private set; }

        internal static ConfigEntry<string> Listed { get; private set; }

        internal static ConfigEntry<string> LeftAlone { get; private set; }

        internal static ConfigEntry<OthersPieces> Others { get; private set; }

        internal static ConfigEntry<bool> LogDetails { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first.
            Enabled = settings.Enabled;

            WhichOnTop = settings.Bind(ModuleSettings.General, "WhichOnTop", OnTop.SmallerPiece,
                "Which one shows on top", 9,
                "Where two pieces are laid into each other, which one's surface is drawn over the other's. " +
                "SmallerPiece (the default): the smaller of the two, so a 1x1 floor laid into a 2x2 shows. " +
                "BiggerPiece: the bigger one. ByKind: beams and poles over roofs, roofs over walls, walls over floors, " +
                "furniture and decor over everything. NoPreference: each overlap is still kept apart, and which one " +
                "shows is picked by the pieces' places, the same every time. Pieces of nearly one size (within a " +
                "quarter) are only kept apart, never ordered by size. The game keeps no record of when a piece was " +
                "built, so the newer of two can't be told apart from the older.");

            NudgeMm = settings.Bind(ModuleSettings.General, "NudgeMm", 1f,
                "Nudge (mm)", 8,
                "How far apart two overlapping surfaces are drawn, in millimetres. One of the two pieces is drawn this " +
                "much bigger on every side (and lifted by half of it); a third piece laid into both, twice as much, " +
                "and so on. Bigger stops the flicker farther away; smaller changes the look less. Nothing moves where " +
                "it's saved, and the piece is never moved for anyone else.",
                new AcceptableValueRange<float>(0.5f, 5f));

            Pieces = settings.Bind(ModuleSettings.General, "WhichPieces", WhichPieces.EveryPiece,
                "Which pieces", 7,
                "EveryPiece (the default): every building piece, except those on \"Pieces left alone\". " +
                "OnlyListedPieces: only the pieces on \"Listed pieces\". A piece left out is drawn as the game draws " +
                "it, and the pieces laid into it are still kept apart from it.");

            Listed = settings.Bind(ModuleSettings.General, "ListedPieces", string.Empty,
                "Listed pieces", 6,
                "The pieces fixed with \"Which pieces\" set to OnlyListedPieces: their prefab names, separated by " +
                "commas (wood_floor is the 2x2 wood floor, wood_floor_1x1 the 1x1, wood_beam the wood beam).");

            LeftAlone = settings.Bind(ModuleSettings.General, "LeftAlone", RugsDefault,
                "Pieces left alone", 5,
                "Pieces always drawn as the game draws them: their prefab names, separated by commas. The rugs by " +
                "default, which the game, and Grounded, already lay at heights of their own.");

            Others = settings.Bind(ModuleSettings.General, "OthersPieces", OthersPieces.FixedToo,
                "Pieces built by others", 4,
                "FixedToo (the default): pieces anyone built are fixed. OnlyMine: only the pieces you built are drawn " +
                "nudged, and others' pieces as the game draws them; where yours is laid into theirs, yours is drawn " +
                "off theirs.");

            LogDetails = settings.Bind(ModuleSettings.General, "LogDetails", false,
                "Log what it does", 1,
                "Writes to the log how many pieces are laid into others and how many are drawn nudged, each time " +
                "that changes. Off by default.");

            // The Pick buttons in F1 (card B03): each list picked from pictures of the game's pieces, its text written as
            // Kinds' NameList reads it (split at commas, any case).
            PickerSettings.Bind(settings);
            Lists.Attach(Listed, PieceList("The pieces fixed while \"Which pieces\" is OnlyListedPieces."));
            Lists.Attach(LeftAlone, PieceList("Pieces always drawn as the game draws them."));
        }

        private static ListSpec PieceList(string says) => new ListSpec
        {
            Module = Module.Name,
            Kinds = new[] { ThingKind.Pieces },
            Format = new ListFormat { Separators = ",", Joiner = "," },
            Says = says,
            Warn = text => Module.Log?.LogWarning(text),
        };
    }
}
