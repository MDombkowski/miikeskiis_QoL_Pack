using System.Linq;
using BepInEx.Configuration;
using QoLMods.Chests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UIButton = UnityEngine.UI.Button;   // StoreStash's own Button (the quick stack's) would hide it

namespace StoreStash
{
    /// <summary>
    /// The personal chest button in an open chest's window (card A80, his words: "a container button below the Reserve
    /// Stock lock icon to toggle a container's status as a Personal Chest or not"; on out of the box at his word): a copy
    /// of the game's Place stacks button, square, in the column right of Place stacks and outside the window's edge, directly
    /// under Reserve Stock's padlock (in the padlock's place while there's none), above the setup and tidy buttons. Its
    /// picture is a person (MineIcon): gold (the colour of "Yours") while the chest is his, pale while it's nobody's, blue
    /// while someone else marked it. A click does what Left Alt + H does at the open chest (Mine.PressOn): marks it as his,
    /// takes his mark off, or on someone else's says only they can take it off. The game's own tooltip says what a click
    /// does and, on his own chest, what other players may do there. It lives in the game's chest window, made the first
    /// time it shows, and again after a world change rebuilt the window.
    /// </summary>
    internal static class MineButton
    {
        /// <summary>Its Unity object's name: the setup and tidy buttons find it by this name to sit under it.</summary>
        internal const string ObjectName = "StoreStash_PersonalChestButton";

        /// <summary>Reserve Stock's padlock button's name (ReserveStock\src\WindowButton.cs): the button sits under it.</summary>
        private const string PadlockName = "ReserveStock_Button";

        /// <summary>The colour while nobody marked the chest, as the setup button's plain tag.</summary>
        internal static readonly Color PlainColor = new Color(0.82f, 0.8f, 0.76f, 1f);

        /// <summary>The colour while someone else marked it.</summary>
        internal static readonly Color TheirsColor = new Color(0.55f, 0.7f, 1f, 1f);

        private static UIButton button;
        private static Image icon;
        private static UITooltip tooltip;
        private static bool toldNoTooltip;
        private static bool tipStale = true;
        private static long shownMark = -1L;
        private static Color shownColor;

        /// <summary>A setting its tooltip or colour names changed (the key, what others may do, the colour of Yours): both
        /// are made again when it next shows.</summary>
        internal static void TipChanged() => tipStale = true;

        /// <summary>Every frame: shows the button while a chest that can carry the mark is open, as its mark stands; hides it
        /// otherwise, and while StoreStash or its setting is off.</summary>
        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            Container chest = Module.IsOn && MineSettings.Button.Value && gui != null && InventoryGui.IsVisible()
                ? GameHooks.OpenChest(gui)
                : null;
            if (chest == null || !Mine.KeyActs(chest, ChestClaims.PlayerId()))
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
            Transform padlock = stack.parent != null ? stack.parent.Find(PadlockName) : null;
            Place((RectTransform)button.transform, stack, padlock != null && padlock.gameObject.activeInHierarchy ? 1 : 0,
                  MineSettings.ButtonOffset.Value);
            if (!button.gameObject.activeSelf)
            {
                button.gameObject.SetActive(true);
            }
            long me = ChestClaims.PlayerId();
            long mark = ChestMine.MarkOf(chest);
            Color color = Look(mark, me);
            if (mark != shownMark || color != shownColor || tipStale)
            {
                shownMark = mark;
                shownColor = color;
                tipStale = false;
                icon.color = color;
                if (tooltip != null)
                {
                    KeyboardShortcut key = Settings.MineKey.Value;   // with no key set, the tooltip names none (not "None")
                    tooltip.Set("Personal chest", Tip(mark, me, key.MainKey == KeyCode.None ? string.Empty : Settings.KeyText(key), MineSettings.Others.Value,
                                                      MineSettings.ModsFollow.Value, Mine.Whose(chest)));
                }
            }
        }

        /// <summary>Its colour: gold while the chest is his (the colour of "Yours"), blue while someone else's, pale while
        /// nobody's.</summary>
        internal static Color Look(long mark, long me) =>
            mark == 0L ? PlainColor : mark == me ? Settings.MineColor.Value : TheirsColor;

        /// <summary>The tooltip's words: what a click does, with the key that does the same (none named when
        /// <paramref name="key"/> is empty), and on his own chest what other players may do there.</summary>
        internal static string Tip(long mark, long me, string key, OthersAtMyChests others, bool modsFollow, string whose)
        {
            key = string.IsNullOrEmpty(key) ? string.Empty : $" ({key})";
            if (mark != 0L && mark != me)
            {
                return $"{whose} personal chest: only they can take their mark off.";
            }
            if (mark == 0L)
            {
                return $"Click to mark this chest as yours{key}: your personal quick stack fills it, and other players' quick " +
                       "stacks leave it alone." + Others(others, modsFollow, marked: false);
            }
            return $"Yours: click to take your mark off{key}." + Others(others, modsFollow, marked: true);
        }

