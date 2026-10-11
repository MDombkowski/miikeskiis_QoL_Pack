using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valheim.UI;

namespace PowerWheel
{
    /// <summary>
    /// What the mod adds to the look of the game's wheel: locked powers dimmed with a padlock, the names under the icons
    /// ("Names" at Always), and the description beside the wheel ("Show what it does"). Each is a nicety with a guard of
    /// its own (MODULES.md rule 6): its first error loses only that nicety for the session, with one log line, and the
    /// wheel works on. Texts are made from code as the game's own are (made inactive, the game's font set before they
    /// wake, isOrthographic on), from the font of the wheel's centre title (archive/LESSONS_LEARNED-2026-09, "TextMeshPro
    /// text made from code measures at a tenth until it wakes").
    /// </summary>
    internal static class Looks
    {
        /// <summary>How much of a locked power's icon shows.</summary>
        private const float LockedAlpha = 0.35f;

        /// <summary>The names' size against the centre title's.</summary>
        private const float LabelShare = 0.6f;

        /// <summary>The description's text size against the centre title's.</summary>
        private const float DescriptionShare = 0.5f;

        private static readonly Color DescriptionBackground = new Color(0f, 0f, 0f, 0.8f);

        private static bool dressLost;
        private static bool labelsLost;
        private static bool describeLost;

        private static Sprite padlock;
        private static RadialBase fontOf;
        private static TextMeshProUGUI title;

        private static GameObject panel;
        private static TextMeshProUGUI panelText;
        private static Slot describing;

        /// <summary>Before the wheel opens: each locked power's icon dimmed, with a padlock at its corner.</summary>
        internal static void Dress(List<Slot> slots)
        {
            if (dressLost)
            {
                return;
            }
            try
            {
                foreach (Slot slot in slots)
                {
                    if (slot.Hidden && slot.Element != null && slot.Element.Icon != null)
                    {
                        slot.Element.Icon.preserveAspect = true;   // the padlock is its icon, kept its own shape (card B06)
                    }
                    else if (slot.Locked && slot.Element != null && slot.Element.Icon != null)
                    {
                        Lock(slot.Element.Icon);
                    }
                }
            }
            catch (Exception error)
            {
                dressLost = true;
                Module.Log.LogWarning($"Locked powers can't be dimmed or marked with a padlock ({error.Message}); they still can't be " +
                                      $"picked, and the wheel says \"{PowerRules.LockedSubtitle}\" under their names.\n{error}");
            }
        }

        /// <summary>Once the wheel shows, with "Names" at Always: each power's name under its icon.</summary>
        internal static void Label(RadialBase radial, List<Slot> slots)
        {
            if (labelsLost || Settings.Names.Value != NamesChoice.Always)
            {
                return;
            }
            try
            {
                foreach (Slot slot in slots)
                {
                    if (slot.Power != null && !slot.Hidden && slot.Element != null && slot.Element.Icon != null)
                    {
                        NameUnder(radial, slot.Element.Icon, slot.ShownName);
                    }
                }
            }
            catch (Exception error)
            {
                labelsLost = true;
                Module.Log.LogWarning($"The names can't be shown under the icons ({error.Message}); the name of the power under the " +
                                      $"pointer still shows in the middle of the wheel.\n{error}");
            }
        }

        /// <summary>Every frame the wheel shows, with "Show what it does" on: what the power under the pointer does,
        /// beside the wheel. Hidden for an empty slot, with the setting off, and once the wheel is gone.</summary>
        internal static void Describe(RadialBase radial, Slot slot)
        {
            if (describeLost || !Settings.ShowWhatItDoes.Value || slot == null || slot.Effect == null || slot.Hidden)
            {
                Hide();
                return;
            }
            try
            {
                Describing(radial, slot);
            }
            catch (Exception error)
            {
                describeLost = true;
                Hide();
                Module.Log.LogWarning($"What a power does can't be shown beside the wheel ({error.Message}); the wheel works on " +
                                      $"without it.\n{error}");
            }
        }

        /// <summary>The description beside the wheel hidden (the wheel closed, or the mod switched off).</summary>
        internal static void Hide()
        {
            describing = null;
            if (panel != null && panel.activeSelf)
            {
                panel.SetActive(false);
            }
        }

