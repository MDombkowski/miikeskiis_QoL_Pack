using System;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// The tools' pictures in the build menu, made as the game starts from the game's own pictures (AdvancedTerrainModifiers'
    /// pictures are its author's and OCDheim's, and aren't used; his word of 2026-09-30: make our own):
    /// - **The picture:** the game's own picture of the tool it copies, as it is; the raise picture upside down for the two
    ///   Lower grounds; and for the paved and cultivated paths, Pathen's brush stroke filled with the tool's own finish, the
    ///   game's paving stones or its tilled soil (<see cref="FillStroke"/>), keeping the stroke's own light, shade and edges.
    /// - **The mark,** drawn on it so a tool reads at a glance: a square frame for the square tools, a band for Pathen
    ///   (square), a cross for Remove, a down arrow for the Lower grounds, a level sign for Flatten.
    /// The picture's pixels are copied through a small render texture, as the game's pictures sit in an atlas that can't
    /// be read directly; if that fails, the tool keeps the game's picture and the log says so once. The drawing itself is
    /// plain arithmetic on the pixels, so the harness can make the pictures without the game.
    /// </summary>
    internal static class Icons
    {
        /// <summary>How near its middle the finish is read for a point of Pathen's stroke: the stroke runs corner to corner,
        /// the game's paving and cultivating pictures are round patches, so read at 0.72 of the way out every point of the
        /// stroke lands on the patch (checked on the game's own pictures, read from its files).</summary>
        internal const float StrokeScale = 0.72f;

        /// <summary>How much of the stroke's own light and shade the finish takes (its darker streak down the middle).</summary>
        internal const float StrokeShade = 0.6f;

        private static bool warned;

        private static readonly Color32 White = new Color32(255, 255, 255, 255);
        private static readonly Color32 Shadow = new Color32(0, 0, 0, 190);
        private static readonly Color32 Badge = new Color32(20, 20, 20, 210);
        private static readonly Color32 Red = new Color32(225, 45, 35, 255);

        /// <param name="own">The game's picture of the tool the copy is made from.</param>
        /// <param name="shape">For <see cref="Picture.InPathenStroke"/>: Pathen's picture, whose stroke is filled.</param>
        internal static Sprite Make(Sprite own, Sprite shape, Picture picture, Mark mark)
        {
            if (own == null)
            {
                return null;
            }
            try
            {
                Sprite source = picture == Picture.InPathenStroke && shape != null ? shape : own;
                Color32[] pixels = Read(source, out int width, out int height);
                if (source == shape)
                {
                    Color32[] finish = Read(own, out int finishWidth, out int finishHeight);
                    FillStroke(pixels, width, height, finish, finishWidth, finishHeight);
                }
                Draw(pixels, width, height, picture, mark);
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "Terraformer_" + own.name + "_" + picture + "_" + mark,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                var pivot = new Vector2(source.pivot.x / source.rect.width, source.pivot.y / source.rect.height);
                float perUnit = source.pixelsPerUnit * width / source.rect.width;
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, perUnit, 0, SpriteMeshType.FullRect);
                sprite.name = texture.name;
                return sprite;
            }
            catch (Exception error)
            {
                if (!warned)
                {
                    warned = true;
                    Module.Log.LogWarning($"A tool's picture couldn't be made ({error.Message}); Terraformer's tools show the game's " +
                                          "own pictures instead, with their names telling them apart.");
                }
                return own;
            }
        }

        /// <summary>The drawing on a picture's pixels (rows from the bottom, as a texture holds them): turned upside down
        /// for the Lower grounds, and the mark.</summary>
        internal static void Draw(Color32[] pixels, int width, int height, Picture picture, Mark mark)
        {
            if (picture == Picture.UpsideDown)
            {
                FlipUpsideDown(pixels, width, height);
            }
            DrawMark(pixels, width, height, mark);
        }

        // The picture's own part of its texture, at its own size (at most 128 px a side), trimmed margins put back.
        private static Color32[] Read(Sprite sprite, out int width, out int height)
        {
            Texture2D source = sprite.texture;
            if (source == null || source.width <= 0 || source.height <= 0)
            {
                throw new InvalidOperationException("it has no texture");
            }
            Rect whole = sprite.rect;
            Rect packed = sprite.textureRect;   // throws for a tightly packed picture
            Vector2 offset = sprite.textureRectOffset;
            float scale = Mathf.Min(1f, 128f / Mathf.Max(whole.width, whole.height));
            width = Mathf.Max(8, Mathf.RoundToInt(whole.width * scale));
            height = Mathf.Max(8, Mathf.RoundToInt(whole.height * scale));
            int x = Mathf.Clamp(Mathf.RoundToInt(offset.x * scale), 0, width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(offset.y * scale), 0, height - 1);
            int packedWidth = Mathf.Clamp(Mathf.RoundToInt(packed.width * scale), 1, width - x);
            int packedHeight = Mathf.Clamp(Mathf.RoundToInt(packed.height * scale), 1, height - y);
            RenderTexture target = RenderTexture.GetTemporary(packedWidth, packedHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture before = RenderTexture.active;
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, target, new Vector2(packed.width / source.width, packed.height / source.height),
                              new Vector2(packed.x / source.width, packed.y / source.height));
                RenderTexture.active = target;
                copy.SetPixels32(new Color32[width * height]);
                copy.ReadPixels(new Rect(0f, 0f, packedWidth, packedHeight), x, y, false);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(copy);
            }
        }

        private static void FlipUpsideDown(Color32[] pixels, int width, int height)
        {
            for (int row = 0; row < height / 2; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    int a = row * width + column;
                    int b = (height - 1 - row) * width + column;
                    (pixels[a], pixels[b]) = (pixels[b], pixels[a]);
                }
            }
        }

        /// <summary>
        /// Pathen's brush stroke filled with a tool's own finish (Paved road (path): the game's paving stones; Cultivate
        /// (path): its tilled soil): every pixel of the stroke takes the finish's colour at the matching place, read
        /// <see cref="StrokeScale"/> of the way out from the finish's middle, lit by the stroke's own light and shade
        /// (<see cref="StrokeShade"/>). The stroke's alpha, its feathered edges and its clear pixels are kept.
        /// </summary>
        internal static void FillStroke(Color32[] stroke, int width, int height, Color32[] finish, int finishWidth, int finishHeight)
        {
            double sum = 0;
            int count = 0;
            foreach (Color32 pixel in stroke)
            {
                if (pixel.a > 200)
                {
                    sum += Light(pixel);
                    count++;
                }
            }
            float mean = count > 0 ? (float)(sum / count) : 0f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color32 was = stroke[index];
                    if (was.a == 0)
                    {
                        continue;
                    }
                    float fx = FinishAt(x, width, finishWidth);
                    float fy = FinishAt(y, height, finishHeight);
                    Sample(finish, finishWidth, finishHeight, fx, fy, out float r, out float g, out float b);
                    float shade = mean > 0f ? 1f + StrokeShade * (Light(was) / mean - 1f) : 1f;
                    stroke[index] = new Color32(ToByte(r * shade), ToByte(g * shade), ToByte(b * shade), was.a);
                }
            }
        }

        /// <summary>Where a stroke's pixel reads the finish, along one side: <see cref="StrokeScale"/> of the way out from the
        /// middle, pixel centre to pixel centre, so pictures of two sizes line up (review round 2).</summary>
        internal static float FinishAt(int pixel, int size, int finishSize)
        {
            float middle = (size - 1) / 2f;
            return (middle + (pixel - middle) * StrokeScale + 0.5f) * finishSize / size - 0.5f;
        }

        // The finish's colour between its pixels (0 to 1 each channel), weighed from the four around the point by their
        // distance and their alpha, so its clear rim, whose colour means nothing, doesn't tint the stroke (review round 2).
        private static void Sample(Color32[] pixels, int width, int height, float x, float y, out float r, out float g, out float b)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            float fx = x - x0;
            float fy = y - y0;
            r = 0f;
            g = 0f;
            b = 0f;
            float total = 0f;
            float plainR = 0f;
            float plainG = 0f;
            float plainB = 0f;
            for (int dy = 0; dy <= 1; dy++)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    float weight = (dx == 0 ? 1f - fx : fx) * (dy == 0 ? 1f - fy : fy);
                    Color32 p = pixels[Within(y0 + dy, height) * width + Within(x0 + dx, width)];
                    float opaque = weight * p.a / 255f;
                    r += p.r / 255f * opaque;
                    g += p.g / 255f * opaque;
                    b += p.b / 255f * opaque;
                    total += opaque;
                    plainR += p.r / 255f * weight;
                    plainG += p.g / 255f * weight;
                    plainB += p.b / 255f * weight;
                }
            }
            if (total > 1e-4f)
            {
                r /= total;
                g /= total;
                b /= total;
            }
            else
            {
                r = plainR;
                g = plainG;
                b = plainB;
            }
        }

        private static int Within(int value, int size) => value < 0 ? 0 : value >= size ? size - 1 : value;

        // A pixel's light, 0 to 1.
        private static float Light(Color32 pixel) => (0.299f * pixel.r + 0.587f * pixel.g + 0.114f * pixel.b) / 255f;

        private static byte ToByte(float value) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value * 255f)));

        private static void DrawMark(Color32[] pixels, int width, int height, Mark mark)
        {
            int size = Math.Min(width, height);
            float thick = Math.Max(2f, size / 18f);
            if (mark == Mark.Frame || mark == Mark.FramedBand || mark == Mark.FramedDownArrow || mark == Mark.FramedLevel)
            {
                float inset = size * 0.1f;
                Frame(pixels, width, height, inset, inset, width - 1 - inset, height - 1 - inset, thick + 2f, Shadow);
                Frame(pixels, width, height, inset, inset, width - 1 - inset, height - 1 - inset, thick, White);
            }
            // The corner badge, bottom right: a dark disc holding the mark's own sign.
            float radius = size * 0.2f;
            float centreX = width - 1 - radius - size * 0.04f;
            float centreY = radius + size * 0.04f;
            switch (mark)
            {
                case Mark.Band:
                case Mark.FramedBand:
                    Disc(pixels, width, height, centreX, centreY, radius, Badge);
                    Line(pixels, width, height, centreX - radius * 0.6f, centreY, centreX + radius * 0.6f, centreY, thick, White);
                    break;
                case Mark.DownArrow:
                case Mark.FramedDownArrow:
                    Disc(pixels, width, height, centreX, centreY, radius, Badge);
                    Line(pixels, width, height, centreX, centreY + radius * 0.6f, centreX, centreY - radius * 0.55f, thick, White);
                    Line(pixels, width, height, centreX - radius * 0.45f, centreY - radius * 0.1f, centreX, centreY - radius * 0.6f, thick, White);
                    Line(pixels, width, height, centreX + radius * 0.45f, centreY - radius * 0.1f, centreX, centreY - radius * 0.6f, thick, White);
                    break;
                case Mark.Level:
                case Mark.FramedLevel:
                    // Two flat lines, a level sign.
                    Disc(pixels, width, height, centreX, centreY, radius, Badge);
                    Line(pixels, width, height, centreX - radius * 0.6f, centreY + radius * 0.25f, centreX + radius * 0.6f, centreY + radius * 0.25f, thick, White);
                    Line(pixels, width, height, centreX - radius * 0.6f, centreY - radius * 0.25f, centreX + radius * 0.6f, centreY - radius * 0.25f, thick, White);
                    break;
                case Mark.Slope:
                    // A ramp: a wedge whose top rises from the lower left to the upper right (Slope, B02).
                    Disc(pixels, width, height, centreX, centreY, radius, Badge);
                    Line(pixels, width, height, centreX - radius * 0.6f, centreY - radius * 0.4f, centreX + radius * 0.6f, centreY - radius * 0.4f, thick, White);
                    Line(pixels, width, height, centreX - radius * 0.6f, centreY - radius * 0.4f, centreX + radius * 0.6f, centreY + radius * 0.45f, thick, White);
                    Line(pixels, width, height, centreX + radius * 0.6f, centreY - radius * 0.4f, centreX + radius * 0.6f, centreY + radius * 0.45f, thick, White);
                    break;
                case Mark.Cross:
                    Disc(pixels, width, height, centreX, centreY, radius, Badge);
                    Line(pixels, width, height, centreX - radius * 0.5f, centreY - radius * 0.5f, centreX + radius * 0.5f, centreY + radius * 0.5f, thick, Red);
                    Line(pixels, width, height, centreX - radius * 0.5f, centreY + radius * 0.5f, centreX + radius * 0.5f, centreY - radius * 0.5f, thick, Red);
                    break;
            }
        }

        private static void Frame(Color32[] pixels, int width, int height, float left, float bottom, float right, float top, float thick, Color32 colour)
        {
            Line(pixels, width, height, left, bottom, right, bottom, thick, colour);
            Line(pixels, width, height, right, bottom, right, top, thick, colour);
            Line(pixels, width, height, right, top, left, top, thick, colour);
            Line(pixels, width, height, left, top, left, bottom, thick, colour);
        }

        private static void Line(Color32[] pixels, int width, int height, float x0, float y0, float x1, float y1, float thick, Color32 colour)
        {
            float length = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            int steps = Math.Max(1, (int)Math.Ceiling(length * 2f));
            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                Disc(pixels, width, height, x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, thick / 2f, colour);
            }
        }

        private static void Disc(Color32[] pixels, int width, int height, float centreX, float centreY, float radius, Color32 colour)
        {
            int left = Math.Max(0, (int)Math.Floor(centreX - radius));
            int right = Math.Min(width - 1, (int)Math.Ceiling(centreX + radius));
            int bottom = Math.Max(0, (int)Math.Floor(centreY - radius));
            int top = Math.Min(height - 1, (int)Math.Ceiling(centreY + radius));
            for (int y = bottom; y <= top; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    float dx = x - centreX;
                    float dy = y - centreY;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        Over(pixels, y * width + x, colour);
                    }
                }
            }
        }

        // Paints a colour over a pixel by its alpha, keeping what shows through.
        private static void Over(Color32[] pixels, int index, Color32 colour)
        {
            Color32 under = pixels[index];
            float a = colour.a / 255f;
            float b = under.a / 255f;
            float outAlpha = a + b * (1f - a);
            if (outAlpha <= 0f)
            {
                return;
            }
            byte Mix(byte top, byte bottom) => (byte)Math.Max(0, Math.Min(255, (int)Math.Round((top * a + bottom * b * (1f - a)) / outAlpha)));
            pixels[index] = new Color32(Mix(colour.r, under.r), Mix(colour.g, under.g), Mix(colour.b, under.b), (byte)Math.Round(outAlpha * 255f));
        }
    }
}
