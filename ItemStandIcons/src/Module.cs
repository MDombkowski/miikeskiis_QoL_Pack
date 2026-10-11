using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.ItemStands;
using QoLMods.Kit;

namespace ItemStandIcons
{
    /// <summary>
    /// Item Stand Icons as a module of miikeskii's QoL Pack: a small mark on every item that fits an item stand, saying
    /// which (an up arrow the wall stand, a right arrow the flat one, both), and a second mark, a mannequin, on every item
    /// the armor stand takes (0.2.0), worked out from the game's own rules, wherever item icons show: his inventory (and
    /// EquipmentAndQuickSlots' slots), chests, the hotbar, the crafting menu (the list, the chosen recipe, its
    /// ingredients), the trader, and the build menu's costs if he wants (card A51); if he wants, only while he aims at a
    /// stand (Aim). Client-side only. It uses a part of the bundle, the item stands part (QoLMods\parts\ItemStands: the
    /// rules, the drawn pictures, the mark), which Add to Cart uses too, and carries it in its own parts\ folder. The same
    /// code runs in the bundle and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent (the item stands
        /// part knows it too, as StandShare.ModuleGuid, to find these settings for Add to Cart).</summary>
        internal const string Guid = "modprojects.itemstandicons";

        internal const string Name = "Item Stand Icons";

        internal const string Version = "0.2.1";

        /// <summary>The name of the item stands' mark on each icon (MODULES.md rule 10).</summary>
        internal const string MarkName = "ItemStandIcons_StandMark";

        /// <summary>The name of the armor stand's mark on each icon.</summary>
        internal const string ArmorMarkName = "ItemStandIcons_ArmorMark";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "ItemStandIcons";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off; off out of the box. Off takes every mark off the icons at once; on brings them back " +
            "(with \"Show the marks\" on).";

        /// <summary>Off in a new settings file (card A63, 2026-10-06: his own setup keeps it off), so nobody's icons change
        /// until it's switched on; switched on, it shows his own marks. A file still at the old default (on) moves once
        /// (Settings.Bind).</summary>
        internal override bool EnabledByDefault => false;

        internal override string SwitchedOffNote => "No item icon carries a mark.";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            StandFit.Resolve(missingOptional);
            StandFit.Warn = line => Log.LogWarning(line);
            GameHooks.Resolve(missing, missingOptional);
            // Add to Cart, in the same plugin (the bundle), follows what this module shows.
            StandShare.Current = Settings.View;
        }

        internal override string UpdateTask => "marking the crafting list and the trader";

        /// <summary>Also while switched off: taking the marks back follows the switch.</summary>
        internal override void Update() => Places.Tick();

        internal override IEnumerable<Action> UndoSteps => new Action[] { Places.HideAll };
    }
}
