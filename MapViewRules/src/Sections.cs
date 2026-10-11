using System;
using System.Collections.Generic;
using QoLMods.Kit;

namespace MapViewRules
{
    /// <summary>
    /// Every section of rules: All pins, one per Cartur's Map Pins category and pickable group, one each for the pins
    /// Cartur's doesn't place (hand-placed pins, death markers, the game's own markers, portals, the pins that follow
    /// boats, carts and tames, and any other), and one per name in the override list. The part names are permanent: they name the
    /// sections of the settings file.
    /// </summary>
    internal static class Sections
    {
        private static readonly Dictionary<string, RuleSection> categories = new Dictionary<string, RuleSection>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RuleSection> groups = new Dictionary<string, RuleSection>(StringComparer.OrdinalIgnoreCase);

        // Every override section bound this session, by its title (Words.Title): a section can't be unbound while the game
        // runs, so a name taken off the list keeps its section (unused) until the next start, and gets it back if re-added.
        private static readonly Dictionary<string, RuleSection> overrideSections = new Dictionary<string, RuleSection>(StringComparer.Ordinal);

        // The override list as it applies now, in its order: each name's words and its section.
        private static readonly List<KeyValuePair<string, RuleSection>> overrides = new List<KeyValuePair<string, RuleSection>>();

        private static ModuleSettings settings;

        // How many sections have been bound: each takes the next eleven orders under the one heading (Folds.TitleOrder).
        private static int bound;

        internal static RuleSection All { get; private set; }

        internal static RuleSection Pickables { get; private set; }

        internal static RuleSection Moving { get; private set; }

        internal static RuleSection HandPlaced { get; private set; }

        internal static RuleSection Deaths { get; private set; }

        internal static RuleSection GameMarkers { get; private set; }

        internal static RuleSection Portals { get; private set; }

        internal static RuleSection Others { get; private set; }

        /// <summary>How many names the override list applies now.</summary>
        internal static int OverrideCount => overrides.Count;

        /// <summary>Binds every section, then the sections of the names in the override list now. All pins changes nothing
        /// by default (never hides, never shrinks). Since card A63 (2026-10-06) his own rules are the defaults of some
        /// sections (SectionDefaults: chests, dungeons, lore stones, ore, runestones and spawners lose their icons and names
        /// past a zoom; pickables and berries show, their names only zoomed fully in; the override list's Boar, Lox, Wolf
        /// and Cloudberry are off the minimap), and every other section follows, as before.</summary>
        internal static void Bind(ModuleSettings bindWith)
        {
            settings = bindWith;
            const string all = "the All pins section";
            All = new RuleSection("All pins", "pins", null).Bind(settings, bound++, icons: 100, names: 100, shrinkFrom: 100, smallest: 20);
            foreach (Kinds.Category category in Kinds.Categories)
            {
                categories[category.Name] = new RuleSection(category.Part, category.Plural, all).Bind(settings, bound++, 0, 0, 0, 0);
            }
            Pickables = categories[Kinds.Pickable];
            foreach (Kinds.Category group in Kinds.Groups)
            {
                groups[group.Name] = new RuleSection(group.Part, group.Plural, "the Pickables section").Bind(settings, bound++, 0, 0, 0, 0);
            }
            Moving = new RuleSection("Boats, carts and tames", "the pins Cartur's Map Pins keeps on your boats, carts and tames", all).Bind(settings, bound++, 0, 0, 0, 0);
            HandPlaced = new RuleSection("Hand-placed pins", "the pins you and your friends placed by hand", all).Bind(settings, bound++, 0, 0, 0, 0);
            Deaths = new RuleSection("Death markers", "death markers", all).Bind(settings, bound++, 0, 0, 0, 0);
            GameMarkers = new RuleSection("Game markers", "the game's own markers (your bed, and the places it marks by itself)", all).Bind(settings, bound++, 0, 0, 0, 0);
            Portals = new RuleSection("Portals", "XPortal Map Picker's portal pins", all).Bind(settings, bound++, 0, 0, 0, 0);
            Others = new RuleSection("Other pins", "the pins no other section covers", all).Bind(settings, bound++, 0, 0, 0, 0);
            ApplyOverrides();
        }

