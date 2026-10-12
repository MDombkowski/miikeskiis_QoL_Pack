using UnityEngine;

namespace HouseTrained
{
    /// <summary>What a tame's ground contact for one physics step is.</summary>
    internal enum GroundKind
    {
        /// <summary>Terrain, rock, a floor the rules don't touch: the game decides.</summary>
        Other,
        /// <summary>Another creature or a player.</summary>
        Creature,
        /// <summary>A building piece that counts as a wall (pieces players built, or any by the setting).</summary>
        WallPiece,
    }

    /// <summary>
    /// Tames in pens, point 1 (decisions 24 to 26), as plain rules: which ground contacts a tame doesn't count, and when a
    /// tame left on a creature is nudged off. The harness checks them; the patch on Character.UpdateGroundContact gathers
    /// the facts.
    /// </summary>
    internal static class GroundRule
    {
        /// <summary>Whether a contact at <paramref name="contactY"/> is on top of a creature whose body's centre is at
        /// <paramref name="otherCentreY"/>: at or above it (card B12). Below it, the two only touch at their sides.</summary>
        internal static bool OnTop(float contactY, float otherCentreY) => contactY >= otherCentreY;

        /// <summary>
        /// How high a ground contact is, for the step test (code review 1, finding 2): above the lowest point of the
        /// tame's body (its root capsule's bottom, which sits above the creature's root for some: 0.37 m for a moose calf),
        /// less what a rounded body touching a slope reads anyway (its radius × (1 − the surface's upward part): a body on
        /// a ramp touches it a little above its own bottom).
        /// </summary>
        /// <param name="footOffset">The capsule's bottom above the root, at scale 1.</param>
        /// <param name="surfaceUp">The upward part of the surface's normal where the contact is (1 on a flat top).</param>
        internal static float Height(float contactY, float rootY, float footOffset, float scale, float bodyRadius, float surfaceUp)
        {
            float slope = bodyRadius * scale * (1f - Mathf.Clamp01(surfaceUp));
            return contactY - (rootY + footOffset * scale) - slope;
        }

        /// <summary>The upward part of the surface a slope is allowed for: the piece's face that the short ray down met,
        /// only when it is the face the body touches (its normal the contact's own: a ramp, a stair's slope); at an edge,
        /// whatever the face beside it (a stair's or a ramp's side, a roof's low edge), or when the ray missed, 1: none
        /// (code review 2, finding 4).</summary>
        internal static float SlopeUp(bool metFace, Vector3 faceNormal, Vector3 contactNormal) =>
            metFace && Vector3.Dot(faceNormal, contactNormal) > 0.95f ? faceNormal.y : 1f;

