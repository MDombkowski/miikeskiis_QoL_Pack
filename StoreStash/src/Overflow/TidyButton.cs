using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;   // StoreStash's own Button (the quick stack's) would hide it

namespace StoreStash
{
    /// <summary>
    /// The tidy button in an open chest's window (card A42; off out of the box, MODULES.md rule 4): a copy of the game's
    /// Place stacks button, square, in the column right of Place stacks and outside the window's edge where Reserve Stock's
    /// padlock, the personal chest button (card A80) and the setup button sit: under whichever of them show, else in the
    /// padlock's place; not on another player's chest he may only look at. Its picture is two
    /// arrows (TidyIcon); the game's own tooltip says what it does. A click does what the tidy key does for this chest. It
    /// shows on a chest with a setup (on any chest that can have one while a tidy covers every group), and lives in the
    /// game's chest window, made the first time it shows, and again after a world change rebuilt the window.
    /// </summary>
    internal static class TidyButton
    {
        private const string ObjectName = "StoreStash_TidyButton";

        /// <summary>The two buttons above it in the column, when they show: Reserve Stock's padlock (ReserveStock\src\
        /// WindowButton.cs) and the setup button (SetupButton).</summary>
        private const string PadlockName = "ReserveStock_Button";

        private const string SetupName = "StoreStash_SetupButton";

        private static UIButton button;
        private static UITooltip tooltip;
        private static bool toldNoTooltip;
        private static bool tipStale = true;

        /// <summary>Every frame: shows the button while a chest it can tidy from is open, hides it otherwise, and while
        /// StoreStash, chest setups, Stack Overflow or the button's setting is off.</summary>
        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            Container chest = OverflowSettings.Active && OverflowSettings.ShowButton.Value && gui != null && InventoryGui.IsVisible()
                ? GameHooks.OpenChest(gui)
                : null;
            // Not on another player's chest he may only look at (card A80): a tidy moves its things.
            if (chest == null || !Shows(chest) || !QoLMods.Chests.ChestAccess.MayTouch(chest))
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
            var stack = (RectTransform)gui.m_stackAllButton.transform;
            int above = Showing(stack, PadlockName) + Showing(stack, MineButton.ObjectName) + Showing(stack, SetupName);
            Place((RectTransform)button.transform, stack, above, OverflowSettings.ButtonOffset.Value);
            if (!button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(true);
            }
            if (tipStale && tooltip != null)
            {
                tipStale = false;
                tooltip.Set("Tidy chests set up alike", Tip(Settings.KeyText(OverflowSettings.Key.Value), OverflowSettings.Covers.Value));
            }
        }

        /// <summary>The tooltip's words: what a click does, and the key that does the same.</summary>
        internal static string Tip(string key, TidyCovers covers) =>
            (covers == TidyCovers.EveryGroupInRange
                ? "Moves things between the chests set up alike nearby, every group of them,"
                : "Moves things between the chests set up like this one,") +
            $" so each kind sits in as few of them as possible ({key}).";

        /// <summary>Whether the button shows on this chest: one with a setup a tidy follows; any chest that can have one while
        /// a tidy covers every group.</summary>
        internal static bool Shows(Container chest) =>
            Setups.Settable(chest) && (OverflowSettings.Covers.Value == TidyCovers.EveryGroupInRange || Tidy.AlikeKey(chest) != null);

        /// <summary>The key or what it covers changed: the tooltip is written again when it next shows.</summary>
        internal static void TipChanged() => tipStale = true;

        /// <summary>The undo step after an error: the button goes with its window.</summary>
        internal static void Remove()
        {
            if (button != null)
            {
                Object.Destroy(button.gameObject);
            }
            button = null;
            tooltip = null;
            tipStale = true;
        }

        private static void Hide()
        {
            if (button != null && button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(false);
            }
        }

        // 1 when a button of that name shows beside Place stacks, else 0.
        private static int Showing(RectTransform stack, string name)
        {
            Transform found = stack.parent != null ? stack.parent.Find(name) : null;
            return found != null && found.gameObject.activeInHierarchy ? 1 : 0;
        }

