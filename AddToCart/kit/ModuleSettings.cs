// The QoL Mods kit. The one copy to edit is Games\Valheim\QoLMods\kit\; every mod folder carries a copy of it in its
// own kit\ folder, so each mod still builds and publishes on its own. After changing it here, run
// QoLMods\tools\sync-kit.ps1. The rules for a module are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;

namespace QoLMods.Kit
{
    /// <summary>
    /// Binds a module's settings where its host keeps them. A module names each setting's part ("General", "Map" …)
    /// and key; the host decides the section. In a single release, each part is a section of the mod's own file, as
    /// it always was. In the bundle, the module's settings share one section named after it (or "&lt;Name&gt; - &lt;part&gt;").
    /// Keys never change, so the bundle can take a single release's settings over once.
    /// </summary>
    internal sealed class ModuleSettings
    {
        /// <summary>The part that holds the Enabled switch, and a module's general settings.</summary>
        internal const string General = "General";

        /// <summary>The display order of the Enabled switch: above every setting of the module. A module's own orders
        /// stay below this and are unique across the whole module, since its parts may share one section.</summary>
        internal const int EnabledOrder = 1000;

        private readonly ConfigFile config;
        private readonly Func<string, string> sectionOf;
        private readonly List<Bound> bound = new List<Bound>();

        internal ModuleSettings(ConfigFile config, Func<string, string> sectionOf)
        {
            this.config = config;
            this.sectionOf = sectionOf;
            IsNewToFile = !FileHasSection(config.ConfigFilePath, sectionOf(General));
        }

        internal ConfigEntry<bool> Enabled { get; private set; }

        /// <summary>True when the settings file had no section for this module before this start: the module is new to
        /// this file. Measured before anything is bound.</summary>
        internal bool IsNewToFile { get; }

        internal ConfigEntry<T> Bind<T>(string part, string key, T defaultValue, string displayName, int order, string description,
                                        AcceptableValueBase acceptableValues = null)
        {
            if (order >= EnabledOrder)
            {
                throw new ArgumentOutOfRangeException(nameof(order), $"A setting's order must stay below {EnabledOrder}, the Enabled switch's.");
            }
            return BindAny(part, key, defaultValue, displayName, order, description, acceptableValues);
        }

        internal void BindEnabled(string description)
        {
            Enabled = BindAny(General, "Enabled", true, "Enabled", EnabledOrder, description, null);
        }

        /// <summary>
        /// Copies the values this module's settings have in another settings file, such as its single release's, into
        /// the settings bound here, matched by part (the old file's section) and key. The other file is only read.
        /// Returns how many values were copied. Keys in the other file that nothing here binds are ignored.
        /// </summary>
        internal int CopyFrom(string otherFile)
        {
            Dictionary<string, string> values = ReadValues(otherFile);
            int copied = 0;
            foreach (Bound setting in bound)
            {
                if (values.TryGetValue(setting.Part + "\n" + setting.Key, out string value))
                {
                    setting.Entry.SetSerializedValue(value);
                    copied++;
                }
            }
            return copied;
        }

        private ConfigEntry<T> BindAny<T>(string part, string key, T defaultValue, string displayName, int order, string description,
                                          AcceptableValueBase acceptableValues)
        {
            var hints = new ConfigurationManagerAttributes { DispName = displayName, Order = order };
            ConfigEntry<T> entry = config.Bind(sectionOf(part), key, defaultValue, new ConfigDescription(description, acceptableValues, hints));
            bound.Add(new Bound(part, key, entry));
            return entry;
        }

        private static bool FileHasSection(string path, string section)
        {
            if (!File.Exists(path))
            {
                return false;
            }
            string heading = "[" + section + "]";
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Trim() == heading)
                {
                    return true;
                }
            }
            return false;
        }

        // A BepInEx settings file: "[Section]" lines, "#" comment lines, and "Key = Value" lines. Keyed "section\nkey".
        private static Dictionary<string, string> ReadValues(string path)
        {
            var values = new Dictionary<string, string>();
            string section = string.Empty;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                int equals = line.IndexOf('=');
                if (equals > 0)
                {
                    values[section + "\n" + line.Substring(0, equals).Trim()] = line.Substring(equals + 1).Trim();
                }
            }
            return values;
        }

        private readonly struct Bound
        {
            internal readonly string Part;
            internal readonly string Key;
            internal readonly ConfigEntryBase Entry;

            internal Bound(string part, string key, ConfigEntryBase entry)
            {
                Part = part;
                Key = key;
                Entry = entry;
            }
        }
    }
}
