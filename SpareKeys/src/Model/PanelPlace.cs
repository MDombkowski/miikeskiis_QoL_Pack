using System.Collections.Generic;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>
    /// Where a panel goes on the screen (pixels from the top left), beside a window it must not cover: the windows to keep
    /// clear are a configuration manager's (its main window, and its Edit window while open: IMGUI is drawn over Unity UI,
    /// so they would cover the panel) or the Controls menu's "press a key" dialog. Plain arithmetic, so the harness checks
    /// it.
    /// </summary>
    internal static class PanelPlace
    {
        private const float Gap = 8f;

        /// <summary>
        /// The panel's top-left corner. <paramref name="beside"/> is the window it belongs beside (the one waiting for a
        /// key); <paramref name="avoid"/> every window to keep clear (a window covering half the screen or more can't be,
        /// and is let be). With <paramref name="sides"/> it tries the right of that window, its left, then the right and
        /// left of all of them together; then below and above it, below and above them all; then the screen's corners;
        /// the first place on the screen that covers none of them wins. If none does, the bottom right (sides) or the
        /// bottom middle.
        /// </summary>
        internal static Vector2 Choose(Vector2 size, Rect beside, IList<Rect> avoid, bool sides, Vector2 screen)
        {
            var clear = new List<Rect>();
            Rect all = beside;
            foreach (Rect rect in avoid)
            {
                if (rect.width * rect.height < screen.x * screen.y * 0.5f)
                {
                    clear.Add(rect);
                    all = Union(all, rect);
                }
            }
            float w = size.x;
            float h = size.y;
            var tries = new List<Vector2>();
            if (sides)
            {
                tries.Add(new Vector2(beside.xMax + Gap, beside.y));
                tries.Add(new Vector2(beside.xMin - Gap - w, beside.y));
                tries.Add(new Vector2(all.xMax + Gap, beside.y));
                tries.Add(new Vector2(all.xMin - Gap - w, beside.y));
            }
            tries.Add(new Vector2(beside.center.x - w / 2f, beside.yMax + Gap));
            tries.Add(new Vector2(beside.center.x - w / 2f, beside.yMin - Gap - h));
            tries.Add(new Vector2(all.center.x - w / 2f, all.yMax + Gap));
            tries.Add(new Vector2(all.center.x - w / 2f, all.yMin - Gap - h));
            tries.Add(new Vector2(screen.x - w - Gap, screen.y - h - Gap));
            tries.Add(new Vector2(Gap, screen.y - h - Gap));
            tries.Add(new Vector2(screen.x - w - Gap, Gap));
            tries.Add(new Vector2(Gap, Gap));
            foreach (Vector2 at in tries)
            {
                // Up and down it may slide to stay on the screen; sideways it must fit as it is.
                float y = Mathf.Clamp(at.y, 0f, Mathf.Max(0f, screen.y - h));
                if (at.x < 0f || at.x + w > screen.x || y + h > screen.y + 0.5f)
                {
                    continue;
                }
                var panel = new Rect(at.x, y, w, h);
                bool covers = false;
                foreach (Rect rect in clear)
                {
                    covers |= panel.Overlaps(rect);
                }
                if (!covers)
                {
                    return new Vector2(at.x, y);
                }
            }
            float x = sides ? screen.x - w - Gap : (screen.x - w) / 2f;
            return new Vector2(Mathf.Clamp(x, 0f, Mathf.Max(0f, screen.x - w)), Mathf.Clamp(screen.y - h - screen.y * 0.03f, 0f, Mathf.Max(0f, screen.y - h)));
        }

        private static Rect Union(Rect a, Rect b) =>
            Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
    }
}