        /// <summary>Whether the step's ground contact is cleared, so the tame doesn't count as on the ground: another
        /// creature never counts (no stacks), and a wall piece's edge higher than a step above its feet doesn't either
        /// (no walking up onto fences, walls or chests; a floor at its feet, stairs and ramps still carry it).</summary>
        internal static bool Clears(GroundKind kind, float heightAboveFeet, float step, bool noStacking, bool noClimbing)
        {
            switch (kind)
            {
                case GroundKind.Creature:
                    return noStacking;
                case GroundKind.WallPiece:
                    return noClimbing && heightAboveFeet > step;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Whether a walking tame faces where it walks or is still turning (code review 1, finding 1). A tame turns before it
    /// walks: the game sets its move along its facing, shortened by the angle to its path's next corner (BaseAI.MoveTowards:
    /// 1 − the angle ÷ its least move angle, 90° for every tame; nothing while it turns, without smooth movement), so the
    /// move's length tells how far it still has to turn. Its head is looked ahead of, and its walk timed for "stuck", only
    /// while it faces where it walks.
    /// </summary>
    internal static class Heading
    {
        /// <summary>The widest angle between its facing and its path's next corner that still counts.</summary>
        internal const float Widest = 30f;

        /// <summary>Facing within the widest angle of where it walks next, by the move the game last set.</summary>
        internal static bool Aimed(Vector3 move, float moveMinAngle)
        {
            move.y = 0f;
            float least = 1f - Mathf.Clamp01(Widest / Mathf.Max(1f, moveMinAngle));
            return move.sqrMagnitude > 1e-4f && move.magnitude >= least - 1e-4f;
        }

        /// <summary>Moving forward at all (while it turns, the game still moves it along its facing, a little: code
        /// review 2, finding 1).</summary>
        internal static bool Creeping(Vector3 move)
        {
            move.y = 0f;
            return move.sqrMagnitude > 1e-4f;
        }

        /// <summary>Which part of a walk this update is, for its watch: <paramref name="fresh"/>, the walk was set this very
        /// update, and the game, taking the first corner of its new path (where the tame stands) off, hasn't moved it along
        /// it yet, so its move is the last walk's (the last look, nit 3); else turning or heading, by its move.</summary>
        internal static WalkPhase Phase(bool fresh, Vector3 move, float moveMinAngle) =>
            fresh ? WalkPhase.Fresh : Aimed(move, moveMinAngle) ? WalkPhase.Heading : WalkPhase.Turning;
    }

    /// <summary>What a walking tame's update is, for the walk's watch (point 2).</summary>
    internal enum WalkPhase
    {
        /// <summary>Set this very update: nothing is judged on a move that is still the last walk's.</summary>
        Fresh,
        /// <summary>Still turning toward its path's next corner: not stuck, and a creep toward a wall is stopped.</summary>
        Turning,
        /// <summary>Facing where it goes: timed for stuck, and its head looked ahead of.</summary>
        Heading,
    }

    /// <summary>
    /// The idle wander's centre (code review 1, finding 6; code review 2, finding 3): the game's own, the tame's patrol point
    /// when it has one (a wolf told to stay), except that while "Ignore a far-away spawn point" is on, a spawn point near
    /// enough to centre the wander of a tame the game can't command no longer does; and whether it is far (more than
    /// twice the wander range), when the walk is the game's own run back, which the module leaves alone (before the walk
    /// and while it runs: the last look, finding 1). Plain values, so the harness checks it.
    /// </summary>
    internal static class WanderCentre
    {
        internal static Vector3 Of(Vector3 here, bool hasPatrol, Vector3 patrol, bool commandable, bool farHomeOn, float range, out bool far)
        {
            Vector3 center = hasPatrol && (commandable || !farHomeOn || Level(patrol - here) > range * 2f) ? patrol : here;
            far = Level(center - here) > range * 2f;
            return center;
        }

        private static float Level(Vector3 offset) => Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z);
    }

    /// <summary>
    /// How far a walking tame goes beyond where it is now (code review 2, finding 1): the game drives an idle walk at the
    /// creature's full speed (4 m/s for the Lox, 6 the moose, 7 the asksvin), and once its move stops, its speed falls by
    /// its acceleration every physics step (50 a second: Character.UpdateWalking), so it slides on (0.3 m for the Lox,
    /// 0.7 the moose, 1 the asksvin). Plain values, so the harness checks it.
    /// </summary>
    internal static class Travel
    {
        /// <summary>The longest gap between two looks ahead of a head, in seconds: a look is due 0.1 s after the last, and
        /// AI updates come every 0.04 or 0.06 s, so it can fall on the third update, 0.16 s on (the last look, nit 2).</summary>
        internal const float LookGap = 0.16f;

        /// <summary>How far it slides once its move stops.</summary>
        internal static float Slide(float speed, float acceleration) => acceleration > 0f ? speed * speed / (100f * acceleration) : 0f;

        /// <summary>How far ahead of its centre a walking tame's head must be clear now: its reach and the margin, what it
        /// covers before the next look, and its slide.</summary>
        internal static float Ahead(float reach, float margin, float speed, float acceleration, float every) =>
            reach + margin + speed * every + Slide(speed, acceleration);

        /// <summary>How far past a wander point it ends up: the game stops its move half a metre short (BaseAI.MoveTo),
        /// then it slides.</summary>
        internal static float PastArrival(float speed, float acceleration) => Mathf.Max(0f, Slide(speed, acceleration) - 0.5f);
    }

    /// <summary>
    /// The capsule cast that looks ahead of a tame's head (point 2), measured from its root: as wide as the head, from half
    /// a metre above the body's lowest point (or half the head's height above it, for a small creature) to the head's top,
    /// swept so its front ends at the distance asked (code review 1, finding 5 and nit 16; code review 2, finding 5).
    /// </summary>
    internal static class HeadCast
    {
        internal static void Band(Reach reach, float foot, out float low, out float high, out float radius)
        {
            radius = Mathf.Max(0.05f, reach.Radius);
            low = foot + Mathf.Min(0.5f, (reach.Top - foot) * 0.5f) + radius;
            high = Mathf.Max(low, reach.Top - radius);
        }

        /// <summary>How far the capsule is swept: its front starts a radius ahead of the centre.</summary>
        internal static float Sweep(float distance, float radius) => Mathf.Max(0f, distance - radius);
    }

    /// <summary>
    /// Per tame, when the log may be told each kind of thing again (code review 2, nit 7): a new kind at once, the same
    /// kind again only after the wait (some come every physics step). A few kinds at a time; the oldest is forgotten.
    /// Kinds are told apart by the string itself (each is a constant).
    /// </summary>
    internal sealed class LogGate
    {
        private readonly string[] kinds = new string[8];
        private readonly float[] until = new float[8];

        internal bool Due(string kind, float now, float wait)
        {
            int free = -1;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (ReferenceEquals(kinds[i], kind))
                {
                    if (now < until[i])
                    {
                        return false;
                    }
                    until[i] = now + wait;
                    return true;
                }
                // An empty slot first, else the one told longest ago.
                if (free < 0 || (kinds[free] != null && (kinds[i] == null || until[i] < until[free])))
                {
                    free = i;
                }
            }
            kinds[free] = kind;
            until[free] = now + wait;
            return true;
        }

        internal void Clear()
        {
            System.Array.Clear(kinds, 0, kinds.Length);
            System.Array.Clear(until, 0, until.Length);
        }
    }

