using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using Valheim.SettingsGui;

namespace SpareKeys
{
    /// <summary>
    /// Everything the mod reaches by reflection, found once: the game's button table and console binds, and the Controls
    /// menu's "press a key" dialog, at start; shudnal's configuration manager (F1) at the first frame, once every plugin
    /// has started. A member that isn't found turns off only what needs it (the game's binds fall back to its defaults; no
    /// console binds; no panel in the Controls menu; no panel in F1), said once in the log; the window never needs any.
    /// </summary>
    internal static class Reach
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>shudnal's configuration manager (1.1.25 read for this): its plugin id.</summary>
        internal const string ShudnalCm = "_shudnal.ConfigurationManager";

        /// <summary>BepInEx's own configuration manager.</summary>
        internal const string BepInExCm = "com.bepis.bepinex.configurationmanager";

        /// <summary>Bindrune (isimp): its settings file is isimp.Bindrune.cfg, so its plugin id.</summary>
        internal const string Bindrune = "isimp.Bindrune";

        private static FieldInfo buttons;
        private static FieldInfo binds;
        private static FieldInfo bindDialog;
        private static FieldInfo selectedKey;

        internal static void Resolve(List<string> missingOptional)
        {
            buttons = typeof(ZInput).GetField("m_buttons", AnyInstance);
            if (buttons == null || !typeof(IDictionary).IsAssignableFrom(buttons.FieldType))
            {
                buttons = null;
                missingOptional.Add("ZInput.m_buttons (the game's own keys are taken from its defaults, without your rebinds)");
            }
            binds = typeof(Terminal).GetField("m_binds", AnyStatic);
            if (binds == null || !typeof(IDictionary).IsAssignableFrom(binds.FieldType))
            {
                binds = null;
                missingOptional.Add("Terminal.m_binds (your console binds aren't counted)");
            }
            bindDialog = typeof(KeyboardMouseSettings).GetField("m_bindDialog", AnyInstance);
            selectedKey = typeof(KeyboardMouseSettings).GetField("m_selectedKey", AnyInstance);
            if (bindDialog == null || bindDialog.FieldType != typeof(GameObject) || selectedKey == null || selectedKey.FieldType != typeof(KeySetting))
            {
                bindDialog = selectedKey = null;
                missingOptional.Add("KeyboardMouseSettings.m_bindDialog or m_selectedKey (no panel beside the game's Controls menu)");
            }
        }

        internal static bool ControlsPanelPossible => bindDialog != null;

        /// <summary>The game's buttons by name, with the bind each reads now (his rebinds and Jötunn's buttons included);
        /// null when the table can't be read.</summary>
        internal static List<KeyValuePair<string, string>> GameButtons()
        {
            if (buttons == null || ZInput.instance == null || !(buttons.GetValue(ZInput.instance) is IDictionary table))
            {
                return null;
            }
            var list = new List<KeyValuePair<string, string>>();
            foreach (DictionaryEntry entry in table)
            {
                if (entry.Key is string name && entry.Value is ZInput.ButtonDef button)
                {
                    string path;
                    try
                    {
                        path = button.GetActionPath(true);
                    }
                    catch (Exception)
                    {
                        continue;   // a button without a binding: it uses no key
                    }
                    list.Add(new KeyValuePair<string, string>(name, path));
                }
            }
            return list;
        }

        /// <summary>His console binds, key by key; empty when they can't be read.</summary>
        internal static List<KeyValuePair<KeyCode, List<string>>> ConsoleBinds()
        {
            var list = new List<KeyValuePair<KeyCode, List<string>>>();
            if (binds != null && binds.GetValue(null) is IDictionary table)
            {
                foreach (DictionaryEntry entry in table)
                {
                    if (entry.Key is KeyCode key && entry.Value is List<string> commands)
                    {
                        list.Add(new KeyValuePair<KeyCode, List<string>>(key, new List<string>(commands)));
                    }
                }
            }
            return list;
        }

        /// <summary>The Controls tab's dialog "press a key" and the row being bound, while it shows.</summary>
        internal static bool Binding(KeyboardMouseSettings settings, out GameObject dialog, out string button)
        {
            dialog = null;
            button = null;
            if (bindDialog == null || settings == null)
            {
                return false;
            }
            dialog = bindDialog.GetValue(settings) as GameObject;
            button = (selectedKey.GetValue(settings) as KeySetting)?.m_keyName;
            return dialog != null && dialog.activeInHierarchy;
        }

        // ---- At the first frame: the configuration manager, Bindrune, ComfyMods' Shortcuts ----

        private static object manager;
        private static Func<bool> displaying;
        private static FieldInfo windowRect;
        private static PropertyInfo scale;
        private static Func<bool> picking;
        private static FieldInfo pickingEntry;
        private static FieldInfo editPicking;
        private static FieldInfo editWindow;
        private static PropertyInfo editOpen;
        private static FieldInfo editRect;

