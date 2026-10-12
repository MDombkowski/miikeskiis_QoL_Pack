using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// Hover help in Outfitter's window (his words, 2026-10-06: "'Fully Replace', 'Swap gear only', and 'Swap gear + the
    /// rest' need on-hover help text to explain what they mean"; job 30, Session 3): pointing at a button for a moment shows
    /// a few words on what it does, in a small box just under it (above it near the screen's foot), on Outfitter's own
    /// canvas. One box for the whole window, made once; never catching a click. "Hover help" off: none.
    /// </summary>
    internal static class Tips
    {
        /// <summary>How long the pointer rests on a button before its words show (the game's own tooltips wait about as
        /// long).</summary>
        private const float Delay = 0.45f;

        private const float BoxWidth = 320f;

        /// <summary>How often the words of a tip showing are asked again (they may say what the button would do now, and
        /// some ask the stands): twice a second, not every frame (review round 1, m9).</summary>
        private const float Reask = 0.5f;

        private static GameObject box;
        private static Text words;
        private static Tip resting;
        private static float since;
        private static float nextAsk;
        private static readonly Vector3[] corners = new Vector3[4];

        /// <summary>Every frame the window or the pop-up shows: the box comes up once the pointer has rested long enough,
        /// and goes when it leaves, the window closes, or the setting is off.</summary>
        internal static void Update(bool windowShows) => Update(windowShows, false);

        /// <summary>As the other; while the pop-up shows (<paramref name="popupShows"/>), only the tips that always show
        /// (Tip.Always: an item's name beside its icon in the missing-items question, card B21) come up, whatever "Hover
        /// help" says, and no button's of the window under it.</summary>
        internal static void Update(bool windowShows, bool popupShows)
        {
            bool allowed = resting != null &&
                           (resting.Always ? windowShows || popupShows
                                           : windowShows && !popupShows && Settings.HoverHelp != null && Settings.HoverHelp.Value);
            bool wanted = allowed && resting.isActiveAndEnabled && Time.unscaledTime - since >= Delay;
            if (!wanted)
            {
                Hide();
                return;
            }
            if (box != null && box.activeSelf && Time.unscaledTime < nextAsk)
            {
                return;   // showing, and asked less than half a second ago
            }
            nextAsk = Time.unscaledTime + Reask;
            string text = resting.Words?.Invoke();
            if (string.IsNullOrEmpty(text))
            {
                Hide();
                return;
            }
            Show(text, (RectTransform)resting.transform);
        }

        internal static void Rest(Tip tip)
        {
            if (resting != tip)
            {
                resting = tip;
                since = Time.unscaledTime;
                Hide();
            }
        }

        /// <summary>A click on the button under the pointer: its words asked again at the next frame (they may say what it
        /// does now, and the window may have moved: review round 2, n8).</summary>
        internal static void Clicked(Tip tip)
        {
            if (resting == tip)
            {
                nextAsk = 0f;
            }
        }

        internal static void Leave(Tip tip)
        {
            if (resting == tip)
            {
                resting = null;
                Hide();
            }
        }

        /// <summary>Takes the box down and forgets the button under the pointer (the window closed, an undo step).</summary>
        internal static void Forget()
        {
            resting = null;
            Hide();
        }

        private static void Hide()
        {
            if (box != null && box.activeSelf)
            {
                box.SetActive(false);
            }
        }

        private static void Show(string text, RectTransform over)
        {
            Transform canvas = Ui.CanvasRoot();
            if (box == null || box.transform.parent != canvas)
            {
                Make(canvas);
            }
            if (words.text != text)
            {
                words.text = text;
            }
            var canvasRect = (RectTransform)canvas;
            var rect = (RectTransform)box.transform;
            rect.sizeDelta = new Vector2(BoxWidth, rect.sizeDelta.y);   // its width first: the words are measured at it (n14)
            float height = Mathf.Ceil(words.preferredHeight) + 14f;
            rect.sizeDelta = new Vector2(BoxWidth, height);
            // Under the button, its left edges lined up; above it when that would run off the canvas's foot.
            over.GetWorldCorners(corners);
            Vector2 bottomLeft = canvasRect.InverseTransformPoint(corners[0]);
            Vector2 topLeft = canvasRect.InverseTransformPoint(corners[1]);
            Rect area = canvasRect.rect;
            float x = Mathf.Clamp(bottomLeft.x, area.xMin + 4f, area.xMax - BoxWidth - 4f);
            float y = bottomLeft.y - 6f;
            if (y - height < area.yMin + 4f)
            {
                y = topLeft.y + 6f + height;
            }
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            if (!box.activeSelf)
            {
                box.SetActive(true);
            }
            box.transform.SetAsLastSibling();
        }

        private static void Make(Transform canvas)
        {
            if (box != null)
            {
                UnityEngine.Object.Destroy(box);
            }
            box = new GameObject("Outfitter_Tip", typeof(RectTransform));
            box.transform.SetParent(canvas, false);
            ((RectTransform)box.transform).sizeDelta = new Vector2(BoxWidth, 40f);
            Image back = box.AddComponent<Image>();
            back.color = new Color(0.08f, 0.07f, 0.06f, 0.94f);
            back.raycastTarget = false;
            Outline edge = box.AddComponent<Outline>();
            edge.effectColor = new Color(1f, 0.75f, 0.35f, 0.6f);
            edge.effectDistance = new Vector2(1f, -1f);
            CanvasGroup group = box.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            GUIManager gui = GUIManager.Instance;
            GameObject made = gui.CreateText(string.Empty, box.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                             gui.AveriaSerif, 14, Ui.Plain, true, Color.black, BoxWidth - 16f, 40f, false);
            var textRect = (RectTransform)made.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(8f, 6f);
            textRect.offsetMax = new Vector2(-8f, -6f);
            words = made.GetComponent<Text>();
            words.alignment = TextAnchor.UpperLeft;
            words.horizontalOverflow = HorizontalWrapMode.Wrap;
            words.verticalOverflow = VerticalWrapMode.Overflow;
            words.raycastTarget = false;
            words.supportRichText = false;
            box.SetActive(false);
        }
    }

    /// <summary>
    /// A button's hover help and its hover look: the words <see cref="Tips"/> shows while the pointer rests on it, and,
    /// for a faded choice (an Equip mode not chosen), full brightness while the pointer is over it, so it reads as one to
    /// click. Never catches a click itself.
    /// </summary>
    internal sealed class Tip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        /// <summary>The words, asked each time they show (they may say what the button would do now).</summary>
        internal Func<string> Words;

        /// <summary>Shown whatever "Hover help" says, and over the pop-up: what it names can be read nowhere else (an item's
        /// name beside its icon in the missing-items question, card B21).</summary>
        internal bool Always;

        /// <summary>Drawn faded until the pointer is over it (Ui.SetFaded).</summary>
        internal bool Faded;

        internal bool Over { get; private set; }

        public void OnPointerEnter(PointerEventData data)
        {
            Over = true;
            Module.Guard("showing a button's help", () =>
            {
                Tips.Rest(this);
                Ui.PaintFade(this);
            });
        }

        public void OnPointerExit(PointerEventData data)
        {
            Over = false;
            Module.Guard("hiding a button's help", () =>
            {
                Tips.Leave(this);
                Ui.PaintFade(this);
            });
        }

        public void OnPointerClick(PointerEventData data)
        {
            Module.Guard("showing a button's help", () => Tips.Clicked(this));
        }

        private void OnDisable()
        {
            Over = false;
            Module.Guard("hiding a button's help", () => Tips.Leave(this));   // review round 1, n13
        }
    }
}
