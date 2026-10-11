using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using QoLMods.Kit;
using Hints = QoLMods.Kit.ConfigurationManagerAttributes;   // Jötunn has one of its own, in no namespace

namespace StoreStash
{
    /// <summary>
    /// How StoreStash's features show in a configuration manager's window (F1): all under StoreStash's own heading, Enabled
    /// alone at the top, and each key feature (Quick stack, Favorites, Personal quick stack, Personal chests, Sorting, Trash,
    /// Chest setups, Stack Overflow) behind a title row that opens and closes it: his rule of one heading per mod, sections
    /// folded inside (his look at Map View Rules, 2026-09-28; Map View Rules' fold rows are the worked example), and his
    /// words of 2026-10-09, "sub-sections in the config for each key feature of StoreStash" (card A82, point 5). Inside a
    /// feature the settings sit in small groups, each behind a row of its own, drawn indented (his first look at chest
    /// setups, card A41's second round, decision 22): a group's row is listed while the feature it's in is open, and its
    /// settings while it and every row above it are open. The settings file keeps a section per part in
    /// the single release, and the one "StoreStash" section in the bundle. Each row is a setting of its own ("…Unfolded"),
    /// which remembers whether it is open. Rows open and close through their settings' Browsable hint, which shudnal's
    /// ConfigurationManager reads again while its window is open (1.1.22). BepInEx's own reads it only as its window opens,
    /// so with that one, an older shudnal's, or none, nothing folds, and every title shows over its settings.
    /// </summary>
    internal static class Folds
    {
        private const string LiveManagerGuid = "_shudnal.ConfigurationManager";

        private static readonly System.Version LiveFrom = new System.Version(1, 1, 22);   // the game has a Version of its own

        private static readonly List<Fold> folds = new List<Fold>();
        private static bool decided;
        private static bool rebuildWanted;

        /// <summary>Asks the configuration manager to rebuild its list (Jötunn's ConfigManagerUtils.BuildSettingList, set
        /// from ManagerLink at start), so a feature just opened lists at once. Null: no asking.</summary>
        internal static Action RebuildList;

        /// <summary>Whether the features fold now. Decided at the module's first frame, once every mod has loaded; until
        /// then every feature is open.</summary>
        internal static bool Live { get; private set; }

        /// <summary>Every row added, features and groups, in the order they were added.</summary>
        internal static IReadOnlyList<Fold> All => folds;

        /// <summary>
        /// Binds a feature's title row, its setting <paramref name="key"/> in <paramref name="part"/> (false: closed), at
        /// <paramref name="order"/>, right above its settings' orders, and lists it and <paramref name="entries"/> (the
        /// feature's settings outside its groups) under StoreStash's heading as a fold-out: listed while it is open, or while
        /// rows don't fold. Returns the feature, for its groups (AddGroup).
        /// </summary>
        internal static Fold Add(ModuleSettings settings, string part, string key, string title, int order, IEnumerable<ConfigEntryBase> entries) =>
            Bind(settings, part, key, title, order, entries, parent: null, group: false);

        /// <summary>
        /// Binds a group's row, as Add does a feature's, inside <paramref name="parent"/> (a feature; null: a group of no
        /// feature, listed at the top: none is, since every key feature has its fold, card A82). The row is listed while
        /// its feature is open; its settings while it and its feature are.
        /// </summary>
        internal static Fold AddGroup(ModuleSettings settings, string part, string key, string title, int order, IEnumerable<ConfigEntryBase> entries,
                                      Fold parent) =>
            Bind(settings, part, key, title, order, entries, parent, group: true);

        private static Fold Bind(ModuleSettings settings, string part, string key, string title, int order, IEnumerable<ConfigEntryBase> entries,
                                 Fold parent, bool group)
        {
            var fold = new Fold(title, entries, parent, group);
            ConfigEntry<bool> unfolded = settings.Bind(part, key, false, title, order,
                $"Whether the settings under \"{title}\" show open in the settings window (F1), under its title row; a click on " +
                "the row opens or closes it. Only how that window shows them: the settings are the same either way.",
                null, FoldDrawer.For(fold));
            fold.Unfolded = unfolded;
            string heading = settings.SectionOf(ModuleSettings.General);
            Hints row = ModuleSettings.HintsOf(unfolded);
            if (row != null)
            {
                row.Category = heading;
                row.HideSettingName = true;
                row.HideDefaultButton = true;
                fold.RowHints = row;
            }
            foreach (ConfigEntryBase entry in fold.Entries)
            {
                Hints hints = ModuleSettings.HintsOf(entry);
                if (hints != null)
                {
                    hints.Category = heading;
                    fold.Hints.Add(hints);
                }
            }
            parent?.Groups.Add(fold);
            folds.Add(fold);
            Apply(fold);
            unfolded.SettingChanged += (sender, args) =>
            {
                Apply(fold);
                rebuildWanted |= Live && fold.Open;
            };
            return fold;
        }

