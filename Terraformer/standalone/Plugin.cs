using BepInEx;
using Jotunn.Utils;
using QoLMods.Kit;

namespace Terraformer
{
    /// <summary>
    /// Terraformer's single release: the module (src\Module.cs) as its own plugin, with its own settings file
    /// (BepInEx\config\modprojects.terraformer.cfg). When miikeskii's QoL Pack is installed and holds this module, this
    /// plugin stands aside and says so in the log. The bundle's project never compiles this folder.
    /// </summary>
    [BepInPlugin(Module.Guid, Module.Name, Module.Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(Bundle.Guid, BepInDependency.DependencyFlags.SoftDependency)]   // so the bundle starts first, if it's there
    [BepInDependency(Module.AdvancedTerrainModifiers, BepInDependency.DependencyFlags.SoftDependency)]   // so it has started when Terraformer looks, and Terraformer stays off
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