        private static void Lock(Image icon)
        {
            CanvasGroup dim = icon.GetComponent<CanvasGroup>();
            if (dim == null)
            {
                dim = icon.gameObject.AddComponent<CanvasGroup>();
            }
            dim.alpha = LockedAlpha;   // the game's selection colour goes on the icon's own colour, under this

            RectTransform iconRect = icon.rectTransform;
            float side = Mathf.Max(8f, iconRect.rect.height * iconRect.localScale.y * 0.5f);
            var go = new GameObject("PowerWheel_Padlock", typeof(RectTransform));
            go.SetActive(false);
            var rect = (RectTransform)go.transform;
            rect.SetParent(iconRect.parent, false);
            rect.sizeDelta = new Vector2(side * Padlock.Width / Padlock.Height, side);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Vector3 corner = Corner(iconRect, 1f, 0f);
            rect.localPosition = corner + new Vector3(-side * 0.3f, side * 0.35f, 0f);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            Image picture = go.AddComponent<Image>();
            picture.sprite = PadlockSprite();
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            go.SetActive(true);
        }

        private static void NameUnder(RadialBase radial, Image icon, string name)
        {
            TextMeshProUGUI source = TitleFor(radial);
            if (source == null)
            {
                return;
            }
            RectTransform iconRect = icon.rectTransform;
            Transform parent = iconRect.parent;
            float parentScale = parent.lossyScale.y;
            if (!(parentScale > 0f))
            {
                return;
            }
            float size = source.fontSize * source.transform.lossyScale.y / parentScale * LabelShare;
            float width = Mathf.Max(iconRect.rect.width * iconRect.localScale.x * 3f, size * 8f);

            var go = new GameObject("PowerWheel_Name", typeof(RectTransform));
            go.SetActive(false);
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, size * 1.5f);
            rect.localPosition = Corner(iconRect, 0.5f, 0f) + new Vector3(0f, -size * 0.15f, 0f);
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            TextMeshProUGUI text = NewText(go, source, size);
            text.alignment = TextAlignmentOptions.Top;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.text = name;
            go.SetActive(true);
        }

        private static void Describing(RadialBase radial, Slot slot)
        {
            TextMeshProUGUI source = TitleFor(radial);
            if (source == null)
            {
                Hide();
                return;
            }
            float scale = radial.transform.lossyScale.x;
            if (!(scale > 0f))
            {
                Hide();
                return;
            }
            float size = source.fontSize * source.transform.lossyScale.y / scale * DescriptionShare;
            float pad = size * 0.6f;
            float iconDistance = Wheel.IconDistance > 0f ? Wheel.IconDistance : 200f * scale;
            float width = Mathf.Max(iconDistance * 1.3f / scale, size * 16f);

            if (panel == null || panelText == null || panel.transform.parent != radial.transform)
            {
                if (panel != null)
                {
                    UnityEngine.Object.Destroy(panel);
                }
                panel = new GameObject("PowerWheel_Description", typeof(RectTransform));
                panel.SetActive(false);
                var frame = (RectTransform)panel.transform;
                frame.SetParent(radial.transform, false);
                frame.pivot = new Vector2(0f, 0.5f);
                panel.AddComponent<LayoutElement>().ignoreLayout = true;
                Image back = panel.AddComponent<Image>();
                back.color = DescriptionBackground;
                back.raycastTarget = false;

                var textObject = new GameObject("PowerWheel_DescriptionText", typeof(RectTransform));
                textObject.SetActive(false);
                var textRect = (RectTransform)textObject.transform;
                textRect.SetParent(frame, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                panelText = NewText(textObject, source, size);
                panelText.alignment = TextAlignmentOptions.TopLeft;
                panelText.textWrappingMode = TextWrappingModes.Normal;
                panelText.overflowMode = TextOverflowModes.Overflow;
                textObject.SetActive(true);
                describing = null;
            }

            var rect = (RectTransform)panel.transform;
            if (!panel.activeSelf)
            {
                panel.SetActive(true);   // shown before measuring: TextMeshPro measures a sleeping text at a tenth
            }
            if (!ReferenceEquals(describing, slot))
            {
                describing = slot;
                string does = Localization.instance.Localize(slot.Effect.GetTooltipString() ?? string.Empty).Trim();
                panelText.fontSize = size;
                panelText.text = $"<color=orange>{slot.ShownName}</color>" + (does.Length > 0 ? "\n" + does : string.Empty);
                var textRect = (RectTransform)panelText.transform;
                textRect.offsetMin = new Vector2(pad, pad);
                textRect.offsetMax = new Vector2(-pad, -pad);
                float height = panelText.GetPreferredValues(panelText.text, width - 2f * pad, 0f).y + 2f * pad;
                rect.sizeDelta = new Vector2(width, height);
            }
            // Beside the ring, right of the wheel's centre, on screen (the game's wheel is drawn over the whole screen).
            Vector2 centre = radial.InfoPosition;
            rect.position = new Vector3(centre.x + iconDistance * 1.45f, centre.y, rect.position.z);
        }

        // The font, size and look of the wheel's centre title: the largest text of the game's centre (ElementInfo).
        private static TextMeshProUGUI TitleFor(RadialBase radial)
        {
            if (ReferenceEquals(fontOf, radial) && title != null)
            {
                return title;
            }
            fontOf = radial;
            title = null;
            ElementInfo info = radial.GetComponentInChildren<ElementInfo>(true);
            Component from = info != null ? (Component)info : radial;
            foreach (TextMeshProUGUI text in from.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text != null && text.font != null && (title == null || text.fontSize > title.fontSize))
                {
                    title = text;
                }
            }
            return title;
        }

