using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TraderCircles
{
    /// <summary>
    /// The circles' layers: one over the big map's picture, and one over the small map's when that setting is on. Each
    /// is laid first among the picture's children, so it is drawn under the pins, the player's marker and other mods'
    /// drawings (Map Rings' thin rings stay visible on top of the fill), and clipped to the picture's frame. A layer is
    /// made when first needed and goes with the map when the world is left.
    /// </summary>
    internal static class CircleLayer
    {
        private static readonly View large = new View("TraderCircles_LargeMap");
        private static readonly View small = new View("TraderCircles_SmallMap");
        private static readonly List<Piece> pieces = new List<Piece>();

        /// <summary>After each frame: shows the circles on whichever map is open, where the map shows them now, and applies
        /// the settings, so a change made in a configuration manager shows at once.</summary>
        internal static void Refresh()
        {
            Minimap map = Minimap.instance;
            bool any = map != null && Module.IsOn && Traders.AnyCircles;
            large.Refresh(map, any && map.m_mode == Minimap.MapMode.Large ? map.m_mapImageLarge : null);
            small.Refresh(map, any && map.m_mode == Minimap.MapMode.Small && Settings.ShowOnMinimap.Value ? map.m_mapImageSmall : null);
        }

        /// <summary>Destroys both layers. Safe to call at any time.</summary>
        internal static void RemoveAll()
        {
            large.Remove();
            small.Remove();
        }

        private sealed class View
        {
            private readonly string name;
            private GameObject layer;
            private CircleGraphic graphic;
            private RawImage onPicture;

            internal View(string name)
            {
                this.name = name;
            }

            /// <summary>Draws the circles over a map picture, or hides the layer when the picture is null.</summary>
            internal void Refresh(Minimap map, RawImage picture)
            {
                if (picture == null)
                {
                    if (layer != null && layer.activeSelf)
                    {
                        layer.SetActive(false);
                    }
                    return;
                }
                if (layer == null || onPicture != picture)
                {
                    Remove();
                    Create(picture);
                }
                if (!layer.activeSelf)
                {
                    layer.SetActive(true);
                }

                pieces.Clear();
                bool outline = Settings.Outline.Value;
                foreach (Trader trader in Trader.All)
                {
                    if (!trader.ShowsCircles)
                    {
                        continue;
                    }
                    Color32 fill = Settings.FillColour(trader);
                    Color32 edge = Settings.OutlineColour(trader);
                    for (int i = 0; i < trader.Circles.Count; i++)
                    {
                        Circle circle = trader.Circles[i];
                        if (!circle.Holds(trader.Spots[i]))
                        {
                            continue;   // fails the self-check: never drawn (the log's summary counts it)
                        }
                        pieces.Add(new Piece { Circle = circle, Fill = fill, Outline = outline, OutlineColour = edge });
                    }
                }
                graphic.SetPieces(pieces, WorldToPicture(map, picture));
            }

            // Where the map shows the world now, as a scale and a shift from the world's metres (x, z) to the picture's
            // units: the game's own WorldToMapPoint and MapPointToLocalGuiPos, which place its pins, are both straight
            // scalings, so two points settle it.
            private static Vector4 WorldToPicture(Minimap map, RawImage picture)
            {
                const float apart = 1000f;
                GameHooks.WorldToMapPoint(map, Vector3.zero, out float x0, out float y0);
                GameHooks.WorldToMapPoint(map, new Vector3(apart, 0f, apart), out float x1, out float y1);
                Vector2 origin = GameHooks.MapPointToLocalGuiPos(map, x0, y0, picture);
                Vector2 corner = GameHooks.MapPointToLocalGuiPos(map, x1, y1, picture);
                return new Vector4((corner.x - origin.x) / apart, (corner.y - origin.y) / apart, origin.x, origin.y);
            }

            internal void Remove()
            {
                if (layer != null)
                {
                    Object.Destroy(layer);
                }
                layer = null;
                graphic = null;
                onPicture = null;
            }

            private void Create(RawImage picture)
            {
                RectTransform mapImage = picture.rectTransform;
                layer = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
                layer.layer = mapImage.gameObject.layer;
                RectTransform area = (RectTransform)layer.transform;
                area.SetParent(mapImage, worldPositionStays: false);
                Cover(area);
                area.SetAsFirstSibling();

                var drawing = new GameObject(name + "_Circles", typeof(RectTransform), typeof(CanvasRenderer), typeof(CircleGraphic));
                drawing.layer = layer.layer;
                RectTransform box = (RectTransform)drawing.transform;
                box.SetParent(area, worldPositionStays: false);
                Cover(box);
                graphic = drawing.GetComponent<CircleGraphic>();
                graphic.raycastTarget = false;
                graphic.color = Color.white;
                onPicture = picture;
            }

            // Covers the parent exactly, with its origin at the bottom-left corner: the game's MapPointToLocalGuiPos
            // measures from there.
            private static void Cover(RectTransform box)
            {
                box.anchorMin = Vector2.zero;
                box.anchorMax = Vector2.one;
                box.pivot = Vector2.zero;
                box.offsetMin = Vector2.zero;
                box.offsetMax = Vector2.zero;
            }
        }
    }
}
