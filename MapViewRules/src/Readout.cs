using TMPro;
using UnityEngine;

namespace MapViewRules
{
    /// <summary>
    /// The zoom readout: "Zoom 38" in a corner of the large map and of the minimap while the setting is on, in the same
    /// numbers every zoom setting takes, so the player can find a zoom they like and type its number in. Drawn over the
    /// map in the game's own font and look (copied from the map's biome name), and takes no clicks.
    /// </summary>
    internal static class Readout
    {
        private const string ObjectName = "MapViewRules_ZoomReadout";

        private static Minimap builtOn;
        private static Label large;
        private static Label small;

        /// <summary>Called every frame: shows, moves and updates the readout of the map shown, or hides both.</summary>
        internal static void Tick()
        {
            Minimap map = Minimap.instance;
            if (!Module.IsOn || !Settings.ZoomReadout.Value || map == null)
            {
                Hide();
                return;
            }
            if (builtOn != map || large == null || small == null || !large.Alive || !small.Alive)
            {
                Remove();
                builtOn = map;
                large = new Label(map.m_largeRoot, map.m_biomeNameLarge, 26f, 14f);
                small = new Label(map.m_smallRoot, map.m_biomeNameSmall, 17f, 7f);
            }
            if (map.m_mode == Minimap.MapMode.Large)
            {
                large.Show(map.m_mapImageLarge.rectTransform, Settings.ReadoutCornerLarge.Value, ZoomLevel.Large(map));
                small.Hide();
            }
            else if (map.m_mode == Minimap.MapMode.Small)
            {
                small.Show(map.m_mapImageSmall.rectTransform, Settings.ReadoutCornerMinimap.Value, ZoomLevel.Small(map));
                large.Hide();
            }
            else
            {
                Hide();
            }
        }

        /// <summary>Takes both readouts away for good (until they are wanted again).</summary>
        internal static void Remove()
        {
            large?.Destroy();
            small?.Destroy();
            large = small = null;
            builtOn = null;
        }

        private static void Hide()
        {
            large?.Hide();
            small?.Hide();
        }

        private sealed class Label
        {
            private readonly GameObject gameObject;
            private readonly RectTransform rect;
            private readonly TextMeshProUGUI text;
            private readonly float margin;
            private int shownLevel = int.MinValue;
            private Corner shownCorner = (Corner)(-1);
            private readonly Vector3[] corners = new Vector3[4];

            internal Label(GameObject root, TMP_Text look, float fontSize, float margin)
            {
                this.margin = margin;
                gameObject = new GameObject(ObjectName, typeof(RectTransform));
                gameObject.transform.SetParent(root.transform, false);
                gameObject.transform.SetAsLastSibling();   // over the map and its pins
                rect = (RectTransform)gameObject.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(300f, 60f);
                text = gameObject.AddComponent<TextMeshProUGUI>();
                if (look != null)
                {
                    text.font = look.font;
                    text.fontSharedMaterial = look.fontSharedMaterial;
                    Color color = look.color;
                    color.a = 1f;
                    text.color = color;
                }
                text.fontSize = fontSize;
                text.raycastTarget = false;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                gameObject.SetActive(false);
            }

            internal bool Alive => gameObject != null;

            /// <summary>Puts the readout in a corner of the map picture, inside it by a margin, showing this zoom.</summary>
            internal void Show(RectTransform map, Corner corner, float level)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }
                // The map picture's corners, in the readout's parent's space: whatever the layout, the readout sits on the
                // picture itself.
                map.GetWorldCorners(corners);   // bottom left, top left, top right, bottom right
                bool top = corner == Corner.TopLeft || corner == Corner.TopRight;
                bool left = corner == Corner.TopLeft || corner == Corner.BottomLeft;
                Vector3 world = top ? (left ? corners[1] : corners[2]) : (left ? corners[0] : corners[3]);
                Vector3 local = rect.parent.InverseTransformPoint(world);
                rect.pivot = new Vector2(left ? 0f : 1f, top ? 1f : 0f);
                rect.localPosition = new Vector3(local.x + (left ? margin : -margin), local.y + (top ? -margin : margin), 0f);
                if (corner != shownCorner)
                {
                    shownCorner = corner;
                    text.alignment = top
                        ? (left ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight)
                        : (left ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight);
                }
                int whole = ZoomLevel.Whole(level);
                if (whole != shownLevel)
                {
                    shownLevel = whole;
                    text.text = "Zoom " + whole;
                }
            }

            internal void Hide()
            {
                if (gameObject != null && gameObject.activeSelf)
                {
                    gameObject.SetActive(false);
                }
            }

            internal void Destroy()
            {
                if (gameObject != null)
                {
                    Object.Destroy(gameObject);
                }
            }
        }
    }
}
