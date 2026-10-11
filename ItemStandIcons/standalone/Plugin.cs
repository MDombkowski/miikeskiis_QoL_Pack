using BepInEx;
using Jotunn.Utils;
using QoLMods.Kit;

namespace ItemStandIcons
{
    /// <summary>
    /// Item Stand Icons' single release: the module (src\Module.cs) as its own plugin, with its own settings file
    /// (BepInEx\config\modprojects.itemstandicons.cfg). When miikeskii's QoL Pack is installed and holds this module, this
    /// plugin stands aside and says so in the log. The bundle's project never compiles this folder.
    /// </summary>
    [BepInPlugin(Module.Guid, Module.Name, Module.Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(Bundle.Guid, BepInDependency.DependencyFlags.SoftDependency)]   // so the bundle starts first, if it's there
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ModuleHost host;

        /// <summary>Whether its marks show now (it runs, "Show the marks" is on, its key held): read by Add to Cart's single
        /// release, which draws the same marks as this one (the item stands part's StandShare.ShowsMarksProperty).</summary>
        public bool ShowsMarks => host != null && Settings.Showing;

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
