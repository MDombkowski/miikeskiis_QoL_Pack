using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using QoLMods.Chests;
using QoLMods.Kit;

namespace CraftFromChests
{
    /// <summary>
    /// Craft from Chests as a module of miikeskii's QoL Pack: crafting, building and filling stations use what lies in
    /// the chests nearby as if he carried it, as AzuCraftyBoxes does, but every take goes through the pack's chest
    /// service (QoLMods\parts\Chests), which first claims the chest from its owner the way the game's own chest window
    /// does, so a friend's open chest is never taken from and nothing is lost or copied. The menus show how much there is
    /// ("57/10") and the build menu how many can be placed. Client-side only. It carries the chest service in its own
    /// parts\ folder. The same code runs in the bundle and in its single release (standalone\Plugin.cs); see
    /// QoLMods\MODULES.md. Card A27.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.craftfromchests";

        internal const string Name = "Craft from Chests";

        internal const string Version = "0.6.0";

        /// <summary>Other mods that take from chests for crafting. With one of them running, this module stays off: two
        /// mods taking from the same chests at once could lose or copy items. The bundle and the single release list them
        /// as soft dependencies, so they have started, and can be seen, by the time this module looks.</summary>
        internal static readonly (string Guid, string Name)[] Rivals =
        {
            (AzuCraftyBoxes, "AzuCraftyBoxes"),
            (CraftFromContainers, "CraftFromContainers"),
        };

        /// <summary>AzuCraftyBoxes' plugin ID (the soft dependencies name it).</summary>
        internal const string AzuCraftyBoxes = "Azumatt.AzuCraftyBoxes";

        /// <summary>aedenthorn's CraftFromContainers' plugin ID, as AzuCraftyBoxes names it.</summary>
        internal const string CraftFromContainers = "aedenthorn.CraftFromContainers";

        /// <summary>shudnal's MyLittleUI, whose Multicraft "Craft an amount" hides while it's on (OtherMulticraft). A soft
        /// dependency, so it starts first and its Multicraft can be found when the patches go on.</summary>
        internal const string MyLittleUI = "shudnal.MyLittleUI";

        // Marks a rival in the missing list, so DescribeMissing can say what to do about it.
        private const string RivalMark = "rival:";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "CraftFromChests";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off: crafting, building and stations use only what you carry, as without the mod, " +
            "and the menus show only what you carry. On brings the chests back. The key below switches it too.";

        internal override string SwitchedOffNote => "Crafting, building and stations use only what you carry.";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        /// <summary>This module, as the chest service knows it: "Chests beyond the server's reach" is the pack's shared
        /// setting (card A69), and its range is what it asks of Far Fetched on the server (card A70).</summary>
        internal static readonly ChestUser Chests = new ChestUser(Name, () => Log, Guard, () => IsOn,
            reach: () => Settings.Range == null ? 0f : ChestSettings.RangeOf(Settings.Range));

        internal override string UpdateTask => "taking from chests";

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            foreach ((string guid, string name) in Rivals)
            {
                if (Chainloader.PluginInfos.TryGetValue(guid, out BepInEx.PluginInfo rival) && rival.Instance != null)
                {
                    missing.Add(RivalMark + name);
                }
            }
            ChestHooks.Resolve(missing, Chests);
            GameHooks.Resolve(missing, missingOptional);
            QoLMods.ListPicker.Lists.Resolve(missingOptional);
        }

        internal override string DescribeMissing(IList<string> missing)
        {
            List<string> rivals = missing.Where(each => each.StartsWith(RivalMark, StringComparison.Ordinal))
                                         .Select(each => each.Substring(RivalMark.Length)).ToList();
            if (rivals.Count == 0)
            {
                return base.DescribeMissing(missing);
            }
            string names = string.Join(" and ", rivals);
            return $"{names} {(rivals.Count > 1 ? "are" : "is")} installed, and two mods taking from the same chests at once " +
                   $"could lose or copy items. Uninstall {names} (or switch it off in the mod manager) and start the game " +
                   $"again to use {Name}. {SwitchedOffNote}";
        }

        internal override void Update()
        {
            ChestClaims.Tick();
            Toggle.Update();
            Supply.Update();
            Crafting.Update();
            Building.Update();
            Batches.Update();
        }

        internal override string LateUpdateTask => "showing the amount to craft";

        /// <summary>After the game drew the crafting menu: the amount beside the Craft button (card B16).</summary>
        internal override void LateUpdate() => BatchPanel.Refresh();

        internal override IEnumerable<Action> UndoSteps =>
            new Action[] { Supply.Cancel, Crafting.Reset, Batches.Reset, BatchPanel.Remove, Building.Reset, Stations.Reset, Display.PutBackAll, ChestClaims.ReleaseHolds };
    }
}
