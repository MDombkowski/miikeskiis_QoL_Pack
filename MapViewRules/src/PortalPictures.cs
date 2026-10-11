using UnityEngine;

namespace MapViewRules
{
    /// <summary>
    /// How a portal's pin is known: a pin of type None (the type the game gives the pins it adds itself, with a picture
    /// of their own) drawn with the game's own white portal pin, the picture of Icon4, the fifth of the pins a player can
    /// place. XPortal Map Picker's portal pins (A40) are made so, and a module never uses another's code (MODULES.md), so
    /// this is how the two meet. A portal pin a player placed by hand is of type Icon4 itself, and stays a hand-placed
    /// pin. The picture is read from the map, whose pictures are the game's own and the same in every world.
    /// </summary>
    internal static class PortalPictures
    {
        private static Sprite portalPin;

        /// <summary>Whether a pin is a portal's: type None, drawn with the game's portal pin.</summary>
        internal static bool Is(Minimap.PinData pin)
        {
            if (pin.m_type != Minimap.PinType.None || pin.m_icon == null)
            {
                return false;
            }
            if (portalPin == null && !Find())
            {
                return false;
            }
            return pin.m_icon == portalPin;
        }

        // The game's portal pin picture, from the map's own list; false while there is no map.
        private static bool Find()
        {
            Minimap map = Minimap.instance;
            if (map == null || map.m_icons == null)
            {
                return false;
            }
            foreach (Minimap.SpriteData icon in map.m_icons)
            {
                if (icon.m_name == Minimap.PinType.Icon4 && icon.m_icon != null)
                {
                    portalPin = icon.m_icon;
                    Sorter.Invalidate();   // a pin sorted before now wasn't known as a portal's
                    Drawing.RequestRedraw();
                    return true;
                }
            }
            return false;
        }
    }
}
