using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Kit;

namespace MapViewRules
{
    /// <summary>Whether a section's pins are drawn on a map, or the same as the section it follows.</summary>
    internal enum Shown
    {
        Follow,
        Show,
        Hide,
    }

    /// <summary>
    /// The defaults of a section of rules that doesn't follow in everything: his own rules, made the pack's defaults by
    /// card A63 (2026-10-06). Keyed by the section's part name, an override's included ("Override - Boar"); any other
    /// section follows in everything, as before, and the keys and part names are unchanged. A settings file still at the
    /// old default (follow) moves once (ModuleSettings.MovedDefault), also an override section bound later.
    /// </summary>
    internal sealed class SectionDefaults
    {
        internal Shown LargeShow = Shown.Follow;
        internal Shown LargeNames = Shown.Follow;
        internal int LargeIconsPast = RuleSection.Follow;
        internal int LargeNamesPast = RuleSection.Follow;
        internal Shown MinimapShow = Shown.Follow;

        private static readonly SectionDefaults Follows = new SectionDefaults();

        private static readonly Dictionary<string, SectionDefaults> Own = new Dictionary<string, SectionDefaults>(StringComparer.Ordinal)
        {
            ["Chests"] = new SectionDefaults { LargeIconsPast = 30, LargeNamesPast = 10 },
            ["Dungeons"] = new SectionDefaults { LargeIconsPast = 30, LargeNamesPast = 20 },
            ["Lore stones"] = new SectionDefaults { LargeIconsPast = 20, LargeNamesPast = 10 },
            ["Ore"] = new SectionDefaults { LargeIconsPast = 20, LargeNamesPast = 10 },
            ["Runestones"] = new SectionDefaults { LargeIconsPast = 20, LargeNamesPast = 10 },
            ["Spawners"] = new SectionDefaults { LargeIconsPast = 30, LargeNamesPast = 20 },
            ["Pickables"] = new SectionDefaults { LargeShow = Shown.Show, LargeNames = Shown.Show, LargeIconsPast = 10, LargeNamesPast = 0 },
            ["Pickables - Berries"] = new SectionDefaults { LargeShow = Shown.Show, LargeNames = Shown.Show, LargeIconsPast = 10, LargeNamesPast = 0 },
            ["Override - Boar"] = new SectionDefaults { LargeIconsPast = 10, LargeNamesPast = 10, MinimapShow = Shown.Hide },
            ["Override - Lox"] = new SectionDefaults { LargeIconsPast = 10, LargeNamesPast = 10, MinimapShow = Shown.Hide },
            ["Override - Wolf"] = new SectionDefaults { LargeIconsPast = 10, LargeNamesPast = 10, MinimapShow = Shown.Hide },
            ["Override - Cloudberry"] = new SectionDefaults { MinimapShow = Shown.Hide },
        };

        /// <summary>The defaults of the section with this part name: its own, or follow in everything.</summary>
        internal static SectionDefaults For(string part) => part != null && Own.TryGetValue(part, out SectionDefaults own) ? own : Follows;
    }

    /// <summary>
    /// One section of rules: ten settings saying how the large map and the minimap draw its pins at each zoom. All pins
    /// is the base, with a value for everything. Every other section can leave any of its values to the section it
    /// follows (-1, or Follow), so a change in All pins reaches every section that still follows it: a pickable group
    /// follows Pickables, a category follows All pins, and an override follows its pin's own group or category.
    /// </summary>
    internal sealed class RuleSection
    {
        /// <summary>The value that leaves a number to the section this one follows.</summary>
        internal const int Follow = -1;

        /// <summary>The smallest size a pin shrinks to, as a percentage of its normal size.</summary>
        internal const int SmallestSize = 10;

        // Its ten settings, for Changed.
        private readonly List<ConfigEntryBase> values = new List<ConfigEntryBase>();

        private ConfigEntry<bool> unfolded;
        private ConfigEntry<bool> largeShownBase;
        private ConfigEntry<bool> smallShownBase;
        private ConfigEntry<Shown> largeShown;
        private ConfigEntry<bool> largeNamesShownBase;
        private ConfigEntry<Shown> largeNamesShown;
        private ConfigEntry<Shown> smallShown;
        private ConfigEntry<int> largeIcons;
        private ConfigEntry<int> largeNames;
        private ConfigEntry<int> largeShrink;
        private ConfigEntry<int> largeSmallest;
        private ConfigEntry<int> smallIcons;
        private ConfigEntry<int> smallShrink;
        private ConfigEntry<int> smallSmallest;

        /// <param name="part">Its settings part, which names its section. Permanent.</param>
        /// <param name="plural">How its descriptions name its pins ("berries").</param>
        /// <param name="follows">How its descriptions name what -1 means ("the Pickables section"); null for All pins.</param>
        internal RuleSection(string part, string plural, string follows)
        {
            Part = part;
            Plural = plural;
            Follows = follows;
        }

        internal string Part { get; }

        internal string Plural { get; }

        internal string Follows { get; }

        /// <summary>All pins: the base, whose every value is set.</summary>
        internal bool IsBase => Follows == null;

