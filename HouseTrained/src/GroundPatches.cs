using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// The patches of "With the Hoarstone" (card B26) and the stay mark of following (card B25), in the module's way
// (Patches.cs' header). Each acts only while its part is on and Hoarstone is on (Ground.Live), on the game that runs the
// tame's AI.
namespace HouseTrained
{
    /// <summary>The idle wander (BaseAI.IdleMovement): before it, a tame off the ground it keeps to walks home instead
    /// (the game's wander skipped); after it, a tame on the ground whose walk heads off it picks again inside. Before
    /// "Tames in pens"' own patch on the same method, so its picks see the walk home.</summary>
    [HarmonyPatch]
    internal static class GroundIdlePatch
    {
        private static readonly Action before = Before;
        private static readonly Action after = After;
        private static BaseAI ai;
        private static float dt;
        private static bool run;

        private static bool Prepare() => GameHooks.IdleMovement != null && GameHooks.MoveTo != null && GameHooks.AiCharacter != null && !Broken.Is(Feature.GroundIdle);

        private static MethodBase TargetMethod() => GameHooks.IdleMovement;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.GroundIdle, original, ex, harmony);

        // __0: float dt.
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(BaseAI __instance, float __0, bool __runOriginal)
        {
            if (!__runOriginal || !Ground.Live || __instance == null)
            {
                return true;   // another mod's prefix skipped the wander: it has decided (review F12)
            }
            ai = __instance;
            dt = __0;
            run = true;
            Module.Guard("a tame's walk back to the Hoarstone's ground", before);
            ai = null;
            return run;
        }

        private static void Before() => run = Ground.BeforeIdle(ai, dt);

        [HarmonyPriority(Priority.High)]
        private static void Postfix(BaseAI __instance, bool __runOriginal)
        {
            if (!__runOriginal || !Ground.Live || __instance == null)
            {
                return;
            }
            ai = __instance;
            Module.Guard("a tame's wander on the Hoarstone's ground", after);
            ai = null;
        }

        private static void After() => Ground.AfterIdle(ai);
    }

    /// <summary>A creature's target, after the game kept or changed it (MonsterAI.UpdateTarget), and after "Tames in
    /// pens"' let-go: an enemy off the ground let go of (by the setting), a hunt kept up, an intruder on the ground hunted.</summary>
    [HarmonyPatch]
    internal static class GroundTargetPatch
    {
        private static readonly Action work = Work;
        private static MonsterAI ai;
        private static bool hear;
        private static bool see;

        private static bool Prepare() => GameHooks.UpdateTarget != null && GameHooks.GroundTargetCreature != null && GameHooks.LastKnownTargetPos != null &&
                                         GameHooks.BeenAtLastPos != null && GameHooks.TimeSinceSensed != null && GameHooks.TimeSinceAttacking != null &&
                                         GameHooks.TargetStatic != null && GameHooks.GroundTimeSinceHurt != null && GameHooks.GroundTargetInfo != null &&
                                         GameHooks.GroundAlerted != null && GameHooks.AiCharacter != null && !Broken.Is(Feature.GroundTarget);

        private static MethodBase TargetMethod() => GameHooks.UpdateTarget;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.GroundTarget, original, ex, harmony);

        // __2: out bool canHearTarget, __3: out bool canSeeTarget.
        [HarmonyPriority(Priority.Low)]
        private static void Postfix(MonsterAI __instance, ref bool __2, ref bool __3, bool __runOriginal)
        {
            if (!__runOriginal || !Ground.Live || __instance == null)
            {
                return;
            }
            ai = __instance;
            hear = __2;
            see = __3;
            Module.Guard("a tame's target on the Hoarstone's ground", work);
            __2 = hear;
            __3 = see;
            ai = null;
        }

        private static void Work() => Ground.AfterUpdateTarget(ai, ref hear, ref see);
    }

    /// <summary>The game's follow (BaseAI.Follow): with "… they may leave it while following you" off, a follower stops at
    /// the ground's edge.</summary>
    [HarmonyPatch]
    internal static class GroundFollowPatch
    {
        private static readonly Action work = Work;
        private static BaseAI ai;
        private static GameObject go;
        private static float dt;
        private static bool run;

        private static bool Prepare() => GameHooks.Follow != null && GameHooks.MoveTo != null && GameHooks.AiCharacter != null && !Broken.Is(Feature.GroundFollow);

        private static MethodBase TargetMethod() => GameHooks.Follow;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.GroundFollow, original, ex, harmony);

        // __0: GameObject go, __1: float dt.
        private static bool Prefix(BaseAI __instance, GameObject __0, float __1, bool __runOriginal)
        {
            if (!__runOriginal || !Ground.Live || Settings.GroundFollow.Value || __instance == null)
            {
                return true;
            }
            ai = __instance;
            go = __0;
            dt = __1;
            run = true;
            Module.Guard("a follower at the Hoarstone's ground's edge", work);
            ai = null;
            go = null;
            return run;
        }

        private static void Work() => run = Ground.BeforeFollow(ai, go, dt);
    }

    /// <summary>The game's command to follow or stay, after it ran on the game that looks after the tame
    /// (Tameable.RPC_Command): a tame the game doesn't command is marked as told to stay, or not (card B25).</summary>
    [HarmonyPatch]
    internal static class StayMarkPatch
    {
        private static readonly Action work = Work;
        private static Tameable tameable;

        private static bool Prepare() => GameHooks.TameableRpcCommand != null && !Broken.Is(Feature.TameStayMark);

        private static MethodBase TargetMethod() => GameHooks.TameableRpcCommand;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.TameStayMark, original, ex, harmony);

        private static bool followedBefore;

        // Whether it followed before the command, so the postfix writes only when the command changed it.
        private static void Prefix(Tameable __instance, out bool __state)
        {
            __state = false;
            if (!Parts.ModuleOn || __instance == null)
            {
                return;
            }
            try
            {
                __state = Following.FollowingHere(__instance);
            }
            catch (Exception)
            {
                // Read only: a failure leaves "followed before" false, and the postfix may write the mark once more.
            }
        }

        private static void Postfix(Tameable __instance, bool __state, bool __runOriginal)
        {
            if (!__runOriginal || !Parts.ModuleOn || __instance == null)
            {
                return;
            }
            tameable = __instance;
            followedBefore = __state;
            Module.Guard("marking a tame told to stay", work);
            tameable = null;
        }

        private static void Work() => Following.AfterCommand(tameable, followedBefore);
    }
}
