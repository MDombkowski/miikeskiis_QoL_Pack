using System;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// The pictures on the sort buttons and the trash can (card A53): three bars growing shorter beside an arrow pointing
    /// down, and a bin with its lid. Drawn in code, once each, since the game has neither: white with a dark rim, so a
    /// button's colour tints it and the rim keeps it readable, as StoreStash's quick stack picture (Icon) and the pack's
    /// padlock are drawn.
    /// </summary>
    internal static class SortTrashPictures
    {
        /// <summary>Each picture's width and height in pixels.</summary>
        internal const int Size = 32;

        private static Sprite sort;
        private static Sprite trash;

        /// <summary>The sort picture, drawn the first time it's asked for, and again if Unity has destroyed it.</summary>
        internal static Sprite Sort => sort != null ? sort : (sort = Make("StoreStash_SortIcon", SortShape));

        /// <summary>The trash can's picture, the same way.</summary>
        internal static Sprite Trash => trash != null ? trash : (trash = Make("StoreStash_TrashIcon", TrashShape));

        private static Sprite Make(string name, Func<Vector2, float> shape)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,   // never unloaded with a scene, never saved
            };
            texture.SetPixels(Pixels(shape));
            texture.Apply(true, true);
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            made.name = name;
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }

        /// <summary>A picture's pixels, row by row from the bottom as Texture2D.SetPixels takes them: white where the shape
        /// is, a dark rim around it, clear elsewhere, each edge smoothed over a pixel. Plain arithmetic, so the scratch
        /// harness can check it without the engine.</summary>
        internal static Color[] Pixels(Func<Vector2, float> shape)
        {
            const float rim = 1.3f;
            var dark = new Color(0.08f, 0.06f, 0.05f, 1f);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float distance = shape(new Vector2(x + 0.5f, y + 0.5f));
                    float fill = Coverage(distance);
                    float edge = Coverage(distance - rim);
                    Color color = Color.Lerp(dark, Color.white, fill);
                    color.a = Mathf.Max(fill, edge);
                    pixels[y * Size + x] = color;
                }
            }
            return pixels;
        }

        /// <summary>The sort picture's signed distance: three bars, each shorter than the one above, and an arrow down.</summary>
        internal static float SortShape(Vector2 at)
        {
            float top = Box(at, new Vector2(13f, 25f), new Vector2(9f, 1.8f), 1.2f);
            float middle = Box(at, new Vector2(10.5f, 16f), new Vector2(6.5f, 1.8f), 1.2f);
            float bottom = Box(at, new Vector2(8f, 7f), new Vector2(4f, 1.8f), 1.2f);
            float shaft = Segment(at, new Vector2(26f, 27f), new Vector2(26f, 8f)) - 1.6f;
            float head = Mathf.Min(Segment(at, new Vector2(21.5f, 12.5f), new Vector2(26f, 7f)), Segment(at, new Vector2(30.5f, 12.5f), new Vector2(26f, 7f))) - 1.6f;
            return Mathf.Min(Mathf.Min(top, middle), Mathf.Min(bottom, Mathf.Min(shaft, head)));
        }

        /// <summary>The trash can's signed distance: a bin with two ribs cut into it, its lid and the lid's handle.</summary>
        internal static float TrashShape(Vector2 at)
        {
            float body = Box(at, new Vector2(16f, 11.5f), new Vector2(8.5f, 9.5f), 1.5f);
            float ribs = Mathf.Min(Box(at, new Vector2(12.8f, 11.5f), new Vector2(0.9f, 6.2f), 0.9f),
                                   Box(at, new Vector2(19.2f, 11.5f), new Vector2(0.9f, 6.2f), 0.9f));
            float bin = Mathf.Max(body, -ribs);
            float lid = Box(at, new Vector2(16f, 24.3f), new Vector2(11f, 1.6f), 1f);
            float handle = Box(at, new Vector2(16f, 27.3f), new Vector2(3.5f, 1.4f), 0.8f);
            return Mathf.Min(bin, Mathf.Min(lid, handle));
        }

        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        // Signed distance to a box with rounded corners, by its centre and half size.
        private static float Box(Vector2 at, Vector2 centre, Vector2 half, float round)
        {
            var d = new Vector2(Mathf.Abs(at.x - centre.x) - half.x + round, Mathf.Abs(at.y - centre.y) - half.y + round);
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        // Distance to a line segment.
        private static float Segment(Vector2 at, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(at - a, ab) / ab.sqrMagnitude);
            return (at - (a + ab * t)).magnitude;
        }
    }
}
