using System;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>What a wander pick asks of the world: the game's navmesh and physics in game, a made-up pen in the
    /// harness.</summary>
    internal interface IWanderWorld
    {
        /// <summary>The candidate put on the tame's own navmesh at the tame's own level (sampled there with its own agent,
        /// never given its height from above as the game does, which lands on a roof: design review F1); false when there
        /// is no navmesh of its own there.</summary>
        bool Snap(Vector3 candidate, out Vector3 onMesh);

        /// <summary>The game's own test of a wander point (BaseAI.IsValidRandomMovePoint: water, lava, fire).</summary>
        bool Valid(Vector3 point);

        /// <summary>Whether the tame's body, standing at the point facing that way, keeps the margin from every wall
        /// (measured physically, review F8).</summary>
        bool Clear(Vector3 point, Vector3 facing, float margin);

        /// <summary>Whether the tame can walk all the way from one point to the other (a full path), and the direction of
        /// the path's last leg, the way it arrives.</summary>
        bool HavePath(Vector3 from, Vector3 to, out Vector3 arrival);

        /// <summary>Whether the tame's head stays clear of walls when it stands at the point, facing that way.</summary>
        bool HeadClear(Vector3 point, Vector3 facing);
    }

    /// <summary>
    /// Tames in pens, point 4 (decision 34): where a tame wanders next. Picked the game's way (a random direction turned
    /// from its facing, 0.7 to 1.0 × its range from the wander's centre: where it stands, or its patrol point;
    /// BaseAI.RandomMovement), but only a point on its own navmesh at its own level, that the game itself would take,
    /// with its body the margin from the walls, a full path and its head clear on arrival; two tries at the game's
    /// distance, two at half, two at a quarter, and so on. None fits: the tame stands still until its next pick. Plain
    /// values only (the rotation is worked out here, not by the engine's Quaternion), so the harness runs it on made-up
    /// pens against the research's simulation.
    /// </summary>
    internal static class WanderPick
    {
        /// <summary>Picks a wander point, or returns false (stand still).</summary>
        /// <param name="center">The wander's centre, as the game takes it: where a tame stands, or its patrol point (a
        /// wolf told to stay, a spawner's tame near its spawner): code review 1, finding 6.</param>
        /// <param name="from">Where the tame stands: the paths start there, and the points are put at its level.</param>
        /// <param name="forward">Where it faces (the game turns the random direction from its facing).</param>
        /// <param name="range">Its wander range, scaled by "How far tames wander".</param>
        /// <param name="random">A random number between the two bounds (UnityEngine.Random.Range in game).</param>
        internal static bool Pick(Vector3 center, Vector3 from, Vector3 forward, float range, int tries, float margin, Func<float, float, float> random,
                                  IWanderWorld world, out Vector3 point)
        {
            point = from;
            center.y = from.y;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();
            for (int i = 0; i < tries; i++)
            {
                float scale = Scale(i);
                float angle = random(0f, 360f) * Mathf.Deg2Rad;
                float distance = random(range * 0.7f, range) * scale;
                Vector3 candidate = center + Turn(forward, angle) * distance;
                if (Takes(from, candidate, margin, world, out Vector3 onMesh))
                {
                    point = onMesh;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The distance's scale for each try: 1, 1, 0.5, 0.5, 0.25, 0.25 …</summary>
        internal static float Scale(int attempt) => Mathf.Pow(0.5f, attempt / 2);

        /// <summary>A direction turned about the vertical by an angle in radians, as Quaternion.Euler(0, degrees, 0) turns
        /// it.</summary>
        internal static Vector3 Turn(Vector3 direction, float radians)
        {
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector3(cos * direction.x + sin * direction.z, 0f, -sin * direction.x + cos * direction.z);
        }

        /// <summary>Whether a candidate passes every test, cheapest first; <paramref name="onMesh"/> is where it lands on
        /// the navmesh.</summary>
        internal static bool Takes(Vector3 from, Vector3 candidate, float margin, IWanderWorld world, out Vector3 onMesh)
        {
            if (!world.Snap(candidate, out onMesh) || !world.Valid(onMesh))
            {
                return false;
            }
            Vector3 straight = Level(onMesh - from, Vector3.forward);
            if (!world.Clear(onMesh, straight, margin))
            {
                return false;
            }
            if (!world.HavePath(from, onMesh, out Vector3 arrival))
            {
                return false;
            }
            return world.HeadClear(onMesh, Level(arrival, straight));
        }

        /// <summary>A direction made level and of length 1, or the fallback when it has none.</summary>
        internal static Vector3 Level(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 1e-6f ? direction.normalized : fallback;
        }
    }

    /// <summary>
    /// An idle walk that goes nowhere ends there (decision 36): walking toward a wander point but not 0.2 m further after
    /// the set time. Per tame; plain values, so the harness checks it.
    /// </summary>
    internal sealed class StuckWatch
    {
        /// <summary>A walk must get at least this far in the set time.</summary>
        internal const float Least = 0.2f;

        private Vector3 mark;
        private float markedAt = float.NaN;

        /// <summary>A new walk starts (or the tame stood still): measure from here.</summary>
        internal void Restart(Vector3 position, float now)
        {
            mark = position;
            markedAt = now;
        }

        /// <summary>Called while it walks: true when it has got less than 0.2 m in the last <paramref name="after"/>
        /// seconds (0: never).</summary>
        internal bool Stuck(Vector3 position, float now, float after)
        {
            if (after <= 0f)
            {
                return false;
            }
            if (float.IsNaN(markedAt))
            {
                Restart(position, now);
                return false;
            }
            Vector3 moved = position - mark;
            moved.y = 0f;
            if (moved.sqrMagnitude >= Least * Least)
            {
                Restart(position, now);
                return false;
            }
            return now - markedAt >= after;
        }
    }
}
