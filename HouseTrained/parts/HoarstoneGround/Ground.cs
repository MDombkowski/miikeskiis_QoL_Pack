// The QoL Mods Hoarstone ground part: the one copy to edit is Games\Valheim\QoLMods\parts\HoarstoneGround\. Every mod
// that uses it carries a copy in its own parts\HoarstoneGround\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so
// each mod still builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

namespace QoLMods.HoarstoneGround
{
    /// <summary>What the Hoarstone module answers for the others: whether it's on, and where its settlements are.</summary>
    internal interface IGroundSource
    {
        /// <summary>True while the Hoarstone module runs and is switched on.</summary>
        bool On { get; }

        /// <summary>Whether a point lies inside a settlement's shape of the given layers (<see cref="Ground.WithAHoarstone"/>,
        /// <see cref="Ground.WithoutAHoarstone"/>), as the Hoarstone's spawn check decides it. Main thread only.</summary>
        bool Inside(Vector3 point, int layers);

        /// <summary>The nearest Hoarstone it knows within <paramref name="within"/> metres (level distance; its height is
        /// the point's own), among the pieces its game has seen this session.</summary>
        bool NearestStone(Vector3 from, float within, out Vector3 stone);
    }

    /// <summary>
    /// How another module (House Trained, card B26) asks the Hoarstone module (card A88) about its settlements without using
    /// its code (a module never does). In the bundle both run in one plugin, so Hoarstone hands this part its answers
    /// (<see cref="Provide"/>, from its Resolve); between single releases, the part finds Hoarstone's plugin and asks its
    /// public members, named below. Those names never change, as a setting's key never does: renaming one would quietly
    /// stop the other module's use of it. Nothing here runs by itself, and it has no patches.
    /// </summary>
    internal static class Ground
    {
        /// <summary>Hoarstone's single release's plugin ID.</summary>
        internal const string ModuleGuid = "modprojects.hoarstone";

        /// <summary>The layer of the settlements a Hoarstone stands in.</summary>
        internal const int WithAHoarstone = 1;

        /// <summary>The layer of the settlements without one (big enough for Hoarstone to count them).</summary>
        internal const int WithoutAHoarstone = 2;

        /// <summary>Hoarstone's single release's public members: <c>bool GroundOn { get; }</c>,
        /// <c>bool GroundInside(Vector3 point, int layers)</c>, <c>bool GroundNearestStone(Vector3 from, float within,
        /// out Vector3 stone)</c>. The last two answer false while it's off, as GroundOn says.</summary>
        internal const string OnProperty = "GroundOn";

        internal const string InsideMethod = "GroundInside";

        internal const string StoneMethod = "GroundNearestStone";

        /// <summary>Where the Hoarstone module stands for this plugin.</summary>
        internal enum Standing
        {
            /// <summary>Neither in this plugin nor installed as its own release.</summary>
            NotInstalled,

            /// <summary>Installed, but switched off (or stood aside, or off after an error).</summary>
            Off,

            On,
        }

        private delegate bool InsideCall(Vector3 point, int layers);

        private delegate bool StoneCall(Vector3 from, float within, out Vector3 stone);

        private static IGroundSource local;
        private static BaseUnityPlugin single;
        private static PropertyInfo singleOn;
        private static InsideCall singleInside;
        private static StoneCall singleStone;
        private static BaseUnityPlugin refused;
        private static bool toldOld;

        /// <summary>The plugin started under an ID, or null; a seam a harness replaces (it has no plugins loaded).</summary>
        internal static Func<string, BaseUnityPlugin> FindPlugin = guid =>
            Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo info) ? info?.Instance : null;

        /// <summary>A warning for the log of the module that set it last (House Trained's), said once.</summary>
        internal static Action<string> Warn { get; set; }

        /// <summary>The Hoarstone module in this plugin hands over its answers (from its Resolve).</summary>
        internal static void Provide(IGroundSource source) => local = source;

        /// <summary>The Hoarstone module in this plugin itself (for its single release's public members), or null.</summary>
        internal static IGroundSource Local => local;

        /// <summary>Where Hoarstone stands now: in this plugin, else its single release, looked for again until found
        /// (plugins start in any order; a dictionary lookup).</summary>
        internal static Standing Now
        {
            get
            {
                if (local != null)
                {
                    return Guarded(() => local.On) ? Standing.On : Standing.Off;
                }
                if (!FindSingle())
                {
                    return Standing.NotInstalled;
                }
                return Guarded(() => singleOn.GetValue(single) is bool on && on) ? Standing.On : Standing.Off;
            }
        }

        /// <summary>Whether a point lies inside a settlement of the given layers, as Hoarstone decides it; false while
        /// Hoarstone isn't on.</summary>
        internal static bool Inside(Vector3 point, int layers)
        {
            if (local != null)
            {
                return local.On && local.Inside(point, layers);
            }
            return FindSingle() && singleInside(point, layers);   // its member checks its own switch: no reflection per point
        }

        /// <summary>The nearest Hoarstone Hoarstone knows within the distance; false while Hoarstone isn't on.</summary>
        internal static bool NearestStone(Vector3 from, float within, out Vector3 stone)
        {
            stone = from;
            if (local != null)
            {
                return local.On && local.NearestStone(from, within, out stone);
            }
            return FindSingle() && singleStone(from, within, out stone);
        }

        // An answer of the other plugin that throws counts as no (its own guard will have said why).
        private static bool Guarded(Func<bool> ask)
        {
            try
            {
                return ask();
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Hoarstone's single release, found through its plugin, its three members bound once.
        private static bool FindSingle()
        {
            if (singleOn != null)
            {
                return true;
            }
            BaseUnityPlugin plugin;
            try
            {
                plugin = FindPlugin(ModuleGuid);
            }
            catch (Exception)
            {
                plugin = null;   // BepInEx not started (never in game): as not installed
            }
            if (plugin is null || ReferenceEquals(plugin, refused))   // a reference test: Unity's own == would call a plugin it never made "destroyed"
            {
                return false;
            }
            Type type = plugin.GetType();
            try
            {
                PropertyInfo on = type.GetProperty(OnProperty, BindingFlags.Instance | BindingFlags.Public);
                MethodInfo inside = type.GetMethod(InsideMethod, BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(Vector3), typeof(int) }, null);
                MethodInfo nearest = type.GetMethod(StoneMethod, BindingFlags.Instance | BindingFlags.Public, null,
                                                    new[] { typeof(Vector3), typeof(float), typeof(Vector3).MakeByRefType() }, null);
                if (on != null && on.PropertyType == typeof(bool) && inside != null && inside.ReturnType == typeof(bool) &&
                    nearest != null && nearest.ReturnType == typeof(bool))
                {
                    singleInside = (InsideCall)Delegate.CreateDelegate(typeof(InsideCall), plugin, inside);
                    singleStone = (StoneCall)Delegate.CreateDelegate(typeof(StoneCall), plugin, nearest);
                    single = plugin;
                    singleOn = on;
                    return true;
                }
            }
            catch (Exception)
            {
                // Members not as expected: an older Hoarstone, said below.
            }
            refused = plugin;
            if (!toldOld)
            {
                toldOld = true;
                Warn?.Invoke("Hoarstone (its own release) is too old to say where its settlements are, so tames can't keep to its " +
                             "ground; update it, or use miikeskii's QoL Pack.");
            }
            return false;
        }
    }
}
