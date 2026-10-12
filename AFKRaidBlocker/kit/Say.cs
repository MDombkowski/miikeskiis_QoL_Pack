using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace QoLMods.Kit
{
    /// <summary>
    /// The one way a module shows a message (card B14): top left, or in the middle of the screen, as the game's own are,
    /// fitted to the screen first. The game's message texts never wrap (TextMeshPro, no wrapping, overflow: the middle one
    /// 40 points, centred on a 257-unit box, the top-left one 20 points from about 96 units in, both read from the game's
    /// HudMessage and TopLeftMessage prefabs, card A82), so a line wider than the screen ran off both sides of it: here
    /// every line too wide is broken at spaces into lines that fit, measured by the game's own message text. A message
    /// that fits goes exactly as it was. StoreStash's fitter (card A82), made the whole pack's after Topper-Upper's linking
    /// message ran off the screen too (card B14). No module calls MessageHud.ShowMessage or Character.Message itself, nor
    /// writes the HUD's message texts: the bundle's packer refuses a DLL that does anywhere but here
    /// (tools\check-messages.ps1). Shown through MessageHud.ShowMessage, which hides it while he hides the HUD, as the
    /// game's own; the message log only when asked (review m5: Player.Message never logs, ShowMessage's top-left lines
    /// did, so the modules that called ShowMessage pass log: true).
    /// </summary>
    internal static class Say
    {
        /// <summary>How much of the screen's width a message leaves free on each side, as a share of it (card A82).</summary>
        internal const float Margin = 0.05f;

        /// <summary>A message in the middle of the screen, fitted to it.</summary>
        internal static void Center(string text) => To(MessageHud.MessageType.Center, text);

        /// <summary>A message top left, fitted to the screen. <paramref name="log"/>: also into the game's message log,
        /// as MessageHud.ShowMessage puts top-left lines (Player.Message doesn't).</summary>
        internal static void TopLeft(string text, bool log = false) => To(MessageHud.MessageType.TopLeft, text, 0, null, log);

        /// <summary>A message there, fitted to the screen; with an amount and an icon as the game's top-left lines have
        /// them, and into the game's message log with <paramref name="log"/> (top-left lines only, as the game does). On
        /// this computer's screen, through the game's own HUD, whether or not a character is spawned.</summary>
        internal static void To(MessageHud.MessageType where, string text, int amount = 0, Sprite icon = null, bool log = false) =>
            Send(where, Fit(where, text), amount, icon, log);

        /// <summary>Shows a fitted message. Only a scratch test harness sets another (to read what he's told); a module
        /// never calls it (the packer refuses one that does: it would skip the fitting).</summary>
        internal static Action<MessageHud.MessageType, string, int, Sprite, bool> Send = (where, text, amount, icon, log) =>
        {
            MessageHud hud = MessageHud.instance;
            if (hud != null)   // Unity's own null test
            {
                hud.ShowMessage(where, text, amount, icon, false, log);
            }
        };

        /// <summary>How wide a line shows in that kind of message, in the screen's units, measured by the game's own
        /// message text (its font, its size); null without a HUD (the text is then sent as it is). Only a scratch test
        /// harness sets another.</summary>
        internal static Func<MessageHud.MessageType, Func<string, float>> Measure = where =>
        {
            TMP_Text text = TextOf(where);
            if (text == null)
            {
                return null;
            }
            float scale = ToScreen(text);
            return line => text.GetPreferredValues(line).x * scale;
        };

        /// <summary>How wide a line of that kind may be, in the same units: the screen's width as the HUD's canvas has it,
        /// less Margin on each side, and for a top-left message less where its text starts; 0 without a HUD. Only a
        /// scratch test harness sets another.</summary>
        internal static Func<MessageHud.MessageType, float> Room = GameRoom;

        /// <summary>The text as the game will show it ($ words in his language): MessageHud localises every message. Only a
        /// scratch test harness sets another.</summary>
        internal static Func<string, string> Localize = text => Localization.instance != null ? Localization.instance.Localize(text) : text;

        /// <summary>The game's own text for that kind of message, or null without a HUD.</summary>
        internal static TMP_Text TextOf(MessageHud.MessageType where)
        {
            MessageHud hud = MessageHud.instance;
            if (hud == null)
            {
                return null;
            }
            TMP_Text text = where == MessageHud.MessageType.Center ? hud.m_messageCenterText : hud.m_messageText;
            return text != null ? text : null;
        }

        /// <summary>
        /// A message fitted to the screen: as the game will show it (localised), every line wider than the room for that
        /// kind of message broken at spaces into lines that fit. A message that fits already is returned exactly as it
        /// was. Fitting a fitted message changes nothing. Never fails: without a HUD, or if measuring goes wrong, the text
        /// is returned as it is.
        /// </summary>
        internal static string Fit(MessageHud.MessageType where, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            try
            {
                Func<string, float> width = Measure(where);
                float room = Room(where);
                if (width == null || !(room > 0f))
                {
                    return text;
                }
                string shown = Localize(text) ?? text;
                string fitted = Wrap(shown, width, room);
                return ReferenceEquals(fitted, shown) ? text : fitted;
            }
            catch (Exception)
            {
                return text;   // a message as it was beats no message
            }
        }

        /// <summary>
        /// Breaks every line of <paramref name="text"/> wider than <paramref name="room"/> (as <paramref name="width"/>
        /// measures it) at spaces into lines that fit, each as full as it can be; a single word wider than the room (a name
        /// in a language written without spaces) is broken between its letters, never inside a letter of two halves.
        /// Spaces are kept as they were, but the one each break replaces. Returns the very same string when every line fits
        /// already. Plain code, for a scratch test harness too.
        /// </summary>
        internal static string Wrap(string text, Func<string, float> width, float room)
        {
            if (string.IsNullOrEmpty(text) || width == null || !(room > 0f))
            {
                return text;
            }
            string[] lines = text.Split('\n');
            var output = new List<string>(lines.Length + 2);
            bool changed = false;
            foreach (string line in lines)
            {
                if (width(line) <= room)
                {
                    output.Add(line);
                    continue;
                }
                changed = true;
                var current = new StringBuilder();
                bool started = false;
                foreach (string word in line.Split(' '))
                {
                    if (word.Length > 0 && width(word) > room)
                    {
                        // Too wide on a line of its own: a line of its own, broken between letters.
                        if (started && current.Length > 0)
                        {
                            output.Add(current.ToString());
                        }
                        List<string> pieces = Pieces(word, width, room);
                        for (int p = 0; p < pieces.Count - 1; p++)
                        {
                            output.Add(pieces[p]);
                        }
                        current.Clear().Append(pieces[pieces.Count - 1]);
                        started = true;
                        continue;
                    }
                    if (!started)
                    {
                        current.Append(word);
                        started = true;
                        continue;
                    }
                    if (word.Length > 0 && width(current + " " + word) > room)
                    {
                        output.Add(current.ToString());
                        current.Clear().Append(word);
                    }
                    else
                    {
                        current.Append(' ').Append(word);
                    }
                }
                output.Add(current.ToString());
            }
            return changed ? string.Join("\n", output) : text;
        }

        // A word too wide for a line, in pieces that fit, as full as they can be: never between the two halves of a letter
        // (a surrogate pair), and at least one letter a piece.
        private static List<string> Pieces(string word, Func<string, float> width, float room)
        {
            var pieces = new List<string>();
            int start = 0;
            while (start < word.Length)
            {
                int end = start + Letter(word, start);
                while (end < word.Length)
                {
                    int next = end + Letter(word, end);
                    if (width(word.Substring(start, next - start)) > room)
                    {
                        break;
                    }
                    end = next;
                }
                pieces.Add(word.Substring(start, end - start));
                start = end;
            }
            return pieces;
        }

        // How many chars the letter at i takes: two for a surrogate pair.
        private static int Letter(string text, int i) =>
            char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;

        // The text's own units in the screen's (its root canvas's), should the text or anything between be scaled (card
        // A82's review N7): the room is measured in the screen's units.
        private static float ToScreen(TMP_Text text)
        {
            Canvas canvas = text.canvas;
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            float screen = root != null ? root.transform.lossyScale.x : 0f;
            float own = text.rectTransform.lossyScale.x;
            return screen > 0f && own > 0f ? own / screen : 1f;
        }

        // The room for a line of that kind, from the HUD's own canvas: the middle message centred where its text is, the
        // top-left one from where its text starts; 0 without a HUD.
        private static float GameRoom(MessageHud.MessageType where)
        {
            TMP_Text text = TextOf(where);
            Canvas canvas = text != null ? text.canvas : null;
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            RectTransform screen = root != null ? root.transform as RectTransform : null;
            if (screen == null)
            {
                return 0f;
            }
            Rect area = screen.rect;
            if (!(area.width > 0f))
            {
                return 0f;
            }
            var corners = new Vector3[4];
            text.rectTransform.GetWorldCorners(corners);   // bottom left, top left, top right, bottom right
            float left = screen.InverseTransformPoint(corners[0]).x;
            float right = screen.InverseTransformPoint(corners[2]).x;
            float margin = area.width * Margin;
            if (where == MessageHud.MessageType.Center)
            {
                float middle = (left + right) / 2f;   // the game centres its middle message on its box
                return 2f * (Mathf.Min(middle - area.xMin, area.xMax - middle) - margin);
            }
            return area.xMax - margin - Mathf.Max(left, area.xMin);
        }
    }
}
