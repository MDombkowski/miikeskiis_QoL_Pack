using System.Linq;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace Handyman
{
    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.handyman.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "Handyman". Every one takes effect at
    /// once, also when changed in game with a configuration manager: the mod reads them at each press of the key.
    /// Card A44 has the reason for each default. Until card A63 (2026-10-06) the mod changed nothing out of the box
    /// (MODULES.md rule 4); since then "R picks Repair" is on, his own setting made the default, and a file still at the
    /// old default moves once (the kit's MovedDefault).
    /// </summary>
    internal static class Settings
    {
        internal const float ShortestHold = 0.2f;
        internal const float LongestHold = 1.5f;

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<bool> RPicksRepair { get; private set; }

        internal static ConfigEntry<KeyChoice> Key { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> OwnKey { get; private set; }

        internal static ConfigEntry<AgainChoice> TapAgain { get; private set; }

        internal static ConfigEntry<NoPieceChoice> NoPiece { get; private set; }

        internal static ConfigEntry<PutAwayChoice> PutAway { get; private set; }

        internal static ConfigEntry<float> HoldSeconds { get; private set; }

        internal static ConfigEntry<bool> RestoreTurn { get; private set; }

        internal static ConfigEntry<ToolsChoice> Tools { get; private set; }

        internal static ConfigEntry<bool> ServingTrayRemove { get; private set; }

        internal static ConfigEntry<MenuChoice> BuildMenuOpen { get; private set; }

        internal static ConfigEntry<bool> Message { get; private set; }

        internal static ConfigEntry<bool> Sound { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first.
            Enabled = settings.Enabled;

            RPicksRepair = settings.Bind(ModuleSettings.General, "RPicksRepair", true,
                "R picks Repair", 20,
                "On (the default): with the hammer in hand, a tap of R (the game's \"Hide/show weapon\" key, or the key in \"Own key\") " +
                "picks the hammer's Repair, a tap again goes back to the piece you had, turned as it was, and holding R " +
                "puts the hammer away (each of those is a setting below). Off: R puts the hammer away, as in the game. " +
                "Only build tools with a Repair change; every other item, and R with empty hands, work as in the game.");
            settings.MovedDefault(RPicksRepair, false, 1);

            Key = settings.Bind(ModuleSettings.General, "Key", KeyChoice.GameHideKey,
                "Key", 19,
                "GameHideKey: the game's \"Hide/show weapon\" key, R unless you rebound it in the game's controls (a rebind " +
                "carries over). OwnKey: the key in \"Own key\"; R then puts the hammer away as in the game, and \"Putting " +
                "the hammer away\" and \"How long is a hold\" don't apply.");

            OwnKey = settings.Bind(ModuleSettings.General, "OwnKey", new KeyboardShortcut(KeyCode.H),
                "Own key", 18,
                "The key that picks Repair when \"Key\" is OwnKey (H is free in the game). A mouse button works too " +
                "(Mouse3 and Mouse4 are the side buttons).");

            TapAgain = settings.Bind(ModuleSettings.General, "TapAgain", AgainChoice.BackToMyPiece,
                "Tap again while Repair is picked", 17,
                "BackToMyPiece: picks the piece you had before Repair (however Repair was picked), turned as it was. " +
                "PutHammerAway: puts the hammer away. Nothing: the key does nothing until another piece is picked.");

            NoPiece = settings.Bind(ModuleSettings.General, "NoPiece", NoPieceChoice.Nothing,
                "With no piece to go back to", 16,
                "When a tap should go back to your piece but this tool hasn't had another piece picked since the game " +
                "started (the game starts the hammer on Repair), or that piece can't be picked now (out of season). " +
                "Nothing: nothing happens (with \"Message\" on, it says so). PutHammerAway: puts the hammer away. " +
                "OpenBuildMenu: opens the build menu to pick one.");

            PutAway = settings.Bind(ModuleSettings.General, "PutAway", PutAwayChoice.HoldTheKey,
                "Putting the hammer away", 15,
                "With the game's Hide key. HoldTheKey: hold R to put the hammer away; let go sooner and it's a tap. " +
                "ShiftAndTheKey: Shift + R puts it away at once, and R alone picks Repair at once. HotbarKeyOnly: R never " +
                "puts it away; the hammer's hotbar key does, as in the game.");

            HoldSeconds = settings.Bind(ModuleSettings.General, "HoldSeconds", 0.33f,
                "How long is a hold (seconds)", 14,
                "With \"Putting the hammer away\" at HoldTheKey: how long R must be held to put the hammer away. " +
                "0.33 is the game's own limit for a tap of its gamepad's hide button.",
                new AcceptableValueRange<float>(ShortestHold, LongestHold));

            RestoreTurn = settings.Bind(ModuleSettings.General, "RestoreTurn", true,
                "Going back restores the piece's turn", 13,
                "On: going back to your piece turns it as it was, also a piece the game turns at random each time it's " +
                "picked. Off: the game turns it as it does when you pick a piece in the build menu.");

            Tools = settings.Bind(ModuleSettings.General, "Tools", ToolsChoice.EveryToolWithRepair,
                "Tools", 12,
                "Which tools with a Repair the key works with. EveryToolWithRepair: the hammer and any other mod's build " +
                "tool that has a Repair. HammerOnly: the game's hammer only. The hoe and the cultivator have no Repair, so R " +
                "works as in the game with them; the serving tray's Remove has a setting of its own, below.");

            ServingTrayRemove = settings.Bind(ModuleSettings.General, "ServingTrayRemove", false,
                "Serving tray picks its Remove", 11,
                "On: with the serving tray (or another tool with a Remove and no Repair) in hand, the key picks its Remove " +
                "the same way. Off: R works as in the game with it.");

            BuildMenuOpen = settings.Bind(ModuleSettings.General, "BuildMenuOpen", MenuChoice.PickRepairAndClose,
                "With the build menu open", 10,
                "PickRepairAndClose: a tap picks Repair and closes the menu, as the menu's own hammer button does (a hold " +
                "still puts the hammer away). AsInTheGame: R puts the hammer away, as in the game. Nothing: the key does " +
                "nothing while the menu is open, Shift + R included.");

            Message = settings.Bind(ModuleSettings.General, "Message", true,
                "Message", 9,
                "On: a short message at the top left says what was picked (\"Repair\", then your piece's name).");

            Sound = settings.Bind(ModuleSettings.General, "Sound", true,
                "Sound", 8,
                "On: the build menu's own click sounds when the key picks a piece.");
        }

        /// <summary>True in the frame the key goes down with its modifiers held, read through the game's own input.</summary>
        internal static bool Pressed(KeyboardShortcut key) =>
            key.MainKey != KeyCode.None && ZInput.GetKeyDown(key.MainKey, false) && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));
    }
}
