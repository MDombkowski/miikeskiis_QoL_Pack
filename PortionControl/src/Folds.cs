using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;
using Hints = QoLMods.Kit.ConfigurationManagerAttributes;   // Jötunn has one of its own, in no namespace

namespace PortionControl
{
    /// <summary>
    /// How the settings show in a configuration manager's window (F1): under Portion Control's one heading, in six fold
    /// rows (Split window, its Max weight, right-click, the chest window, amounts by kind, keys and other mods), each a
    /// title row that opens and closes its settings, as Map View Rules' sections and StoreStash's parts fold (his look at
    /// F1, 2026-09-28: one heading per mod, sections folded inside it). The title row is a setting of its own that
    /// remembers whether it is open. A fold opens and closes through its settings' Browsable hint, which shudnal's
    /// ConfigurationManager reads again while its window is open (1.1.22 and later); with BepInEx's own, an older
    /// shudnal's, or none, every fold stays open under its title. Written for Portion Control after Map View Rules'
    /// Folds and FoldDrawer (the pack's own code).
    /// </summary>
    internal static class Folds
    {
        private const string LiveManagerGuid = "_shudnal.ConfigurationManager";

        private static readonly System.Version LiveFrom = new System.Version(1, 1, 22);   // the game has a Version of its own

        private static readonly List<Fold> folds = new List<Fold>();
        private static bool decided;
        private static bool rebuildWanted;
        private static Action rebuildList;
        private static bool lookedForRebuild;

        /// <summary>Whether the folds fold now. Decided at the module's first frame, once every mod has loaded; until
        /// then every fold is open.</summary>
        internal static bool Live { get; private set; }

        /// <summary>Binds a fold's title row: a setting of its own ("open or not"), drawn as "[+] Title".</summary>
        internal static ConfigEntry<bool> TitleRow(ModuleSettings settings, string key, string title, int order)
        {
            var fold = new Fold(title);
            ConfigEntry<bool> row = settings.Bind(ModuleSettings.General, key, false, title, order,
                "Opens and closes the settings of \"" + title + "\" in a configuration manager's window (F1). Changes nothing " +
                "in the game.", null, entry => Draw(entry, fold));
            fold.Unfolded = row;
            folds.Add(fold);
            return row;
        }

        /// <summary>Lists a fold's settings under the heading: listed while it is open, or while folds don't fold.</summary>
        internal static void Add(ConfigEntry<bool> row, string heading, params ConfigEntryBase[] settings)
        {
            Fold fold = folds.Find(each => each.Unfolded == row);
            if (fold == null)
            {
                return;   // not a title row of TitleRow's
            }
            Hints title = ModuleSettings.HintsOf(row);
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
                fold.Settings.Add(setting);
            }
            Apply(fold);
            row.SettingChanged += (sender, args) =>
            {
                Apply(fold);
                rebuildWanted |= Live && fold.Unfolded.Value;
            };
        }

        /// <summary>Every frame, outside the manager's drawing: the first time, decides whether the folds fold; after a
        /// fold opened, asks the manager to rebuild its list, which it shows from its next layout.</summary>
        internal static void Tick()
        {
            if (!decided)
            {
                Decide(ManagerFoldsLive());
            }
            if (rebuildWanted)
            {
                rebuildWanted = false;
                if (!lookedForRebuild)
                {
                    lookedForRebuild = true;
                    rebuildList = ManagerLink.Rebuild();
                }
                try
                {
                    rebuildList?.Invoke();
                }
                catch (Exception)
                {
                    // A manager that can't be asked still lists the fold at its own pace: stop asking.
                    rebuildList = null;
                }
            }
        }

