// The QoL Mods item marks, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemMarks\. Every mod that uses it
// carries a copy in its own parts\ItemMarks\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using UnityEngine;
using UnityEngine.UI;

namespace QoLMods.ItemMarks
{
    /// <summary>
    /// The border a mark draws on an item's cell: a thin ring along the cell's edge, drawn in code once (white, tinted by
    /// the Image's colour: gold for favourites), nine-sliced so it keeps its width at any cell size. It sits over the cell's
    /// own pictures and never catches a click (raycastTarget off), so dragging, splitting and right-clicking the item work
    /// as before.
    /// </summary>
    internal static class MarkBorder
    {
        /// <summary>The picture's width and height in pixels.</summary>
        internal const int Size = 16;

        /// <summary>How thick the ring is, in pixels (and so in the cell's units).</summary>
        internal const int Ring = 2;

        /// <summary>How far inside the cell's edge a kind's ring sits, by its place (MarkUser.Place): trash marks (place 1)
        /// draw inside favourites (place 0), so both show.</summary>
        internal const float Inset = 1f;

        internal const float InsetPerKind = 3f;

        private static Sprite sprite;

        /// <summary>The ring, drawn the first time it's asked for, and again if Unity has destroyed it.</summary>
        internal static Sprite Sprite => sprite != null ? sprite : (sprite = Make());

        private static Sprite Make()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "QoLMods_ItemMarks_Ring",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,   // never unloaded with a scene, never saved
            };
            texture.SetPixels(Pixels());
            texture.Apply(false, true);
            // The ring and a pixel inside it are the nine-slice's border: the middle stretches, clear.
            float border = Ring + 1;
            Sprite made = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                        new Vector4(border, border, border, border));
            made.name = texture.name;
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }

        /// <summary>The picture's pixels, row by row from the bottom as Texture2D.SetPixels takes them: white on the ring
        /// (the outermost Ring pixels), clear inside. Plain arithmetic, so the scratch harness can check it without the
        /// engine.</summary>
        internal static Color[] Pixels()
        {
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(Size - 1 - x, Size - 1 - y));
                    pixels[y * Size + x] = edge < Ring ? Color.white : Color.clear;
                }
            }
            return pixels;
        }

        /// <summary>Shows or hides the ring named <paramref name="name"/> on a cell (made the first time it shows), at its
        /// kind's <paramref name="place"/> (0 along the edge, each next one inside), in <paramref name="colour"/>.</summary>
        internal static void Show(Transform cell, string name, int place, bool on, Color colour)
        {
            Transform found = cell.Find(name);
            if (!on)
            {
                if (found != null && found.gameObject.activeSelf)
                {
                    found.gameObject.SetActive(false);
                }
                return;
            }
            Image image;
            if (found == null)
            {
                var made = new GameObject(name, typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)made.transform;
                rect.SetParent(cell, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                float inset = Inset + place * InsetPerKind;
                rect.offsetMin = new Vector2(inset, inset);
                rect.offsetMax = new Vector2(-inset, -inset);
                image = made.GetComponent<Image>();
                image.sprite = Sprite;
                image.type = Image.Type.Sliced;
                image.fillCenter = false;
                image.raycastTarget = false;
                rect.SetAsLastSibling();
            }
            else
            {
                image = found.GetComponent<Image>();
                if (!found.gameObject.activeSelf)
                {
                    found.gameObject.SetActive(true);
                }
            }
            if (image != null && image.color != colour)
            {
                image.color = colour;
            }
        }
    }
}
