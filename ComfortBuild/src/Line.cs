using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ComfortBuild
{
    /// <summary>
    /// The comfort line's own box, beside the build panel at the bottom of the screen (the game's SelectedInfo: the
    /// piece's picture, name, description and materials), on the side the setting "Comfort line" says. The game's
    /// description box is a fixed 330 × 61 that shrinks its text to fit, so a line added to it would shrink the game's
    /// own words; above the panel, while building, sit the stamina and eitr bars (Hud.UpdateStamina moves them there),
    /// and above those the build menu. Right of the panel only the chat window comes, while it shows; left of it,
    /// EquipmentAndQuickSlots' quick slots sit beside the Forsaken power in his game (review, 2026-09-30), so right is
    /// the default. The box has its own text, in the description's font and on the panel's own background, set only
    /// when the line changes, from its foot up (so its first words stay clear of the build menu at a larger GUI scale).
    /// Being the panel's child, it shows and hides with it, and scales with it.
    /// </summary>
    internal static class Line
    {
        private const string StripName = "ComfortBuild_Line";

        /// <summary>The box's width, in the panel's own units (the panel is 410 wide and 162.65 high).</summary>
        private const float Width = 200f;

        private static GameObject strip;
        private static TextMeshProUGUI text;
        private static string shown;
        private static LineSide placed = LineSide.Off;

        /// <summary>Shows this line beside the panel, or hides the box for null (or with the setting Off).</summary>
        internal static void Show(Hud hud, string line)
        {
            LineSide side = Settings.Line.Value;
            if (line == null || side == LineSide.Off)
            {
                Hide();
                return;
            }
            if (strip == null)
            {
                Make(hud);
                if (strip == null)
                {
                    return;
                }
            }
            if (side != placed)
            {
                Place(side);
            }
            if (line != shown)
            {
                shown = line;
                text.text = line;
            }
            if (!strip.activeSelf)
            {
                strip.SetActive(true);
            }
        }

        internal static void Hide()
        {
            if (strip != null && strip.activeSelf)
            {
                strip.SetActive(false);
            }
            shown = null;
        }

        /// <summary>A new world's HUD: the old strip went with the old one.</summary>
        internal static void Forget()
        {
            strip = null;
            text = null;
            shown = null;
            placed = LineSide.Off;
        }

        // Beside the panel: anchored to its right edge (pivot left) or its left edge (pivot right), its full height.
        private static void Place(LineSide side)
        {
            placed = side;
            var rect = (RectTransform)strip.transform;
            bool right = side == LineSide.Right;
            rect.anchorMin = new Vector2(right ? 1f : 0f, 0f);
            rect.anchorMax = new Vector2(right ? 1f : 0f, 1f);
            rect.pivot = new Vector2(right ? 0f : 1f, 0.5f);
            rect.sizeDelta = new Vector2(Width, 0f);
            rect.anchoredPosition = new Vector2(right ? 6f : -6f, 0f);
        }

        private static void Make(Hud hud)
        {
            TMP_Text description = hud != null ? hud.m_pieceDescription : null;
            Transform piece = description != null ? description.transform.parent : null;
            Transform panel = piece != null ? piece.parent : null;
            if (panel == null)
            {
                return;
            }
            Transform old = panel.Find(StripName);
            if (old != null)
            {
                Object.Destroy(old.gameObject);
            }
            var go = new GameObject(StripName, typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(panel, false);

            // The panel's own background, if it has one to copy.
            Image back = go.AddComponent<Image>();
            back.raycastTarget = false;
            Transform panelBack = panel.Find("Bkg2");
            Image source = panelBack != null ? panelBack.GetComponent<Image>() : null;
            if (source != null && source.sprite != null)
            {
                back.sprite = source.sprite;
                back.type = source.type;
                back.color = source.color;
            }
            else
            {
                back.color = new Color(0f, 0f, 0f, 0.6f);
            }

            var textObject = new GameObject("Text", typeof(RectTransform));
            textObject.transform.SetParent(go.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);
            // Made under the strip while it is inactive, its font set before it wakes (LESSONS_LEARNED: TextMeshPro's
            // own default font isn't in the game).
            text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = description.font;
            text.fontSharedMaterial = description.fontSharedMaterial;
            text.color = description.color;
            text.fontSize = 15f;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10f;
            text.fontSizeMax = 15f;
            text.alignment = TextAlignmentOptions.BottomLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = true;
            text.raycastTarget = false;
            strip = go;
            shown = null;
            placed = LineSide.Off;
        }
    }
}
