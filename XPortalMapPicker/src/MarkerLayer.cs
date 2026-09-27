using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace XPortalMapPicker
{
    /// <summary>
    /// The mod's own layer of portal markers over the big map while picking. It is not made of the game's pins, so
    /// nothing of it can be saved to the map or shared, and no other mod treats it as a pin. It exists only while
    /// picking and is destroyed as a whole when picking ends.
    /// </summary>
    internal static class MarkerLayer
    {
        private const string LayerName = "XPortalMapPicker_PortalMarkers";

        private const int LabelFontSize = 16;

        // Space between a marker's picture and its name.
        private const float LabelGap = 2f;

        private static readonly List<PortalMarker> markers = new List<PortalMarker>();
        private static GameObject layer;
        private static int markerSizeApplied;

        /// <summary>Lays one marker per portal over the big map, above everything else drawn on the map picture.</summary>
        internal static void Create(Minimap map, List<XPortalHooks.Portal> portals)
        {
            RectTransform mapImage = map.m_mapImageLarge.rectTransform;
            layer = new GameObject(LayerName, typeof(RectTransform));
            layer.layer = mapImage.gameObject.layer;

            // The layer covers the map picture exactly, the way Jotunn's own map overlays do. A marker anchored at the
            // layer's bottom-left corner can then be placed with the game's MapPointToLocalGuiPos, which measures from
            // the map picture's bottom-left corner.
            RectTransform area = (RectTransform)layer.transform;
            area.SetParent(mapImage, worldPositionStays: false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;
            area.SetAsLastSibling();

            Sprite portalIcon = PortalIcon(map);
            foreach (XPortalHooks.Portal portal in portals)
            {
                markers.Add(CreateMarker(map, area, portal, portalIcon));
            }
            markerSizeApplied = 0;
        }

        /// <summary>Runs every frame while picking, after the map has moved: puts each marker on its portal and
        /// applies the settings, so a change made in a configuration manager shows at once.</summary>
        internal static void Refresh(Minimap map)
        {
            if (layer == null)
            {
                return;
            }
            ApplyMarkerSize(map);

            RawImage mapImage = map.m_mapImageLarge;
            Rect onScreen = mapImage.uvRect;   // the part of the map texture the big map shows
            bool showUnexplored = Settings.ShowPortalsInUnexploredAreas.Value;
            bool namesAlways = Settings.PortalNames.Value == PortalNameMode.Always;
            foreach (PortalMarker marker in markers)
            {
                // The game's own rule for drawing a pin: its point lies inside the part of the map on screen.
                bool shown = marker.MapX > onScreen.xMin && marker.MapX < onScreen.xMax
                          && marker.MapY > onScreen.yMin && marker.MapY < onScreen.yMax
                          && (showUnexplored || GameHooks.IsExplored(map, marker.Location));
                SetActive(marker.gameObject, shown);
                if (shown)
                {
                    ((RectTransform)marker.transform).anchoredPosition = GameHooks.MapPointToLocalGuiPos(map, marker.MapX, marker.MapY, mapImage);
                    SetActive(marker.Label.gameObject, namesAlways || marker.PointerOver);
                }
            }
        }

        /// <summary>Destroys the layer and every marker on it. Safe to call at any time.</summary>
        internal static void Remove()
        {
            if (layer != null)
            {
                UnityEngine.Object.Destroy(layer);
            }
            layer = null;
            markers.Clear();
        }

        private static PortalMarker CreateMarker(Minimap map, RectTransform area, XPortalHooks.Portal portal, Sprite portalIcon)
        {
            string name = string.IsNullOrEmpty(portal.Name) ? Localization.instance.Localize("$piece_portal_tag_none") : portal.Name;
            var marker = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(PortalMarker));
            marker.layer = area.gameObject.layer;
            RectTransform box = (RectTransform)marker.transform;
            box.SetParent(area, worldPositionStays: false);
            box.anchorMin = Vector2.zero;
            box.anchorMax = Vector2.zero;
            box.pivot = new Vector2(0.5f, 0.5f);

            Image picture = marker.GetComponent<Image>();
            picture.sprite = portalIcon;
            picture.preserveAspect = true;

            // The picture turns Valheim orange under the mouse.
            Button button = marker.GetComponent<Button>();
            button.targetGraphic = picture;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colours = ColorBlock.defaultColorBlock;
            colours.highlightedColor = GUIManager.Instance.ValheimOrange;
            colours.pressedColor = GUIManager.Instance.ValheimOrange;
            colours.selectedColor = Color.white;
            button.colors = colours;
            ZDOID portalId = portal.Id;
            button.onClick.AddListener(() => PickMode.OnMarkerClicked(portalId));

            GameHooks.WorldToMapPoint(map, portal.Location, out float mapX, out float mapY);
            PortalMarker component = marker.GetComponent<PortalMarker>();
            component.Setup(portal.Id, portal.Location, mapX, mapY, CreateLabel(box, name));
            return component;
        }

        /// <summary>A name centred a little below a marker's picture like the game's own pin names, in Jotunn's Valheim
        /// style (Averia Serif Bold, white with a black outline). The players' names use it too (PlayerLayer).</summary>
        internal static Text CreateLabel(RectTransform marker, string name)
        {
            var labelObject = new GameObject("Name", typeof(RectTransform), typeof(Text));
            labelObject.layer = marker.gameObject.layer;
            RectTransform box = (RectTransform)labelObject.transform;
            box.SetParent(marker, worldPositionStays: false);
            box.anchorMin = new Vector2(0.5f, 0f);
            box.anchorMax = new Vector2(0.5f, 0f);
            box.pivot = new Vector2(0.5f, 1f);
            box.anchoredPosition = new Vector2(0f, -LabelGap);

            Text label = labelObject.GetComponent<Text>();
            GUIManager.Instance.ApplyTextStyle(label, LabelFontSize);
            label.alignment = TextAnchor.UpperCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = name;

            // Exactly as big as the text: clicking the name chooses the portal too, and no invisible box around it
            // catches clicks meant for the map.
            box.sizeDelta = new Vector2(label.preferredWidth, label.preferredHeight);
            return label;
        }

        private static void ApplyMarkerSize(Minimap map)
        {
            int percent = Settings.MarkerSize.Value;
            if (percent == markerSizeApplied)
            {
                return;
            }
            markerSizeApplied = percent;
            float size = map.m_pinSizeLarge * percent / 100f;
            foreach (PortalMarker marker in markers)
            {
                ((RectTransform)marker.transform).sizeDelta = new Vector2(size, size);
            }
        }

        // The picture of Icon4, the fifth of the pins a player can place, which the game draws as a portal.
        private static Sprite PortalIcon(Minimap map) => map.m_icons.Find(icon => icon.m_name == Minimap.PinType.Icon4).m_icon;

        private static void SetActive(GameObject gameObject, bool active)
        {
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }
    }
}
