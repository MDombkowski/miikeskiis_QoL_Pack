using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Chests;
using QoLMods.Kit;

namespace AddToCart
{
    /// <summary>
    /// Add to Cart as a module of miikeskii's QoL Pack: a window of everything he can build, craft or process, and of
    /// every item in the chests near him (Take), a cart with −/+ for how many of each, a running preview of the
    /// materials, and one Fetch that takes only what he's short (or, on Take, the items themselves) from those chests,
    /// claiming each chest from its owner first so nothing is lost or copied; Locate labels the chests holding an item.
    /// Since 0.3.0 each tab browses one way (the browsing philosophy): a list on the left, Group and Sort, headings, and
    /// his lists as the game's own favorite categories. Client-side only. It uses two parts of the bundle, the chest
    /// service (QoLMods\parts\Chests) and the catalogue (QoLMods\parts\Catalogue: families, kinds, biomes, orders, the
    /// browsing rules), which it carries in its own parts\ folder. The same code runs in the bundle and in its single
    /// release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.addtocart";

        internal const string Name = "Add to Cart";

        internal const string Version = "0.3.3";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "AddToCart";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off closes its window at once, and its key does nothing; on brings the key back.";

        internal override string SwitchedOffNote => "Its window doesn't open, and no chest is touched.";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        /// <summary>This module, as the chest service knows it.</summary>
        internal static readonly ChestUser Chests = new ChestUser(Name, () => Log, Guard, () => IsOn);

        internal override string UpdateTask => "running the cart";

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            ChestHooks.Resolve(missing);
            Favorites.Resolve(missingOptional);
        }

        internal override void Update()
        {
            ChestClaims.Tick();
            CartWindow.Update();
        }

        internal override string LateUpdateTask => "placing the Locate labels";

        internal override void LateUpdate() => Locator.LateUpdate();

        internal override IEnumerable<Action> UndoSteps => new Action[] { CartWindow.Close, ChestClaims.ReleaseHolds, Locator.Stop };
    }
}
