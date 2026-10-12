using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using HoarstoneLink = QoLMods.HoarstoneGround.Ground;

namespace HouseTrained
{
    /// <summary>
    /// With the Hoarstone (card B26, his words of 2026-10-11): tames keep to a Hoarstone's ground, leave it only to fight
    /// (and come back) or to follow him, a "Return to base" key on a tame off it, and tames guarding it. The ground is the
    /// Hoarstone module's (asked through the Hoarstone ground part), so the part acts only while Hoarstone is on, in the
    /// game that looks after the tame (its AI runs only there: research 2026-10-11, B section 5). Everything is steering
    /// through the game's own AI: the wander's picks kept inside, a walk home by the game's own MoveTo, a hunt set up as the
    /// game sets one (B section 2). A tame's home (where it was last inside when it left, or where it was sent) is written
    /// on its record ("modprojects.housetrained.home"), so whichever game looks after it next still knows; nothing else of
    /// the game's state is changed, and the game's patrol point (its "stay") is never used for it (B section 3).
    /// </summary>
    internal static class Ground
    {
        /// <summary>A tame's home on its record: a point on the ground it walks back to; zero for none. Permanent: it's in
        /// players' worlds.</summary>
        internal static readonly int HomeHash = "modprojects.housetrained.home".GetStableHashCode();

        /// <summary>A tame's base on its record: the last place on a ground it kept to before it left it, by any way
        /// (following you out too), never cleared; read only by "Return to base", so it knows the ground it came from
        /// after a relog far away (the last look). Permanent: it's in players' worlds.</summary>
        internal static readonly int BaseHash = "modprojects.housetrained.base".GetStableHashCode();

        internal const string AskName = "modprojects.housetrained.home";
        internal const string AnswerName = "modprojects.housetrained.homed";
        internal const int Protocol = 1;

        /// <summary>How far a Hoarstone is looked for when a tame with no home is sent back, in metres.</summary>
        internal const float StoneSearch = 1500f;

        /// <summary>A walk home that gets no nearer for this long tries a nearer point, in seconds.</summary>
        internal const float HomeStuck = 10f;

        /// <summary>Stalled this long after that, it gives up, in seconds.</summary>
        internal const float HomeGiveUp = 20f;

        /// <summary>A hunt that gets no nearer to its enemy for this long is given up.</summary>
        internal const float HuntStuck = 12f;

        /// <summary>Path queries guarding may make each second, over every tame.</summary>
        internal const int GuardPathsPerSecond = 4;

        private const float LogEvery = 10f;

        private static readonly Func<Vector3, bool> inside = Inside;
        private static readonly Func<float, float, float> Random = (low, high) => UnityEngine.Random.Range(low, high);
        private static readonly GroundWorld World = new GroundWorld();
        private static readonly List<Character> Intruders = new List<Character>();
        private static float intrudersUntil;
        private static int pathsLeft;
        private static float pathsUntil;

        /// <summary>Set once a frame by Parts: the part on and Hoarstone on (his words: only while Hoarstone is on).</summary>
        internal static bool Live;

        /// <summary>Whether a point lies on the ground of a settlement whose ground keeps tames, as Hoarstone decides it.</summary>
        internal static bool Inside(Vector3 point) => HoarstoneLink.Inside(point, GroundRules.Layers(Settings.GroundOthers.Value));

        // ------------------------------------------------------------------------------------------- a tame's state

