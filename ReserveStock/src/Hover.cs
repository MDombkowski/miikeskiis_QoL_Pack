using HarmonyLib;
using QoLMods.Chests;
using UnityEngine;

namespace ReserveStock
{
    /// <summary>
    /// What pointing at a chest shows: a line, Reserved, on a reserved chest; and, with its setting, the key and what it
    /// would do on every chest he could open. Added after every other mod's hover text (MyLittleUI replaces the game's
    /// with its own), so it is never lost.
    /// </summary>
    internal static class Hover
    {
        /// <summary>The Reserved line's colour, as the padlock's.</summary>
        private static readonly string ReservedColor = "#" + ColorUtility.ToHtmlStringRGB(ReserveLock.ReservedColor);

        /// <summary>The hover text with this mod's lines added, as the settings say.</summary>
        internal static string Add(Container chest, string text)
        {
            if (chest.GetType() != typeof(Container))
            {
                return text;
            }
            bool reserved = ChestReserve.IsReserved(chest);
            if (reserved && Settings.ShowHoverLine.Value)
            {
                text += $"\n<color={ReservedColor}>Reserved</color>";
            }
            // No hint while the key is cleared: it would read "[None]", and the key does nothing then.
            if (Settings.ShowKeyHint.Value && Settings.Key.Value.MainKey != KeyCode.None && Mark.Reservable(chest) &&
                Mark.WhyNot(chest, !reserved) == null)
            {
                text += $"\n[<color=yellow><b>{Settings.KeyText(Settings.Key.Value)}</b></color>] {(reserved ? "Unreserve" : "Reserve")}";
            }
            return text;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
    internal static class SayReservedWhenPointing
    {
        // Last, after MyLittleUI's, which replaces the whole text with its own.
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(Container __instance, ref string __result)
        {
            if (!Module.IsOn || __instance == null || __result == null || (!Settings.ShowHoverLine.Value && !Settings.ShowKeyHint.Value))
            {
                return;
            }
            string text = __result;
            Module.Guard("showing a chest's reserve in its hover text", () => text = Hover.Add(__instance, text));
            __result = text;
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
    internal static class KeyDoesntOpenTheChest
    {
        /// <summary>
        /// While the key is held (Left Alt + E), the game's own E doesn't open the chest: that press reserves it
        /// (Mark.Update reads the key). Before MyLittleUI's rename prefix and the chest service's, which both step aside
        /// when an earlier prefix has stopped the game's own.
        /// </summary>
        private static bool Prefix(Container __instance, Humanoid character, ref bool __result)
        {
            if (!Module.IsOn || character == null || character != Player.m_localPlayer || !Settings.Held(Settings.Key.Value) ||
                !Mark.Reservable(__instance))
            {
                return true;
            }
            __result = false;
            return false;
        }
    }
}
