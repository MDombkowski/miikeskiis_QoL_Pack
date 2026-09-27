using UnityEngine;

namespace XPortalMapPicker
{
    /// <summary>
    /// The clean map while picking. The big map draws every pin (the player's, Cartur's Map Pins', pings, other
    /// players, event and location markers) as an icon under Minimap.m_pinRootLarge and its name under
    /// Minimap.m_pinNameRootLarge. Switching those two layers off leaves the explored map, its fog and the player's
    /// own marker, which are drawn elsewhere, and the pins themselves are untouched: they show again the moment the
    /// layers are switched back on. The other players' live icons are pins in that layer too; PlayerLayer draws copies
    /// of them on the clean map when "Show players while picking" is on.
    /// </summary>
    internal static class CleanMap
    {
        private static bool applied;
        private static bool warned;

        // The layers switched off here, so exactly those are switched back on.
        private static GameObject hiddenIcons;
        private static GameObject hiddenNames;

        /// <summary>Whether the pins' icon layer is switched off right now, so the other players' icons are hidden with
        /// it and PlayerLayer draws them instead.</summary>
        internal static bool IconsHidden => hiddenIcons != null;

        /// <summary>Switches the pin layers off; does nothing if they are already off.</summary>
        internal static void Apply(Minimap map)
        {
            if (applied)
            {
                return;
            }
            applied = true;
            hiddenIcons = SwitchOff(map, map.m_pinRootLarge);
            hiddenNames = SwitchOff(map, map.m_pinNameRootLarge);
        }

        /// <summary>Switches back on the layers Apply switched off. Safe to call at any time.</summary>
        internal static void Restore()
        {
            if (hiddenIcons != null)
            {
                hiddenIcons.SetActive(true);
            }
            if (hiddenNames != null)
            {
                hiddenNames.SetActive(true);
            }
            hiddenIcons = null;
            hiddenNames = null;
            applied = false;
        }

        // Switches a layer off and returns it; returns null, and leaves the layer alone, if it is already off or if it
        // also holds what must stay on screen (the map picture itself, or the player's or the ship's marker).
        private static GameObject SwitchOff(Minimap map, RectTransform layer)
        {
            if (layer == null || !layer.gameObject.activeSelf)
            {
                return null;
            }
            if (Holds(layer, map.m_mapImageLarge.transform) || Holds(layer, map.m_largeMarker) || Holds(layer, map.m_largeShipMarker))
            {
                if (!warned)
                {
                    warned = true;
                    Module.Log.LogWarning($"The big map's layer '{layer.name}' also holds the map or your own marker, so it stays on " +
                                          "while picking and some pins stay visible. The game's map may have changed.");
                }
                return null;
            }
            layer.gameObject.SetActive(false);
            return layer.gameObject;
        }

        private static bool Holds(Transform layer, Transform item) => item != null && item.IsChildOf(layer);
    }
}
