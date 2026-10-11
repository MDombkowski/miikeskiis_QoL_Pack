using System.Collections.Generic;
using Hoarstone.Shapes;
using UnityEngine;
using UnityEngine.UI;

namespace Hoarstone
{
    /// <summary>
    /// The outlines on the big map and the small map in the corner, as Trader Circles draws its circles: a layer laid
    /// first among the map picture's children (so under the pins and the player's marker), clipped to the picture's frame,
    /// holding one drawing of thin lines that takes no clicks. A layer is made when first needed and goes with the map
    /// when the world is left.
    /// </summary>
    internal static class MapOutline
    {
        private static readonly View large = new View("Hoarstone_LargeMap");
        private static readonly View small = new View("Hoarstone_SmallMap");

        internal static void Show(IReadOnlyList<OutlineLine> lines, bool wanted)
        {
            Minimap map = Minimap.instance;
            bool any = wanted && lines.Count > 0 && map != null && GameHooks.MapReady;
            large.Refresh(map, any && map.m_mode == Minimap.MapMode.Large ? map.m_mapImageLarge : null, lines);
            small.Refresh(map, any && map.m_mode == Minimap.MapMode.Small ? map.m_mapImageSmall : null, lines);
        }

        internal static void Clear()
        {
            large.Remove();
            small.Remove();
        }

        private sealed class View
        {
            private readonly string name;
            private GameObject layer;
            private OutlineGraphic graphic;
            private RawImage onPicture;

            internal View(string name)
            {
                this.name = name;
            }

            internal void Refresh(Minimap map, RawImage picture, IReadOnlyList<OutlineLine> lines)
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
                graphic.SetLines(lines, WorldToPicture(map, picture), Settings.KeptColour.Value, Settings.OthersColour.Value);
            }

            // Where the map shows the world now, as a scale and a shift from the world's metres (x, z) to the picture's
            // units: the game's own WorldToMapPoint and MapPointToLocalGuiPos, which place its pins, are both straight
            // scalings, so two points settle it (Trader Circles' way).
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

                var drawing = new GameObject(name + "_Lines", typeof(RectTransform), typeof(CanvasRenderer), typeof(OutlineGraphic));
                drawing.layer = layer.layer;
                RectTransform box = (RectTransform)drawing.transform;
                box.SetParent(area, worldPositionStays: false);
                Cover(box);
                graphic = drawing.GetComponent<OutlineGraphic>();
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

    /// <summary>The outlines as one mesh of thin lines over a map picture, rebuilt only when the lines, the view or the
    /// colours change. Its rectangle covers the picture, measured from its bottom-left corner.</summary>
    internal sealed class OutlineGraphic : MaskableGraphic
    {
        private const float Width = 2f;
        private const int MostSegments = 15000;   // four corners each, under Unity's 65,000 a mesh

        private IReadOnlyList<OutlineLine> lines;
        private Vector4 view;
        private Color32 kept, other;

        internal void SetLines(IReadOnlyList<OutlineLine> next, Vector4 nextView, Color keptColour, Color otherColour)
        {
            Color32 k = keptColour, o = otherColour;
            if (ReferenceEquals(next, lines) && nextView == view && Same(k, kept) && Same(o, other))
            {
                return;
            }
            lines = next;
            view = nextView;
            kept = k;
            other = o;
            SetVerticesDirty();
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (lines == null)
            {
                return;
            }
            Rect area = rectTransform.rect;
            int segments = 0;
            foreach (OutlineLine line in lines)
            {
                Color32 colour = line.Kept ? kept : other;
                for (int k = 1; k < line.Points.Count && segments < MostSegments; k++)
                {
                    Vector2 a = ToPicture(line.Points[k - 1]), b = ToPicture(line.Points[k]);
                    if (Mathf.Max(a.x, b.x) < area.xMin || Mathf.Min(a.x, b.x) > area.xMax || Mathf.Max(a.y, b.y) < area.yMin || Mathf.Min(a.y, b.y) > area.yMax)
                    {
                        continue;
                    }
                    AddLine(mesh, a, b, colour);
                    segments++;
                }
            }
        }

        private Vector2 ToPicture(Flat world) => new Vector2((float)world.X * view.x + view.z, (float)world.Z * view.y + view.w);

        private static void AddLine(VertexHelper mesh, Vector2 from, Vector2 to, Color32 colour)
        {
            Vector2 along = to - from;
            if (along.sqrMagnitude < 0.0001f)
            {
                return;
            }
            // Each end reaches half a width past its point, so the segments of a line meet without gaps at its corners.
            Vector2 unit = along.normalized * (Width / 2f);
            Vector2 side = new Vector2(-unit.y, unit.x);
            from -= unit;
            to += unit;
            int first = mesh.currentVertCount;
            mesh.AddVert(from - side, colour, Vector2.zero);
            mesh.AddVert(from + side, colour, Vector2.zero);
            mesh.AddVert(to + side, colour, Vector2.zero);
            mesh.AddVert(to - side, colour, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