        // Each value, or null to leave it to the section this one follows. The base never returns null.
        internal bool? LargeShown => IsBase ? largeShownBase.Value : Decode(largeShown.Value);

        internal bool? LargeNamesShown => IsBase ? largeNamesShownBase.Value : Decode(largeNamesShown.Value);

        internal bool? SmallShown => IsBase ? smallShownBase.Value : Decode(smallShown.Value);

        internal int? LargeIconsPast => Number(largeIcons);

        internal int? LargeNamesPast => Number(largeNames);

        internal int? LargeShrinkFrom => Number(largeShrink);

        internal int? LargeSmallest => Number(largeSmallest);

        internal int? SmallIconsPast => Number(smallIcons);

        internal int? SmallShrinkFrom => Number(smallShrink);

        internal int? SmallSmallest => Number(smallSmallest);

        /// <summary>How many of its settings were moved to their new defaults as it was bound, when it is bound while the
        /// game runs (an override section): the host moves those of the sections bound at the start.</summary>
        internal int MovedLate { get; private set; }

        /// <summary>
        /// Binds its ten settings in its own part, and its title row in a configuration manager's window (Folds). The base
        /// gets the defaults given; every other section follows by default, but where SectionDefaults has its own (his
        /// rules, card A63), whose old default, follow, moves once. Each part is a section of its own in the
        /// file (Module.OneSectionInBundle is false), but the window lists them all under one heading, so the orders are
        /// unique across the module: the index-th section bound takes eleven (Folds.TitleOrder).
        /// </summary>
        internal RuleSection Bind(ModuleSettings settings, int index, int icons, int names, int shrinkFrom, int smallest)
        {
            string follow = IsBase ? string.Empty : $" -1: the same as {Follows}.";
            int lowest = IsBase ? 0 : Follow;
            int lowestSize = IsBase ? SmallestSize : Follow;
            var zoom = new AcceptableValueRange<int>(lowest, 100);
            var size = new AcceptableValueRange<int>(lowestSize, 100);
            const string zoomWord = "0 is fully zoomed in and 100 fully out; the zoom readout (General) shows where you are, and the button fills it in.";
            int order = Folds.TitleOrder(index);
            SectionDefaults own = SectionDefaults.For(Part);

            // A default of his (card A63) in place of follow: a file still at follow moves once, at the start or, for a
            // section bound later, at once.
            void Moved<T>(ConfigEntry<T> entry, T follows)
            {
                if (!IsBase && !Equals(entry.DefaultValue, follows) && settings.MovedDefault(entry, follows, 1))
                {
                    MovedLate++;
                }
            }

            // Every setting's description opens with its section's name: the window lists them all under one heading, where
            // a search or a tooltip would otherwise not say which section a "Minimap: show" is.
            ConfigEntry<T> Rule<T>(string key, T defaultValue, string displayName, int ruleOrder, string description,
                                   AcceptableValueBase range = null, System.Action<ConfigEntryBase> drawer = null) =>
                settings.Bind(Part, key, defaultValue, displayName, ruleOrder, Part + ": " + description, range, drawer);

            unfolded = settings.Bind(Part, "Unfolded", false, Part, order,
                $"Whether this section's settings show open in the settings window (F1), under its title row ({Part}); a " +
                "click on the row opens or closes it. Only how that window shows them: the rules are the same either way.",
                null, FoldDrawer.For(this));

            if (IsBase)
            {
                largeShownBase = Rule("LargeMapShow", true, "Large map: icons", order - 1,
                    "Whether the large map draws pins' icons. Their names have a switch of their own, below: a name whose " +
                    "icon is hidden is drawn alone, where its pin is. Every other section follows this one unless it says otherwise.");
                largeNamesShownBase = Rule("LargeMapNames", true, "Large map: names", order - 2,
                    "Whether the large map draws pins' names, with their icons or, where the icons are hidden, alone. With " +
                    "both hidden, a pin isn't drawn at all and can't be clicked. Every other section follows this one unless " +
                    "it says otherwise.");
            }
            else
            {
                largeShown = Rule("LargeMapShow", own.LargeShow, "Large map: icons", order - 1,
                    $"Whether the large map draws the icons of {Plural}. Their names have a switch of their own, below: a " +
                    $"name whose icon is hidden is drawn alone, where its pin is. Follow: the same as {Follows}.");
                Moved(largeShown, Shown.Follow);
                largeNamesShown = Rule("LargeMapNames", own.LargeNames, "Large map: names", order - 2,
                    $"Whether the large map draws the names of {Plural}, with their icons or, where the icons are hidden, " +
                    $"alone. With both hidden, a pin isn't drawn at all and can't be clicked. Follow: the same as {Follows}.");
                Moved(largeNamesShown, Shown.Follow);
            }
            largeIcons = Rule("LargeMapIconsPast", IsBase ? icons : own.LargeIconsPast, "Large map: hide icons past zoom", order - 3,
                $"Past this zoom the large map stops drawing {Plural}, icons and names; 100 never hides them. {zoomWord}{follow}",
                zoom, ZoomDrawer.For(minimap: false, lowest));
            Moved(largeIcons, Follow);
            largeNames = Rule("LargeMapNamesPast", IsBase ? names : own.LargeNamesPast, "Large map: hide names past zoom", order - 4,
                $"Past this zoom the large map stops drawing the names of {Plural} (their icons, if shown, stay); 100 never hides " +
                $"them. A hidden name shows while the pointer is over its pin (General). {zoomWord}{follow}",
                zoom, ZoomDrawer.For(minimap: false, lowest));
            Moved(largeNames, Follow);
            largeShrink = Rule("LargeMapShrinkFrom", IsBase ? shrinkFrom : Follow, "Large map: shrink from zoom", order - 5,
                $"Past this zoom the large map draws {Plural} smaller and smaller, down to the size below when zoomed fully " +
                $"out; 100 never shrinks them. {zoomWord}{follow}",
                zoom, ZoomDrawer.For(minimap: false, lowest));
            largeSmallest = Rule("LargeMapSmallest", IsBase ? smallest : Follow, "Large map: size fully zoomed out (%)", order - 6,
                $"How big the large map draws {Plural} when zoomed fully out, as a percentage of their normal size " +
                $"({SmallestSize} to 100).{follow}",
                size);

            if (IsBase)
            {
                smallShownBase = Rule("MinimapShow", true, "Minimap: show", order - 7,
                    "Whether the minimap draws pins at all. Every other section follows this one unless it says otherwise.");
            }
            else
            {
                smallShown = Rule("MinimapShow", own.MinimapShow, "Minimap: show", order - 7,
                    $"Whether the minimap draws {Plural}. Follow: the same as {Follows}.");
                Moved(smallShown, Shown.Follow);
            }
            smallIcons = Rule("MinimapIconsPast", IsBase ? icons : Follow, "Minimap: hide icons past zoom", order - 8,
                $"Past this zoom the minimap stops drawing {Plural}; 100 never hides them. The minimap never draws names. " +
                $"{zoomWord}{follow}",
                zoom, ZoomDrawer.For(minimap: true, lowest));
            smallShrink = Rule("MinimapShrinkFrom", IsBase ? shrinkFrom : Follow, "Minimap: shrink from zoom", order - 9,
                $"Past this zoom the minimap draws {Plural} smaller and smaller, down to the size below when zoomed fully " +
                $"out; 100 never shrinks them. {zoomWord}{follow}",
                zoom, ZoomDrawer.For(minimap: true, lowest));
            smallSmallest = Rule("MinimapSmallest", IsBase ? smallest : Follow, "Minimap: size fully zoomed out (%)", order - 10,
                $"How big the minimap draws {Plural} when zoomed fully out, as a percentage of their normal size " +
                $"({SmallestSize} to 100).{follow}",
                size);

            values.Add(IsBase ? (ConfigEntryBase)largeShownBase : largeShown);
            values.Add(IsBase ? (ConfigEntryBase)largeNamesShownBase : largeNamesShown);
            values.AddRange(new ConfigEntryBase[] { largeIcons, largeNames, largeShrink, largeSmallest });
            values.Add(IsBase ? (ConfigEntryBase)smallShownBase : smallShown);
            values.AddRange(new ConfigEntryBase[] { smallIcons, smallShrink, smallSmallest });
            Folds.Add(unfolded, values, settings.SectionOf(ModuleSettings.General));

            // A change shows at once: the map is asked to draw its pins again.
            if (IsBase)
            {
                Watch(largeShownBase);
                Watch(largeNamesShownBase);
                Watch(smallShownBase);
            }
            else
            {
                Watch(largeShown);
                Watch(largeNamesShown);
                Watch(smallShown);
            }
            foreach (ConfigEntry<int> number in new[] { largeIcons, largeNames, largeShrink, largeSmallest, smallIcons, smallShrink, smallSmallest })
            {
                Watch(number);
            }
            return this;
        }

