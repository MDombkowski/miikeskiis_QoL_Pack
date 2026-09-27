// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;

namespace QoLMods.Chests
{
    /// <summary>
    /// The chest service's patches, applied by ChestHooks (not by a module's host: the service is shared, so no single
    /// module's host may remove them). Not [HarmonyPatch] classes, so no host ever picks them up by mistake. Each does
    /// nothing unless the service is waiting for something, and none can throw into the game.
    /// </summary>
    internal static class ChestPatches
    {
        /// <summary>
        /// Prefix on Container.RPC_OpenResponse. An answer to the service's own open request is taken by the service,
        /// and the game's own handling (showing the chest's window, or "in use") is skipped. Every other answer, such as
        /// to his own press of E, goes to the game as usual. Other mods' postfixes still run: HexQuickStackStorage sorts
        /// this computer's copy of the chest, which keeps every item and is saved with it once the chest is his.
        /// </summary>
        internal static bool OpenResponsePrefix(Container __instance, bool granted)
        {
            return !ChestClaims.TakeAnswer(__instance, granted);
        }

        /// <summary>Postfix on Container.Awake: every chest joins the service's list as it comes into being.</summary>
        internal static void AwakePostfix(Container __instance)
        {
            try
            {
                ChestFinder.Register(__instance);
            }
            catch (Exception error)
            {
                ZLog.LogWarning($"[QoL Mods chest service] Couldn't note a chest: {error}");
            }
        }

        /// <summary>Prefix on Container.Interact, run last: his own press of E on a chest the service has asked for makes
        /// the answer his, so the chest's window opens as usual. Not when an earlier prefix has stopped the game's own
        /// open request (MyLittleUI's rename on Shift+E), or no answer of his is coming.</summary>
        internal static void InteractPrefix(Container __instance, bool hold, bool __runOriginal)
        {
            if (!hold && __runOriginal)
            {
                ChestClaims.Forget(__instance);
            }
        }
    }
}
