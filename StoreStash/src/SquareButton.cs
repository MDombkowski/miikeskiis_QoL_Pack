using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StoreStash
{
    /// <summary>
    /// A square button beside his inventory: a copy of the game's own button (Place stacks), with a picture drawn in code and
    /// the game's own tooltip. Anchored to the inventory panel's bottom right, it stays with the weight tab when bought rows
    /// make the panel taller, and it slides in with the panel as the window opens. Its size and place are read every frame,
    /// so a setting counts at once; its picture is set as it's made, and its tooltip's words are written again after
    /// TipChanged. The quick stack button (Button) and the personal quick stack button (PersonalButton) are each one.
    /// </summary>
    internal sealed class SquareButton
    {
        private readonly string objectName;
        private readonly Func<Sprite> picture;
        private readonly Action clicked;
        private readonly Func<bool> wanted;
        private readonly Func<InventoryGui, bool> usable;
        private readonly Func<Vector2> place;
        private readonly Func<float> size;
        private readonly Func<string> tip;

        private UnityEngine.UI.Button button;
        private Image icon;
        private UITooltip tooltip;
        private string shownTip;
        private Vector2 placedAt;
        private float sizedAt;
        private bool toldNoTooltip;

        /// <param name="objectName">Its Unity object's name, StoreStash_… (MODULES.md rule 10).</param>
        /// <param name="picture">Its picture, white, tinted by the button's colour.</param>
        /// <param name="clicked">What a click does (inside the module's Guard: the caller's).</param>
        /// <param name="wanted">Its setting: shown at all.</param>
        /// <param name="usable">Whether a click would do anything now; greyed while not.</param>
        /// <param name="place">Its middle, from the inventory panel's bottom right corner, its offset setting included.</param>
        /// <param name="size">Its width and height.</param>
        /// <param name="tip">Its tooltip's words, written again after TipChanged.</param>
        internal SquareButton(string objectName, Func<Sprite> picture, Action clicked, Func<bool> wanted, Func<InventoryGui, bool> usable,
                              Func<Vector2> place, Func<float> size, Func<string> tip)
        {
            this.objectName = objectName;
            this.picture = picture;
            this.clicked = clicked;
            this.wanted = wanted;
            this.usable = usable;
            this.place = place;
            this.size = size;
            this.tip = tip;
        }

        /// <summary>Whether the button exists (for the scratch test harness).</summary>
        internal bool Made => button != null;

        /// <summary>The words its tooltip shows were written from settings that changed (a key): they're written again.</summary>
        internal void TipChanged() => shownTip = null;

        /// <summary>Every frame: shown while his inventory is open, the mod and its setting on; greyed while a press would do
        /// nothing.</summary>
        internal void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            if (!Module.IsOn || !wanted() || gui == null || gui.m_player == null || !InventoryGui.IsVisible())
            {
                Hide();
                return;
            }
            if (button == null)   // Unity's own test: gone with the window after a world change
            {
                Create(gui);
                if (button == null)
                {
                    return;
                }
            }
            Vector2 at = place();
            float side = size();
            if (at != placedAt || side != sizedAt)
            {
                Arrange(at, side);
            }
            if (!button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(true);
            }
            bool canPress = usable(gui);
            if (button.interactable != canPress)
            {
                button.interactable = canPress;
                icon.color = canPress ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            if (tooltip != null && shownTip == null)
            {
                shownTip = tip();
                tooltip.Set("StoreStash", shownTip);
            }
        }

        /// <summary>The undo step after an error: the button goes.</summary>
        internal void Remove()
        {
            if (button != null)
            {
                UnityEngine.Object.Destroy(button.gameObject);
            }
            button = null;
            icon = null;
            tooltip = null;
            shownTip = null;
        }

        private void Hide()
        {
            if (button != null && button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(false);
            }
        }

        // Its place and size, and its picture's: 4 px of the button's frame on every side.
        private void Arrange(Vector2 at, float side)
        {
            placedAt = at;
            sizedAt = side;
            var rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(side, side);
            rect.anchoredPosition = at;
            ((RectTransform)icon.transform).sizeDelta = new Vector2(side - 8f, side - 8f);
        }

        private void Create(InventoryGui gui)
        {
            UnityEngine.UI.Button template = gui.m_stackAllButton;
            if (template == null)
            {
                return;
            }
            GameObject copy = UnityEngine.Object.Instantiate(template.gameObject, gui.m_player);
            copy.name = objectName;
            // Not a second Place stacks: no gamepad key of its own (the copied UIGamePad would press it with Place stacks'
            // key) and no label, only the picture.
            foreach (UIGamePad pad in copy.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint != null)
                {
                    UnityEngine.Object.Destroy(pad.m_hint);
                }
                UnityEngine.Object.DestroyImmediate(pad);
            }
            foreach (TMP_Text label in copy.GetComponentsInChildren<TMP_Text>(true))
            {
                label.gameObject.SetActive(false);
            }
            UnityEngine.UI.Button made = copy.GetComponent<UnityEngine.UI.Button>();
            if (made == null)
            {
                UnityEngine.Object.Destroy(copy);
                return;
            }
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            made.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            made.onClick.AddListener(() => clicked());
            // The game's click sound (ButtonSfx) adds itself to onClick as the copy wakes, which it did inside Instantiate,
            // on the event just replaced: switched off and on, it adds itself to this one, so the first click sounds too.
            if (copy.activeSelf)
            {
                copy.SetActive(false);
                copy.SetActive(true);
            }
            Navigation navigation = made.navigation;
            navigation.mode = Navigation.Mode.None;
            made.navigation = navigation;

            var pictureObject = new GameObject(objectName + "_Icon", typeof(RectTransform), typeof(Image));
            var pictureRect = (RectTransform)pictureObject.transform;
            pictureRect.SetParent(copy.transform, false);
            pictureRect.anchorMin = new Vector2(0.5f, 0.5f);
            pictureRect.anchorMax = new Vector2(0.5f, 0.5f);
            pictureRect.pivot = new Vector2(0.5f, 0.5f);
            pictureRect.anchoredPosition = Vector2.zero;
            Image drawn = pictureObject.GetComponent<Image>();
            drawn.sprite = picture();
            drawn.raycastTarget = false;
            drawn.preserveAspect = true;

            // The game's own tooltip, with the look of the window's buttons' (the repair button's, then the craft button's).
            UITooltip source = TooltipOf(gui.m_repairButton) ?? TooltipOf(gui.m_craftButton) ??
                               gui.GetComponentsInChildren<UITooltip>(true).FirstOrDefault(each => each.m_tooltipPrefab != null);
            UITooltip hint = null;
            if (source != null)
            {
                hint = copy.GetComponent<UITooltip>();
                if (hint == null)   // Unity's own test, not ??, which it doesn't see
                {
                    hint = copy.AddComponent<UITooltip>();
                }
                hint.m_tooltipPrefab = source.m_tooltipPrefab;
            }
            else if (!toldNoTooltip)
            {
                toldNoTooltip = true;
                Module.Log.LogInfo($"The button {objectName} has no tooltip: no tooltip of the game's was found in the window.");
            }

            button = made;
            icon = drawn;
            tooltip = hint;
            shownTip = null;
            Arrange(place(), size());
        }

        // A button's own tooltip, if it has one with a picture to show; else a plain C# null, so ?? works on it.
        private static UITooltip TooltipOf(UnityEngine.UI.Button source)
        {
            UITooltip found = source != null ? source.GetComponent<UITooltip>() : null;
            return found != null && found.m_tooltipPrefab != null ? found : null;
        }
    }
}