        /// <summary>Where a tame this game looks after stands toward the ground, worked out again after a moment (and at
        /// once when a setting changed); writes its home when it has just left the ground, and clears it once back on.</summary>
        internal static GroundState StateOf(Character tame, BaseAI ai, out TameInfo info)
        {
            info = null;
            if (!Live || tame == null || ai == null || !tame.IsTamed() || tame.IsDead())
            {
                return GroundState.None;
            }
            info = Tames.Of(tame);
            if (!info.Grounded || info.Summon)
            {
                return GroundState.None;
            }
            if (tame.HaveRider())
            {
                // Ridden, the rider steers, and wherever it's left is where he wanted it: a ride ends any "just left" and
                // any home (the rider's game owns it while ridden, so it can clear the record).
                info.LastInsideAt = float.NaN;
                ZNetView ridden = tame.GetComponent<ZNetView>();
                ZDO rode = ridden != null && ridden.IsValid() ? ridden.GetZDO() : null;
                if (rode != null && ridden.IsOwner() && rode.GetVec3(HomeHash, Vector3.zero) != Vector3.zero)
                {
                    rode.Set(HomeHash, Vector3.zero);
                }
                return info.Ground = GroundState.None;
            }
            float now = Time.time;
            if (now < info.GroundUntil && info.GroundVersion == Settings.Version)
            {
                return info.Ground;
            }
            info.GroundUntil = now + 0.25f;
            info.GroundVersion = Settings.Version;
            ZNetView view = tame.GetComponent<ZNetView>();
            ZDO record = view != null && view.IsValid() ? view.GetZDO() : null;
            if (record == null)
            {
                return info.Ground = GroundState.None;
            }
            Vector3 here = tame.transform.position;
            // Following its player here (this game runs its AI, so the game's own follow target says). A tame that
            // follows only by its record (its player logged out, or dead until he comes back) is any other tame: on the
            // ground it keeps to it, and it comes back from a fight; off it with no home (it followed him out), it waits
            // where it is, and follows him again when he comes near (code review 1, m3; the last look). Following him
            // out writes no home: "Return to base" then goes to the nearest Hoarstone.
            bool followingHere = ai is MonsterAI monster && monster.GetFollowTarget() != null;
            bool isInside = Inside(here);
            Vector3 patrol = Vector3.zero;
            bool hasPatrol = GameHooks.PatrolPointOf != null && GameHooks.PatrolPointOf(ai, out patrol);
            Vector3 home = record.GetVec3(HomeHash, Vector3.zero);
            bool hasHome = home != Vector3.zero;
            GroundState was = info.Ground;
            GroundState state = GroundRules.Classify(followingHere, isInside, hasPatrol, hasPatrol && Inside(patrol), hasHome,
                                                     GroundRules.LeftJustNow(now, info.LastInsideAt), out bool writeHome);
            bool owner = view.IsOwner();
            if (isInside)
            {
                info.LastInside = here;
                info.LastInsideAt = now;
                info.LeftNoted = false;
                if (hasHome && owner && !followingHere)
                {
                    record.Set(HomeHash, Vector3.zero);   // back on the ground: no home needed (a value, never a removal)
                }
            }
            else
            {
                if (owner && !info.LeftNoted && !float.IsNaN(info.LastInsideAt))
                {
                    // Off the ground, by any way: its base, for "Return to base" (once per leaving; only where it moved).
                    info.LeftNoted = true;
                    if ((record.GetVec3(BaseHash, Vector3.zero) - info.LastInside).sqrMagnitude > 64f)
                    {
                        record.Set(BaseHash, info.LastInside);
                    }
                }
                if (followingHere)
                {
                    info.LastInsideAt = float.NaN;   // led out: never "just left" once it stops following
                }
            }
            if (followingHere && hasHome && owner)
            {
                // Following, a home from before (a fight, "Return to base") would walk it there once its player is gone
                // (the last look).
                record.Set(HomeHash, Vector3.zero);
            }
            if (writeHome && owner)
            {
                record.Set(HomeHash, info.LastInside);
            }
            if (state != was)
            {
                info.HomeWalk.Restart(here, now);
                info.HomeStuckSince = float.NaN;
                info.HomeFor = new Vector3(float.NaN, float.NaN, float.NaN);   // a new walk works its target out afresh
                if (was == GroundState.Returning && state == GroundState.Inside && GameHooks.WanderReached != null)
                {
                    // Back in: a fresh pick, never the old walk that led it out (code review 1, m4).
                    GameHooks.WanderReached(ai) = true;
                }
                Tell(info, now, StateLine(state));
            }
            return info.Ground = state;
        }

        private static string StateLine(GroundState state)
        {
            switch (state)
            {
                case GroundState.Inside:
                    return "is on a Hoarstone's ground";
                case GroundState.Returning:
                    return "is off the ground it keeps to, and goes back";
                case GroundState.Away:
                    return "is off any ground it keeps to (told to stay there, or never on it)";
                case GroundState.Following:
                    return "follows a player";
                default:
                    return "isn't kept to a ground";
            }
        }

        // A few metres onto the ground along the line toward a home (the home itself when the line never meets the ground
        // as this game knows it).
        private static Vector3 Deeper(Vector3 from, Vector3 home) =>
            GroundRules.MarchIn(from, home, inside, GroundRules.Step, Vector3.Distance(from, home) + GroundRules.Depth, GroundRules.Depth, out Vector3 point)
                ? point
                : home;

        // The point a Returning tame walks to: its patrol point when that's on the ground (told to stay there), else its
        // home on its record, else its last place inside.
        private static Vector3 HomeOf(Character tame, BaseAI ai, TameInfo info)
        {
            if (GameHooks.PatrolPointOf != null && GameHooks.PatrolPointOf(ai, out Vector3 patrol) && Inside(patrol))
            {
                return patrol;
            }
            ZDO record = tame.GetComponent<ZNetView>()?.GetZDO();
            Vector3 home = record != null ? record.GetVec3(HomeHash, Vector3.zero) : Vector3.zero;
            return home != Vector3.zero ? home : info.LastInside;
        }

        // ------------------------------------------------------------------------------------------- the wander

