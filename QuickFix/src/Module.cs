using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace QuickFix
{
    /// <summary>
    /// Quick Fix as a module of miikeskii's QoL Pack (card B22, his idea and his name, 2026-10-11): the Grinding Wheel
    /// repairs all non-metallic gear and the Smith's Anvil all metallic gear at a press of the Use key, each by its
    /// setting; both can be placed with no Forge near; repairs on opening or coming near a station, by setting; and the
    /// Sharpening Stone, used from the hotbar, repairs gear anywhere over one bar, at a cost. Client-side only: durability
    /// is each player's own, kept with the character. The same code runs in the bundle and in its single release
    /// (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.quickfix";

        internal const string Name = "Quick Fix";

        internal const string Version = "0.2.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "QuickFix";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        /// <summary>Its parts are sections of their own in the settings file ("Quick Fix - Sharpening Stone" …), listed
        /// under the one heading in the window (Folds).</summary>
        internal override bool OneSectionInBundle => false;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off: the Grinding Wheel and Smith's Anvil do nothing and need a Forge near again, " +
            "stations repair one item at a time, and the Sharpening Stone is only a material, as in the game; a repair " +
            "running stops, costing nothing. On brings it all back.";

        internal override string SwitchedOffNote => "Repairs work as in the game.";

        internal override string UpdateTask => "keeping the stone and the repairs near stations in step with the settings";

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
            Folds.RebuildList = ManagerLink.Rebuild();   // null with a Jötunn too old to ask
            QoLMods.ListPicker.Lists.Resolve(missingOptional);
        }

        /// <summary>Every frame while the module runs, also while switched off: the fold-outs, the stone's wear following
        /// the settings, a repair whose player is gone, and the stations near him.</summary>
        internal override void Update()
        {
            Folds.Tick();
            bool on = IsOn;
            StoneWear.Follow(on);
            StoneRepair.Watch(on);
            Stations.Tick(on);
        }

        /// <summary>After an error, each step on its own (MODULES.md rule 7): a repair running stops, costing nothing; the
        /// stone is put back as the game has it; what was near is forgotten.</summary>
        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            StoneRepair.Stop,
            StoneWear.PutBack,
            Stations.Forget,
        };
    }
}
