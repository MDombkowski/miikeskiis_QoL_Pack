using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PortionControl
{
    /// <summary>
    /// The chest window's two buttons (card A54, his feedback of 2026-09-30), placed from the game's own layout (its
    /// IngameGui_Inventory prefab, read with the data library's reader: Take all 133 × 40 at the window's top left, the
    /// weight box 80 × 60 hanging outside its bottom right corner):
    /// - "-1", a square copy of the game's Take all, 4 to its right and level with it: everything but one of each kind,
    ///   as Take all takes (Mover.TakeAll). Not in a grave's window: a grave left holding one of each kind never empties,
    ///   so it never goes.
    /// - Max weight, a square copy of Place stacks with the game's own encumbered picture, 4 above the weight box's top (its
    ///   picture, which reaches 16 above the box itself: his first look, 2026-09-30), raised by its setting: as much
    ///   as he can carry without being encumbered (Mover.TakeMaxWeight); a left and a right click each take their
    ///   setting's way (his answer: all it can on the left, one of each kind left on the right).
    /// Both show only while the chest window does, which the game shows only while this computer owns the chest (so the
    /// chest it writes is saved), each with the game's tooltip, greyed while a click would do nothing. Made the first time
    /// a chest is opened, and again after a world change rebuilt the window; switched off, hidden (destroyed after an
    /// error).
    /// </summary>
    internal static class ChestButtons
    {
        private const float MinusWidth = 40f;
        private const float MaxSide = 36f;
        private const float Gap = 4f;

        /// <summary>How often the greyed states are worked out again, in frames: each one walks the chest and his
        /// inventory.</summary>
        private const int StateEvery = 6;

        private static InventoryGui builtFor;
        private static Button minusOne;
        private static Button maxWeight;
        private static UITooltip minusTip;
        private static UITooltip maxTip;
        private static string minusTipText;
        private static string maxTipText;
        private static bool toldNoTooltip;
        private static int stateFrame = -100;
        private static Container stateChest;
        private static bool minusUsable;
        private static bool maxUsable;
        private static LeaveOneOf? minusTipFor;
        private static string minusTipWords;
        private static MaxWeightClick? maxTipLeft;
        private static MaxWeightClick? maxTipRight;
        private static bool? maxTipGrave;
        private static string maxTipWords;
        private static readonly Dictionary<Graphic, Color> ownColours = new Dictionary<Graphic, Color>();

        /// <summary>Every frame (the module's Update).</summary>
        internal static void Update()
        {
            InventoryGui gui = InventoryGui.instance;
            Container chest = Module.IsOn && gui != null && InventoryGui.IsVisible() ? GameHooks.CurrentContainer(gui) : null;
            bool open = chest != null && GameHooks.CanUseChest && gui.m_container != null &&
                        gui.m_container.gameObject.activeInHierarchy && chest.IsOwner();
            bool showMinus = open && Settings.ChestAllButOne.Value && gui.m_takeAllButton != null && chest.GetComponent<TombStone>() == null;
            bool showMax = open && Settings.ChestMaxWeight.Value && gui.m_stackAllButton != null && gui.m_containerWeight != null;
            if (!showMinus && !showMax)
            {
                Hide();
                return;
            }
            if (builtFor != gui)
            {
                Remove();
                builtFor = gui;
            }
            if (showMinus && minusOne == null)   // Unity's own test: gone with the window after a world change
            {
                minusOne = MakeMinusOne(gui);
            }
            if (showMax && maxWeight == null)
            {
                maxWeight = MakeMaxWeight(gui);
            }

            if (Time.frameCount - stateFrame >= StateEvery || chest != stateChest)
            {
                stateFrame = Time.frameCount;
                stateChest = chest;
                WorkOutStates(chest);
            }
            // Both take from the chest, as Take all does: greyed whenever the game's own Take all is (the pack's chest service
            // greys it at a friend's personal chest he lets you only look at, card A80).
            bool takeAllUsable = GameHooks.ChestTouchable(gui);
            Show(minusOne, showMinus, minusUsable && takeAllUsable);
            Show(maxWeight, showMax, maxUsable && takeAllUsable);
            if (showMinus && minusOne != null)
            {
                PlaceMinusOne(gui);
                Tip(minusTip, ref minusTipText, MinusTipWords());
            }
            if (showMax && maxWeight != null)
            {
                PlaceMaxWeight(gui);
                Tip(maxTip, ref maxTipText, MaxTipWords(chest.GetComponent<TombStone>() != null));
            }
        }

        // The tooltips' words, made again only when a setting they name changes.
        private static string MinusTipWords()
        {
            LeaveOneOf which = Settings.ChestLeaveOneOf.Value;
            if (minusTipFor != which)
            {
                minusTipFor = which;
                minusTipWords = "Take all but one of each kind" +
                                (Settings.ChestLeaveOneOf.Value == LeaveOneOf.StackableKindsOnly ? " that stacks" : string.Empty) +
                                ": the one left stays in that kind's first stack.";
            }
            return minusTipWords;
        }

        private static string MaxTipWords(bool grave)
        {
            MaxWeightClick left = Settings.ChestMaxWeightLeftClick.Value;
            MaxWeightClick right = Settings.ChestMaxWeightRightClick.Value;
            if (maxTipLeft != left || maxTipRight != right || maxTipGrave != grave)
            {
                maxTipLeft = left;
                maxTipRight = right;
                maxTipGrave = grave;
                maxTipWords = grave
                    ? "Max weight: take as much as you can carry without being encumbered. In a grave, either click takes all it can."
                    : "Max weight: take as much as you can carry without being encumbered. Left click: " +
                      Describe(Settings.ChestMaxWeightLeftClick.Value) + ". Right click: " + Describe(Settings.ChestMaxWeightRightClick.Value) + ".";
            }
            return maxTipWords;
        }

        private static string Describe(MaxWeightClick click) =>
            click == MaxWeightClick.LeaveOneOfEachKind ? "leaves one of each kind" : "all it can";

        /// <summary>Whether a click leaves one of each kind: its setting's way, but never in a grave, which a kind left
        /// in keeps from ever emptying and going (review round 1: the reason the -1 isn't there).</summary>
        private static bool LeavesOne(MaxWeightClick click, Container chest) =>
            click == MaxWeightClick.LeaveOneOfEachKind && chest.GetComponent<TombStone>() == null;

        /// <summary>The undo step after an error: the buttons go with their window.</summary>
        internal static void Remove()
        {
            if (minusOne != null)
            {
                Object.Destroy(minusOne.gameObject);
            }
            if (maxWeight != null)
            {
                Object.Destroy(maxWeight.gameObject);
            }
            minusOne = null;
            maxWeight = null;
            minusTip = null;
            maxTip = null;
            minusTipText = null;
            maxTipText = null;
            builtFor = null;
            stateChest = null;
            ownColours.Clear();
        }

        private static void Hide()
        {
            Show(minusOne, false, false);
            Show(maxWeight, false, false);
            stateChest = null;
        }

        private static void Show(Button button, bool show, bool usable)
        {
            if (button == null)
            {
                return;
            }
            if (button.gameObject.activeSelf != show)
            {
                button.gameObject.SetActive(show);
            }
            if (show && button.interactable != usable)
            {
                button.interactable = usable;
                // Its label and picture faded while it can't be clicked, each put back as it was after (the button's
                // own picture greys by itself).
                foreach (Graphic part in button.GetComponentsInChildren<Graphic>(true))
                {
                    if (part.gameObject == button.gameObject)
                    {
                        continue;
                    }
                    if (!ownColours.TryGetValue(part, out Color own))
                    {
                        own = part.color;
                        ownColours[part] = own;
                    }
                    part.color = usable ? own : new Color(own.r * 0.6f, own.g * 0.6f, own.b * 0.6f, own.a * 0.5f);
                }
            }
        }

        private static void Tip(UITooltip tip, ref string shown, string text)
        {
            if (tip != null && shown != text)
            {
                shown = text;
                tip.Set("Portion Control", text);
            }
        }

        // Whether each click would do anything now.
        private static void WorkOutStates(Container chest)
        {
            minusUsable = false;
            maxUsable = false;
            Player player = Player.m_localPlayer;
            Inventory from = chest != null ? chest.GetInventory() : null;
            if (player == null || from == null)
            {
                return;
            }
            Inventory mine = player.GetInventory();
            Dictionary<ItemDrop.ItemData, int> keep = Mover.OneOfEachKind(from, Settings.ChestLeaveOneOf.Value == LeaveOneOf.StackableKindsOnly);
            bool anyTakesAll = !LeavesOne(Settings.ChestMaxWeightLeftClick.Value, chest) || !LeavesOne(Settings.ChestMaxWeightRightClick.Value, chest);
            float limit = player.GetMaxCarryWeight();
            float free = Settings.LeaveFree.Value;
            float total = mine.GetTotalWeight();
            foreach (ItemDrop.ItemData item in from.GetAllItems())
            {
                int kept = keep.TryGetValue(item, out int k) ? k : 0;
                if (item.m_stack - kept > 0)
                {
                    minusUsable = true;
                }
                int cap = anyTakesAll ? item.m_stack : item.m_stack - kept;
                if (!maxUsable && cap > 0 && HasRoom(mine, item) &&
                    Weights.MostSpread(mine.GetAllItems(), total, item, 1, limit, free) >= 1)
                {
                    maxUsable = true;
                }
                if (minusUsable && maxUsable)
                {
                    return;
                }
            }
        }

        // Room for at least one of the item in his inventory, as Mover.Across would put it.
        private static bool HasRoom(Inventory mine, ItemDrop.ItemData item)
        {
            foreach (ItemDrop.ItemData stack in mine.GetAllItems())
            {
                if (stack.m_stack < stack.m_shared.m_maxStackSize && stack.IsSameType(item))
                {
                    return true;
                }
            }
            return GameHooks.EmptyCell(mine, item).x >= 0;
        }

        // ---- The clicks

        private static void TakeAllButOne()
        {
            if (!Ready(out InventoryGui gui, out Container chest, out Player player))
            {
                return;
            }
            GameHooks.ClearCursor(gui);
            Inventory from = chest.GetInventory();
            Dictionary<ItemDrop.ItemData, int> keep = Mover.OneOfEachKind(from, Settings.ChestLeaveOneOf.Value == LeaveOneOf.StackableKindsOnly);
            Mover.TakeAll(from, player.GetInventory(), keep, GameHooks.EmptyCell);
            stateFrame = -100;
        }

        private static void TakeMaxWeight(bool rightClick)
        {
            if (!Ready(out InventoryGui gui, out Container chest, out Player player))
            {
                return;
            }
            MaxWeightClick how = rightClick ? Settings.ChestMaxWeightRightClick.Value : Settings.ChestMaxWeightLeftClick.Value;
            GameHooks.ClearCursor(gui);
            Inventory from = chest.GetInventory();
            Inventory mine = player.GetInventory();
            Dictionary<ItemDrop.ItemData, int> keep = LeavesOne(how, chest)
                ? Mover.OneOfEachKind(from, Settings.ChestLeaveOneOf.Value == LeaveOneOf.StackableKindsOnly)
                : null;
            float limit = player.GetMaxCarryWeight();
            bool wasOver = mine.GetTotalWeight() > limit;
            int moved = Mover.TakeMaxWeight(from, mine, keep, limit, Settings.LeaveFree.Value, GameHooks.EmptyCell,
                                            inventory => inventory.GetTotalWeight());
            if (moved > 0)
            {
                // The game's own sound for a move between inventories (its Ctrl + click).
                gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
            }
            if (!wasOver && mine.GetTotalWeight() > limit)
            {
                Module.Log.LogWarning($"Max weight left you over your limit ({mine.GetTotalWeight()} of {limit}): please report this.");
            }
            stateFrame = -100;
        }

        // The chest window still shows a chest this computer owns, and he can take from it (not mid-teleport, as the
        // game's Take all asks; not while the game's Take all is greyed).
        private static bool Ready(out InventoryGui gui, out Container chest, out Player player)
        {
            gui = InventoryGui.instance;
            chest = gui != null ? GameHooks.CurrentContainer(gui) : null;
            player = Player.m_localPlayer;
            return gui != null && chest != null && player != null && chest.IsOwner() && !player.IsTeleporting() &&
                   gui.m_container != null && gui.m_container.gameObject.activeInHierarchy && GameHooks.CanUseChest && GameHooks.ChestTouchable(gui);
        }

        // ---- The buttons

        private static Button MakeMinusOne(InventoryGui gui)
        {
            Button made = Copy(gui.m_takeAllButton, "PortionControl_ChestMinusOne", TakeAllButOne, gui, out minusTip);
            if (made == null)
            {
                return null;
            }
            TMP_Text label = made.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "-1";
                label.gameObject.SetActive(true);
            }
            return made;
        }

        private static Button MakeMaxWeight(InventoryGui gui)
        {
            Button made = Copy(gui.m_stackAllButton, "PortionControl_ChestMaxWeight", () => TakeMaxWeight(false), gui, out maxTip);
            if (made == null)
            {
                return null;
            }
            foreach (TMP_Text label in made.GetComponentsInChildren<TMP_Text>(true))
            {
                label.gameObject.SetActive(false);
            }
            var picture = new GameObject("PortionControl_EncumberedIcon", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)picture.transform;
            rect.SetParent(made.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(MaxSide - 8f, MaxSide - 8f);
            Image icon = picture.GetComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            StatusEffect encumbered = ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(SEMan.s_statusEffectEncumbered) : null;
            icon.sprite = encumbered != null ? encumbered.m_icon : null;
            if (icon.sprite == null)
            {
                // No picture found: the button says what it does instead.
                Object.Destroy(picture);
                TMP_Text label = made.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.text = "Max";
                    label.gameObject.SetActive(true);
                }
                Module.Log.LogInfo("The game's encumbered picture wasn't found: the chest window's Max weight button says \"Max\" instead.");
            }
            RightClick.On(made, () => Module.Guard("taking as much as you can carry", () => TakeMaxWeight(true)));
            return made;
        }

        /// <summary>A copy of one of the chest window's own buttons: its picture, font and click sound; no gamepad key (a
        /// copied one would press it with the original's key), never the selected object, our click, and the game's
        /// tooltip. Made switched off, so the click sound hooks our click as it's first shown.</summary>
        private static Button Copy(Button template, string name, System.Action clicked, InventoryGui gui, out UITooltip tip)
        {
            tip = null;
            if (template == null || template.transform.parent == null)
            {
                return null;
            }
            GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent, false);
            copy.name = name;
            copy.SetActive(false);
            foreach (UIGamePad pad in copy.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint != null)
                {
                    Object.Destroy(pad.m_hint);
                }
                Object.DestroyImmediate(pad);
            }
            Button made = copy.GetComponent<Button>();
            if (made == null)
            {
                Object.Destroy(copy);
                return null;
            }
            made.onClick = new Button.ButtonClickedEvent();
            made.onClick.AddListener(() => Module.Guard("using the chest window's buttons", clicked));
            made.navigation = new Navigation { mode = Navigation.Mode.None };
            UITooltip source = TooltipOf(gui.m_repairButton) ?? TooltipOf(gui.m_craftButton);
            if (source != null)
            {
                tip = copy.GetComponent<UITooltip>();
                if (tip == null)   // Unity's own test
                {
                    tip = copy.AddComponent<UITooltip>();
                }
                tip.m_tooltipPrefab = source.m_tooltipPrefab;
                tip.m_gamepadFocusObject = null;   // a copied one could point at the original's (review round 1)
            }
            else if (!toldNoTooltip)
            {
                toldNoTooltip = true;
                Module.Log.LogInfo("The chest window's buttons have no tooltip: no tooltip of the game's was found in the window.");
            }
            return made;
        }

        // A button's own tooltip, if it has one with a picture to show; else a plain C# null, so ?? works on it.
        private static UITooltip TooltipOf(Button source)
        {
            UITooltip found = source != null ? source.GetComponent<UITooltip>() : null;
            return found != null && found.m_tooltipPrefab != null ? found : null;
        }

        // Right of Take all, level with it, as tall as it (read every frame: it follows the window if a mod moves Take all).
        private static void PlaceMinusOne(InventoryGui gui)
        {
            var takeAll = (RectTransform)gui.m_takeAllButton.transform;
            var rect = (RectTransform)minusOne.transform;
            float height = takeAll.rect.height;
            var at = new Vector2(takeAll.anchoredPosition.x + (1f - takeAll.pivot.x) * takeAll.rect.width + Gap + MinusWidth / 2f,
                                 takeAll.anchoredPosition.y + (0.5f - takeAll.pivot.y) * height);
            Set(rect, takeAll.anchorMin, takeAll.anchorMax, at, new Vector2(MinusWidth, height));
        }

        // Above the chest's weight box, centred on it: above the highest of the box and what is drawn in it, since the box's
        // weight picture is centred on the box's top edge and reaches 16 above it (IngameGui_Inventory: the box 80 × 60, its
        // picture 32 × 32 held 30 above its middle), and the button on the box's own edge covered the picture's top (his
        // first look, 2026-09-30); then raised by his setting. If another mod moves the box, the button goes with it.
        private static void PlaceMaxWeight(InventoryGui gui)
        {
            var box = gui.m_containerWeight.transform.parent as RectTransform ?? gui.m_containerWeight.rectTransform;
            var rect = (RectTransform)maxWeight.transform;
            if (box.parent != null && rect.parent != box.parent)
            {
                rect.SetParent(box.parent, false);   // beside the box, never in it: so its own height never counts in TopOf
            }
            var at = new Vector2(box.anchoredPosition.x + (0.5f - box.pivot.x) * box.rect.width,
                                 box.anchoredPosition.y + TopOf(box) + Gap + Settings.ChestMaxWeightRaise.Value + MaxSide / 2f);
            Set(rect, box.anchorMin, box.anchorMax, at, new Vector2(MaxSide, MaxSide));
        }

        /// <summary>How far above its own pivot a box reaches, what is drawn in it included (its children that show and draw
        /// something, each by its own rect and place), in its parent's units.</summary>
        private static float TopOf(RectTransform box)
        {
            float top = box.rect.yMax;
            foreach (Transform child in box)
            {
                if (child is RectTransform part && part.gameObject.activeSelf && part.GetComponentInChildren<Graphic>() != null)
                {
                    top = Mathf.Max(top, part.localPosition.y + part.rect.yMax * part.localScale.y);
                }
            }
            return top * box.localScale.y;
        }

        private static void Set(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 at, Vector2 size)
        {
            if (rect.anchorMin != anchorMin || rect.anchorMax != anchorMax)
            {
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
            }
            if (rect.pivot != new Vector2(0.5f, 0.5f))
            {
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
            if (rect.sizeDelta != size)
            {
                rect.sizeDelta = size;
            }
            if (rect.anchoredPosition != at)
            {
                rect.anchoredPosition = at;
            }
        }
    }
}