        /// <summary>
        /// Before the game's idle wander: a tame off the ground it keeps to walks home by the game's own MoveTo, and the
        /// wander is skipped (false). Its walk is watched. The game's MoveTo counts "no path" and a partial path's end as
        /// arrived, and keeps a failed path for up to 5 s, so an arrival off the ground asks for a new path every 2 s (the
        /// navmesh far off is built only once a path asks for it: research B section 4). Getting no nearer for 10 s, it
        /// goes for the nearest point of the ground along its way instead; stalled for 20 s more, it gives up, its home
        /// forgotten, and stands free. Moving on again after a stall starts the count afresh (code review 1, F4).
        /// </summary>
        internal static bool BeforeIdle(BaseAI ai, float dt)
        {
            Character tame = GameHooks.AiCharacter(ai);
            GroundState state = StateOf(tame, ai, out TameInfo info);
            bool hasTarget = ai is MonsterAI monster && GameHooks.GroundTargetCreature != null && GameHooks.GroundTargetCreature(monster) != null;
            if (!GroundRules.WalksHome(state, hasTarget, Settings.GroundFight.Value) || tame.IsFlying())
            {
                return true;
            }
            float now = Time.time;
            Vector3 here = tame.transform.position;
            Vector3 home = HomeOf(tame, ai, info);
            if (home != info.HomeFor)
            {
                // Its target, once per home: the first point of the ground on the way to its home, a few metres in, so
                // arriving means being on it (the last look: its last place inside lay at the edge).
                info.HomeFor = home;
                info.HomeTarget = Deeper(here, home);
                info.HomeStuckSince = float.NaN;
                info.HomeWalk.Restart(here, now);
            }
            info.WalksHomeFrame = Time.frameCount;
            bool arrived = GameHooks.MoveTo(ai, dt, info.HomeTarget, 1f, ai.IsAlerted());
            if (arrived && now >= info.ReplanAt)
            {
                // Arrived off the ground: a partial path's end, or no path yet (MoveTo's own rule). A new path every 2 s,
                // from here, deeper in if the line from here meets the ground elsewhere.
                info.ReplanAt = now + 2f;
                if (GameHooks.LastPathTime != null)
                {
                    GameHooks.LastPathTime(ai) = -1000f;
                }
                Vector3 deeper = Deeper(here, home);
                if ((deeper - info.HomeTarget).sqrMagnitude > 1f)
                {
                    info.HomeTarget = deeper;
                }
                else if (!Inside(here))
                {
                    info.HomeTarget = home;   // the same point again, short of the ground (a thin strip): the home itself (the last look, 5)
                }
            }
            if (!info.HomeWalk.Stuck(here, now, HomeStuck))
            {
                if (!float.IsNaN(info.HomeStuckSince) && (here - info.HomeStallAt).sqrMagnitude > 9f)
                {
                    info.HomeStuckSince = float.NaN;   // on its way again
                }
                return false;
            }
            // No nearer for a while: first the nearest point of the ground along the way.
            if (float.IsNaN(info.HomeStuckSince))
            {
                info.HomeStuckSince = now;
                info.HomeStallAt = here;
                if (GroundRules.MarchIn(here, home, inside, GroundRules.Step, Vector3.Distance(here, home) + GroundRules.Depth, GroundRules.Depth, out Vector3 nearer) &&
                    (nearer - info.HomeTarget).sqrMagnitude > 1f)
                {
                    info.HomeTarget = nearer;
                    info.HomeWalk.Restart(here, now);
                    Tell(info, now, "found no way to its home, and tries the nearest point of the ground on its way");
                    return false;
                }
            }
            if (now - info.HomeStuckSince < HomeGiveUp)
            {
                return false;
            }
            // Given up: stands free out there ("Return to base" can send it again).
            ZDO record = tame.GetComponent<ZNetView>()?.GetZDO();
            if (record != null && tame.IsOwner())
            {
                record.Set(HomeHash, Vector3.zero);
            }
            info.LastInsideAt = float.NaN;
            info.HomeStuckSince = float.NaN;
            info.GroundUntil = 0f;
            Tell(info, now, "found no way back to the ground, and stays where it is");
            return true;
        }

        /// <summary>After the game's idle wander: a tame on the ground whose walk heads off it (a pick of the game's, or any
        /// other) picks again inside, the game's way, or stands until its next pick.</summary>
        internal static void AfterIdle(BaseAI ai)
        {
            Character tame = GameHooks.AiCharacter(ai);
            if (StateOf(tame, ai, out TameInfo info) != GroundState.Inside || tame.IsFlying() || tame.IsSwimming())
            {
                return;
            }
            ref bool reached = ref GameHooks.WanderReached(ai);
            ref Vector3 target = ref GameHooks.WanderTarget(ai);
            if (reached || target == info.WanderChecked)
            {
                return;
            }
            float now = Time.time;
            Vector3 here = tame.transform.position;
            // A new walk: its place on the ground, and its path too (a place across a bay of the shape would take it off
            // the ground on the way, and back: code review 1, F6); checked once per walk.
            if (Inside(target) && PathStaysInside(ai, here, target))
            {
                info.WanderChecked = target;
                return;
            }
            Vector3 center = here;
            if (GameHooks.PatrolPointOf != null && GameHooks.PatrolPointOf(ai, out Vector3 patrol) && Inside(patrol))
            {
                center = patrol;
            }
            // At most once a second (each pick may ask for paths): a game pick that keeps heading off the ground (a far
            // spawner's point pulling it) has the tame stand meanwhile.
            bool due = now >= info.RepickAt;
            if (due)
            {
                info.RepickAt = now + 1f;
            }
            if (due && World.Start(ai) && WanderPick.Pick(center, here, tame.transform.forward, ai.m_randomMoveRange, 6, 0f, Random, World, out Vector3 point))
            {
                target = point;
                info.WanderChecked = point;
                reached = false;
                if (GameHooks.LastPathTime != null)
                {
                    GameHooks.LastPathTime(ai) = -1000f;
                }
                Tell(info, now, "picked a place on the ground instead of one off it");
            }
            else
            {
                reached = true;
                tame.SetMoveDir(Vector3.zero);
                Tell(info, now, "found nowhere on the ground to wander to, so it stands");
            }
        }

        /// <summary>For "Tames in pens"' own wander picks: a tame on the ground picks only places on it.</summary>
        internal static bool MayWanderTo(BaseAI ai, Vector3 point)
        {
            if (!Live || GameHooks.AiCharacter == null)
            {
                return true;
            }
            return StateOf(GameHooks.AiCharacter(ai), ai, out _) != GroundState.Inside || Inside(point);
        }

