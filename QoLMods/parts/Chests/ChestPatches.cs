// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;

namespace QoLMods.Chests
{
    /// <summary>
    /// The chest service's patches, applied by ChestHooks (not by a module's host: the service is shared, so no single
    /// module's host may remove them). Not [HarmonyPatch] classes, so no host ever picks them up by mistake. Each does
    /// nothing unless the service is waiting for something or holds the chest, and none can throw into the game.
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

        /// <summary>Prefix on Container.Interact, run last and after MyLittleUI's (ChestHooks): his own press of E on a chest the service has asked for makes
        /// the answer his, so the chest's window opens as usual. Not when an earlier prefix has stopped the game's own
        /// open request (MyLittleUI's rename on Shift+E), or no answer of his is coming. A chest another player marked as
        /// theirs and keeps closed to others (card A80, ChestAccess) isn't asked for at all: his press only says whose it
        /// is, and counts as used, as the game's own "can't open" does.</summary>
        internal static bool InteractPrefix(Container __instance, bool hold, bool __runOriginal, ref bool __result)
        {
            if (hold || !__runOriginal)
            {
                return true;
            }
            if (ChestAccess.RefusesOpen(__instance))
            {
                __result = true;
                return false;
            }
            ChestClaims.Forget(__instance);
            return true;
        }

        /// <summary>
        /// Prefix, run first, on the game's three requests for a chest (Container.RPC_RequestOpen, RPC_RequestStack and
        /// RPC_RequestTakeAll, each (long uid, long playerID), run on the chest's owner): another player's request for a
        /// chest the service holds is dropped (ChestClaims.Drop), as the game drops one that reaches a computer no longer
        /// owning the chest; his next press after the hold gets it. Every other request goes to the game as usual.
        /// Positional arguments, so a renamed parameter can't stop the patch from applying.
        /// </summary>
        internal static bool RequestPrefix(Container __instance, long __0)
        {
            bool drop = ChestClaims.Drop(__instance, __0);
            DroppedNow = drop ? __instance : null;
            return !drop;
        }

        /// <summary>The chest whose request RequestPrefix, which runs first, has just dropped (null: the last one went to the
        /// game), for MultiUserChestPrefix, which runs after it in the same call.</summary>
        internal static Container DroppedNow;

        /// <summary>
        /// Prefix on MultiUserChest's own prefixes on the open and stack requests (ChestHooks.AtFirstTurn), whose first
        /// argument is the chest: skipped, answering "skip the game's method" as they would, when the service's prefix has
        /// just dropped that request during a hold. They don't check __runOriginal, so without this they answered the
        /// request anyway: a "yes" without the chest, or the chest handed over during the hold.
        /// </summary>
        internal static bool MultiUserChestPrefix(Container __0, ref bool __result)
        {
            if (__0 != null && ReferenceEquals(DroppedNow, __0))
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
