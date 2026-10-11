using System;
using System.Runtime.CompilerServices;
using BepInEx;

namespace MapViewRules
{
    /// <summary>
    /// The configuration manager, through Jötunn's ConfigManagerUtils (Jötunn 2.21 added it, 2.23 taught it shudnal's; the
    /// package asks for 2.30.2). Reached only through these small methods, each compiled on its own and its failure caught,
    /// so with an older Jötunn installed by hand only the fold-outs' asking and the title rows' colours are lost, never the
    /// module.
    /// </summary>
    internal static class ManagerLink
    {
        private static bool broken;

        /// <summary>What asks the manager to rebuild its list, or null where Jötunn can't.</summary>
        internal static Action Rebuild()
        {
            try
            {
                return RebuildFromJotunn();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The manager's plugin, or null without one (or without a Jötunn that finds it).</summary>
        internal static BaseUnityPlugin Plugin()
        {
            if (broken)
            {
                return null;
            }
            try
            {
                return PluginFromJotunn();
            }
            catch (Exception)
            {
                broken = true;
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Action RebuildFromJotunn() => Jotunn.Utils.ConfigManagerUtils.BuildSettingList;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static BaseUnityPlugin PluginFromJotunn() => Jotunn.Utils.ConfigManagerUtils.Plugin;
    }
}
