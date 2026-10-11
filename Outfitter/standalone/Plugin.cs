using BepInEx;
using Jotunn.Utils;
using QoLMods.Kit;

namespace Outfitter
{
    /// <summary>
    /// Outfitter's single release: the module (src\Module.cs) as its own plugin, with its own settings file
    /// (BepInEx\config\modprojects.outfitter.cfg). When miikeskii's QoL Pack is installed and holds this module, this
    /// plugin stands aside and says so in the log. The bundle's project never compiles this folder.
    /// </summary>
    [BepInPlugin(Module.Guid, Module.Name, Module.Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(Bundle.Guid, BepInDependency.DependencyFlags.SoftDependency)]   // so the bundle starts first, if it's there
    [BepInDependency(Eaqs.Guid, BepInDependency.DependencyFlags.SoftDependency)]     // EquipmentAndQuickSlots, if it's there, starts first
    // Mods with inventory rows or slots of their own: soft, so they've started when Outfitter looks (it stays off beside them)
    [BepInDependency(Module.AzuExtendedPlayerInventory, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Module.ExtraSlots, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Module.ExtendedPlayerInventory, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Module.ComfyQuickSlots, BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ModuleHost host;

        private void Awake()
        {
            host = ModuleHost.RunAlone(this, new Module(), Logger);
        }

        private void Update()
        {
            host?.Update();
        }

        private void LateUpdate()
        {
            host?.LateUpdate();
        }
    }
}
