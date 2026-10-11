using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// The Harmony patches: prefixes and postfixes only, so a part switched off changes nothing (his thought of 2026-09-29).
// Each target was looked up in GameHooks first; a patch whose target wasn't found is skipped (Prepare), and only its own
// part stays off. The smoke and projectile patches aren't applied at all while the JereKuusela mod each stands aside for is
// installed, so a broken patch of that mod on the same method can't touch them.
// Every class has a Cleanup (Broken.Patching): if its patch can't be applied (another mod's broken patch on the same method
// makes every later patch of it fail, in HarmonyX), only its own feature is lost, instead of the kit taking every class off,
// the sky's with them.
// The game's own parameters are taken by position (__0, __1), never by name, so a renamed parameter in a game update can't
// stop a patch from being applied.
// Every body asks its part's switch first. The reach, smoke, projectile and item work runs inside Module.Guard (the first
// error switches those parts off for the session). The sky part runs inside its own guard instead (Broken.Running): an error
// elsewhere must never switch it off, since caged crops die at ripening without it; so the module keeps its patches after an
// error (Module.KeepPatchesAfterError), and every other patch does nothing then.
// A postfix that writes the game's state steps back when another mod's prefix skipped the game's own method
// (__runOriginal false): that mod has decided.
namespace BarNone
{
    /// <summary>Reach: after the game picks what he points at, pass listed pieces. It runs every frame, so its work is a
    /// delegate made once, fed through fields (no allocation per frame).</summary>
    [HarmonyPatch]
    internal static class FindHoverObjectPatch
    {
        private static readonly Action work = Work;
        private static Player player;
        private static GameObject hovered;
        private static Character creature;

        private static bool Prepare() => GameHooks.FindHoverObject != null;

        private static MethodBase TargetMethod() => GameHooks.FindHoverObject;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.Reach, original, ex, harmony);

        // __0: out GameObject hover, __1: out Character hoverCreature.
        private static void Postfix(Player __instance, ref GameObject __0, ref Character __1)
        {
            if (!ReachPart.On)
            {
                return;
            }
            player = __instance;
            hovered = __0;
            creature = __1;
            Module.Guard("reaching through a piece", work);
            __0 = hovered;
            __1 = creature;
            player = null;
            hovered = null;
            creature = null;
        }

