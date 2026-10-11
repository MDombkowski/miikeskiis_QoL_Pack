using System;
using System.Collections.Generic;

namespace Grounded
{
    /// <summary>One of a piece's colliders, as heights above its centre point (a mesh collider or any other shape), and
    /// the area its box covers seen from above, in square metres.</summary>
    internal readonly struct ColliderSpan
    {
        internal readonly bool IsMesh;
        internal readonly float Bottom;
        internal readonly float Top;
        internal readonly float Area;

        internal ColliderSpan(bool isMesh, float bottom, float top, float area)
        {
            IsMesh = isMesh;
            Bottom = bottom;
            Top = top;
            Area = area;
        }
    }

    /// <summary>
    /// How high a piece lies on the floor found under it, in metres, from plain numbers (the floor's top as its collider
    /// has it, the top of what is drawn of it, how far the piece is drawn below or above its own centre point, the rugs
    /// already lying there), so the scratch harness can run every rule without the game.
    ///
    /// A rug is "clip everything" in the game: its centre point goes exactly where it is aimed (Player.UpdatePlacementGhost),
    /// so for such a piece Grounded hands the game the height its centre point should have. Any other piece is set by the
    /// game so the underside of its collider touches the point aimed at, so for those Grounded hands the floor's surface.
    /// </summary>
    internal static class Heights
    {
        /// <summary>The most a floor's drawn top counts below its collider's top: a floor drawn farther below its box than
        /// this (some other shape, a modded floor) is taken as drawn this far below it. The game's wood and ashwood floors
        /// are 1.6 and 2.1 cm (the research).</summary>
        internal const float MaxGap = 0.05f;

        /// <summary>The most "down to what you see" lowers a piece below the floor's surface as the game sees it. The
        /// Deer rug on a wood floor needs 7.5 cm; a piece drawn far above its centre point never sinks out of sight.</summary>
        internal const float MaxSink = 0.10f;

        /// <summary>"Down to what you see" leaves the piece's lowest drawn part this far above the floor's drawn top, so the
        /// two are never drawn at one height (which flickers).</summary>
        internal const float SeenClearance = 0.002f;

        /// <summary>How far the part of a rug the hammer's ray is sure to meet stays above the floor's collider top while
        /// rugs are kept where the hammer can point at them.</summary>
        internal const float Clearance = 0.005f;

        /// <summary>"On top" also steps over a rug drawn up to this much more than a step above the new rug's own height
        /// (the same kind laid a step up, a near kind), so a new rug shows above the rugs under it drawn at nearly its
        /// height; rugs drawn higher than that (a fur rug under straw) stay above, as they look anyway.</summary>
        internal const float Near = 0.01f;

        /// <summary>The share of a rug's drawn area, seen from above, a collider must cover to count for PickTop.</summary>
        internal const float CoverShare = 0.5f;

        /// <summary>Rounding allowance when comparing heights.</summary>
        internal const float Slack = 0.0001f;

        /// <summary>The floor's surface as you see it: the top of what is drawn of it, never more than MaxGap below its
        /// collider's top, and never above it. The collider's top for "nothing drawn" (NaN).</summary>
        internal static float VisibleTop(float colliderTop, float drawnTop)
        {
            if (float.IsNaN(drawnTop) || drawnTop >= colliderTop)
            {
                return colliderTop;
            }
            return colliderTop - Math.Min(colliderTop - drawnTop, MaxGap);
        }

        /// <summary>
        /// The height above a piece's centre point that the hammer's ray is sure to meet first, from above, wherever it
        /// comes down on the piece: a box's (or sphere's, capsule's) top, and a mesh collider's lowest point (a sheet may
        /// be tilted, or thin and curved, and its box's top corner is no surface at all); the highest of those. NaN for no
        /// collider. The hammer's remove, copy and highlight take the first collider their ray meets (Player.RemovePiece),
        /// so this must stay above the floor's collider top for the rug, not the floor, to be pointed at.
        ///
        /// Only colliders covering at least CoverShare of what is drawn of the piece (<paramref name="drawnArea"/>, seen
        /// from above) count, since a small one (the Bearskin's seat, 0.7 m up, or its head) is met only where it is; with
        /// none that large, the largest collider alone (the Moose rug's box is smaller than its hide).
        /// </summary>
        internal static float PickTop(IList<ColliderSpan> colliders, float drawnArea)
        {
            float pick = float.NaN;
            if (colliders == null || colliders.Count == 0)
            {
                return pick;
            }
            int largest = 0;
            for (int i = 0; i < colliders.Count; i++)
            {
                if (colliders[i].Area > colliders[largest].Area)
                {
                    largest = i;
                }
                if (colliders[i].Area >= CoverShare * drawnArea)
                {
                    pick = float.IsNaN(pick) ? Sure(colliders[i]) : Math.Max(pick, Sure(colliders[i]));
                }
            }
            return float.IsNaN(pick) ? Sure(colliders[largest]) : pick;
        }

