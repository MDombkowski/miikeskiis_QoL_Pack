using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BarNone
{
    /// <summary>
    /// Smoke: puffs pass listed pieces, and a fire right under a listed piece keeps burning.
    /// Each game makes and moves its own smoke puffs (they aren't network objects), within 64 m of its own player. While the
    /// part is on, every puff carries a SmokePasser, which at the puff's first touch of a listed piece tells the physics
    /// engine to ignore that pair from then on. Switched on, the puffs already there get theirs; switched off, every pair is
    /// restored and the helpers go, so nothing is left behind. Fires are tested again a quarter of a second after the part,
    /// its fire settings or (while listed pieces don't count) its list change, instead of at their next test within 4 s.
    /// </summary>
    internal static class SmokePart
    {
        private static readonly Collider[] touching = new Collider[64];
        private static readonly RaycastHit[] above = new RaycastHit[16];
        private static readonly List<Collider> kept = new List<Collider>();
        private static readonly List<bool> listed = new List<bool>(16);

        /// <summary>How long after a change of the fire settings every fire is tested again (a slider dragged in F1 sends a
        /// change every frame).</summary>
        internal const float FireRetestDelay = 0.25f;

        private static bool wasOn;
        private static string puffList;
        private static FireState fireState = FireRules.StateOf(false, false, FireRules.GameClearSpace, null);
        private static bool fireStateKnown;
        private static float retestAt = -1f;
        private static int smokeLayer = -2;
        private static int touchMask;

        /// <summary>The part is switched on: the module on, "Smoke: passes through pieces" on, and Smoke_Collision not
        /// installed.</summary>
        internal static bool On => Module.IsOn && Settings.SmokeThrough.Value && !Rivals.SmokeStandsAside;

        /// <summary>Puffs pass listed pieces: the part on, and its hook on Smoke.Awake found and applied.</summary>
        internal static bool PuffsOn => On && !Broken.Is(Feature.SmokePuffs) && GameHooks.SmokeReady;

        /// <summary>Fires under listed pieces burn: the part on, and its hook on the fire's test found and applied.</summary>
        internal static bool FiresOn => On && !Broken.Is(Feature.Fires) && GameHooks.FireReady;

        /// <summary>The game's layer for smoke puffs (a puff touching another puff is never a piece), or -1.</summary>
        internal static int SmokeLayer => smokeLayer != -2 ? smokeLayer : (smokeLayer = LayerMask.NameToLayer("smoke"));

        /// <summary>After Smoke.Awake: the new puff gets its helper while the part is on.</summary>
        internal static void AfterSmokeAwake(Smoke puff)
        {
            if (puff != null && puff.GetComponent<SmokePasser>() == null)
            {
                puff.gameObject.AddComponent<SmokePasser>();
            }
        }

        /// <summary>Every frame (while the module runs): switched on, the puffs already there get their helpers and pass what
        /// they rest against; switched off, every pair is restored and the helpers removed; the smoke list changed, every
        /// live puff follows it; and every fire is tested again a moment after the part, its fire settings or (while listed
        /// pieces don't count) the list change. Compared field by field, so an unchanged frame allocates nothing.</summary>
        internal static void Follow()
        {
            bool on = PuffsOn;
            string list = on ? Settings.SmokePieces.Value : null;
            if (on != wasOn)
            {
                wasOn = on;
                puffList = list;
                if (on)
                {
                    foreach (IMonoUpdater each in Smoke.Instances.ToArray())
                    {
                        if (each is Smoke puff && puff != null)
                        {
                            SmokePasser passer = puff.GetComponent<SmokePasser>();
                            if (passer == null)
                            {
                                passer = puff.gameObject.AddComponent<SmokePasser>();
                            }
                            passer.PassWhatItTouches();
                        }
                    }
                }
                else
                {
                    RestorePairs();
                }
            }
            else if (on && !string.Equals(list, puffList, StringComparison.Ordinal))
            {
                puffList = list;
                foreach (SmokePasser passer in SmokePasser.Live.ToArray())
                {
                    if (passer != null)
                    {
                        passer.FollowTheList();
                    }
                }
            }

            FireState now = FireRules.StateOf(FiresOn, Settings.FiresUnderPieces.Value, Settings.FireClearSpace.Value, Settings.SmokePieces.Value);
            if (!fireStateKnown || !now.Equals(fireState))
            {
                bool first = !fireStateKnown;
                fireStateKnown = true;
                fireState = now;
                if (!first || now.On)
                {
                    retestAt = Time.unscaledTime + FireRetestDelay;
                }
            }
            if (retestAt >= 0f && Time.unscaledTime >= retestAt)
            {
                retestAt = -1f;
                CheckFiresAgain();
            }
        }

        /// <summary>Every ignored pair restored and every helper removed, and every fire tested again by the game's own
        /// rules at once (an undo step after an error).</summary>
        internal static void RestoreAll()
        {
            RestorePairs();
            fireState = FireRules.StateOf(false, false, FireRules.GameClearSpace, null);
            fireStateKnown = true;
            retestAt = -1f;
            CheckFiresAgain();
        }

        private static void RestorePairs()
        {
            foreach (SmokePasser passer in SmokePasser.Live.ToArray())
            {
                if (passer != null)
                {
                    passer.Restore();
                    Object.Destroy(passer);
                }
            }
            SmokePasser.Live.Clear();
            wasOn = false;
        }

        /// <summary>Each loaded fire's own test run again now (the postfix then applies, or not, by the settings as they
        /// are now). Not a lit fire whose vent reads blocked right now: that is also what a fire that has just loaded or
        /// just relit reads, until its first puff (its spawner's timer is stale), which the game avoids by waiting 4 s
        /// before a fire's first test; and a vent that really is blocked blocks the fire whatever the clear space says. Its
        /// own next test, within 4 s, will do.</summary>
        internal static void CheckFiresAgain()
        {
            if (GameHooks.CheckUnderTerrain == null)
            {
                return;
            }
            foreach (Fireplace fire in Object.FindObjectsByType<Fireplace>(FindObjectsSortMode.None))
            {
                SmokeSpawner vent = fire.m_smokeSpawner;
                if (vent != null && vent.gameObject.activeInHierarchy && vent.IsBlocked())
                {
                    continue;
                }
                try
                {
                    GameHooks.CheckUnderTerrain.Invoke(fire, null);
                }
                catch (Exception)
                {
                    // The game's own test on a fire not ready for it (one still starting): its next own test, within 4 s, will do.
                }
            }
        }

        /// <summary>The listed colliders overlapping any of this puff's colliders now (a puff resting against a cage floor
        /// when the part is switched on sends no new touch). Only the layers smoke collides with are searched, other puffs
        /// left out, so a pile of puffs can't crowd the piece out of the search.</summary>
        internal static List<Collider> ListedTouching(Collider[] own)
        {
            kept.Clear();
            int mask = TouchMask();
            foreach (Collider part in own)
            {
                if (part == null)
                {
                    continue;
                }
                Bounds bounds = part.bounds;
                int count = Physics.OverlapSphereNonAlloc(bounds.center, bounds.extents.magnitude + 0.05f, touching, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider other = touching[i];
                    if (other != null && !kept.Contains(other) && PieceLists.Smoke.Has(other))
                    {
                        kept.Add(other);
                    }
                }
            }
            return kept;
        }

        // Every layer the smoke layer collides with, by the game's own collision matrix, but the smoke layer itself.
        private static int TouchMask()
        {
            if (touchMask == 0)
            {
                int smoke = SmokeLayer;
                for (int layer = 0; layer < 32; layer++)
                {
                    if (layer != smoke && (smoke < 0 || !Physics.GetIgnoreLayerCollision(smoke, layer)))
                    {
                        touchMask |= 1 << layer;
                    }
                }
            }
            return touchMask;
        }

        /// <summary>After the game's Fireplace.CheckUnderTerrain: while the part is on and its fire settings differ from the
        /// game's, the fire's "blocked" is worked out again: the ground over it and its trapped smoke as the game tests
        /// them, and the ray up from its cover point (of the length set) counting only what isn't a listed piece.</summary>
        internal static void AfterCheckUnderTerrain(Fireplace fire)
        {
            bool skipListed = Settings.FiresUnderPieces.Value;
            float space = Mathf.Clamp(Settings.FireClearSpace.Value, 0f, 3f);
            if (!FireRules.Differs(skipListed, space) || fire.m_disableCoverCheck)
            {
                return;   // the game's own test, as it just ran (a fire that skips the test isn't blocked)
            }
            Vector3 position = fire.transform.position;
            bool groundAbove = Heightmap.GetHeight(position, out float height) && height > position.y + fire.m_checkTerrainOffset;
            bool pieceAbove = false;
            bool smokeTrapped = false;
            if (!groundAbove)
            {
                pieceAbove = PieceAbove(position + Vector3.up * fire.m_coverCheckOffset, space, skipListed);
                smokeTrapped = !pieceAbove && fire.m_smokeSpawner && fire.m_smokeSpawner.IsBlocked();
            }
            GameHooks.SetBlocked(fire, FireRules.Blocked(groundAbove, pieceAbove, smokeTrapped));
        }

        // The fire's ray, as the game casts it but of the length set, every hit collected (FireRules.PieceAbove decides).
        private static bool PieceAbove(Vector3 from, float space, bool skipListed)
        {
            if (space <= 0f)
            {
                return false;
            }
            int count = Physics.RaycastNonAlloc(from, Vector3.up, above, space, GameHooks.FireSolidMask);
            listed.Clear();
            for (int i = 0; i < count && i < above.Length; i++)
            {
                listed.Add(PieceLists.Smoke.Has(above[i].collider));
            }
            return FireRules.PieceAbove(count, above.Length, listed, skipListed);
        }
    }

    /// <summary>The fire's rules, apart from Unity so the harness can check them.</summary>
    internal static class FireRules
    {
        /// <summary>The game's clear space above a fire, in metres.</summary>
        internal const float GameClearSpace = 0.5f;

        /// <summary>Whether the settings ask for anything but the game's own test (then it's worked out again).</summary>
        internal static bool Differs(bool skipListed, float space) => skipListed || Math.Abs(space - GameClearSpace) > 0.0001f;

        /// <summary>What the fires' test depends on: when it changes, every fire is tested again. While the part is off,
        /// the settings don't count (the game's own test); while listed pieces count, the list doesn't.</summary>
        internal static FireState StateOf(bool on, bool skipListed, float space, string list) =>
            on ? new FireState(true, skipListed, space, skipListed ? list : null) : new FireState(false, false, GameClearSpace, null);

        /// <summary>Whether a hit above the fire lets it burn: a listed piece while listed pieces don't count.</summary>
        internal static bool Passes(bool skipListed, bool listed) => skipListed && listed;

        /// <summary>The ray's verdict: a piece is right above the fire when any hit doesn't pass. A full buffer can't tell,
        /// so it counts as blocked, as any hit would for the game.</summary>
        internal static bool PieceAbove(int count, int bufferSize, IReadOnlyList<bool> listed, bool skipListed)
        {
            if (count >= bufferSize)
            {
                return true;
            }
            for (int i = 0; i < count; i++)
            {
                if (!Passes(skipListed, listed[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The fire is blocked by the ground over it, a piece right above it, or its own trapped smoke, as in the
        /// game.</summary>
        internal static bool Blocked(bool groundAbove, bool pieceAbove, bool smokeTrapped) => groundAbove || pieceAbove || smokeTrapped;
    }

    /// <summary>What the fires' test depends on, compared field by field (no allocation per frame).</summary>
    internal readonly struct FireState : IEquatable<FireState>
    {
        internal readonly bool On;
        internal readonly bool SkipListed;
        internal readonly float Space;
        internal readonly string List;

        internal FireState(bool on, bool skipListed, float space, string list)
        {
            On = on;
            SkipListed = skipListed;
            Space = space;
            List = list;
        }

        public bool Equals(FireState other) =>
            On == other.On && SkipListed == other.SkipListed && Space.Equals(other.Space) && string.Equals(List, other.List, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is FireState other && Equals(other);

        public override int GetHashCode() => (On ? 1 : 0) + (SkipListed ? 2 : 0) + Space.GetHashCode() * 4;
    }

    /// <summary>
    /// Rides on a smoke puff while the smoke part is on. At the puff's first touch of a listed piece's collider, the physics
    /// engine is told to ignore that pair, so the puff drifts through. The collision's collider is read at once and the
    /// collision itself never kept: Valheim Performance Optimizations has the engine reuse one collision object for every
    /// callback. Every collider of the puff is paired, wherever it sits on the puff.
    /// </summary>
    internal sealed class SmokePasser : MonoBehaviour
    {
        /// <summary>Every helper alive, so switching off can restore them all.</summary>
        internal static readonly List<SmokePasser> Live = new List<SmokePasser>();

        private static readonly Action ignoreWaiting = IgnoreWaiting;
        private static SmokePasser waitingPasser;
        private static Collider waitingOther;

        private readonly List<Collider> ignored = new List<Collider>();
        private Collider[] own;

        private void Awake()
        {
            own = GetComponentsInChildren<Collider>();
            Live.Add(this);
        }

        private void OnDestroy()
        {
            Live.Remove(this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            Collider other = collision.collider;   // read at once: the collision may be reused for the next callback
            if (own == null || own.Length == 0 || other == null || !SmokePart.PuffsOn || other.gameObject.layer == SmokePart.SmokeLayer)
            {
                return;
            }
            waitingPasser = this;
            waitingOther = other;
            Module.Guard("letting smoke through a piece", ignoreWaiting);
        }

        /// <summary>Lets the puff through the listed pieces it already overlaps (when the part is switched on while it
        /// rests against one).</summary>
        internal void PassWhatItTouches()
        {
            if (own == null)
            {
                return;
            }
            foreach (Collider other in SmokePart.ListedTouching(own))
            {
                Ignore(other);
            }
        }

        /// <summary>The smoke list changed: a pair whose piece is no longer listed is restored, and the listed pieces the
        /// puff rests against now are passed.</summary>
        internal void FollowTheList()
        {
            for (int i = ignored.Count - 1; i >= 0; i--)
            {
                Collider other = ignored[i];
                if (other != null && PieceLists.Smoke.Has(other))
                {
                    continue;
                }
                if (other != null)
                {
                    foreach (Collider part in own)
                    {
                        if (part != null)
                        {
                            Physics.IgnoreCollision(part, other, false);
                        }
                    }
                }
                ignored.RemoveAt(i);
            }
            PassWhatItTouches();
        }

        /// <summary>Every pair this puff ignores is restored.</summary>
        internal void Restore()
        {
            foreach (Collider other in ignored)
            {
                if (other == null)
                {
                    continue;
                }
                foreach (Collider part in own)
                {
                    if (part != null)
                    {
                        Physics.IgnoreCollision(part, other, false);
                    }
                }
            }
            ignored.Clear();
        }

        internal int IgnoredCount => ignored.Count;

        // Ignores the pair, again if need be: the engine forgets a pair when either collider is switched off and on, and
        // the puff then touches it anew.
        private void Ignore(Collider other)
        {
            foreach (Collider part in own)
            {
                if (part != null)
                {
                    Physics.IgnoreCollision(part, other, true);
                }
            }
            if (!ignored.Contains(other))
            {
                ignored.Add(other);
            }
        }

        private static void IgnoreWaiting()
        {
            SmokePasser passer = waitingPasser;
            Collider other = waitingOther;
            waitingPasser = null;
            waitingOther = null;
            if (passer != null && other != null && PieceLists.Smoke.Has(other))
            {
                passer.Ignore(other);
            }
        }
    }
}
