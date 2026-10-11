using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UiButton = UnityEngine.UI.Button;   // StoreStash.Button is the quick stack button

namespace StoreStash
{
    /// <summary>
    /// One of sorting's and the trash's square buttons (card A53): a copy of the game's own Place stacks button with a drawn
    /// picture instead of its words, and the game's own tooltip. Made the first time it shows, under the parent it's
    /// shown in, and again after a world change rebuilt the window. As the pack's other copied buttons: no gamepad key of
    /// its own (the copy would press it with Place stacks' key), navigation off (else a clicked one keeps the keyboard and
    /// Enter presses it again), its click replaced before it first shows (the game's click sound adds itself each time
    /// it's shown). Its place is set every frame it shows, so it follows the settings and the game.
    /// </summary>
    internal sealed class SortTrashButton
    {
        private readonly string objectName;
        private readonly Func<Sprite> picture;
        private readonly Action clicked;
        private readonly float pictureSize;
        private UiButton button;
        private Image icon;
        private UITooltip tooltip;
        private string shownTip;
        private bool toldNoTooltip;

        /// <param name="objectName">Its Unity object's name, StoreStash_…, so no other module finds it.</param>
        /// <param name="picture">Its picture.</param>
        /// <param name="pictureSize">The picture's width and height on the button.</param>
        /// <param name="clicked">What a click does (run inside the module's Guard by the caller).</param>
        internal SortTrashButton(string objectName, Func<Sprite> picture, float pictureSize, Action clicked)
        {
            this.objectName = objectName;
            this.picture = picture;
            this.pictureSize = pictureSize;
            this.clicked = clicked;
        }

        /// <summary>Shows it under <paramref name="parent"/>, its middle at <paramref name="position"/> from the anchor,
        /// with this tooltip (set only when it changes), picture colour and clickability. False if it couldn't be made.</summary>
        internal bool Show(InventoryGui gui, Transform parent, Vector2 anchor, Vector2 position, float size, string tip, Color colour, bool interactable)
        {
            if (button == null || button.transform.parent != parent)   // Unity's own test: gone with the window
            {
                Remove();
                Create(gui, parent);
                if (button == null)
                {
                    return false;
                }
            }
            var rect = (RectTransform)button.transform;
            if (rect.anchorMin != anchor || rect.anchorMax != anchor)
            {
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
            }
            if (rect.anchoredPosition != position)
            {
                rect.anchoredPosition = position;
            }
            var sized = new Vector2(size, size);
            if (rect.sizeDelta != sized)
            {
                rect.sizeDelta = sized;
            }
            if (!button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(true);
            }
            if (button.interactable != interactable)
            {
                button.interactable = interactable;
            }
            if (icon.color != colour)
            {
                icon.color = colour;
            }
            if (tooltip != null && tip != shownTip)
            {
                shownTip = tip;
                tooltip.Set("StoreStash", tip);
            }
            return true;
        }

        internal void Hide()
        {
            if (button != null && button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(false);
            }
        }

        /// <summary>The undo step after an error, and before making it again: the button goes.</summary>
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

        private void Create(InventoryGui gui, Transform parent)
        {
            UiButton template = gui != null ? gui.m_stackAllButton : null;
            if (template == null || parent == null)
            {
                return;
            }
            GameObject copy = UnityEngine.Object.Instantiate(template.gameObject, parent);
            copy.name = objectName;
            copy.SetActive(false);   // shown by Show, once placed
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
            UiButton made = copy.GetComponent<UiButton>();
            if (made == null)
            {
                UnityEngine.Object.Destroy(copy);
                return;
            }
            var rect = (RectTransform)copy.transform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            made.onClick = new UiButton.ButtonClickedEvent();
            made.onClick.AddListener(() => clicked());
            Navigation navigation = made.navigation;
            navigation.mode = Navigation.Mode.None;
            made.navigation = navigation;

            var pictureObject = new GameObject(objectName + "_Icon", typeof(RectTransform), typeof(Image));
            var pictureRect = (RectTransform)pictureObject.transform;
            pictureRect.SetParent(copy.transform, false);
            pictureRect.anchorMin = new Vector2(0.5f, 0.5f);
            pictureRect.anchorMax = new Vector2(0.5f, 0.5f);
            pictureRect.pivot = new Vector2(0.5f, 0.5f);
            pictureRect.sizeDelta = new Vector2(pictureSize, pictureSize);
            pictureRect.anchoredPosition = Vector2.zero;
            Image image = pictureObject.GetComponent<Image>();
            image.sprite = picture();
            image.raycastTarget = false;
            image.preserveAspect = true;

            // The game's own tooltip, with the look of the window's buttons' (the repair button's, then the craft button's);
            // any other in the window only if neither has one (an item slot's differs, and mods add their own).
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
                Module.Log.LogInfo($"{objectName} has no tooltip: no tooltip of the game's was found in the window.");
            }

            button = made;
            icon = image;
            tooltip = hint;
            shownTip = null;
        }

        // A button's own tooltip, if it has one with a picture to show; else a plain C# null, so ?? works on it.
        private static UITooltip TooltipOf(UiButton source)
        {
            UITooltip found = source != null ? source.GetComponent<UITooltip>() : null;
            return found != null && found.m_tooltipPrefab != null ? found : null;
        }
    }
}
