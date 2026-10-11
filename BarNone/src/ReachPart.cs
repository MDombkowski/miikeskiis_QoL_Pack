using System;
using System.Collections.Generic;
using UnityEngine;

namespace BarNone
{
    /// <summary>
    /// Reach: what he points at, past listed pieces. Every frame the game casts one ray from the camera (50 m, the
    /// player's own mask), sorts the hits and takes the first one that isn't his own body, within his reach (3.5 m from his
    /// eye plus the target's hover offset); nothing behind it counts (Player.FindHoverObject). After it, unless the game's
    /// answer is something usable (never passed), this casts the same ray again, and when the first hit really is a listed
    /// piece, walks past listed pieces to the first hit that isn't one and takes that exactly as the game takes a first hit.
    /// (The first hit is judged by its own collider, so a listed wall built on a ship counts, though the game names the
    /// ship as the target.)
    /// </summary>
    internal static class ReachPart
    {
        internal const float RayLength = 50f;

        private static readonly RaycastHit[] hits = new RaycastHit[64];
        private static readonly LiveHit[] live = CreateLive(hits.Length);
        private static readonly IComparer<RaycastHit> byDistance = new ByDistance();

        private static int maskOf;
        private static int mask;

        /// <summary>The part runs: the module on, "Reach: through see-through pieces" on, its hooks found, and its key held
        /// if one is set.</summary>
        internal static bool On =>
            Module.IsOn && Settings.ReachThrough.Value && !Broken.Is(Feature.Reach) && GameHooks.ReachReady &&
            (Settings.ReachKey.Value.MainKey == KeyCode.None || Settings.Held(Settings.ReachKey.Value));

        /// <summary>After the game's FindHoverObject: its answer stands unless there's something to reach behind listed
        /// pieces (ReachWalk.Choose).</summary>
        internal static void AfterFindHoverObject(Player player, ref GameObject hover, ref Character hoverCreature)
        {
            if (hover != null && PieceNames.IsUsable(hover))
            {
                return;   // the game points at something usable, which is never passed: its answer stands as it is
            }
            GameCamera camera = GameCamera.instance;
            if (camera == null || player.m_eye == null)
            {
                return;
            }
            Transform view = camera.transform;
            int count = Physics.RaycastNonAlloc(view.position, view.forward, hits, RayLength, MaskOf(player));
            Array.Sort(hits, 0, count, byDistance);
            for (int i = 0; i < count; i++)
            {
                live[i].Set(hits[i], player);
            }
            int chosen = ReachWalk.Choose(live, count, Settings.ReachCreatures.Value, player.m_maxInteractDistance,
                                          out bool inReach, out bool showsCreature);
            if (chosen < 0)
            {
                return;
            }
            Collider target = hits[chosen].collider;
            hoverCreature = showsCreature ? CreatureOf(target) : null;
            hover = inReach ? HoverObjectOf(target) : null;
        }

        /// <summary>The creature a hit collider belongs to, the way the game finds it: through its rigidbody, else on the
        /// collider's own object.</summary>
        internal static Character CreatureOf(Collider collider) =>
            collider.attachedRigidbody ? collider.attachedRigidbody.GetComponent<Character>() : collider.GetComponent<Character>();

        // The object the game makes the target for a hit: the collider's own object if it's hoverable itself, else its
        // rigidbody's, else the collider's object.
        private static GameObject HoverObjectOf(Collider collider)
        {
            if (collider.GetComponent<Hoverable>() != null)
            {
                return collider.gameObject;
            }
            return collider.attachedRigidbody ? collider.attachedRigidbody.gameObject : collider.gameObject;
        }

        // The player's own hover mask, read once per player (the game sets it in Player.Awake and never changes it).
        private static int MaskOf(Player player)
        {
            int id = player.GetInstanceID();
            if (maskOf != id || mask == 0)
            {
                mask = GameHooks.InteractMask(player);
                maskOf = id;
            }
            return mask;
        }

        private static LiveHit[] CreateLive(int size)
        {
            var made = new LiveHit[size];
            for (int i = 0; i < size; i++)
            {
                made[i] = new LiveHit();
            }
            return made;
        }

