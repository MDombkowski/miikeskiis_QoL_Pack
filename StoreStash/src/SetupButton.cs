using System.Linq;
using QoLMods.Catalogue;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;   // StoreStash's own Button (the quick stack's) would hide it

namespace StoreStash
{
    /// <summary>
    /// The setup button in an open chest's window (card A41; on out of the box since card A63, off before): a copy of the game's
    /// Place stacks button, square, just right of Place stacks and outside the window's edge, under Reserve Stock's
    /// padlock and the personal chest button (card A80, MineButton), each where it shows (in the padlock's place while
    /// neither does), the column agreed with 28-B and 29-B, whose buttons sit elsewhere; not on another player's chest he
    /// may only look at. A tag, orange while the chest has a setup; the game's own tooltip says what the chest takes. A click
    /// opens or closes the chest's setup window. It lives in the game's chest window, made the first time it shows, and
    /// again after a world change rebuilt the window.
    /// </summary>
    internal static class SetupButton
    {
        private const string ObjectName = "StoreStash_SetupButton";

        /// <summary>Reserve Stock's padlock button's name (ReserveStock\src\WindowButton.cs): the button sits under it.</summary>
        private const string PadlockName = "ReserveStock_Button";

        /// <summary>The button's width and height, as the padlock's.</summary>
        internal const float Size = 36f;

        /// <summary>The space between Place stacks and the column, and between the padlock and the button.</summary>
        internal const float Gap = 4f;

        private static readonly Color SetUpColor = new Color(1f, 0.72f, 0.26f, 1f);
        private static readonly Color PlainColor = new Color(0.82f, 0.8f, 0.76f, 1f);

        private static UIButton button;
        private static Image icon;
        private static UITooltip tooltip;
        private static bool toldNoTooltip;
        private static string shownFor;

        /// <summary>Every frame: shows the button while a chest that can have a setup is open, hides it otherwise, and while
        /// StoreStash, chest setups or the button's setting is off.</summary>
        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            Container chest = SetupSettings.Active && SetupSettings.ShowButton.Value && gui != null && InventoryGui.IsVisible()
                ? GameHooks.OpenChest(gui)
                : null;
            // Not on another player's chest he may only look at (card A80): its setup is theirs to change.
            if (chest == null || !Setups.Settable(chest) || !QoLMods.Chests.ChestAccess.MayTouch(chest))
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
            // Under the padlock and the personal chest button (card A80), each where it shows.
            Place((RectTransform)button.transform, stack, Showing(stack, PadlockName) + Showing(stack, MineButton.ObjectName), SetupSettings.ButtonOffset.Value);
            if (!button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(true);
            }
            // The record's own text, as stored (no new text made every frame): drawn again only when it changes.
            string state = QoLMods.Chests.ChestSetup.Read(chest);
            if (state != shownFor)
            {
                shownFor = state;
                ItemPicks picks = Setups.Of(state);
                icon.color = picks != null ? SetUpColor : PlainColor;
                if (tooltip != null)
                {
                    string key = Settings.KeyText(SetupSettings.Key.Value);
                    tooltip.Set("Chest setup", picks != null
                        ? $"Takes: {Setups.Describe(picks, 5)}. Click to change what it takes ({key})."
                        : $"Click to set up what this chest takes when you quick stack ({key}).");
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
            shownFor = null;
        }

        private static void Hide()
        {
            if (button != null && button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(false);
            }
            shownFor = null;
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
            // key) and no label, only the tag.
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
            // onClick as the button is shown, and the copy was made shown, with the sound on the click just replaced
            // (Portion Control's lesson; review 1).
            made.onClick = new UIButton.ButtonClickedEvent();
            made.onClick.AddListener(Clicked);
            made.interactable = true;   // never a greyed copy (Place stacks is greyed at a chest he may only look at: card A80)
            Navigation navigation = made.navigation;
            navigation.mode = Navigation.Mode.None;
            made.navigation = navigation;
            copy.SetActive(false);

            var picture = new GameObject("StoreStash_SetupTag", typeof(RectTransform), typeof(Image));
            var pictureRect = (RectTransform)picture.transform;
            pictureRect.SetParent(copy.transform, false);
            pictureRect.anchorMin = pictureRect.anchorMax = pictureRect.pivot = new Vector2(0.5f, 0.5f);
            pictureRect.sizeDelta = new Vector2(26f, 26f);
            pictureRect.anchoredPosition = Vector2.zero;
            Image image = picture.GetComponent<Image>();
            image.sprite = SetupIcon.Sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;

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
                Module.Log.LogInfo("The chest window's setup button has no tooltip: no tooltip of the game's was found in the window.");
            }

            button = made;
            icon = image;
            tooltip = hint;
            shownFor = null;
        }

        // A button's own tooltip, if it has one with a picture to show; else a plain C# null, so ?? works on it.
        private static UITooltip TooltipOf(UIButton of)
        {
            UITooltip found = of != null ? of.GetComponent<UITooltip>() : null;
            return found != null && found.m_tooltipPrefab != null ? found : null;
        }

        /// <summary>Square, right of Place stacks and outside the window's edge, in the same panel: under the padlock and the
        /// personal chest button when they show (<paramref name="above"/> of them), in the padlock's place when neither
        /// does; set every frame it shows, so it follows wherever Place stacks goes.</summary>
        internal static void Place(RectTransform rect, RectTransform stack, int above, Vector2 offset)
        {
            rect.anchorMin = stack.anchorMin;
            rect.anchorMax = stack.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Size, Size);
            rect.anchoredPosition = Spot(stack.anchoredPosition, stack.rect.size, stack.pivot, above) + offset;
        }

        /// <summary>Where the button's middle goes, from Place stacks' position, size and pivot: right of it by the gap, level
        /// with its middle (the padlock's spot), and one button and a gap lower when the padlock is there.</summary>
        internal static Vector2 Spot(Vector2 stackPosition, Vector2 stackSize, Vector2 stackPivot, bool underPadlock) =>
            Spot(stackPosition, stackSize, stackPivot, underPadlock ? 1 : 0);

        /// <summary>The column's spot for a button with <paramref name="above"/> buttons above it (the padlock, the personal
        /// chest button): right of Place stacks by the gap, level with its middle, one button and a gap lower for each.</summary>
        internal static Vector2 Spot(Vector2 stackPosition, Vector2 stackSize, Vector2 stackPivot, int above) =>
            new Vector2(stackPosition.x + stackSize.x * (1f - stackPivot.x) + Gap + Size / 2f,
                        stackPosition.y + stackSize.y * (0.5f - stackPivot.y) - Mathf.Max(0, above) * (Size + Gap));

        // 1 when a button of that name shows beside Place stacks, else 0.
        private static int Showing(RectTransform stack, string name)
        {
            Transform found = stack.parent != null ? stack.parent.Find(name) : null;
            return found != null && found.gameObject.activeInHierarchy ? 1 : 0;
        }

        private static void Clicked()
        {
            Module.Guard("opening a chest's setup", () =>
            {
                if (SetupSettings.Active)
                {
                    SetupWindow.Toggle(GameHooks.OpenChest(InventoryGui.instance));
                }
            });
        }
    }
}
