using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AddToCart
{
    /// <summary>
    /// Grey copies of the thumbnails' pictures, for thumbnails the chests can't cover (the setting "Thumbnails the chests
    /// can't cover"). Unity's own UI material can't turn a picture grey (a colour only multiplies it), and 0.3.1's borrowing
    /// of the game's icon material found nothing to borrow (his test: "They're still in color"). So the mod makes its own:
    /// it draws the picture's part of its texture into a small render texture, reads the pixels back, turns each grey by
    /// its brightness, and keeps the copy as a sprite of its own. A few a frame (Work), each picture once a session.
    /// </summary>
    internal static class Greys
    {
        private static readonly Dictionary<Sprite, Sprite> Made = new Dictionary<Sprite, Sprite>();
        private static readonly HashSet<Sprite> Failed = new HashSet<Sprite>();
        private static readonly Queue<Sprite> Wanted = new Queue<Sprite>();
        private static readonly HashSet<Sprite> Queued = new HashSet<Sprite>();
        private static bool loggedFailure;
        private static bool loggedSuccess;

        /// <summary>The largest side of a grey copy, in pixels: the thumbnails are drawn at 52 px.</summary>
        private const float MostSize = 128f;

        /// <summary>Where the first failure and the first copy made are written, once a session (the module's log; the
        /// window sets them).</summary>
        internal static Action<string> Warn;

        internal static Action<string> Info;

        /// <summary>The grey copy of a picture if it is made; otherwise null, and the picture waits its turn (Work), unless
        /// it can't be made at all (CanGrey).</summary>
        internal static Sprite Of(Sprite sprite)
        {
            if (sprite == null)
            {
                return null;
            }
            if (Made.TryGetValue(sprite, out Sprite grey) && grey != null)
            {
                return grey;
            }
            if (!Failed.Contains(sprite) && Queued.Add(sprite))
            {
                Wanted.Enqueue(sprite);
            }
            return null;
        }

        /// <summary>False once making this picture's grey copy has failed: it then stays in colour, faded.</summary>
        internal static bool CanGrey(Sprite sprite) => sprite != null && !Failed.Contains(sprite);

        /// <summary>Makes up to <paramref name="most"/> of the grey copies waiting; how many it made (the cells showing them
        /// are then drawn again). The first copy made, and the first that can't be, are written to the log (review 1: a
        /// grey that fails quietly looks, in game, like no grey at all, which was 0.3.1's trouble).</summary>
        internal static int Work(int most)
        {
            int made = 0;
            while (made < most && Wanted.Count > 0)
            {
                Sprite sprite = Wanted.Dequeue();
                Queued.Remove(sprite);
                if (sprite == null || Made.ContainsKey(sprite) || Failed.Contains(sprite))
                {
                    continue;
                }
                try
                {
                    Sprite grey = Make(sprite, out string how);
                    Made[sprite] = grey;
                    made++;
                    if (!loggedSuccess)
                    {
                        loggedSuccess = true;
                        Info?.Invoke($"Grey copies of the thumbnails the chests can't cover: the first made, of \"{sprite.name}\" ({how}).");
                    }
                }
                catch (Exception error)
                {
                    Failed.Add(sprite);
                    if (!loggedFailure)
                    {
                        loggedFailure = true;
                        Warn?.Invoke($"A grey copy of the picture \"{sprite.name}\" couldn't be made: {error.Message}. Such pictures stay in colour, faded.");
                    }
                }
            }
            return made;
        }

        /// <summary>Lets go of the copies of pictures the game has unloaded (a new world may load its icons afresh): called
        /// when the window is built again.</summary>
        internal static void Prune()
        {
            foreach (Sprite gone in Made.Keys.Where(key => key == null).ToList())
            {
                Sprite grey = Made[gone];
                Made.Remove(gone);
                if (grey != null)
                {
                    UnityEngine.Object.Destroy(grey.texture);
                    UnityEngine.Object.Destroy(grey);
                }
            }
            Failed.RemoveWhere(key => key == null);
            Queued.RemoveWhere(key => key == null);
        }

        // The picture's own part of its texture (in an atlas, its packed place), at the picture's own shape and no bigger
        // than MostSize (the thumbnails are drawn at 52 px; review 1), with a packed picture's trimmed margins put back, so
        // the copy lines up with the picture exactly. Throws, saying why, when it can't.
        private static Sprite Make(Sprite sprite, out string how)
        {
            Texture2D source = sprite.texture;
            if (source == null || source.width <= 0 || source.height <= 0)
            {
                throw new InvalidOperationException("it has no texture");
            }
            Rect whole = sprite.rect;
            Rect packed = sprite.textureRect;   // throws for a tightly packed picture: that one stays in colour
            Vector2 offset = sprite.textureRectOffset;
            if (whole.width < 1f || whole.height < 1f || packed.width < 1f || packed.height < 1f)
            {
                throw new InvalidOperationException($"it is {whole.width}×{whole.height}, {packed.width}×{packed.height} packed");
            }
            if (offset.x < 0f || offset.y < 0f || offset.x + packed.width > whole.width + 0.5f || offset.y + packed.height > whole.height + 0.5f)
            {
                throw new InvalidOperationException($"its packed part ({packed.width}×{packed.height} at {offset.x}, {offset.y}) lies outside its {whole.width}×{whole.height}");
            }
            float scale = Mathf.Min(1f, MostSize / Mathf.Max(whole.width, whole.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(whole.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(whole.height * scale));
            int x = Mathf.Clamp(Mathf.RoundToInt(offset.x * scale), 0, width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(offset.y * scale), 0, height - 1);
            int packedWidth = Mathf.Clamp(Mathf.RoundToInt(packed.width * scale), 1, width - x);
            int packedHeight = Mathf.Clamp(Mathf.RoundToInt(packed.height * scale), 1, height - y);
            how = $"{width}×{height} from its {whole.width}×{whole.height} in a {source.width}×{source.height} texture{(sprite.packed ? ", packed" : string.Empty)}";
            RenderTexture target = RenderTexture.GetTemporary(packedWidth, packedHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            RenderTexture before = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target, new Vector2(packed.width / source.width, packed.height / source.height),
                              new Vector2(packed.x / source.width, packed.y / source.height));
                RenderTexture.active = target;
                var copy = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = sprite.name + " (grey)",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                if (packedWidth != width || packedHeight != height)
                {
                    copy.SetPixels32(new Color32[width * height]);   // the trimmed margins: see-through
                }
                copy.ReadPixels(new Rect(0f, 0f, packedWidth, packedHeight), x, y, false);
                Color32[] pixels = copy.GetPixels32();
                Grey(pixels);
                copy.SetPixels32(pixels);
                copy.Apply(false, true);   // and let go of the copy kept for reading
                var pivot = new Vector2(sprite.pivot.x / whole.width, sprite.pivot.y / whole.height);
                Sprite grey = Sprite.Create(copy, new Rect(0f, 0f, width, height), pivot, sprite.pixelsPerUnit * scale, 0, SpriteMeshType.FullRect);
                grey.name = copy.name;
                return grey;
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>Turns each pixel grey by its brightness (the usual weights for red, green and blue), keeping how
        /// see-through it is.</summary>
        internal static void Grey(Color32[] pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                byte level = (byte)Math.Min(255, (pixel.r * 299 + pixel.g * 587 + pixel.b * 114 + 500) / 1000);
                pixels[i] = new Color32(level, level, level, pixel.a);
            }
        }
    }
}