        /// <summary>Gives every section's names the value its icons have (RuleSection.CarryOverShow): once, for a settings
        /// file from 0.1.0, whose "show" hid both.</summary>
        internal static void CarryOverShow()
        {
            foreach (RuleSection section in AllSections())
            {
                section.CarryOverShow();
            }
        }

        /// <summary>Whether any section shrinks pins or hides names at some zoom (then Cartur's own shrinking and name
        /// cut-off would do it a second time).</summary>
        internal static bool AnyShrinkOrNames()
        {
            foreach (RuleSection section in AllSections())
            {
                if (section.LargeShrinkFrom < 100 || section.LargeNamesPast < 100 || section.SmallShrinkFrom < 100)
                {
                    return true;   // a null (following) value compares false, so only values set count
                }
            }
            return false;
        }

        private static IEnumerable<RuleSection> AllSections()
        {
            yield return All;
            foreach (RuleSection section in categories.Values)
            {
                yield return section;
            }
            foreach (RuleSection section in groups.Values)
            {
                yield return section;
            }
            yield return Moving;
            yield return HandPlaced;
            yield return Deaths;
            yield return GameMarkers;
            yield return Portals;
            yield return Others;
            foreach (KeyValuePair<string, RuleSection> each in overrides)
            {
                yield return each.Value;
            }
        }

        /// <summary>The section of one of Cartur's categories, by its name there, or null.</summary>
        internal static RuleSection ForCategory(string category) =>
            category != null && categories.TryGetValue(category, out RuleSection section) ? section : null;

        /// <summary>The section of one of Cartur's pickable groups, by its name there, or null.</summary>
        internal static RuleSection ForGroup(string group) =>
            group != null && groups.TryGetValue(group, out RuleSection section) ? section : null;

        /// <summary>
        /// Reads the override list again: binds a section for each new name (it shows in a configuration manager the
        /// next time its window opens, a fold-out after the others) and makes the list's order the order names are tried
        /// in. Every pin is sorted again.
        /// </summary>
        internal static void ApplyOverrides()
        {
            int movedLate = 0;
            overrides.Clear();
            foreach (string name in Words.ParseList(Settings.Overrides.Value))
            {
                // Named by its words as typed, in their order, whatever their case and spacing: the same name typed
                // "cloudberry" or "Cloudberry " finds its section and its values again, now and at the next start. The
                // same words in another order are another section, now as at the next start, so none is lost unseen.
                string title = Words.Title(name);
                if (!overrideSections.TryGetValue(title, out RuleSection section))
                {
                    section = new RuleSection("Override - " + title, $"the pins matching \"{title}\"",
                        $"the pin's own group or category. This section applies while \"{title}\" is in the override list (General)");
                    section.Bind(settings, bound++, 0, 0, 0, 0);
                    overrideSections[title] = section;
                    movedLate += section.MovedLate;
                }
                overrides.Add(new KeyValuePair<string, RuleSection>(Words.Signature(name), section));
            }
            if (movedLate > 0)
            {
                Module.Log.LogInfo($"Moved {movedLate} setting(s) of the override sections bound now to this version's defaults, " +
                                   "since the settings file still held the old ones. That happens once.");
            }
            Sorter.Invalidate();
        }

        /// <summary>The first override in the list whose words are those of the pin's kind ("Cloudberry"), its whole key
        /// ("Pickable:Cloudberry") or its name as the map shows it ("Cloudberries"), or null.</summary>
        internal static RuleSection OverrideFor(string kindWords, string keyWords, string nameWords)
        {
            foreach (KeyValuePair<string, RuleSection> each in overrides)
            {
                if ((kindWords.Length > 0 && each.Key == kindWords) || (keyWords.Length > 0 && each.Key == keyWords) ||
                    (nameWords.Length > 0 && each.Key == nameWords))
                {
                    return each.Value;
                }
            }
            return null;
        }
    }
}
