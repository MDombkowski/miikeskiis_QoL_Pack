// The QoL Mods item stands part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemStands\. Every mod that uses
// it carries a copy in its own parts\ItemStands\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;
using UnityEngine;

namespace QoLMods.ItemStands
{
    /// <summary>The pictures a mark can be: one arrow, or the two together in one of three shapes.</summary>
    internal enum ArrowKind
    {
        /// <summary>Up: the wall (vertical) stand.</summary>
        Up,

        /// <summary>Right: the flat (horizontal) stand.</summary>
        Right,

        /// <summary>Both, side by side.</summary>
        SideBySide,

        /// <summary>Both, up above right.</summary>
        Stacked,

        /// <summary>Both, as one corner with a head at each end.</summary>
        Corner,

        /// <summary>The armor stand: a mannequin's head and shoulders on a pole.</summary>
        Armor,
    }

    /// <summary>
    /// The item stand marks' arrows, drawn in code, since the game's fonts have neither ↑ nor → (typed, they'd come from
    /// a Japanese or Chinese fallback font, twice as wide), and the armor stand's mannequin. White, with a dark outline or
    /// without, so an Image's colour tints the picture and leaves the outline dark, readable over any picture (as the
    /// pack's padlock, ReserveLock). Each picture is drawn the first time it's asked for, with smaller copies for small
    /// icons, and kept.
    /// </summary>
    internal static class StandArrows
    {
        /// <summary>The mannequin's half width at the sleeves, in units (it is an arrow's length tall).</summary>
        internal const float ArmorHalfWidth = 12f;

        // The mannequin, bottom to top, in units from its foot's middle: a base, a pole, a tunic (a body with short
        // sleeves at the shoulders, its corners rounded), and the head, a little apart from the shoulders so the outline
        // shows a neck.
        internal const float BaseHalfWidth = 7f;
        internal const float BaseHeight = 2.5f;
        internal const float PoleHalfWidth = 2f;
        internal const float TorsoBottom = 9f;
        internal const float TorsoTop = 20.5f;
        internal const float TorsoHalfWidth = 7f;
        internal const float SleeveBottom = 14f;
        internal const float TorsoRound = 2f;
        internal const float HeadRadius = 5.25f;

        /// <summary>One arrow's length, in the drawing's units; a mark's size in pixels is this length on screen.</summary>
        internal const float Length = 32f;

        internal const float HeadLength = 14f;

        internal const float HeadHalfWidth = 10f;

        internal const float ShaftHalfWidth = 3.5f;

        /// <summary>The outline's width, in units (about a pixel at the default size).</summary>
        internal const float Rim = 2.6f;

        /// <summary>The clear space round the arrows in a picture, in units: the outline and a pixel's smoothing.</summary>
        internal const float Margin = Rim + 1f;

        /// <summary>The space between the two arrows of "both", in units.</summary>
        internal const float Gap = 5f;

        /// <summary>How much longer each arrow of the corner shape is, so its two heads stay apart.</summary>
        internal const float CornerReach = 8f;

        /// <summary>Pixels per unit in the pictures: an arrow is 64 pixels long, twice the largest mark.</summary>
        internal const int Resolution = 2;

        private static readonly Dictionary<int, Sprite> Made = new Dictionary<int, Sprite>();