        private static float Sure(ColliderSpan collider) => collider.IsMesh ? collider.Bottom : collider.Top;

        /// <summary>
        /// Where a clip-everything piece's centre point goes, before any step above other rugs. "The game's surface": on
        /// the floor's collider top, as the game puts the first rug on a bare floor. "What you see": lower, so the lowest
        /// drawn part of the piece lies SeenClearance above the floor's visible top, but never higher than the game's
        /// surface (the Straw rug and the Bearskin are drawn below their centre point and stay there) and never more than
        /// MaxSink lower. Then lowered by the "Lower by" setting.
        ///
        /// Kept pickable: never so low that the part the hammer's ray is sure to meet (PickTop) sinks below the floor's
        /// collider top plus Clearance, nor above the game's surface.
        /// </summary>
        /// <param name="drawnMin">How far the lowest drawn part of the piece is above its centre point (negative: below).</param>
        /// <param name="pickTop">Heights.PickTop of its colliders; NaN for none.</param>
        internal static float BasePivot(HowLow mode, float gameTop, float visibleTop, float drawnMin, float lowerBy,
                                        bool keepPickable, float pickTop)
        {
            float pivot = gameTop;
            if (mode == HowLow.WhatYouSee)
            {
                float seen = Math.Min(visibleTop, gameTop) + SeenClearance - drawnMin;
                pivot = Math.Max(Math.Min(gameTop, seen), gameTop - MaxSink);
            }
            pivot -= Math.Max(0f, lowerBy);
            if (keepPickable && !float.IsNaN(pickTop))
            {
                pivot = Math.Max(pivot, Math.Min(gameTop, gameTop + Clearance - pickTop));
            }
            return pivot;
        }

        /// <summary>Where any other piece's collider rests: the floor's surface as the game sees it, or as you see it;
        /// then lowered by the "Lower by" setting.</summary>
        internal static float BaseSurface(HowLow mode, float gameTop, float visibleTop, float lowerBy)
        {
            float surface = mode == HowLow.WhatYouSee ? Math.Min(visibleTop, gameTop) : gameTop;
            return surface - Math.Max(0f, lowerBy);
        }

        /// <summary>
        /// Where the bottom of a new rug's drawing goes among the rugs already lying under it (each given by the bottom of
        /// its drawing). Two rugs whose drawings start less than a step apart could flicker where they overlap. With no
        /// step the rug stays at <paramref name="bottom"/>. "On top": a step above every rug drawn from less than a step
        /// below it to less than a step and Near above it, and so on up from each new height, so a new rug of the same
        /// kind lies above the ones under it; rugs drawn well above or below are left as they are. "Lowest free": the
        /// lowest height from <paramref name="bottom"/> up that is at least a step from every one of them.
        /// </summary>
        internal static float Stack(float bottom, IList<float> others, float step, Overlap overlap)
        {
            if (step <= 0f || others == null || others.Count == 0)
            {
                return bottom;
            }
            var sorted = new List<float>(others);
            sorted.Sort();
            if (overlap == Overlap.OnTop)
            {
                float height = bottom;
                foreach (float other in sorted)
                {
                    if (other <= bottom - step + Slack)
                    {
                        continue;   // a step or more below: no flicker, left below
                    }
                    if (other >= height + step + Near - Slack)
                    {
                        break;      // well above where it would lie, and so is every later one: left above
                    }
                    height = Math.Max(height, other + step);
                }
                return height;
            }
            // The free heights are those at least a step from every rug: the lowest of them from the bottom up is the
            // bottom itself or just above one of them.
            var candidates = new List<float> { bottom };
            foreach (float other in sorted)
            {
                if (other + step > bottom)
                {
                    candidates.Add(other + step);
                }
            }
            candidates.Sort();
            foreach (float candidate in candidates)
            {
                bool free = true;
                foreach (float other in sorted)
                {
                    if (Math.Abs(candidate - other) < step - Slack)
                    {
                        free = false;
                        break;
                    }
                }
                if (free)
                {
                    return candidate;
                }
            }
            return sorted[sorted.Count - 1] + step;
        }
    }
}