    /// <summary>
    /// Per tame, the enemies behind walls that a path query found out of reach, remembered for a few seconds, so each
    /// search for another enemy asks about others (code review 2, nit 13). A few at a time; the oldest is forgotten.
    /// </summary>
    internal sealed class FarMemory
    {
        internal const float Lasts = 5f;

        private readonly object[] who = new object[4];
        private readonly float[] until = new float[4];

        internal bool Knows(object enemy, float now)
        {
            for (int i = 0; i < who.Length; i++)
            {
                if (ReferenceEquals(who[i], enemy) && now < until[i])
                {
                    return true;
                }
            }
            return false;
        }

        internal void Remember(object enemy, float now)
        {
            int slot = 0;
            for (int i = 0; i < who.Length; i++)
            {
                if (ReferenceEquals(who[i], enemy))
                {
                    slot = i;
                    break;
                }
                if (until[i] < until[slot])
                {
                    slot = i;
                }
            }
            who[slot] = enemy;
            until[slot] = now + Lasts;
        }

        internal void Clear()
        {
            System.Array.Clear(who, 0, who.Length);
            System.Array.Clear(until, 0, until.Length);
        }
    }

    /// <summary>A tame still on a creature after a second is nudged off (decision 26). Per tame.</summary>
    internal sealed class NudgeWatch
    {
        /// <summary>How long a tame must stay on creatures before the nudge, in seconds.</summary>
        internal const float After = 1f;

        /// <summary>A gap longer than this between two steps on a creature starts the count again.</summary>
        internal const float Gap = 0.25f;

        /// <summary>The most nudges in one Window (card B12): a tame still on a creature after them is left to the game,
        /// never pushed on and on.</summary>
        internal const int MostNudges = 3;

        /// <summary>The window MostNudges counts in, in seconds.</summary>
        internal const float Window = 10f;

        private float since = float.NaN;
        private float last = float.NaN;
        private float windowFrom = float.NaN;
        private int nudges;

        /// <summary>The tame stood on a creature in this step: true when it is time to nudge it (then the count starts
        /// again, so it's nudged at most once a second, and at most MostNudges times in Window seconds).</summary>
        internal bool OnCreature(float now)
        {
            if (float.IsNaN(since) || now - last > Gap)
            {
                since = now;
            }
            last = now;
            if (now - since < After)
            {
                return false;
            }
            since = now;
            if (float.IsNaN(windowFrom) || now - windowFrom >= Window)
            {
                windowFrom = now;
                nudges = 0;
            }
            if (nudges >= MostNudges)
            {
                return false;
            }
            nudges++;
            return true;
        }

        /// <summary>Which way to push: sideways, away from the other's centre (straight down would do nothing; if the two
        /// centres line up, its own right).</summary>
        internal static Vector3 Away(Vector3 tame, Vector3 other, Vector3 right)
        {
            Vector3 away = tame - other;
            away.y = 0f;
            return away.sqrMagnitude > 1e-4f ? away.normalized : right;
        }
    }

    /// <summary>
    /// A tame lets go of an enemy behind a wall that it can't walk to (decision 31): only once the enemy has stayed out of
    /// reach for the set time and at least two checks in a row, since a navmesh tile not built yet answers "no path" for a
    /// moment. Per tame.
    /// </summary>
    internal sealed class LetGoWatch
    {
        private float firstAt = float.NaN;
        private object watched;

        /// <summary>One check of the tame's target: true when it should let go of it now. The first check that finds a
        /// target out of reach only starts the count, so letting go always takes two checks in a row.</summary>
        internal bool Check(object target, bool outOfReach, float now, float after)
        {
            if (!outOfReach || target == null || !ReferenceEquals(target, watched))
            {
                watched = outOfReach ? target : null;
                firstAt = outOfReach && target != null ? now : float.NaN;
                return false;
            }
            return now - firstAt >= after;
        }

        /// <summary>Forget the watched target (it was let go, or the tame has none).</summary>
        internal void Reset()
        {
            watched = null;
            firstAt = float.NaN;
        }
    }

    /// <summary>
    /// Births and grow-ups land inside (decision 27): the spots around a parent a newborn is moved to when a wall stands
    /// between them, first clear one first: behind the parent (where the game puts it), in front, left, right.
    /// </summary>
    internal static class Placement
    {
        internal static Vector3[] AroundParent(Vector3 parent, Vector3 forward, float distance)
        {
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            var right = new Vector3(forward.z, 0f, -forward.x);
            return new[]
            {
                parent - forward * distance,
                parent + forward * distance,
                parent - right * distance,
                parent + right * distance,
            };
        }
    }
}
