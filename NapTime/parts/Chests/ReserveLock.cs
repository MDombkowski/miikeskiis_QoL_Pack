// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>
    /// The padlock that shows Reserve Stock's mark (card A12), the same in every mod: closed for a reserved chest, open
    /// for a free one. Reserve Stock's button in the chest window shows it, and Craft from Chests puts it on the counts.
    /// Drawn in code, once, since the game's fonts have no padlock: white with a dark rim, so an Image's colour tints the
    /// lock and leaves the rim dark, readable over any picture.
    /// </summary>
    internal static class ReserveLock
    {
        /// <summary>The colour of a reserved chest's lock: orange, as the "Reserved" line in its hover text.</summary>
        internal static readonly Color ReservedColor = new Color(1f, 0.62f, 0.12f, 1f);

        /// <summary>The colour of a free chest's open lock.</summary>
        internal static readonly Color FreeColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        /// <summary>The picture's width and height in pixels.</summary>
        internal const int Size = 32;

        private static Sprite closed;
        private static Sprite open;

        /// <summary>The closed padlock (a reserved chest). Drawn the first time it's asked for, and again if Unity has
        /// destroyed it.</summary>
        internal static Sprite Closed => closed != null ? closed : (closed = Make(shut: true));

        /// <summary>The open padlock (a chest that isn't reserved).</summary>
        internal static Sprite Open => open != null ? open : (open = Make(shut: false));

        private static Sprite Make(bool shut)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true)   // with smaller copies, for the badge's 13 px
            {
                name = shut ? "QoLMods_ReserveLock_Closed" : "QoLMods_ReserveLock_Open",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,   // never unloaded with a scene, never saved
            };
            texture.SetPixels(Pixels(shut));
            texture.Apply(true, true);   // the smaller copies made; no longer readable: its copy in memory is let go
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>
        /// The picture's pixels, row by row from the bottom, as Texture2D.SetPixels takes them: white where the lock is, a
        /// dark rim around it and inside its keyhole, clear elsewhere, each edge smoothed over a pixel. Plain arithmetic
        /// on distances to each shape, so the scratch harness can check it without the engine.
        /// </summary>
        internal static Color[] Pixels(bool shut)
        {
            const float rim = 1.3f;
            var dark = new Color(0.08f, 0.06f, 0.05f, 1f);
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var at = new Vector2(x + 0.5f, y + 0.5f);
                    float shape = Distance(at, shut);
                    float fill = Coverage(shape);
                    float edge = Coverage(shape - rim);
                    Color color = Color.Lerp(dark, Color.white, fill);
                    color.a = Mathf.Max(fill, edge);
                    pixels[y * Size + x] = color;
                }
            }
            return pixels;
        }

        // How much of a pixel a shape covers, from its signed distance to the pixel's centre (negative inside).
        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        // The signed distance from a point to the padlock: its body, less a keyhole, and its shackle (lifted, one leg out
        // of the body, when open).
        private static float Distance(Vector2 at, bool shut)
        {
            float body = Box(at, new Vector2(16f, 9f), new Vector2(10f, 7f), 2.5f);
            float keyhole = Mathf.Min(Circle(at, new Vector2(16f, 10.5f), 2.2f), Box(at, new Vector2(16f, 7.3f), new Vector2(0.9f, 3f), 0f));
            body = Mathf.Max(body, -keyhole);

            float lift = shut ? 0f : 3f;
            var centre = new Vector2(16f, 19.5f + lift);
            const float middle = 6f;
            const float half = 1.6f;
            // The arch: a ring, its top half only.
            float arch = Mathf.Max(Mathf.Abs((at - centre).magnitude - middle) - half, centre.y - at.y);
            // The legs, down into the body; open, the right one stops short of it. They reach a little into the arch,
            // so no seam shows where they meet.
            float top = centre.y + 0.6f;
            float left = Box(at, new Vector2(16f - middle, (14f + top) / 2f), new Vector2(half, (top - 14f) / 2f), 0f);
            float rightFoot = shut ? 14f : centre.y - 2.5f;
            float right = Box(at, new Vector2(16f + middle, (rightFoot + top) / 2f), new Vector2(half, (top - rightFoot) / 2f), 0f);
            float shackle = Mathf.Min(arch, Mathf.Min(left, right));
            return Mathf.Min(body, shackle);
        }

        // Signed distance to a box with rounded corners, by its centre and half size.
        private static float Box(Vector2 at, Vector2 centre, Vector2 half, float round)
        {
            var d = new Vector2(Mathf.Abs(at.x - centre.x) - half.x + round, Mathf.Abs(at.y - centre.y) - half.y + round);
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        private static float Circle(Vector2 at, Vector2 centre, float radius) => (at - centre).magnitude - radius;
    }
}
