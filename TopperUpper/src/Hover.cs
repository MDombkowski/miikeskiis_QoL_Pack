using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using TopperUpper.Logic;

namespace TopperUpper
{
    /// <summary>
    /// Topper-Upper's lines in the game's own hover texts (the research's 4.2): on a light or station it feeds, whether
    /// it's fed or left alone and the key that switches it, its linked chests and the link key, and why it wasn't fed the
    /// last time it was looked at ("Resin kept: 48 near you, 50 kept", "Black Metal at its most: 121/120"); on a linked
    /// chest, which station it feeds or fills. Added last, after every other mod's (MyLittleUI replaces the game's text
    /// with its own), so they are never lost; each only with its hover setting on.
    /// </summary>
    internal static class Hover
    {
        private const string Grey = "#c8c8c8";

        /// <summary>The text with the lines for this light or station added.</summary>
        internal static string Add(Fed fed, string text)
        {
            if (fed == null || string.IsNullOrEmpty(text))
            {
                return text;
            }
            bool light = fed.IsLight;
            if (light ? !Settings.LightHover.Value || !Settings.FeedLights.Value || !Lights.Read(fed, out _)
                      : !Settings.StationHover.Value || !Settings.FeedStations.Value || !Stations.Feedable(fed))
            {
                return text;
            }
            var lines = new StringBuilder(text);
            bool kind = light ? Lights.KindSaysFed(fed) : Stations.KindSaysFed(fed);
            int mark = fed.Mark;
            bool isFed = FeedMark.IsFed(mark, kind);
            BepInEx.Configuration.KeyboardShortcut keyShortcut = light ? Settings.LightKey.Value : Settings.StationKey.Value;
            string key = Keys.Text(keyShortcut);
            string fedWord = light ? "topped up" : "fed";
            string state = isFed ? (light ? "Topped up from chests" : "Fed from chests") : mark == FeedMark.LeftAlone ? "Left alone" : $"Not {fedWord} (its kind's switch)";
            string action = isFed ? "Leave it alone" : light ? "Top it up" : "Feed it";
            if ((light ? Settings.LightKeySteps.Value : Settings.StationKeySteps.Value) == KeySteps.FedLeftAloneAndAsItsKind)
            {
                action = "Change";
            }
            lines.Append($"\n<color={Grey}>{state}</color>");
            if (keyShortcut.MainKey != UnityEngine.KeyCode.None)
            {
                lines.Append($"  [<color=yellow><b>{key}</b></color>] {action}");
            }
            if (!light)
            {
                string links = Links.Summary(fed);
                if (links.Length > 0)
                {
                    lines.Append($"\n<color={Grey}>{links}</color>");
                }
                if (Settings.LinkKey.Value.MainKey != UnityEngine.KeyCode.None)
                {
                    lines.Append($"  [<color=yellow><b>{Keys.Text(Settings.LinkKey.Value)}</b></color>] Link chests");
                }
            }
            if (isFed && !string.IsNullOrEmpty(fed.Why))
            {
                lines.Append($"\n<color={Grey}>Not {fedWord} now: {fed.Why}</color>");
            }
            return lines.ToString();
        }

        /// <summary>A linked chest's text with its line added: "Topper-Upper: an input of the Smelter".</summary>
        internal static string AddToChest(Container chest, string text)
        {
            if (!Settings.StationHover.Value || chest == null || string.IsNullOrEmpty(text))
            {
                return text;
            }
            List<KeyValuePair<Fed, bool>> linked = Links.LinkedTo(chest);
            if (linked.Count == 0)
            {
                return text;
            }
            var lines = new StringBuilder(text);
            foreach (KeyValuePair<Fed, bool> each in linked)
            {
                lines.Append($"\n<color={Grey}>Linked: {(each.Value ? "an output" : "an input")} of the {Names.Of(each.Key)}</color>");
            }
            return lines.ToString();
        }

        /// <summary>One hover postfix's work, guarded: an error costs the line, never the game's text.</summary>
        internal static string On(UnityEngine.MonoBehaviour component, string text)
        {
            if (!Module.IsOn || component == null || string.IsNullOrEmpty(text))
            {
                return text;
            }
            string result = text;
            Module.Guard("adding a hover line", () => result = Add(Registry.Of(component), text));
            return result;
        }
    }

    // The game's hover texts. Each is a postfix, last; each asks whether the mod is on first.

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
    internal static class HoverFireplace
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Fireplace __instance, ref string __result) => __result = Hover.On(__instance, __result);
    }

    [HarmonyPatch(typeof(Smelter), "OnHoverAddOre")]
    internal static class HoverSmelterOre
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Smelter __instance, ref string __result) => __result = Hover.On(__instance, __result);
    }

    [HarmonyPatch(typeof(Smelter), "OnHoverAddFuel")]
    internal static class HoverSmelterFuel
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Smelter __instance, ref string __result)
        {
            // A station with a queue shows its lines on the queue's switch; only the hot tub and the Frigid Kiln on their
            // fuel's.
            if (__instance.m_addOreSwitch == null || __instance.m_maxOre <= 0)
            {
                __result = Hover.On(__instance, __result);
            }
        }
    }

    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.GetHoverText))]
    internal static class HoverFermenter
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Fermenter __instance, ref string __result) => __result = Hover.On(__instance, __result);
    }

    [HarmonyPatch(typeof(ShieldGenerator), "OnHoverAddFuel")]
    internal static class HoverShieldGenerator
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ShieldGenerator __instance, ref string __result) => __result = Hover.On(__instance, __result);
    }

    [HarmonyPatch(typeof(CookingStation), "OnHoverFuelSwitch")]
    internal static class HoverCookingFuel
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(CookingStation __instance, ref string __result) => __result = Hover.On(__instance, __result);
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.GetHoverText))]
    internal static class HoverTurret
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Turret __instance, ref string __result) => __result = Hover.On(__instance, __result);
    }

    [HarmonyPatch(typeof(Beehive), nameof(Beehive.GetHoverText))]
    internal static class HoverBeehive
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Beehive __instance, ref string __result) => __result = Hover.On(__instance, __result);
    }

    [HarmonyPatch(typeof(SapCollector), nameof(SapCollector.GetHoverText))]
    internal static class HoverSapCollector
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(SapCollector __instance, ref string __result) => __result = Hover.On(__instance, __result);
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class HoverLinkedChest
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Container __instance, ref string __result)
        {
            if (!Module.IsOn || __instance == null || string.IsNullOrEmpty(__result) || !Settings.StationHover.Value)
            {
                return;
            }
            string text = __result;
            Module.Guard("adding a linked chest's hover line", () => text = Hover.AddToChest(__instance, text));
            __result = text;
        }
    }
}
