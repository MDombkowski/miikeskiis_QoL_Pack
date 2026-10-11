using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace MenUI
{
    /// <summary>
    /// MenUI as a module of miikeskii's QoL Pack: when other mods add buttons to the game's main menu (or to the menu Esc
    /// opens in game), the list grows downward until its last buttons, Quit among them, fall below the window. MenUI
    /// measures whatever buttons the menu holds and fits them back on screen, shrinking the list and moving it up as its
    /// settings say, and puts them all in the arrow keys' order. Client-side only; no patches: it looks at the menus
    /// after each frame. The same code runs in the bundle and in its single release (standalone\Plugin.cs); see
    /// QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.menui";

        internal const string Name = "MenUI";

        internal const string Version = "0.1.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "MenUI";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. On, a menu whose buttons reach past the bottom of the window is fitted back on " +
            "screen; a menu that fits is left exactly as it is. Off puts the menus back as the game and other mods made " +
            "them, at once.";

        internal override string SwitchedOffNote => "The menus look as the game and other mods make them.";

        internal override string LateUpdateTask => "fitting the menus";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional) => GameHooks.Resolve(missing, missingOptional);

        /// <summary>After each frame, once the game's animators and every other mod have placed the menus: fit them, or
        /// put them back while the mod is off. The kit runs this inside the module's guard.</summary>
        internal override void LateUpdate() => MenuFit.AfterFrame(IsOn);

        internal override IEnumerable<Action> UndoSteps => MenuFit.UndoSteps;
    }
}