        // The game's own order for the hover hits: by distance.
        private sealed class ByDistance : IComparer<RaycastHit>
        {
            public int Compare(RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance);
        }

        /// <summary>One hit of this frame's ray, its facts worked out only when the walk asks for them.</summary>
        private sealed class LiveHit : ReachWalk.IHit
        {
            private RaycastHit hit;
            private Player player;

            internal void Set(RaycastHit newHit, Player newPlayer)
            {
                hit = newHit;
                player = newPlayer;
            }

            public bool IsSelf => hit.collider.attachedRigidbody && hit.collider.attachedRigidbody.gameObject == player.gameObject;

            public bool IsListed => PieceLists.Reach.Has(hit.collider);

            public bool IsUsable => PieceNames.IsUsable(hit.collider);

            public bool IsCreature => CreatureOf(hit.collider) != null;

            public bool CreatureShows
            {
                get
                {
                    Character character = CreatureOf(hit.collider);
                    return character != null && (!character.GetBaseAI() || !character.GetBaseAI().IsSleeping()) &&
                           !ParticleMist.IsMistBlocked(player.GetCenterPoint(), character.GetCenterPoint());
                }
            }

            public float EyeDistance => Vector3.Distance(player.m_eye.position, hit.point);

            public float HoverOffset => hit.collider.GetComponentInParent<Hoverable>()?.GetHoverOffset() ?? 0f;
        }
    }

    /// <summary>
    /// The reach part's rule, apart from Unity so the harness can check it: which hit of the sorted hover ray becomes the
    /// target once listed pieces are passed, or none, and then the game's own answer stands.
    /// </summary>
    internal static class ReachWalk
    {
        internal interface IHit
        {
            /// <summary>His own body (the game skips it).</summary>
            bool IsSelf { get; }

            /// <summary>A piece on the reach list.</summary>
            bool IsListed { get; }

            /// <summary>Usable itself (an Interactable: a door, a gate): never passed. A hover text alone isn't use.</summary>
            bool IsUsable { get; }

            /// <summary>A creature (a Character).</summary>
            bool IsCreature { get; }

            /// <summary>A creature the game would name as the hover creature: awake, and not hidden by mist.</summary>
            bool CreatureShows { get; }

            /// <summary>From his eye to the point hit.</summary>
            float EyeDistance { get; }

            /// <summary>The hit's hoverable's own offset, added to his reach (0 without one).</summary>
            float HoverOffset { get; }
        }

        /// <summary>
        /// The index of the hit to point at, or -1 to keep the game's answer. The game's answer stands when the first hit
        /// that isn't his body isn't a listed piece, when nothing lies behind the listed pieces, when what does is a creature
        /// and creatures aren't reached, and when it's neither within reach nor a creature to name. A listed piece that can
        /// be used itself is never passed: it is taken as the game takes it. For the hit chosen, <paramref name="inReach"/>
        /// says whether it becomes the target (else the target is nothing) and <paramref name="showsCreature"/> whether it
        /// is named the hover creature, both by the game's own rules.
        /// </summary>
        internal static int Choose(IReadOnlyList<IHit> hits, int count, bool creaturesToo, float reach, out bool inReach, out bool showsCreature)
        {
            inReach = false;
            showsCreature = false;
            bool first = true;
            for (int i = 0; i < count; i++)
            {
                IHit hit = hits[i];
                if (hit.IsSelf)
                {
                    continue;
                }
                bool listed = hit.IsListed;
                if (first)
                {
                    first = false;
                    if (!listed)
                    {
                        return -1;
                    }
                }
                if (listed && !hit.IsUsable)
                {
                    continue;
                }
                if (hit.IsCreature && !creaturesToo)
                {
                    return -1;
                }
                bool within = hit.EyeDistance < reach + hit.HoverOffset;
                bool names = hit.IsCreature && hit.CreatureShows;
                if (!within && !names)
                {
                    return -1;
                }
                inReach = within;
                showsCreature = names;
                return i;
            }
            return -1;
        }
    }
}
