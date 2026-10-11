// The QoL Mods item stands part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemStands\. Every mod that uses
// it carries a copy in its own parts\ItemStands\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace QoLMods.ItemStands
{
    /// <summary>What Item Stand Icons shows right now: whether its marks show, how they look, and the rule.</summary>
    internal sealed class StandView
    {
        internal StandView(bool showing, StandLook look, StandRule rule)
        {
            Showing = showing;
            Look = look ?? StandLook.Default;
            Rule = rule ?? StandRule.Default;
        }

        /// <summary>True while its marks show (on, "Show the marks" on, and its key held if it has one).</summary>
        internal bool Showing { get; }

        internal StandLook Look { get; }

        internal StandRule Rule { get; }
    }

    /// <summary>
    /// How another module (Add to Cart) shows the marks as Item Stand Icons is set, without using its code (a module never
    /// does): in the bundle both run in one plugin, so Item Stand Icons hands the part what it shows (Current); between
    /// single releases, the part reads the look and the rule from Item Stand Icons' settings file, found through its
    /// plugin, by the keys below (Item Stand Icons' own setting keys), and asks the plugin itself whether its marks show
    /// (its public bool ShowsMarks). The keys and that property never change: renaming one would quietly stop Add to
    /// Cart's marks.
    /// </summary>
    internal static class StandShare
    {
        /// <summary>Item Stand Icons' single release's plugin ID.</summary>
        internal const string ModuleGuid = "modprojects.itemstandicons";

        internal const string KeyEnabled = "Enabled";
        internal const string KeyShowMarks = "ShowMarks";
        internal const string KeyMarkWall = "MarkWallOnly";
        internal const string KeyMarkFlat = "MarkFlatOnly";
        internal const string KeyMarkBoth = "MarkBoth";
        internal const string KeyLook = "Look";
        internal const string KeyBothShape = "BothShape";
        internal const string KeyColour = "Colour";
        internal const string KeyOutline = "Outline";
        internal const string KeySize = "Size";
        internal const string KeyPlace = "Place";
        internal const string KeyNudgeRight = "NudgeRight";
        internal const string KeyNudgeUp = "NudgeUp";
        internal const string KeyHoldKey = "HoldKey";
        internal const string KeyRule = "Rule";
        internal const string KeyStands = "Stands";
        internal const string KeyOtherStands = "OtherStands";

        // Since 0.2.0: the armor stand's mark, and the armor stands that count. An older Item Stand Icons has none of
        // them, so its file gives the defaults.
        internal const string KeyMarkArmor = "MarkArmor";
        internal const string KeyArmorPlace = "ArmorPlace";
        internal const string KeyArmorNudgeRight = "ArmorNudgeRight";
        internal const string KeyArmorNudgeUp = "ArmorNudgeUp";
        internal const string KeyArmorStands = "ArmorStands";

        /// <summary>Set by Item Stand Icons when it runs in the same plugin (the bundle): what it shows right now.</summary>
        internal static Func<StandView> Current { get; set; }

        /// <summary>The public property of Item Stand Icons' single release that says whether its marks show now: it
        /// running (not stood aside, not switched off after an error), "Show the marks" on, its key held.</summary>
        internal const string ShowsMarksProperty = "ShowsMarks";

        private static ConfigFile singleFile;
        private static BaseUnityPlugin singlePlugin;
        private static PropertyInfo singleShows;
        private static bool lookedForSingle;
        private static int singleVersion;
        private static int builtVersion = -1;
        private static StandLook singleLook;
        private static StandRule singleRule;
        private static StandView singleView;

        /// <summary>
        /// What Item Stand Icons shows now, for another module to follow: from the same plugin, else from its single
        /// release (whether it shows from the plugin, the look and rule from its settings file); null when it isn't
        /// installed.
        /// </summary>
        internal static StandView Find()
        {
            Func<StandView> current = Current;
            if (current != null)
            {
                return current();
            }
            ConfigFile file = SingleFile();
            if (file == null)
            {
                return null;
            }
            if (builtVersion != singleVersion || singleLook == null)
            {
                builtVersion = singleVersion;
                singleLook = LookFrom(key => Value(file, key));
                singleRule = RuleFrom(key => Value(file, key));
            }
            // Whether it shows is asked of the plugin itself, not its file (review 1): a copy that failed to start or
            // switched itself off after an error shows nothing, whatever its settings say.
            bool on;
            try
            {
                on = singleShows != null && singlePlugin != null && singleShows.GetValue(singlePlugin) is bool shows && shows;
            }
            catch (Exception)
            {
                on = false;
            }
            // The same object until something in it changes: Add to Cart asks every frame its window is open (review 2).
            if (singleView == null || singleView.Showing != on || !ReferenceEquals(singleView.Look, singleLook) || !ReferenceEquals(singleView.Rule, singleRule))
            {
                singleView = new StandView(on, singleLook, singleRule);
            }
            return singleView;
        }

        // Item Stand Icons' single release's settings, found once through its plugin (in the section General there).
        private static ConfigFile SingleFile()
        {
            if (lookedForSingle)
            {
                return singleFile;
            }
            if (!Chainloader.PluginInfos.TryGetValue(ModuleGuid, out PluginInfo info))
            {
                return null;   // not started yet, or not installed: looked for again next time
            }
            lookedForSingle = true;
            singlePlugin = info?.Instance;
            singleFile = singlePlugin != null ? singlePlugin.Config : null;
            singleShows = singlePlugin?.GetType().GetProperty(ShowsMarksProperty, BindingFlags.Instance | BindingFlags.Public);
            if (singleShows != null && singleShows.PropertyType != typeof(bool))
            {
                singleShows = null;
            }
            if (singlePlugin != null && singleShows == null)
            {
                // Said once (review 2): an older or renamed Item Stand Icons would otherwise leave the marks out silently.
                StandFit.Warn?.Invoke($"Item Stand Icons (its own release) has no public \"{ShowsMarksProperty}\", so its switch can't be followed and " +
                                      "\"AsItemStandIcons\" shows no marks; update it (\"Always\" shows them in its look without it).");
            }
            if (singleFile != null)
            {
                singleFile.SettingChanged += (sender, args) => singleVersion++;
            }
            return singleFile;
        }

        private static object Value(ConfigFile file, string key)
        {
            var definition = new ConfigDefinition("General", key);
            return file.ContainsKey(definition) ? file[definition]?.BoxedValue : null;
        }

        /// <summary>A look from Item Stand Icons' settings, read by key (its own entries, or its settings file's): a value
        /// that is missing or can't be read takes the default look's.</summary>
        internal static StandLook LookFrom(Func<string, object> value)
        {
            StandLook fallback = StandLook.Default;
            return new StandLook(
                Bool(value(KeyMarkWall), fallback.Wall),
                Bool(value(KeyMarkFlat), fallback.Flat),
                Bool(value(KeyMarkBoth), fallback.Both),
                Bool(value(KeyMarkArmor), fallback.Armor),
                Choice(value(KeyLook), fallback.Look),
                Choice(value(KeyBothShape), fallback.BothShape),
                value(KeyColour) is Color colour ? colour : fallback.Colour,
                Bool(value(KeyOutline), fallback.Outline),
                Number(value(KeySize), fallback.Size),
                Choice(value(KeyPlace), fallback.Place),
                Number(value(KeyNudgeRight), 0f),
                Number(value(KeyNudgeUp), 0f),
                Choice(value(KeyArmorPlace), fallback.ArmorPlace),
                Number(value(KeyArmorNudgeRight), 0f),
                Number(value(KeyArmorNudgeUp), 0f));
        }

        /// <summary>The rule from Item Stand Icons' settings, read by key.</summary>
        internal static StandRule RuleFrom(Func<string, object> value) =>
            new StandRule(Choice(value(KeyRule), StandCheck.GameCheck) == StandCheck.GameCheck,
                          value(KeyStands) as string ?? StandFit.DefaultStands,
                          Bool(value(KeyOtherStands), false),
                          value(KeyArmorStands) as string ?? StandFit.DefaultArmorStands);

        /// <summary>Whether a "hold this key" setting lets the marks show now: yes with no key set, else while it (and
        /// its modifiers) are held.</summary>
        internal static bool KeyHeld(object setting) => !(setting is KeyboardShortcut key) || KeyHeld(key);

        internal static bool KeyHeld(KeyboardShortcut key)
        {
            if (key.MainKey == KeyCode.None)
            {
                return true;
            }
            if (!ZInput.GetKey(key.MainKey, false))
            {
                return false;
            }
            foreach (KeyCode modifier in key.Modifiers)
            {
                if (!ZInput.GetKey(modifier, false))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Bool(object value, bool fallback) => value is bool yes ? yes : fallback;

        private static float Number(object value, float fallback)
        {
            switch (value)
            {
                case float single:
                    return single;
                case int whole:
                    return whole;
                case double wide:
                    return (float)wide;
                default:
                    return fallback;
            }
        }

        // Another plugin's copy of an enum is another type: read it by name.
        private static T Choice<T>(object value, T fallback) where T : struct =>
            value != null && System.Enum.TryParse(value.ToString(), out T parsed) ? parsed : fallback;
    }
}
