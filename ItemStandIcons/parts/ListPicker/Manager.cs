// The QoL Mods list picker, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ListPicker\. Every mod that uses
// it carries a copy in its own parts\ListPicker\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;

namespace QoLMods.ListPicker
{
    /// <summary>
    /// The configuration manager whose window (F1) the picker opens over, reached by reflection through its public members
    /// only (shudnal's ConfigurationManager, 1.1.25 read by decompiling; BepInEx's own has the same two): whether its window
    /// shows (DisplayingWindow), its hotkey check (OverrideHotkey: while the picker is open, Escape closes the picker, not
    /// the settings window under it, and F1 does nothing), and the scale it draws at (shudnal's ScaleFactor), so the picker's text is the
    /// same size as the window's. Each lookup is made once; a member that isn't there is simply not used.
    /// </summary>
    internal static class Manager
    {
        private static readonly string[] Guids = { "_shudnal.ConfigurationManager", "com.bepis.bepinex.configurationmanager" };

        private static bool looked;
        private static BaseUnityPlugin plugin;
        private static PropertyInfo displaying;
        private static FieldInfo overrideHotkey;
        private static PropertyInfo scale;

        private static void Look()
        {
            if (looked)
            {
                return;
            }
            looked = true;
            try
            {
                foreach (string guid in Guids)
                {
                    if (Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo info) && info?.Instance != null)
                    {
                        plugin = info.Instance;
                        break;
                    }
                }
                if (plugin is null)
                {
                    return;
                }
                Type type = plugin.GetType();
                displaying = type.GetProperty("DisplayingWindow", BindingFlags.Public | BindingFlags.Instance);
                overrideHotkey = type.GetField("OverrideHotkey", BindingFlags.Public | BindingFlags.Instance);
                scale = type.GetProperty("ScaleFactor", BindingFlags.Public | BindingFlags.Instance);
                if (displaying != null && displaying.PropertyType != typeof(bool))
                {
                    displaying = null;
                }
                if (overrideHotkey != null && overrideHotkey.FieldType != typeof(bool))
                {
                    overrideHotkey = null;
                }
                if (scale != null && scale.PropertyType != typeof(float))
                {
                    scale = null;
                }
            }
            catch (Exception)
            {
                plugin = null;
            }
        }

        /// <summary>Whether the settings window shows; true when that can't be told (the picker then stays until it is
        /// closed itself).</summary>
        internal static bool WindowShows()
        {
            Look();
            if (plugin is null || displaying == null)
            {
                return true;
            }
            try
            {
                return (bool)displaying.GetValue(plugin, null);
            }
            catch (Exception)
            {
                displaying = null;
                return true;
            }
        }

        /// <summary>The scale the settings window draws at (1 where it has none to say).</summary>
        internal static float Scale()
        {
            Look();
            if (plugin is null || scale == null)
            {
                return 1f;
            }
            try
            {
                float value = (float)scale.GetValue(plugin, null);
                return value > 0.25f && value < 8f ? value : 1f;
            }
            catch (Exception)
            {
                scale = null;
                return 1f;
            }
        }

        // What OverrideHotkey was before the picker set it, and whether the picker holds it now.
        private static bool held;
        private static bool before;

        /// <summary>
        /// Holds the settings window's own keys while the picker is open (true), so its Escape and F1 don't close it under
        /// the picker; gives them back as they were (false). Safe to call any number of times either way.
        /// </summary>
        internal static void HoldKeys(bool hold)
        {
            Look();
            if (plugin is null || overrideHotkey == null || hold == held)
            {
                return;
            }
            try
            {
                if (hold)
                {
                    before = (bool)overrideHotkey.GetValue(plugin);
                    overrideHotkey.SetValue(plugin, true);
                    held = true;
                }
                else
                {
                    held = false;
                    overrideHotkey.SetValue(plugin, before);
                }
            }
            catch (Exception)
            {
                held = false;
                overrideHotkey = null;
            }
        }
    }
}
