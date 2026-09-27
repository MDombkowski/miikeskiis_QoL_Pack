using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TraderCircles
{
    /// <summary>One circle as drawn: its cut shape and its colours.</summary>
    internal struct Piece
    {
        internal Circle Circle;
        internal Color32 Fill;
        internal bool Outline;
        internal Color32 OutlineColour;

        internal bool SameAs(Piece other) =>
            ReferenceEquals(Circle, other.Circle) && Outline == other.Outline && Same(Fill, other.Fill) && Same(OutlineColour, other.OutlineColour);

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    /// <summary>
    /// Draws the circles, each cut to its trader's ground, filled and see-through, each optionally with a thin outline
    /// around the drawn part, as one mesh over the map picture. It is the mod's own drawing, not the game's map pins, so
    /// no other mod resizes or recolours it and nothing of it can be saved or shared. It takes no clicks. Its rectangle
    /// covers the map picture, measured from the bottom-left corner.
    /// </summary>
    internal sealed class CircleGraphic : MaskableGraphic
    {
        /// <summary>How thick an outline is, in interface units.</summary>
        private const float OutlineWidth = 2f;

        /// <summary>Unity allows 65,000 corners in one mesh; this many are shared out between the circles on the picture.</summary>
        private const int CornerBudget = 60000;

        private readonly List<Piece> pieces = new List<Piece>();

        // From the world's metres (x, z) to the picture's units: x * view.x + view.z, z * view.y + view.w.
        private Vector4 view;

        /// <summary>Takes the circles to draw and where the map shows the world now; the mesh is rebuilt only when either
        /// differs from the last time.</summary>
        internal void SetPieces(List<Piece> next, Vector4 nextView)
        {
            bool same = next.Count == pieces.Count && nextView == view;
            for (int i = 0; same && i < next.Count; i++)
            {
                same = next[i].SameAs(pieces[i]);
            }
            if (same)
            {
                return;
            }
            pieces.Clear();
            pieces.AddRange(next);
            view = nextView;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect area = rectTransform.rect;
            int onPicture = 0;
            foreach (Piece piece in pieces)
            {
                if (OnPicture(piece.Circle, area))
                {
                    onPicture++;
                }
            }
            if (onPicture == 0)
            {
                return;
            }
            int budget = CornerBudget / onPicture;
            foreach (Piece piece in pieces)
            {
                if (OnPicture(piece.Circle, area))
                {
                    Add(mesh, piece, budget);
                }
            }
        }

        private bool OnPicture(Circle circle, Rect area)
        {
            Vector2 centre = ToPicture(circle.Centre);
            float reachX = Mathf.Abs(circle.Radius * view.x);
            float reachY = Mathf.Abs(circle.Radius * view.y);
            return centre.x + reachX >= area.xMin && centre.x - reachX <= area.xMax && centre.y + reachY >= area.yMin && centre.y - reachY <= area.yMax;
        }

        // One circle: a strip of four-cornered pieces between its lines, and the outline along the drawn part's edges.
        private void Add(VertexHelper mesh, Piece piece, int budget)
        {
            Circle circle = piece.Circle;
            int lines = circle.Angles.Length;
            int perLine = 4 + (piece.Outline ? 8 : 0);
            int stride = Mathf.Max(1, Mathf.CeilToInt((float)(lines * perLine + 16) / budget));
            var kept = new List<int>(lines / stride + 2);
            for (int i = 0; i < lines; i += stride)
            {
                kept.Add(i);
            }
            if (kept[kept.Count - 1] != lines - 1)
            {
                kept.Add(lines - 1);
            }
            int pairs = circle.FullTurn ? kept.Count : kept.Count - 1;

            for (int p = 0; p < pairs; p++)
            {
                int a = kept[p];
                int b = kept[(p + 1) % kept.Count];
                if (!Drawn(circle, a) || !Drawn(circle, b))
                {
                    continue;
                }
                AddQuad(mesh, At(circle, a, circle.Lo[a]), At(circle, a, circle.Hi[a]), At(circle, b, circle.Hi[b]), At(circle, b, circle.Lo[b]), piece.Fill);
                if (piece.Outline)
                {
                    AddLine(mesh, At(circle, a, circle.Lo[a]), At(circle, b, circle.Lo[b]), piece.OutlineColour);
                    AddLine(mesh, At(circle, a, circle.Hi[a]), At(circle, b, circle.Hi[b]), piece.OutlineColour);
                }
            }
            if (!piece.Outline)
            {
                return;
            }
            // Across the drawn part's ends: where it starts or stops along the circle.
            for (int k = 0; k < kept.Count; k++)
            {
                int i = kept[k];
                if (!Drawn(circle, i))
                {
                    continue;
                }
                bool hasBefore = circle.FullTurn || k > 0;
                bool hasAfter = circle.FullTurn || k < kept.Count - 1;
                bool startsHere = !hasBefore || !Drawn(circle, kept[(k - 1 + kept.Count) % kept.Count]);
                bool endsHere = !hasAfter || !Drawn(circle, kept[(k + 1) % kept.Count]);
                if (startsHere || endsHere)
                {
                    AddLine(mesh, At(circle, i, circle.Lo[i]), At(circle, i, circle.Hi[i]), piece.OutlineColour);
                }
            }
        }

        private static bool Drawn(Circle circle, int line) => circle.Lo[line] <= circle.Hi[line];

        private Vector2 At(Circle circle, int line, float distance) =>
            ToPicture(new Vector2(distance * Mathf.Cos(circle.Angles[line]), distance * Mathf.Sin(circle.Angles[line])));

        private Vector2 ToPicture(Vector2 world) => new Vector2(world.x * view.x + view.z, world.y * view.y + view.w);

        private static void AddQuad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 colour)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(a, colour, Vector2.zero);
            mesh.AddVert(b, colour, Vector2.zero);
            mesh.AddVert(c, colour, Vector2.zero);
            mesh.AddVert(d, colour, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }

        private static void AddLine(VertexHelper mesh, Vector2 from, Vector2 to, Color32 colour)
        {
            Vector2 along = to - from;
            if (along.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Vector2 side = new Vector2(-along.y, along.x).normalized * (OutlineWidth / 2f);
            AddQuad(mesh, from - side, from + side, to + side, to - side, colour);
        }
    }
}
