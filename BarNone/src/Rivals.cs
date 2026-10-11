using BepInEx;
using BepInEx.Bootstrap;

namespace BarNone
{
    /// <summary>
    /// JereKuusela's two mods that Bar None replaces. While one of them is loaded, the matching part of Bar None stands
    /// aside, so the two never both change the same thing: Smoke_Collision for the smoke part (smoke and fires),
    /// Projectile_Collision for the projectile part. Looked for when the module starts (both are soft dependencies of the
    /// bundle and the single release, so they have started first when installed) and once more at the first frame, when
    /// every plugin has started whatever order they started in.
    /// </summary>
    internal static class Rivals
    {
        /// <summary>Smoke_Collision's plugin ID.</summary>
        internal const string SmokeCollision = "smoke_collision";

        /// <summary>Projectile_Collision's plugin ID.</summary>
        internal const string ProjectileCollision = "projectile_collision";

        private static bool checkedAtFirstFrame;

        /// <summary>Smoke_Collision is loaded: the smoke part stands aside.</summary>
        internal static bool SmokeStandsAside { get; private set; }

        /// <summary>Projectile_Collision is loaded: the projectile part stands aside.</summary>
        internal static bool ProjectilesStandAside { get; private set; }

        internal static void Check()
        {
            if (!SmokeStandsAside && Loaded(SmokeCollision))
            {
                SmokeStandsAside = true;
                Module.Warn("Smoke_Collision (JereKuusela) is installed, so Bar None's smoke part stands aside: smoke and " +
                            "fires follow that mod. Uninstall it to use Bar None's smoke part instead.");
            }
            if (!ProjectilesStandAside && Loaded(ProjectileCollision))
            {
                ProjectilesStandAside = true;
                Module.Warn("Projectile_Collision (JereKuusela) is installed, so Bar None's projectile part stands aside: " +
                            "projectiles follow that mod. Uninstall it to use Bar None's projectile part instead.");
            }
        }

        internal static void CheckOnceAtFirstFrame()
        {
            if (checkedAtFirstFrame)
            {
                return;
            }
            checkedAtFirstFrame = true;
            Check();
        }

        // Loaded: BepInEx made the plugin's instance (a plugin whose start failed has none). A plugin lives for the whole
        // game once made, so a plain reference test says it, without asking the engine.
        private static bool Loaded(string guid) =>
            Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo info) && info != null && !ReferenceEquals(info.Instance, null);
    }
}
