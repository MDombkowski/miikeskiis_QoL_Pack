using BepInEx.Configuration;
using QoLMods.Catalogue;
using QoLMods.Kit;
using UnityEngine;

namespace ComfortBuild
{
    /// <summary>The order of the pieces on the Comfort tab, inside each group.</summary>
    internal enum PieceOrder
    {
        Progression,
        ComfortHighestFirst,
        Name,
    }

    /// <summary>How a piece that would add nothing where he stands looks.</summary>
    internal enum NothingLook
    {
        GreyWithTick,
        DimmedWithTick,
        TickOnly,
    }

    /// <summary>Where the comfort line shows: beside the build panel, on its right or its left, or not at all.</summary>
    internal enum LineSide
    {
        Right,
        Left,
        Off,
    }

    /// <summary>The corner of a thumbnail the comfort number sits in.</summary>
    internal enum Corner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.comfortbuild.cfg, in the section
    /// General; inside miikeskii's QoL Pack, in the bundle's file, in the section "Comfort Build". Every one takes effect
    /// at once: the mod reads them each time it fills the build menu or counts comfort.
    /// </summary>
    internal static class Settings
    {
        /// <summary>The default colour of the comfort number: the light blue Add to Cart's Build tab uses too (its
        /// Ui.Comfort), so the two show comfort alike.</summary>
        internal static readonly Color DefaultNumberColor = new Color(0.55f, 0.85f, 1f, 1f);

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<bool> ComfortTab { get; private set; }

        internal static ConfigEntry<bool> GreyOut { get; private set; }

        internal static ConfigEntry<NothingLook> Look { get; private set; }

        internal static ConfigEntry<bool> EveryTab { get; private set; }

        internal static ConfigEntry<LineSide> Line { get; private set; }

        internal static ConfigEntry<bool> GroupValues { get; private set; }

        internal static ConfigEntry<bool> UnlitAsLit { get; private set; }

        internal static ConfigEntry<PieceOrder> Order { get; private set; }

        internal static ConfigEntry<string> GroupOrder { get; private set; }

        internal static ConfigEntry<Corner> NumberCorner { get; private set; }

        internal static ConfigEntry<Color> NumberColor { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings in a section by
            // their Order, highest first.
            Enabled = settings.Enabled;

            // On for everyone, at his word (2026-09-30): it adds a tab and changes nothing already in the menu.
            ComfortTab = settings.Bind(ModuleSettings.General, "ComfortTab", true,
                "Comfort tab", 90,
                "A Comfort tab in the build menu, after Favorites: only the pieces that give comfort, with their comfort " +
                "groups down the left, each piece's comfort in a corner of its picture, and what counts where you stand. " +
                "The game's own tabs are left as they are unless \"Comfort on every tab\" is on.");

            GreyOut = settings.Bind(ModuleSettings.General, "GreyOut", true,
                "Grey out unneeded", 80,
                "Pieces that wouldn't raise your comfort where you stand are shown in grey, with a small tick, and the " +
                "comfort line beside the build panel says why: one as good or better of the same group is already near you (only the best of each " +
                "group counts), or, for a piece with no group, one of the same kind. The switch \"Grey Out Unneeded\" at " +
                "the top of the Comfort tab's list changes this setting too.");

            Look = settings.Bind(ModuleSettings.General, "Look", NothingLook.GreyWithTick,
                "Look of the unneeded", 75,
                "How a piece that wouldn't raise your comfort looks, with \"Grey out unneeded\" on. GreyWithTick: " +
                "its picture in grey, with the game's small tick in its bottom corner (\"you have this covered\"). " +
                "DimmedWithTick: its picture darkened, in colour, with the tick. TickOnly: only the tick. The tick keeps it " +
                "apart from the game's own look for a piece you can't afford.");

            EveryTab = settings.Bind(ModuleSettings.General, "EveryTab", false,
                "Comfort on every tab", 70,
                "Off: the comfort numbers, the grey and the comfort line beside the build panel show on the Comfort tab " +
                "only. On: on the game's own tabs too (By Usage, By Material, Recent, Favorites).");

            Line = settings.Bind(ModuleSettings.General, "Line", LineSide.Right,
                "Comfort line", 65,
                "Where the comfort line shows: a box beside the build panel at the bottom of the screen, saying what the " +
                "piece under the pointer (or the one in hand) would add to your comfort where you stand, and why not all " +
                "of it. Right: right of the panel (the chat window covers it while it shows). Left: left of the panel " +
                "(where some mods put a bar of their own, such as EquipmentAndQuickSlots' quick slots). Off: no box.");

            GroupValues = settings.Bind(ModuleSettings.General, "GroupValues", true,
                "What counts, beside each group", 60,
                "On: each comfort group in the Comfort tab's list shows what it gives where you stand and the most it can " +
                "give with the pieces you can build, as \"Chairs 2/3\".");

            UnlitAsLit = settings.Bind(ModuleSettings.General, "UnlitAsLit", false,
                "Count unlit fires as lit", 50,
                "Off: a fire that is out gives nothing, as the game counts it, so a new fire would still add comfort. On: " +
                "a fire that is out counts as if it were lit, for the grey and the numbers (the game still counts it only " +
                "while it burns). Also the hot tub, which counts only while heated.");

            Order = settings.Bind(ModuleSettings.General, "Order", PieceOrder.Progression,
                "Order", 40,
                "The order of the pieces in each group on the Comfort tab. Progression: in the order you unlock them, " +
                "Meadows first, then the game's own order. ComfortHighestFirst: highest comfort first. Name: A to Z. Under " +
                "\"Show All\", the " +
                "groups follow one another in the list's order.");

            GroupOrder = settings.Bind(ModuleSettings.General, "GroupOrder", Taxonomy.DefaultComfortGroupLine,
                "Order of the groups", 30,
                "The comfort groups in the Comfort tab's list, separated by commas. Groups left out go at the end in the " +
                "default order; names it doesn't know are ignored. Default: the game's own order, with Other (pieces with " +
                "no group, which count once per kind) last.");

            NumberCorner = settings.Bind(ModuleSettings.General, "NumberCorner", Corner.TopLeft,
                "Corner of the number", 20,
                "Which corner of a piece's picture shows its comfort.");

            NumberColor = settings.Bind(ModuleSettings.General, "NumberColor", DefaultNumberColor,
                "Colour of the number", 10,
                "The colour of the comfort number in a piece's corner, of the tick on a piece that adds nothing, of the " +
                "values beside each group and \"Comfort here\", and of the comfort in the comfort line.");

            // What the menu draws follows these; the order of the grid only the last two.
            Watch(ComfortTab, false);
            Watch(GreyOut, false);
            Watch(Look, false);
            Watch(EveryTab, false);
            Watch(Line, false);
            Watch(GroupValues, false);
            Watch(UnlitAsLit, false);
            Watch(NumberCorner, false);
            Watch(NumberColor, false);
            Watch(Order, true);
            Watch(GroupOrder, true);
        }

        /// <summary>Goes up whenever one of the settings the menu draws with changes (in F1, or by the switch).</summary>
        internal static int Version { get; private set; }

        /// <summary>Goes up whenever the order of the pieces or of the groups changes.</summary>
        internal static int OrderVersion { get; private set; }

        private static void Watch<T>(ConfigEntry<T> entry, bool order)
        {
            entry.SettingChanged += (sender, args) =>
            {
                Version++;
                if (order)
                {
                    OrderVersion++;
                }
            };
        }
    }
}
