using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace SpareKeys
{
    /// <summary>
    /// Spare Keys as a module of miikeskii's QoL Pack: a window with a picture of the keyboard and mouse where every key
    /// is coloured by who uses it (the game, with his rebinds; every mod's key settings; his console binds; keys found in
    /// mods' code), and the same picture beside F1's and the game's Controls menu's "press a key" while he picks one. It
    /// only reads: it never changes a setting of the game's or another mod's. Client-side only. The same code runs in the
    /// bundle and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.sparekeys";

        internal const string Name = "Spare Keys";

        internal const string Version = "0.2.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "SpareKeys";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        // Three parts of its own (window, what counts, while picking a key); in the bundle they share one section, one
        // heading in F1 (his rule: one heading per mod).
        internal override string EnabledDescription =>
            "Turns the mod on or off. Off closes the key map and the panels at once, and its key does nothing; on brings " +
            "them back. It only ever reads keys: it never changes the game's or another mod's settings.";

        internal override string SwitchedOffNote => "Nothing else changes: it only ever showed keys.";

        internal override string UpdateTask => "showing the key map";

        internal static ManualLogSource Log => current?.Host?.Log;

        /// <summary>True while the module runs and its Enabled switch is on.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current?.Host?.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional) => Reach.Resolve(missingOptional);

        internal override void Update() => Frames.Update();

        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            KeyMapWindow.Close,
            PickPanels.HideAll,
            Collector.Forget,
        };
    }
}
