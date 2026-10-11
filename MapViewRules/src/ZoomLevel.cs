using UnityEngine;

namespace MapViewRules
{
    /// <summary>
    /// The one zoom number every setting, the readout and the settings window's buttons use: 0 is fully zoomed in and
    /// 100 fully out, and every step of the mouse wheel moves it by the same amount.
    ///
    /// The game zooms by multiplying: each notch of the wheel (or Map Zoom's step, or a press of the comma and period keys)
    /// multiplies the map's zoom value by the same factor, between the map's own m_minZoom and m_maxZoom. So the number is
    /// the zoom's logarithm, scaled to 0–100 between those two. Cartur's Map Pins measures a straight percentage of the
    /// zoom value instead, which puts half the wheel's notches between 0 and 9; this spreads them evenly. The large map and
    /// the minimap share the same limits, each with its own zoom (LargeZoom, SmallZoom).
    /// </summary>
    internal static class ZoomLevel
    {
        /// <summary>The zoom number of a zoom value, between the map's limits: 0 to 100, or 0 if the limits make no sense.</summary>
        internal static float Of(float zoom, float min, float max)
        {
            if (!(min > 0f) || !(max > min) || !(zoom > 0f))
            {
                return 0f;
            }
            float level = 100f * Mathf.Log(zoom / min) / Mathf.Log(max / min);
            return Mathf.Clamp(level, 0f, 100f);
        }

        /// <summary>The large map's zoom number: its current zoom, or the last one it had while it is closed.</summary>
        internal static float Large(Minimap map) => Of(map.LargeZoom, map.m_minZoom, map.m_maxZoom);

        /// <summary>The minimap's zoom number.</summary>
        internal static float Small(Minimap map) => Of(map.SmallZoom, map.m_minZoom, map.m_maxZoom);

        /// <summary>A zoom number as a setting stores it and the readout shows it: a whole number.</summary>
        internal static int Whole(float level) => Mathf.RoundToInt(level);
    }
}