        /// <summary>The picture for an item that fits so, in a mark's shape; null for an item that fits no stand.</summary>
        internal static Sprite Of(Fit fit, BothShape bothShape, bool outline)
        {
            if (fit == Fit.None)
            {
                return null;
            }
            ArrowKind kind = KindOf(fit, bothShape);
            int key = (int)kind * 2 + (outline ? 1 : 0);
            if (Made.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            sprite = Make(kind, outline);
            Made[key] = sprite;
            return sprite;
        }

        internal static ArrowKind KindOf(Fit fit, BothShape bothShape)
        {
            if (fit == Fit.Armor)
            {
                return ArrowKind.Armor;
            }
            if (fit == Fit.Wall)
            {
                return ArrowKind.Up;
            }
            if (fit == Fit.Flat)
            {
                return ArrowKind.Right;
            }
            return bothShape == BothShape.Stacked ? ArrowKind.Stacked : bothShape == BothShape.Corner ? ArrowKind.Corner : ArrowKind.SideBySide;
        }

        /// <summary>A picture's width and height in pixels.</summary>
        internal static Vector2Int PixelSize(ArrowKind kind)
        {
            Vector2 units = DrawnSize(kind);
            return new Vector2Int(Mathf.CeilToInt(units.x * Resolution - 0.001f), Mathf.CeilToInt(units.y * Resolution - 0.001f));
        }

        /// <summary>A picture's width and height in units (its pixels over the resolution), margins included: a mark on
        /// screen is this times its size over Length.</summary>
        internal static Vector2 UnitSize(ArrowKind kind)
        {
            Vector2Int pixels = PixelSize(kind);
            return new Vector2(pixels.x / (float)Resolution, pixels.y / (float)Resolution);
        }

        // What the arrows take, margins included, before rounding up to whole pixels.
        private static Vector2 DrawnSize(ArrowKind kind)
        {
            float wide = 2f * HeadHalfWidth;
            switch (kind)
            {
                case ArrowKind.Up:
                    return new Vector2(wide + 2f * Margin, Length + 2f * Margin);
                case ArrowKind.Right:
                    return new Vector2(Length + 2f * Margin, wide + 2f * Margin);
                case ArrowKind.SideBySide:
                    return new Vector2(wide + Gap + Length + 2f * Margin, Length + 2f * Margin);
                case ArrowKind.Stacked:
                    return new Vector2(Length + 2f * Margin, Length + Gap + wide + 2f * Margin);
                case ArrowKind.Armor:
                    return new Vector2(2f * ArmorHalfWidth + 2f * Margin, Length + 2f * Margin);
                default:
                    return new Vector2(Length + CornerReach + 2f * Margin, Length + CornerReach + 2f * Margin);
            }
        }

        private static Sprite Make(ArrowKind kind, bool outline)
        {
            Vector2Int size = PixelSize(kind);
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGBA32, true)   // with smaller copies, for small icons
            {
                name = $"QoLMods_StandArrows_{kind}{(outline ? "_Outlined" : string.Empty)}",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,   // never unloaded with a scene, never saved
            };
            texture.SetPixels(Pixels(kind, outline));
            texture.Apply(true, true);   // the smaller copies made; no longer readable: its copy in memory is let go
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size.x, size.y), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>
        /// A picture's pixels, row by row from the bottom, as Texture2D.SetPixels takes them: white where an arrow is, a
        /// dark rim round it (with <paramref name="outline"/>), clear elsewhere, each edge smoothed over a pixel. Plain
        /// arithmetic on distances to each shape, so the scratch harness can check it without the engine.
        /// </summary>
        internal static Color[] Pixels(ArrowKind kind, bool outline)
        {
            Vector2Int size = PixelSize(kind);
            // The arrows are laid out in DrawnSize; the whole-pixel picture is a little bigger, so they are centred in it.
            Vector2 drawn = DrawnSize(kind);
            Vector2 shift = (new Vector2(size.x, size.y) / Resolution - drawn) * 0.5f;
            var dark = new Color(0.08f, 0.06f, 0.05f, 1f);
            var pixels = new Color[size.x * size.y];
            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var at = new Vector2((x + 0.5f) / Resolution, (y + 0.5f) / Resolution) - shift;
                    float inPixels = Distance(at, kind) * Resolution;
                    float fill = Coverage(inPixels);
                    Color color;
                    if (outline)
                    {
                        float edge = Coverage(inPixels - Rim * Resolution);
                        color = Color.Lerp(dark, Color.white, fill);
                        color.a = Mathf.Max(fill, edge);
                    }
                    else
                    {
                        color = Color.white;
                        color.a = fill;
                    }
                    pixels[y * size.x + x] = color;
                }
            }
            return pixels;
        }

        // How much of a pixel a shape covers, from its signed distance to the pixel's centre in pixels (negative inside).
        private static float Coverage(float distance) => Mathf.Clamp01(0.5f - distance);

        /// <summary>The signed distance, in units, from a point of a picture (measured from its drawn corner) to its
        /// arrows: negative inside.</summary>
        internal static float Distance(Vector2 at, ArrowKind kind)
        {
            float wide = HeadHalfWidth;
            switch (kind)
            {
                case ArrowKind.Up:
                    return UpArrow(at, new Vector2(Margin + wide, Margin), Length);
                case ArrowKind.Right:
                    return RightArrow(at, new Vector2(Margin, Margin + wide), Length);
                case ArrowKind.SideBySide:
                    // The right arrow sits on the up arrow's baseline, as a typed ↑→ does: beside the up arrow's shaft,
                    // below its head, so their outlines stay apart.
                    return Mathf.Min(UpArrow(at, new Vector2(Margin + wide, Margin), Length),
                                     RightArrow(at, new Vector2(Margin + 2f * wide + Gap, Margin + wide), Length));
                case ArrowKind.Stacked:
                    return Mathf.Min(UpArrow(at, new Vector2(Margin + wide, Margin + 2f * wide + Gap), Length),
                                     RightArrow(at, new Vector2(Margin, Margin + wide), Length));
                case ArrowKind.Armor:
                    return Mannequin(at - new Vector2(Margin + ArmorHalfWidth, Margin));
                default:
                    {
                        // One corner: the shafts' centre lines meet at (wide, wide) from the drawn corner, each arrow
                        // starting at the other's outer edge, so the corner is square.
                        float start = wide - ShaftHalfWidth;
                        float length = Length + CornerReach - start;
                        return Mathf.Min(UpArrow(at, new Vector2(Margin + wide, Margin + start), length),
                                         RightArrow(at, new Vector2(Margin + start, Margin + wide), length));
                    }
            }
        }

        // An arrow pointing up from its tail's middle, of a given length.
        private static float UpArrow(Vector2 at, Vector2 tail, float length)
        {
            float shaft = length - HeadLength;
            // The shaft reaches a little into the head, so no seam shows where they meet.
            float body = Box(at, new Vector2(tail.x, tail.y + shaft / 2f + 0.25f), new Vector2(ShaftHalfWidth, shaft / 2f + 0.25f));
            float head = Triangle(at, new Vector2(tail.x - HeadHalfWidth, tail.y + shaft), new Vector2(tail.x + HeadHalfWidth, tail.y + shaft),
                                  new Vector2(tail.x, tail.y + length));
            return Mathf.Min(body, head);
        }

        // An arrow pointing right from its tail's middle.
        private static float RightArrow(Vector2 at, Vector2 tail, float length)
        {
            float shaft = length - HeadLength;
            float body = Box(at, new Vector2(tail.x + shaft / 2f + 0.25f, tail.y), new Vector2(shaft / 2f + 0.25f, ShaftHalfWidth));
            float head = Triangle(at, new Vector2(tail.x + shaft, tail.y + HeadHalfWidth), new Vector2(tail.x + shaft, tail.y - HeadHalfWidth),
                                  new Vector2(tail.x + length, tail.y));
            return Mathf.Min(body, head);
        }

        // The armor stand's mannequin, from its foot's middle: base, pole, tunic, head.
        private static float Mannequin(Vector2 at)
        {
            float foot = Box(at, new Vector2(0f, BaseHeight / 2f), new Vector2(BaseHalfWidth, BaseHeight / 2f));
            // The pole reaches a little into the base and the tunic, so no seam shows where they meet.
            float pole = Box(at, new Vector2(0f, (BaseHeight + TorsoBottom) / 2f), new Vector2(PoleHalfWidth, (TorsoBottom - BaseHeight) / 2f + 0.25f));
            float body = RoundBox(at, new Vector2(0f, (TorsoBottom + TorsoTop) / 2f), new Vector2(TorsoHalfWidth, (TorsoTop - TorsoBottom) / 2f), TorsoRound);
            float sleeves = RoundBox(at, new Vector2(0f, (SleeveBottom + TorsoTop) / 2f), new Vector2(ArmorHalfWidth, (TorsoTop - SleeveBottom) / 2f), TorsoRound);
            float head = (at - new Vector2(0f, Length - HeadRadius)).magnitude - HeadRadius;
            return Mathf.Min(Mathf.Min(foot, pole), Mathf.Min(Mathf.Min(body, sleeves), head));
        }

        // Signed distance to a box with rounded corners, by its centre, half size (corners included) and corner radius.
        private static float RoundBox(Vector2 at, Vector2 centre, Vector2 half, float round) =>
            Box(at, centre, half - new Vector2(round, round)) - round;

        // Signed distance to a box, by its centre and half size.
        private static float Box(Vector2 at, Vector2 centre, Vector2 half)
        {
            var d = new Vector2(Mathf.Abs(at.x - centre.x) - half.x, Mathf.Abs(at.y - centre.y) - half.y);
            var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f));
            return outside.magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        // Signed distance to a triangle (Inigo Quilez's formula): the nearest edge's distance, negative inside.
        private static float Triangle(Vector2 at, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            Vector2 e0 = p1 - p0, e1 = p2 - p1, e2 = p0 - p2;
            Vector2 v0 = at - p0, v1 = at - p1, v2 = at - p2;
            Vector2 q0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 q1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 q2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float distance = Mathf.Min(Mathf.Min(Vector2.Dot(q0, q0), Vector2.Dot(q1, q1)), Vector2.Dot(q2, q2));
            float side = Mathf.Min(Mathf.Min(s * (v0.x * e0.y - v0.y * e0.x), s * (v1.x * e1.y - v1.y * e1.x)), s * (v2.x * e2.y - v2.y * e2.x));
            return -Mathf.Sqrt(distance) * Mathf.Sign(side);
        }
    }
}
