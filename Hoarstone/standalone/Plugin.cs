using BepInEx;
using Jotunn.Utils;
using QoLMods.HoarstoneGround;
using QoLMods.Kit;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// Hoarstone's single release: the module (src\Module.cs) as its own plugin, with its own settings file
    /// (BepInEx\config\modprojects.hoarstone.cfg). When miikeskii's QoL Pack is installed and holds this module, this
    /// plugin stands aside and says so in the log. The bundle's project never compiles this folder.
    /// </summary>
    [BepInPlugin(Module.Guid, Module.Name, Module.Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(Bundle.Guid, BepInDependency.DependencyFlags.SoftDependency)]   // so the bundle starts first, if it's there
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ModuleHost host;

        /// <summary>Whether this release runs Hoarstone and it's switched on, for another module's single release (the
        /// Hoarstone ground part, Ground.OnProperty). Permanent, as a setting's key is.</summary>
        public bool GroundOn => host != null && Ground.Local != null && Ground.Local.On;

        /// <summary>Whether a point lies inside a settlement's shape of the given layers (Ground.InsideMethod). Permanent.</summary>
        public bool GroundInside(Vector3 point, int layers) => GroundOn && Ground.Local.Inside(point, layers);

        /// <summary>The nearest Hoarstone within the distance (Ground.StoneMethod). Permanent.</summary>
        public bool GroundNearestStone(Vector3 from, float within, out Vector3 stone)
        {
            stone = from;
            return GroundOn && Ground.Local.NearestStone(from, within, out stone);
        }

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
