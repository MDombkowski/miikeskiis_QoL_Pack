using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace MapViewRules
{
    /// <summary>
    /// Map View Rules as a module of miikeskii's QoL Pack: which pins the big map and the minimap draw at each zoom,
    /// per category of Cartur's Map Pins, per pickable group and per chosen kind or pin name (icons and names apart),
    /// what they draw of the friends' map from a cartography table, plus a zoom readout and "use current map zoom" buttons
    /// in the settings window. It only decides what is drawn: every pin stays in the
    /// game's list, saved and shared as before. Client-side only. The same code runs in the bundle and in its single
    /// release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.mapviewrules";

        internal const string Name = "Map View Rules";

        internal const string Version = "0.2.2";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "MapViewRules";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off shows again at once every pin, name and friends' explored ground it hid, gives " +
            "every pin its full size back and takes the zoom readout away; on applies the rules again.";

        internal override string SwitchedOffNote => "The maps draw every pin as they do without the mod.";

        internal override string UpdateTask => "following its settings";

        internal override string LateUpdateTask => "keeping hidden names and the friends' ground hidden";

        // Some thirty sections of rules: in the bundle's file each gets a section of its own, "Map View Rules - <part>",
        // and a configuration manager's window lists them all under "Map View Rules", as fold-outs (Folds).
        internal override bool OneSectionInBundle => false;

        internal override IEnumerable<Action> UndoSteps => new Action[] { Drawing.RestoreAll, Readout.Remove, FriendsMap.Release };

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            Settings.CarryOverOnce();   // after the bundle's takeover of a single release's settings
            Settings.ApplyMovedOverrides();   // after the host's moved defaults (card A63): an old file's empty list became his four names
            GameHooks.Resolve(missing, missingOptional);
            Cartur.Resolve(missingOptional);
            FriendsMap.Resolve(missingOptional);
            Sorter.ByAnotherPlayer = Players.ByAnother;   // a friend's pin by its owner or its author
            Folds.RebuildList = ManagerLink.Rebuild();   // null with a Jötunn too old to ask
        }

        internal override void Update()
        {
            Folds.Tick();
            Drawing.Follow();
            Readout.Tick();
        }

        // After every Update, so the game's delayed switching-on of new names, and its fade of the friends' ground, have
        // already happened this frame.
        internal override void LateUpdate()
        {
            Drawing.AfterFrame();
            FriendsMap.Tick();
        }
    }
}
