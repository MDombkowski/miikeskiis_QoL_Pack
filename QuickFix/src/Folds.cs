using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using QoLMods.Kit;
using Hints = QoLMods.Kit.ConfigurationManagerAttributes;   // Jötunn has one of its own, in no namespace (LESSONS_LEARNED)

namespace QuickFix
{
    /// <summary>
    /// How the parts of the settings show in a configuration manager's window (F1): all under the module's one heading
    /// ("Quick Fix" in the bundle, "General" alone), each part behind a title row that opens and closes it, as Map
    /// View Rules' sections do (his pick of 2026-09-28: one heading per mod; Topper-Upper's Folds, unchanged but for its
    /// namespace). A part opens and
    /// closes through its settings' Browsable hint, which shudnal's ConfigurationManager reads again while its window is
    /// open (1.1.22 or later). With BepInEx's own manager, an older shudnal's, or none, every part stays open under its
    /// title.
    /// </summary>
    internal static class Folds
    {
        private const string LiveManagerGuid = "_shudnal.ConfigurationManager";

        private static readonly System.Version LiveFrom = new System.Version(1, 1, 22);   // the game has a Version of its own

        private static readonly List<Fold> folds = new List<Fold>();
        private static bool decided;
        private static bool rebuildWanted;

        /// <summary>Asks the configuration manager to rebuild its list (Jötunn's ConfigManagerUtils.BuildSettingList,
        /// ManagerLink), so a part just opened lists at once. Null: no asking.</summary>
        internal static Action RebuildList;

        /// <summary>Whether the parts fold now. Decided at the module's first frame, once every mod has loaded.</summary>
        internal static bool Live { get; private set; }

        /// <summary>Lists a part's title row and its settings under the heading, as a fold-out.</summary>
        internal static void Add(ConfigEntry<bool> unfolded, IEnumerable<ConfigEntryBase> settings, string heading)
        {
            var fold = new Fold(unfolded);
            Hints title = ModuleSettings.HintsOf(unfolded);
            if (title != null)
            {
                title.Category = heading;
                title.HideSettingName = true;
                title.HideDefaultButton = true;
            }
            foreach (ConfigEntryBase setting in settings)
            {
                Hints hints = ModuleSettings.HintsOf(setting);
                if (hints != null)
                {
                    hints.Category = heading;
                    fold.SettingHints.Add(hints);
                }
            }
            folds.Add(fold);
            Apply(fold);
            unfolded.SettingChanged += (sender, args) =>
            {
                Apply(fold);
                rebuildWanted |= Live && fold.Unfolded.Value;
            };
        }

        /// <summary>Called every frame, outside the manager's drawing: the first time, decides whether the parts fold;
        /// after a part opened, asks the manager to rebuild its list.</summary>
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
                    // A manager that can't be asked still lists the part at its own pace: stop asking.
                    RebuildList = null;
                }
            }
        }

        private static void Decide(bool live)
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