        // What other players may do at it, as his setting says: on his chest, now; on one not yet marked, once it's his.
        private static string Others(OthersAtMyChests others, bool modsFollow, bool marked)
        {
            string they = marked ? "Other players" : "Once it's yours, other players";
            switch (others)
            {
                case OthersAtMyChests.LookDontTouch:
                    return $" {they} can look inside but not touch" + (modsFollow ? "." : " (their mods may still use it).");
                case OthersAtMyChests.KeepThemOut:
                    return $" {they} can't open it" + (modsFollow ? "." : " (their mods may still use it).");
                default:
                    return marked ? " Other players open it as usual." : string.Empty;
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
            tipStale = true;
            shownMark = -1L;
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
            // onClick as the button is shown, and the copy was made shown (Portion Control's lesson); no keyboard navigation,
            // or a clicked one would keep the keyboard and Enter would press it again. Always clickable, even when Place
            // stacks is greyed (another player's chest he may only look at): there it only says whose the chest is.
            made.onClick = new UIButton.ButtonClickedEvent();
            made.onClick.AddListener(Clicked);
            made.interactable = true;
            Navigation navigation = made.navigation;
            navigation.mode = Navigation.Mode.None;
            made.navigation = navigation;
            copy.SetActive(false);

            var picture = new GameObject("StoreStash_PersonalChestPerson", typeof(RectTransform), typeof(Image));
            var pictureRect = (RectTransform)picture.transform;
            pictureRect.SetParent(copy.transform, false);
            pictureRect.anchorMin = pictureRect.anchorMax = pictureRect.pivot = new Vector2(0.5f, 0.5f);
            pictureRect.sizeDelta = new Vector2(26f, 26f);
            pictureRect.anchoredPosition = Vector2.zero;
            Image image = picture.GetComponent<Image>();
            image.sprite = MineIcon.Sprite;
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
                Module.Log.LogInfo("The chest window's personal chest button has no tooltip: no tooltip of the game's was found in the window.");
            }

            button = made;
            icon = image;
            tooltip = hint;
            tipStale = true;
            shownMark = -1L;
        }

        // A button's own tooltip, if it has one with a picture to show; else a plain C# null, so ?? works on it.
        private static UITooltip TooltipOf(UIButton of)
        {
            UITooltip found = of != null ? of.GetComponent<UITooltip>() : null;
            return found != null && found.m_tooltipPrefab != null ? found : null;
        }

        /// <summary>Square, right of Place stacks and outside the window's edge, in the same panel: <paramref name="above"/>
        /// buttons (the padlock) and a gap each lower than Place stacks' middle; set every frame it shows, so it follows
        /// wherever Place stacks goes (and the padlock, as Reserve Stock places it from Place stacks).</summary>
        internal static void Place(RectTransform rect, RectTransform stack, int above, Vector2 offset)
        {
            rect.anchorMin = stack.anchorMin;
            rect.anchorMax = stack.anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(SetupButton.Size, SetupButton.Size);
            rect.anchoredPosition = SetupButton.Spot(stack.anchoredPosition, stack.rect.size, stack.pivot, above) + offset;
        }

        private static void Clicked()
        {
            Module.Guard("marking the open chest as yours from its button", () =>
            {
                Container chest = Module.IsOn ? GameHooks.OpenChest(InventoryGui.instance) : null;
                if (chest != null && Mine.KeyActs(chest, ChestClaims.PlayerId()))
                {
                    Mine.PressOn(chest);
                }
            });
        }
    }

    /// <summary>
    /// The personal chest button's picture: a person, head and shoulders, drawn as the quick stack's picture is (Icon):
    /// white with a dark rim, so the button's colour tints it.
    /// </summary>
    internal static class MineIcon
    {
        private static Sprite sprite;

        /// <summary>Drawn the first time it's asked for, and again if Unity has destroyed it.</summary>
        internal static Sprite Sprite => sprite != null ? sprite : (sprite = Icon.Make("StoreStash_PersonalChestIcon", Pixels()));

        /// <summary>The pixels, as Icon.Pixels gives them, of this picture.</summary>
        internal static Color[] Pixels() => Icon.Pixels(Distance);

        /// <summary>The signed distance to the picture: a round head above rounded shoulders, a gap between them, clear of
        /// the picture's edges by more than the rim.</summary>
        internal static float Distance(Vector2 at)
        {
            float head = (at - new Vector2(16f, 21.5f)).magnitude - 6f;
            float shoulders = Icon.Box(at, new Vector2(16f, 7.5f), new Vector2(11f, 5.5f), 5f);
            float gap = (at - new Vector2(16f, 21.5f)).magnitude - 8f;   // shoulders cut back around the head
            return Mathf.Min(head, Mathf.Max(shoulders, -gap));
        }
    }
}
