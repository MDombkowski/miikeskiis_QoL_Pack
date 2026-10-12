// The QoL Mods list picker, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ListPicker\. Every mod that uses
// it carries a copy in its own parts\ListPicker\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;
using UnityEngine;

namespace QoLMods.ListPicker
{
    /// <summary>
    /// The game's pictures (item, piece and trophy icons, Unity sprites) drawn in the settings window's immediate-mode GUI:
    /// a sprite's part of its texture (its packed place in an atlas, with the margins a packing trimmed put back, as the
    /// pictures part's Greys reads it), fitted into a square at its own shape. A sprite that can't be drawn this way (one
    /// packed tightly, whose place isn't a rectangle) is remembered and drawn as a tile with its first letter instead, as
    /// is a thing with no picture at all (a creature with no trophy). Only on a Repaint: drawing does nothing else.
    /// </summary>
    internal static class Thumbs
    {
        private static readonly HashSet<Sprite> Broken = new HashSet<Sprite>();
        private static GUIStyle letter;
        private static Texture2D tile;

        /// <summary>Draws the picture in the square, or the letter tile; <paramref name="dim"/> greys it (not yet found).</summary>
        internal static void Draw(Rect square, Sprite sprite, string name, bool dim)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            Color colour = GUI.color;
            if (dim)
            {
                GUI.color = new Color(colour.r * 0.55f, colour.g * 0.55f, colour.b * 0.55f, colour.a * 0.75f);
            }
            try
            {
                if (sprite == null || Broken.Contains(sprite) || !DrawSprite(square, sprite))
                {
                    DrawLetter(square, name);
                }
            }
            finally
            {
                GUI.color = colour;
            }
        }

        private static bool DrawSprite(Rect square, Sprite sprite)
        {
            try
            {
                Texture2D texture = sprite.texture;
                if (texture == null)
                {
                    Broken.Add(sprite);
                    return false;
                }
                Rect whole = sprite.rect;
                Rect packed = sprite.textureRect;   // throws for a tightly packed picture: drawn as a letter from then on
                Vector2 offset = sprite.textureRectOffset;
                if (whole.width < 1f || whole.height < 1f || packed.width < 1f || packed.height < 1f)
                {
                    Broken.Add(sprite);
                    return false;
                }
                // The whole picture fitted into the square at its own shape, then its packed part at its place inside.
                float fit = Mathf.Min(square.width / whole.width, square.height / whole.height);
                float width = whole.width * fit;
                float height = whole.height * fit;
                float left = square.x + (square.width - width) / 2f;
                float top = square.y + (square.height - height) / 2f;
                // The GUI counts down from the top, a texture up from the bottom.
                var place = new Rect(left + offset.x * fit, top + (whole.height - offset.y - packed.height) * fit,
                                     packed.width * fit, packed.height * fit);
                var uv = new Rect(packed.x / texture.width, packed.y / texture.height, packed.width / texture.width,
                                  packed.height / texture.height);
                GUI.DrawTextureWithTexCoords(place, texture, uv, true);
                return true;
            }
            catch (System.Exception)
            {
                Broken.Add(sprite);
                return false;
            }
        }

        private static void DrawLetter(Rect square, string name)
        {
            if (tile == null)
            {
                tile = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                tile.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.12f));
                tile.Apply();
            }
            GUI.DrawTexture(square, tile);
            if (letter == null)
            {
                letter = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            letter.fontSize = Mathf.Max(10, Mathf.RoundToInt(square.height * 0.45f));
            string first = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
            GUI.Label(square, first, letter);
        }
    }
}
