using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace MapZoom
{
    /// <summary>
    /// Map Zoom as a module of miikeskii's QoL Pack: the mouse wheel zooms the big map in fewer notches, by a setting
    /// that works at once, and toward the mouse pointer. Client-side only. The same code runs in the bundle and in its
    /// single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.mapzoom";

        internal const string Name = "Map Zoom";

        internal const string Version = "0.1.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "MapZoom";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off gives the mouse wheel back to the game's own zoom at once; on brings this " +
            "mod's zoom back.";

        internal override string SwitchedOffNote => "The map zooms as it does without the mod.";

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
