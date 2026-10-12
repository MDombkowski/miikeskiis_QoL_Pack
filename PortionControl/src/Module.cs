using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using QoLMods.Kit;

namespace PortionControl
{
    /// <summary>
    /// Portion Control as a module of miikeskii's QoL Pack: the game's split window (Shift + click on a stack) gains an
    /// amount box, slider buttons (Min, -1, 50%, +1, Max), one-click percentages, his fixed amounts, All -1 and Max weight
    /// (as much of a chest's stack as he can carry without being encumbered). A one-click amount goes on the cursor through
    /// the game's own OK, so the game moves the items; a right-click on an amount splits the stack into stacks of it. The
    /// chest window gains a -1 beside Take all and a Max weight button above its weight (card A54). Written from scratch to
    /// replace BetterSplitDialog (cards A17 and A49); its split window stands aside while BetterSplitDialog or Abacus is
    /// installed, and the chest window's buttons still work then. Client-side only. The same code runs in the bundle and
    /// in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.portioncontrol";

        internal const string Name = "Portion Control";

        internal const string Version = "0.2.7";

        /// <summary>turbero's BetterSplitDialog, which this module replaces.</summary>
        internal const string BetterSplitDialog = "Turbero.BetterSplitDialog";

        /// <summary>ComfyMods' Abacus (by bruce), which adds its own buttons to the same window.</summary>
        internal const string Abacus = "bruce.valheim.brucecomfymods.abacus";

        /// <summary>Azumatt's CurrencyPocket: with it and BetterSplitDialog's one-click buttons together, coins were copied
        /// (BetterSplitDialog's changelog, 1.1.0; the cause isn't documented), so the one-click buttons stay hidden with it
        /// unless a setting says otherwise.</summary>
        internal const string CurrencyPocket = "Azumatt.CurrencyPocket";

        /// <summary>Mods that add buttons to the same split window. With one of them running, this module's split window
        /// stands aside (the game's, or theirs, stays as it is), so the two never put buttons on top of each other; the chest
        /// window's buttons still work. The bundle and the single release list them as soft dependencies, so they have
        /// started, and can be seen, by the time this module looks.</summary>
        internal static readonly (string Guid, string Name)[] Rivals =
        {
            (BetterSplitDialog, "BetterSplitDialog"),
            (Abacus, "Abacus"),
        };

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "PortionControl";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off: the split window (Shift + click on a stack) and the chest window are the game's " +
            "own again, at once; on brings the buttons back.";

        internal override string SwitchedOffNote => "The split window and the chest window are the game's own.";

        internal override string UpdateTask => "keeping the split window's and the chest window's buttons, and the settings' groups, up to date";

        /// <summary>True when CurrencyPocket was found at start.</summary>
        internal static bool CurrencyPocketLoaded { get; private set; }

        /// <summary>True when BetterSplitDialog or Abacus was found at start: the split window is theirs, and only the
        /// chest window's buttons run.</summary>
        internal static bool SplitWindowStandsAside { get; private set; }

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            List<string> rivals = Rivals.Where(rival => Chainloader.PluginInfos.TryGetValue(rival.Guid, out BepInEx.PluginInfo info) && info.Instance != null)
                                        .Select(rival => rival.Name).ToList();
            SplitWindowStandsAside = rivals.Count > 0;
            GameHooks.Resolve(missing, missingOptional, SplitWindowStandsAside);
            if (SplitWindowStandsAside && missing.Count == 0)
            {
                string names = string.Join(" and ", rivals);
                Log.LogInfo($"{names} {(rivals.Count > 1 ? "are" : "is")} installed and adds buttons to the same split window, so " +
                            $"{Name} leaves the split window to it: uninstall {names} (or switch it off in the mod manager) and " +
                            $"start the game again for {Name}'s." +
                            (GameHooks.CanUseChest ? " The chest window's -1 and Max weight still work." : string.Empty));
            }
            CurrencyPocketLoaded = Chainloader.PluginInfos.TryGetValue(CurrencyPocket, out BepInEx.PluginInfo pocket) && pocket.Instance != null;
            if (CurrencyPocketLoaded && !SplitWindowStandsAside && missing.Count == 0)
            {
                Log.LogInfo("CurrencyPocket is installed: the one-click buttons follow the setting \"With CurrencyPocket " +
                            "installed\" (hidden unless it says otherwise), since with it BetterSplitDialog's one-click " +
                            "buttons once copied coins.");
            }
        }

        internal override void Update()
        {
            Folds.Tick();
            Window.Update();
            ChestButtons.Update();
        }

        internal override IEnumerable<Action> UndoSteps => new Action[] { Window.Remove, ChestButtons.Remove, Folds.OpenAll };
    }
}
