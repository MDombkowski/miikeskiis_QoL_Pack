using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;
using QoLMods.ListPicker;

namespace ZFighter
{
    /// <summary>
    /// Z-Fighter as a module of miikeskii's QoL Pack: where two building pieces are laid into each other, one of them
    /// is drawn a millimetre or so bigger on this player's screen, so their surfaces no longer flicker where they
    /// overlap. Only how pieces are drawn changes: never a piece's place, collider or saved record. Client-side only.
    /// The same code runs in the bundle and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.zfighter";

        internal const string Name = "Z-Fighter";

        internal const string Version = "0.2.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "ZFighter";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the fix on or off. On: where two building pieces are laid into each other, one of them is drawn a " +
            "millimetre or so bigger on your screen, so their surfaces stop flickering where they overlap. Nothing " +
            "moves where it's saved, and friends see their own game. Off: every piece is drawn as the game draws it, " +
            "at once.";

        internal override string SwitchedOffNote => "Every piece is drawn as the game draws it.";

        internal override string UpdateTask => "keeping overlapping pieces apart";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            GameHooks.Resolve(missing, missingOptional);
            Lists.Resolve(missingOptional);
        }

        internal override void Update() => Tracker.Update();

        /// <summary>After an error: every piece drawn as the game draws it, then forgotten.</summary>
        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            Tracker.RestoreAll,
            Tracker.Clear,
        };
    }
}