        /// <summary>
        /// Once, for a settings file from 0.1.0, where "Large map: show" (LargeMapShow) hid icons and names together: its
        /// names get the same value, so the map looks as it did. Called before anything is drawn.
        /// </summary>
        internal void CarryOverShow()
        {
            if (IsBase)
            {
                largeNamesShownBase.Value = largeShownBase.Value;
            }
            else
            {
                largeNamesShown.Value = largeShown.Value;
            }
        }

        /// <summary>Whether any of its ten settings isn't at its default, for its title row.</summary>
        internal bool Changed()
        {
            foreach (ConfigEntryBase value in values)
            {
                if (!Equals(value.BoxedValue, value.DefaultValue))
                {
                    return true;
                }
            }
            return false;
        }

        private static void Watch<T>(ConfigEntry<T> entry) => entry.SettingChanged += (sender, args) => Drawing.RequestRedraw();

        private static bool? Decode(Shown shown) => shown == Shown.Show ? true : shown == Shown.Hide ? false : (bool?)null;

        private int? Number(ConfigEntry<int> entry)
        {
            int value = entry.Value;
            if (value >= 0)
            {
                return value;
            }
            return IsBase ? 0 : (int?)null;   // the base's range starts at 0; a hand-edited -1 there counts as 0
        }
    }
}
