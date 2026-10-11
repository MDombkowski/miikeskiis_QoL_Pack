using System;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// The quick stack button's picture: a chest with an arrow pointing down into it. Drawn in code, once, since the game
    /// has no such icon: white with a dark rim, so the button's colour tints it and the rim keeps it readable, as the pack's
    /// padlock is drawn (QoLMods\parts\Chests\ReserveLock.cs). The personal quick stack's picture (PersonalIcon) is drawn
    /// the same way from the same shapes.
    /// </summary>
    internal static class Icon
    {
        /// <summary>The picture's width and height in pixels.</summary>
        internal const int Size = 32;

        private static Sprite sprite;

        /// <summary>Drawn the first time it's asked for, and again if Unity has destroyed it.</summary>
        internal static Sprite Sprite => sprite != null ? sprite : (sprite = Make("StoreStash_Icon", Pixels()));

        /// <summary>A sprite of Size by Size from these pixels, never unloaded with a scene and never saved.</summary>
        internal static Sprite Make(string name, Color[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,   // never unloaded with a scene, never saved
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            made.name = texture.name;
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }

        /// <summary>The pixels, row by row from the bottom as Texture2D.SetPixels takes them: white where the picture is, a
        /// dark rim around it, clear elsewhere, each edge smoothed over a pixel. Plain arithmetic on distances to each shape,
        /// so the scratch harness can check it without the engine.</summary>
        internal static Color[] Pixels() => Pixels(Distance);

        /// <summary>The pixels of any picture given by its signed distance, drawn as this one is.</summary>
        internal static Color[] Pixels(Func<Vector2, float> distance)
        {
            const float rim = 1.3f;
            var dark = new Color(0.08f, 0.06f, 0.05f, 1f);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var at = new Vector2(x + 0.5f, y + 0.5f);
                    float shape = distance(at);
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

        /// <summary>The signed distance to the picture: the chest's body and its lid, with a gap between them, and the
        /// arrow above.</summary>
        internal static float Distance(Vector2 at)
        {
            float body = Box(at, new Vector2(16f, 7.5f), new Vector2(11f, 5.5f), 1.5f);
            float lid = Box(at, new Vector2(16f, 15.5f), new Vector2(11f, 1.8f), 1f);
            float clasp = Box(at, new Vector2(16f, 11f), new Vector2(1.8f, 2.2f), 0.5f);
            float shaft = Segment(at, new Vector2(16f, 30f), new Vector2(16f, 21.5f)) - 1.6f;
            float head = Mathf.Min(Segment(at, new Vector2(11f, 25f), new Vector2(16f, 20f)), Segment(at, new Vector2(21f, 25f), new Vector2(16f, 20f))) - 1.6f;
            return Mathf.Min(Mathf.Min(Mathf.Min(body, lid), clasp), Mathf.Min(shaft, head));
        }

        /// <summary>Signed distance to a box with rounded corners, by its centre and half size.</summary>
        internal static float Box(Vector2 at, Vector2 centre, Vector2 half, float round)
        {
            var d = new Vector2(Mathf.Abs(at.x - centre.x) - half.x + round, Mathf.Abs(at.y - centre.y) - half.y + round);
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        /// <summary>Distance to a line segment.</summary>
        internal static float Segment(Vector2 at, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(at - a, ab) / ab.sqrMagnitude);
            return (at - (a + ab * t)).magnitude;
        }
    }
}
