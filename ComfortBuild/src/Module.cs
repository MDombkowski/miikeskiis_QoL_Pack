using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Catalogue;
using QoLMods.Kit;
using QoLMods.Pictures;

namespace ComfortBuild
{
    /// <summary>
    /// Comfort Build as a module of miikeskii's QoL Pack: a Comfort tab in the build menu with only the pieces that give
    /// comfort, their comfort groups down the left, each piece's comfort in a corner of its picture, and the pieces that
    /// wouldn't raise his comfort where he stands greyed out, saying why (card A13). Client-side only. The same code runs
    /// in the bundle and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.comfortbuild";

        internal const string Name = "Comfort Build";

        internal const string Version = "0.1.2";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "ComfortBuild";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off takes the Comfort tab, the numbers and the grey out of the build menu at once; " +
            "on brings them back.";

        internal override string SwitchedOffNote => "The build menu is the game's own.";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            Game.Resolve(missing, missingOptional);
            Greys.Warn = line => Log.LogWarning(line);
            Greys.Info = line => Log.LogInfo(line);
        }

        internal override string UpdateTask => "following the build menu";

        /// <summary>Also while switched off: taking back what it shows follows the switch.</summary>
        internal override void Update() => Menu.Tick();

        internal override IEnumerable<Action> UndoSteps => Menu.UndoSteps;
    }
}
