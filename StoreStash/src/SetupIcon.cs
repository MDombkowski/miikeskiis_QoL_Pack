using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// The setup button's picture: a tag, as his words were "tag them with categories" (card A41). Drawn in code, once,
    /// since the game has no such icon: white with a dark rim, so the button's colour tints it and the rim keeps it
    /// readable, as the quick stack's button (Icon) and the pack's padlock are drawn.
    /// </summary>
    internal static class SetupIcon
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
                name = "StoreStash_SetupIcon",
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

        /// <summary>The pixels, row by row from the bottom as Texture2D.SetPixels takes them: white where the tag is, a dark
        /// rim around it, clear elsewhere and in its hole, each edge smoothed over a pixel. Plain arithmetic on distances,
        /// so the scratch harness can check it without the engine.</summary>
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

        // The signed distance to the tag, tilted a little: a body with a pointed end on the left, less a hole near the point.
        private static float Distance(Vector2 at)
        {
            Vector2 p = Rotate(at - new Vector2(16f, 16f), -0.35f) + new Vector2(16f, 16f);
            float body = Box(p, new Vector2(19.5f, 16f), new Vector2(8.5f, 7f), 1.5f);
            // The point: the half plane left of each slanted edge, cut to the body's height, joined to the body.
            float upper = Edge(p, new Vector2(4f, 16f), new Vector2(11.5f, 23f));
            float lower = Edge(p, new Vector2(11.5f, 9f), new Vector2(4f, 16f));
            float point = Mathf.Max(Mathf.Max(upper, lower), Mathf.Max(p.x - 12f, Mathf.Abs(p.y - 16f) - 7f));
            float tag = Mathf.Min(body, point);
            float hole = 2.2f - (p - new Vector2(10.5f, 16f)).magnitude;
            return Mathf.Max(tag, hole);
        }

        private static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians);
            float s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // Signed distance to a box with rounded corners, by its centre and half size.
        private static float Box(Vector2 at, Vector2 centre, Vector2 half, float round)
        {
            var d = new Vector2(Mathf.Abs(at.x - centre.x) - half.x + round, Mathf.Abs(at.y - centre.y) - half.y + round);
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
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
