using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// House Trained as a module of miikeskii's QoL Pack (card A48, his idea of 2026-09-29 and his four points on tames in
    /// pens of 2026-09-30; his name for it): tames spare buildings, creatures' area attacks hurt the creatures around them,
    /// and tames stay in their pens. Three parts, each with its own switch, all on out of the box since card A63 (off
    /// before); since card A84 (his feedback of 2026-10-09), two more: tames heal faster in pens (PenHealing), and a
    /// creature can be fed from the hand (HandFeed). Client-side only: each
    /// game applies its own settings to the creatures and pieces it simulates, and nothing is written into the world but
    /// what the game itself writes (a tame's health and fed time). The
    /// same code runs in the bundle and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.housetrained";

        internal const string Name = "House Trained";

        internal const string Version = "0.2.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "HouseTrained";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        /// <summary>Its parts are sections of their own in the settings file ("House Trained - Friendly fire" …), listed
        /// under the one heading in the window (Folds).</summary>
        internal override bool OneSectionInBundle => false;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off stops every part at once, and creatures, tames and buildings behave as in the game; " +
            "on brings back each part switched on below. Out of the box every part waits for its own switch.";

        internal override string SwitchedOffNote => "Creatures, tames and buildings behave as in the game.";

        internal override string UpdateTask => "keeping its parts in step with their settings";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>A warning in the module's log, or nowhere while it has no host yet.</summary>
        internal static void Warn(string text) => current?.Host?.Log?.LogWarning(text);

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first, through Parts.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            GameHooks.Resolve(missingOptional);
            Walls.PieceMask = LayerMask.GetMask("piece");
            Folds.RebuildList = ManagerLink.Rebuild();   // null with a Jötunn too old to ask
        }

        /// <summary>Every frame while the module runs, also while switched off: the fold-outs, which parts are on (and
        /// what switching one off puts back), the hello, healing in pens, and feeds asked of other games.</summary>
        internal override void Update()
        {
            Folds.Tick();
            float now = Time.time;
            Parts.Follow(now);
            Hello.Tick(now, IsOn, Parts.FriendlyOn);
            PenHealing.Tick(now);
            HandFeed.Tick(now);
        }

        /// <summary>After an error, each step on its own (MODULES.md rule 7; code review 1, nit 18): every part read as off,
        /// every flag put back on the attacks, area objects and projectiles and every grudge forgotten, every tame's timers
        /// and the birth notes forgotten, and the hello's last "off".</summary>
        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            Parts.AllOff,
            Friendly.PutBack,
            Tames.ForgetAllPens,
            Pens.Forget,
            Hello.Forget,
        };
    }
}
