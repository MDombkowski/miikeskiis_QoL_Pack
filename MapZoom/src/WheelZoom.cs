using UnityEngine;

namespace MapZoom
{
    /// <summary>
    /// The mouse wheel's zoom on the big map, done by this mod in place of the game's.
    ///
    /// The game (Minimap.UpdateMap) clamps the wheel to ±0.05 a frame and zooms by that times LargeZoom × 2: at most
    /// 10% a notch, ×0.9 in and ×1.1 out, around the middle of the view. Its own zoom-toward-the-pointer branch is
    /// switched off in its code (`if (false &amp;&amp; …)`). Here, just before UpdateMap runs, the mod reads the wheel the
    /// same way, zooms by its own step, and hides the wheel from the game for the rest of that call, so the game's
    /// keys, gamepad, dragging and redrawing all carry on as usual.
    /// </summary>
    internal static class WheelZoom
    {
        // The game's clamp: a wheel value this large or larger in one frame counts as one whole notch.
        private const float NotchValue = 0.05f;

        /// <summary>True from the moment the mod has zoomed for this frame's wheel until UpdateMap ends: the game's own
        /// read of the wheel inside UpdateMap then gets 0.</summary>
        internal static bool Swallowing { get; private set; }

        // The big map's view as the game drew it at the end of the last UpdateMap: the frame, the offset and the zoom.
        // Zooming toward the pointer is right only while the view on screen is that one. On the frame the map opens,
        // or something recentres it (the game's SetMapMode or ShowPointOnMap, XPortal Map Picker's zoom-to-fit), the
        // screen still shows an older view, so that notch zooms around the middle instead.
        private static int drawnFrame = -1;
        private static Vector3 drawnOffset;
        private static float drawnZoom;

        internal static void BeforeUpdateMap(Minimap map, Player player, bool takeInput)
        {
            Swallowing = false;
            // The same conditions the game zooms the big map with the wheel under; anything else is left to the game.
            if (!takeInput || map.m_mode != Minimap.MapMode.Large || ZInput.IsGamepadActive() || Minimap.InTextInput() || player == null)
            {
                return;
            }
            float min = map.m_minZoom;
            float max = map.m_maxZoom;
            if (!(min > 0f) || !(max > min))
            {
                return;
            }
            // Read through ZInput, so other mods that silence the wheel over their own windows (Cartur's Map Pins, the
            // configuration manager) silence it here too.
            float wheel = ZInput.GetMouseScrollWheel();
            if (wheel == 0f)
            {
                return;
            }
            Swallowing = true;

            // Up to one notch a frame, as the game counts them; a smooth-scrolling wheel or touchpad gives parts of one.
            // Positive is the wheel turned away from you, which zooms in.
            float notches = Mathf.Clamp(wheel, -NotchValue, NotchValue) / NotchValue;
            int endToEnd = Mathf.Max(Settings.FewestNotches, Settings.NotchesEndToEnd.Value);
            float step = Mathf.Pow(max / min, 1f / endToEnd);     // the same step in and out, from the map's own limits

            float before = map.LargeZoom;
            bool viewIsCurrent = GameHooks.CanZoomTowardPointer && drawnFrame == Time.frameCount - 1
                                 && drawnZoom == before && drawnOffset == GameHooks.GetMapOffset(map);
            map.LargeZoom = before * Mathf.Pow(step, -notches);    // the setter keeps it within the limits
            float ratio = map.LargeZoom / before;
            if (Mathf.Approximately(ratio, 1f) || !Settings.ZoomTowardPointer.Value || !viewIsCurrent)
            {
                return;
            }

            // Keep the place under the pointer under it. The map still shows last frame's view here (the game redraws it
            // later in UpdateMap), so the pointer's world point is read before the view changes. The view's middle is
            // the player's position plus the map offset; scaling its distance from that point by the zoom's ratio keeps
            // the point in place. Worked out on the world, not the screen, so it holds wherever the map sits on screen.
            Vector3 pointer = ZInput.pointerPosition;
            if (pointer.x < 0f || pointer.y < 0f || pointer.x > Screen.width || pointer.y > Screen.height)
            {
                return;
            }
            Vector3 under = GameHooks.ScreenToWorldPoint(map, pointer);
            if (under == Vector3.zero)
            {
                return;                                             // the game couldn't place the pointer on the map
            }
            Vector3 origin = player.transform.position;
            Vector3 offset = GameHooks.GetMapOffset(map);
            Vector3 middle = origin + offset;
            Vector3 newMiddle = under + (middle - under) * ratio;
            GameHooks.SetMapOffset(map, new Vector3(newMiddle.x - origin.x, offset.y, newMiddle.z - origin.z));
        }

        internal static void AfterUpdateMap(Minimap map)
        {
            Swallowing = false;
            if (map != null && map.m_mode == Minimap.MapMode.Large && GameHooks.CanZoomTowardPointer)
            {
                drawnFrame = Time.frameCount;
                drawnOffset = GameHooks.GetMapOffset(map);
                drawnZoom = map.LargeZoom;
            }
            else
            {
                drawnFrame = -1;
            }
        }
    }
}
