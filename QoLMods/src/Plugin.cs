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
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal const string Version = "0.6.4";

        private readonly List<ModuleHost> hosts = new List<ModuleHost>();

        // Every module, in the order their sections appear in the settings.
        private static IEnumerable<QoLModule> Modules() => new QoLModule[]
        {
            new XPortalMapPicker.Module(),
            new MapZoom.Module(),
            new TraderCircles.Module(),
            new AddToCart.Module(),
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
