using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using QoLMods.Kit;
using Hints = QoLMods.Kit.ConfigurationManagerAttributes;   // Jötunn has one of its own, in no namespace

namespace MapViewRules
{
    /// <summary>
    /// How the sections of rules show in a configuration manager's window (F1). The settings file keeps a section per
    /// part, but the window lists them all under the module's own heading ("Map View Rules" in the bundle, "General"
    /// alone), each behind a title row that opens and closes it: some thirty sections at the pack's top level were a mess
    /// (his look at F1, and his pick, 2026-09-28, A09). The title row is a setting of the section's own, "Unfolded", which
    /// remembers whether it is open. A section opens and closes through its settings' Browsable hint, which shudnal's
    /// ConfigurationManager reads again while its window is open (1.1.22, read in its code). BepInEx's own reads it only
    /// as its window opens, so with that one, an older shudnal's, or none, every section stays open under its title.
    /// </summary>
    internal static class Folds
    {
        private const string LiveManagerGuid = "_shudnal.ConfigurationManager";

        private static readonly System.Version LiveFrom = new System.Version(1, 1, 22);   // the game has a Version of its own

        /// <summary>The title row's order of the first section of rules. A configuration manager lists a heading's
        /// settings by Order, highest first: General's run from 1000 (Enabled) down to 70 (Settings), then each section of
        /// rules takes eleven, in the order the sections were bound: its title row, then its ten settings.</summary>
        internal const int FirstTitleOrder = 60;

        internal const int OrdersPerSection = 11;

        private static readonly List<Fold> folds = new List<Fold>();
        private static bool decided;
        private static bool rebuildWanted;

        /// <summary>Asks the configuration manager to rebuild its list (the module sets Jötunn's
        /// ConfigManagerUtils.BuildSettingList, ManagerLink). Without it, shudnal's lists a section just opened only once its
        /// round of 64 settings a frame reaches them, a few tenths of a second behind the click. Only on opening: a closed
        /// section's rows are still drawn at the next frame, where the manager reads each drawn row again. Null: no
        /// asking.</summary>
        internal static Action RebuildList;

        /// <summary>Whether the sections fold now. Decided at the module's first frame, once every mod has loaded; until
        /// then every section is open.</summary>
        internal static bool Live { get; private set; }

        /// <summary>The order of the title row of the section bound index-th (0: All pins); its ten settings take the
        /// ten orders below it.</summary>
        internal static int TitleOrder(int index) => FirstTitleOrder - OrdersPerSection * index;

        /// <summary>Lists a section's title row and its settings under the heading, as a fold-out: listed while it is open,
        /// or while sections don't fold.</summary>
        internal static void Add(ConfigEntry<bool> unfolded, IEnumerable<ConfigEntryBase> settings, string heading)
        {
            var fold = new Fold(unfolded);
            Hints title = ModuleSettings.HintsOf(unfolded);
            title.Category = heading;
            title.HideSettingName = true;
            title.HideDefaultButton = true;
            foreach (ConfigEntryBase setting in settings)
            {
                Hints hints = ModuleSettings.HintsOf(setting);
                hints.Category = heading;
                fold.SettingHints.Add(hints);
            }
            folds.Add(fold);
            Apply(fold);
            unfolded.SettingChanged += (sender, args) =>
            {
                Apply(fold);
                rebuildWanted |= Live && fold.Unfolded.Value;
            };
        }

        /// <summary>Called every frame, outside the manager's drawing: the first time, decides whether the sections fold;
        /// after a section opened, asks the manager to rebuild its list, which it shows from its next layout.</summary>
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
                    // A manager that can't be asked still lists the section at its own pace: stop asking, keep the rules.
                    RebuildList = null;
                }
            }
        }

        /// <summary>Sets whether the sections fold, and lists or hides every section's settings to match.</summary>
        internal static void Decide(bool live)
        {
            decided = true;
            Live = live;
            foreach (Fold fold in folds)
            {
                Apply(fold);
            }
        }

        private static void Apply(Fold fold)
        {
            bool listed = !Live || fold.Unfolded.Value;
            foreach (Hints hints in fold.SettingHints)
            {
                hints.Browsable = listed;   // true, never null, to list again: a manager keeps the last one it read
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

        private sealed class Fold
        {
            internal readonly ConfigEntry<bool> Unfolded;
            internal readonly List<Hints> SettingHints = new List<Hints>();

            internal Fold(ConfigEntry<bool> unfolded)
            {
                Unfolded = unfolded;
            }
        }
    }
}
