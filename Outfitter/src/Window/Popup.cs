using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Outfitter.Window
{
    using Text = UnityEngine.UI.Text;   // not Outfitter.Text, the names' filter

    /// <summary>
    /// Outfitter's pop-up: a question with two answers (the missing-items question, a confirm, a delete), or a name to type
    /// (saving, renaming). Drawn on Outfitter's own canvas, so it shows with the window or without it (a Loadout Stand's
    /// equip asks with it too: it is the equip step's <c>Outfit.Ask</c>). While it shows, the game's input waits, and
    /// Escape answers no. Each question has a number, so an equip that ends can take down its own question and no other.
    /// </summary>
    internal static class Popup
    {
        private const float Width = 520f;
        private const float Height = 330f;

        /// <summary>Its size at Small: what "Window size" keeps to the screen while it shows without the window.</summary>
        internal static Vector2 Size => new Vector2(Width, Height);

        private static GameObject shade;
        private static GameObject panel;
        private static Text title;
        private static Text body;
        private static InputField field;
        private static Button yesButton;
        private static Button noButton;
        private static Action<bool> answer;
        private static Action<string> named;

        // The scrolling list a question with items draws in (AskList), where the body's text is for the other questions.
        private const float ListTop = 56f;
        private const float ListSide = 24f;
        private const float ListBottom = 76f;
        private const float BarWidth = 12f;
        private const float BlankLine = 10f;
        private const float IconSize = 26f;
        private const int FontSize = 16;
        private static float ListRowWidth => Width - 2f * ListSide - BarWidth - 8f;   // the content's width less its padding
        private static RectTransform listView;
        private static RectTransform list;
        private static ScrollRect listScroll;

        // An empty name is an answer too (the box asked with "emptyShows").
        private static bool emptyAnswers;

        private static bool blocking;
        private static bool releaseNextFrame;
        private static int asked;

        internal static bool Showing => shade != null && shade.activeSelf;

        /// <summary>Asks a question; <paramref name="then"/> gets true for the first answer, false for the second (and for
        /// Escape, and if the pop-up is taken down unanswered). Returns the question's number (for CloseIf).</summary>
        internal static int Ask(string question, string yes, string no, Action<bool> then)
        {
            Build();
            Settle(false);
            title.text = "Outfitter";
            body.text = question;
            field.gameObject.SetActive(false);
            Ui.SetButtonText(yesButton, yes);
            Ui.SetButtonText(noButton, no);
            answer = then;
            named = null;
            Show();
            return ++asked;
        }

        /// <summary>
        /// Asks a question with a list of items (the missing-items question, card B21, his feedback of 2026-10-11): the
        /// heading, a blank line, a bullet per item with its icon to the left of how many and why (its name beside it, or
        /// only when the pointer rests on the icon, by "Missing items in the pop-up"), a blank line, the lines after the
        /// list, and the question, in a list that scrolls when it's longer than the pop-up. Answers as <see cref="Ask"/>.
        /// </summary>
        internal static int AskList(Equip.ListQuestion question, string yes, string no, Action<bool> then)
        {
            Build();
            Settle(false);
            title.text = "Outfitter";
            body.text = string.Empty;
            field.gameObject.SetActive(false);
            Ui.SetButtonText(yesButton, yes);
            Ui.SetButtonText(noButton, no);
            answer = then;
            named = null;
            Show();
            int number = ++asked;
            FillList(question);
            return number;
        }

        // The list's content, made afresh for each question (shown first: a layout isn't worked out while hidden).
        private static void FillList(Equip.ListQuestion question)
        {
            listView.gameObject.SetActive(true);
            DrawerUi.Clear(list);
            // With a gamepad there's no pointer to rest on an icon: the names show beside them (the review of 0.6.0).
            bool namesBeside = Settings.MissingList == null || Settings.MissingList.Value == MissingListLook.IconAndName || ZInput.IsGamepadActive();
            ListText(question.Heading, TextAnchor.UpperCenter);
            DrawerUi.Row(list, BlankLine);
            foreach (Equip.ListedItem item in question.Items)
            {
                ListItem(item, namesBeside);
            }
            if (question.MoreLine != null)
            {
                ListText(question.MoreLine, TextAnchor.UpperLeft);
            }
            DrawerUi.Row(list, BlankLine);
            foreach (string line in question.After)
            {
                ListText(line, TextAnchor.UpperCenter);
            }
            ListText(question.Prompt, TextAnchor.UpperCenter);
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            listScroll.verticalNormalizedPosition = 1f;   // from the top
        }

        // A line of the list's text, wrapped to its width.
        private static void ListText(string text, TextAnchor align)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            Text line = Ui.Label(list, text, 0f, 0f, ListRowWidth, 20f, FontSize, align);
            line.verticalOverflow = VerticalWrapMode.Overflow;
        }

        // One item's line: a bullet, its icon (its name on hover), and how many and why (its name too when asked, or when
        // it has no icon to point at).
        private static void ListItem(Equip.ListedItem item, bool namesBeside)
        {
            Sprite sprite = null;
            try
            {
                sprite = item.Template != null ? item.Template.GetIcon() : null;
            }
            catch (Exception)
            {
                sprite = null;   // an item whose icon can't be had shows its name instead
            }
            bool withName = namesBeside || sprite == null;
            float textX = IconSize + 26f;
            float textWidth = ListRowWidth - textX - 4f;
            var row = new GameObject("Outfitter_MissingRow", typeof(RectTransform));
            row.transform.SetParent(list, false);
            Text words = Ui.Label(row.transform, item.Line(withName), textX, 0f, textWidth, 20f, FontSize, TextAnchor.MiddleLeft);
            float height = Mathf.Max(IconSize + 4f, Mathf.Ceil(words.preferredHeight) + 6f);
            Ui.Place(words.gameObject, textX, 0f, textWidth, height);
            row.AddComponent<LayoutElement>().preferredHeight = height;
            Ui.Label(row.transform, Equip.ListQuestion.Bullet, 4f, 0f, 14f, height, FontSize, TextAnchor.MiddleLeft);
            if (sprite != null)
            {
                GameObject icon = Ui.Box("Outfitter_MissingIcon", row.transform, 20f, (height - IconSize) / 2f, IconSize, IconSize);
                Image image = icon.AddComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = true;   // the pointer resting on it names the item
                string name = item.Name;
                Ui.Help(image, () => name);
                icon.GetComponent<Tip>().Always = true;
            }
        }

        /// <summary>Asks for a name; <paramref name="then"/> gets it (plain, at most 40 characters), or isn't called when he
        /// cancels. Returns the box's number (for CloseIf: a box he cancelled never calls back, so its asker can't tell it
        /// went; Loadout Stands, harness area B).</summary>
        internal static int Name(string heading, string initial, Action<string> then) => Name(heading, initial, then, null);

        /// <summary>As the other, and with <paramref name="emptyShows"/> an empty box is an answer too (an armour stand's
        /// own name taken off: review round 1 of Session 2, m4): the empty box shows those words (the game's name).</summary>
        internal static int Name(string heading, string initial, Action<string> then, string emptyShows)
        {
            Build();
            Settle(false);
            title.text = heading;
            body.text = emptyShows == null
                ? "Letters, numbers and spaces; up to 40."
                : $"Letters, numbers and spaces; up to 40.\nLeave it empty for the game's name, {emptyShows}.";
            field.gameObject.SetActive(true);
            field.text = initial ?? string.Empty;
            if (field.placeholder is Text hint)
            {
                hint.text = emptyShows ?? "Name";
            }
            Ui.SetButtonText(yesButton, "Save");
            Ui.SetButtonText(noButton, "Cancel");
            answer = null;
            named = then;
            emptyAnswers = emptyShows != null;
            Show();
            int number = ++asked;
            field.Select();
            field.ActivateInputField();
            return number;
        }

        /// <summary>
        /// Every frame: Escape answers no, Enter saves a name. Returns true when it used this frame's Escape (the window
        /// then leaves it alone: R1, finding 10). A pop-up taken down with its scene (a new world) gives back its hold on
        /// the game's input, and its question is answered no.
        /// </summary>
        internal static bool Update()
        {
            if (releaseNextFrame)
            {
                releaseNextFrame = false;
                GUIManager.BlockInput(false);
            }
            if (shade == null && (blocking || answer != null || named != null))
            {
                Settle(false);
                return false;
            }
            if (!Showing)
            {
                return false;
            }
            Ui.FitCanvas();
            if (ZInput.GetKeyDown(KeyCode.Escape, false))
            {
                Settle(false, byEscape: true);
                return true;
            }
            if (named != null && (ZInput.GetKeyDown(KeyCode.Return, false) || ZInput.GetKeyDown(KeyCode.KeypadEnter, false)))
            {
                Settle(true);
            }
            if (listView != null && listView.gameObject.activeSelf)
            {
                float wheel = ZInput.GetMouseScrollWheel();
                if (wheel != 0f)
                {
                    DrawerUi.Wheel(wheel, 3f * (IconSize + 4f), new[] { listScroll });
                }
                else if (ZInput.IsGamepadActive())
                {
                    // The right stick scrolls it (up: towards the top), 400 pixels a second at full tilt.
                    float stick = -ZInput.GetJoyRightStickY();
                    float hidden = list.rect.height - listView.rect.height;
                    if (Mathf.Abs(stick) > 0.2f && hidden > 0f)
                    {
                        listScroll.verticalNormalizedPosition = Mathf.Clamp01(listScroll.verticalNormalizedPosition + stick * 400f * Time.unscaledDeltaTime / hidden);
                    }
                }
            }
            return false;
        }

        /// <summary>Takes the pop-up down unanswered (a no). Also the module's undo step: a hold due back at the next frame
        /// is given back now, since nothing of the module's runs once an error has switched it off (R2, finding 4).</summary>
        internal static void Close()
        {
            Settle(false);
            if (releaseNextFrame)
            {
                releaseNextFrame = false;
                GUIManager.BlockInput(false);
            }
        }

        /// <summary>Takes down question <paramref name="number"/> unanswered, if it's still the one showing.</summary>
        internal static void CloseIf(int number)
        {
            if (number == asked && (answer != null || named != null))
            {
                Settle(false);
            }
        }

        private static void Show()
        {
            listView.gameObject.SetActive(false);   // AskList shows it again
            shade.SetActive(true);
            shade.transform.SetAsLastSibling();
            if (!blocking)
            {
                blocking = true;
                GUIManager.BlockInput(true);
            }
        }

        // Hides it and hands the answer on (once).
        private static void Settle(bool yes, bool byEscape = false)
        {
            Action<bool> then = answer;
            Action<string> thenName = named;
            // Empty is an answer only when he left the box empty: a name of characters the filter drops is no name, as in
            // the other boxes (review round 2 of Session 2, n3).
            bool emptyOk = emptyAnswers && field != null && string.IsNullOrWhiteSpace(field.text);
            string typed = field != null ? Outfitter.Text.Plain(field.text, Loadouts.Loadout.MaxName) : string.Empty;
            answer = null;
            named = null;
            emptyAnswers = false;
            if (shade != null)
            {
                shade.SetActive(false);
            }
            if (blocking)
            {
                // Jötunn counts requests to hold the game's input (GUIManager.BlockInput): the pop-up gives back its own,
                // whether or not the window keeps one. Answered by Escape with no window behind it, it gives it back at the
                // next frame, so the same press can't also open the game's menu (R1, finding 11).
                blocking = false;
                if (byEscape && !LoadoutWindow.Open && shade != null)
                {
                    releaseNextFrame = true;
                }
                else
                {
                    GUIManager.BlockInput(false);
                }
            }
            if (then != null)
            {
                then(yes);
            }
            if (thenName != null && yes && (typed.Length > 0 || emptyOk))
            {
                thenName(typed);
            }
        }

        private static void Build()
        {
            if (shade != null)
            {
                return;
            }
            Transform root = Ui.CanvasRoot();
            shade = Ui.Box("Outfitter_Popup", root, 0f, 0f, 10f, 10f, new Color(0f, 0f, 0f, 0.5f));
            var shadeRect = (RectTransform)shade.transform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = Vector2.one;
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().raycastTarget = true;   // the window under it can't be clicked meanwhile
            panel = GUIManager.Instance.CreateWoodpanel(shade.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Width, Height, false);
            panel.name = "Outfitter_PopupPanel";
            title = Ui.Label(panel.transform, "Outfitter", 20f, 16f, Width - 40f, 34f, 22, TextAnchor.MiddleCenter, Ui.Orange, bold: true);
            body = Ui.Label(panel.transform, string.Empty, 30f, 56f, Width - 60f, 170f, 16, TextAnchor.UpperCenter);
            list = DrawerUi.Scroll("Outfitter_PopupList", panel.transform, ListSide, ListTop, Width - 2f * ListSide, Height - ListTop - ListBottom, out listScroll);
            DrawerUi.List(list, 2f);
            listView = (RectTransform)listScroll.transform;
            listView.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);   // the wood shows through; it still catches the wheel
            listScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;   // a bar only when it scrolls
            listView.gameObject.SetActive(false);
            GameObject input = GUIManager.Instance.CreateInputField(panel.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                                                    InputField.ContentType.Standard, "Name", 18, Width - 100f, 40f);
            Ui.Place(input, 50f, 150f, Width - 100f, 40f);
            field = input.GetComponent<InputField>();
            field.characterLimit = Loadouts.Loadout.MaxName;
            field.lineType = InputField.LineType.SingleLine;
            yesButton = Ui.Button(panel.transform, "Yes", Width / 2f - 170f, Height - 64f, 160f, 40f, () => Module.Guard("answering Outfitter's pop-up", () => Settle(true)));
            noButton = Ui.Button(panel.transform, "No", Width / 2f + 10f, Height - 64f, 160f, 40f, () => Module.Guard("answering Outfitter's pop-up", () => Settle(false)));
            shade.SetActive(false);
        }
    }
}