        /// <summary>After an error: every setting listed again, as a manager shows them without folds.</summary>
        internal static void OpenAll()
        {
            Live = false;
            foreach (Fold fold in folds)
            {
                Apply(fold);
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

        // ---- The title row, drawn with the window's own immediate-mode GUI: "[+] Chest window", "[-]" when open, and
        // "(changed)" while any of its settings isn't at its default. Where folds don't fold, the title alone, over its
        // settings. Plain characters, as the window's own headings use: its font may lack arrows.

        private static GUIStyle row;
        private static GUIStyle rowFrom;
        private static GUIStyle titleStyle;
        private static GUIStyle titleFrom;
        private static bool lookedForColours;
        private static FieldInfo changedColour;
        private static FieldInfo defaultColour;

        private static void Draw(ConfigEntryBase entry, Fold fold)
        {
            bool changed = fold.Changed();
            string mark = changed ? "   (changed)" : string.Empty;
            Color before = GUI.contentColor;
            // shudnal's colours a drawn row by its own value, so an open fold would show as changed: by its settings instead.
            if (ValueColour(changed) is Color colour)
            {
                GUI.contentColor = colour;
            }
            try
            {
                if (!Live)
                {
                    GUILayout.Label(fold.Title + mark, TitleStyle(), GUILayout.ExpandWidth(true));
                    return;
                }
                bool open = entry.BoxedValue is bool unfolded && unfolded;
                if (GUILayout.Button((open ? "[-] " : "[+] ") + fold.Title + mark, RowStyle(), GUILayout.ExpandWidth(true)))
                {
                    entry.BoxedValue = !open;
                }
            }
            finally
            {
                GUI.contentColor = before;
            }
        }

        // The colours shudnal's ConfigurationManager gives a value at its default and a changed one (its public
        // _fontColorValueDefault and _fontColorValueChanged); null with another manager, which leaves the colour as it is.
        private static Color? ValueColour(bool changed)
        {
            if (!lookedForColours)
            {
                lookedForColours = true;
                Type manager = ManagerLink.Plugin()?.GetType();
                changedColour = manager?.GetField("_fontColorValueChanged", BindingFlags.Public | BindingFlags.Static);
                defaultColour = manager?.GetField("_fontColorValueDefault", BindingFlags.Public | BindingFlags.Static);
            }
            return (changed ? changedColour : defaultColour)?.GetValue(null) is ConfigEntry<Color> colour ? colour.Value : (Color?)null;
        }

        // Made from the window's own skin, and made again if the window changes its skin.
        private static GUIStyle RowStyle()
        {
            if (row == null || rowFrom != GUI.skin.button)
            {
                rowFrom = GUI.skin.button;
                row = new GUIStyle(rowFrom) { alignment = TextAnchor.MiddleLeft };
            }
            row.fontSize = rowFrom.fontSize;   // the window sets its font size for each call
            return row;
        }

        private static GUIStyle TitleStyle()
        {
            if (titleStyle == null || titleFrom != GUI.skin.label)
            {
                titleFrom = GUI.skin.label;
                titleStyle = new GUIStyle(titleFrom) { fontStyle = FontStyle.Bold };
            }
            titleStyle.fontSize = titleFrom.fontSize;
            return titleStyle;
        }

        private sealed class Fold
        {
            internal readonly string Title;
            internal ConfigEntry<bool> Unfolded;
            internal readonly List<Hints> SettingHints = new List<Hints>();
            internal readonly List<ConfigEntryBase> Settings = new List<ConfigEntryBase>();

            internal Fold(string title)
            {
                Title = title;
            }

            internal bool Changed()
            {
                foreach (ConfigEntryBase setting in Settings)
                {
                    if (!Equals(setting.BoxedValue, setting.DefaultValue))
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    /// <summary>
    /// The configuration manager, through Jötunn's ConfigManagerUtils (Jötunn 2.21 added it, 2.23 taught it shudnal's).
    /// Reached only through these small methods, each compiled on its own and its failure caught, so with an older Jötunn
    /// only the folds' asking and the title rows' colours are lost, never the module (as Map View Rules reaches it).
    /// </summary>
    internal static class ManagerLink
    {
        private static bool broken;

        /// <summary>What asks the manager to rebuild its list, or null where Jötunn can't.</summary>
        internal static Action Rebuild()
        {
            try
            {
                return RebuildFromJotunn();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The manager's plugin, or null without one (or without a Jötunn that finds it).</summary>
        internal static BaseUnityPlugin Plugin()
        {
            if (broken)
            {
                return null;
            }
            try
            {
                return PluginFromJotunn();
            }
            catch (Exception)
            {
                broken = true;
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Action RebuildFromJotunn() => Jotunn.Utils.ConfigManagerUtils.BuildSettingList;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static BaseUnityPlugin PluginFromJotunn() => Jotunn.Utils.ConfigManagerUtils.Plugin;
    }
}
