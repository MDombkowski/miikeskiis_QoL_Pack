using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AddToCart
{
    /// <summary>
    /// Small helpers for the window, built from Unity's own UI parts in Valheim's style (Jötunn's GUIManager does the
    /// styling). Every position is from the parent's top-left corner, x to the right and y downwards, in pixels.
    /// </summary>
    internal static class Ui
    {
        internal static readonly Color Dim = new Color(0f, 0f, 0f, 0.45f);
        internal static readonly Color Cell = new Color(0.12f, 0.1f, 0.08f, 0.85f);
        internal static readonly Color CellInCart = new Color(0.35f, 0.25f, 0.08f, 0.95f);

        internal static readonly Color Bad = new Color(1f, 0.45f, 0.35f, 1f);
        internal static readonly Color Good = new Color(0.6f, 0.9f, 0.5f, 1f);
        internal static readonly Color Plain = new Color(0.95f, 0.92f, 0.85f, 1f);
        internal static readonly Color Quiet = new Color(0.75f, 0.72f, 0.65f, 1f);
        internal static readonly Color Comfort = new Color(0.55f, 0.85f, 1f, 1f);
        internal static readonly Color Gold = new Color(1f, 0.85f, 0.3f, 1f);

        /// <summary>A row of the list on the left: nothing, under the pointer, and picked (the mock-up's steel blue).</summary>
        internal static readonly Color RowPlain = new Color(0f, 0f, 0f, 0f);
        internal static readonly Color RowHover = new Color(1f, 1f, 1f, 0.09f);
        internal static readonly Color RowPicked = new Color(0.36f, 0.49f, 0.65f, 0.95f);

        /// <summary>A row with nothing in it on this tab (one of his lists, kept in place), drawn faint.</summary>
        internal static readonly Color Faint = new Color(0.75f, 0.72f, 0.65f, 0.45f);

        internal static Color Orange => GUIManager.Instance.ValheimOrange;

        /// <summary>Places a rect from its parent's top-left corner.</summary>
        internal static RectTransform Place(GameObject thing, float x, float y, float width, float height)
        {
            var rect = (RectTransform)thing.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>A plain rectangle, with a colour if one is given.</summary>
        internal static GameObject Box(string name, Transform parent, float x, float y, float width, float height, Color? color = null)
        {
            var box = new GameObject(name, typeof(RectTransform));
            box.transform.SetParent(parent, false);
            Place(box, x, y, width, height);
            if (color.HasValue)
            {
                box.AddComponent<Image>().color = color.Value;
            }
            return box;
        }

        internal static Text Label(Transform parent, string text, float x, float y, float width, float height, int size = 16,
                                   TextAnchor align = TextAnchor.MiddleLeft, Color? color = null, bool bold = false)
        {
            GUIManager gui = GUIManager.Instance;
            GameObject made = gui.CreateText(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                             bold ? gui.AveriaSerifBold : gui.AveriaSerif, size, color ?? Plain, true, Color.black, width, height, false);
            Place(made, x, y, width, height);
            Text label = made.GetComponent<Text>();
            label.alignment = align;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        internal static Button Button(Transform parent, string text, float x, float y, float width, float height, UnityAction onClick)
        {
            GameObject made = GUIManager.Instance.CreateButton(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, width, height);
            Place(made, x, y, width, height);
            Button button = made.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>
        /// A dropdown in the game's style whose items light up under the pointer and keep to one line: Jötunn's own
        /// switches the items' background off, so nothing showed under the pointer, and long names wrapped (his note on
        /// 0.2.0's group list). The open list hangs from the dropdown's left edge, <paramref name="listWidth"/> wide.
        /// </summary>
        internal static Dropdown Dropdown(Transform parent, float x, float y, float width, float height, float listWidth)
        {
            GameObject made = GUIManager.Instance.CreateDropDown(parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, 15, width, height);
            Place(made, x, y, width, height);
            Dropdown dropdown = made.GetComponent<Dropdown>();
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
                // Room for ten choices (Sort can have nine on food); Unity shrinks it to fit fewer.
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

        /// <summary>A checkbox in the game's style, <paramref name="size"/> square.</summary>
        internal static Toggle Checkbox(Transform parent, float x, float y, float size)
        {
            GameObject made = GUIManager.Instance.CreateToggle(parent, size, size);
            // Jötunn parents it keeping its place in the world, which scales it by one over the GUI's scale: anywhere but
            // 1920×1080 at 100% it came out the wrong size (review 1). Its own size, rotation and depth, then.
            made.transform.localScale = Vector3.one;
            made.transform.localRotation = Quaternion.identity;
            Place(made, x, y, size, size);
            Vector3 at = made.transform.localPosition;
            made.transform.localPosition = new Vector3(at.x, at.y, 0f);
            Text label = made.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }
            return made.GetComponent<Toggle>();
        }

        /// <summary>A small button for the line above the results ("Show All ×").</summary>
        internal static Button Chip(Transform parent, float x, float y, float width, float height, UnityAction onClick)
        {
            Button chip = Button(parent, string.Empty, x, y, width, height, onClick);
            Text text = chip.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 13;
                text.resizeTextForBestFit = false;
            }
            return chip;
        }

        /// <summary>A row of the list on the left: a background that lights up under the pointer, and clicks.</summary>
        internal static Clicks ListRow(Transform content, float height, out Image background)
        {
            GameObject row = Row(content, height, RowPlain);
            background = row.GetComponent<Image>();
            background.raycastTarget = true;
            return row.AddComponent<Clicks>();
        }

        internal static Image Icon(Transform parent, Sprite sprite, float x, float y, float size)
        {
            GameObject made = Box("Icon", parent, x, y, size, size);
            Image image = made.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A thumbnail's picture, which turns grey and fades when the chests can't cover one more (Thumbnail.Show).</summary>
        internal static Thumbnail Thumbnail(Transform parent, Sprite sprite, float x, float y, float size) =>
            new Thumbnail(Icon(parent, sprite, x, y, size), sprite);

        /// <summary>
        /// A vertical scroll area. Returns its content, which grows downwards with its children: give it a layout group.
        /// </summary>
        internal static RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject view = Box(name, parent, x, y, width, height, Dim);
            view.AddComponent<RectMask2D>();
            ScrollRect scroll = view.AddComponent<ScrollRect>();

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(view.transform, false);
            RectTransform contentRect = Place(content, 0f, 0f, width - 12f, 0f);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject bar = new GameObject("Scrollbar", typeof(RectTransform));
            bar.transform.SetParent(view.transform, false);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 1f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(10f, 0f);
            bar.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            Scrollbar scrollbar = bar.AddComponent<Scrollbar>();

            GameObject area = new GameObject("Sliding Area", typeof(RectTransform));
            area.transform.SetParent(bar.transform, false);
            Stretch((RectTransform)area.transform);
            GameObject handle = new GameObject("Handle", typeof(RectTransform));
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
            // The wheel is handled by the window itself (Wheel), in rows: through the game's input, the scroll view's
            // own handling moved only a few pixels a notch.
            scroll.scrollSensitivity = 0f;
            return contentRect;
        }

        /// <summary>
        /// Scrolls whichever of these scroll views the pointer is over by the wheel's turn this frame, in rows of
        /// <paramref name="rowHeight"/> pixels: at most one notch a frame, as the game counts them.
        /// </summary>
        internal static void Wheel(float rows, float rowHeight, params RectTransform[] contents)
        {
            float wheel = ZInput.GetMouseScrollWheel();
            if (wheel == 0f)
            {
                return;
            }
            // ZInput scales the mouse's own reading by 0.15 (ZInput.cs, the MouseScrollDelta action): one notch is 0.15
            // or more.
            float notches = Mathf.Clamp(wheel / 0.15f, -1f, 1f);
            Vector2 pointer = ZInput.pointerPosition;
            foreach (RectTransform content in contents)
            {
                ScrollRect scroll = content != null ? content.GetComponentInParent<ScrollRect>() : null;
                if (scroll == null || !RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, pointer, null))
                {
                    continue;
                }
                float hidden = content.rect.height - scroll.viewport.rect.height;
                if (hidden > 0f)
                {
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + notches * rows * rowHeight / hidden);
                }
                return;
            }
        }

        /// <summary>The input field that has the keyboard, if any: the window's keys stay quiet while he types.</summary>
        internal static bool Typing()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            InputField field = selected != null ? selected.GetComponent<InputField>() : null;
            return field != null && field.isFocused;
        }

        internal static bool Shift() => ZInput.GetKey(KeyCode.LeftShift, false) || ZInput.GetKey(KeyCode.RightShift, false);

        internal static bool Ctrl() => ZInput.GetKey(KeyCode.LeftControl, false) || ZInput.GetKey(KeyCode.RightControl, false);

        internal static bool Alt() => ZInput.GetKey(KeyCode.LeftAlt, false) || ZInput.GetKey(KeyCode.RightAlt, false);

        /// <summary>A row for a list inside a Scroll: full width, fixed height, laid out by the content's VerticalLayoutGroup.</summary>
        internal static GameObject Row(Transform content, float height, Color? color = null)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(content, false);
            row.AddComponent<LayoutElement>().preferredHeight = height;
            if (color.HasValue)
            {
                row.AddComponent<Image>().color = color.Value;
            }
            return row;
        }

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

        internal static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);   // gone from the layout this frame; destroyed at the end of it
                UnityEngine.Object.Destroy(child);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>A thumbnail's picture, in colour or as its grey copy (Greys).</summary>
    internal sealed class Thumbnail
    {
        internal readonly Image Icon;
        private readonly Sprite colour;

        internal Thumbnail(Image icon, Sprite colour)
        {
            Icon = icon;
            this.colour = colour;
        }

        /// <summary>
        /// Draws it as available, or as the settings say: grey (its own grey copy, made a few a frame, so for a moment it
        /// may still be in colour) and faded to "Opacity of thumbnails the chests can't cover". One whose grey copy isn't
        /// there shows in colour, faded, so it never looks available. Returns true while it waits for its grey copy.
        /// </summary>
        internal bool Show(bool unavailable)
        {
            UnavailableLook look = Settings.Unavailable.Value;
            bool wantsGrey = unavailable && look != UnavailableLook.Faded;
            Sprite greyCopy = wantsGrey ? Greys.Of(colour) : null;
            Looks.Unavailable(unavailable, look, Settings.Opacity.Value, greyCopy != null, out bool grey, out float alpha);
            Sprite shown = grey ? greyCopy : colour;
            if (Icon.sprite != shown)
            {
                Icon.sprite = shown;
            }
            Icon.color = new Color(1f, 1f, 1f, alpha);
            return wantsGrey && greyCopy == null && Greys.CanGrey(colour);
        }
    }

    /// <summary>Mouse events for a cell or a row: left, right and middle clicks, and hovering.</summary>
    internal sealed class Clicks : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal Action Left;
        internal Action Right;
        internal Action Middle;
        internal Action Enter;
        internal Action Exit;

        public void OnPointerClick(PointerEventData data)
        {
            Action act = data.button == PointerEventData.InputButton.Right ? Right
                : data.button == PointerEventData.InputButton.Middle ? Middle
                : data.button == PointerEventData.InputButton.Left ? Left
                : null;
            if (act != null)
            {
                Module.Guard("handling a click in the cart window", act);
            }
        }

        public void OnPointerEnter(PointerEventData data)
        {
            if (Enter != null)
            {
                Module.Guard("showing what's under the pointer", Enter);
            }
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (Exit != null)
            {
                Module.Guard("showing what's under the pointer", Exit);
            }
        }
    }
}
