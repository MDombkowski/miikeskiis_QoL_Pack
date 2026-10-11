using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>
    /// Reads every user of every key from the running game, read only: the game's buttons with his rebinds, its fixed
    /// keys, his console binds, every loaded mod's key settings (and its Jötunn buttons with no setting behind them), the
    /// pack's own window keys, and what the code scan found. The map is worked out again only when something it reads
    /// changes: a setting of any mod (each settings file's own change event), a rebind in the game's Controls, a console
    /// bind, debug mode; or when the window or a panel opens. Nothing heavy runs per frame.
    /// </summary>
    internal static class Collector
    {
        private static KeyTable table;
        private static bool dirty = true;
        private static bool debugModeSeen;
        private static float nextLook;
        private static string bindsSeen;
        private static bool rebindSeen;
        private static readonly HashSet<ConfigFile> watched = new HashSet<ConfigFile>();
        private static readonly HashSet<string> loggedUnreadable = new HashSet<string>();

        /// <summary>The map as it stands, worked out again first if something it reads has changed (at most twice a second
        /// for changes of settings, which another mod may make in a burst; at once when the window or a panel opens).</summary>
        internal static KeyTable Table
        {
            get
            {
                bool debugOn = DebugModeOn();
                if (table == null || forced || debugOn != debugModeSeen || (dirty && Time.unscaledTime - built >= 0.5f))
                {
                    dirty = forced = false;
                    debugModeSeen = debugOn;
                    built = Time.unscaledTime;
                    Options options = Settings.ReadOptions(debugOn);
                    List<Use> uses = ReadAll();
                    // A change of some other setting (a window's place, a colour) changes no use: the map, and every view
                    // of it, stays as it is.
                    string seen = Signature(uses, options);
                    if (table == null || seen != tableSignature)
                    {
                        tableSignature = seen;
                        table = new KeyTable(uses, options);
                        Version++;
                    }
                }
                return table;
            }
        }

        private static bool forced;
        private static float built;
        private static string tableSignature;

        // Everything a use and the options tell the map, as one line.
        private static string Signature(List<Use> uses, Options options)
        {
            var text = new StringBuilder();
            text.Append((int)options.Plain).Append((int)options.Code).Append(options.MenusTypingWindows).Append((int)options.Debug)
                .Append(options.DebugModeOn).Append(options.ConsoleBinds).Append(options.SwitchedOffModules).Append(options.Gamepad)
                .Append((int)options.Shared).Append((int)options.Unknown).Append('\n');
            foreach (Use use in uses)
            {
                text.Append(use.OwnerId).Append('|').Append(use.Owner).Append('|').Append(use.What).Append('|').Append(use.Section).Append('|')
                    .Append(use.Detail).Append('|').Append(use.Main).Append('|').Append(string.Join(",", use.Mods)).Append('|')
                    .Append(use.Place.Signature).Append('|').Append(use.When).Append('|').Append(use.Known).Append((int)use.Kind)
                    .Append(use.Maybe).Append(use.Held).Append(use.Loose).Append(use.OverGame).Append(use.DebugMode).Append(use.SwitchedOff).Append('\n');
            }
            return text.ToString();
        }

        /// <summary>Goes up each time the map is worked out again, so a view redraws only then.</summary>
        internal static int Version { get; private set; }

        /// <summary>Read everything again at the next look (the window or a panel opening).</summary>
        internal static void MarkDirty() => forced = true;

        /// <summary>
        /// Every frame while the window or a panel shows: once a second, whether the game's binds or his console binds
        /// changed, and whether a rebind in the Controls menu just finished (changes of settings arrive by their own event).
        /// </summary>
        internal static void Watch()
        {
            bool rebinding = ZInput.s_IsRebindActive;
            if (rebindSeen && !rebinding)
            {
                dirty = true;
            }
            rebindSeen = rebinding;
            if (Time.unscaledTime < nextLook)
            {
                return;
            }
            nextLook = Time.unscaledTime + 1f;
            string binds = BindsNow();
            if (binds != bindsSeen)
            {
                bindsSeen = binds;
                dirty = true;
            }
        }

        // The game's binds and his console binds as one line, to tell when they change.
        private static string BindsNow()
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, string> button in Reach.GameButtons() ?? new List<KeyValuePair<string, string>>())
            {
                text.Append(button.Key).Append('=').Append(button.Value).Append(';');
            }
            foreach (KeyValuePair<KeyCode, List<string>> bind in Reach.ConsoleBinds())
            {
                text.Append((int)bind.Key).Append(':').Append(string.Join("|", bind.Value)).Append(';');
            }
            return text.ToString();
        }

        private static bool DebugModeOn()
        {
            try
            {
                return Player.m_debugMode && global::Console.instance != null && global::Console.instance.IsCheatsEnabled();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The module's undo step: stop listening to settings files and drop the map.</summary>
        internal static void Forget()
        {
            foreach (ConfigFile config in watched)
            {
                config.SettingChanged -= SettingChanged;
                config.ConfigReloaded -= Reloaded;
            }
            watched.Clear();
            table = null;
            dirty = true;
        }

        private static void SettingChanged(object sender, SettingChangedEventArgs args) => dirty = true;

        private static void Reloaded(object sender, EventArgs args) => dirty = true;

        /// <summary>A setting's name as a configuration manager shows it (its DispName hint, any mod's own copy of the hint
        /// class), else its key.</summary>
        internal static string DisplayNameOf(ConfigEntryBase entry)
        {
            object[] tags = entry?.Description?.Tags;
            if (tags != null)
            {
                foreach (object tag in tags)
                {
                    FieldInfo field = tag?.GetType().GetField("DispName", BindingFlags.Instance | BindingFlags.Public);
                    if (field != null && field.GetValue(tag) is string name && name.Length > 0)
                    {
                        return name;
                    }
                }
            }
            return entry?.Definition.Key ?? string.Empty;
        }

        // ---- Reading ----

        /// <summary>The whens of other mods' keys: Whens' list of the ones Spare Keys knows, then his own in F1, so a line
        /// of his wins. Read again with everything else.</summary>
        internal static List<Whens.Line> WhenLines { get; private set; } = new List<Whens.Line>();

        private static string whensSeen;

        private static List<Whens.Line> ReadWhens()
        {
            string his = Settings.WhensList.Value ?? string.Empty;
            var problems = new List<string>();
            var lines = Whens.ReadList(Whens.OtherMods);
            lines.AddRange(Whens.ReadList(his, problems));
            if (his != whensSeen)
            {
                whensSeen = his;
                foreach (string problem in problems)
                {
                    Module.Log?.LogWarning($"\"Whens for other mods' keys\": {problem}.");
                }
            }
            return lines;
        }

        private static List<Use> ReadAll()
        {
            var uses = new List<Use>();
            WhenLines = ReadWhens();
            var packModules = new HashSet<string>(StringComparer.Ordinal);
            List<Use> settingUses = ReadSettings(packModules);
            uses.AddRange(settingUses);

            // The game's buttons, live (with his rebinds and Jötunn's buttons), or its defaults if the table can't be read.
            List<KeyValuePair<string, string>> buttons = Reach.GameButtons() ?? GameKeys.Defaults().ToList();
            foreach (KeyValuePair<string, string> button in buttons)
            {
                if (Reach.ShortcutsThere && MovedByShortcuts(button.Key))
                {
                    continue;
                }
                Use use = GameKeys.FromButton(button.Key, button.Value) ??
                          ModKeys.FromJotunnButton(button.Key, button.Value, PluginName, settingUses);
                if (use != null)
                {
                    uses.Add(use);
                }
            }
            uses.AddRange(GameKeys.FixedKeys(Reach.ShortcutsThere));
            foreach (KeyValuePair<KeyCode, List<string>> bind in Reach.ConsoleBinds())
            {
                Use use = ModKeys.FromConsoleBind(bind.Key, bind.Value);
                if (use != null)
                {
                    uses.Add(use);
                }
            }
            uses.AddRange(ModKeys.PackFixed(packModules.Contains));
            uses.AddRange(CodeScan.Uses());
            if (KeyMapWindow.LeftToBindrune)
            {
                // Its own key, left to Bindrune, isn't a use of it.
                uses.RemoveAll(use => use.OwnerId == "pack:" + Module.Name && use.Detail == "OpenKey");
            }
            return uses;
        }

        // ComfyMods' Shortcuts makes these buttons' keys settings of its own (its hotbar and console keys).
        private static bool MovedByShortcuts(string button) =>
            button == "Console" || (button.StartsWith("Hotbar", StringComparison.Ordinal) && button.Length <= 10 && button != "HotbarUse");

        private static string PluginName(string id) =>
            Chainloader.PluginInfos.TryGetValue(id, out PluginInfo plugin) ? plugin.Metadata.Name : null;

        private static List<Use> ReadSettings(HashSet<string> packModules)
        {
            var uses = new List<Use>();
            var seen = new HashSet<ConfigFile>();
            PluginInfo bundle = Chainloader.PluginInfos.TryGetValue(ModKeys.BundleId, out PluginInfo found) && found.Instance != null ? found : null;
            var bundleSections = new HashSet<string>(StringComparer.Ordinal);
            if (bundle != null)
            {
                foreach (ConfigDefinition definition in bundle.Instance.Config.Keys)
                {
                    bundleSections.Add(definition.Section);
                }
            }
            foreach (PluginInfo plugin in Chainloader.PluginInfos.Values.ToList())
            {
                ConfigFile config = plugin?.Instance != null ? plugin.Instance.Config : null;
                if (config == null || !seen.Add(config))
                {
                    continue;
                }
                if (watched.Add(config))
                {
                    config.SettingChanged += SettingChanged;
                    config.ConfigReloaded += Reloaded;
                }
                string id = plugin.Metadata.GUID;
                string name = plugin.Metadata.Name;
                bool isBundle = string.Equals(id, ModKeys.BundleId, StringComparison.OrdinalIgnoreCase);
                if (!isBundle && ModKeys.PackModuleOf(id, name, null) != null && bundleSections.Contains(name))
                {
                    continue;   // a module's single release beside the bundle that runs it: it stands aside, its keys do nothing
                }
                foreach (ConfigDefinition definition in config.Keys.ToList())
                {
                    ConfigEntryBase entry = config[definition];
                    string type = entry.SettingType == typeof(KeyboardShortcut) ? "KeyboardShortcut"
                        : entry.SettingType == typeof(KeyCode) ? "KeyCode"
                        : entry.SettingType == typeof(string) ? "String"
                        : null;
                    if (type == null || (type == "String" && !ModKeys.LooksLikeKeyName(definition.Key)))
                    {
                        continue;   // a text setting is read only when it's named like a key
                    }
                    string module = ModKeys.PackModuleOf(id, name, definition.Section);
                    if (module != null)
                    {
                        packModules.Add(module);
                    }
                    var record = new SettingRecord
                    {
                        PluginId = id,
                        PluginName = name,
                        Section = definition.Section,
                        Key = definition.Key,
                        DisplayName = DisplayNameOf(entry),
                        Type = type,
                        Value = entry.GetSerializedValue(),
                        PackModule = module,
                        ModuleOn = module == null || ModuleOn(config, isBundle ? module : ModuleSettingsGeneral),
                        Sibling = key => Value(config, definition.Section, key),
                    };
                    Use use = ModKeys.FromSetting(record, out string unreadable, WhenLines);
                    if (unreadable != null && loggedUnreadable.Add(id + "\n" + definition))
                    {
                        Module.Log?.LogInfo($"{name}'s setting \"{record.DisplayName}\" holds {unreadable}, a key the game can't read: left off the map.");
                    }
                    if (use != null)
                    {
                        uses.Add(use);
                    }
                }
            }
            return uses;
        }

        /// <summary>The setting a key is being picked for in F1, as a use without its key (its when, how it reads its key),
        /// for judging every key against it; null when it can't be told (the panel then shows the plain map).</summary>
        internal static Use SelfFor(ConfigEntryLike like)
        {
            ConfigEntryBase entry = like?.Entry;
            ConfigFile config = entry?.ConfigFile;
            if (config == null)
            {
                return null;
            }
            string type = entry.SettingType == typeof(KeyboardShortcut) ? "KeyboardShortcut"
                : entry.SettingType == typeof(KeyCode) ? "KeyCode"
                : entry.SettingType == typeof(string) ? "String"
                : null;
            if (type == null)
            {
                return null;
            }
            ConfigDefinition definition = entry.Definition;
            bool isBundle = string.Equals(like.PluginId, ModKeys.BundleId, StringComparison.OrdinalIgnoreCase);
            string module = ModKeys.PackModuleOf(like.PluginId, like.Plugin, definition.Section);
            var record = new SettingRecord
            {
                PluginId = like.PluginId,
                PluginName = like.Plugin,
                Section = definition.Section,
                Key = definition.Key,
                DisplayName = DisplayNameOf(entry),
                Type = type,
                Value = entry.GetSerializedValue(),
                PackModule = module,
                ModuleOn = module == null || ModuleOn(config, isBundle ? module : ModuleSettingsGeneral),
                Sibling = key => Value(config, definition.Section, key),
            };
            return ModKeys.Template(record, WhenLines);
        }

        private const string ModuleSettingsGeneral = "General";

        private static bool ModuleOn(ConfigFile config, string section) => Value(config, section, "Enabled") != "false";

        private static string Value(ConfigFile config, string section, string key)
        {
            var definition = new ConfigDefinition(section, key);
            return config.ContainsKey(definition) ? config[definition].GetSerializedValue() : null;
        }

        // ---- The log ----

        private static bool clashesLogged;

        /// <summary>"Log clashes at start": once, at the first frame the game's input is up.</summary>
        internal static void LogClashesOnce()
        {
            if (clashesLogged || ZInput.instance == null)
            {
                return;
            }
            clashesLogged = true;
            if (!Settings.LogClashes.Value)
            {
                return;
            }
            List<KeyState> clashes = Table.Clashes();
            if (clashes.Count == 0)
            {
                Module.Log?.LogInfo("No clashes: no two things use the same key at once, as far as can be seen.");
                return;
            }
            var text = new StringBuilder($"{clashes.Count} {(clashes.Count == 1 ? "clash" : "clashes")}, as far as can be seen:");
            foreach (KeyState clash in clashes)
            {
                text.Append($"\n  {Combo.Describe(clash.Key, clash.Layer)}{(clash.Status == Status.MaybeClash ? " (maybe)" : string.Empty)}: " +
                            $"{Browse.Users(clash, 10)}. {Browse.Why(clash)}");
            }
            Module.Log?.LogInfo(text.ToString());
        }
    }
}
