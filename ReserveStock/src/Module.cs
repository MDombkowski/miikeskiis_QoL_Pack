using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Chests;
using QoLMods.Kit;

namespace ReserveStock
{
    /// <summary>
    /// Reserve Stock as a module of miikeskii's QoL Pack: a chest can be reserved, with Left Alt + E while pointing at it
    /// or a button in its window, and quick stack (the pack's own StoreStash, and HexQuickStackStorage) and the pack's own
    /// mods that take from chests (Craft from Chests, Add to Cart) then leave it alone. The mark is saved on the chest itself,
    /// so it works for every player running the pack, and lasts with the world. Client-side only. It carries the chest
    /// service in its own parts\ folder, which already honours the mark (ChestReserve). The same code runs in the bundle
    /// and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md. Card A12.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.reservestock";

        internal const string Name = "Reserve Stock";

        internal const string Version = "0.1.13";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "ReserveStock";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off: no button in the chest window, no key, no hover line, and HexQuickStackStorage's " +
            "quick stack uses reserved chests again. The chests keep their mark, and StoreStash, Craft from Chests and Add to " +
            "Cart still leave them alone. On brings it all back.";

        internal override string SwitchedOffNote =>
            "Reserved chests keep their mark: StoreStash, Craft from Chests and Add to Cart still leave them alone, but " +
            "HexQuickStackStorage's quick stack uses them.";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        /// <summary>This module, as the chest service knows it: its claims of a chest for the key.</summary>
        internal static readonly ChestUser Chests = new ChestUser(Name, () => Log, Guard, () => IsOn);

        internal override string UpdateTask => "reserving chests";

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            ChestHooks.Resolve(missing, Chests);
            GameHooks.Resolve(missing, missingOptional);
            Hex.Resolve(missingOptional);
        }

        internal override string ReadyMessage => $"{Name} {Version} is ready. {Hex.Status}";

        internal override void Update()
        {
            ChestClaims.Tick();
            Mark.Update();
            WindowButton.Update();
        }

        internal override IEnumerable<Action> UndoSteps =>
            new Action[] { Mark.Cancel, WindowButton.Remove, ChestClaims.ReleaseHolds };
    }
}
