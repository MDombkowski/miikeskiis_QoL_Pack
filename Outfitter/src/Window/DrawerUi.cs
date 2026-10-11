using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// What the nearby items drawer draws with that Outfitter's own Ui doesn't have, ported from Add to Cart's window
    /// (its Ui.cs and Favorites.Star): a scroll list with a bar, which the drawer scrolls by the wheel in rows; a dropdown
    /// whose choices light up under the pointer and keep to one line; a checkbox; a chip; a row of the list on the left;
    /// and the build menu's favourite star. Positions are from the parent's top-left corner, as in Ui.
    /// </summary>
    internal static class DrawerUi
    {
        internal static readonly Color Dim = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>A row with nothing in it (his favourites, while none is near), drawn faint.</summary>
        internal static readonly Color Faint = new Color(0.75f, 0.72f, 0.65f, 0.45f);

        internal static readonly Color Gold = new Color(1f, 0.85f, 0.3f, 1f);

        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo pieceButtonPrefab;
        private static FieldInfo favoriteStar;
        private static bool starLookedUp;
        private static Sprite star;

        /// <summary>
        /// A vertical scroll area with a bar on its right. Returns its content, which grows downwards with its children:
        /// give it a layout (List). The wheel is the drawer's to handle (Wheel), in rows: through the game's input, the
        /// scroll view's own handling moved only a few pixels a notch (Add to Cart's finding). <paramref name="scroll"/>
        /// is handed back, since a search for it from the content finds nothing while the window is hidden.
        /// </summary>
        internal static RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height, out ScrollRect scroll)
        {
            GameObject view = Ui.Box(name, parent, x, y, width, height, Dim);
            view.GetComponent<Image>().raycastTarget = true;   // catches the pointer over empty space
            view.AddComponent<RectMask2D>();
            scroll = view.AddComponent<ScrollRect>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(view.transform, false);
            RectTransform contentRect = Ui.Place(content, 0f, 0f, width - 12f, 0f);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bar = new GameObject("Scrollbar", typeof(RectTransform));
            bar.transform.SetParent(view.transform, false);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 1f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(10f, 0f);
            bar.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            Scrollbar scrollbar = bar.AddComponent<Scrollbar>();
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            var area = new GameObject("Sliding Area", typeof(RectTransform));
            area.transform.SetParent(bar.transform, false);
            Stretch((RectTransform)area.transform);
            var handle = new GameObject("Handle", typeof(RectTransform));
            handle.transform.SetParent(area.transform, false);
            Stretch((RectTransform)handle.transform);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.85f, 0.6f, 0.25f, 0.9f);
            scrollbar.handleRect = (RectTransform)handle.transform;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scroll.content = contentRect;
            scroll.viewport = (RectTransform)view.transform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            scroll.scrollSensitivity = 0f;
            return contentRect;
        }

        /// <summary>Lays a scroll area's content out as a list, top to bottom.</summary>
        internal static void List(RectTransform content, float spacing)
        {
            VerticalLayoutGroup list = content.gameObject.AddComponent<VerticalLayoutGroup>();
            list.spacing = spacing;
            list.padding = new RectOffset(4, 4, 4, 4);
            list.childControlHeight = true;
            list.childControlWidth = true;
            list.childForceExpandHeight = false;
            list.childForceExpandWidth = true;
        }

        /// <summary>
        /// Scrolls whichever of these scroll areas the pointer is over by this frame's turn of the wheel
        /// (<paramref name="wheel"/>, as ZInput reads it), <paramref name="step"/> pixels a notch, at most one notch a frame.
        /// </summary>
        internal static void Wheel(float wheel, float step, ScrollRect[] scrolls)
        {
            // ZInput scales the mouse's own reading by 0.15 (its MouseScrollDelta action): one notch is 0.15 or more.
            float notches = Mathf.Clamp(wheel / 0.15f, -1f, 1f);
            Vector2 pointer = ZInput.pointerPosition;
            foreach (ScrollRect scroll in scrolls)
            {
                if (scroll == null || scroll.viewport == null || scroll.content == null ||
                    !RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, pointer, null))
                {
                    continue;
                }
                float hidden = scroll.content.rect.height - scroll.viewport.rect.height;
                if (hidden > 0f)
                {
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + notches * step / hidden);
                }
                return;
            }
        }

        /// <summary>A row of a scroll list: full width, a fixed height, laid out by the content's list.</summary>
        internal static GameObject Row(Transform content, float height)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(content, false);
            row.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        /// <summary>A row of the list on the left: a background that lights up under the pointer, and its clicks.</summary>
        internal static Clicks ListRow(Transform content, float height, out Image background)
        {
            GameObject row = Row(content, height);
            background = row.AddComponent<Image>();
            background.color = Ui.RowPlain;
            background.raycastTarget = true;
            Clicks clicks = row.AddComponent<Clicks>();
            clicks.Background = background;
            return clicks;
        }

        /// <summary>Empties a scroll list: its rows leave the layout at once and are destroyed at the end of the frame.</summary>
        internal static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        /// <summary>A picture that never catches the pointer.</summary>
        internal static Image Icon(Transform parent, Sprite sprite, float x, float y, float size)
        {
            GameObject made = Ui.Box("Icon", parent, x, y, size, size);
            Image image = made.AddComponent<Image>();
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A dropdown in the game's style whose choices light up under the pointer and keep to one line (Jötunn's own
        /// switches the choices' background off, and long names wrapped: his note on Add to Cart 0.2.0). The open list
        /// hangs from its left edge, <paramref name="listWidth"/> wide. Its navigation is off, as Ui.Button's is.
        /// </summary>
        internal static Dropdown Dropdown(Transform parent, float x, float y, float width, float height, float listWidth)
        {
            GameObject made = GUIManager.Instance.CreateDropDown(parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, 15, width, height);
            Ui.Place(made, x, y, width, height);
            Dropdown dropdown = made.GetComponent<Dropdown>();
            dropdown.navigation = new Navigation { mode = Navigation.Mode.None };
            Transform item = dropdown.template != null ? dropdown.template.Find("Viewport/Content/Item") : null;
            Toggle toggle = item != null ? item.GetComponent<Toggle>() : null;
            if (toggle != null && toggle.targetGraphic is Image background)
            {
                background.enabled = true;
                background.sprite = null;
                background.color = Color.white;
                toggle.transition = Selectable.Transition.ColorTint;
                toggle.colors = new ColorBlock
                {
                    normalColor = new Color(1f, 1f, 1f, 0f),
                    highlightedColor = new Color(1f, 1f, 1f, 0.2f),
                    pressedColor = new Color(1f, 1f, 1f, 0.3f),
                    selectedColor = new Color(1f, 1f, 1f, 0.2f),
                    disabledColor = new Color(1f, 1f, 1f, 0f),
                    colorMultiplier = 1f,
                    fadeDuration = 0.05f,
                };
            }
            if (dropdown.itemText != null)
            {
                dropdown.itemText.horizontalOverflow = HorizontalWrapMode.Overflow;
                dropdown.itemText.verticalOverflow = VerticalWrapMode.Overflow;
            }
            if (dropdown.captionText != null)
            {
                // A long choice ("Auto (Equipment Slot)") shrinks to fit the closed dropdown rather than wrapping.
                dropdown.captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
                dropdown.captionText.verticalOverflow = VerticalWrapMode.Truncate;
                dropdown.captionText.resizeTextForBestFit = true;
                dropdown.captionText.resizeTextMinSize = 10;
                dropdown.captionText.resizeTextMaxSize = 15;
            }
            if (dropdown.template != null)
            {
                RectTransform template = dropdown.template;
                template.anchorMin = template.anchorMax = new Vector2(0f, 0f);
                template.pivot = new Vector2(0f, 1f);
                template.anchoredPosition = new Vector2(0f, 2f);
                // Room for ten choices (Sort has nine on food); Unity shrinks it to fit fewer.
                template.sizeDelta = new Vector2(Mathf.Max(width, listWidth), 212f);
            }
            return dropdown;
        }

        /// <summary>Sets a dropdown's choices, only if they changed (so a list he has open isn't pulled from under him),
        /// and its value, without calling its handler.</summary>
        internal static void SetChoices(Dropdown dropdown, List<string> shown, List<string> labels, int value)
        {
            if (!labels.SequenceEqual(shown))
            {
                shown.Clear();
                shown.AddRange(labels);
                dropdown.ClearOptions();
                dropdown.AddOptions(labels);
            }
            dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, Mathf.Max(0, labels.Count - 1)));
            dropdown.RefreshShownValue();
        }

        /// <summary>Whether a dropdown's list is open: Unity shows it as a child named "Dropdown List" while open (and
        /// for its fade as it closes).</summary>
        internal static bool Expanded(Dropdown dropdown)
        {
            Transform list = dropdown != null ? dropdown.transform.Find("Dropdown List") : null;
            return list != null && list.gameObject.activeInHierarchy;
        }

        /// <summary>A checkbox in the game's style, <paramref name="size"/> square, its navigation off.</summary>
        internal static Toggle Checkbox(Transform parent, float x, float y, float size)
        {
            GameObject made = GUIManager.Instance.CreateToggle(parent, size, size);
            // Jötunn parents it keeping its place in the world, which scales it by one over the GUI's scale: anywhere but
            // 1920×1080 at 100% it came out the wrong size (Add to Cart, review 1). Its own size, rotation and depth, then.
            made.transform.localScale = Vector3.one;
            made.transform.localRotation = Quaternion.identity;
            Ui.Place(made, x, y, size, size);
            Vector3 at = made.transform.localPosition;
            made.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Text label = made.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }
            Toggle toggle = made.GetComponent<Toggle>();
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            return toggle;
        }

        /// <summary>A small button for the line above the results ("Show All ×").</summary>
        internal static Button Chip(Transform parent, float x, float y, float width, float height, UnityAction onClick)
        {
            Button chip = Ui.Button(parent, string.Empty, x, y, width, height, onClick);
            Text text = chip.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 13;
                text.resizeTextForBestFit = false;
                text.supportRichText = false;   // it can show what he typed
            }
            return chip;
        }

        internal static void SetChip(Button chip, string text) => Ui.SetButtonText(chip, text);

        /// <summary>The build menu's own favourite star, or null (the drawer then draws a "*"). Looked up once; nothing
        /// here can switch the mod off, since the star only decorates.</summary>
        internal static Sprite Star()
        {
            if (star != null || Hud.instance == null || Hud.instance.m_buildUi == null)
            {
                return star;
            }
            try
            {
                if (!starLookedUp)
                {
                    starLookedUp = true;
                    pieceButtonPrefab = typeof(BuildUi).GetField("m_pieceButtonPrefab", AnyInstance);
                    favoriteStar = typeof(BuildUiPieceButton).GetField("m_favoriteStar", AnyInstance);
                }
                if (pieceButtonPrefab == null || favoriteStar == null)
                {
                    return null;
                }
                var prefab = pieceButtonPrefab.GetValue(Hud.instance.m_buildUi) as GameObject;
                BuildUiPieceButton button = prefab != null ? prefab.GetComponent<BuildUiPieceButton>() : null;
                var image = button != null ? favoriteStar.GetValue(button) as Image : null;
                star = image != null ? image.sprite : null;
            }
            catch (Exception)
            {
                star = null;
            }
            return star;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
