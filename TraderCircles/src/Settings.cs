using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace TraderCircles
{
    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.tradercircles.cfg, in the sections
    /// General and Look; inside miikeskii's QoL Pack, in the bundle's file, in the section "Trader Circles". Every one
    /// takes effect at once, also when changed in game with a configuration manager: the mod reads them every frame.
    /// </summary>
    internal static class Settings
    {
        internal const string Look = "Look";

        /// <summary>The sizes offered, in metres across (his choice, 2026-09-25): each has circles of its own, and fewer sizes
        /// leave less to compare.</summary>
        internal static readonly int[] Sizes = { 600, 900, 1200, 1500, 2000 };

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<int> Diameter { get; private set; }

        internal static ConfigEntry<int> FillOpacity { get; private set; }

        internal static ConfigEntry<bool> Outline { get; private set; }

        internal static ConfigEntry<bool> ShowOnMinimap { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first; the bundle puts both parts in one section, so the orders run on across them.
            Enabled = settings.Enabled;

            int order = 90;
            foreach (Trader trader in Trader.All)
            {
                trader.Shown = settings.Bind(ModuleSettings.General, trader.Key, false,
                    $"Circles for {trader.DisplayName}", order,
                    $"On: circles on the big map around every place {trader.DisplayName} could still be, until " +
                    $"{trader.DisplayName} is found (by you or anyone else in the world). The first time this is on in a " +
                    "world, the mod asks the server where those places are, as a vegvisir does, and keeps the answer to " +
                    "itself: no pins, no messages.");
                order -= 10;
            }

            Diameter = settings.Bind(ModuleSettings.General, "Diameter", 1200,
                "Circle size (metres across)", 50,
                "How wide each circle is, in metres: 600, 900, 1200, 1500 or 2000. Every circle holds the place it stands " +
                "for somewhere inside it, never at a set point, and each size has circles of its own. The part of a circle " +
                "where that trader can't appear isn't drawn.",
                new AcceptableValueList<int>(Sizes));

            FillOpacity = settings.Bind(Look, "FillOpacity", 25,
                "How solid the fill is (%)", 40,
                "How strongly each circle's colour covers the map, in percent: low is more see-through.",
                new AcceptableValueRange<int>(5, 90));

            Outline = settings.Bind(Look, "Outline", false,
                "Thin outline", 35,
                "On: each circle also gets a thin outline in its own colour.");

            order = 30;
            foreach (Trader trader in Trader.All)
            {
                trader.Colour = settings.Bind(Look, trader.Key + "Colour", trader.DefaultColour,
                    $"{trader.DisplayName}'s colour", order,
                    $"The colour of {trader.DisplayName}'s circles. Only the colour counts; how see-through it is comes from " +
                    "\"How solid the fill is\".");
                order -= 1;
            }

            ShowOnMinimap = settings.Bind(Look, "ShowOnMinimap", false,
                "Also on the small map", 20,
                "On: the circles are drawn on the small map in the corner too. At its usual zoom the small map shows less " +
                "ground than one circle, so near a circle it is just tinted.");
        }

        /// <summary>The fill's colour for a trader: its colour setting, with the fill's see-through setting.</summary>
        internal static Color FillColour(Trader trader)
        {
            Color colour = trader.Colour.Value;
            colour.a = Mathf.Clamp01(FillOpacity.Value / 100f);
            return colour;
        }

        /// <summary>The outline's colour: the same colour, more solid than the fill.</summary>
        internal static Color OutlineColour(Trader trader)
        {
            Color colour = trader.Colour.Value;
            colour.a = Mathf.Clamp01(Mathf.Max(0.8f, FillOpacity.Value / 100f));
            return colour;
        }
    }
}
