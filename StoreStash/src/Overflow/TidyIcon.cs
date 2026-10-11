using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// The tidy button's picture (card A42): two arrows, one over the other, pointing each way, for things moving between
    /// chests both ways. Drawn in code, once, since the game has no such icon: white with a dark rim, so the button's colour
    /// tints it and the rim keeps it readable, as the setup button's tag is drawn (SetupIcon).
    /// </summary>
    internal static class TidyIcon
    {
        /// <summary>The picture's width and height in pixels.</summary>
        internal const int Size = 32;

        private static Sprite sprite;

        /// <summary>Drawn the first time it's asked for, and again if Unity has destroyed it.</summary>
        internal static Sprite Sprite => sprite != null ? sprite : (sprite = Make());

        private static Sprite Make()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = "StoreStash_TidyIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,   // never unloaded with a scene, never saved
            };
            texture.SetPixels(Pixels());
            texture.Apply(true, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            made.name = texture.name;
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }

        /// <summary>The pixels, row by row from the bottom as Texture2D.SetPixels takes them: white where the arrows are, a
        /// dark rim around them, clear elsewhere, each edge smoothed over a pixel. Plain arithmetic on distances, so the
        /// scratch harness can check it without the engine.</summary>
        internal static Color[] Pixels()
        {
            const float rim = 1.3f;
            var dark = new Color(0.08f, 0.06f, 0.05f, 1f);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var at = new Vector2(x + 0.5f, y + 0.5f);
                    float shape = Distance(at);
                    float fill = Coverage(shape);
                    float edge = Coverage(shape - rim);
                    Color color = Color.Lerp(dark, Color.white, fill);
                    color.a = Mathf.Max(fill, edge);
                    pixels[y * Size + x] = color;
                }
            }
            return pixels;
        }

        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        // The signed distance to the two arrows: the upper pointing right, the lower pointing left.
        private static float Distance(Vector2 at)
        {
            float upper = Arrow(at, 21f, pointsRight: true);
            float lower = Arrow(at, 11f, pointsRight: false);
            return Mathf.Min(upper, lower);
        }

        // An arrow across the picture at height y: a shaft and a head at one end.
        private static float Arrow(Vector2 at, float y, bool pointsRight)
        {
            // Mirrored for the left-pointing one, so one shape serves both.
            Vector2 p = pointsRight ? at : new Vector2(Size - at.x, at.y);
            float shaft = Box(p, new Vector2(13f, y), new Vector2(8f, 1.6f));
            // The head: a triangle whose tip is at x 28, its back at x 20, 6 above and below the shaft's middle.
            float upperEdge = Edge(p, new Vector2(20f, y + 6f), new Vector2(28f, y));
            float lowerEdge = Edge(p, new Vector2(28f, y), new Vector2(20f, y - 6f));
            float back = 20f - p.x;
            float head = Mathf.Max(Mathf.Max(upperEdge, lowerEdge), back);
            return Mathf.Min(shaft, head);
        }

        // Signed distance to a box, by its centre and half size.
        private static float Box(Vector2 at, Vector2 centre, Vector2 half)
        {
            var d = new Vector2(Mathf.Abs(at.x - centre.x) - half.x, Mathf.Abs(at.y - centre.y) - half.y);
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        // Signed distance to the line through a and b, negative on its right (walking from a to b).
        private static float Edge(Vector2 at, Vector2 a, Vector2 b)
        {
            Vector2 along = (b - a).normalized;
            var normal = new Vector2(-along.y, along.x);
            return Vector2.Dot(at - a, normal);
        }
    }
}
