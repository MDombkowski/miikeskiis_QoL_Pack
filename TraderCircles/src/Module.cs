using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace TraderCircles
{
    /// <summary>
    /// Trader Circles as a module of miikeskii's QoL Pack: see-through coloured circles on the big map around every place
    /// a trader could still be, each offset at random but the same for everyone in the world, kept inside the ground where
    /// that trader can appear, and gone once the trader is found. Client-side only: it asks the unmodded server the
    /// question a vegvisir asks, and catches the answers itself, so no pin, message or head turn comes of it. The same
    /// code runs in the bundle and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.tradercircles";

        internal const string Name = "Trader Circles";

        internal const string Version = "0.1.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "TraderCircles";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off hides every circle at once; on shows them again. Each trader also has its own " +
            "switch below, all off at first.";

        internal override string SwitchedOffNote => "No circles are drawn, and the server is not asked anything.";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>True while the module runs, switched on or off: its patches are in place.</summary>
        internal static bool IsActive => current != null && current.Host != null && current.Host.Active;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override string UpdateTask => "keeping track of the traders";

        internal override string LateUpdateTask => "drawing the circles";

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional) => GameHooks.Resolve(missing, missingOptional);

        internal override void Update() => Traders.Update();

        internal override void LateUpdate() => CircleLayer.Refresh();

        internal override IEnumerable<Action> UndoSteps => new Action[] { CircleLayer.RemoveAll };

        /// <summary>After an error its one patch stays: the catch for the server's answers. An answer still on its way
        /// would otherwise become a pin on the map. The catch does nothing else while the mod is off.</summary>
        internal override bool KeepPatchesAfterError => true;
    }
}
