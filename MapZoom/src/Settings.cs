using BepInEx.Configuration;
using QoLMods.Kit;

namespace MapZoom
{
    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.mapzoom.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "Map Zoom". Every one takes effect at
    /// once, also when changed in game with a configuration manager: the mod reads them at each notch of the wheel.
    /// </summary>
    internal static class Settings
    {
        /// <summary>The fewest notches allowed end to end, so one notch can never jump most of the way in or out.</summary>
        internal const int FewestNotches = 4;

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<int> NotchesEndToEnd { get; private set; }

        internal static ConfigEntry<bool> ZoomTowardPointer { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first.
            Enabled = settings.Enabled;

            NotchesEndToEnd = settings.Bind(ModuleSettings.General, "NotchesEndToEnd", 18,
                "Scroll notches, fully in to fully out", 2,
                "How many notches of the mouse wheel take the big map from fully zoomed in to fully zoomed out, and " +
                "the same number back. Fewer zooms faster. Every notch zooms by the same step, so it feels the same " +
                "at every zoom. Without the mod the game takes about 44 notches out and 40 back in. The comma and " +
                "period keys and a gamepad zoom as they always do.",
                new AcceptableValueRange<int>(FewestNotches, 60));

            ZoomTowardPointer = settings.Bind(ModuleSettings.General, "ZoomTowardPointer", true,
                "Zoom toward the mouse pointer", 1,
                "On: the wheel zooms toward the place under the mouse pointer, which stays under the pointer, as on " +
                "web maps. Off: the wheel zooms around the middle of the view, as the game does.");
        }
    }
}
