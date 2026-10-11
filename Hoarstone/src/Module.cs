using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BepInEx.Logging;
using QoLMods.Kit;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// Hoarstone as a module of miikeskii's QoL Pack (card A88): it works out each settlement's whole shape from the
    /// pieces players built (the Settlement Shape Lab's method), keeps monsters from spawning inside it, draws it on
    /// request, and adds the Hoarstone, a settlement's marker, to the hammer's menu: a real vanilla Ward placed marked and
    /// switched off. Client-side only: spawning runs in the game that owns the zone, and friends without the pack play as
    /// before. The same code runs in the bundle and in its single release (standalone\Plugin.cs); see
    /// QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.hoarstone";

        internal const string Name = "Hoarstone";

        internal const string Version = "0.1.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "Hoarstone";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        /// <summary>Its parts are sections of their own in the settings file ("Hoarstone - Shape" …), listed under the
        /// one heading in the window (Folds).</summary>
        internal override bool OneSectionInBundle => false;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off: monsters spawn as in the game, the Hoarstone leaves the hammer's menu, outlines " +
            "and tints go, and a Hoarstone already placed is the game's own switched-off Ward again. On brings it all back.";

        internal override string SwitchedOffNote => "Monsters spawn as in the game, and a Hoarstone is a switched-off Ward.";

        internal override string UpdateTask => "keeping its settlements and drawings up to date";

        /// <summary>Its patches stay after an error (MODULES.md rule 7; review round 1): every one does nothing while the
        /// module is off, but the four that must not stop (a Hoarstone entry never places an object of its own kind, its
        /// mark, and taking one down gives back what it cost).</summary>
        internal override bool KeepPatchesAfterError => true;

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        /// <summary>For a patch on a busy game method, which catches its own errors rather than allocating a guard on
        /// every call: hands an error to the host as Guard would have, which switches the mod off.</summary>
        internal static void Fail(string task, Exception error) =>
            current.Host.Guard(task, () => ExceptionDispatchInfo.Capture(error).Throw());

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            GameHooks.Resolve(missing, missingOptional);
            Folds.RebuildList = ManagerLink.Rebuild();   // null with a Jötunn too old to ask
        }

        /// <summary>Every frame while the module runs, also while switched off: the fold-outs, the records and what's
        /// worked out from them, the Hoarstone's menu entry and looks, and the outlines.</summary>
        internal override void Update()
        {
            Folds.Tick();
            float now = Time.unscaledTime;
            World.Tick(now);
            Stone.Tick(now);
            Outlines.Tick(now);
        }

        /// <summary>After an error, or switched off for good: each step on its own (MODULES.md rule 7).</summary>
        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            Outlines.Clear,
            Stone.TakeOut,
            Stone.Untint,
            World.Forget,
        };
    }
}