        /// <summary>Puts settings bound after their row into it (a quick stack rule bound with Personal Stacker's settings:
        /// "Quick stack leaves chests others marked alone"), listed and hidden with it as the row's own.</summary>
        internal static void AddTo(Fold fold, params ConfigEntryBase[] entries)
        {
            foreach (ConfigEntryBase entry in entries)
            {
                if (entry == null || fold.Entries.Contains(entry))
                {
                    continue;
                }
                fold.Entries.Add(entry);
                Hints hints = ModuleSettings.HintsOf(entry);
                if (hints != null)
                {
                    hints.Category = fold.RowHints != null ? fold.RowHints.Category : hints.Category;
                    fold.Hints.Add(hints);
                }
            }
            Apply(fold);
        }

        /// <summary>Called every frame, outside the manager's drawing: the first time, decides whether the features fold;
        /// after a row opened, asks the manager to rebuild its list, which it shows from its next layout.</summary>
        internal static void Tick()
        {
            if (!decided)
            {
                Decide(ManagerFoldsLive());
            }
            if (rebuildWanted)
            {
                rebuildWanted = false;
                try
                {
                    RebuildList?.Invoke();
                }
                catch (Exception)
                {
                    // A manager that can't be asked still lists the feature at its own pace: stop asking.
                    RebuildList = null;
                }
            }
        }

        /// <summary>Sets whether the features fold, and lists or hides every row's settings to match.</summary>
        internal static void Decide(bool live)
        {
            decided = true;
            Live = live;
            foreach (Fold fold in folds)
            {
                Apply(fold);
            }
        }

        // Lists or hides a row's settings, and a group's row itself, as it and the rows above it are open; then its groups.
        private static void Apply(Fold fold)
        {
            if (fold.Parent != null && fold.RowHints != null)
            {
                fold.RowHints.Browsable = !Live || fold.Parent.Open;   // true, never null, to list again
            }
            bool listed = !Live || fold.Open;
            foreach (Hints hints in fold.Hints)
            {
                hints.Browsable = listed;   // true, never null, to list again: a manager keeps the last one it read
            }
            foreach (Fold group in fold.Groups)
            {
                Apply(group);
            }
        }

        private static bool ManagerFoldsLive()
        {
            if (!Chainloader.PluginInfos.TryGetValue(LiveManagerGuid, out PluginInfo info) || info == null)
            {
                return false;
            }
            System.Version version = info.Metadata?.Version;
            return version != null && version >= LiveFrom;
        }

        /// <summary>One row's fold: a feature (Add) or a group (AddGroup), its title, its row's setting, the settings it
        /// opens, and a feature's groups.</summary>
        internal sealed class Fold
        {
            internal readonly string Title;

            /// <summary>The feature a group is in; null for a feature (and for a group made with none: none is now).</summary>
            internal readonly Fold Parent;

            /// <summary>A group's row (drawn indented), not a feature's.</summary>
            internal readonly bool IsGroup;

            internal readonly List<ConfigEntryBase> Entries = new List<ConfigEntryBase>();
            internal readonly List<Hints> Hints = new List<Hints>();
            internal readonly List<Fold> Groups = new List<Fold>();
            internal ConfigEntry<bool> Unfolded;
            internal Hints RowHints;

            internal Fold(string title, IEnumerable<ConfigEntryBase> entries, Fold parent = null, bool isGroup = false)
            {
                Title = title;
                Parent = parent;
                IsGroup = isGroup;
                if (entries != null)
                {
                    Entries.AddRange(entries);
                }
            }

            /// <summary>Whether its settings show: it is open, and so is every row above it.</summary>
            internal bool Open => Unfolded != null && Unfolded.Value && (Parent == null || Parent.Open);

            /// <summary>Whether any of its settings, or its groups', isn't at its default: its row says "(changed)".</summary>
            internal bool Changed()
            {
                foreach (ConfigEntryBase entry in Entries)
                {
                    if (entry != null && !Equals(entry.BoxedValue, entry.DefaultValue))
                    {
                        return true;
                    }
                }
                foreach (Fold group in Groups)
                {
                    if (group.Changed())
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }
}
