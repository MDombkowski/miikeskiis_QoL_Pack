using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// Which parts and points are on this frame, worked out once a frame from the module's switch, the settings and the
    /// features lost (every patch asks these first, before any setting: some run every physics step), and what switching
    /// one off puts back (MODULES.md rules 5 and 7; review F13).
    /// </summary>
    internal static class Parts
    {
        internal static bool SpareOn;
        internal static bool CropsOn;
        internal static bool FriendlyOn;
        internal static bool FightBackOn;
        internal static bool PensOn;
        internal static bool GroundOn;
        internal static bool BirthsOn;
        internal static bool GrowUpsOn;
        internal static bool SightOn;
        internal static bool LetGoOn;
        internal static bool WanderOn;
        internal static bool HeadsOn;
        internal static bool EaseOutOn;
        internal static bool FarHomeOn;
        internal static bool HealOn;
        internal static bool FeedOn;
        /// <summary>The module running and switched on (card B25: E makes a follower stay even with Following off).</summary>
        internal static bool ModuleOn;
        internal static bool FollowOn;
        /// <summary>"With the Hoarstone"'s switch; whether it acts also needs Hoarstone on (Ground.Live).</summary>
        internal static bool KeepToGroundOn;

        private static bool wasFriendly;
        private static bool wasFightBack;
        private static bool wasPens;
        private static bool wasGround;
        private static float nextTidy;

        /// <summary>Every frame, from the module's Update (also while it is switched off), with the frame's time.</summary>
        internal static void Follow(float now)
        {
            bool on = Module.IsOn;
            SpareOn = on && Settings.Spare.Value;
            CropsOn = SpareOn && Settings.SpareCrops.Value;
            // Friendly fire needs its judge: without it nothing would keep it off players and his tames (decision 41).
            FriendlyOn = on && Settings.Friendly.Value && !Broken.Is(Feature.FriendlyJudge);
            FightBackOn = FriendlyOn && Settings.FfFightBack.Value && !Broken.Is(Feature.FightBackHurt) && !Broken.Is(Feature.FightBackEnemy);
            PensOn = on && Settings.Pens.Value;
            GroundOn = PensOn && (Settings.NoStacking.Value || Settings.NoClimbing.Value);
            BirthsOn = PensOn && Settings.Births.Value && !Broken.Is(Feature.PensSetLevel);
            GrowUpsOn = PensOn && Settings.GrowUps.Value && !Broken.Is(Feature.PensSetLevel);
            SightOn = PensOn && Settings.NoSight.Value;
            LetGoOn = PensOn && Settings.LetGo.Value;
            WanderOn = PensOn && Settings.Wander.Value;
            HeadsOn = PensOn && Settings.Heads.Value;
            EaseOutOn = HeadsOn && Settings.EaseOut.Value;
            FarHomeOn = PensOn && Settings.FarHome.Value;
            // Healing in pens and feeding from the hand stand on their own (card A84): neither needs "Tames in pens".
            HealOn = on && Settings.Heal.Value;
            FeedOn = on && Settings.Feed.Value && !Broken.Is(Feature.FeedHand);
            // Following and "With the Hoarstone" (cards B25, B26); the second acts only while Hoarstone is on (his words).
            ModuleOn = on;
            FollowOn = on && Settings.Follow.Value && !Broken.Is(Feature.TameInteract);
            KeepToGroundOn = on && Settings.Ground.Value;
            Ground.Live = KeepToGroundOn && QoLMods.HoarstoneGround.Ground.Now == QoLMods.HoarstoneGround.Ground.Standing.On;

            if (wasFriendly && !FriendlyOn)
            {
                Friendly.PutBack();
            }
            else if (wasFightBack && !FightBackOn)
            {
                Friendly.ForgetGrudges();
            }
            if (FriendlyOn)
            {
                Friendly.Follow(Settings.FfAreaAttacks.Value, Settings.FfMelee.Value, Settings.FfAreaObjects.Value, Settings.FfProjectiles.Value);
            }
            if (wasPens && !PensOn)
            {
                Tames.ForgetAllPens();
                Pens.Forget();
            }
            if (wasGround && !Ground.Live)
            {
                Ground.Forget();
            }
            wasFriendly = FriendlyOn;
            wasFightBack = FightBackOn;
            wasPens = PensOn;
            wasGround = Ground.Live;

            if (now >= nextTidy)
            {
                nextTidy = now + 1f;
                Friendly.Tidy(now);
            }
        }

        /// <summary>After an error switched the module off: every part read as off (the module's other undo steps put back
        /// what the parts changed).</summary>
        internal static void AllOff()
        {
            SpareOn = CropsOn = FriendlyOn = FightBackOn = PensOn = GroundOn = BirthsOn = GrowUpsOn = false;
            SightOn = LetGoOn = WanderOn = HeadsOn = EaseOutOn = FarHomeOn = HealOn = FeedOn = false;
            ModuleOn = FollowOn = KeepToGroundOn = Ground.Live = false;
            wasFriendly = wasFightBack = wasPens = wasGround = false;
        }
    }
}