        private static void Work() => ReachPart.AfterFindHoverObject(player, ref hovered, ref creature);
    }

    /// <summary>Smoke: each new puff gets its helper while the smoke part is on.</summary>
    [HarmonyPatch]
    internal static class SmokeAwakePatch
    {
        private static readonly Action work = Work;
        private static Smoke puff;

        private static bool Prepare() => GameHooks.SmokeAwake != null && !Rivals.SmokeStandsAside;

        private static MethodBase TargetMethod() => GameHooks.SmokeAwake;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SmokePuffs, original, ex, harmony);

        private static void Postfix(Smoke __instance)
        {
            if (!SmokePart.PuffsOn)
            {
                return;
            }
            puff = __instance;
            Module.Guard("letting a new puff of smoke through pieces", work);
            puff = null;
        }

        private static void Work() => SmokePart.AfterSmokeAwake(puff);
    }

    /// <summary>Items: each new loose item gets its helper while the items part is on.</summary>
    [HarmonyPatch]
    internal static class ItemDropAwakePatch
    {
        private static readonly Action work = Work;
        private static ItemDrop item;

        private static bool Prepare() => GameHooks.ItemsReady;

        private static MethodBase TargetMethod() => GameHooks.ItemDropAwake;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.Items, original, ex, harmony);

        private static void Postfix(ItemDrop __instance)
        {
            if (!ItemPart.On)
            {
                return;
            }
            item = __instance;
            Module.Guard("letting a new item fall through pieces", work);
            item = null;
        }

        private static void Work() => ItemPart.AfterItemDropAwake(item);
    }

    /// <summary>Smoke: a fire's clear space above it, with listed pieces left out.</summary>
    [HarmonyPatch]
    internal static class CheckUnderTerrainPatch
    {
        private static readonly Action work = Work;
        private static Fireplace fire;

        private static bool Prepare() => GameHooks.FireReady && !Rivals.SmokeStandsAside;

        private static MethodBase TargetMethod() => GameHooks.CheckUnderTerrain;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.Fires, original, ex, harmony);

        private static void Postfix(Fireplace __instance, bool __runOriginal)
        {
            if (!__runOriginal || !SmokePart.FiresOn)
            {
                return;
            }
            fire = __instance;
            Module.Guard("testing the space above a fire", work);
            fire = null;
        }

        private static void Work() => SmokePart.AfterCheckUnderTerrain(fire);
    }

    /// <summary>Projectiles: a hit on a listed piece skipped, so the game's own loop tries the next one. Every hit of every
    /// projectile comes through here, so its work is a delegate made once, fed through fields (no allocation per hit).
    /// First among the prefixes, so another mod's prefix can see the skip (HarmonyX still runs every prefix and postfix of
    /// other mods: only the game's own hit is skipped).</summary>
    [HarmonyPatch]
    internal static class ProjectileOnHitPatch
    {
        private static readonly Action work = Work;
        private static Projectile projectile;
        private static Collider hit;
        private static bool runGame;

        private static bool Prepare() => GameHooks.ProjectilesReady && !Rivals.ProjectilesStandAside;

        private static MethodBase TargetMethod() => GameHooks.ProjectileOnHit;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.Projectiles, original, ex, harmony);

        // __0: Collider collider.
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Projectile __instance, Collider __0)
        {
            if (__0 == null || !ProjectilePart.Watching)
            {
                return true;
            }
            projectile = __instance;
            hit = __0;
            runGame = true;
            Module.Guard("letting a projectile through a piece", work);
            projectile = null;
            hit = null;
            return runGame;
        }

        private static void Work() => runGame = ProjectilePart.BeforeOnHit(projectile, hit);
    }

    /// <summary>Sky: a plant's roof, with listed pieces open to the sky. Asked several times a second for every plant
    /// near a player, so it allocates nothing.</summary>
    [HarmonyPatch]
    internal static class HaveRoofPatch
    {
        private static bool Prepare() => GameHooks.HaveRoof != null;

        private static MethodBase TargetMethod() => GameHooks.HaveRoof;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SkyRoof, original, ex, harmony);

        private static void Postfix(Plant __instance, ref bool __result, bool __runOriginal)
        {
            if (!__result || !__runOriginal || !SkyPart.PlantsOn)
            {
                return;
            }
            try
            {
                __result = SkyPart.RoofRemains(__instance);
            }
            catch (Exception error)
            {
                Broken.Running(Feature.SkyRoof, "letting the sky through to a plant", error);
            }
        }
    }

    /// <summary>Sky: a plant's growing space, with listed pieces left out (its own switch).</summary>
    [HarmonyPatch]
    internal static class HaveGrowSpacePatch
    {
        private static bool Prepare() => GameHooks.HaveGrowSpace != null;

        private static MethodBase TargetMethod() => GameHooks.HaveGrowSpace;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SkySpace, original, ex, harmony);

        private static void Postfix(Plant __instance, ref bool __result, bool __runOriginal)
        {
            if (__result || !__runOriginal || !SkyPart.SpaceOn)
            {
                return;
            }
            try
            {
                __result = SkyPart.HasSpace(__instance);
            }
            catch (Exception error)
            {
                Broken.Running(Feature.SkySpace, "leaving listed pieces out of a plant's space", error);
            }
        }
    }

    /// <summary>Sky: a beehive's or birds' nest's cover, with listed pieces left out (its own switch).</summary>
    [HarmonyPatch]
    internal static class BeehiveFreeSpacePatch
    {
        private static bool Prepare() => GameHooks.BeehivesReady;

        private static MethodBase TargetMethod() => GameHooks.BeehiveFreeSpace;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SkyBeehives, original, ex, harmony);

        private static void Postfix(Beehive __instance, ref bool __result, bool __runOriginal)
        {
            if (__result || !__runOriginal || !SkyPart.BeehivesOn || __instance.m_maxCover <= 0f || __instance.m_coverPoint == null)
            {
                return;
            }
            try
            {
                __result = SkyRules.FreeSpace(SkyPart.CoverWithoutListed(__instance.m_coverPoint.position), __instance.m_maxCover);
            }
            catch (Exception error)
            {
                Broken.Running(Feature.SkyBeehives, "leaving listed pieces out of a beehive's cover", error);
            }
        }
    }

    /// <summary>Sky: the Wisp Fountain's and the Eternal Pyre's cover, with listed pieces left out (their own switch).</summary>
    [HarmonyPatch]
    internal static class WispFreeSpacePatch
    {
        private static bool Prepare() => GameHooks.WispsReady;

        private static MethodBase TargetMethod() => GameHooks.WispFreeSpace;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SkyWisps, original, ex, harmony);

        private static void Postfix(WispSpawner __instance, ref bool __result, bool __runOriginal)
        {
            if (__result || !__runOriginal || !SkyPart.WispsOn || __instance.m_maxCover <= 0f || __instance.m_coverPoint == null)
            {
                return;
            }
            try
            {
                __result = SkyRules.FreeSpace(SkyPart.CoverWithoutListed(__instance.m_coverPoint.position), __instance.m_maxCover);
            }
            catch (Exception error)
            {
                Broken.Running(Feature.SkyWisps, "leaving listed pieces out of a wisp fountain's cover", error);
            }
        }
    }

    /// <summary>Sky: a windmill's cover, with listed pieces left out (its own switch).</summary>
    [HarmonyPatch]
    internal static class WindmillCheckCoverPatch
    {
        private static bool Prepare() => GameHooks.WindmillsReady;

        private static MethodBase TargetMethod() => GameHooks.WindmillCheckCover;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SkyWindmills, original, ex, harmony);

        private static void Postfix(Windmill __instance, bool __runOriginal)
        {
            if (!__runOriginal || !SkyPart.WindmillsOn || __instance.m_propeller == null)
            {
                return;
            }
            try
            {
                float cover = SkyPart.CoverWithoutListed(__instance.m_propeller.transform.position);
                if (cover >= 0f)
                {
                    GameHooks.SetWindmillCover(__instance, cover);
                }
            }
            catch (Exception error)
            {
                Broken.Running(Feature.SkyWindmills, "leaving listed pieces out of a windmill's cover", error);
            }
        }
    }
}