        private static void Create(InventoryGui gui)
        {
            UIButton template = gui.m_stackAllButton;
            if (template == null || template.transform.parent == null)
            {
                return;
            }
            GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
            copy.name = ObjectName;
            // Not a second Place stacks: no gamepad key of its own (the copied UIGamePad would press it with Place stacks'
            // key) and no label, only the picture.
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
            UIButton made = copy.GetComponent<UIButton>();
            if (made == null)
            {
                Object.Destroy(copy);
                return;
            }
            // Its own click, then hidden until Update first shows it: the game's click sound (ButtonSfx) adds itself to
            // onClick as the button is shown, and the copy was made shown (Portion Control's lesson), so the click is replaced
            // first; no keyboard navigation, or a clicked one would keep the keyboard and Enter would press it again.
            made.onClick = new UIButton.ButtonClickedEvent();
            made.onClick.AddListener(Clicked);
            made.interactable = true;   // never a greyed copy (Place stacks is greyed at a chest he may only look at: card A80)
            Navigation navigation = made.navigation;
            navigation.mode = Navigation.Mode.None;
            made.navigation = navigation;
            copy.SetActive(false);

            var picture = new GameObject("StoreStash_TidyArrows", typeof(RectTransform), typeof(Image));
            var pictureRect = (RectTransform)picture.transform;
            pictureRect.SetParent(copy.transform, false);
            pictureRect.anchorMin = pictureRect.anchorMax = pictureRect.pivot = new Vector2(0.5f, 0.5f);
            pictureRect.sizeDelta = new Vector2(26f, 26f);
            pictureRect.anchoredPosition = Vector2.zero;
            Image image = picture.GetComponent<Image>();
            image.sprite = TidyIcon.Sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.color = new Color(0.82f, 0.8f, 0.76f, 1f);

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
                Module.Log.LogInfo("The chest window's tidy button has no tooltip: no tooltip of the game's was found in the window.");
            }

            button = made;
            tooltip = hint;
            tipStale = true;
        }

        // A button's own tooltip, if it has one with a picture to show; else a plain C# null, so ?? works on it.
        private static UITooltip TooltipOf(UIButton of)
        {
            UITooltip found = of != null ? of.GetComponent<UITooltip>() : null;
            return found != null && found.m_tooltipPrefab != null ? found : null;
        }

        /// <summary>Square, right of Place stacks and outside the window's edge, in the same panel: <paramref name="above"/>
        /// buttons (the padlock, the setup button) and a gap each lower than Place stacks' middle; set every frame it shows,
        /// so it follows wherever Place stacks goes.</summary>
        internal static void Place(RectTransform rect, RectTransform stack, int above, Vector2 offset)
        {
            rect.anchorMin = stack.anchorMin;
            rect.anchorMax = stack.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(SetupButton.Size, SetupButton.Size);
            rect.anchoredPosition = Spot(stack.anchoredPosition, stack.rect.size, stack.pivot, above) + offset;
        }

        /// <summary>Where the button's middle goes, from Place stacks' position, size and pivot: right of it by the gap, level
        /// with its middle (the padlock's spot), and one button and a gap lower for each button above it in the column.</summary>
        internal static Vector2 Spot(Vector2 stackPosition, Vector2 stackSize, Vector2 stackPivot, int above) =>
            new Vector2(stackPosition.x + stackSize.x * (1f - stackPivot.x) + SetupButton.Gap + SetupButton.Size / 2f,
                        stackPosition.y + stackSize.y * (0.5f - stackPivot.y) - Mathf.Max(0, above) * (SetupButton.Size + SetupButton.Gap));

        private static void Clicked()
        {
            Module.Guard("tidying chests set up alike from the chest window", () =>
            {
                if (OverflowSettings.Active)
                {
                    Tidy.ButtonClicked(GameHooks.OpenChest(InventoryGui.instance));
                }
            });
        }
    }
}
