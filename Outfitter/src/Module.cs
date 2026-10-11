using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Outfitter.Equip;
using Outfitter.Loadouts;
using QoLMods.Chests;
using QoLMods.ItemMarks;
using QoLMods.Kit;

namespace Outfitter
{
    /// <summary>
    /// Outfitter as a module of miikeskii's QoL Pack (card A34, his Loadouts idea of 2026-09-26 and 2026-09-29; his name
    /// for it): saved loadouts of the cells he picks, kept in their places counted from the bottom of his inventory, and
    /// put on in one go from a window of its own: what the loadout needs comes from the chests near him, what he wore goes
    /// away where his chests' setups say, in one of his three modes, instantly; the same loadout again tops it up; share
    /// codes for friends; with or without EquipmentAndQuickSlots. Loadout Stands (card A46, runbook job 30) is built
    /// inside it, under src\Stands (his pick, 2026-10-05). Client-side only. It uses three parts of the bundle, the chest
    /// service (QoLMods\parts\Chests), the item marks (QoLMods\parts\ItemMarks: his favourites) and the catalogue
    /// (QoLMods\parts\Catalogue: reading chests' setups), which it carries in its own parts\ folder. The same code runs in
    /// the bundle and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.outfitter";

        internal const string Name = "Outfitter";

        internal const string Version = "0.5.3";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "Outfitter";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        /// <summary>Its parts are sections of their own in the settings file ("Outfitter - Equipping" …), listed under the
        /// one heading in the window (Folds).</summary>
        internal override bool OneSectionInBundle => false;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off closes its window at once, its keys and picks do nothing, and no chest is touched; " +
            "your saved loadouts stay saved. On brings it all back.";

        internal override string SwitchedOffNote => "Its window doesn't open, and no chest is touched. Saved loadouts stay saved.";

        internal override string UpdateTask => "running its window and the equip under way";

        internal override string LateUpdateTask => "drawing its marks after each frame";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>A warning in the module's log, or nowhere while it has no host yet.</summary>
        internal static void Warn(string text) => current?.Host?.Log?.LogWarning(text);

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        /// <summary>This module, as the chest service knows it: "Chests beyond the server's reach" is the pack's shared
        /// setting (card A69), and its range is what it asks of Far Fetched on the server (card A70).</summary>
        internal static readonly ChestUser Chests = new ChestUser(Name, () => Log, Guard, () => IsOn,
            reach: () => Settings.Range == null ? 0f : ChestSettings.RangeOf(Settings.Range));

        /// <summary>Mods that give the player's inventory rows or slots of their own (EquipmentAndQuickSlots refuses to run
        /// beside each, its fresh read §11.1): Outfitter knows only the game's rows and EquipmentAndQuickSlots' slots, so
        /// with one of these it would take armour off their slots and put it away at every equip. It stays off while one is
        /// installed, and says so (card A34, "Works with"; R1, finding 6). The bundle and the single release list each as a
        /// soft dependency, so it has started, and can be seen, by the time this module looks.</summary>
        internal const string AzuExtendedPlayerInventory = "Azumatt.AzuExtendedPlayerInventory";
        internal const string ExtraSlots = "shudnal.ExtraSlots";
        internal const string ExtendedPlayerInventory = "aedenthorn.ExtendedPlayerInventory";
        internal const string ComfyQuickSlots = "com.bruce.valheim.comfyquickslots";

        private static readonly (string Guid, string Name)[] Rivals =
        {
            (AzuExtendedPlayerInventory, "AzuExtendedPlayerInventory"),
            (ExtraSlots, "ExtraSlots"),
            (ExtendedPlayerInventory, "Extended Player Inventory"),
            (ComfyQuickSlots, "ComfyQuickSlots"),
        };

        // Marks a rival in the missing list, so DescribeMissing can say what to do about it (as StoreStash marks Hex).
        private const string RivalMark = "rival:";

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override string DescribeMissing(IList<string> missing)
        {
            var rivals = new List<string>();
            var others = new List<string>();
            foreach (string each in missing)
            {
                if (each.StartsWith(RivalMark, StringComparison.Ordinal))
                {
                    rivals.Add(each.Substring(RivalMark.Length));
                }
                else
                {
                    others.Add(each);
                }
            }
            if (rivals.Count == 0)
            {
                return base.DescribeMissing(missing);
            }
            string names = string.Join(" and ", rivals);
            bool one = rivals.Count == 1;
            string text = $"{names} give{(one ? "s" : "")} your inventory rows or slots of {(one ? "its" : "their")} own, which {Name} " +
                          $"doesn't know: it would take armour off those slots and put it away at every equip, so it stays off while " +
                          $"{names} {(one ? "is" : "are")} installed.";
            if (others.Count > 0)
            {
                text += $" The game has also changed, and these parts were not found: {string.Join(", ", others)}.";
            }
            return $"{text} {SwitchedOffNote}";
        }

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            foreach ((string guid, string name) in Rivals)
            {
                if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue(guid, out BepInEx.PluginInfo info) && info?.Instance != null)
                {
                    missing.Add(RivalMark + name);
                }
            }
            ChestHooks.Resolve(missing, Chests);
            ItemMarks.Resolve(missingOptional);   // only reads his favourites: a game update that breaks marking leaves Outfitter working
            GameHooks.Resolve(missing, missingOptional);
            Eaqs.Find(Log);
            Folds.RebuildList = ManagerLink.Rebuild();   // null with a Jötunn too old to ask
            Outfit.Ask = Window.Popup.Ask;   // the equip's questions (missing items, confirm), for the window and the stands alike
            Outfit.Dismiss = Window.Popup.CloseIf;
            Stands.Stands.Resolve(missing, missingOptional);
        }

        /// <summary>Every frame while the module runs, also while switched off: the chest service's turn, the fold-outs,
        /// where loadouts are kept, the window and its pop-up (closed when switched off), the equip under way, and the
        /// stands.</summary>
        internal override void Update()
        {
            ChestClaims.Tick();
            Folds.Tick();
            Store.Tick();
            Window.LoadoutWindow.Update();
            Outfit.Tick();
            Stands.Stands.Update();
        }

        /// <summary>After an error, each step on its own (MODULES.md rule 7): the window and the pop-up closed, the drawer,
        /// his inventory's panel, the hover help and a drag forgotten, the picked cells' frames taken down, the equip under way given up (nothing half moved: each
        /// step finishes inside one frame), the chests' holds forgotten when over, and the stands' own.</summary>
        internal override IEnumerable<Action> UndoSteps
        {
            get
            {
                var steps = new List<Action>
                {
                    Window.LoadoutWindow.Close,
                    Window.Popup.Close,
                    Window.Drawer.Forget,
                    Window.InventoryPanel.Forget,
                    Window.Tips.Forget,
                    Window.FoodPanel.Forget,
                    Window.StandPanel.Forget,
                    Window.Drag.Cancel,
                    Window.StandMenu.Close,
                    Window.Picks.HideFrames,
                    Outfit.Abandon,
                    ChestClaims.ReleaseHolds,
                };
                steps.AddRange(Stands.Stands.UndoSteps);
                return steps;
            }
        }
    }
}
