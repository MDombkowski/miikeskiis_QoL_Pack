using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Jotunn.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PortionControl
{
    /// <summary>
    /// The buttons in the game's split window, built once from the game's own OK button (its look, its sound) inside the
    /// window's panel, and brought up to date every frame the window shows, so every setting works at once:
    /// - the amount box, beside the "12/50" amount, sets the slider as he types; Enter takes it;
    /// - the slider buttons, Min, -1, 50%, +1 and Max, only move the slider;
    /// - the one-click buttons (percentages, fixed amounts, All -1, Max weight) set the slider and run the game's own OK,
    ///   so the amount goes on the cursor and the game moves the items when he drops them (his answer 11 on A17);
    /// - a right-click on a percentage or a fixed amount splits the stack into stacks of it (his feedback of 2026-09-30),
    ///   moved by this mod (Mover.IntoStacks);
    /// - the lists of percentages and fixed amounts are the general settings', or his amounts by item, category and stack
    ///   size (AmountRules) where he set some.
    /// To make room the window grows taller, and the parts of it above the slider are lifted by half as much, so they keep
    /// their place from its top and the new rows open between the slider and OK. Switched off, the window goes back to the
    /// game's own size and positions, and every button of ours is hidden (destroyed after an error).
    /// </summary>
    internal static class Window
    {
        private const float RowHeight = 32f;
        private const float Gap = 6f;
        private const float SideInner = 196f;
        private const float SidePadding = 10f;

        /// <summary>How far outside its rect the game draws a panel's wooden frame (the window's frame reaches 20 beyond
        /// each edge), so the side panel's frame and the window's don't overlap.</summary>
        private const float FrameOverhang = 20f;

        private static readonly Color Faded = new Color(1f, 1f, 1f, 0.35f);

        // The game's window, as the game made it.
        private static InventoryGui gui;
        private static SplitDialog dialog;
        private static RectTransform panel;
        private static RectTransform amountRect;
        private static Button template;
        private static readonly List<RectTransform> lifted = new List<RectTransform>();
        private static readonly List<float> liftedY = new List<float>();
        private static Vector2 panelSize;
        private static float okTopFromBottom;
        private static float liftedBottomFromMiddle;
        private static float grownBy;

        // Ours: everything under one object in the window's panel.
        private static GameObject root;
        private static RectTransform rows;
        private static RectTransform side;
        private static RectTransform sideRows;
        private static InputField box;
        private static readonly Button[] sliderButtons = new Button[5];
        private static readonly List<Button> percentButtons = new List<Button>();
        private static readonly List<Button> fixedButtons = new List<Button>();
        private static Button allButOne;
        private static Button maxWeight;

        // The stack the window splits, and what the buttons were last set for.
        private static ItemDrop.ItemData item;
        private static Inventory source;
        private static string typed;
        private static int typedValue = -1;
        private static string lastLayout;
        private static string percentText;
        private static string fixedText;
        private static List<int> generalPercents = new List<int>();
        private static List<int> generalFixeds = new List<int>();
        private static List<int> percents = new List<int>();
        private static List<int> fixeds = new List<int>();
        private static AmountRules.Parsed rules;
        private static string rulesItems;
        private static string rulesCategories;
        private static string rulesSizes;
        private static bool toldCantSplit;
        private static readonly List<KeyValuePair<int, int>> percentShown = new List<KeyValuePair<int, int>>();
        private static int maxWeightAmount;
        private static int boxFrame = -10;
        private static int enterFrame = -10;
        private static bool maxWeightMine;

        /// <summary>A button placed in a row, with its width.</summary>
        private struct Placed
        {
            internal Button Button;
            internal float Width;

            internal Placed(Button button, float width)
            {
                Button = button;
                Width = width;
            }
        }

        /// <summary>Called as the game opens the window on a stack (after it has set it up).</summary>
        internal static void Opened(InventoryGui opener, ItemDrop.ItemData stack, Inventory from)
        {
            gui = opener;
            item = stack;
            source = from;
            typed = null;
            typedValue = -1;
            if (!Build(opener.m_splitDialog))
            {
                return;
            }
            if (box != null)
            {
                box.SetTextWithoutNotify(string.Empty);
            }
            Refresh();
            if (box != null && Settings.AmountBox.Value && Settings.TypeAtOnce.Value)
            {
                box.Select();
                box.ActivateInputField();
            }
        }

        /// <summary>Every frame (the module's Update, also while it is switched off).</summary>
        internal static void Update()
        {
            if (!Module.IsOn || Module.SplitWindowStandsAside)
            {
                PutBack();
                return;
            }
            InventoryGui current = InventoryGui.instance;
            SplitDialog shown = current != null ? current.m_splitDialog : null;
            if (shown == null || !shown.IsActive)
            {
                return;
            }
            // The game keeps the stack it splits while the window shows; follow it, so a window opened while the mod was
            // off (or by another route) gets its buttons too.
            ItemDrop.ItemData splitting = GameHooks.SplitItem(current);
            Inventory from = GameHooks.SplitInventory(current);
            if (splitting == null || from == null)
            {
                return;
            }
            if (current != gui || splitting != item || from != source)
            {
                gui = current;
                item = splitting;
                source = from;
                typed = null;
                typedValue = -1;
                if (box != null)
                {
                    box.SetTextWithoutNotify(string.Empty);
                }
            }
            if (!Build(shown))
            {
                return;
            }
            ApplyTyped();
            Refresh();
            Keys();
        }

        /// <summary>Before the game reads the window's keys: apply what was typed, and while the box has the keyboard, take
        /// Enter here and keep the game from reading the digits too. True: let the game read its keys.</summary>
        internal static bool BeforeGameKeys(InventoryGui reading)
        {
            if (Module.SplitWindowStandsAside)
            {
                return true;
            }
            ApplyTyped();
            if (box == null || reading != gui)
            {
                return true;
            }
            if (enterFrame == Time.frameCount - 1)
            {
                // Enter came while the box still had the keyboard, last frame: now the box holds every digit typed
                // with it, so its number is the one to take (last look after review round 2).
                enterFrame = -10;
                SetSlider(box.text);
                if (Valid())
                {
                    GameHooks.SplitOk(gui);
                }
                return false;
            }
            if (!HadKeyboard())
            {
                return true;
            }
            // The box has the keyboard, or gave it up this frame or the last (Enter ends its edit, and Unity may have
            // handled that before this): a digit typed in the same frame as Enter was the box's, so the game's own
            // reading of the digits must not see it too (review round 2).
            if (ZInput.GetKeyDown(KeyCode.Return, false) || ZInput.GetKeyDown(KeyCode.KeypadEnter, false))
            {
                if (box.isFocused)
                {
                    // Unity hasn't given the box this frame's keys yet: take its number next frame.
                    enterFrame = Time.frameCount;
                    return false;
                }
                SetSlider(box.text);
                if (Valid())
                {
                    GameHooks.SplitOk(gui);
                }
                return false;
            }
            return !box.isFocused;
        }

        /// <summary>True while the amount box has the keyboard, and for the frame after it gave it up.</summary>
        private static bool HadKeyboard()
        {
            if (box == null)
            {
                return false;
            }
            if (box.isFocused)
            {
                boxFrame = Time.frameCount;
                return true;
            }
            return Time.frameCount - boxFrame <= 1;
        }

        /// <summary>After an error: the window as the game made it, and nothing of ours left in it.</summary>
        internal static void Remove()
        {
            PutBack();
            if (root != null)
            {
                Object.Destroy(root);
            }
            root = null;
            box = null;
            allButOne = null;
            maxWeight = null;
            Array.Clear(sliderButtons, 0, sliderButtons.Length);
            percentButtons.Clear();
            fixedButtons.Clear();
            lastLayout = null;
        }

        /// <summary>The window at the game's own size, with nothing of ours showing.</summary>
        private static void PutBack()
        {
            if (root != null && root.activeSelf)
            {
                root.SetActive(false);
            }
            Grow(0f);
            lastLayout = null;
        }

        /// <summary>Builds our part of the window once per window (a new world makes a new one). False if the game's
        /// window isn't there.</summary>
        private static bool Build(SplitDialog window)
        {
            if (window == null)
            {
                return false;
            }
            if (root != null && dialog == window && panel != null)
            {
                if (!root.activeSelf)
                {
                    root.SetActive(true);
                }
                return true;
            }
            RectTransform windowPanel = GameHooks.Panel(window);
            Button ok = GameHooks.OkButton(window);
            TMP_Text amount = GameHooks.AmountText(window);
            if (windowPanel == null || ok == null || amount == null)
            {
                return false;
            }
            if (root != null)
            {
                Object.Destroy(root);
            }
            dialog = window;
            panel = windowPanel;
            template = ok;
            amountRect = amount.rectTransform;
            grownBy = 0f;
            lastLayout = null;
            Measure();

            root = new GameObject("PortionControl_Root", typeof(RectTransform));
            root.transform.SetParent(panel, false);
            Stretch((RectTransform)root.transform);

            rows = Container("PortionControl_Rows", root.transform);
            rows.anchorMin = rows.anchorMax = new Vector2(0.5f, 0f);
            rows.pivot = new Vector2(0.5f, 0f);

            side = Container("PortionControl_Side", root.transform);
            side.anchorMin = side.anchorMax = new Vector2(1f, 0.5f);
            side.pivot = new Vector2(0f, 0.5f);
            Frame(side);
            sideRows = Container("PortionControl_SideRows", side);
            Stretch(sideRows);

            string[] sliderNames = { "Min", "-1", "50%", "+1", "Max" };
            for (int index = 0; index < sliderButtons.Length; index++)
            {
                int which = index;
                sliderButtons[index] = MakeButton("Slider" + index, rows, () => MoveSlider(which));
                SetLabel(sliderButtons[index], sliderNames[index]);
            }
            percentButtons.Clear();
            fixedButtons.Clear();
            allButOne = MakeButton("AllButOne", rows, () => Take(Amounts.AllButOne(item.m_stack)));
            SetLabel(allButOne, "All -1");
            maxWeight = MakeButton("MaxWeight", rows, TakeMaxWeight);
            box = MakeBox(root.transform);
            return true;
        }

        /// <summary>Reads the window as the game made it: its size, where OK's top sits above its bottom, and the parts
        /// held at its middle (the icon, the amount, the slider), which are lifted as the window grows.</summary>
        private static void Measure()
        {
            panelSize = panel.sizeDelta;
            lifted.Clear();
            liftedY.Clear();
            float lowest = float.MaxValue;
            foreach (Transform child in panel)
            {
                if (!(child is RectTransform rect) || child.name.StartsWith("PortionControl_", StringComparison.Ordinal))
                {
                    continue;
                }
                if (Mathf.Approximately(rect.anchorMin.y, 0.5f) && Mathf.Approximately(rect.anchorMax.y, 0.5f))
                {
                    lifted.Add(rect);
                    liftedY.Add(rect.anchoredPosition.y);
                    if (child.gameObject.activeSelf)
                    {
                        lowest = Mathf.Min(lowest, rect.anchoredPosition.y - rect.sizeDelta.y * rect.pivot.y);
                    }
                }
            }
            liftedBottomFromMiddle = lowest == float.MaxValue ? -panelSize.y / 4f : lowest;
            RectTransform ok = (RectTransform)template.transform;
            okTopFromBottom = Mathf.Approximately(ok.anchorMin.y, 0f) && Mathf.Approximately(ok.anchorMax.y, 0f)
                ? ok.anchoredPosition.y + ok.sizeDelta.y * (1f - ok.pivot.y)
                : 60f;
        }

        /// <summary>Makes the window <paramref name="by"/> taller than the game made it (0: as the game made it), lifting
        /// the parts held at its middle by half as much.</summary>
        private static void Grow(float by)
        {
            if (panel == null || Mathf.Approximately(by, grownBy))
            {
                return;
            }
            panel.sizeDelta = new Vector2(panelSize.x, panelSize.y + by);
            for (int index = 0; index < lifted.Count; index++)
            {
                if (lifted[index] != null)
                {
                    Vector2 at = lifted[index].anchoredPosition;
                    lifted[index].anchoredPosition = new Vector2(at.x, liftedY[index] + by / 2f);
                }
            }
            grownBy = by;
        }

        /// <summary>Brings every button up to date with the stack, his inventory and the settings, and lays them out
        /// again when what shows has changed.</summary>
        private static void Refresh()
        {
            if (item == null || root == null)
            {
                return;
            }
            int stack = item.m_stack;
            if (stack >= 1 && !Mathf.Approximately(dialog.m_splitSlider.maxValue, stack))
            {
                // The stack grew or shrank while the window shows (auto-pickup, say): the slider follows, as the game's
                // own Ctrl + Shift keeps an amount (review round 2).
                dialog.UpdateLimits(stack, true);
            }
            bool pocket = Module.CurrencyPocketLoaded && Settings.CurrencyPocket.Value == WithCurrencyPocket.OneClickButtonsHidden;
            bool beside = Settings.Layout.Value == SplitLayout.BesideTheWindow;
            Transform oneClickParent = beside ? sideRows : rows;

            // The slider buttons.
            var sliderRow = new List<Placed>();
            Slider slider = dialog.m_splitSlider;
            bool down = slider.value > slider.minValue;
            bool up = slider.value < slider.maxValue;
            bool[] canMove = { down, down, true, up, up };
            for (int index = 0; index < sliderButtons.Length; index++)
            {
                bool show = Settings.SliderButtons.Value;
                Show(sliderButtons[index], show, canMove[index], rows);
                if (show)
                {
                    sliderRow.Add(new Placed(sliderButtons[index], 58f));
                }
            }

            // The percentages and the fixed amounts: the general lists, or his for this item.
            Lists();
            bool roundUp = Settings.Rounding.Value == PercentRounding.Up;
            percentShown.Clear();
            if (!pocket)
            {
                percentShown.AddRange(Amounts.PercentAmounts(stack, percents, roundUp));
            }
            var percentPlaced = new List<Placed>();
            for (int index = 0; index < Math.Max(percentShown.Count, percentButtons.Count); index++)
            {
                if (index >= percentButtons.Count)
                {
                    int which = index;
                    Button made = MakeButton("Percent" + index, oneClickParent, () => TakePercent(which));
                    RightClick.On(made, () => Module.Guard("splitting the stack", () => SplitPercent(which)));
                    percentButtons.Add(made);
                }
                Button button = percentButtons[index];
                bool show = index < percentShown.Count;
                Show(button, show, true, oneClickParent);
                if (show)
                {
                    KeyValuePair<int, int> shown = percentShown[index];
                    SetLabel(button, Settings.PercentShowsAmount.Value ? $"{shown.Key}% ({shown.Value})" : $"{shown.Key}%");
                    percentPlaced.Add(new Placed(button, beside ? (SideInner - Gap) / 2f : 76f));
                }
            }

            // The fixed amounts.
            var fixedPlaced = new List<Placed>();
            for (int index = 0; index < Math.Max(fixeds.Count, fixedButtons.Count); index++)
            {
                if (index >= fixedButtons.Count)
                {
                    int which = index;
                    Button made = MakeButton("Fixed" + index, oneClickParent, () => TakeFixed(which));
                    RightClick.On(made, () => Module.Guard("splitting the stack", () => SplitFixed(which)));
                    fixedButtons.Add(made);
                }
                Button button = fixedButtons[index];
                bool listed = !pocket && index < fixeds.Count;
                bool fits = listed && Amounts.Fits(fixeds[index], stack);
                bool show = listed && (fits || Settings.TooBig.Value == WhenUnusable.GreyedOut);
                Show(button, show, fits, oneClickParent);
                if (show)
                {
                    SetLabel(button, fixeds[index].ToString());
                    fixedPlaced.Add(new Placed(button, beside ? (SideInner - 2f * Gap) / 3f : 50f));
                }
            }

            // All -1 and Max weight.
            var specialPlaced = new List<Placed>();
            bool showAll = !pocket && Settings.AllButOne.Value;
            Show(allButOne, showAll, true, oneClickParent);
            if (showAll)
            {
                specialPlaced.Add(new Placed(allButOne, beside ? SideInner : 110f));
            }
            bool canTake = false;
            string label = null;
            bool showMax = !pocket && Settings.MaxWeight.Value && MaxWeightState(out canTake, out label);
            Show(maxWeight, showMax, showMax && canTake, oneClickParent);
            if (showMax)
            {
                SetLabel(maxWeight, label);
                specialPlaced.Add(new Placed(maxWeight, beside ? SideInner : 200f));
            }

            Lay(beside, sliderRow, percentPlaced, fixedPlaced, specialPlaced);

            // The amount box, beside the "12/50" amount, placed after the rows so it follows the amount as the window
            // grows (review round 1).
            bool showBox = Settings.AmountBox.Value && box != null;
            if (box != null)
            {
                if (box.gameObject.activeSelf != showBox)
                {
                    box.gameObject.SetActive(showBox);
                }
                if (showBox)
                {
                    RectTransform rect = (RectTransform)box.transform;
                    var at = new Vector2(amountRect.anchoredPosition.x + 118f, amountRect.anchoredPosition.y);
                    if (rect.anchoredPosition != at)
                    {
                        rect.anchoredPosition = at;
                    }
                    if (!box.isFocused && typedValue >= 0 && (int)dialog.m_splitSlider.value != typedValue)
                    {
                        // The slider moved some other way: what was typed no longer says the amount.
                        box.SetTextWithoutNotify(string.Empty);
                        typedValue = -1;
                    }
                    else if (!box.isFocused && typedValue >= 0 && box.text != typedValue.ToString())
                    {
                        // What was typed was more than the stack: the box shows what the slider took (review round 2).
                        box.SetTextWithoutNotify(typedValue.ToString());
                    }
                }
            }
        }

        /// <summary>Places the rows, when what shows has changed: in the window between the slider and OK, and beside it
        /// in its own panel for the BesideTheWindow layout.</summary>
        private static void Lay(bool beside, List<Placed> sliderRow, List<Placed> percentPlaced, List<Placed> fixedPlaced, List<Placed> specialPlaced)
        {
            var inWindow = new List<List<Placed>>();
            var inSide = new List<List<Placed>>();
            if (sliderRow.Count > 0)
            {
                inWindow.Add(sliderRow);
            }
            List<List<Placed>> oneClick = beside ? inSide : inWindow;
            Chunk(oneClick, percentPlaced, beside ? 2 : 4);
            Chunk(oneClick, fixedPlaced, beside ? 3 : 6);
            if (beside)
            {
                foreach (Placed placed in specialPlaced)
                {
                    inSide.Add(new List<Placed> { placed });
                }
            }
            else if (specialPlaced.Count > 0)
            {
                inWindow.Add(specialPlaced);
            }

            string layout = Describe(beside, inWindow, inSide);
            if (layout == lastLayout)
            {
                return;
            }
            lastLayout = layout;

            float height = Place(rows, inWindow);
            float room = liftedBottomFromMiddle + panelSize.y / 2f - okTopFromBottom;
            float grow = height > 0f ? Mathf.Max(0f, height + 2f * Gap - room) : 0f;
            Grow(grow);
            rows.sizeDelta = new Vector2(360f, height);
            float band = room + grow;
            rows.anchoredPosition = new Vector2(0f, okTopFromBottom + Mathf.Max(0f, (band - height) / 2f));

            bool sideShows = inSide.Count > 0;
            if (side.gameObject.activeSelf != sideShows)
            {
                side.gameObject.SetActive(sideShows);
            }
            if (sideShows)
            {
                float sideHeight = Place(sideRows, inSide);
                side.sizeDelta = new Vector2(SideInner + 2f * SidePadding, sideHeight + 2f * SidePadding);
                side.anchoredPosition = new Vector2(2f * FrameOverhang + Gap, 0f);
                sideRows.offsetMin = new Vector2(SidePadding, SidePadding);
                sideRows.offsetMax = new Vector2(-SidePadding, -SidePadding);
            }
        }

        private static void Chunk(List<List<Placed>> into, List<Placed> buttons, int perRow)
        {
            for (int start = 0; start < buttons.Count; start += perRow)
            {
                into.Add(buttons.GetRange(start, Math.Min(perRow, buttons.Count - start)));
            }
        }

        /// <summary>Places rows of buttons top down, each row centred, in a container anchored at its top; returns the
        /// height they take.</summary>
        private static float Place(RectTransform container, List<List<Placed>> placedRows)
        {
            float y = 0f;
            foreach (List<Placed> row in placedRows)
            {
                float total = -Gap;
                foreach (Placed placed in row)
                {
                    total += placed.Width + Gap;
                }
                float x = -total / 2f;
                foreach (Placed placed in row)
                {
                    RectTransform rect = (RectTransform)placed.Button.transform;
                    if (rect.parent != container)
                    {
                        rect.SetParent(container, false);
                    }
                    rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(placed.Width, RowHeight);
                    rect.anchoredPosition = new Vector2(x + placed.Width / 2f, -(y + RowHeight / 2f));
                    x += placed.Width + Gap;
                }
                y += RowHeight + Gap;
            }
            return placedRows.Count > 0 ? y - Gap : 0f;
        }

        private static string Describe(bool beside, List<List<Placed>> inWindow, List<List<Placed>> inSide)
        {
            var text = new System.Text.StringBuilder(beside ? "B" : "A");
            foreach (List<List<Placed>> group in new[] { inWindow, inSide })
            {
                text.Append('|');
                foreach (List<Placed> row in group)
                {
                    text.Append('/');
                    foreach (Placed placed in row)
                    {
                        text.Append(placed.Button.GetInstanceID()).Append(':').Append(placed.Width).Append(',');
                    }
                }
            }
            return text.ToString();
        }

        /// <summary>Max weight's state for the stack now: whether it shows, whether it can be clicked, its label; and the
        /// amount it takes, kept for the click.</summary>
        private static bool MaxWeightState(out bool canTake, out string label)
        {
            canTake = false;
            label = "Max weight";
            maxWeightAmount = 0;
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }
            Inventory mine = player.GetInventory();
            maxWeightMine = source == mine;
            float limit = player.GetMaxCarryWeight();
            float free = Settings.LeaveFree.Value;
            if (maxWeightMine)
            {
                switch (Settings.ForMyOwnStack.Value)
                {
                    case OwnStack.Hidden:
                        return false;
                    case OwnStack.GreyedOut:
                        return true;
                }
                label = "Just enough";
                maxWeightAmount = Weights.JustEnough(mine.GetAllItems(), mine.GetTotalWeight(), item, limit, free);
            }
            else
            {
                maxWeightAmount = Weights.MaxTake(mine.GetAllItems(), mine.GetTotalWeight(), item, limit, free);
            }
            if (maxWeightAmount < 1)
            {
                return Settings.WhenNone.Value == WhenUnusable.GreyedOut;
            }
            canTake = true;
            if (Settings.MaxWeightShowsAmount.Value)
            {
                label += $" ({maxWeightAmount})";
            }
            return true;
        }

        /// <summary>The optional keys, while the window shows and the box doesn't have the keyboard.</summary>
        private static void Keys()
        {
            bool typing = HadKeyboard();
            KeyboardShortcut allKey = Settings.AllButOneKey.Value;
            if (!(typing && TheBoxUses(allKey)) && Settings.Pressed(allKey))
            {
                Press(allButOne);
            }
            KeyboardShortcut maxKey = Settings.MaxWeightKey.Value;
            if (!(typing && TheBoxUses(maxKey)) && Settings.Pressed(maxKey))
            {
                Press(maxWeight);
            }
        }

        /// <summary>The keys the amount box uses while it has the keyboard; any other key still works as a shortcut then
        /// (review round 2: with "type into the box at once", the box has the keyboard from the start).</summary>
        private static bool TheBoxUses(KeyboardShortcut shortcut)
        {
            KeyCode key = shortcut.MainKey;
            bool ctrl = false;
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                ctrl |= modifier == KeyCode.LeftControl || modifier == KeyCode.RightControl;
            }
            return (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) || (key >= KeyCode.Keypad0 && key <= KeyCode.KeypadEnter) ||
                   key == KeyCode.Minus || key == KeyCode.Backspace || key == KeyCode.Delete || key == KeyCode.Return ||
                   key == KeyCode.LeftArrow || key == KeyCode.RightArrow || key == KeyCode.UpArrow || key == KeyCode.DownArrow ||
                   key == KeyCode.Home || key == KeyCode.End || key == KeyCode.Escape ||
                   (ctrl && (key == KeyCode.A || key == KeyCode.C || key == KeyCode.V || key == KeyCode.X));
        }

        private static void Press(Button button)
        {
            if (button != null && button.gameObject.activeInHierarchy && button.interactable)
            {
                button.onClick.Invoke();
            }
        }

        /// <summary>The window still splits the stack the buttons were set for, and it is still where it was.</summary>
        private static bool Valid() =>
            gui != null && dialog != null && dialog.IsActive && item != null && source != null && item.m_stack > 0 &&
            source.ContainsItem(item) && GameHooks.SplitItem(gui) == item;

        /// <summary>A one-click amount: the slider set to it, then the game's own OK, so it goes on the cursor and closes
        /// the window.</summary>
        private static void Take(int amount)
        {
            if (!Valid())
            {
                return;
            }
            dialog.SliderValue = Mathf.Clamp(amount, 1, item.m_stack);
            GameHooks.SplitOk(gui);
        }

        private static void TakePercent(int index)
        {
            if (item != null && index < percentShown.Count)
            {
                Take(Amounts.Percent(item.m_stack, percentShown[index].Key, Settings.Rounding.Value == PercentRounding.Up));
            }
        }

        /// <summary>The percentages and fixed amounts for the stack now: the general settings' lists (read again when
        /// they change), or, with "Amounts by item, category and stack size" on, the most specific of his.</summary>
        private static void Lists()
        {
            if (percentText != Settings.Percentages.Value)
            {
                percentText = Settings.Percentages.Value;
                generalPercents = Amounts.ParseList(percentText, 1, 100, Amounts.MostPercentages);
            }
            if (fixedText != Settings.FixedAmounts.Value)
            {
                fixedText = Settings.FixedAmounts.Value;
                generalFixeds = Amounts.ParseList(fixedText, 1, 99999, Amounts.MostFixed);
            }
            percents = generalPercents;
            fixeds = generalFixeds;
            if (!Settings.AmountsByKind.Value || item == null)
            {
                return;
            }
            if (rules == null || !ReferenceEquals(rulesItems, Settings.ByItem.Value) ||
                !ReferenceEquals(rulesCategories, Settings.ByCategory.Value) || !ReferenceEquals(rulesSizes, Settings.ByStackSize.Value))
            {
                rulesItems = Settings.ByItem.Value;
                rulesCategories = Settings.ByCategory.Value;
                rulesSizes = Settings.ByStackSize.Value;
                rules = new AmountRules.Parsed(rulesItems, rulesCategories, rulesSizes);
            }
            (string family, string member) = KindList.HomeOf(item);
            (fixeds, percents) = AmountRules.For(rules, item.m_shared.m_name, family, member, item.m_shared.m_maxStackSize,
                                                 generalFixeds, generalPercents);
        }

        private static void SplitPercent(int index)
        {
            if (item != null && index < percentShown.Count)
            {
                SplitIntoStacks(Amounts.Percent(item.m_stack, percentShown[index].Key, Settings.Rounding.Value == PercentRounding.Up));
            }
        }

        private static void SplitFixed(int index)
        {
            if (item != null && index < fixeds.Count && Amounts.Fits(fixeds[index], item.m_stack))
            {
                SplitIntoStacks(fixeds[index]);
            }
        }

        /// <summary>
        /// A right-click on an amount (his feedback of 2026-09-30): the stack split into as many stacks of it as it makes,
        /// each into an empty cell of its own: from a chest into his inventory; from his inventory into the chest open
        /// beside it, or, with none open, within his own inventory. The leftover stays where the stack was, or comes along
        /// (his setting). Never a stack he has equipped (a part moved would copy the "equipped" mark) or a quest item, and
        /// a chest only while this computer owns it (the game saves a chest only from its owner's computer). What finds no
        /// cell stays where it was, with the game's own "no room" line. The window closes, as after any one-click amount.
        /// </summary>
        private static void SplitIntoStacks(int amount)
        {
            if (!Settings.RightClickSplits.Value || !Valid() || amount < 1 || amount > item.m_stack)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            if (player == null || !GameHooks.CanMoveAcross)
            {
                if (!toldCantSplit)
                {
                    toldCantSplit = true;
                    Module.Log.LogInfo("A right-click can't split stacks: the game's own members it needs weren't found.");
                }
                return;
            }
            if (item.m_equipped || item.m_shared.m_questItem)
            {
                Module.Log.LogInfo($"A right-click doesn't split {item.m_shared.m_name}: " +
                                   (item.m_equipped ? "it's equipped (unequip it first)." : "it's a quest item."));
                return;
            }
            Inventory mine = player.GetInventory();
            Container chest = GameHooks.CurrentContainer(gui);
            bool chestShows = chest != null && gui.m_container != null && gui.m_container.gameObject.activeInHierarchy;
            // A chest he may only look at (a friend's, card A80: its Take all greyed) is no place to split into: his stack is
            // split within his own inventory, as with no chest open.
            Inventory open = chestShows && GameHooks.ChestTouchable(gui) ? chest.GetInventory() : null;
            Inventory to;
            if (open != null && source == open)
            {
                to = mine;
            }
            else if (source == mine)
            {
                to = open ?? mine;
            }
            else
            {
                Module.Log.LogInfo("A right-click splits only a stack in your inventory or in the chest open beside it.");
                return;
            }
            if (open != null && (source == open || to == open) && !chest.IsOwner())
            {
                Module.Log.LogInfo("A right-click doesn't split into or out of a chest another player's game is looking after.");
                return;
            }
            (int shares, int leftover) = Amounts.Split(item.m_stack, amount);
            var sizes = new List<int>();
            for (int share = 0; share < shares; share++)
            {
                sizes.Add(amount);
            }
            if (leftover > 0 && to != source && Settings.RightClickLeftover.Value == Leftover.TakeIt)
            {
                sizes.Add(leftover);
            }
            // Within his own inventory the stack itself stays as the last share (or keeps the leftover).
            int wanted = to == source ? (leftover > 0 ? shares : shares - 1) : sizes.Count;
            Inventory from = source;
            ItemDrop.ItemData stack = item;
            GameHooks.SplitCancel(gui);
            int made = Mover.IntoStacks(from, stack, sizes, to, GameHooks.EmptyCell);
            if (made > 0)
            {
                // The game's own sound for a move between inventories (its Ctrl + click).
                gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
            }
            if (made < wanted)
            {
                QoLMods.Kit.Say.Center("$msg_noroom");
                Module.Log.LogInfo($"Split {stack.m_shared.m_name} into {made} of {wanted} new stacks of {amount}: the rest had no " +
                                   "room, and stays where it was.");
            }
        }

        private static void TakeFixed(int index)
        {
            if (item != null && index < fixeds.Count && Amounts.Fits(fixeds[index], item.m_stack))
            {
                Take(fixeds[index]);
            }
        }

        /// <summary>Max weight (or, on his own stack, "just enough"): worked out again at the click, then on the cursor,
        /// or straight across if the setting says so and it is safe (StraightAcrossTo); otherwise on the cursor.</summary>
        private static void TakeMaxWeight()
        {
            if (!Valid() || !MaxWeightState(out bool canTake, out _) || !canTake || maxWeightAmount < 1)
            {
                return;
            }
            int amount = maxWeightAmount;
            Inventory to = Settings.MaxWeightGoes.Value == MaxWeightTarget.StraightAcross ? StraightAcrossTo() : null;
            if (to == null)
            {
                Take(amount);
                return;
            }
            Inventory from = source;
            ItemDrop.ItemData stack = item;
            if (!maxWeightMine)
            {
                // Spread over several of his stacks it sums in another order: step down if that rounds over.
                Inventory mine = Player.m_localPlayer.GetInventory();
                amount = Weights.FitSpread(mine.GetAllItems(), mine.GetTotalWeight(), stack, amount,
                                           Player.m_localPlayer.GetMaxCarryWeight(), Settings.LeaveFree.Value);
                if (amount < 1)
                {
                    return;
                }
            }
            GameHooks.SplitCancel(gui);
            int moved = Mover.Across(from, stack, amount, to, GameHooks.EmptyCell);
            if (moved > 0)
            {
                // The game's own sound for a move between inventories (its Ctrl + click).
                gui.m_moveItemEffects.Create(gui.transform.position, Quaternion.identity);
            }
            if (moved < amount)
            {
                Module.Log.LogInfo($"Max weight moved {moved} of {amount} {stack.m_shared.m_name}: " +
                                   "the rest had no room, and stays where it was.");
            }
        }

        /// <summary>
        /// Where Max weight may move straight across to, or null for the cursor (review round 1). Only between his
        /// inventory and the chest open beside it, and only while this computer owns that chest: the game saves a chest
        /// only from its owner's computer, so a move in a chest owned elsewhere would copy or lose items at its next
        /// reload. Never an equipped stack (the game's own drop unequips it first; a move would copy the "equipped" mark),
        /// and never without room for at least one: the cursor then lets him choose where it goes.
        /// </summary>
        private static Inventory StraightAcrossTo()
        {
            if (!GameHooks.CanMoveAcross || item.m_equipped || item.m_shared.m_questItem)
            {
                return null;
            }
            Player player = Player.m_localPlayer;
            Container chest = GameHooks.CurrentContainer(gui);
            // Nor a chest he may only look at (a friend's, card A80): the amount goes on the cursor, and the game's own drop
            // (refused there by the chest service) decides.
            if (player == null || chest == null || !chest.IsOwner() || !GameHooks.ChestTouchable(gui))
            {
                return null;
            }
            Inventory mine = player.GetInventory();
            Inventory open = chest.GetInventory();
            Inventory to;
            if (source == open && !maxWeightMine)
            {
                to = mine;
            }
            else if (source == mine && maxWeightMine)
            {
                to = open;
            }
            else
            {
                return null;
            }
            foreach (ItemDrop.ItemData stack in to.GetAllItems())
            {
                if (stack.m_stack < stack.m_shared.m_maxStackSize && stack.IsSameType(item))
                {
                    return to;
                }
            }
            return GameHooks.EmptyCell(to, item).x >= 0 ? to : null;
        }

        /// <summary>The slider buttons: Min, -1, 50%, +1, Max. They move the slider only; the window stays open.</summary>
        private static void MoveSlider(int which)
        {
            if (!Valid())
            {
                return;
            }
            Slider slider = dialog.m_splitSlider;
            float value = slider.value;
            switch (which)
            {
                case 0: value = slider.minValue; break;
                case 1: value -= 1f; break;
                case 2: value = Amounts.Half(item.m_stack); break;
                case 3: value += 1f; break;
                default: value = slider.maxValue; break;
            }
            dialog.SliderValue = Mathf.Clamp(value, slider.minValue, slider.maxValue);
        }

        /// <summary>What was typed into the box, onto the slider (once).</summary>
        private static void ApplyTyped()
        {
            if (typed == null)
            {
                return;
            }
            string text = typed;
            typed = null;
            SetSlider(text);
        }

        private static void SetSlider(string text)
        {
            if (dialog == null || !dialog.IsActive || !int.TryParse(text, out int value) || value < 1)
            {
                return;
            }
            int clamped = (int)Mathf.Clamp(value, dialog.m_splitSlider.minValue, dialog.m_splitSlider.maxValue);
            dialog.SliderValue = clamped;
            typedValue = clamped;
        }

        // Building blocks.

        private static RectTransform Container(string name, Transform parent)
        {
            var made = new GameObject(name, typeof(RectTransform));
            made.transform.SetParent(parent, false);
            return (RectTransform)made.transform;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>The side panel's wooden frame: a copy of the window's own (the first stretched picture in its panel),
        /// or a dark box if the game has none.</summary>
        private static void Frame(RectTransform target)
        {
            foreach (Transform child in panel)
            {
                if (child.gameObject.activeSelf && child is RectTransform rect && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
                    child.GetComponent<Image>() is Image picture && picture.sprite != null)
                {
                    GameObject frame = Object.Instantiate(child.gameObject, target, false);
                    frame.name = "PortionControl_Frame";
                    frame.transform.SetAsFirstSibling();
                    foreach (Transform inside in frame.transform)
                    {
                        Object.Destroy(inside.gameObject);
                    }
                    return;
                }
            }
            target.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.08f, 0.06f, 0.92f);
        }

        /// <summary>A button made from the game's own OK: its picture, its font and its click sound. Its gamepad key and
        /// hint go (keyboard and mouse only, his answer 12), and its click is ours.</summary>
        private static Button MakeButton(string name, Transform parent, Action onClick)
        {
            GameObject made = Object.Instantiate(template.gameObject, parent, false);
            made.name = "PortionControl_" + name;
            foreach (UIGamePad pad in made.GetComponentsInChildren<UIGamePad>(true))
            {
                Object.DestroyImmediate(pad);
            }
            TMP_Text label = null;
            foreach (Transform child in made.transform)
            {
                TMP_Text text = child.GetComponent<TMP_Text>();
                if (label == null && text != null)
                {
                    label = text;
                    continue;
                }
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }
            if (label != null)
            {
                RectTransform rect = label.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(4f, 2f);
                rect.offsetMax = new Vector2(-4f, -2f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 10f;
                label.fontSizeMax = 18f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.text = string.Empty;
            }
            Button button = made.GetComponent<Button>();
            // Never the selected object: a clicked +1 would otherwise keep the keyboard, and Enter or the arrow keys
            // would press it (or a button beside it) again (review round 1).
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Module.Guard("taking an amount", onClick));
            made.SetActive(false);
            return button;
        }

        private static TMP_Text LabelOf(Button button)
        {
            foreach (Transform child in button.transform)
            {
                TMP_Text text = child.GetComponent<TMP_Text>();
                if (text != null)
                {
                    return text;
                }
            }
            return null;
        }

        private static void SetLabel(Button button, string text)
        {
            TMP_Text label = LabelOf(button);
            if (label != null && label.text != text)
            {
                label.text = text;
            }
        }

        private static void Show(Button button, bool show, bool interactable, Transform parent)
        {
            if (button == null)
            {
                return;
            }
            if (show && button.transform.parent != parent)
            {
                button.transform.SetParent(parent, false);
            }
            if (button.gameObject.activeSelf != show)
            {
                button.gameObject.SetActive(show);
            }
            if (show)
            {
                SetInteractable(button, interactable);
            }
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button == null)
            {
                return;
            }
            if (button.interactable != interactable)
            {
                button.interactable = interactable;
            }
            TMP_Text label = LabelOf(button);
            if (label != null)
            {
                float alpha = interactable ? 1f : Faded.a;
                if (!Mathf.Approximately(label.alpha, alpha))
                {
                    label.alpha = alpha;
                }
            }
        }

        /// <summary>The amount box, in the game's style (Jötunn's), made to take whole numbers only. What is typed is only
        /// recorded here and put on the slider at the next turn: an input field ends its edit as it is switched off or
        /// rebuilt, so its handlers must touch nothing else (LESSONS_LEARNED, Unity UI).</summary>
        private static InputField MakeBox(Transform parent)
        {
            GameObject made = GUIManager.Instance.CreateInputField(parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                                                   InputField.ContentType.IntegerNumber, "...", 18, 72f, 30f);
            made.name = "PortionControl_Box";
            made.transform.localScale = Vector3.one;
            made.transform.localRotation = Quaternion.identity;
            RectTransform rect = (RectTransform)made.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(72f, 30f);
            Vector3 at = rect.localPosition;
            rect.localPosition = new Vector3(at.x, at.y, 0f);
            InputField field = made.GetComponent<InputField>();
            field.characterLimit = 6;
            // Every change is recorded as it is typed; the end of an edit adds nothing, and recording it again would put
            // the slider back after a click on its track took the keyboard from the box (review round 1).
            field.onValueChanged.AddListener(text => typed = text);
            return field;
        }
    }
}
