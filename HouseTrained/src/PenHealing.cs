using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// Healing in pens (card A84): every tame this game looks after (the owner of its record), hurt and inside a pen, gets
    /// its health back at the set share of its full health a minute, on top of the game's own healing and hungry or not
    /// (his words: nothing needed but being tamed and in a pen). The rules are HealRules'. Run from the module's Update, not
    /// from a patch on the creature's AI (Skarif's ValheimPerformanceOverhaul switches its AI throttling off when another
    /// mod patches MonsterAI.UpdateAI, and its idle-tame optimizer may stop a tame's AI: neither touches this). It needs
    /// none of the "Tames in pens" part's switches: whether a tame is in a pen is worked out here, since that part keeps
    /// tames in wherever they are and never asks it. A pen is walls (as "What counts as a wall" says) round the tame within
    /// the set distance, along 16 level directions at three heights (Walls.BetweenSpots, as births are placed), with the
    /// set number of directions allowed open. Character.Heal on the owner heals at once (the game's RPC_Heal); a game that
    /// doesn't look after the tame never heals it, so two friends on the pack never heal it twice, and a tame looked after
    /// by a game without House Trained (or with this off) heals as in the game.
    /// </summary>
    internal static class PenHealing
    {
        /// <summary>How often the tames loaded here are looked over, in seconds (each is healed on its own beat).</summary>
        private const float LookEvery = 0.25f;

        private static readonly Action work = Work;
        private static readonly Func<int, bool> walledAt = WalledAt;
        private static float nextLook;

        // The tame being looked at for its pen, and the direction being looked along.
        private static Vector3 probeHere;
        private static float probeFloor;
        private static float probeTerrain;
        private static float probeReach;
        private static bool probeFollows;
        private static Vector3 probeWay;
        private static Vector3 stretchFrom;
        private static float stretchGround;
        private static readonly Func<float, float, bool> walledBetween = WalledBetween;
        private static int testsLeft;

        /// <summary>Every frame, from the module's Update.</summary>
        internal static void Tick(float now)
        {
            if (!Parts.HealOn || now < nextLook)
            {
                return;
            }
            nextLook = now + LookEvery;
            Module.Guard("healing tames in pens", work);
        }

        private static void Work()
        {
            float now = Time.time;
            testsLeft = HealRules.MostPenTests;
            List<Character> all = Character.GetAllCharacters();
            for (int i = 0; i < all.Count; i++)
            {
                Character creature = all[i];
                if (creature == null || creature.IsPlayer() || !creature.IsTamed())
                {
                    continue;
                }
                TameInfo info = Tames.Of(creature);
                if (!info.HealSeen)
                {
                    // Each tame's beat at its own random phase (code review m3).
                    info.HealSeen = true;
                    info.NextHeal = HealRules.FirstBeat(now, UnityEngine.Random.value);
                    continue;
                }
                if (now < info.NextHeal)
                {
                    continue;
                }
                info.NextHeal = now + HealRules.Beat;
                Beat(creature, info, now);
            }
        }

        // One beat of one tame (HealRules.BeatAmount): its pen looked for only when it's hurt.
        private static void Beat(Character tame, TameInfo info, float now)
        {
            beatTame = tame;
            beatInfo = info;
            beatNow = now;
            float amount = HealRules.BeatAmount(Parts.HealOn, tame.IsOwner(), tame.IsTamed(), tame.IsDead(), tame.GetHealth(), tame.GetMaxHealth(),
                                                Settings.HealRate.Value, now, ref info.HealLast, inPen);
            beatTame = null;
            beatInfo = null;
            if (amount > 0f)
            {
                tame.Heal(amount, Settings.HealNumbers.Value);
            }
        }

        private static readonly Func<bool> inPen = () => InPen(beatTame, beatInfo, beatNow);
        private static Character beatTame;
        private static TameInfo beatInfo;
        private static float beatNow;

        // Whether walls close the tame in, looked at again after a while (and at once when a setting changed).
        private static bool InPen(Character tame, TameInfo info, float now)
        {
            bool known = info.PenVersion == Settings.Version;
            if (now < info.PenUntil && known)
            {
                return info.InPen;
            }
            if (testsLeft <= 0)
            {
                // This look's tests are spent: the last answer meanwhile (none yet, or a setting changed: not healed for
                // now), and the test at a later look.
                return known && info.InPen;
            }
            testsLeft--;
            info.PenUntil = HealRules.NextPenCheck(now, UnityEngine.Random.value);
            info.PenVersion = Settings.Version;
            Transform root = tame.transform;
            float scale = root.localScale.y;
            probeHere = root.position;
            probeFloor = probeHere.y + info.FootOffset * scale;
            float reach = Settings.HealReach.Value;
            probeReach = reach;
            // On the terrain its looks follow the ground, stretch by stretch; on a floor above it (a barn), they stay level.
            probeTerrain = Walls.TerrainAt(probeHere, float.NegativeInfinity);
            probeFollows = Mathf.Abs(probeTerrain - probeHere.y) < HealRules.OnTerrain;
            int gaps = Settings.HealGaps.Value;
            info.InPen = HealRules.Walled(walledAt, HealRules.Directions, gaps, out int closed);
            int said = info.InPen ? 1 : 0;
            if (Settings.HealLog.Value && info.PenSaid != said)
            {
                info.PenSaid = said;
                Module.Log.LogInfo(info.InPen
                    ? $"House Trained: a hurt {info.Prefab} is in a pen ({closed} of {HealRules.Directions} directions walled): it heals {Settings.HealRate.Value}% of its health a minute (healing in pens)."
                    : $"House Trained: a hurt {info.Prefab} isn't in a pen (open in more than {gaps} of {HealRules.Directions} directions within {reach} m): it heals as in the game (healing in pens).");
            }
            return info.InPen;
        }

        // Whether a fencing piece stands within reach along one direction, at the pen test's heights: level from its floor,
        // or following the ground stretch by stretch from the terrain at its feet (a slope would carry a level look over or
        // under a fence; code review n6: every stretch measured from the terrain, the first too).
        private static bool WalledAt(int index)
        {
            HealRules.Direction(index, HealRules.Directions, out float x, out float z);
            probeWay = new Vector3(x, 0f, z);
            if (!probeFollows)
            {
                return Walls.PenWallBetween(probeHere, probeFloor, probeHere + probeWay * probeReach, probeFloor);
            }
            stretchFrom = probeHere;
            stretchGround = probeTerrain;
            return HealRules.March(probeReach, HealRules.Stretch, walledBetween);
        }

        // One stretch of a look along the ground: from where the last one ended, to the ground at its far end.
        private static bool WalledBetween(float from, float to)
        {
            Vector3 end = probeHere + probeWay * to;
            float endGround = Walls.TerrainAt(end, stretchGround);
            bool walled = Walls.PenWallBetween(stretchFrom, stretchGround, end, endGround);
            stretchFrom = end;
            stretchGround = endGround;
            return walled;
        }
    }
}
