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
