using System;
using System.Reflection;
using HarmonyLib;

namespace BarNone
{
    /// <summary>The pieces of Bar None that can each be lost on their own, without taking the others with them.</summary>
    internal enum Feature
    {
        Reach,
        SmokePuffs,
        Fires,
        Projectiles,
        SkyRoof,
        SkySpace,
        SkyBeehives,
        SkyWisps,
        SkyWindmills,
        Items,
    }

    /// <summary>
    /// Which features are lost for this session, and why. Two ways to lose one:
    /// - **Its patch couldn't be applied** (each patch class's Cleanup): HarmonyX adds a patch to a method's shared record
    ///   before rebuilding the method, and keeps it when the rebuild fails, so another mod's broken patch on the same method
    ///   (after a game update, say) makes every later patch of that method fail. The kit applies a module's patch classes in
    ///   one go and takes all of them off at the first failure, which would take the sky part down with the rest; a Cleanup
    ///   that returns null keeps the failure to its own class, so only that feature is lost, and it takes its own record
    ///   back off the method.
    /// - **An error in the sky part while playing** (its own guard): the sky part doesn't use Module.Guard, whose first error
    ///   switches the whole module off, since caged crops die at ripening without it; an error in one sky feature loses only
    ///   that feature (the crops' roof test isn't lost to an error in the beehives' cover).
    /// </summary>
    internal static class Broken
    {
        private static readonly bool[] lost = new bool[Enum.GetValues(typeof(Feature)).Length];

        internal static bool Is(Feature feature) => lost[(int)feature];

        /// <summary>A patch class's Cleanup: nothing when its patch went on; otherwise the feature is lost for the session,
        /// with a warning, and its record is taken back off the method. Always null, so the kit goes on to the next class.
        /// Never throws: a Cleanup that throws would stop the kit's patching after all.</summary>
        internal static Exception Patching(Feature feature, MethodBase original, Exception error, Harmony harmony)
        {
            if (error == null)
            {
                return null;
            }
            lost[(int)feature] = true;
            // First take Bar None's record back off the method, so a failure that is Bar None's own can't break other
            // mods' later patches of it; then say so, each in its own try.
            try
            {
                if (original != null && harmony != null)
                {
                    harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
                }
            }
            catch (Exception)
            {
                // Taking the record back can fail the same way the patch did; the feature is lost either way.
            }
            try
            {
                Module.Warn($"Bar None couldn't change {What(feature)} (another mod's patch on the same method may have failed first): " +
                            $"{Consequence(feature)} until the game is started again. The rest of Bar None is not affected.\n{error}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't stop the kit's patching.
            }
            return null;
        }

        /// <summary>The sky part's own guard: its first error in a feature loses only that feature, for the session, and
        /// says what that means. The call that failed keeps the game's own answer.</summary>
        internal static void Running(Feature feature, string task, Exception error)
        {
            if (lost[(int)feature])
            {
                return;
            }
            lost[(int)feature] = true;
            Module.Warn($"Bar None stopped changing {What(feature)} after an error while {task}: {Consequence(feature)} until the game " +
                        $"is started again. The rest of Bar None is not affected.\n{error}");
        }

        internal static string What(Feature feature) => feature switch
        {
            Feature.Reach => "what you point at (Player.FindHoverObject)",
            Feature.SmokePuffs => "smoke puffs (Smoke.Awake)",
            Feature.Fires => "fires' clear space (Fireplace.CheckUnderTerrain)",
            Feature.Projectiles => "projectile hits (Projectile.OnHit)",
            Feature.SkyRoof => "crops' sky (Plant.HaveRoof)",
            Feature.SkySpace => "crops' growing space (Plant.HaveGrowSpace)",
            Feature.SkyBeehives => "beehives' cover (Beehive.HaveFreeSpace)",
            Feature.SkyWisps => "wisp fountains' cover (WispSpawner.HaveFreeSpace)",
            Feature.SkyWindmills => "windmills' cover (Windmill.CheckCover)",
            _ => "loose items (ItemDrop.Awake)",
        };

        internal static string Consequence(Feature feature) => feature switch
        {
            Feature.Reach => "you reach no further than the first thing in the way, as in the game,",
            Feature.SmokePuffs => "smoke stops at every piece, as in the game,",
            Feature.Fires => "fires follow the game's own clear-space test",
            Feature.Projectiles => "projectiles hit every piece, as in the game,",
            Feature.SkyRoof => "crops and saplings under listed pieces have \"no sun\", as in the game, and die when they ripen,",
            Feature.SkySpace => "listed pieces take a crop's growing space, as in the game,",
            Feature.SkyBeehives => "beehives count listed pieces as cover, as in the game,",
            Feature.SkyWisps => "wisp fountains count listed pieces as cover, as in the game,",
            Feature.SkyWindmills => "windmills count listed pieces as cover, as in the game,",
            _ => "items land on every piece, as in the game,",
        };
    }
}
