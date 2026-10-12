using System;
using HoarstoneLink = QoLMods.HoarstoneGround.Ground;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>Where a tame stands toward a Hoarstone's ground, for "With the Hoarstone" (card B26).</summary>
    internal enum GroundState
    {
        /// <summary>Not looked after by the part (switched off, Hoarstone off, not chosen, a summon, ridden).</summary>
        None,

        /// <summary>Following a player: it goes where he goes (or, with "… while following you" off, to the edge).</summary>
        Following,

        /// <summary>On the ground: it keeps to it.</summary>
        Inside,

        /// <summary>Off the ground, and kept to it: it walks back in (once any fight is over).</summary>
        Returning,

        /// <summary>Off the ground and free: told to stay out there, or never on it.</summary>
        Away,
    }

    /// <summary>
    /// "With the Hoarstone" (card B26, his words of 2026-10-11), the rules on plain values, so the harness checks them:
    /// where a tame stands toward the ground and what it does about it, the points it walks to, who guards and whom, and
    /// the F1 title. The ground itself is the Hoarstone module's (the Hoarstone ground part); a tame's own state is the
    /// game's (follow, its patrol point) and the module's (where it last was inside, its home).
    /// </summary>
    internal static class GroundRules
    {
        /// <summary>How long after a tame was last seen inside a step outside still counts as having just left (a chase, a
        /// flee, a push), in seconds; later, a tame found outside with no home was never kept.</summary>
        internal const float JustLeft = 10f;

        /// <summary>How far past the edge a tame sent home goes in, in metres.</summary>
        internal const float Depth = 4f;

        /// <summary>The step of a walk along a line, in metres (the shapes' squares are 2 m).</summary>
        internal const float Step = 1f;

        /// <summary>The title row of the part in F1: it works only while Hoarstone is on (his words: "make sure it's clear
        /// in the mod config at the top somewhere that this only is enabled if Hoarstone is enabled"), and whether it is.</summary>
        internal static string Title(HoarstoneLink.Standing hoarstone)
        {
            switch (hoarstone)
            {
                case HoarstoneLink.Standing.On:
                    return "With the Hoarstone (works only while Hoarstone is on: it is)";
                case HoarstoneLink.Standing.Off:
                    return "With the Hoarstone (works only while Hoarstone is on: it's switched off)";
                default:
                    return "With the Hoarstone (works only while Hoarstone is on: it isn't installed)";
            }
        }

        /// <summary>The layers of settlements whose ground keeps tames: those with a Hoarstone, and the others too with
        /// "Also settlements without a Hoarstone".</summary>
        internal static int Layers(bool othersToo) => HoarstoneLink.WithAHoarstone | (othersToo ? HoarstoneLink.WithoutAHoarstone : 0);

        /// <summary>
        /// Where a tame stands toward the ground now. <paramref name="writeHome"/>: it has just stepped off the ground it
        /// was kept on, with no home yet, so its last place inside becomes its home.
        /// - Following someone: Following, whatever else.
        /// - On the ground: Inside.
        /// - Off it, with a patrol point (told to stay, or a spawner's point): Returning when the point is on the ground
        ///   (told to stay inside, then pushed or chased out), else Away (told to stay out there, as in the game).
        /// - Off it, with a home (it left in a fight, or was sent home): Returning.
        /// - Off it, last seen inside a moment ago: Returning, and its last place inside becomes its home.
        /// - Otherwise Away: never kept (caught out there, or left long ago).
        /// </summary>
        internal static GroundState Classify(bool following, bool inside, bool hasPatrol, bool patrolInside, bool hasHome, bool justLeft,
                                             out bool writeHome)
        {
            writeHome = false;
            if (following)
            {
                return GroundState.Following;
            }
            if (inside)
            {
                return GroundState.Inside;
            }
            if (hasPatrol)
            {
                return patrolInside ? GroundState.Returning : GroundState.Away;
            }
            if (hasHome)
            {
                return GroundState.Returning;
            }
            if (justLeft)
            {
                writeHome = true;
                return GroundState.Returning;
            }
            return GroundState.Away;
        }

        /// <summary>Whether a step outside is still "just left": the tame was seen inside no longer ago than
        /// <see cref="JustLeft"/> (NaN: never seen inside).</summary>
        internal static bool LeftJustNow(float now, float lastInsideAt) => !float.IsNaN(lastInsideAt) && now - lastInsideAt <= JustLeft && now >= lastInsideAt;

        /// <summary>Whether a Returning tame walks home now: not while it fights (an enemy is its target) with "… they may
        /// leave it to fight" on; off, its fight off the ground is let go of (<see cref="LetGoOutside"/>), so it walks.</summary>
        internal static bool WalksHome(GroundState state, bool hasTarget, bool fightAllowed) =>
            state == GroundState.Returning && !(hasTarget && fightAllowed);

        /// <summary>Whether a tame kept to the ground lets go of its target off the ground: with "… they may leave it to
        /// fight" off, unless that enemy hurt it a moment ago (it defends itself).</summary>
        internal static bool LetGoOutside(GroundState state, bool fightAllowed, bool targetInside, float timeSinceHurt) =>
            (state == GroundState.Inside || state == GroundState.Returning) && !fightAllowed && !targetInside && timeSinceHurt > 5f;

        /// <summary>Whether "Return to base" applies to a tame (its key shown and pressed): the part on, Hoarstone on, a
        /// tame chosen, not a summon, and off the ground.</summary>
        internal static bool ReturnApplies(bool partOn, bool returnOn, bool hoarstoneOn, bool tamed, bool chosen, bool summon, bool inside) =>
            partOn && returnOn && hoarstoneOn && tamed && chosen && !summon && !inside;

        /// <summary>
        /// The first point on the ground along the line from a tame toward a place (its home, or a Hoarstone), then
        /// <paramref name="depth"/> further in while still on it: where it's sent. False when the line never reaches the
        /// ground within <paramref name="most"/> metres (the ground there isn't known on this game).
        /// </summary>
        internal static bool MarchIn(Vector3 from, Vector3 toward, Func<Vector3, bool> inside, float step, float most, float depth, out Vector3 point)
        {
            point = toward;
            Vector3 line = toward - from;
            line.y = 0f;
            float length = line.magnitude;
            if (length < 1e-3f)
            {
                return inside(from) && Assign(from, out point);
            }
            Vector3 way = line / length;
            float limit = Mathf.Min(length + depth, most);
            for (float d = 0f; d <= limit; d += step)
            {
                Vector3 here = from + way * d;
                if (!inside(here))
                {
                    continue;
                }
                // In: on along the line while still inside, up to the depth (and never past the place itself plus the depth).
                float deepest = d;
                for (float e = d + step; e <= Mathf.Min(d + depth, limit); e += step)
                {
                    if (!inside(from + way * e))
                    {
                        break;
                    }
                    deepest = e;
                }
                point = from + way * deepest;
                point.y = toward.y;
                return true;
            }
            return false;
        }

        private static bool Assign(Vector3 value, out Vector3 point)
        {
            point = value;
            return true;
        }

        /// <summary>
        /// For a follower with "… they may leave it while following you" off: the last point on the ground along the line
        /// from the tame toward the player, <paramref name="margin"/> short of the edge (the tame's own place when even
        /// that is past it). False when the player is on the ground (it follows as in the game).
        /// </summary>
        internal static bool Edge(Vector3 tame, Vector3 player, Func<Vector3, bool> inside, float step, float margin, out Vector3 point)
        {
            point = tame;
            if (inside(player))
            {
                return false;
            }
            Vector3 line = player - tame;
            line.y = 0f;
            float length = line.magnitude;
            if (length < 1e-3f)
            {
                return true;
            }
            Vector3 way = line / length;
            float last = 0f;
            for (float d = step; d <= length; d += step)
            {
                if (!inside(tame + way * d))
                {
                    break;
                }
                last = d;
            }
            point = tame + way * Mathf.Max(0f, last - margin);
            return true;
        }

        /// <summary>Whether a tame in a pen may go for an enemy, by "Tames in a pen guard it": never, only while a door or
        /// gate of its pen is open, or always; a tame not in a pen always may (it still needs a way there).</summary>
        internal static bool PennedMayGo(PennedGuard rule, bool penned, bool gateOpen) =>
            !penned || rule == PennedGuard.Always || (rule == PennedGuard.ThroughAnOpenGate && gateOpen);

        /// <summary>Whether a tame guards the ground now: the setting on, on the ground, free (not following, not told to
        /// stay: a patrol point keeps it at its spot, as in the game), not ridden, able to fight, and without a target of
        /// its own.</summary>
        internal static bool Guards(bool guardOn, GroundState state, bool hasPatrol, bool ridden, bool canFight, bool hasTarget) =>
            guardOn && state == GroundState.Inside && !hasPatrol && !ridden && canFight && !hasTarget;

        /// <summary>Whether a creature on the ground is one tames guard against: alive, not a player, not tamed, not asleep,
        /// not flying or swimming (no tame can get to it), not one the game's AI skips, the game's enemy of the tame, and
        /// not a harmless animal unless "… against harmless animals too".</summary>
        internal static bool Intruder(bool dead, bool player, bool tamed, bool asleep, bool flying, bool swimming, bool skipped, bool enemy,
                                      bool harmless, bool harmlessToo) =>
            !dead && !player && !tamed && !asleep && !flying && !swimming && !skipped && enemy && (!harmless || harmlessToo);

        /// <summary>Whether an intruder is within "How far they go for an enemy" (0: anywhere).</summary>
        internal static bool WithinReach(float distance, int reach) => reach <= 0 || distance <= reach;

        /// <summary>Whether a hunt is over: the enemy gone, dead, off the ground, or no nearer for <paramref name="stuckFor"/>
        /// seconds.</summary>
        internal static bool HuntOver(bool enemyGone, bool enemyDead, bool enemyInside, float noCloserFor, float stuckFor) =>
            enemyGone || enemyDead || !enemyInside || noCloserFor >= stuckFor;

        /// <summary>Whether a tame that "arrived" where the game sent it is still far from the enemy (no path, or a partial
        /// one), so it's sent on again rather than left to search around a stale spot.</summary>
        internal static bool SendOnAgain(bool arrived, float distanceToEnemy, float attackReach) => arrived && distanceToEnemy > Mathf.Max(3f, attackReach + 1f);

        /// <summary>The faction of animals that never fight (deer, hares and the like): the game's AnimalsVeg.</summary>
        internal static bool Harmless(Character.Faction faction) => faction == Character.Faction.AnimalsVeg;
    }
}
