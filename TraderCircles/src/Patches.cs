using System.Reflection;
using HarmonyLib;
using UnityEngine;

// The one Harmony patch. Its target was looked up and checked in GameHooks before anything was patched.
namespace TraderCircles
{
    /// <summary>
    /// Catches the server's answers to this mod's own question before the game sees them. The game's handler would turn
    /// each answer into a saved pin, show a message and turn the character's head (Game.RPC_DiscoverLocationResponse);
    /// for an answer carrying this mod's tag it never runs. Answers to anyone else's question (a vegvisir, another mod)
    /// pass through untouched. It catches a tagged answer even while the mod is switched off, so an answer still on its
    /// way when the switch goes off can't become a pin either. It runs first, before any other mod's patch there.
    /// </summary>
    [HarmonyPatch]
    internal static class ReplyCatchPatch
    {
        private static MethodBase TargetMethod() => GameHooks.LocationResponse;

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(string pinName, Vector3 pos)
        {
            if (!ServerQuestion.IsOurs(pinName))
            {
                return true;
            }
            Module.Guard("receiving the server's answer", () => ServerQuestion.Receive(pinName, pos));
            return false;
        }
    }
}