        // Whichever configuration manager is installed (shudnal's, else BepInEx's own), for closing its window when the
        // button in F1 opens the key map: both have a public DisplayingWindow.
        private static object closeTarget;
        private static PropertyInfo closeSwitch;

        /// <summary>Whether the F1 panel can work: shudnal's configuration manager and every member it reads were found.</summary>
        internal static bool F1PanelPossible { get; private set; }

        /// <summary>Bindrune is installed: it uses Insert too.</summary>
        internal static bool BindruneThere { get; private set; }

        /// <summary>ComfyMods' Shortcuts is installed: it moves the game's fixed keys into its own settings.</summary>
        internal static bool ShortcutsThere { get; private set; }

        internal static void AtFirstFrame()
        {
            foreach (PluginInfo plugin in Chainloader.PluginInfos.Values)
            {
                string id = plugin?.Metadata?.GUID ?? string.Empty;
                BindruneThere |= id.Equals(Bindrune, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(plugin?.Metadata?.Name, "Bindrune", StringComparison.OrdinalIgnoreCase);
                ShortcutsThere |= id.IndexOf("shortcuts", StringComparison.OrdinalIgnoreCase) >= 0 &&
                                  (id.IndexOf("redseiko", StringComparison.OrdinalIgnoreCase) >= 0 || id.IndexOf("comfy", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (BindruneThere)
            {
                Module.Log?.LogInfo("Bindrune is installed and uses Insert too: Spare Keys leaves Insert to it. Set \"Key to open the key map\" " +
                                    "to another key to open the key map with a key of its own, or use its button in F1.");
            }
            if (ShortcutsThere)
            {
                Module.Log?.LogInfo("ComfyMods' Shortcuts is installed: the game's fixed keys it makes settings of are read from its settings.");
            }
            ResolveManager();
        }

        private static void ResolveManager()
        {
            if (!Chainloader.PluginInfos.TryGetValue(ShudnalCm, out PluginInfo cm) || cm.Instance == null)
            {
                if (Chainloader.PluginInfos.TryGetValue(BepInExCm, out PluginInfo bepinex) && bepinex.Instance != null)
                {
                    Closer(bepinex.Instance);
                    Module.Log?.LogInfo("BepInEx's own configuration manager is installed, not shudnal's: the panel beside it while picking a key " +
                                        "stays off (the key map's own key and window work as usual).");
                }
                return;
            }
            Type type = cm.Instance.GetType();
            Closer(cm.Instance);
            Assembly assembly = type.Assembly;
            Type drawer = assembly.GetType("ConfigurationManager.SettingFieldDrawer");
            Type edit = assembly.GetType("ConfigurationManager.SettingEditWindow");
            PropertyInfo shows = type.GetProperty("DisplayingWindow", AnyInstance);
            windowRect = type.GetField("currentWindowRect", AnyInstance);
            scale = type.GetProperty("ScaleFactor", AnyInstance);
            PropertyInfo waiting = drawer?.GetProperty("SettingKeyboardShortcut", AnyStatic);
            pickingEntry = drawer?.GetField("_currentKeyboardShortcutToSet", AnyStatic);
            editPicking = edit?.GetField("_currentKeyboardShortcutToSet", AnyStatic);
            editWindow = type.GetField("_configSettingWindow", AnyInstance);
            editOpen = edit?.GetProperty("IsOpen", AnyInstance);
            editRect = edit?.GetField("_windowRect", AnyInstance);
            if (editRect != null && editRect.FieldType != typeof(Rect))
            {
                editRect = null;   // the panel then goes beside the main window only
            }
            var missing = new List<string>();
            if (shows == null || shows.PropertyType != typeof(bool) || shows.GetGetMethod(true) == null)
            {
                missing.Add("DisplayingWindow");
            }
            if (windowRect == null || windowRect.FieldType != typeof(Rect))
            {
                missing.Add("currentWindowRect");
            }
            if (scale == null || scale.PropertyType != typeof(float))
            {
                missing.Add("ScaleFactor");
            }
            if (waiting == null || waiting.PropertyType != typeof(bool) || waiting.GetGetMethod(true) == null)
            {
                missing.Add("SettingFieldDrawer.SettingKeyboardShortcut");
            }
            if (missing.Count > 0)
            {
                Module.Log?.LogWarning($"The configuration manager ({cm.Metadata.Name} {cm.Metadata.Version}) has changed: {string.Join(", ", missing)} " +
                                       "not found. The panel beside it while picking a key stays off; everything else works.");
                return;
            }
            try
            {
                // Asked every frame while the mod is on: delegates made once, so asking costs nothing.
                displaying = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), cm.Instance, shows.GetGetMethod(true));
                picking = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), waiting.GetGetMethod(true));
            }
            catch (Exception error)
            {
                Module.Log?.LogWarning($"The configuration manager ({cm.Metadata.Name} {cm.Metadata.Version}) can't be asked whether it waits for a key " +
                                       $"({error.GetType().Name}). The panel beside it while picking a key stays off; everything else works.");
                return;
            }
            manager = cm.Instance;
            F1PanelPossible = true;
        }

