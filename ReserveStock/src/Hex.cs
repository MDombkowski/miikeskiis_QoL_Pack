using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using QoLMods.Chests;

namespace ReserveStock
{
    /// <summary>
    /// HexQuickStackStorage (Hex_Viking), which he and his friends run. Its quick stack to nearby chests (P, and the Q
    /// button) picks every chest through one internal method, ContainerService.CanUseContainer(Container): a postfix here
    /// makes it say no to a reserved chest. Its Place stacks and holding E, on the one open chest, run
    /// QuickStackService.QuickStack(Player, Container), which the setting "Place stacks leaves reserved chests alone too"
    /// stops (PlaceStacks). Hex has no API, so both are looked up by name when the game starts; checked against 1.4.3
    /// (2026-09-25). Without Hex, or with a Hex that has changed so they aren't found, those parts stay off and the log
    /// says why; everything else works either way. Hex updates often: check these two names on each of its updates.
    /// </summary>
    internal static class Hex
    {
        /// <summary>HexQuickStackStorage's plugin ID. The bundle and the single release list it as a soft dependency, so it
        /// has started, and can be found, when this module looks.</summary>
        internal const string Guid = "com.hex.quickstackstorage";

        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Hex's ContainerService.CanUseContainer(Container), or null.</summary>
        internal static MethodInfo CanUseContainer { get; private set; }

        /// <summary>Hex's QuickStackService.QuickStack(Player, Container): its Place stacks, or null.</summary>
        internal static MethodInfo QuickStackOne { get; private set; }

        /// <summary>For the module's ready line: what it found of Hex.</summary>
        internal static string Status { get; private set; } = string.Empty;

        internal static void Resolve(List<string> missingOptional)
        {
            CanUseContainer = null;
            QuickStackOne = null;
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) || info.Instance == null)
            {
                Status = "HexQuickStackStorage isn't running, so there's no quick stack to keep out of reserved chests.";
                return;
            }
            Assembly assembly = info.Instance.GetType().Assembly;
            string version = info.Metadata.Version.ToString();
            MethodInfo check = assembly.GetType("HexQuickStackStorage.ContainerService")?
                                       .GetMethod("CanUseContainer", AnyStatic, null, new[] { typeof(Container) }, null);
            if (check != null && check.ReturnType == typeof(bool))
            {
                CanUseContainer = check;
            }
            else
            {
                missingOptional.Add($"HexQuickStackStorage {version}'s ContainerService.CanUseContainer (that version has changed: its quick stack " +
                                    "to nearby chests still stacks into reserved chests)");
            }
            QuickStackOne = assembly.GetType("HexQuickStackStorage.QuickStackService")?
                                    .GetMethod("QuickStack", AnyStatic, null, new[] { typeof(Player), typeof(Container) }, null);
            if (QuickStackOne == null)
            {
                missingOptional.Add($"HexQuickStackStorage {version}'s QuickStackService.QuickStack(Player, Container) (with \"Place stacks leaves " +
                                    "reserved chests alone too\" on, its Place stacks still works at a reserved chest)");
            }
            Status = CanUseContainer != null
                ? $"HexQuickStackStorage {version}: its quick stack to nearby chests leaves reserved chests alone."
                : $"HexQuickStackStorage {version} has changed: its quick stack isn't kept out of reserved chests (see the warning).";
        }

        /// <summary>What Hex's check says, with the marks taken into account while the mod is on: no for a reserved chest, and
        /// no for another player's personal chest whose marker keeps other players' mods out (card A80, ChestAccess).</summary>
        internal static bool MayUse(Container chest, bool hexSays) =>
            hexSays && !(Module.IsOn && chest != null && (ChestReserve.IsReserved(chest) || !ChestAccess.ModsMayUse(chest, ChestClaims.PlayerId())));
    }

    [HarmonyPatch]
    internal static class QuickStackLeavesReservedChestsAlone
    {
        private static bool Prepare() => Hex.CanUseContainer != null;

        private static MethodBase TargetMethod() => Hex.CanUseContainer;

        // Positional: Hex's parameter name may change without its meaning changing.
        private static void Postfix(Container __0, ref bool __result)
        {
            if (!__result || !Module.IsOn)
            {
                return;
            }
            bool result = __result;
            Module.Guard("keeping quick stack out of reserved chests", () => result = Hex.MayUse(__0, result));
            __result = result;
        }
    }
}
