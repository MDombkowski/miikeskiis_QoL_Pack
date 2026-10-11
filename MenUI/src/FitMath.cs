using System;

namespace MenUI
{
    /// <summary>
    /// Where a menu's buttons should go, worked out on numbers alone (heights along the window, in any one unit), so it
    /// can be checked without the game. A menu is scaled about its pivot, as Unity scales a transform, then shifted.
    /// </summary>
    internal static class FitMath
    {
        private const float Tiny = 0.0001f;

        /// <summary>
        /// Fits the buttons' extent [bottom, top] into the band [low, high].
        /// </summary>
        /// <param name="pivot">Where the menu's pivot is: scaling happens about it.</param>
        /// <param name="anchor">What stays put while it shrinks: 1 its top, 0.5 its middle.</param>
        /// <param name="smallest">The smallest scale before moving, for ShrinkThenMoveUp (0 to 1).</param>
        /// <param name="scale">The scale to apply (1 = as it is).</param>
        /// <param name="shift">How far to move it after scaling (positive = up).</param>
        /// <returns>False when nothing needs to change (it fits, or nothing could be worked out).</returns>
        internal static bool Solve(float bottom, float top, float pivot, float low, float high, float anchor,
                                   FitMode mode, float smallest, out float scale, out float shift)
        {
            scale = 1f;
            shift = 0f;
            float height = top - bottom;
            float room = high - low;
            if (height <= Tiny || room <= Tiny || float.IsNaN(height) || float.IsNaN(room))
            {
                return false;
            }
            if (bottom >= low - Tiny && top <= high + Tiny)
            {
                return false;
            }

            anchor = Math.Max(0f, Math.Min(1f, anchor));
            smallest = Math.Max(0.05f, Math.Min(1f, smallest));
            float anchorAt = bottom + anchor * height;

            // The largest scale that fits by moving as well: the whole band's height.
            float byMoving = Math.Min(1f, room / height);

            // The largest scale that fits without moving, the anchor staying put; 0 when no scale can.
            float inPlace = 1f;
            float below = anchorAt - bottom;
            float above = top - anchorAt;
            if (below > Tiny)
            {
                inPlace = Math.Min(inPlace, (anchorAt - low) / below);
            }
            if (above > Tiny)
            {
                inPlace = Math.Min(inPlace, (high - anchorAt) / above);
            }
            if (anchorAt < low - Tiny || anchorAt > high + Tiny)
            {
                inPlace = 0f;
            }

            float k;
            switch (mode)
            {
                case FitMode.MoveUpThenShrink:
                    k = byMoving;
                    break;
                case FitMode.ShrinkOnly:
                    k = inPlace > Tiny ? inPlace : byMoving;
                    break;
                default:
                    k = inPlace >= smallest ? inPlace : Math.Min(smallest, byMoving);
                    break;
            }
            k = Math.Max(Tiny, Math.Min(1f, k));

            // Shrink about the anchor, then move the least distance that brings it inside the band.
            float newBottom = anchorAt - k * below;
            float newTop = anchorAt + k * above;
            float move = 0f;
            if (newBottom < low)
            {
                move = low - newBottom;
            }
            else if (newTop > high)
            {
                move = high - newTop;
            }
            newTop += move;

            // The same end state, expressed as a scale about the pivot and a shift.
            scale = k;
            shift = newTop - (pivot + k * (top - pivot));
            return Math.Abs(1f - scale) > Tiny || Math.Abs(shift) > Tiny;
        }
    }
}
