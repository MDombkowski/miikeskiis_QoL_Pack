using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// Small helpers for Outfitter's window and pop-up, built from Unity's own UI parts in Valheim's style (Jötunn's
    /// GUIManager does the styling), as Add to Cart's window is. Every position is from the parent's top-left corner, x to
    /// the right and y downwards, in pixels.
    /// </summary>
    internal static class Ui
    {
        internal static readonly Color Cell = new Color(0.12f, 0.1f, 0.08f, 0.85f);
        internal static readonly Color CellWorn = new Color(0.16f, 0.27f, 0.42f, 0.9f);
        internal static readonly Color CellPicked = new Color(0.55f, 0.36f, 0.08f, 0.95f);

        /// <summary>A food slot's cell (job 30, Session 3): a rule, not an item.</summary>
        internal static readonly Color CellFood = new Color(0.14f, 0.3f, 0.14f, 0.9f);
        internal static readonly Color Missing = new Color(1f, 0.35f, 0.3f, 0.9f);
        internal static readonly Color Bad = new Color(1f, 0.45f, 0.35f, 1f);
        internal static readonly Color Good = new Color(0.6f, 0.9f, 0.5f, 1f);
        internal static readonly Color Plain = new Color(0.95f, 0.92f, 0.85f, 1f);
        internal static readonly Color Quiet = new Color(0.75f, 0.72f, 0.65f, 1f);
        internal static readonly Color RowPlain = new Color(0f, 0f, 0f, 0f);
        internal static readonly Color RowHover = new Color(1f, 1f, 1f, 0.09f);
        internal static readonly Color RowPicked = new Color(0.36f, 0.49f, 0.65f, 0.95f);

        internal static Color Orange => GUIManager.Instance.ValheimOrange;

        private static Canvas ownCanvas;

        /// <summary>
        /// Outfitter's own canvas, over the one Jötunn keeps for mods' windows, in the game's scene (a new world makes a new
        /// one), at the game's GUI scale. The window and the pop-up are drawn on it, so a stand's question shows without
        /// the window.
        /// </summary>
        internal static Transform CanvasRoot()
        {
            if (ownCanvas == null)
            {
                Canvas front = GUIManager.CustomGUIFront.GetComponent<Canvas>();
                Canvas game = front != null ? front.rootCanvas : null;
                var holder = new GameObject("Outfitter_Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                holder.layer = GUIManager.CustomGUIFront.layer;
                if (holder.scene != GUIManager.CustomGUIFront.scene)
                {
                    SceneManager.MoveGameObjectToScene(holder, GUIManager.CustomGUIFront.scene);
                }
                ownCanvas = holder.GetComponent<Canvas>();
                ownCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ownCanvas.sortingOrder = 2002;   // over Jötunn's canvas for mods' windows (2000) and Add to Cart's (2001)
                if (front != null)
                {
                    ownCanvas.additionalShaderChannels = front.additionalShaderChannels;
                }
                if (game != null)
                {
                    ownCanvas.pixelPerfect = game.pixelPerfect;
                    ownCanvas.scaleFactor = game.scaleFactor;
                    ownCanvas.referencePixelsPerUnit = game.referencePixelsPerUnit;
                }
            }
            return ownCanvas.transform;
        }

        /// <summary>The size of what the canvas shows now, at Small (the window, with the drawer when it's open; the
        /// pop-up alone): what "Window size" keeps to the screen.</summary>
        internal static Vector2 Content = new Vector2(900f, 760f);

        /// <summary>Follows the game's GUI scale (a change in its settings) and "Window size", every frame the window or
        /// pop-up shows: the canvas is drawn at the game's scale times the size's factor, so the text stays sharp.</summary>
        internal static void FitCanvas()
        {
            if (ownCanvas == null || GUIManager.CustomGUIFront == null)
            {
                return;
            }
            Canvas front = GUIManager.CustomGUIFront.GetComponent<Canvas>();
            Canvas game = front != null ? front.rootCanvas : null;
            if (game == null)
            {
                return;
            }
            WindowSize size = Settings.Size != null ? Settings.Size.Value : WindowSize.Small;
            float wanted = game.scaleFactor * SizeFactor(size, game.scaleFactor, Screen.width, Screen.height, Content.x, Content.y);
            if (!Mathf.Approximately(ownCanvas.scaleFactor, wanted))
            {
                ownCanvas.scaleFactor = wanted;
            }
        }

        /// <summary>
        /// How much bigger than the game's GUI scale the window is drawn (Add to Cart's Looks.SizeFactor, its sizes): the
        /// setting's size, or as much of it as fits the screen with a margin, and never smaller than the game's own scale (a
        /// window that doesn't fit at Small stays as it always was).
        /// </summary>
        internal static float SizeFactor(WindowSize size, float gameScale, float screenWidth, float screenHeight, float width, float height)
        {
            float wanted = size == WindowSize.Large ? 1.5f : size == WindowSize.Medium ? 1.25f : 1f;
            float fits = Mathf.Min(screenWidth * 0.98f / Mathf.Max(1f, width), screenHeight * 0.96f / Mathf.Max(1f, height)) / Mathf.Max(0.01f, gameScale);
            return Mathf.Max(1f, Mathf.Min(wanted, fits));
        }

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
            label.supportRichText = false;   // a loadout's name (a friend's code) can never format the text
            return label;
        }

        /// <summary>A button in the game's style. Its navigation is off (a clicked button would otherwise keep the keyboard,
        /// and Enter or an arrow key press it again: job 11's lesson); its click is set once, when it's made.</summary>
        internal static Button Button(Transform parent, string text, float x, float y, float width, float height, UnityAction onClick)
        {
            GameObject made = GUIManager.Instance.CreateButton(text, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, width, height);
            Place(made, x, y, width, height);
            Button button = made.GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            return button;
        }

        internal static void SetButtonText(Button button, string text)
        {
            Text label = button != null ? button.GetComponentInChildren<Text>() : null;
            if (label != null && label.text != text)
            {
                label.text = text;
            }
        }

        /// <summary>How a button looks, by what it would do now (his words, 2026-10-06: "highlight the buttons when [they'd
        /// have an effect with cells selected], and fade them out almost entirely when a button would have zero effect";
        /// job 30, Session 3). Off: faded to a quarter, not clickable. On: as the game draws it. Lit: on, with an orange
        /// frame (what it does depends on the highlighted cells, or it's a switch that is on).</summary>
        internal enum Look
        {
            Off,
            On,
            Lit,
        }

        private const float OffAlpha = 0.25f;
        private const float FadedAlpha = 0.45f;

        /// <summary>Sets a button's look; changes nothing that's already so.</summary>
        internal static void Set(Button button, Look look)
        {
            if (button == null)
            {
                return;
            }
            bool on = look != Look.Off;
            if (button.interactable != on)
            {
                button.interactable = on;
            }
            Tip tip = button.GetComponent<Tip>();
            float alpha = !on ? OffAlpha : tip != null && tip.Faded && !tip.Over ? FadedAlpha : 1f;
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = button.gameObject.AddComponent<CanvasGroup>();
            }
            if (!Mathf.Approximately(group.alpha, alpha))
            {
                group.alpha = alpha;
            }
            bool lit = look == Look.Lit;
            Transform frame = button.transform.Find(LitFrame);
            if (frame == null && lit)
            {
                frame = MakeFrame(button.transform);
            }
            if (frame != null && frame.gameObject.activeSelf != lit)
            {
                frame.gameObject.SetActive(lit);
            }
        }

        private const string LitFrame = "Outfitter_Lit";

        // A Lit button's orange frame: four thin edges in a child of its own, just outside the button, catching no pointer
        // (not an Outline on the button itself, which would meet any effect Jotunn's button carries: review round 1).
        private static Transform MakeFrame(Transform button)
        {
            var frame = new GameObject(LitFrame, typeof(RectTransform));
            frame.transform.SetParent(button, false);
            var rect = (RectTransform)frame.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-3f, -3f);
            rect.offsetMax = new Vector2(3f, 3f);
            Edge(frame.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f));
            Edge(frame.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 3f));
            Edge(frame.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(3f, 0f));
            Edge(frame.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(3f, 0f));
            return frame.transform;
        }

        private static void Edge(Transform frame, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size)
        {
            var edge = new GameObject("Edge", typeof(RectTransform));
            edge.transform.SetParent(frame, false);
            var rect = (RectTransform)edge.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            Image image = edge.AddComponent<Image>();
            image.color = new Color(1f, 0.62f, 0.15f, 1f);
            image.raycastTarget = false;
        }

        /// <summary>A choice drawn faded until the pointer is over it (an Equip mode not chosen): it reads as one to click.</summary>
        internal static void SetFaded(Button button, bool faded)
        {
            Tip tip = button != null ? button.GetComponent<Tip>() : null;
            if (tip == null || tip.Faded == faded)
            {
                return;
            }
            tip.Faded = faded;
            PaintFade(tip);
        }

        /// <summary>A faded choice's brightness, as the pointer comes and goes.</summary>
        internal static void PaintFade(Tip tip)
        {
            Button button = tip != null ? tip.GetComponent<Button>() : null;
            if (button == null || !button.interactable)
            {
                return;
            }
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = button.gameObject.AddComponent<CanvasGroup>();
            }
            group.alpha = tip.Faded && !tip.Over ? FadedAlpha : 1f;
        }

        /// <summary>Gives a button (or any element that catches the pointer) its hover help.</summary>
        internal static void Help(Component thing, Func<string> words)
        {
            if (thing == null)
            {
                return;
            }
            Tip tip = thing.GetComponent<Tip>();
            if (tip == null)
            {
                tip = thing.gameObject.AddComponent<Tip>();
            }
            tip.Words = words;
        }

        internal static void Help(Component thing, string words) => Help(thing, () => words);

        internal static Image Icon(Transform parent, float x, float y, float size)
        {
            GameObject made = Box("Icon", parent, x, y, size, size);
            Image image = made.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        /// <summary>A vertical scroll area; returns its content, which grows downwards with its children (a vertical
        /// layout group on it).</summary>
        internal static RectTransform Scroll(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject area = Box(name, parent, x, y, width, height, new Color(0f, 0f, 0f, 0.35f));
            ScrollRect scroll = area.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            GameObject viewport = Box("Viewport", area.transform, 0f, 0f, width, height);
            viewport.AddComponent<RectMask2D>();
            Image catcher = viewport.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);   // catches the wheel over empty space
            GameObject content = Box("Content", viewport.transform, 0f, 0f, width, 0f);
            var rect = (RectTransform)content.transform;
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 2f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = rect;
            return rect;
        }

        /// <summary>A row of a scroll list: a background that lights up under the pointer, and a click.</summary>
        internal static Clicks Row(Transform content, float height, out Image background, out Text label)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(content, false);
            LayoutElement element = row.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            background = row.AddComponent<Image>();
            background.color = RowPlain;
            Clicks clicks = row.AddComponent<Clicks>();
            clicks.Background = background;
            label = Label(row.transform, string.Empty, 8f, 0f, 220f, height, 16);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(8f, 0f);
            labelRect.offsetMax = new Vector2(-4f, 0f);
            return clicks;
        }

        /// <summary>Whether an input field has the keyboard (the window's keys stay quiet while he types).</summary>
        internal static bool Typing()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<InputField>() is InputField field && field.isFocused;
        }
    }

    /// <summary>A rectangle that answers the pointer: a left click, a right click, entering and leaving; its background
    /// lights up under the pointer unless told otherwise.</summary>
    internal sealed class Clicks : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal Action OnLeft;
        internal Action OnRight;
        internal Action OnEnter;
        internal Action OnExit;
        internal Image Background;
        internal Color Normal = Ui.RowPlain;
        internal bool Lights = true;

        public void OnPointerClick(PointerEventData data)
        {
            Action act = data.button == PointerEventData.InputButton.Right ? OnRight : data.button == PointerEventData.InputButton.Left ? OnLeft : null;
            if (act != null)
            {
                Module.Guard("answering a click in Outfitter's window", act);
            }
        }

        public void OnPointerEnter(PointerEventData data)
        {
            if (Lights && Background != null)
            {
                Background.color = Ui.RowHover;
            }
            if (OnEnter != null)
            {
                Module.Guard("showing what's under the pointer", OnEnter);
            }
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (Lights && Background != null)
            {
                Background.color = Normal;
            }
            if (OnExit != null)
            {
                Module.Guard("showing what's under the pointer", OnExit);
            }
        }
    }
}
