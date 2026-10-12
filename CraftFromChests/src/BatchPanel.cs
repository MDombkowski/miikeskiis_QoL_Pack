using System.Collections.Generic;
using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CraftFromChests
{
    /// <summary>
    /// The number beside the Craft button (card B16): the amount on the left, + above - on the right, both copies of the
    /// game's own Craft button so they look like it. It takes the right quarter of the Craft button's own place, and the
    /// Craft button gives that quarter up while it shows, so nothing else in the menu is covered at any size of the screen
    /// (MyLittleUI's Multicraft sat in the same place). The number shrinks to fit its box. Made once per crafting menu,
    /// set up each frame after the game drew the menu (LateUpdate), shown only while crafting an amount applies to the
    /// recipe shown and the Craft button shows (the game hides it while a bar runs). Off, it hides and the Craft button
    /// gets its whole width back at once. While a run goes, the crafting bar's text says how many are left: "(36)".
    /// </summary>
    internal static class BatchPanel
    {
        private const string PanelName = "CraftFromChests_Amount";

        /// <summary>The share of the Craft button's width the panel takes.</summary>
        private const float Share = 0.25f;

        private static InventoryGui builtFor;
        private static RectTransform panel;
        private static RectTransform craftButton;
        private static Vector2 craftOffsetMax;
        private static bool shrunk;
        private static Button plus;
        private static Button minus;
        private static TMP_Text number;
        private static TMP_Text progress;
        private static bool progressChanged;
        private static int progressShown;
        private static int numberShown = -1;
        private static UITooltip plusTip;
        private static UITooltip minusTip;
        private static bool tipKeysSet;
        private static KeyboardShortcut tipStep;
        private static KeyboardShortcut tipMost;

        /// <summary>The pointer is over the panel (for the wheel).</summary>
        internal static bool Hovered;

        /// <summary>The panel shows this frame.</summary>
        internal static bool Showing { get; private set; }

        /// <summary>Every frame, after the game drew the menu.</summary>
        internal static void Refresh()
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null)
            {
                Showing = false;
                return;
            }
            bool wanted = Batches.PanelWanted(gui);
            if (wanted && (builtFor != gui || panel == null))
            {
                Build(gui);
            }
            Showing = wanted && panel != null;
            if (panel != null && panel.gameObject.activeSelf != Showing)
            {
                panel.gameObject.SetActive(Showing);
            }
            if (Showing)
            {
                Shrink();
                int amount = Batches.Amount;
                if (amount != numberShown)
                {
                    numberShown = amount;
                    number.text = amount.ToString();
                }
                plus.interactable = amount < Batches.Ceiling;
                minus.interactable = amount > 1;
                Tooltips();
                if (amount > 1 && (ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyLStick")))
                {
                    // The game's "Craft x 5" while Shift is held doesn't apply to a run: the amount does.
                    TMP_Text label = gui.m_craftButton.GetComponentInChildren<TMP_Text>();
                    if (label != null)
                    {
                        label.text = Localization.instance.Localize("$inventory_craftbutton");
                    }
                }
            }
            else
            {
                Unshrink();
            }
            ShowProgress(gui);
        }

        /// <summary>Takes it all away: the panel, the Craft button's width, the bar's text. An undo step.</summary>
        internal static void Remove()
        {
            Unshrink();
            if (progress != null && progressChanged)
            {
                progress.text = Localization.instance.Localize("$inventory_craftingprog");
            }
            progressChanged = false;
            if (panel != null)
            {
                Object.Destroy(panel.gameObject);
            }
            panel = null;
            builtFor = null;
            Showing = false;
            Hovered = false;
        }

        // The two buttons' tooltips, naming the keys as the settings have them now (written again only when they change).
        private static void Tooltips()
        {
            KeyboardShortcut step = Settings.StepFiveKey.Value;
            KeyboardShortcut most = Settings.MostKey.Value;
            if (tipKeysSet && step.Equals(tipStep) && most.Equals(tipMost))
            {
                return;
            }
            tipKeysSet = true;
            tipStep = step;
            tipMost = most;
            string stepName = step.MainKey == KeyCode.None ? null : step.ToString();
            string mostName = most.MainKey == KeyCode.None ? "Right click" : most + " or right click";
            if (plusTip != null)
            {
                plusTip.m_text = "One more." + (stepName == null ? "" : $" {stepName}: five more.") + $" {mostName}: the most you can make.";
            }
            if (minusTip != null)
            {
                minusTip.m_text = "One fewer." + (stepName == null ? "" : $" {stepName}: five fewer.") + $" {mostName}: back to 1.";
            }
        }

        // The bar's text while a run goes: how many are left, the craft under way included.
        private static void ShowProgress(InventoryGui gui)
        {
            if (progress == null && gui.m_craftProgressPanel != null)
            {
                Transform text = gui.m_craftProgressPanel.Find("Text");
                progress = text != null ? text.GetComponent<TMP_Text>() : null;
            }
            if (progress == null)
            {
                return;
            }
            if (Batches.Running && Batches.Amount > 0)
            {
                if (!progressChanged || progressShown != Batches.Amount)
                {
                    progressShown = Batches.Amount;
                    progress.text = Localization.instance.Localize("$inventory_craftingprog") + $" ({progressShown})";
                    progressChanged = true;
                }
            }
            else if (progressChanged)
            {
                progress.text = Localization.instance.Localize("$inventory_craftingprog");
                progressChanged = false;
            }
        }

        // The Craft button gives up its right quarter, by its right edge only (its anchors stay as they are, so a mod that
        // sets them, as MyLittleUI does each frame, can't undo it); measured again each frame, since the menu's width
        // follows the screen.
        private static void Shrink()
        {
            if (craftButton == null)
            {
                return;
            }
            if (!shrunk)
            {
                craftOffsetMax = craftButton.offsetMax;
                shrunk = true;
            }
            float cut = Width(craftButton, craftOffsetMax) * Share;
            Set(craftButton, craftButton.anchorMin, craftButton.anchorMax, craftButton.offsetMin, new Vector2(craftOffsetMax.x - cut, craftOffsetMax.y));
            // Both of the panel's side edges hang from the Craft button's right anchor, so its left edge is measured from the
            // same point as its right one, whether the button stretches with its parent or not: it covers exactly the
            // quarter the button gave up, never the button itself. Top and bottom are the button's own.
            float right = craftButton.anchorMax.x;
            Set(panel, new Vector2(right, craftButton.anchorMin.y), new Vector2(right, craftButton.anchorMax.y),
                new Vector2(craftOffsetMax.x - cut, craftButton.offsetMin.y), craftOffsetMax);
        }

        // Writes a rectangle only where it differs, so the menu isn't laid out again every frame for nothing.
        private static void Set(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect.anchorMin != anchorMin)
            {
                rect.anchorMin = anchorMin;
            }
            if (rect.anchorMax != anchorMax)
            {
                rect.anchorMax = anchorMax;
            }
            if (rect.offsetMin != offsetMin)
            {
                rect.offsetMin = offsetMin;
            }
            if (rect.offsetMax != offsetMax)
            {
                rect.offsetMax = offsetMax;
            }
        }

        // The Craft button's right edge back exactly as it was before the first shrink (only that offset was ever changed).
        private static void Unshrink()
        {
            if (shrunk && craftButton != null && craftButton.offsetMax != craftOffsetMax)
            {
                craftButton.offsetMax = craftOffsetMax;
            }
            shrunk = false;
        }

        // The Craft button's whole width (as if it had its right edge back), from its parent's width and its anchors.
        private static float Width(RectTransform button, Vector2 offsetMax)
        {
            var parent = button.parent as RectTransform;
            float parentWidth = parent != null ? parent.rect.width : 0f;
            return Mathf.Max(0f, parentWidth * (button.anchorMax.x - button.anchorMin.x) + offsetMax.x - button.offsetMin.x);
        }

        private static void Build(InventoryGui gui)
        {
            Remove();
            if (gui.m_craftButton == null)
            {
                return;
            }
            craftButton = gui.m_craftButton.GetComponent<RectTransform>();
            TMP_Text craftText = gui.m_craftButton.GetComponentInChildren<TMP_Text>(true);
            if (craftButton == null || craftText == null)
            {
                return;
            }
            builtFor = gui;
            panel = new GameObject(PanelName, typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(craftButton.parent, false);
            panel.gameObject.AddComponent<WheelArea>();
            plus = MakeButton(gui, "CraftFromChests_AmountUp", "+", 1);
            minus = MakeButton(gui, "CraftFromChests_AmountDown", "-", -1);
            number = Object.Instantiate(craftText, panel);
            number.name = "CraftFromChests_AmountNumber";
            RectTransform box = number.rectTransform;
            box.anchorMin = Vector2.zero;
            box.anchorMax = new Vector2(0.5f, 1f);
            box.offsetMin = new Vector2(2f, 0f);
            box.offsetMax = new Vector2(-2f, 0f);
            number.textWrappingMode = TextWrappingModes.NoWrap;
            number.enableAutoSizing = true;
            number.fontSizeMin = 8f;
            number.fontSizeMax = Mathf.Max(8f, craftText.fontSize * 1.25f);
            number.alignment = TextAlignmentOptions.Center;
            number.raycastTarget = true;   // so the wheel works over the number too (it covers nothing but its own box)
            number.text = "1";
            numberShown = -1;
            panel.gameObject.SetActive(false);
        }

        // A copy of the Craft button with none of its own handlers: a left click steps, a right click goes to the most or
        // 1, and a gamepad's right stick up or down steps (as MyLittleUI's did).
        private static Button MakeButton(InventoryGui gui, string name, string label, int direction)
        {
            GameObject copy = Object.Instantiate(gui.m_craftButton.gameObject, panel);
            copy.name = name;
            copy.SetActive(true);
            Button button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Module.Guard("changing the amount to craft", () => Batches.Change(direction, false)));
            foreach (EventTrigger trigger in copy.GetComponentsInChildren<EventTrigger>(true))
            {
                trigger.triggers = new List<EventTrigger.Entry>();
            }
            UIGamePad pad = copy.GetComponent<UIGamePad>();
            if (pad != null)
            {
                pad.m_zinputKey = direction > 0 ? "JoyRStickUp" : "JoyRStickDown";
                pad.m_keyCode = KeyCode.None;
                if (pad.m_hint != null && pad.m_hint.transform.IsChildOf(copy.transform))
                {
                    Object.Destroy(pad.m_hint);
                }
                pad.m_hint = null;
            }
            UITooltip tooltip = copy.GetComponent<UITooltip>();
            if (direction > 0)
            {
                plusTip = tooltip;
            }
            else
            {
                minusTip = tooltip;
            }
            tipKeysSet = false;
            copy.AddComponent<AmountRightClick>().Direction = direction;
            var rect = copy.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, direction > 0 ? 0.5f : 0f);
            rect.anchorMax = new Vector2(1f, direction > 0 ? 1f : 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TMP_Text text = copy.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = label;
                text.enableAutoSizing = true;
                text.fontSizeMin = 8f;
                text.textWrappingMode = TextWrappingModes.NoWrap;
            }
            return button;
        }
    }

    /// <summary>The pointer over the amount panel, for the mouse wheel.</summary>
    internal sealed class WheelArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => BatchPanel.Hovered = true;

        public void OnPointerExit(PointerEventData eventData) => BatchPanel.Hovered = false;

        private void OnDisable() => BatchPanel.Hovered = false;
    }

    /// <summary>A right click on + or -: the most he can make, or back to 1 (Unity's Button answers only the left click).</summary>
    internal sealed class AmountRightClick : MonoBehaviour, IPointerClickHandler
    {
        internal int Direction;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
            {
                Module.Guard("changing the amount to craft", () => Batches.Change(Direction, true));
            }
        }
    }
}