        private static TextMeshProUGUI NewText(GameObject go, TextMeshProUGUI source, float size)
        {
            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
            text.fontStyle = source.fontStyle;
            Color color = source.color;
            color.a = 1f;
            text.color = color;
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.richText = true;
            text.raycastTarget = false;
            text.isOrthographic = true;
            return text;
        }

        /// <summary>A point of a rect in its parent's space: (0, 0) its bottom left, (1, 1) its top right.</summary>
        private static Vector3 Corner(RectTransform rect, float across, float up)
        {
            Rect area = rect.rect;
            Vector3 scale = rect.localScale;
            return rect.localPosition + new Vector3((area.xMin + area.width * across) * scale.x, (area.yMin + area.height * up) * scale.y, 0f);
        }

        /// <summary>The padlock as a power's whole icon (card B06), drawn four times larger than the corner one so it
        /// stays sharp at an icon's size.</summary>
        internal static Sprite PadlockIcon()
        {
            if (padlockIcon != null)
            {
                return padlockIcon;
            }
            padlockIcon = Draw(Padlock.Mask(IconScale), Padlock.Width * IconScale, Padlock.Height * IconScale, "PowerWheel_PadlockIcon");
            return padlockIcon;
        }

        /// <summary>The padlock icon, or none if it can't be drawn (the element then shows no icon at all, never the boss's):
        /// a nicety's own guard, so a failure here loses only the padlock, for the session.</summary>
        internal static Sprite PadlockIconOrNone()
        {
            if (padlockIconLost)
            {
                return null;
            }
            try
            {
                return PadlockIcon();
            }
            catch (Exception error)
            {
                padlockIconLost = true;
                Module.Log.LogWarning($"The padlock can't be drawn ({error.Message}); a locked power shows an empty slot instead.\n{error}");
                return null;
            }
        }

        private static bool padlockIconLost;

        private const int IconScale = 4;

        private static Sprite padlockIcon;

        private static Sprite PadlockSprite()
        {
            if (padlock != null)
            {
                return padlock;
            }
            padlock = Draw(Padlock.Mask(), Padlock.Width, Padlock.Height, "PowerWheel_Padlock");
            return padlock;
        }

        private static Sprite Draw(byte[] mask, int width, int height, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var light = new Color32(235, 235, 235, 255);
            var dark = new Color32(20, 20, 20, 240);
            var clear = new Color32(0, 0, 0, 0);
            var pixels = new Color32[mask.Length];
            for (int i = 0; i < mask.Length; i++)
            {
                pixels[i] = mask[i] == Padlock.Light ? light : mask[i] == Padlock.Dark ? dark : clear;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
