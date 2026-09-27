using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XPortalMapPicker
{
    /// <summary>
    /// The other players on the clean map while picking. The clean map switches the game's whole pin layer off, and the
    /// other players' live icons with it, so this layer draws a copy of each of those icons, with its name, below the
    /// portal markers. The copies follow the game's own player pins every frame: a copy shows exactly when the game has
    /// an icon for that player (a public position, on the part of the map on screen), and its name when the game would
    /// show the name (zoomed in far enough). The game's pins themselves are never touched. The copies take no clicks,
    /// so picking works exactly as without them.
    /// </summary>
    internal static class PlayerLayer
    {
        private const string LayerName = "XPortalMapPicker_Players";

        private static readonly List<Image> copies = new List<Image>();
        private static readonly List<Text> names = new List<Text>();
        private static GameObject layer;

        /// <summary>Lays the (still empty) layer over the big map. Called before the portal markers' layer is made, so
        /// the markers are drawn over the players and a player standing at a portal never hides it.</summary>
        internal static void Create(Minimap map)
        {
            RectTransform mapImage = map.m_mapImageLarge.rectTransform;
            layer = new GameObject(LayerName, typeof(RectTransform));
            layer.layer = mapImage.gameObject.layer;

            // Covers the map picture exactly, like the portal markers' layer, so a copy anchored at the bottom-left
            // corner is placed with the game's MapPointToLocalGuiPos.
            RectTransform area = (RectTransform)layer.transform;
            area.SetParent(mapImage, worldPositionStays: false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;
            area.SetAsLastSibling();
            layer.SetActive(false);
        }

        /// <summary>Runs every frame while picking, after the map has moved: shows the players when the clean map has
        /// hidden the game's pins and the setting is on, and puts each copy where the game's player pin is.</summary>
        internal static void Refresh(Minimap map)
        {
            if (layer == null)
            {
                return;
            }
            List<Minimap.PinData> pins = Settings.ShowPlayersWhilePicking.Value && CleanMap.IconsHidden ? GameHooks.PlayerPins(map) : null;
            SetActive(layer, pins != null);
            if (pins == null)
            {
                return;
            }

            RawImage mapImage = map.m_mapImageLarge;
            Rect onScreen = mapImage.uvRect;
            bool namesShown = map.LargeZoom < map.m_showNamesZoom;   // the game's rule for pin names on the big map
            while (copies.Count < pins.Count)
            {
                CreateCopy(map);
            }
            for (int i = 0; i < copies.Count; i++)
            {
                Minimap.PinData pin = i < pins.Count ? pins[i] : null;
                bool shown = false;
                float mapX = 0f;
                float mapY = 0f;
                if (pin != null && pin.m_uiElement != null)
                {
                    GameHooks.WorldToMapPoint(map, pin.m_pos, out mapX, out mapY);
                    shown = mapX > onScreen.xMin && mapX < onScreen.xMax && mapY > onScreen.yMin && mapY < onScreen.yMax;
                }
                Image copy = copies[i];
                SetActive(copy.gameObject, shown);
                if (!shown)
                {
                    continue;
                }

                if (copy.sprite != pin.m_icon)
                {
                    copy.sprite = pin.m_icon;
                }
                copy.color = pin.m_iconElement != null ? pin.m_iconElement.color : Color.white;
                copy.rectTransform.anchoredPosition = GameHooks.MapPointToLocalGuiPos(map, mapX, mapY, mapImage);

                Text name = names[i];
                bool nameShown = namesShown && !string.IsNullOrEmpty(pin.m_name);
                SetActive(name.gameObject, nameShown);
                if (nameShown && name.text != pin.m_name)
                {
                    name.text = pin.m_name;
                    name.rectTransform.sizeDelta = new Vector2(name.preferredWidth, name.preferredHeight);
                }
            }
        }

        /// <summary>Destroys the layer and every copy on it. Safe to call at any time.</summary>
        internal static void Remove()
        {
            if (layer != null)
            {
                UnityEngine.Object.Destroy(layer);
            }
            layer = null;
            copies.Clear();
            names.Clear();
        }

        // One player's icon, at the game's size for pins on the big map, with a name below it in the portal names'
        // style. Neither takes clicks: a right click on a player still reaches the map and cancels picking.
        private static void CreateCopy(Minimap map)
        {
            var icon = new GameObject("Player", typeof(RectTransform), typeof(Image));
            icon.layer = layer.layer;
            RectTransform box = (RectTransform)icon.transform;
            box.SetParent(layer.transform, worldPositionStays: false);
            box.anchorMin = Vector2.zero;
            box.anchorMax = Vector2.zero;
            box.pivot = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(map.m_pinSizeLarge, map.m_pinSizeLarge);

            Image picture = icon.GetComponent<Image>();
            picture.preserveAspect = true;
            picture.raycastTarget = false;

            Text name = MarkerLayer.CreateLabel(box, string.Empty);
            name.raycastTarget = false;
            name.gameObject.SetActive(false);

            icon.SetActive(false);
            copies.Add(picture);
            names.Add(name);
        }

        private static void SetActive(GameObject gameObject, bool active)
        {
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }
    }
}
