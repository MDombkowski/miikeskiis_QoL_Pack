using System;
using System.Reflection;
using HarmonyLib;

// The patches of following (card B25) and "Return to base" (card B26) on E and the hover texts of a tame and its saddle,
// in the module's way (Patches.cs' header): each target looked up in GameHooks, a Cleanup per class, the game's parameters
// by position, every body asking its part's switch first and working inside Module.Guard.
namespace HouseTrained
{
    /// <summary>E on a tame: with "Return to base"'s key held at a tame off a Hoarstone's ground, the game's E is held back
    /// (the key's own press sends it home); else a chosen tame the game doesn't command is commandable for this one call,
    /// so the game's own Interact pets it and sends its follow-or-stay command, and is put back after it, even if the
    /// game's method throws.</summary>
    [HarmonyPatch]
    internal static class InteractPatch
    {
        private static readonly Action work = Work;
        private static Tameable tameable;
        private static Humanoid user;
        private static bool skip;
        private static bool command;

        private static bool Prepare() => GameHooks.TameableInteract != null && !Broken.Is(Feature.TameInteract);

        private static MethodBase TargetMethod() => GameHooks.TameableInteract;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.TameInteract, original, ex, harmony);

        // __0: Humanoid user, __1: bool hold, __2: bool alt (the game's rename).
        private static bool Prefix(Tameable __instance, Humanoid __0, bool __1, bool __2, ref bool __result, out bool __state)
        {
            __state = false;
            if (!Parts.ModuleOn || __instance == null)
            {
                return true;
            }
            tameable = __instance;
            user = __0;
            skip = false;
            command = false;
            Module.Guard("E on a tame", work);
            tameable = null;
            user = null;
            if (skip)
            {
                __result = false;
                return false;
            }
            if (command && !__1 && !__2 && !__instance.m_commandable)
            {
                __instance.m_commandable = true;
                Following.Opened = __instance;
                __state = true;
            }
            return true;
        }

        private static void Work()
        {
            skip = Ground.SkipGameUse(tameable, user);
            command = !skip && Following.CommandsHere(tameable);
        }

        private static Exception Finalizer(Tameable __instance, bool __state, Exception __exception)
        {
            if (__state && __instance != null)
            {
                __instance.m_commandable = false;
                Following.Opened = null;
            }
            return __exception;
        }
    }

    /// <summary>A tame's hover text, after the game: E's word (Follow or Stay) and, off a Hoarstone's ground, "Return to
    /// base"'s key.</summary>
    [HarmonyPatch]
    internal static class TameHoverPatch
    {
        private static readonly Action work = Work;
        private static Tameable tameable;
        private static string text;

        private static bool Prepare() => GameHooks.TameableHover != null && !Broken.Is(Feature.TameHover);

        private static MethodBase TargetMethod() => GameHooks.TameableHover;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.TameHover, original, ex, harmony);

        private static void Postfix(Tameable __instance, ref string __result, bool __runOriginal)
        {
            if (!__runOriginal || !Parts.ModuleOn || __instance == null || string.IsNullOrEmpty(__result))
            {
                return;
            }
            tameable = __instance;
            text = __result;
            Module.Guard("a tame's hover text", work);
            __result = text;
            tameable = null;
            text = null;
        }

        private static void Work() => text = Ground.ReturnLine(tameable, Following.Hover(tameable, text));
    }

    /// <summary>E on a saddle: held back while House Trained's key for it is held (follow or stay, "Return to base"), so
    /// the key's own press acts instead of a ride.</summary>
    [HarmonyPatch]
    internal static class SaddleInteractPatch
    {
        private static readonly Action work = Work;
        private static Sadle saddle;
        private static Humanoid user;
        private static bool skip;

        private static bool Prepare() => GameHooks.SadleInteract != null && !Broken.Is(Feature.SaddleInteract);

        private static MethodBase TargetMethod() => GameHooks.SadleInteract;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SaddleInteract, original, ex, harmony);

        // __0: Humanoid user.
        private static bool Prefix(Sadle __instance, Humanoid __0, ref bool __result)
        {
            if ((!Parts.FollowOn && !Parts.KeepToGroundOn) || __instance == null)
            {
                return true;
            }
            saddle = __instance;
            user = __0;
            skip = false;
            Module.Guard("E on a saddle", work);
            saddle = null;
            user = null;
            if (!skip)
            {
                return true;
            }
            __result = false;
            return false;
        }

        private static void Work()
        {
            Tameable tameable = saddle.GetTameable();
            skip = Following.SkipSaddle(saddle, user) || (tameable != null && Ground.SkipGameUse(tameable, user));
        }
    }

    /// <summary>A saddle's hover text, after the game: House Trained's keys there (follow or stay, "Return to base").</summary>
    [HarmonyPatch]
    internal static class SaddleHoverPatch
    {
        private static readonly Action work = Work;
        private static Sadle saddle;
        private static string text;

        private static bool Prepare() => GameHooks.SadleHover != null && !Broken.Is(Feature.SaddleHover);

        private static MethodBase TargetMethod() => GameHooks.SadleHover;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SaddleHover, original, ex, harmony);

        private static void Postfix(Sadle __instance, ref string __result, bool __runOriginal)
        {
            if (!__runOriginal || (!Parts.FollowOn && !Parts.KeepToGroundOn) || __instance == null || string.IsNullOrEmpty(__result))
            {
                return;
            }
            saddle = __instance;
            text = __result;
            Module.Guard("a saddle's hover text", work);
            __result = text;
            saddle = null;
            text = null;
        }

        private static void Work()
        {
            text = Following.SaddleHover(saddle, text);
            Tameable tameable = saddle.GetTameable();
            if (tameable != null)
            {
                text = Ground.ReturnLine(tameable, text);
            }
        }
    }
}