        private static void Closer(object target)
        {
            PropertyInfo property = target.GetType().GetProperty("DisplayingWindow", BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.PropertyType == typeof(bool) && property.CanRead && property.CanWrite)
            {
                closeTarget = target;
                closeSwitch = property;
            }
        }

        /// <summary>Whether shudnal's configuration manager's window shows (for the F1 panel).</summary>
        internal static bool ManagerOpen => F1PanelPossible && displaying();

        /// <summary>Whether any configuration manager's window shows (asked only when the key is pressed).</summary>
        internal static bool AnyManagerOpen => closeTarget != null && (bool)closeSwitch.GetValue(closeTarget);

        /// <summary>Closes the configuration manager's window (when its button opens the key map): its own public switch,
        /// shudnal's or BepInEx's own.</summary>
        internal static void CloseManager()
        {
            if (AnyManagerOpen)
            {
                closeSwitch.SetValue(closeTarget, false);
            }
        }

        private static object likeFor;
        private static ConfigEntryLike like;

        /// <summary>
        /// Whether a key setting in the configuration manager's window waits for a key now: its window shows, and its list's
        /// key field (or its Edit window's, while that is open) is set. The field isn't cleared when the window closes, so
        /// the window is asked too. <paramref name="entry"/>: the setting picked for, when it can be told (read once per
        /// setting); <paramref name="inEdit"/>: the Edit window is the one waiting.
        /// </summary>
        internal static bool PickingInManager(out ConfigEntryLike entry, out bool inEdit)
        {
            entry = null;
            inEdit = false;
            if (!ManagerOpen)
            {
                likeFor = like = null;   // read afresh at the next pick: the same setting may hold a new key by then
                return false;
            }
            bool inList = picking();
            object picked = inList ? pickingEntry?.GetValue(null) : null;
            if (!inList && editPicking != null && OpenEditWindow() != null)
            {
                picked = editPicking.GetValue(null);
                inEdit = picked != null;
            }
            if (!inList && picked == null)
            {
                likeFor = like = null;
                return false;
            }
            if (like == null || !ReferenceEquals(picked, likeFor))
            {
                likeFor = picked;
                like = ConfigEntryLike.Of(picked);
            }
            entry = like;
            return true;
        }

        // The manager's Edit window, while it is open.
        private static object OpenEditWindow()
        {
            object window = editWindow?.GetValue(manager);
            return window != null && editOpen?.GetValue(window) is bool open && open ? window : null;
        }

        private static float Scale()
        {
            float factor = (float)scale.GetValue(manager);
            return factor <= 0f || float.IsNaN(factor) ? 1f : factor;
        }

        /// <summary>The configuration manager's window on the screen, in screen pixels from the top left.</summary>
        internal static Rect ManagerRect()
        {
            var rect = (Rect)windowRect.GetValue(manager);
            float factor = Scale();
            return new Rect(rect.x * factor, rect.y * factor, rect.width * factor, rect.height * factor);
        }

        /// <summary>Its Edit window on the screen, while it is open (null when closed, or its place can't be read).</summary>
        internal static Rect? EditRect()
        {
            object window = editRect != null ? OpenEditWindow() : null;
            if (window == null)
            {
                return null;
            }
            var rect = (Rect)editRect.GetValue(window);
            float factor = Scale();
            return new Rect(rect.x * factor, rect.y * factor, rect.width * factor, rect.height * factor);
        }
    }

    /// <summary>The setting a configuration manager waits for a key for: its plugin, name and value, when they can be
    /// told (its own entry type is internal to the manager).</summary>
    internal sealed class ConfigEntryLike
    {
        internal string Plugin = string.Empty;
        internal string PluginId = string.Empty;
        internal string Name = string.Empty;
        internal string Value = string.Empty;
        internal bool KeyCodeOnly;

        /// <summary>The setting itself (its section, its key, its file), when it can be told: for its when.</summary>
        internal BepInEx.Configuration.ConfigEntryBase Entry;

        internal static ConfigEntryLike Of(object picked)
        {
            var like = new ConfigEntryLike();
            if (picked == null)
            {
                return like;
            }
            try
            {
                Type type = picked.GetType();
                if (type.GetProperty("Entry")?.GetValue(picked) is BepInEx.Configuration.ConfigEntryBase entry)
                {
                    like.Entry = entry;
                    like.Name = Collector.DisplayNameOf(entry);
                    like.Value = entry.GetSerializedValue();
                    like.KeyCodeOnly = entry.SettingType == typeof(KeyCode);
                }
                else
                {
                    like.Name = type.GetProperty("DispName")?.GetValue(picked) as string ?? string.Empty;
                }
                if (type.GetProperty("PluginInfo")?.GetValue(picked) is BepInPlugin plugin)
                {
                    like.Plugin = plugin.Name;
                    like.PluginId = plugin.GUID;
                }
            }
            catch (Exception)
            {
                // Only words for the panel's title: without them the panel still shows the keys.
            }
            return like;
        }
    }
}
