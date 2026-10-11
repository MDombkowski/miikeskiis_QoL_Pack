using System.Linq;
using QoLMods.Chests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ReserveStock
{
    /// <summary>
    /// The padlock button in an open chest's window: a copy of the game's Place stacks button, square, just right of it
    /// and level with it, outside the window's edge (HexQuickStackStorage's Sort button sits on Place stacks' left, and a
    /// long chest name spills over the space by Take all). Closed and orange while the chest is reserved, open and grey
    /// while it isn't; the game's own tooltip says what a click does. It lives in the game's chest window, made the first
    /// time a chest is opened, and again after a world change rebuilt the window.
    /// </summary>
    internal static class WindowButton
    {
        private const string ObjectName = "ReserveStock_Button";

        /// <summary>The button's width and height, as HexQuickStackStorage's square buttons.</summary>
        internal const float Size = 36f;

        /// <summary>The space between Place stacks and the button.</summary>
        internal const float Gap = 4f;

        private static Button button;
        private static Image icon;
        private static UITooltip tooltip;
        private static bool toldNoTooltip;
        private static int shownState = -1;

        /// <summary>Every frame: shows the button while a chest that can carry the mark is open, as the chest's mark
        /// stands; hides it otherwise, and while the mod or its setting is off.</summary>
        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            Container chest = Module.IsOn && Settings.ShowButton.Value && GameHooks.CanSeeOpenChest && gui != null && InventoryGui.IsVisible()
                ? GameHooks.OpenChest(gui)
                : null;
            // Not on another player's personal chest he may only look at (card A80): reserving it is theirs to do.
            if (chest == null || !Mark.Reservable(chest) || !ChestAccess.MayTouch(chest))
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
            Place((RectTransform)button.transform, (RectTransform)gui.m_stackAllButton.transform);
            if (!button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(true);
            }
            bool reserved = ChestReserve.IsReserved(chest);
            int state = reserved ? 1 : 0;
            if (state != shownState)
            {
                shownState = state;
                icon.sprite = reserved ? ReserveLock.Closed : ReserveLock.Open;
                icon.color = reserved ? ReserveLock.ReservedColor : ReserveLock.FreeColor;
                if (tooltip != null)
                {
                    tooltip.Set("Reserve Stock", reserved
                        ? "Reserved: quick stack, Add to Cart and Craft from Chests leave this chest alone (unless Craft from Chests' setting \"Reserved chests\" uses them). Click to free it."
                        : "Click to reserve this chest: quick stack, Add to Cart and Craft from Chests will leave it alone (unless Craft from Chests' setting \"Reserved chests\" uses them).");
                }
            }
        }

        /// <summary>The undo step after an error: the button goes with its window.</summary>
        internal static void Remove()
        {
            if (button != null)
            {
                Object.Destroy(button.gameObject);
            }
            button = null;
            icon = null;
            tooltip = null;
            shownState = -1;
        }

        private static void Hide()
        {
            if (button != null && button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(false);
            }
        }

        private static void Create(InventoryGui gui)
        {
            Button template = gui.m_stackAllButton;
            if (template == null || template.transform.parent == null)
            {
                return;
            }
            GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
            copy.name = ObjectName;
            // Not a second Place stacks: no gamepad key of its own (the copied UIGamePad would press it with Place stacks'
            // key) and no label, only the padlock.
            foreach (UIGamePad pad in copy.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint != null)
                {
                    Object.Destroy(pad.m_hint);
                }
                Object.DestroyImmediate(pad);
            }
            foreach (TMP_Text label in copy.GetComponentsInChildren<TMP_Text>(true))
            {
                label.gameObject.SetActive(false);
            }
            Button made = copy.GetComponent<Button>();
            if (made == null)
            {
                Object.Destroy(copy);
                return;
            }
            made.onClick = new Button.ButtonClickedEvent();
            made.onClick.AddListener(Clicked);
            made.interactable = true;   // never a greyed copy (Place stacks is greyed at a chest he may only look at: card A80)
            Navigation navigation = made.navigation;
            navigation.mode = Navigation.Mode.None;
            made.navigation = navigation;

            var lockObject = new GameObject("ReserveStock_Lock", typeof(RectTransform), typeof(Image));
            var lockRect = (RectTransform)lockObject.transform;
            lockRect.SetParent(copy.transform, false);
            lockRect.anchorMin = new Vector2(0.5f, 0.5f);
            lockRect.anchorMax = new Vector2(0.5f, 0.5f);
            lockRect.pivot = new Vector2(0.5f, 0.5f);
            lockRect.sizeDelta = new Vector2(26f, 26f);
            lockRect.anchoredPosition = Vector2.zero;
            Image picture = lockObject.GetComponent<Image>();
            picture.raycastTarget = false;
            picture.preserveAspect = true;

            // The game's own tooltip, with the look of the window's buttons' (the repair button's, then the craft
            // button's); any other in the window only if neither has one (an item slot's differs, and mods add their own).
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
                Module.Log.LogInfo("The chest window's reserve button has no tooltip: no tooltip of the game's was found in the window.");
            }

            button = made;
            icon = picture;
            tooltip = hint;
            shownState = -1;
        }

        // A button's own tooltip, if it has one with a picture to show; else a plain C# null, so ?? works on it.
        private static UITooltip TooltipOf(Button button)
        {
            UITooltip tooltip = button != null ? button.GetComponent<UITooltip>() : null;
            return tooltip != null && tooltip.m_tooltipPrefab != null ? tooltip : null;
        }

        /// <summary>Square, just right of Place stacks and level with it, in the same panel: set every frame it shows, so
        /// it follows wherever the game or another mod puts Place stacks.</summary>
        internal static void Place(RectTransform rect, RectTransform stack)
        {
            rect.anchorMin = stack.anchorMin;
            rect.anchorMax = stack.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Size, Size);
            rect.anchoredPosition = Beside(stack.anchoredPosition, stack.rect.size, stack.pivot);
        }

        /// <summary>Where the button's middle goes, from Place stacks' position, size and pivot: its right edge, the gap,
        /// half the button; level with its middle.</summary>
        internal static Vector2 Beside(Vector2 stackPosition, Vector2 stackSize, Vector2 stackPivot) =>
            new Vector2(stackPosition.x + stackSize.x * (1f - stackPivot.x) + Gap + Size / 2f,
                        stackPosition.y + stackSize.y * (0.5f - stackPivot.y));

        private static void Clicked()
        {
            Module.Guard("reserving the open chest", () =>
            {
                if (Module.IsOn)
                {
                    Mark.ToggleOpen(GameHooks.OpenChest(InventoryGui.instance));
                }
            });
        }
    }
}
