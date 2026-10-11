using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpareKeys
{
    /// <summary>
    /// Small helpers for the window and the panels, built from Unity's own UI parts in Valheim's style (Jötunn's
    /// GUIManager does the styling), as the pack's other windows are (Add to Cart's, Outfitter's). Every position is from
    /// the parent's top-left corner, x to the right and y downwards, in pixels at the window's own scale.
    /// </summary>
    internal static class Ui
    {
        internal static readonly Color Dim = new Color(0f, 0f, 0f, 0.45f);
        internal static readonly Color Plain = new Color(0.95f, 0.92f, 0.85f, 1f);
        internal static readonly Color Quiet = new Color(0.75f, 0.72f, 0.65f, 1f);
        internal static readonly Color RowPlain = new Color(0f, 0f, 0f, 0f);
        internal static readonly Color RowHover = new Color(1f, 1f, 1f, 0.09f);
        internal static readonly Color RowPicked = new Color(0.36f, 0.49f, 0.65f, 0.95f);

        internal static Color Orange => GUIManager.Instance.ValheimOrange;

        internal static RectTransform Place(GameObject thing, float x, float y, float width, float height)
        {
            var rect = (RectTransform)thing.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static GameObject Box(string name, Transform parent, float x, float y, float width, float height, Color? color = null)
        {
            var box = new GameObject(name, typeof(RectTransform));
            box.transform.SetParent(parent, false);
            Place(box, x, y, width, height);
            if (color.HasValue)
            {
                Image image = box.AddComponent<Image>();
                image.color = color.Value;
                image.raycastTarget = false;
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

        internal static Button Button(Transform parent, string text, float x, float y, float width, float height, Action onClick)
        {
            GameObject made = GUIManager.Instance.CreateButton(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, width, height);
            Place(made, x, y, width, height);
            Button button = made.GetComponent<Button>();
            button.onClick.AddListener(() => Module.Guard("handling a click in the key map", onClick));
            // Not left selected after a click, so Enter or an arrow key can't press it again (LESSONS_LEARNED, archive).
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        /// <summary>A vertical scroll area; returns its content (laid out as a list, growing downwards).</summary>
        internal static RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject view = Box(name, parent, x, y, width, height, Dim);
            view.GetComponent<Image>().raycastTarget = true;   // the wheel and hover reach the rows through it
            view.AddComponent<RectMask2D>();
            ScrollRect scroll = view.AddComponent<ScrollRect>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(view.transform, false);
            RectTransform contentRect = Place(content, 0f, 0f, width - 12f, 0f);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VerticalLayoutGroup list = content.AddComponent<VerticalLayoutGroup>();
            list.spacing = 1f;
            list.padding = new RectOffset(4, 4, 4, 4);
            list.childControlHeight = true;
            list.childControlWidth = true;
            list.childForceExpandHeight = false;
            list.childForceExpandWidth = true;

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

            scroll.content = contentRect;
            scroll.viewport = (RectTransform)view.transform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            // The wheel is handled by the window itself (Wheel), in rows: under the game's input the scroll view's own
            // wheel moves a few pixels a notch (LESSONS_LEARNED, archive 2026-09).
            scroll.scrollSensitivity = 0f;
            return contentRect;
        }

        /// <summary>Scrolls whichever of these scroll views the pointer is over by the wheel's turn this frame, in rows.</summary>
        internal static void Wheel(float rowHeight, params RectTransform[] contents)
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
                if (scroll == null || !content.gameObject.activeInHierarchy ||
                    !RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, pointer, null))
                {
                    continue;
                }
                float hidden = content.rect.height - scroll.viewport.rect.height;
                if (hidden > 0f)
                {
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + notches * 3f * rowHeight / hidden);
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

        /// <summary>A row of a list inside a Scroll: full width, fixed height.</summary>
        internal static GameObject Row(Transform content, float height, Color? color = null)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(content, false);
            row.AddComponent<LayoutElement>().preferredHeight = height;
            if (color.HasValue)
            {
                Image image = row.AddComponent<Image>();
                image.color = color.Value;
                image.raycastTarget = false;
            }
            return row;
        }

        /// <summary>A row of the list on the left: a background that lights up under the pointer, and clicks.</summary>
        internal static Clicks ListRow(Transform content, float height, out Image background)
        {
            GameObject row = Row(content, height, RowPlain);
            background = row.GetComponent<Image>();
            background.raycastTarget = true;
            return row.AddComponent<Clicks>();
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

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// A root canvas of its own over the game's GUI (Add to Cart's worked example: a nested canvas can't be drawn at a
        /// scale of its own, LESSONS_LEARNED archive 2026-09), in the scene the game's GUI is in, so it goes with it.
        /// <paramref name="clickable"/>: false for the panels, which must never take a click (no raycaster at all).
        /// </summary>
        internal static Canvas RootCanvas(string name, int order, bool clickable)
        {
            if (GUIManager.CustomGUIFront == null)
            {
                return null;
            }
            var holder = clickable
                ? new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster))
                : new GameObject(name, typeof(RectTransform), typeof(Canvas));
            holder.layer = GUIManager.CustomGUIFront.layer;
            if (holder.scene != GUIManager.CustomGUIFront.scene)
            {
                SceneManager.MoveGameObjectToScene(holder, GUIManager.CustomGUIFront.scene);
            }
            Canvas canvas = holder.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            Canvas game = GameCanvas();
            Canvas front = GUIManager.CustomGUIFront.GetComponent<Canvas>();
            if (front != null)
            {
                canvas.additionalShaderChannels = front.additionalShaderChannels;
            }
            if (game != null)
            {
                canvas.pixelPerfect = game.pixelPerfect;
                canvas.referencePixelsPerUnit = game.referencePixelsPerUnit;
                canvas.scaleFactor = game.scaleFactor;
            }
            return canvas;
        }

        /// <summary>The game's own GUI canvas (the root of Jötunn's canvas for mods' windows), for its scale.</summary>
        internal static Canvas GameCanvas()
        {
            Canvas front = GUIManager.CustomGUIFront != null ? GUIManager.CustomGUIFront.GetComponent<Canvas>() : null;
            return front != null ? front.rootCanvas : null;
        }

        /// <summary>
        /// How much bigger than the game's GUI scale the window is drawn: the setting's size, or as much of it as fits the
        /// screen with a margin, never smaller than the game's own scale (Add to Cart's and Outfitter's sizes).
        /// </summary>
        internal static float SizeFactor(WindowSize size, float gameScale, float screenWidth, float screenHeight, float width, float height)
        {
            float wanted = size == WindowSize.Large ? 1.5f : size == WindowSize.Medium ? 1.25f : 1f;
            float fits = Mathf.Min(screenWidth * 0.98f / Mathf.Max(1f, width), screenHeight * 0.96f / Mathf.Max(1f, height)) / Mathf.Max(0.01f, gameScale);
            return Mathf.Max(1f, Mathf.Min(wanted, fits));
        }
    }

    /// <summary>Mouse events for a row or a key: left and right clicks, and hovering. Each runs inside the module's guard.</summary>
    internal sealed class Clicks : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal Action Left;
        internal Action Right;
        internal Action Enter;
        internal Action Exit;

        public void OnPointerClick(PointerEventData data)
        {
            Action act = data.button == PointerEventData.InputButton.Right ? Right
                : data.button == PointerEventData.InputButton.Left ? Left
                : null;
            if (act != null)
            {
                Module.Guard("handling a click in the key map", act);
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
