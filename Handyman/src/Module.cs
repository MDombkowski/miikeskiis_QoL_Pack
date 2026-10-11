using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace Handyman
{
    /// <summary>
    /// Handyman as a module of miikeskii's QoL Pack (card A44, "R-Repair"): with the hammer in hand, a tap of R picks the
    /// hammer's Repair, a tap again goes back to the piece he had, turned as it was, and holding R puts the hammer away.
    /// Client-side only. The same code runs in the bundle and in its single release (standalone\Plugin.cs); see
    /// QoLMods\MODULES.md. "R picks Repair" is on out of the box since card A63 (until then off, MODULES.md rule 4).
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.handyman";

        internal const string Name = "Handyman";

        internal const string Version = "0.1.1";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "Handyman";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off gives R back to the game at once: it puts the hammer away, as in the game. " +
            "On, R picks Repair only while \"R picks Repair\" is on too.";

        internal override string SwitchedOffNote => "R puts the hammer away, as in the game.";

        internal override IEnumerable<Action> UndoSteps => new Action[] { Hammer.Forget };

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional) => GameHooks.Resolve(missing, missingOptional);
    }
}
