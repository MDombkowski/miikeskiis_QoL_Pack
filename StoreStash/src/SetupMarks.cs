using UnityEngine;

namespace StoreStash
{
    /// <summary>The setup window's marks on a thumbnail (decision 25): a check (taken, or "click to take back"), a cross
    /// ("click to take out") and a minus (left out).</summary>
    internal enum MarkShape
    {
        None,
        Check,
        Cross,
        Minus,
    }

    /// <summary>
    /// The marks' pictures, drawn in code, since the game's fonts lack ✓ and ✗ (as the pack's padlock and Item Stand
    /// Icons' arrows are drawn): white strokes with a dark rim, so an Image's colour tints them (green, red) and the rim
    /// keeps them readable over any icon. Each is drawn the first time it's asked for, at twice the size it shows, and
    /// kept.
    /// </summary>
    internal static class SetupMarks
    {
        /// <summary>The pictures' width and height in pixels (a mark shows at 20).</summary>
        internal const int Size = 40;

        /// <summary>A stroke's half width, and the dark rim's width, in pixels.</summary>
        internal const float HalfWidth = 3.6f;

        internal const float Rim = 2.6f;

        private static readonly Sprite[] Made = new Sprite[4];

        /// <summary>The picture of a mark (null for None); drawn again if Unity has destroyed it.</summary>
        internal static Sprite Picture(MarkShape shape)
        {
            if (shape == MarkShape.None)
            {
                return null;
            }
            Sprite sprite = Made[(int)shape];
            return sprite != null ? sprite : (Made[(int)shape] = Make(shape));
        }

        /// <summary>
        /// Which mark a thumbnail shows, as the setup takes its thing (<paramref name="taken"/>: ItemPicks.Fit above 0;
        /// <paramref name="leftOut"/>: left out by name) and while the pointer is on the mark (<paramref name="hot"/>):
        /// taken, a check that turns into a cross (a click takes it out); left out by name, a minus that turns into a check
        /// (a click takes it back); else none.
        /// </summary>
        internal static MarkShape Of(bool taken, bool leftOut, bool hot) =>
            taken ? (hot ? MarkShape.Cross : MarkShape.Check) : leftOut ? (hot ? MarkShape.Check : MarkShape.Minus) : MarkShape.None;

        private static Sprite Make(MarkShape shape)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "StoreStash_SetupMark_" + shape,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,   // never unloaded with a scene, never saved
            };
            texture.SetPixels(Pixels(shape));
            texture.Apply(true, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            made.name = texture.name;
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }

        /// <summary>The pixels of a mark, row by row from the bottom as Texture2D.SetPixels takes them: white on its
        /// strokes, a dark rim around them, clear elsewhere, each edge smoothed over a pixel. Plain arithmetic on distances,
        /// so the scratch harness checks it without the engine.</summary>
        internal static Color[] Pixels(MarkShape shape)
        {
            var dark = new Color(0.08f, 0.06f, 0.05f, 1f);
            var pixels = new Color[Size * Size];
            Vector2[] strokes = Strokes(shape);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var at = new Vector2(x + 0.5f, y + 0.5f);
                    float shapeDistance = float.MaxValue;
                    for (int i = 0; i + 1 < strokes.Length; i += 2)
                    {
                        shapeDistance = Mathf.Min(shapeDistance, Segment(at, strokes[i], strokes[i + 1]) - HalfWidth);
                    }
                    float fill = Coverage(shapeDistance);
                    float edge = Coverage(shapeDistance - Rim);
                    Color color = Color.Lerp(dark, Color.white, fill);
                    color.a = Mathf.Max(fill, edge);
                    pixels[y * Size + x] = color;
                }
            }
            return pixels;
        }

        // Each mark's strokes, two points each, in pixels from the bottom left: the check's short and long arms, the
        // cross's two diagonals, the minus's bar. Kept inside the picture with the rim and a pixel's smoothing to spare.
        private static Vector2[] Strokes(MarkShape shape)
        {
            switch (shape)
            {
                case MarkShape.Check:
                    return new[] { new Vector2(9f, 21f), new Vector2(16.5f, 13f), new Vector2(16.5f, 13f), new Vector2(31f, 29f) };
                case MarkShape.Cross:
                    return new[] { new Vector2(11f, 11f), new Vector2(29f, 29f), new Vector2(11f, 29f), new Vector2(29f, 11f) };
                case MarkShape.Minus:
                    return new[] { new Vector2(10f, 20f), new Vector2(30f, 20f) };
                default:
                    return new Vector2[0];
            }
        }

        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        // The distance from a point to the segment a-b.
        private static float Segment(Vector2 at, Vector2 a, Vector2 b)
        {
            Vector2 along = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(at - a, along) / along.sqrMagnitude);
            return (at - (a + along * t)).magnitude;
        }
    }
}
