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

        /// <summary>The key of the hidden setting, in General, that remembers the last moved default a file has had
        /// (MovedDefault).</summary>
        internal const string DefaultsMovedKey = "DefaultsMoved";

        private readonly ConfigFile config;
        private readonly Func<string, string> sectionOf;
        private readonly List<Bound> bound = new List<Bound>();
        private readonly List<Move> moves = new List<Move>();

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

        /// <param name="customDrawer">Draws the value in a configuration manager's window instead of its own field
        /// (ConfigurationManagerAttributes.CustomDrawer); null for its own field.</param>
        internal ConfigEntry<T> Bind<T>(string part, string key, T defaultValue, string displayName, int order, string description,
                                        AcceptableValueBase acceptableValues = null, Action<ConfigEntryBase> customDrawer = null)
        {
            if (order >= EnabledOrder)
            {
                throw new ArgumentOutOfRangeException(nameof(order), $"A setting's order must stay below {EnabledOrder}, the Enabled switch's.");
            }
            return BindAny(part, key, defaultValue, displayName, order, description, acceptableValues, customDrawer);
        }

        /// <summary>
        /// A setting several modules share (since 2026-10-08, card A69): bound in a section of its own, named the same in
        /// the bundle and in every single release, so in the bundle every module that binds it gets the one entry
        /// (BepInEx hands back the entry already bound under the same section and key, its description and display hints
        /// the first binder's), and a player sets it once for them all. In a single release it is that mod's own, in its
        /// own file. A shared setting's section, key, type and default are the same wherever it's bound (a part keeps them
        /// in one place: the chest service's ChestSettings); its order counts only within its own section. A custom drawer
        /// is passed as Bind's is (since 2026-10-11, card B13: "Use this range in every mod" and its list).
        /// </summary>
        internal ConfigEntry<T> BindShared<T>(string section, string key, T defaultValue, string displayName, int order, string description,
                                              AcceptableValueBase acceptableValues = null, Action<ConfigEntryBase> customDrawer = null)
        {
            // Whether the file had the section before anything bound it, looked at once per file and section (in the
            // bundle the first module to bind it writes it into the file).
            string seenKey = config.ConfigFilePath + "\n" + section;
            if (!SharedAtStart.TryGetValue(seenKey, out bool hadIt))
            {
                SharedAtStart[seenKey] = hadIt = FileHasSection(config.ConfigFilePath, section);
            }
            var hints = new ConfigurationManagerAttributes { DispName = displayName, Order = order, CustomDrawer = customDrawer };
            ConfigEntry<T> entry = config.Bind(section, key, defaultValue, new ConfigDescription(description, acceptableValues, hints));
            if (!hadIt)
            {
                // Taken over from a single release's file (its own section of the same name) only into a file that had
                // none yet: a value already set here for every module is never overwritten by one module's old file
                // (card A69, review 1, m1).
                bound.Add(new Bound(section, key, entry));
            }
            return entry;
        }

        // Per settings file and shared section: whether the file had it before this start (BindShared).
        private static readonly Dictionary<string, bool> SharedAtStart = new Dictionary<string, bool>();

        internal void BindEnabled(string description, bool defaultValue = true)
        {
            Enabled = BindAny(General, "Enabled", defaultValue, "Enabled", EnabledOrder, description, null, null);
        }

        /// <summary>
        /// A setting whose default changed in a later release (since 2026-10-06, card A63: his own settings became the
        /// pack's defaults). BepInEx writes every default into the file at the first start and reads it back from there
        /// after, so a new default never reaches a file written before it. The host therefore moves such a setting to its
        /// new default once per file, right after the settings are bound (and taken over from a single release's file):
        /// only where the file still holds <paramref name="oldDefault"/>, so a value the player chose stays. A player who
        /// chose exactly the old default is moved too: the file can't tell the two apart. <paramref name="step"/> numbers
        /// a module's moves, 1 for its first release that moved any, and a file remembers the last step it has had (the
        /// hidden setting "DefaultsMoved" in General), so each move happens once and a later release can move again with
        /// step 2. Call it in BindSettings, after binding the entry (the Enabled switch is bound before BindSettings).
        /// A setting bound while the game runs (a section bound later, as Map View Rules' override sections), after the
        /// host has moved this file's defaults, is moved at once instead, by the step the file had when the game started;
        /// that call returns whether it moved. Called in BindSettings, it returns false: the host moves it later.
        /// </summary>
        internal bool MovedDefault<T>(ConfigEntry<T> entry, T oldDefault, int step)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }
            if (step < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(step), "A moved default's step starts at 1.");
            }
            var move = new Move(entry, TomlTypeConverter.ConvertToString(oldDefault, typeof(T)), step);
            moves.Add(move);
            if (!movedAtStart)
            {
                return false;
            }
            BindDefaultsMoved();
            if (stepAtStart == null)
            {
                stepAtStart = defaultsMoved.Value;   // no move was named at the start, so nothing has changed the marker since
            }
            bool moved = Apply(move, stepAtStart.Value);
            if (step > defaultsMoved.Value)
            {
                defaultsMoved.Value = step;
            }
            return moved;
        }

        private ConfigEntry<int> defaultsMoved;

        // MoveDefaults has run (the host calls it once, at the start), and the marker the file had before it, if it was bound.
        private bool movedAtStart;
        private int? stepAtStart;

        /// <summary>Binds the hidden "DefaultsMoved" setting, only for a module that has moved a default (MovedDefault).
        /// The host calls it after BindSettings and before taking a single release's settings over, so that a single
        /// release's file passes on which moves it has already had.</summary>
        internal void BindDefaultsMoved()
        {
            if (moves.Count == 0 || defaultsMoved != null)
            {
                return;
            }
            defaultsMoved = BindAny(General, DefaultsMovedKey, 0, "Defaults moved", 0,
                "Which of this mod's changed defaults this file has had, written by the mod: a setting still at an old " +
                "default moves to the new one once. Leave it alone.", null, null);
            ConfigurationManagerAttributes hints = HintsOf(defaultsMoved);
            if (hints != null)
            {
                hints.Browsable = false;
            }
        }

        /// <summary>
        /// Moves each setting MovedDefault named to its new default where the file still holds the old one, for the
        /// steps this file hasn't had yet, then remembers the last step. Returns the display names of the settings moved
        /// (none for a new file: its values are already the new defaults, which are never an old default).
        /// </summary>
        internal List<string> MoveDefaults()
        {
            var moved = new List<string>();
            movedAtStart = true;
            BindDefaultsMoved();
            if (defaultsMoved == null)
            {
                return moved;
            }
            ConfigEntry<int> done = defaultsMoved;
            int last = done.Value;
            stepAtStart = last;
            foreach (Move move in moves)
            {
                if (Apply(move, last))
                {
                    moved.Add(HintsOf(move.Entry)?.DispName ?? move.Entry.Definition.Key);
                }
            }
            int newest = last;
            foreach (Move move in moves)
            {
                newest = Math.Max(newest, move.Step);
            }
            if (newest != last)
            {
                done.Value = newest;
            }
            return moved;
        }

        // Moves one setting to its new default if the file hasn't had its step yet and still holds its old default, compared
        // as the file writes both (so 0.1 is 0.1 in any language, and an enum or a list is its text). A move whose old
        // default is its new one changes nothing, and isn't counted.
        private static bool Apply(Move move, int fileStep)
        {
            if (move.Step <= fileStep || move.Entry.GetSerializedValue() != move.OldDefault ||
                move.OldDefault == TomlTypeConverter.ConvertToString(move.Entry.DefaultValue, move.Entry.SettingType))
            {
                return false;
            }
            move.Entry.BoxedValue = move.Entry.DefaultValue;
            return true;
        }

        /// <summary>The section a part's settings are in, where the host keeps them now (a part's own section alone,
        /// "&lt;Name&gt;" or "&lt;Name&gt; - &lt;part&gt;" in the bundle). For a module that lists several parts under one
        /// heading in a configuration manager (ConfigurationManagerAttributes.Category).</summary>
        internal string SectionOf(string part) => sectionOf(part);

        /// <summary>The display hints a setting was bound with here, for a module that changes how a configuration manager
        /// shows it after binding (its heading, whether it is listed); null for a setting bound elsewhere.</summary>
        internal static ConfigurationManagerAttributes HintsOf(ConfigEntryBase entry)
        {
            object[] tags = entry?.Description?.Tags;
            if (tags != null)
            {
                foreach (object tag in tags)
                {
                    if (tag is ConfigurationManagerAttributes hints)
                    {
                        return hints;
                    }
                }
            }
            return null;
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
                                          AcceptableValueBase acceptableValues, Action<ConfigEntryBase> customDrawer)
        {
            var hints = new ConfigurationManagerAttributes { DispName = displayName, Order = order, CustomDrawer = customDrawer };
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

        private readonly struct Move
        {
            internal readonly ConfigEntryBase Entry;
            internal readonly string OldDefault;
            internal readonly int Step;

            internal Move(ConfigEntryBase entry, string oldDefault, int step)
            {
                Entry = entry;
                OldDefault = oldDefault;
                Step = step;
            }
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
