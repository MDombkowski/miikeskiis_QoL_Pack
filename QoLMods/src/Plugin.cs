using System.Collections.Generic;
using BepInEx;
using Jotunn.Utils;
using QoLMods.Kit;

namespace QoLMods
{
    /// <summary>
    /// miikeskii's QoL Pack: every module in one plugin, each with its own section of the bundle's settings
    /// (BepInEx\config\modprojects.qolmods.cfg) starting with its own Enabled switch. Each module runs under its own
    /// Harmony ID and its errors switch off only that module. Client-side only while every module is.
    /// How to add a module: MODULES.md.
    /// </summary>
    [BepInPlugin(Bundle.Guid, Bundle.Name, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // Other authors' mods that a module works with: soft, so a missing one switches off only that module, and listed
    // so they start before the bundle, where the module can find them.
    [BepInDependency(XPortalMapPicker.XPortalHooks.Guid, BepInDependency.DependencyFlags.SoftDependency)]   // XPortal Map Picker
    [BepInDependency(MapViewRules.Cartur.Guid, BepInDependency.DependencyFlags.SoftDependency)]             // Map View Rules: Cartur's Map Pins
    // Craft from Chests: other mods that take from chests for crafting, so they have started when it looks, and it stays off
    [BepInDependency(CraftFromChests.Module.AzuCraftyBoxes, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(CraftFromChests.Module.CraftFromContainers, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(ReserveStock.Hex.Guid, BepInDependency.DependencyFlags.SoftDependency)]                // Reserve Stock and StoreStash: HexQuickStackStorage (StoreStash stays off while it runs)
    // Portion Control: the mods that add buttons to the same split window (it stands aside for them) and CurrencyPocket
    [BepInDependency(PortionControl.Module.BetterSplitDialog, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(PortionControl.Module.Abacus, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(PortionControl.Module.CurrencyPocket, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Terraformer.Module.AdvancedTerrainModifiers, BepInDependency.DependencyFlags.SoftDependency)]  // Terraformer: AdvancedTerrainModifiers (it stays off while it runs)
    // Bar None: JereKuusela's two mods it replaces (while one runs, the matching part stands aside)
    [BepInDependency(BarNone.Rivals.SmokeCollision, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(BarNone.Rivals.ProjectileCollision, BepInDependency.DependencyFlags.SoftDependency)]
    // Outfitter: EquipmentAndQuickSlots (its slots, by its interface), and the mods with inventory rows or slots of their own
    // (it stays off beside them)
    [BepInDependency(Outfitter.Eaqs.Guid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Outfitter.Module.AzuExtendedPlayerInventory, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Outfitter.Module.ExtraSlots, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Outfitter.Module.ExtendedPlayerInventory, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Outfitter.Module.ComfyQuickSlots, BepInDependency.DependencyFlags.SoftDependency)]
    // Topper-Upper: the mods that also feed fires and stations from chests (while one runs, it feeds nothing)
    [BepInDependency(TopperUpper.Rivals.AutomaticFuel, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(TopperUpper.Rivals.LazyVikings, BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string Version = "0.45.1";

        private readonly List<ModuleHost> hosts = new List<ModuleHost>();

        // Every module, in the order their sections appear in the settings.
        private static IEnumerable<QoLModule> Modules() => new QoLModule[]
        {
            new XPortalMapPicker.Module(),
            new MapZoom.Module(),
            new TraderCircles.Module(),
            new AddToCart.Module(),
            new MapViewRules.Module(),
            new CraftFromChests.Module(),
            new ReserveStock.Module(),
            new Handyman.Module(),
            new PortionControl.Module(),
            new ComfortBuild.Module(),
            new StoreStash.Module(),
            new Terraformer.Module(),
            new ItemStandIcons.Module(),
            new UseCase.Module(),
            new BarNone.Module(),
            new HouseTrained.Module(),
            new Outfitter.Module(),
            new Grounded.Module(),
            new PowerWheel.Module(),
            new SpareKeys.Module(),
            new TopperUpper.Module(),
            new MenUI.Module(),
            new Hoarstone.Module(),
            new AFKRaidBlocker.Module(),
        };

        /// <summary>The version of the module with this id if the bundle runs it, or null if the bundle doesn't hold it or
        /// couldn't start it (then it never patched anything, and a single release can run instead). A module's single
        /// release asks this, by reflection, to know whether to stand aside.</summary>
        public string ModuleVersion(string id)
        {
            ModuleHost host = hosts.Find(each => each.Module.Id == id);
            return host != null && host.Active ? host.Module.ModuleVersion : null;
        }

        private void Awake()
        {
            var summary = new List<string>();
            foreach (QoLModule module in Modules())
            {
                ModuleHost host = ModuleHost.RunInBundle(this, module);
                hosts.Add(host);
                string state = !host.Active ? "switched off, see its warning" : host.Enabled.Value ? "on" : "off in its settings";
                summary.Add($"{module.DisplayName} {module.ModuleVersion} ({state})");
            }
            Logger.LogInfo($"{Bundle.Name} {Version}: {string.Join(", ", summary)}.");
        }

        private void Update()
        {
            foreach (ModuleHost host in hosts)
            {
                host.Update();
            }
        }

        private void LateUpdate()
        {
            foreach (ModuleHost host in hosts)
            {
                host.LateUpdate();
            }
        }
    }
}