        /// <summary>For a wander pick's path ("Tames in pens"' picks, and this part's): for a tame on the ground, every
        /// stretch between the path's corners stays on it (looked at every 2 m).</summary>
        internal static bool CornersStayInside(BaseAI ai, List<Vector3> corners)
        {
            if (!Live || GameHooks.AiCharacter == null || corners == null || StateOf(GameHooks.AiCharacter(ai), ai, out _) != GroundState.Inside)
            {
                return true;
            }
            for (int i = 1; i < corners.Count; i++)
            {
                Vector3 from = corners[i - 1];
                Vector3 leg = corners[i] - from;
                leg.y = 0f;
                float length = leg.magnitude;
                for (float d = 0f; d < length; d += 2f)
                {
                    if (!Inside(from + leg * (d / length)))
                    {
                        return false;
                    }
                }
                if (!Inside(corners[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static readonly List<Vector3> Corners = new List<Vector3>();

        // Whether the game's own path from a tame to a place stays on the ground (no path: as the game would walk it,
        // straight, so the line).
        private static bool PathStaysInside(BaseAI ai, Vector3 from, Vector3 to)
        {
            Corners.Clear();
            if (Pathfinding.instance == null || !Pathfinding.instance.GetPath(from, to, Corners, ai.m_pathAgentType, requireFullPath: false, cleanup: true, havePath: false))
            {
                Corners.Clear();
                Corners.Add(from);
                Corners.Add(to);
            }
            return CornersStayInside(ai, Corners);
        }

        // ------------------------------------------------------------------------------------------- following

        /// <summary>
        /// Before the game's follow (BaseAI.Follow): with "… they may leave it while following you" off, a follower on the
        /// ground goes no further than its edge toward a player off it, and waits there (false: the game's follow skipped);
        /// once he's back on the ground, it follows as in the game.
        /// </summary>
        internal static bool BeforeFollow(BaseAI ai, GameObject go, float dt)
        {
            if (Settings.GroundFollow.Value || go == null)
            {
                return true;
            }
            Character tame = GameHooks.AiCharacter(ai);
            if (StateOf(tame, ai, out TameInfo info) != GroundState.Following || tame.IsFlying())
            {
                return true;
            }
            Vector3 here = tame.transform.position;
            float now = Time.time;
            if (now >= info.EdgeUntil)
            {
                info.EdgeUntil = now + 0.5f;
                info.EdgeHold = Inside(here) && GroundRules.Edge(here, go.transform.position, inside, GroundRules.Step, 2f, out info.EdgeWas);
            }
            if (!info.EdgeHold)
            {
                return true;
            }
            if ((info.EdgeWas - here).sqrMagnitude < 1f || GameHooks.MoveTo(ai, dt, info.EdgeWas, 0.5f, false))
            {
                ai.StopMoving();
            }
            Tell(info, now, "waits at the ground's edge for the player it follows");
            return false;
        }

        // ------------------------------------------------------------------------------------------- targets and guarding

        /// <summary>
        /// After the game kept or changed a tame's target (MonsterAI.UpdateTarget, on its owner): with "… they may leave it
        /// to fight" off, an enemy off the ground is let go of; a hunt under way goes on (the enemy's place kept fresh, the
        /// game's give-up held off while it walks); and a free tame on the ground with no target of its own goes for the
        /// nearest intruder on the ground it can walk to, as the game hunts (research B section 2).
        /// </summary>
        internal static void AfterUpdateTarget(MonsterAI ai, ref bool canHear, ref bool canSee)
        {
            Character tame = GameHooks.AiCharacter(ai);
            GroundState state = StateOf(tame, ai, out TameInfo info);
            if (state == GroundState.None)
            {
                return;
            }
            float now = Time.time;
            Character target = GameHooks.GroundTargetCreature(ai);
            if (target != null)
            {
                if (ReferenceEquals(target, info.Hunting))
                {
                    Hunt(ai, tame, info, target, canHear || canSee, now);
                    return;
                }
                info.Hunting = null;
                if (!Settings.GroundFight.Value &&
                    GroundRules.LetGoOutside(state, false, Inside(target.transform.position), GameHooks.GroundTimeSinceHurt(ai)))
                {
                    Drop(ai, ref canHear, ref canSee);
                    Tell(info, now, "let go of an enemy off the ground");
                }
                return;
            }
            info.Hunting = null;
            bool hasPatrol = GameHooks.PatrolPointOf != null && GameHooks.PatrolPointOf(ai, out _);
            if (!GroundRules.Guards(Settings.GroundGuard.Value, state, hasPatrol, tame.HaveRider(), CanFight(tame, info, now), false) || tame.InAttack() ||
                (tame is Humanoid humanoid && humanoid.m_aiCannotTargetOthers))
            {
                return;
            }
            Character enemy = Pick(ai, tame, info, now);
            if (enemy == null)
            {
                return;
            }
            Start(ai, tame, info, enemy, now);
        }

        // The game's way of chasing an enemy it can't sense: its last known place, not been there, the give-up timers back
        // at zero, alerted (boars and hens flee from an enemy they aren't alerted to), its target named on its record, and
        // the path planned now.
        private static void Start(MonsterAI ai, Character tame, TameInfo info, Character enemy, float now)
        {
            GameHooks.GroundTargetCreature(ai) = enemy;
            GameHooks.TargetStatic(ai) = null;
            GameHooks.LastKnownTargetPos(ai) = enemy.transform.position;
            GameHooks.BeenAtLastPos(ai) = false;
            GameHooks.TimeSinceAttacking(ai) = 0f;
            GameHooks.TimeSinceSensed(ai) = 0f;
            GameHooks.GroundAlerted(ai, true);
            GameHooks.GroundTargetInfo(ai, enemy.GetZDOID());
            if (GameHooks.LastPathTime != null)
            {
                GameHooks.LastPathTime(ai) = -1000f;
            }
            info.Hunting = enemy;
            info.HuntSentTo = enemy.transform.position;
            info.HuntCloser = Vector3.Distance(tame.transform.position, enemy.transform.position);
            info.HuntCloserAt = now;
            Tell(info, now, HuntLine, $"goes for a {Utils.GetPrefabName(enemy.gameObject)} on the ground (guarding)");
        }

        // A hunt under way, each AI update: once it hears or sees the enemy the game's own chase runs; until then its place
        // is kept fresh and the game's give-up held off; it ends when the enemy is gone, dead or off the ground, or the
        // tame gets no nearer for a while (then that enemy is passed over for a while).
        private static void Hunt(MonsterAI ai, Character tame, TameInfo info, Character enemy, bool sensed, float now)
        {
            float distance = Vector3.Distance(tame.transform.position, enemy.transform.position);
            if (distance < info.HuntCloser - 1f)
            {
                info.HuntCloser = distance;
                info.HuntCloserAt = now;
            }
            bool over = GroundRules.HuntOver(enemy == null, enemy.IsDead(), Inside(enemy.transform.position), now - info.HuntCloserAt, HuntStuck);
            if (over)
            {
                info.Hunting = null;
                if (!enemy.IsDead() && now - info.HuntCloserAt >= HuntStuck)
                {
                    info.GuardFar.Remember(enemy, now);
                }
                if (!sensed)
                {
                    bool hear = false, see = false;
                    Drop(ai, ref hear, ref see);
                }
                Tell(info, now, "ended its hunt");
                return;
            }
            if (sensed)
            {
                return;   // the game's own chase and attack
            }
            GameHooks.TimeSinceSensed(ai) = 0f;
            GameHooks.TimeSinceAttacking(ai) = 0f;
            Vector3 at = enemy.transform.position;
            GameHooks.LastKnownTargetPos(ai) = at;
            if ((at - info.HuntSentTo).sqrMagnitude > 4f || GroundRules.SendOnAgain(GameHooks.BeenAtLastPos(ai), distance, 2f))
            {
                info.HuntSentTo = at;
                GameHooks.BeenAtLastPos(ai) = false;
            }
        }

        // The tame lets go of its target, as House Trained's "let go" does (it calms down unless hurt a moment ago).
        private static void Drop(MonsterAI ai, ref bool canHear, ref bool canSee)
        {
            GameHooks.GroundTargetCreature(ai) = null;
            canHear = false;
            canSee = false;
            GameHooks.GroundTargetInfo(ai, ZDOID.None);
            if (GameHooks.GroundTimeSinceHurt(ai) > 5f)
            {
                GameHooks.GroundAlerted(ai, false);
            }
        }

        // The nearest intruder on the ground this tame may go for: the game's enemy of it, within reach, not passed over a
        // moment ago, by the rule for tames in pens, and with a full path to it (a few path queries a second, over every tame).
        private static Character Pick(MonsterAI ai, Character tame, TameInfo info, float now)
        {
            RefreshIntruders(now);
            if (Intruders.Count == 0)
            {
                return null;
            }
            Vector3 here = tame.transform.position;
            Character best = null;
            float bestDistance = float.MaxValue;
            foreach (Character each in Intruders)
            {
                if (each == null || each.IsDead() || !BaseAI.IsEnemy(tame, each) || info.GuardFar.Knows(each, now))
                {
                    continue;
                }
                float distance = Vector3.Distance(here, each.transform.position);
                if (distance < bestDistance && GroundRules.WithinReach(distance, Settings.GroundGuardReach.Value))
                {
                    best = each;
                    bestDistance = distance;
                }
            }
            if (best == null)
            {
                return null;
            }
            bool penned = Penned(tame, info, now);
            if (!GroundRules.PennedMayGo(Settings.GroundGuardPenned.Value, penned, penned && GateOpen(tame, info, now)))
            {
                return null;
            }
            if (now >= pathsUntil)
            {
                pathsUntil = now + 1f;
                pathsLeft = GuardPathsPerSecond;
            }
            if (pathsLeft <= 0)
            {
                return null;
            }
            pathsLeft--;
            if (Pathfinding.instance != null && !Pathfinding.instance.HavePath(here, best.transform.position, ai.m_pathAgentType))
            {
                info.GuardFar.Remember(best, now);
                return null;
            }
            return best;
        }

        // Every creature on the ground that tames guard against, looked over at most once a second.
        private static void RefreshIntruders(float now)
        {
            if (now < intrudersUntil)
            {
                return;
            }
            intrudersUntil = now + 1f;
            Intruders.Clear();
            bool harmlessToo = Settings.GroundGuardAnimals.Value;
            foreach (Character each in Character.GetAllCharacters())
            {
                if (each == null)
                {
                    continue;
                }
                BaseAI theirs = each.GetBaseAI();
                if (GroundRules.Intruder(each.IsDead(), each.IsPlayer(), each.IsTamed(), theirs != null && theirs.IsSleeping(), each.IsFlying(), each.IsSwimming(),
                                         each.m_aiSkipTarget, true, GroundRules.Harmless(each.GetFaction()), harmlessToo) &&
                    Inside(each.transform.position))
                {
                    Intruders.Add(each);
                }
            }
        }

        // Whether the tame carries an attack the game's AI uses on an enemy (a hen doesn't), looked at again after a while.
        private static bool CanFight(Character tame, TameInfo info, float now)
        {
            if (now < info.CanFightUntil)
            {
                return info.CanFight;
            }
            info.CanFightUntil = now + 10f;
            info.CanFight = false;
            if (tame is Humanoid humanoid && humanoid.GetInventory() != null)
            {
                foreach (ItemDrop.ItemData item in humanoid.GetInventory().GetAllItems())
                {
                    if (item?.m_shared != null && item.IsWeapon() && item.m_shared.m_aiTargetType == ItemDrop.ItemData.AiTarget.Enemy)
                    {
                        info.CanFight = true;
                        break;
                    }
                }
            }
            return info.CanFight;
        }

        // In a pen, for guarding: walls round it within "How far a pen's walls may be" in all but "Open directions allowed"
        // of 16 directions ("Pens: healing"'s test, level, with a reach of its own: with healing's 40 m most tames inside a
        // walled base would count as penned, research B section 6). Looked at again after a while.
        private static bool Penned(Character tame, TameInfo info, float now)
        {
            if (now < info.GuardPenUntil)
            {
                return info.GuardPenned;
            }
            info.GuardPenUntil = now + 5f;
            penHere = tame.transform.position;
            penFloor = penHere.y + info.FootOffset * tame.transform.localScale.y;
            penReach = Settings.GroundPenReach.Value;
            info.GuardPenned = HealRules.Walled(walledAt, HealRules.Directions, Settings.HealGaps.Value, out _);
            return info.GuardPenned;
        }

        private static readonly Func<int, bool> walledAt = WalledAt;
        private static Vector3 penHere;
        private static float penFloor;
        private static float penReach;

        private static bool WalledAt(int index)
        {
            HealRules.Direction(index, HealRules.Directions, out float x, out float z);
            return Walls.PenWallBetween(penHere, penFloor, penHere + new Vector3(x, 0f, z) * penReach, penFloor);
        }

        // Whether a door or gate of its pen stands open: a door by the walls round it (where its 16 looks meet them), whose
        // record says open (state 1 or -1; research B section 6). Looked at again after a second.
        private static bool GateOpen(Character tame, TameInfo info, float now)
        {
            if (now < info.GateUntil)
            {
                return info.GateOpen;
            }
            info.GateUntil = now + 1f;
            Vector3 here = tame.transform.position;
            float floor = here.y + info.FootOffset * tame.transform.localScale.y;
            info.GateOpen = Walls.OpenDoorInRing(here, floor, Settings.GroundPenReach.Value, HealRules.Directions);
            return info.GateOpen;
        }

        // ------------------------------------------------------------------------------------------- "Return to base"

        /// <summary>Whether "Return to base" applies to a tame, on this game (its key shown and pressed): off the ground.</summary>
        internal static bool ReturnApplies(Tameable tameable)
        {
            Character creature = tameable != null ? tameable.GetComponent<Character>() : null;
            if (creature == null || !Live)
            {
                return false;
            }
            TameInfo info = Tames.Of(creature);
            // Never while ridden: the rider steers, and a ride clears its home (code review 1, F5).
            return GroundRules.ReturnApplies(Parts.KeepToGroundOn, Settings.GroundReturn.Value, true, tameable.IsTamed(), info.Grounded, info.Summon,
                                             Inside(creature.transform.position)) && !creature.HaveRider() && HasWayHome(creature, info);
        }

        // Whether a tame off the ground has somewhere to go back to: a home on its record, a stay spot on the ground, or a
        // Hoarstone this game knows within reach; looked at again after a couple of seconds (it's asked every frame its
        // hover text shows).
        private static bool HasWayHome(Character tame, TameInfo info)
        {
            float now = Time.time;
            if (now < info.ReturnWayUntil)
            {
                return info.ReturnWay;
            }
            info.ReturnWayUntil = now + 2f;
            ZDO record = tame.GetComponent<ZNetView>()?.GetZDO();
            info.ReturnWay = record != null && Target(tame, record, out _, march: false);
            return info.ReturnWay;
        }

        /// <summary>Whether the game's own E on a tame (or its saddle) is held back now: "Return to base"'s key held by the
        /// local player at a tame it applies to (the key's own press sends it, Tick).</summary>
        internal static bool SkipGameUse(Tameable tameable, Humanoid user) =>
            Live && user != null && user == Player.m_localPlayer && Settings.GroundReturn.Value && Keys.Held(Settings.GroundReturnKey.Value) && ReturnApplies(tameable);

        /// <summary>A tame's (or its saddle's) hover text with "Return to base"'s key, while it applies.</summary>
        internal static string ReturnLine(Tameable tameable, string text)
        {
            string key = Keys.Text(Settings.GroundReturnKey.Value);
            return key.Length > 0 && ReturnApplies(tameable) ? text + FollowRules.KeyLine(key, "Return to base") : text;
        }

        private static readonly Dictionary<ZDOID, Asked> Waiting = new Dictionary<ZDOID, Asked>();
        private static ZRoutedRpc registeredOn;
        private static bool listening;

        private readonly struct Asked
        {
            internal readonly string Name;
            internal readonly float At;

            internal Asked(string name, float at)
            {
                Name = name;
                At = at;
            }
        }

        /// <summary>Every frame, from the module's Update (also while switched off): the messages registered on each new
        /// connection, answers that didn't come given up on, and "Return to base"'s key.</summary>
        internal static void Tick(float now)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc != null && rpc != registeredOn)
            {
                Register(rpc);
            }
            if (Waiting.Count > 0)
            {
                Overdue.Clear();
                foreach (KeyValuePair<ZDOID, Asked> each in Waiting)
                {
                    if (now - each.Value.At > 4f || now < each.Value.At)
                    {
                        Overdue.Add(each.Key);
                    }
                }
                foreach (ZDOID id in Overdue)
                {
                    Say($"{Waiting[id].Name} wasn't sent home: the game looking after it didn't answer (it may not run House Trained).");
                    Waiting.Remove(id);
                }
            }
            if (!Live || !Settings.GroundReturn.Value || !Keys.Pressed(Settings.GroundReturnKey.Value) || !Keys.TakesKeys())
            {
                return;
            }
            Player player = Player.m_localPlayer;
            GameObject hovering = player.GetHoverObject();
            if (hovering == null)
            {
                return;
            }
            Sadle saddle = hovering.GetComponentInParent<Sadle>();
            Tameable tameable = saddle != null ? saddle.GetTameable() : hovering.GetComponentInParent<Tameable>();
            if (tameable != null && ReturnApplies(tameable))
            {
                SendHome(tameable, now);
            }
        }

        private static readonly List<ZDOID> Overdue = new List<ZDOID>();

        // Where it's sent, worked out on this game (its home, else its stay spot if on the ground, else the nearest
        // Hoarstone it knows): the first point of the ground along the way, a few metres in. Then done here if this game
        // looks after it, else asked of the game that does.
        private static void SendHome(Tameable tameable, float now)
        {
            Character tame = tameable.GetComponent<Character>();
            ZNetView view = tameable.GetComponent<ZNetView>();
            if (tame == null || view == null || !view.IsValid())
            {
                return;
            }
            string name = tameable.GetHoverName();
            if (!Target(tame, view.GetZDO(), out Vector3 target))
            {
                Say($"{name} has no Hoarstone's ground to go back to: none is known within {StoneSearch:0} m.");
                return;
            }
            if (view.IsOwner())
            {
                Say(Order(tame, target) ? $"{name} heads back to the Hoarstone's ground." : $"{name} couldn't be sent home.");
                return;
            }
            long owner = view.GetZDO().GetOwner();
            if (!listening || owner == 0L)
            {
                Say($"{name} wasn't sent home: no game this copy of House Trained can ask looks after it this moment. Try again.");
                return;
            }
            ZDOID id = view.GetZDO().m_uid;
            if (Waiting.ContainsKey(id))
            {
                return;   // asked already
            }
            Waiting[id] = new Asked(name, now);
            var package = new ZPackage();
            package.Write(Protocol);
            package.Write(id);
            package.Write(target);
            ZRoutedRpc.instance.InvokeRoutedRPC(owner, AskName, package);
        }

        private static bool Target(Character tame, ZDO record, out Vector3 target, bool march = true)
        {
            Vector3 here = tame.transform.position;
            Vector3 home = record.GetVec3(HomeHash, Vector3.zero);
            BaseAI ai = tame.GetBaseAI();
            if (home == Vector3.zero && ai != null && GameHooks.PatrolPointOf != null && GameHooks.PatrolPointOf(ai, out Vector3 patrol) && Inside(patrol))
            {
                home = patrol;
            }
            if (home == Vector3.zero)
            {
                home = record.GetVec3(BaseHash, Vector3.zero);   // the ground it came from, however long ago
            }
            if (home == Vector3.zero && !HoarstoneLink.NearestStone(here, StoneSearch, out home))
            {
                target = here;
                return false;
            }
            if (!march)
            {
                target = home;
                return true;
            }
            float distance = Vector3.Distance(here, home);
            if (!GroundRules.MarchIn(here, home, inside, GroundRules.Step, distance + GroundRules.Depth, GroundRules.Depth, out target))
            {
                target = home;   // its ground not known on this game along the way: the place itself
            }
            return true;
        }

        /// <summary>On the game that looks after the tame: it stops following and staying (its follow name and patrol point
        /// cleared, as the game's own stay and follow clear them), and its home is the target, so it walks there and keeps to
        /// the ground again.</summary>
        private static bool Order(Character tame, Vector3 target)
        {
            ZNetView view = tame.GetComponent<ZNetView>();
            MonsterAI ai = tame.GetComponent<MonsterAI>();
            if (view == null || !view.IsValid() || !view.IsOwner() || ai == null || !tame.IsTamed() || tame.IsDead() || tame.HaveRider())
            {
                return false;
            }
            ZDO record = view.GetZDO();
            ai.SetFollowTarget(null);
            record.Set(ZDOVars.s_follow, string.Empty);
            ai.ResetPatrolPoint();
            if (record.GetBool(Following.StayHash))
            {
                record.Set(Following.StayHash, false);
            }
            record.Set(HomeHash, target == Vector3.zero ? new Vector3(0.01f, 0f, 0.01f) : target);
            TameInfo info = Tames.Of(tame);
            info.GroundUntil = 0f;
            info.HomeStuckSince = float.NaN;
            info.HomeTarget = target;
            info.HomeWalk.Restart(tame.transform.position, Time.time);
            Tell(info, Time.time, "was sent home (\"Return to base\")");
            return true;
        }

        private static void Register(ZRoutedRpc rpc)
        {
            registeredOn = rpc;
            Waiting.Clear();
            listening = false;
            try
            {
                rpc.Register<ZPackage>(AskName, OnAsk);
                rpc.Register<ZPackage>(AnswerName, OnAnswer);
                listening = true;
            }
            catch (ArgumentException)
            {
                // Another plugin carrying House Trained registered them first: it answers for this game.
            }
        }

        // Asked by another game to send a tame home: done if this game looks after it and the part is on here.
        private static void OnAsk(long sender, ZPackage package)
        {
            try
            {
                int protocol = package.ReadInt();
                ZDOID id = package.ReadZDOID();
                Vector3 target = package.ReadVector3();
                if (protocol < 1)
                {
                    return;
                }
                int answer = 0;   // 0: off here, 1: sent, 2: not this game's, 3: gone
                if (Live && Settings.GroundReturn.Value)
                {
                    GameObject found = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(id) : null;
                    Character tame = found != null ? found.GetComponent<Character>() : null;
                    ZNetView view = found != null ? found.GetComponent<ZNetView>() : null;
                    answer = tame == null || view == null || !view.IsValid() ? 3 : !view.IsOwner() ? 2 : 1;
                    if (answer == 1)
                    {
                        servingTame = tame;
                        servingTarget = target;
                        served = false;
                        Module.Guard("sending a tame home for another player", serve);
                        answer = served ? 1 : 3;
                    }
                }
                var reply = new ZPackage();
                reply.Write(Protocol);
                reply.Write(id);
                reply.Write(answer);
                ZRoutedRpc.instance.InvokeRoutedRPC(sender, AnswerName, reply);
            }
            catch (Exception error)
            {
                Module.Warn($"A friend's \"Return to base\" couldn't be read: {error.Message}");
            }
        }

        private static readonly Action serve = () => served = Order(servingTame, servingTarget);
        private static Character servingTame;
        private static Vector3 servingTarget;
        private static bool served;

        private static void OnAnswer(long sender, ZPackage package)
        {
            try
            {
                package.ReadInt();
                ZDOID id = package.ReadZDOID();
                int answer = package.ReadInt();
                if (!Waiting.TryGetValue(id, out Asked asked))
                {
                    return;
                }
                Waiting.Remove(id);
                switch (answer)
                {
                    case 1:
                        Say($"{asked.Name} heads back to the Hoarstone's ground.");
                        break;
                    case 0:
                        Say($"{asked.Name} wasn't sent home: the game looking after it has this part of House Trained, or Hoarstone, switched off.");
                        break;
                    case 2:
                        Say($"{asked.Name} wasn't sent home: it was changing hands between games. Try again.");
                        break;
                    default:
                        Say($"{asked.Name} wasn't sent home.");
                        break;
                }
            }
            catch (Exception error)
            {
                Module.Warn($"A friend's answer to \"Return to base\" couldn't be read: {error.Message}");
            }
        }

        // ------------------------------------------------------------------------------------------- helpers

        private static void Say(string text) => QoLMods.Kit.Say.Center(text);   // fitted to the screen (card B14)

        private const string HuntLine = "goes for an enemy on the ground (guarding)";

        // Each kind of line at most every 10 s per tame; the kind is a constant, so the gate counts it (code review 1, F8).
        private static void Tell(TameInfo info, float now, string what) => Tell(info, now, what, what);

        private static void Tell(TameInfo info, float now, string kind, string what)
        {
            if (info != null && Settings.GroundLog.Value && info.GroundLog.Due(kind, now, LogEvery))
            {
                Module.Log.LogInfo($"House Trained: a {info.Prefab} {what} (with the Hoarstone).");
            }
        }

        /// <summary>The part switched off, or the module after an error: what's kept between frames forgotten.</summary>
        internal static void Forget()
        {
            Intruders.Clear();
            intrudersUntil = 0f;
            Waiting.Clear();
        }

        /// <summary>
        /// The wander pick's world for a tame on the ground whose own pick headed off it: its own navmesh at its own level
        /// (as "Tames in pens" samples it), the game's own test of a point and the ground's, and a full path. No margins
        /// from walls or head tests: those are "Tames in pens"', which picks inside by itself (MayWanderTo).
        /// </summary>
        private sealed class GroundWorld : IWanderWorld
        {
            private BaseAI ai;
            private float level;
            private NavMeshQueryFilter filter;
            private bool snaps;

            internal bool Start(BaseAI forAi)
            {
                ai = forAi;
                level = forAi.transform.position.y;
                snaps = Pathfinding.instance != null && GameHooks.AgentId != null;
                if (snaps)
                {
                    filter = new NavMeshQueryFilter { agentTypeID = GameHooks.AgentId(Pathfinding.instance, forAi.m_pathAgentType), areaMask = NavMesh.AllAreas };
                }
                return true;
            }

            public bool Snap(Vector3 candidate, out Vector3 onMesh)
            {
                onMesh = candidate;
                if (!snaps)
                {
                    return true;
                }
                if (NavMesh.SamplePosition(new Vector3(candidate.x, level + 0.5f, candidate.z), out NavMeshHit hit, 2f, filter))
                {
                    onMesh = hit.position;
                    return true;
                }
                return false;
            }

            public bool Valid(Vector3 point) => Inside(point) && (GameHooks.ValidWanderPoint == null || GameHooks.ValidWanderPoint(ai, point));

            public bool Clear(Vector3 point, Vector3 facing, float margin) => true;

            private readonly List<Vector3> corners = new List<Vector3>();

            public bool HavePath(Vector3 from, Vector3 to, out Vector3 arrival)
            {
                arrival = to - from;
                Pathfinding paths = Pathfinding.instance;
                if (paths == null)
                {
                    return true;
                }
                // A full path, and all of it on the ground (code review 1, F6).
                return paths.GetPath(from, to, corners, ai.m_pathAgentType, requireFullPath: true, cleanup: true, havePath: true) &&
                       CornersStayInside(ai, corners);   // the path as the game cleans it, as it walks it (the last look, 6)
            }

            public bool HeadClear(Vector3 point, Vector3 facing) => true;
        }
    }
}
