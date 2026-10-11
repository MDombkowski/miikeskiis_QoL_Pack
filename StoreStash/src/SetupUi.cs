using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;   // StoreStash's own Button (the quick stack's) would hide it

namespace StoreStash
{
    /// <summary>
    /// Small helpers for the setup window, built from Unity's own UI parts in Valheim's style (Jötunn's GUIManager does the
    /// styling), after Add to Cart's (its src\Ui.cs, with its lessons: the wheel scrolls in rows, a button never keeps the
    /// keyboard). Every position is from the parent's top-left corner, x to the right and y downwards, in the inventory
    /// screen's units.
    /// </summary>
    internal static class SetupUi
    {
        internal static readonly Color Dim = new Color(0f, 0f, 0f, 0.45f);
        internal static readonly Color Cell = new Color(0.12f, 0.1f, 0.08f, 0.85f);
        internal static readonly Color CellTaken = new Color(0.16f, 0.32f, 0.14f, 0.92f);
        internal static readonly Color CellExcepted = new Color(0.38f, 0.1f, 0.08f, 0.92f);
        internal static readonly Color Plain = new Color(0.95f, 0.92f, 0.85f, 1f);
        internal static readonly Color Quiet = new Color(0.75f, 0.72f, 0.65f, 1f);
        internal static readonly Color Good = new Color(0.6f, 0.9f, 0.5f, 1f);
        internal static readonly Color Bad = new Color(1f, 0.45f, 0.35f, 1f);
        internal static readonly Color RowPlain = new Color(0f, 0f, 0f, 0f);
        internal static readonly Color RowHover = new Color(1f, 1f, 1f, 0.09f);
        internal static readonly Color RowPicked = new Color(0.36f, 0.49f, 0.65f, 0.95f);

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

        /// <summary>A button in the game's style. Never the keyboard's selected object (a clicked button kept it, and
        /// Enter or an arrow key pressed it again: Portion Control's review).</summary>
        internal static UIButton Button(Transform parent, string text, float x, float y, float width, float height, UnityAction onClick)
        {
            GameObject made = GUIManager.Instance.CreateButton(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, width, height);
            Place(made, x, y, width, height);
            UIButton button = made.GetComponent<UIButton>();
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>A button's label, to change its words or colour.</summary>
        internal static Text TextOf(UIButton button) => button != null ? button.GetComponentInChildren<Text>(true) : null;

        /// <summary>A one-line text box in the game's style (Jötunn's, as Add to Cart's filter box), with grey words while
        /// it's empty (<paramref name="placeholder"/>, or none). Like the buttons, never reached by the arrow keys from
        /// another control nor left by them (its navigation off): a box the keyboard only gets with a click. Its
        /// onEndEdit also comes when it's switched off with its window (Unity's InputField.OnDisable): a listener only
        /// notes what was typed (Add to Cart's lesson).</summary>
        internal static InputField Field(Transform parent, float x, float y, float width, float height, int size, string placeholder)
        {
            GameObject made = GUIManager.Instance.CreateInputField(parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                                                   InputField.ContentType.Standard, placeholder, size, width, height);
            Place(made, x, y, width, height);
            InputField field = made.GetComponent<InputField>();
            field.lineType = InputField.LineType.SingleLine;
            Navigation none = field.navigation;
            none.mode = Navigation.Mode.None;
            field.navigation = none;
            return field;
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

        /// <summary>A vertical scroll area. Returns its content, which grows downwards with its children: give it a layout
        /// group (List).</summary>
        internal static RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject view = Box(name, parent, x, y, width, height, Dim);
            view.AddComponent<RectMask2D>();
            ScrollRect scroll = view.AddComponent<ScrollRect>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(view.transform, false);
            RectTransform contentRect = Place(content, 0f, 0f, width - 12f, 0f);
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
            Navigation none = scrollbar.navigation;
            none.mode = Navigation.Mode.None;
            scrollbar.navigation = none;

            scroll.content = contentRect;
            scroll.viewport = (RectTransform)view.transform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            // The wheel is read by the window itself (Wheel), in rows: through the game's input, the scroll view's own
            // handling moved only a few pixels a notch (Add to Cart's lesson).
            scroll.scrollSensitivity = 0f;
            return contentRect;
        }

        /// <summary>Lays a scroll area's content out top to bottom.</summary>
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

        /// <summary>A row for a list inside a Scroll: full width, fixed height.</summary>
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

        /// <summary>
        /// Scrolls whichever of these scroll areas the pointer is over by the wheel's turn this frame, in rows of
        /// <paramref name="rowHeight"/>: at most one notch a frame, as the game counts them.
        /// </summary>
        internal static void Wheel(float rows, float rowHeight, params RectTransform[] contents)
        {
            float wheel = ZInput.GetMouseScrollWheel();
            if (wheel == 0f)
            {
                return;
            }
            // ZInput scales the mouse's own reading by 0.15: one notch is 0.15 or more.
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

        internal static void ScrollToTop(RectTransform content)
        {
            ScrollRect scroll = content != null ? content.GetComponentInParent<ScrollRect>() : null;
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }
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

    /// <summary>Mouse events for a cell, a mark or a row: a left click, a right-click, and hovering. On a button's own
    /// object it adds only what it's given (a right-click on a heading's button): the button keeps its left click.</summary>
    internal sealed class SetupClicks : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal Action Left;
        internal Action Right;
        internal Action Enter;
        internal Action Exit;

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && Left != null)
            {
                Module.Guard("handling a click in the setup window", Left);
            }
            else if (data.button == PointerEventData.InputButton.Right && Right != null)
            {
                Module.Guard("handling a right-click in the setup window", Right);
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

    /// <summary>On a text box: says so the moment it's selected (a click on it), before Unity's InputField takes the
    /// keyboard (in its LateUpdate) and before the window's next Update, so the typing guard holds the game's keys before
    /// the first letter can reach them, whatever order the frame's Updates run in.</summary>
    internal sealed class SetupTypingWatch : MonoBehaviour, ISelectHandler
    {
        internal Action Selected;

        public void OnSelect(BaseEventData data)
        {
            if (Selected != null)
            {
                Module.Guard("holding the game's keys while you type", Selected);
            }
        }
    }
}
