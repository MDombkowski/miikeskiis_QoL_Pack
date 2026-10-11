using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace MapViewRules
{
    /// <summary>
    /// How a zoom setting looks in a configuration manager's window (F1): a slider, its number, and a button that fills in
    /// the current zoom of the setting's own map, the large map's (the last one it had, if it is closed) or the minimap's.
    /// Drawn with the window's own immediate-mode GUI, the configuration manager's "custom drawer" (shudnal's
    /// ConfigurationManager and BepInEx's own both honour it). Without a configuration manager nothing here runs, and the
    /// settings stay plain numbers in the file.
    /// </summary>
    internal static class ZoomDrawer
    {
        /// <summary>The drawer of a zoom setting of the large map or the minimap, whose lowest value is 0, or -1 where it
        /// can follow the section above.</summary>
        internal static Action<ConfigEntryBase> For(bool minimap, int lowest) => entry => Draw(entry, minimap, lowest);

        private static void Draw(ConfigEntryBase entry, bool minimap, int lowest)
        {
            int value = entry.BoxedValue is int number ? number : lowest;
            GUILayout.BeginHorizontal();
            try
            {
                float slid = GUILayout.HorizontalSlider(value, lowest, 100f, GUILayout.ExpandWidth(true), GUILayout.MinWidth(60f));
                int rounded = Mathf.RoundToInt(slid);
                if (rounded != value)
                {
                    entry.BoxedValue = rounded;
                    value = rounded;
                }

                string shown = value.ToString(CultureInfo.InvariantCulture);
                string typed = GUILayout.TextField(shown, GUILayout.Width(40f));
                if (typed != shown && int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
                {
                    entry.BoxedValue = Mathf.Clamp(parsed, lowest, 100);
                }

                Minimap map = Minimap.instance;
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && map != null;
                string label = map == null
                    ? "No map yet"
                    : $"Use {(minimap ? "minimap" : "map")} zoom: {ZoomLevel.Whole(minimap ? ZoomLevel.Small(map) : ZoomLevel.Large(map))}";
                if (GUILayout.Button(label, GUILayout.ExpandWidth(false)) && map != null)
                {
                    entry.BoxedValue = ZoomLevel.Whole(minimap ? ZoomLevel.Small(map) : ZoomLevel.Large(map));
                }
                GUI.enabled = enabled;
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
        }
    }
}
