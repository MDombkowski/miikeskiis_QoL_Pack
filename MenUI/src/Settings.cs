using BepInEx.Configuration;
using QoLMods.Kit;

namespace MenUI
{
    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.menui.cfg, in the section General;
    /// inside miikeskii's QoL Pack, in the bundle's file, in the section "MenUI". Every one takes effect at once, also
    /// when changed in game with a configuration manager: the menus are measured again after every frame.
    /// </summary>
    internal static class Settings
    {
        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<bool> InGameMenu { get; private set; }

        internal static ConfigEntry<FitMode> HowToFit { get; private set; }

        internal static ConfigEntry<int> SmallestSize { get; private set; }

        internal static ConfigEntry<int> SpaceBelow { get; private set; }

        internal static ConfigEntry<int> HighestPoint { get; private set; }

        internal static ConfigEntry<bool> ArrowKeysReachAll { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first.
            Enabled = settings.Enabled;

            InGameMenu = settings.Bind(ModuleSettings.General, "InGameMenu", true,
                "Also fit the in-game menu (Esc)", 6,
                "On: the menu Esc opens in game is fitted the same way when its buttons reach past the window (other " +
                "mods add buttons there too), shrunk around its middle. Off: only the main menu is fitted.");

            HowToFit = settings.Bind(ModuleSettings.General, "HowToFit", FitMode.ShrinkThenMoveUp,
                "How to fit", 5,
                "Shrink, then move up (the default): the buttons shrink, their top staying where it is, until the last " +
                "one is on screen or they reach the smallest size; then the list also moves up, no higher than the " +
                "highest point. Move up, then shrink: the list moves up first, then shrinks. Shrink only: the list never " +
                "moves; it shrinks as far as it takes. The in-game menu shrinks around its middle, and moves toward the " +
                "middle of the window when it must. Whatever is chosen, every button ends up on screen.");

            SmallestSize = settings.Bind(ModuleSettings.General, "SmallestSize", 70,
                "Smallest size (%)", 4,
                "How small the buttons may get, as a percentage of their own size, before the list moves up instead " +
                "(with \"Shrink, then move up\"). If even that can't fit them, they shrink further, so no button is " +
                "ever left off screen.",
                new AcceptableValueRange<int>(30, 100));

            SpaceBelow = settings.Bind(ModuleSettings.General, "SpaceBelow", 2,
                "Space below the last button (% of the window)", 3,
                "The gap kept between the last button and the bottom of the window, as a percentage of the window's " +
                "height. The in-game menu keeps the same gap above its first button too.",
                new AcceptableValueRange<int>(0, 20));

            HighestPoint = settings.Bind(ModuleSettings.General, "HighestPoint", 60,
                "Highest the main menu may reach (% of the window)", 2,
                "How far up the main menu's list may move, as a percentage of the window's height from its bottom: 60 " +
                "keeps it below the game's logo at most window shapes. A list that already starts higher stays where " +
                "it starts.",
                new AcceptableValueRange<int>(30, 95));

            ArrowKeysReachAll = settings.Bind(ModuleSettings.General, "ArrowKeysReachAll", true,
                "Arrow keys reach every button", 1,
                "On: the main menu's up and down arrow keys reach the buttons other mods add, in the order they are " +
                "shown; the game only knows the buttons it had when it started. Off: the game's own list. (A gamepad " +
                "moves between the buttons its own way, which this leaves alone.)");
        }
    }
}
