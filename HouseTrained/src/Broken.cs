using System;
using System.Reflection;
using HarmonyLib;

namespace HouseTrained
{
    /// <summary>The pieces of House Trained that can each be lost on their own (decision 41), one per patched method.</summary>
    internal enum Feature
    {
        SpareSend,
        SpareLand,
        CropsSend,
        CropsLand,
        FriendlyJudge,
        FriendlyAttacks,
        FriendlyAreaObjects,
        FriendlyBlasts,
        FriendlyProjectiles,
        FightBackHurt,
        FightBackEnemy,
        PensGround,
        PensBirths,
        PensEggs,
        PensSetLevel,
        PensGrowUps,
        PensHatching,
        PensSight,
        PensAttack,
        PensFindEnemy,
        PensLetGo,
        PensWander,
        PensFarHome,
        FeedHand,
        FeedSaddle,
    }

    /// <summary>
    /// Which features are lost for this session, and why. A patch class's Cleanup keeps a failure to apply its patch to
    /// that class (HarmonyX adds a patch to a method's shared record before rebuilding the method, and keeps it when the
    /// rebuild fails, so another mod's broken patch on the same method makes every later patch of it fail; the kit applies
    /// a module's classes in one go and takes them all off at the first failure). Bar None's Broken, the worked example
    /// (MODULES.md rule 7; LESSONS_LEARNED, HarmonyX and a failed patch).
    /// </summary>
    internal static class Broken
    {
        private static readonly bool[] lost = new bool[Enum.GetValues(typeof(Feature)).Length];

        internal static bool Is(Feature feature) => lost[(int)feature];

        /// <summary>A patch class's Cleanup: nothing when its patch went on; otherwise the feature is lost for the session,
        /// with a warning, and its record is taken back off the method. Always null, so the kit goes on to the next class.
        /// Never throws.</summary>
        internal static Exception Patching(Feature feature, MethodBase original, Exception error, Harmony harmony)
        {
            if (error == null)
            {
                return null;
            }
            lost[(int)feature] = true;
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
                Module.Warn($"House Trained couldn't change {What(feature)} (another mod's patch on the same method may have failed first): " +
                            $"{Consequence(feature)} until the game is started again. The rest of House Trained is not affected.\n{error}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't stop the kit's patching.
            }
            return null;
        }

        /// <summary>A feature lost because the game no longer has what it needs (GameHooks), said once at the start by the
        /// kit's "Running without" line.</summary>
        internal static void Missing(Feature feature) => lost[(int)feature] = true;

        internal static string What(Feature feature) => feature switch
        {
            Feature.SpareSend => "hits sent to pieces (WearNTear.Damage)",
            Feature.SpareLand => "hits landing on pieces (WearNTear.RPC_Damage)",
            Feature.CropsSend => "hits sent to crops (Destructible.Damage)",
            Feature.CropsLand => "hits landing on crops (Destructible.RPC_Damage)",
            Feature.FriendlyJudge => "hits sent to creatures (Character.Damage)",
            Feature.FriendlyAttacks => "creatures' attacks (Attack.Start)",
            Feature.FriendlyAreaObjects => "area objects (Aoe.Setup)",
            Feature.FriendlyBlasts => "projectiles' blasts (Projectile.DoAOE)",
            Feature.FriendlyProjectiles => "projectiles (Projectile.Setup)",
            Feature.FightBackHurt => "a creature hurt (MonsterAI.OnDamaged)",
            Feature.FightBackEnemy => "who is whose enemy (BaseAI.IsEnemy)",
            Feature.PensGround => "creatures' footing (Character.UpdateGroundContact)",
            Feature.PensBirths => "births (Procreation.Procreate)",
            Feature.PensEggs => "eggs laid (ItemDrop.SetQuality)",
            Feature.PensSetLevel => "new creatures (Character.SetLevel)",
            Feature.PensGrowUps => "growing up (Growup.GrowUpdate)",
            Feature.PensHatching => "hatching (EggGrow.GrowUpdate)",
            Feature.PensSight => "what creatures see (BaseAI.CanSeeTarget)",
            Feature.PensAttack => "creatures' attacks on a target (Humanoid.StartAttack)",
            Feature.PensFindEnemy => "creatures finding an enemy (BaseAI.FindEnemy)",
            Feature.PensLetGo => "creatures keeping a target (MonsterAI.UpdateTarget)",
            Feature.PensWander => "creatures wandering (BaseAI.IdleMovement)",
            Feature.FeedHand => "items used on a creature (Tameable.UseItem)",
            Feature.FeedSaddle => "items used on a creature's saddle (Sadle.UseItem)",
            _ => "creatures' spawn points (BaseAI.GetPatrolPoint)",
        };

        internal static string Consequence(Feature feature) => feature switch
        {
            Feature.SpareSend => "tames' hits on buildings are spared only where they land, on a game running House Trained,",
            Feature.SpareLand => "tames' hits on buildings are spared only where they're sent, and with a share set, not at all,",
            Feature.CropsSend => "tames' hits on crops are spared only where they land,",
            Feature.CropsLand => "tames' hits on crops are spared only where they're sent, and with a share set, not at all,",
            Feature.FriendlyJudge => "friendly fire is off, since nothing could keep it off players and your tames,",
            Feature.FriendlyAttacks => "area attacks and swings hurt only enemies, as in the game,",
            Feature.FriendlyAreaObjects => "shockwaves, clouds, fire patches and novas hurt only enemies, as in the game,",
            Feature.FriendlyBlasts => "projectiles' blasts hurt only enemies, as in the game,",
            Feature.FriendlyProjectiles => "projectiles fly past friends, as in the game,",
            Feature.FightBackHurt => "creatures don't fight back, as in the game,",
            Feature.FightBackEnemy => "creatures don't fight back, as in the game,",
            Feature.PensGround => "tames stand and climb as in the game,",
            Feature.PensBirths => "births land where the game puts them,",
            Feature.PensEggs => "eggs land where the game puts them,",
            Feature.PensSetLevel => "births, eggs and grow-ups land where the game puts them,",
            Feature.PensGrowUps => "grown-ups start where the game puts them,",
            Feature.PensHatching => "hatchlings start where the game puts them,",
            Feature.PensSight => "tames see over and through walls as in the game (they still don't attack across one),",
            Feature.PensAttack => "a tame that sees across a wall attacks across it, as in the game,",
            Feature.PensFindEnemy => "tames take the nearest enemy they sense, as in the game (they still let go of one they can't reach),",
            Feature.PensLetGo => "tames keep an enemy they can't reach, as in the game,",
            Feature.PensWander => "tames wander, stand at the walls and push their heads into them, as in the game,",
            Feature.FeedHand => "creatures can't be fed from the hand (throw their food on the ground, as in the game),",
            Feature.FeedSaddle => "a saddled creature is fed from the hand only while you look at it, not at its saddle,",
            _ => "tames from a spawner run back toward it, as in the game,",
        };
    }
}
