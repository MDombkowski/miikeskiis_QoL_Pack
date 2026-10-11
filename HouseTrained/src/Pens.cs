using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HouseTrained
{
    /// <summary>
    /// Tames in pens (his four points of 2026-09-30; decisions 22 to 37, 43): the work of the part's patches, all on the
    /// tame's owner and for chosen tames only. Point 1 (kept in): footing, births, grow-ups and hatching. Point 3 (no
    /// fighting through a wall): sight, the attack's backstop, finding and letting go of enemies. Points 2 and 4 (heads,
    /// wandering): all inside the idle wander's prefix (review F9).
    /// </summary>
    internal static class Pens
    {
        private const float LogEvery = 10f;

        // The birth being made (Procreation.Procreate, between its prefix and its finalizer).
        private static Character parent;
        private static Vector3 parentAt;
        private static Vector3 parentForward;
        private static float parentOffset;
        private static bool birthNoted;

        // The young growing up or the egg hatching (GrowUpdate, between its prefix and its finalizer).
        private static Character young;
        private static Vector3 youngAt;
        private static bool growNoted;

        private static readonly Func<float, float, float> Random = (low, high) => UnityEngine.Random.Range(low, high);
        private static readonly GameWorld World = new GameWorld();
        private static readonly float[] AwayTurns = { 0f, 45f, -45f };

        // ---------------------------------------------------------------------------------------------------- point 1

        private const string OnCreatureLine = "doesn't stand on a creature";
        private const string ClimbLine = "doesn't climb a piece's edge {0:0.00} m up";
        private const string LetGoLine = "let go of {0} behind a wall, out of reach";

        /// <summary>Before a chosen tame's ground contact counts (every physics step, on its owner): another creature
        /// isn't ground, nor a wall piece's edge above its step (decision 24); a tame left on a creature is nudged off.</summary>
        internal static void BeforeGroundContact(Character tame)
        {
            TameInfo info = Tames.Of(tame);
            if (!info.Penned || (!Settings.PensRidden.Value && tame.HaveRider()))
            {
                return;
            }
            Collider ground = GameHooks.GroundCollider(tame);
            if (ground == null)
            {
                return;
            }
            GroundKind kind = Walls.IsCreature(ground) ? GroundKind.Creature : Walls.IsWall(ground) ? GroundKind.WallPiece : GroundKind.Other;
            if (kind == GroundKind.Other)
            {
                return;
            }
            float step = Settings.Step.Value > 0f ? Settings.Step.Value : info.OwnStep;
            float height = 0f;
            if (kind == GroundKind.WallPiece && Settings.NoClimbing.Value)
            {
                // Above its body's lowest point; a slope read from the piece's own surface only when it would count
                // (code review 1, finding 2): a flat floor costs no ray. The slope only where the body touches that very
                // face, not at an edge beside it (code review 2, finding 4).
                Vector3 contact = GameHooks.GroundPoint(tame);
                Transform root = tame.transform;
                float scale = root.localScale.y;
                height = GroundRule.Height(contact.y, root.position.y, info.FootOffset, scale, info.BodyRadius, 1f);
                if (height > step)
                {
                    // GetLastGroundNormal is this step's contact normal (m_groundContactNormal, set by the contacts before
                    // this method uses it: the last look, nit 4).
                    bool met = Walls.FaceUnder(ground, contact, out Vector3 face);
                    height = GroundRule.Height(contact.y, root.position.y, info.FootOffset, scale, info.BodyRadius,
                                               GroundRule.SlopeUp(met, face, tame.GetLastGroundNormal()));
                }
            }
            if (!GroundRule.Clears(kind, height, step, Settings.NoStacking.Value, Settings.NoClimbing.Value))
            {
                return;
            }
            GameHooks.ResetGroundContact(tame);
            float now = Time.time;
            if (kind == GroundKind.Creature)
            {
                Tell(info, now, OnCreatureLine);
            }
            else if (Telling(info, now, ClimbLine))
            {
                Say(info, string.Format(ClimbLine, height));
            }
            float nudge = Settings.Nudge.Value;
            if (kind != GroundKind.Creature || nudge <= 0f || !info.Nudge.OnCreature(now))
            {
                return;
            }
            Rigidbody body = GameHooks.Body(tame);
            Rigidbody under = ground.attachedRigidbody;
            if (body != null && under != null)
            {
                body.AddForce(NudgeWatch.Away(tame.transform.position, under.position, tame.transform.right) * nudge, ForceMode.VelocityChange);
            }
        }

        /// <summary>A birth starting: its parent noted, if the part looks after it.</summary>
        internal static void BeforeBirth(Procreation procreation)
        {
            birthNoted = false;
            Character mother = procreation.GetComponent<Character>();
            if (mother == null || !mother.IsTamed() || !Tames.Of(mother).Penned)
            {
                return;
            }
            parent = mother;
            parentAt = mother.transform.position;
            parentForward = mother.transform.forward;
            parentOffset = Mathf.Max(procreation.m_spawnOffset, procreation.m_spawnOffsetMax);
            birthNoted = true;
        }

        /// <summary>The birth over, even if the game's method threw: the note cleared (decision 43).</summary>
        internal static void AfterBirth()
        {
            birthNoted = false;
            parent = null;
        }

        /// <summary>A young growing up (or an egg hatching): its spot noted, if it is tamed.</summary>
        internal static void BeforeGrowUp(Character growing, Vector3 at, bool tamed)
        {
            growNoted = false;
            if (!tamed)
            {
                return;
            }
            young = growing;
            youngAt = at;
            growNoted = true;
        }

        internal static void AfterGrowUp()
        {
            growNoted = false;
            young = null;
        }

        /// <summary>A creature given its level: the newborn of the noted birth is put on the parent's side of a wall, the
        /// grown-up of the noted young is moved out of the walls it starts in; any other creature (a spawner's, the
        /// console's) is left alone.</summary>
        internal static void AfterSetLevel(Character created)
        {
            if (created == null)
            {
                return;
            }
            Vector3 at = created.transform.position;
            if (birthNoted && Parts.BirthsOn && created != parent && Utils.DistanceXZ(at, parentAt) <= parentOffset + 1f)
            {
                TameInfo born = Tames.Of(created);
                float scale = created.transform.localScale.y;
                Place(created.transform, created.GetComponent<Rigidbody>(), born.BodyRadius * scale, born.BodyHeight * scale, "a newborn");
            }
            else if (growNoted && Parts.GrowUpsOn && created != young && (at - youngAt).sqrMagnitude < 0.01f && created.IsTamed() && Tames.Of(created).Penned)
            {
                Unstick(created);
            }
        }

        /// <summary>An item given its quality: the egg of the noted birth is put on the parent's side of a wall.</summary>
        internal static void AfterSetQuality(ItemDrop egg)
        {
            if (birthNoted && Parts.BirthsOn && egg != null && Utils.DistanceXZ(egg.transform.position, parentAt) <= parentOffset + 1f)
            {
                Place(egg.transform, egg.GetComponent<Rigidbody>(), 0.2f, 0.4f, "an egg");
            }
        }

        // A newborn with a wall between it and its parent goes to the first clear spot around the parent (behind, in front,
        // left, right), else onto the parent's own spot, which is inside. "Between" is looked for along three level lines
        // above the floor the parent stands on, up to its height (code review 1, finding 10); a spot is clear when a body
        // as wide and as tall as the newborn's, standing just above that floor, meets no wall piece.
        private static void Place(Transform newborn, Rigidbody body, float radius, float tall, string what)
        {
            TameInfo mother = Tames.Of(parent);
            float scale = parent.transform.localScale.y;
            float floor = parentAt.y + mother.FootOffset * scale;
            float tallest = mother.BodyHeight * scale;
            if (!Walls.BetweenSpots(parentAt, newborn.position, floor, tallest))
            {
                return;
            }
            float low = floor + 0.1f + radius;
            float high = Mathf.Max(low, floor + 0.1f + tall - radius);
            Vector3 spot = parentAt;
            foreach (Vector3 each in Placement.AroundParent(parentAt, parentForward, parentOffset))
            {
                if (!Walls.BetweenSpots(parentAt, each, floor, tallest) &&
                    Walls.Overlapping(new Vector3(each.x, low, each.z), new Vector3(each.x, high, each.z), radius, out _) == 0)
                {
                    spot = each;
                    break;
                }
            }
            newborn.position = spot;
            if (body != null)
            {
                body.position = spot;
            }
            if (Settings.PensLog.Value)
            {
                Module.Log.LogInfo($"House Trained put {what} of {Utils.GetPrefabName(parent.gameObject)} on its side of the wall (tames in pens).");
            }
        }

        // A new adult out of every wall piece its capsule starts in, toward the side the young stood on.
        private static void Unstick(Character adult)
        {
            CapsuleCollider capsule = adult.GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                return;
            }
            Transform root = adult.transform;
            Vector3 at = root.position;
            bool moved = false;
            for (int pass = 0; pass < 3; pass++)
            {
                Bounds box = capsule.bounds;
                box.center += at - root.position;
                int count = Walls.Overlapping(box.center - Vector3.up * (box.extents.y - box.extents.x), box.center + Vector3.up * (box.extents.y - box.extents.x),
                                              Mathf.Max(box.extents.x, box.extents.z), out Collider[] walls);
                bool any = false;
                for (int i = 0; i < count; i++)
                {
                    Collider wall = walls[i];
                    if (!Physics.ComputePenetration(capsule, at, root.rotation, wall, wall.transform.position, wall.transform.rotation, out Vector3 way, out float depth))
                    {
                        continue;
                    }
                    // ClosestPoint answers only for convex shapes: a non-convex mesh's box stands in (code review 1, nit 15).
                    Vector3 near = wall is MeshCollider mesh && !mesh.convex ? wall.bounds.ClosestPoint(youngAt) : wall.ClosestPoint(youngAt);
                    Vector3 side = youngAt - near;
                    side.y = 0f;
                    way.y = 0f;
                    if (side.sqrMagnitude > 1e-4f && Vector3.Dot(way, side) < 0f)
                    {
                        // The shortest way out is through the wall: step toward the young's side instead, as far as it takes.
                        way = side.normalized;
                        depth = 0.1f;
                        for (int n = 0; n < 40 && Physics.ComputePenetration(capsule, at + way * depth, root.rotation, wall, wall.transform.position, wall.transform.rotation, out _, out _); n++)
                        {
                            depth += 0.1f;
                        }
                    }
                    else if (way.sqrMagnitude > 1e-6f)
                    {
                        way.Normalize();
                    }
                    else
                    {
                        continue;
                    }
                    at += way * (depth + 0.05f);
                    any = true;
                }
                if (!any)
                {
                    break;
                }
                moved = true;
            }
            if (!moved)
            {
                return;
            }
            root.position = at;
            Rigidbody body = adult.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = at;
            }
            if (Settings.PensLog.Value)
            {
                Module.Log.LogInfo($"House Trained moved a new {Utils.GetPrefabName(adult.gameObject)} out of the wall it grew up in (tames in pens).");
            }
        }

        // ---------------------------------------------------------------------------------------------------- point 3

        /// <summary>After the game's sight test said it sees the target: a chosen tame doesn't see an enemy behind a wall
        /// (from its body's middle, never its eyes; kept 0.25 s, review N3).</summary>
        internal static void AfterCanSee(BaseAI ai, Character target, ref bool seen)
        {
            Character tame = Chosen(ai, out TameInfo info);
            if (tame != null && BehindWall(tame, info, target))
            {
                seen = false;
            }
        }

        /// <summary>Before an attack on a target (never a friend's, a healer's: the patch leaves those to the game): a chosen
        /// tame doesn't attack one behind a wall (its stomp reaches over it). A backstop: the game attacks only what it
        /// sees.</summary>
        internal static bool MayAttack(MonsterAI ai, Character target)
        {
            Character tame = Chosen(ai, out TameInfo info);
            return tame == null || !BehindWall(tame, info, target);
        }

        /// <summary>Path queries one search for another enemy may make (code review 1, finding 11).</summary>
        private const int MostPathQueries = 3;

        private static readonly List<Candidate> Candidates = new List<Candidate>();

        /// <summary>After the game found the closest enemy it senses: one behind a wall that a chosen tame can't walk to is
        /// passed over for the closest that passes, the game's own way (review F10). The others are tried nearest first:
        /// one with no wall between is taken at once; one behind a wall costs a path query, at most a few per search
        /// (code review 1, finding 11), and one found out of reach isn't asked about again for a few seconds, so the next
        /// search asks about others (code review 2, nit 13).</summary>
        internal static void AfterFindEnemy(BaseAI ai, ref Character found)
        {
            Character tame = Chosen(ai, out TameInfo info);
            if (tame == null || !Walls.Between(tame.GetCenterPoint(), found.GetCenterPoint()))
            {
                return;
            }
            float now = Time.time;
            if (!info.Far.Knows(found, now))
            {
                if (HasPath(ai, tame, found))
                {
                    return;
                }
                info.Far.Remember(found, now);
            }
            Vector3 here = ai.transform.position;
            Candidates.Clear();
            foreach (Character each in Character.GetAllCharacters())
            {
                if (each == null || each == found || each == tame || each.IsDead() || each.m_aiSkipTarget || !BaseAI.IsEnemy(tame, each))
                {
                    continue;
                }
                BaseAI theirs = each.GetBaseAI();
                if ((theirs != null && theirs.IsSleeping()) || !ai.CanSenseTarget(each))
                {
                    continue;
                }
                Candidates.Add(new Candidate(Vector3.Distance(each.transform.position, here), each));
            }
            Candidates.Sort();
            Character best = null;
            int queries = 0;
            foreach (Candidate each in Candidates)
            {
                if (!Walls.Between(tame.GetCenterPoint(), each.Enemy.GetCenterPoint()))
                {
                    best = each.Enemy;
                    break;
                }
                if (queries >= MostPathQueries || info.Far.Knows(each.Enemy, now))
                {
                    continue;
                }
                queries++;
                if (HasPath(ai, tame, each.Enemy))
                {
                    best = each.Enemy;
                    break;
                }
                info.Far.Remember(each.Enemy, now);
            }
            Candidates.Clear();
            found = best;
        }

        private readonly struct Candidate : IComparable<Candidate>
        {
            internal readonly float Distance;
            internal readonly Character Enemy;

            internal Candidate(float distance, Character enemy)
            {
                Distance = distance;
                Enemy = enemy;
            }

            public int CompareTo(Candidate other) => Distance.CompareTo(other.Distance);
        }

        /// <summary>After the game kept or changed a tame's target: one behind a wall that it can't walk to, for the set time
        /// and two checks in a row, is let go of, and the tame calms down unless it was hurt in the last few seconds.</summary>
        internal static void AfterUpdateTarget(MonsterAI ai, ref bool canHear, ref bool canSee)
        {
            Character target = GameHooks.TargetCreature(ai);
            if (target == null)
            {
                return;
            }
            Character tame = Chosen(ai, out TameInfo info);
            float now = Time.time;
            if (tame == null || now < info.NextLetGoCheck)
            {
                return;
            }
            info.NextLetGoCheck = now + 0.5f;
            if (!info.LetGo.Check(target, OutOfReach(ai, tame, target), now, Settings.LetGoAfter.Value))
            {
                return;
            }
            info.LetGo.Reset();
            GameHooks.TargetCreature(ai) = null;
            canHear = false;
            canSee = false;
            GameHooks.SetTargetInfo(ai, ZDOID.None);
            if (GameHooks.TimeSinceHurt(ai) > 5f)
            {
                GameHooks.SetAlerted(ai, false);
            }
            if (Telling(info, now, LetGoLine))
            {
                Say(info, string.Format(LetGoLine, Utils.GetPrefabName(target.gameObject)));
            }
        }

        private static bool BehindWall(Character tame, TameInfo info, Character target)
        {
            float now = Time.time;
            if (info.SightTarget != target || now >= info.SightUntil)
            {
                info.SightTarget = target;
                info.SightUntil = now + 0.25f;
                info.SightBlocked = Walls.Between(tame.GetCenterPoint(), target.GetCenterPoint());
            }
            return info.SightBlocked;
        }

        private static bool OutOfReach(BaseAI ai, Character tame, Character enemy) =>
            Walls.Between(tame.GetCenterPoint(), enemy.GetCenterPoint()) && !HasPath(ai, tame, enemy);

        private static bool HasPath(BaseAI ai, Character tame, Character enemy) =>
            Pathfinding.instance == null || Pathfinding.instance.HavePath(tame.transform.position, enemy.transform.position, ai.m_pathAgentType);

        // ---------------------------------------------------------------------------------------------- points 2 and 4

        /// <summary>How often a walking tame's head is looked ahead of, in seconds.</summary>
        private const float CastEvery = 0.1f;

        /// <summary>A standing tame whose walk away found nowhere to go tries again after this long (code review 2, nit 9).</summary>
        private const float AwayAgain = 5f;

        private const string NowhereLine = "found nowhere to wander that fits, so it stands";
        private const string CantFinishLine = "stopped a walk it can't finish";
        private const string GoesNowhereLine = "ended a walk that went nowhere";
        private const string HeadLine = "stopped before its head reached a wall";
        private const string AwayLine = "walked away from the wall its head was in";

        /// <summary>
        /// Before the game's idle wander, every AI update while a chosen tame is idle: its own pick when one is due (point
        /// 4), and standing with its head in a wall, a walk away (point 2). The game's own timer, its rules for water, lava
        /// and fire and its walking are kept; the walk itself is watched once the game has moved it (AfterIdle). A tame not
        /// on its own navmesh is left to the game (decision 37; code review 1, nit 18).
        /// </summary>
        internal static void BeforeIdle(BaseAI ai)
        {
            Character tame = Chosen(ai, out TameInfo info);
            if (tame == null || tame.IsFlying() || tame.IsSwimming() || tame.HaveRider())
            {
                return;
            }
            Vector3 here = tame.transform.position;
            Vector3 center = Centre(ai, info, here, out bool far);
            if (far)
            {
                return;   // the game's own run back toward a far centre
            }
            float now = Time.time;
            ref float timer = ref GameHooks.WanderTimer(ai);
            ref bool reached = ref GameHooks.WanderReached(ai);
            ref Vector3 target = ref GameHooks.WanderTarget(ai);
            if (timer <= 0f)
            {
                if (Parts.WanderOn && World.Start(ai, tame, info, now))
                {
                    float range = ai.m_randomMoveRange * Settings.Range.Value / 100f;
                    if (WanderPick.Pick(center, here, tame.transform.forward, range, Settings.Tries.Value, Settings.WallMargin.Value, Random, World, out Vector3 point))
                    {
                        Walk(ai, info, here, now, ref reached, ref target, point);
                    }
                    else
                    {
                        reached = true;
                        Tell(info, now, NowhereLine);
                    }
                    timer = UnityEngine.Random.Range(ai.m_randomMoveInterval, ai.m_randomMoveInterval * 1.5f);
                }
                return;
            }
            if (reached && Parts.EaseOutOn && now >= info.NextEaseOut)
            {
                info.NextEaseOut = now + 0.5f;
                Reach reach = info.ReachNow(tame.transform);
                if (Walls.AheadOfHead(here, tame.transform.forward, reach, reach.Ahead, Foot(tame, info)) && World.Start(ai, tame, info, now))
                {
                    WalkAway(ai, tame, info, here, now, ref reached, ref target, reach);
                }
            }
        }

        /// <summary>
        /// After the game's idle wander moved a chosen tame, so its move is this update's (code review 2, finding 1), while
        /// it walks: a point the module didn't pick checked once; while it still turns toward its path's next corner, its
        /// creep forward stopped when a wall is ahead of its head, so it turns in place; once it faces the corner, a walk
        /// that goes nowhere ended, and the walk ended before its head can reach a wall, counting what it covers before the
        /// next look and its slide once stopped (point 2).
        /// </summary>
        internal static void AfterIdle(BaseAI ai)
        {
            Character tame = Chosen(ai, out TameInfo info);
            if (tame == null || tame.IsFlying() || tame.IsSwimming() || tame.HaveRider())
            {
                return;
            }
            ref bool reached = ref GameHooks.WanderReached(ai);
            float now = Time.time;
            Vector3 here = tame.transform.position;
            if (reached || !World.Start(ai, tame, info, now))
            {
                return;
            }
            Centre(ai, info, here, out bool far);
            if (far)
            {
                info.FreshWalk = false;
                return;   // the game's own run back toward a far centre, left alone while it runs too (the last look, finding 1)
            }
            Vector3 target = GameHooks.WanderTarget(ai);
            if (Parts.WanderOn && target != info.Picked)
            {
                // A point the module didn't pick (the game's own, or the search for a lost target, which shares these
                // fields): checked once.
                info.Picked = target;
                info.Stuck.Restart(here, now);
                if (!World.HavePath(here, target, out _))
                {
                    Stop(tame, ref reached);
                    Tell(info, now, CantFinishLine);
                    return;
                }
            }
            Vector3 move = tame.GetMoveDir();
            WalkPhase phase = Heading.Phase(info.FreshWalk, move, ai.m_moveMinAngle);
            info.FreshWalk = false;
            if (phase != WalkPhase.Heading)
            {
                // Set this update (its move still the last walk's), or still turning: not stuck; and turning with a creep
                // toward a wall, it turns in place instead.
                info.Stuck.Restart(here, now);
                if (phase == WalkPhase.Turning && Parts.HeadsOn && Heading.Creeping(move) && WallAhead(tame, info, here, now))
                {
                    tame.SetMoveDir(Vector3.zero);
                }
                return;
            }
            if (Parts.WanderOn && info.Stuck.Stuck(here, now, Settings.Stuck.Value))
            {
                Stop(tame, ref reached);
                Tell(info, now, GoesNowhereLine);
                return;
            }
            if (Parts.HeadsOn && WallAhead(tame, info, here, now))
            {
                Stop(tame, ref reached);
                Tell(info, now, HeadLine);
            }
        }

        // Whether a wall is ahead of a walking tame's head, looked at most every CastEvery (the answer kept in between):
        // as far as its reach and the margin, what it covers before the next look (which can come up to Travel.LookGap
        // later), and its slide once stopped.
        private static bool WallAhead(Character tame, TameInfo info, Vector3 here, float now)
        {
            if (now < info.NextHeadCast)
            {
                return info.WallWasAhead;
            }
            info.NextHeadCast = now + CastEvery;
            Reach reach = info.ReachNow(tame.transform);
            Vector3 velocity = tame.GetVelocity();
            velocity.y = 0f;
            float distance = Travel.Ahead(reach.Ahead, Settings.HeadMargin.Value, velocity.magnitude, tame.m_acceleration, Travel.LookGap);
            info.WallWasAhead = Walls.AheadOfHead(here, tame.transform.forward, reach, distance, Foot(tame, info));
            return info.WallWasAhead;
        }

        // A walk ended now: its move stops at once, and the game stops it from its next update.
        private static void Stop(Character tame, ref bool reached)
        {
            reached = true;
            tame.SetMoveDir(Vector3.zero);
        }

        // A walk to a point the module picked starts, and the game plans its path at once rather than following its last
        // one for up to a second (its cache: code review 2, finding 2).
        private static void Walk(BaseAI ai, TameInfo info, Vector3 here, float now, ref bool reached, ref Vector3 target, Vector3 point)
        {
            target = point;
            reached = false;
            info.Picked = point;
            info.Stuck.Restart(here, now);
            info.NextHeadCast = now;
            info.WallWasAhead = false;
            info.FreshWalk = true;
            if (GameHooks.LastPathTime != null)
            {
                GameHooks.LastPathTime(ai) = -1000f;
            }
        }

        // Standing with its head in a wall: a point behind it (or behind and to either side) that passes a pick's tests;
        // none: it tries again only after a while (code review 2, nit 9).
        private static void WalkAway(BaseAI ai, Character tame, TameInfo info, Vector3 here, float now, ref bool reached, ref Vector3 target, Reach reach)
        {
            Vector3 back = -tame.transform.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 1e-6f)
            {
                return;
            }
            back.Normalize();
            float distance = Mathf.Max(2f, reach.Ahead);
            foreach (float turn in AwayTurns)
            {
                Vector3 candidate = here + WanderPick.Turn(back, turn * Mathf.Deg2Rad) * distance;
                if (WanderPick.Takes(here, candidate, Settings.WallMargin.Value, World, out Vector3 point))
                {
                    Walk(ai, info, here, now, ref reached, ref target, point);
                    Tell(info, now, AwayLine);
                    return;
                }
            }
            info.NextEaseOut = now + AwayAgain;
        }

        // The wander's centre for this tame now (WanderCentre), and whether the walk is the game's own run back to a far one.
        private static Vector3 Centre(BaseAI ai, TameInfo info, Vector3 here, out bool far)
        {
            Vector3 patrol = Vector3.zero;
            bool has = GameHooks.PatrolPointOf != null && GameHooks.PatrolPointOf(ai, out patrol);
            return WanderCentre.Of(here, has, patrol, info.Commandable, Parts.FarHomeOn, ai.m_randomMoveRange, out far);
        }

        // Its body's lowest point above its root, now.
        private static float Foot(Character tame, TameInfo info) => info.FootOffset * tame.transform.localScale.y;

        // How fast the game drives its idle walk (Character.UpdateWalking: its speed, the world's enemy speed modifier and
        // its world level).
        private static float WalkSpeed(Character tame) =>
            tame.m_speed * Game.m_enemySpeedSize * (Game.instance != null ? 1f + Game.m_worldLevel * Game.instance.m_worldLevelEnemyMoveSpeedMultiplier : 1f);

        /// <summary>After the game's patrol point: a tame the game can't command ignores one farther than the set times
        /// its own wander range (decision 35): nothing is cleared, and a wolf's "stay" is kept.</summary>
        internal static void AfterPatrolPoint(BaseAI ai, Vector3 point, ref bool result)
        {
            if (!result)
            {
                return;
            }
            Character tame = Chosen(ai, out TameInfo info);
            if (tame != null && !info.Commandable && Utils.DistanceXZ(point, tame.transform.position) > ai.m_randomMoveRange * Settings.FarHomeTimes.Value)
            {
                result = false;
            }
        }

        // The tame an AI runs, if the part looks after it.
        private static Character Chosen(BaseAI ai, out TameInfo info)
        {
            info = null;
            Character tame = GameHooks.AiCharacter != null ? GameHooks.AiCharacter(ai) : null;
            if (tame == null || !tame.IsTamed())
            {
                return null;
            }
            info = Tames.Of(tame);
            return info.Penned ? tame : null;
        }

        private static void Tell(TameInfo info, float now, string what)
        {
            if (Telling(info, now, what))
            {
                Say(info, what);
            }
        }

        // With "write what it does to the log" on: each kind of thing a tame does at once, the same kind again at most every
        // ten seconds (some come every physics step: code review 2, nit 7). Asked before any line is put together (code
        // review 1, finding 9).
        private static bool Telling(TameInfo info, float now, string what) => Settings.PensLog.Value && info.Log.Due(what, now, LogEvery);

        private static void Say(TameInfo info, string what) => Module.Log.LogInfo($"House Trained: a {info.Prefab} {what} (tames in pens).");

        /// <summary>The notes forgotten: the part switched off, or the module after an error.</summary>
        internal static void Forget()
        {
            AfterBirth();
            AfterGrowUp();
        }

        /// <summary>
        /// The wander pick's world in game: the tame's own navmesh (its agent's ID, sampled at its own level: review F1),
        /// the game's own test of a point, the room for its body (Walls.Room, review F8), a full path with its last leg
        /// (Pathfinding.GetPath, review N9) and the head cast.
        /// </summary>
        private sealed class GameWorld : IWanderWorld
        {
            private readonly List<Vector3> corners = new List<Vector3>();
            private BaseAI ai;
            private Character character;
            private TameInfo info;
            private Transform root;
            private float level;
            private NavMeshQueryFilter filter;

            /// <summary>Set up for one tame; false when it isn't standing on its own navmesh (a Lox under a roof lower than
            /// its 7 m path height: the game's own wander then, decision 37), asked at most once a second per tame.</summary>
            internal bool Start(BaseAI forAi, Character tame, TameInfo forInfo, float now)
            {
                Pathfinding paths = Pathfinding.instance;
                if (paths == null || GameHooks.AgentId == null)
                {
                    return false;
                }
                ai = forAi;
                character = tame;
                info = forInfo;
                root = tame.transform;
                level = root.position.y;
                filter = new NavMeshQueryFilter { agentTypeID = GameHooks.AgentId(paths, forAi.m_pathAgentType), areaMask = NavMesh.AllAreas };
                if (now >= forInfo.OwnMeshUntil)
                {
                    forInfo.OwnMeshUntil = now + 1f;
                    forInfo.OnOwnMesh = NavMesh.SamplePosition(root.position + Vector3.up * 0.5f, out _, 1.5f, filter);
                }
                return forInfo.OnOwnMesh;
            }

            public bool Snap(Vector3 candidate, out Vector3 onMesh)
            {
                if (NavMesh.SamplePosition(new Vector3(candidate.x, level + 0.5f, candidate.z), out NavMeshHit hit, 2f, filter))
                {
                    onMesh = hit.position;
                    return true;
                }
                onMesh = candidate;
                return false;
            }

            public bool Valid(Vector3 point) => GameHooks.ValidWanderPoint == null || GameHooks.ValidWanderPoint(ai, point);

            public bool Clear(Vector3 point, Vector3 facing, float margin)
            {
                float scale = root.localScale.y;
                return Walls.Room(point, facing, info.BodyRadius * scale, info.BodyHeight * scale, margin);
            }

            public bool HavePath(Vector3 from, Vector3 to, out Vector3 arrival)
            {
                arrival = to - from;
                Pathfinding paths = Pathfinding.instance;
                if (paths == null || !paths.GetPath(from, to, corners, ai.m_pathAgentType, requireFullPath: true, cleanup: false, havePath: true))
                {
                    return false;
                }
                if (corners.Count >= 2)
                {
                    arrival = corners[corners.Count - 1] - corners[corners.Count - 2];
                }
                return true;
            }

            // Its head clear on arrival: the point is the floor, its root the body's lowest point below it (code review 2,
            // finding 5), and it ends up past the point by its slide less the half metre the game stops short (finding 1).
            public bool HeadClear(Vector3 point, Vector3 facing)
            {
                if (!Parts.HeadsOn)
                {
                    return true;
                }
                Reach reach = info.ReachNow(root);
                float foot = Foot(character, info);
                float distance = reach.Ahead + Settings.HeadMargin.Value + Travel.PastArrival(WalkSpeed(character), character.m_acceleration);
                return !Walls.AheadOfHead(point - Vector3.up * foot, facing, reach, distance, foot);
            }
        }
    }
}
