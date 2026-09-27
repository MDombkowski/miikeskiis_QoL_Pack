using System.ComponentModel;
using BepInEx.Configuration;
using QoLMods.Kit;

namespace XPortalMapPicker
{
    /// <summary>When the portal names show beside their markers.</summary>
    internal enum PortalNameMode
    {
        [Description("Always")]
        Always,

        [Description("Only under the mouse")]
        OnHover,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.xportalmappicker.cfg, in the
    /// sections General and Map; inside miikeskii's QoL Pack, in the bundle's file, in the section "XPortal Map Picker".
    /// Every one takes effect at once, also when changed in game with a configuration manager: the mod reads them each
    /// frame rather than once at start-up.
    /// </summary>
    internal static class Settings
    {
        private const string General = "General";
        private const string Map = "Map";

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<string> ButtonLabel { get; private set; }

        internal static ConfigEntry<bool> CleanMapWhilePicking { get; private set; }

        internal static ConfigEntry<bool> ShowPlayersWhilePicking { get; private set; }

        internal static ConfigEntry<bool> ShowPortalsInUnexploredAreas { get; private set; }

        internal static ConfigEntry<PortalNameMode> PortalNames { get; private set; }

        internal static ConfigEntry<int> MarkerSize { get; private set; }

        internal static ConfigEntry<bool> ZoomToFitPortals { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the sections in the order they
            // are bound here, and the settings in a section by their Order, highest first. The orders are unique across
            // both parts, because inside the bundle the two parts share one section.
            Enabled = settings.Enabled;

            ButtonLabel = settings.Bind(General, "ButtonLabel", "Map",
                "Button label", 7,
                "The text on the button beside XPortal's destination list. The button grows to fit it, but never gets " +
                "wider than XPortal's Ping button, so keep it short.");

            CleanMapWhilePicking = settings.Bind(Map, "CleanMapWhilePicking", true,
                "Clean map while picking", 6,
                "While you pick, hide every other pin on the big map (your own pins, other mods' pins, pings, and other " +
                "players unless \"Show players while picking\" is on), so only the portals show on your explored map. " +
                "Your own position still shows, and all the pins come back the moment you stop picking.");

            ShowPlayersWhilePicking = settings.Bind(Map, "ShowPlayersWhilePicking", true,
                "Show players while picking", 5,
                "On the clean map, keep showing where the other players are right now: their live icons, with their " +
                "names when the map is zoomed in far enough for the game to show pin names. Every other pin stays hidden. " +
                "Only matters while \"Clean map while picking\" is on; without the clean map, every pin shows anyway.");

            ShowPortalsInUnexploredAreas = settings.Bind(Map, "ShowPortalsInUnexploredAreas", true,
                "Show portals in unexplored areas", 4,
                "Show portals that stand where your map is still dark, such as a friend's portal somewhere you have " +
                "never been. Off shows only the portals in places your map has uncovered (explored by you, or shared " +
                "with you at a cartography table).");

            PortalNames = settings.Bind(Map, "PortalNames", PortalNameMode.Always,
                "Portal names", 3,
                "When the portals' names show below their markers: always, or only while the mouse is over a marker.");

            MarkerSize = settings.Bind(Map, "MarkerSize", 100,
                "Marker size (%)", 2,
                "How big the portal markers are, in percent of the game's own map pins.",
                new AcceptableValueRange<int>(50, 300));

            ZoomToFitPortals = settings.Bind(Map, "ZoomToFitPortals", true,
                "Zoom to fit portals", 1,
                "When the map opens for picking, centre it on the portals and zoom out just far enough to show them " +
                "all. Off opens the map the usual way, centred on you. Your own zoom is put back when picking ends.");
        }
    }
}
