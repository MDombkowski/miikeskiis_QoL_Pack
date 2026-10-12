using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace CraftFromChests
{
    /// <summary>
    /// MyLittleUI's Multicraft (shudnal's MyLittleUI, 1.2.29 read on 2026-10-11): its own + and - beside the Craft button,
    /// in the same place as ours, and a queue that presses Craft once per item. Both at once would sit on top of each other
    /// and press Craft twice. So while "Craft an amount" is on, with "Hide MyLittleUI's Multicraft" on (the default), its
    /// private check MultiCraft.CanQueue answers no (a postfix), which is its own way of standing aside: its panel hides,
    /// the Craft button gets its own width back, and its queue never starts. Its settings file is never touched. With that
    /// setting off, ours stays hidden while its Multicraft is switched on (its public IsMulticraftEnabled), and the label
    /// and runs are its. MyLittleUI must have started before this mod for the postfix to go on (a soft dependency in the
    /// single release and the bundle); if it started after, or a newer MyLittleUI renamed what this reads, ours stands
    /// aside (always, when whether its Multicraft is on can't be read), with one line in the log naming what failed.
    /// </summary>
    internal static class OtherMulticraft
    {
        private const string TypeName = "MyLittleUI.MultiCraft";

        /// <summary>Set once the postfix on its CanQueue went on.</summary>
        internal static bool Patched;

        /// <summary>Why the postfix didn't go on, in words for the log (null: it did, or nothing was tried).</summary>
        internal static string NotPatchedWhy;

        private static bool looked;
        private static bool installed;
        private static PropertyInfo enabled;
        private static bool warned;
        private static int frame = -1;
        private static bool asideThisFrame;

        /// <summary>Its MultiCraft class, or null while MyLittleUI isn't running (or has changed).</summary>
        internal static Type Find()
        {
            if (!Chainloader.PluginInfos.TryGetValue(Module.MyLittleUI, out BepInEx.PluginInfo info) || info.Instance == null)
            {
                return null;
            }
            return info.Instance.GetType().Assembly.GetType(TypeName, false);
        }

        /// <summary>Ours hides its Multicraft now: the module and both settings are on (read by the postfix).</summary>
        internal static bool HidesTheirs =>
            Module.IsOn && GameHooks.Batches && Settings.Batches != null && Settings.Batches.Value && Settings.HideMyLittleUI.Value;

        /// <summary>
        /// Ours stands aside for MyLittleUI's Multicraft, so the two never show or press Craft together: while ours doesn't
        /// hide it (the setting is off, or the postfix couldn't go on), whenever its Multicraft is on, and always if whether
        /// it's on can't be read. Read once a frame; says once in the log why, naming what failed.
        /// </summary>
        internal static bool OursStandsAside
        {
            get
            {
                if (frame != UnityEngine.Time.frameCount)
                {
                    frame = UnityEngine.Time.frameCount;
                    asideThisFrame = Aside();
                }
                return asideThisFrame;
            }
        }

        private static bool Aside()
        {
            LookOnce();
            if (!installed)
            {
                return false;
            }
            bool hides = Settings.HideMyLittleUI.Value;
            if (Patched && hides)
            {
                return false;   // its CanQueue says no: its panel hides and its queue stays idle
            }
            bool aside = enabled == null || (bool)enabled.GetValue(null, null);
            if (aside && !warned)
            {
                warned = true;
                string why = hides ? NotPatchedWhy ?? "its Multicraft couldn't be hidden" : null;
                string unknown = enabled == null ? "whether its Multicraft is on can't be read (IsMulticraftEnabled not found)" : null;
                string reasons = why != null && unknown != null ? why + ", and " + unknown : why ?? unknown;
                if (reasons != null)
                {
                    Module.Log.LogWarning($"MyLittleUI: {reasons}. So that two amounts never sit beside the Craft button, \"Craft an amount\" " +
                                          (enabled == null ? "stays hidden while MyLittleUI is installed." : "stays hidden while MyLittleUI's Multicraft is on."));
                }
            }
            return aside;
        }

        // Looks MyLittleUI's members up the first time they're needed, once every mod has started.
        private static void LookOnce()
        {
            if (looked)
            {
                return;
            }
            looked = true;
            installed = Chainloader.PluginInfos.TryGetValue(Module.MyLittleUI, out BepInEx.PluginInfo info) && info.Instance != null;
            if (!installed)
            {
                return;
            }
            Type type = Find();
            enabled = type?.GetProperty("IsMulticraftEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (enabled != null && enabled.PropertyType != typeof(bool))
            {
                enabled = null;
            }
        }
    }

    [HarmonyPatch]
    internal static class QuietTheirMulticraft
    {
        private static MethodBase target;

        private static bool Prepare()
        {
            if (!Chainloader.PluginInfos.ContainsKey(Module.MyLittleUI))
            {
                // Not started yet (it starts first only where it's a soft dependency), or not installed at all.
                OtherMulticraft.NotPatchedWhy = "it started after Craft from Chests, so its Multicraft couldn't be hidden";
                target = null;
                return false;
            }
            Type type = OtherMulticraft.Find();
            MethodInfo method = type == null ? null : AccessTools.Method(type, "CanQueue", new[] { typeof(InventoryGui) });
            target = method != null && method.ReturnType == typeof(bool) ? method : null;
            if (target == null)
            {
                OtherMulticraft.NotPatchedWhy = type == null
                    ? "its MultiCraft class wasn't found (MyLittleUI has changed)"
                    : "its MultiCraft.CanQueue wasn't found (MyLittleUI has changed)";
            }
            return target != null;
        }

        private static MethodBase TargetMethod() => target;

        // Keeps a failure to this one class (as Bar None's Broken.Patching does): the kit applies the module's classes in
        // one go and would take them all off at the first failure.
        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony)
        {
            if (ex == null)
            {
                if (target != null)
                {
                    OtherMulticraft.Patched = true;
                }
                return null;
            }
            OtherMulticraft.Patched = false;
            OtherMulticraft.NotPatchedWhy = "the patch on its MultiCraft.CanQueue failed (see the warning above)";
            try
            {
                if (original != null && harmony != null)
                {
                    harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
                }
            }
            catch (Exception)
            {
                // Taking the record back can fail the same way; ours stands aside for MyLittleUI's either way.
            }
            try
            {
                Module.Log.LogWarning($"MyLittleUI's Multicraft couldn't be hidden: the patch on its MultiCraft.CanQueue failed.\n{ex}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't stop the kit's patching.
            }
            return null;
        }

        private static void Postfix(ref bool __result)
        {
            if (__result && OtherMulticraft.HidesTheirs)
            {
                __result = false;
            }
        }
    }
}
