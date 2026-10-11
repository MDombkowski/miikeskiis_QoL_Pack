using System.Collections.Generic;
using System.Text;
using QoLMods.Catalogue;
using QoLMods.Chests;
using TMPro;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// What he's told, as the game's own messages are: top left, or in the middle of the screen. Every message is fitted to
    /// the screen first (Fit, card A82): the game's message texts never wrap (TextMeshPro, no wrapping, overflow: the
    /// middle one 40 points, centred on a 257-unit box, the top-left one 20 points from about 96 units in, both read from
    /// the game's HudMessage and TopLeftMessage prefabs), so a long line ran off both sides of the screen; a line too wide
    /// is broken at spaces into lines that fit, measured by the game's own text.
    /// </summary>
    internal static class Messages
    {
        /// <summary>The most set-up chests the where-lines name, one line each; then "and 2 more chests".</summary>
        internal const int MostChests = 5;

        /// <summary>The most kinds of things a where-line names for one chest; then "and 3 more".</summary>
        internal const int MostKinds = 4;

        /// <summary>Shows a message to him. Only the scratch test harness sets another (to read what he's told).</summary>
        internal static System.Action<MessageHud.MessageType, string> Say = (where, text) =>
        {
            Player player = Player.m_localPlayer;
            if (player != null)   // Unity's own null test: a destroyed player isn't null to ?.
            {
                player.Message(where, text);
            }
        };

        /// <summary>How much of the screen's width a message leaves free on each side, as a share of it (card A82).</summary>
        internal const float Margin = 0.05f;

        /// <summary>How wide a line shows in that kind of message, in the HUD's own units, measured by the game's own
        /// message text (its font, its size); null without a HUD (the text is then sent as it is). Only the scratch test
        /// harness sets another.</summary>
        internal static System.Func<MessageHud.MessageType, System.Func<string, float>> Measure = where =>
        {
            TMP_Text text = MessageHold.TextOf(where);
            if (text == null)
            {
                return null;
            }
            float scale = ToScreen(text);
            return line => text.GetPreferredValues(line).x * scale;
        };

        // The text's own units in the screen's (its root canvas's), should the text or anything between be scaled (review
        // N7): the room is measured in the screen's units.
        private static float ToScreen(TMP_Text text)
        {
            Canvas canvas = text.canvas;
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            float screen = root != null ? root.transform.lossyScale.x : 0f;
            float own = text.rectTransform.lossyScale.x;
            return screen > 0f && own > 0f ? own / screen : 1f;
        }

        /// <summary>How wide a line of that kind may be, in the same units: the screen's width as the HUD's canvas has it,
        /// less Margin on each side, and for a top-left message less where its text starts; 0 without a HUD. Only the
        /// scratch test harness sets another.</summary>
        internal static System.Func<MessageHud.MessageType, float> Room = GameRoom;

        /// <summary>The text as the game will show it ($ words in his language): MessageHud localises every message. Only
        /// the scratch test harness sets another.</summary>
        internal static System.Func<string, string> Localize = text => Localization.instance != null ? Localization.instance.Localize(text) : text;

        internal static void TopLeft(string text) => Show(MessageHud.MessageType.TopLeft, text);

        internal static void Center(string text) => Show(MessageHud.MessageType.Center, text);

        /// <summary>
        /// The message after a quick stack, or the personal quick stack (card A43), where "Message after a quick stack"
        /// puts it (or nowhere), held a little longer per line after the first (MessageHold). With "Say where things went"
        /// on and a set-up chest that took something by its setup (card A41's second round, decision 26), it says where
        /// those things went instead of the usual line, with a first line for what went into other chests, if anything did:
        /// one message, so the middle of the screen (which shows only the last one sent) loses nothing. With the usual line
        /// off, the where-lines alone, top left (they were asked for). After one stack by a click (card A82), the usual line
        /// names the stack (<paramref name="single"/>, DescribeOne): what went and what stayed, or why none went.
        /// </summary>
        internal static void AfterQuickStack(StackResult result, bool personal = false, string single = null, int stayed = 0, string whyNothing = null)
        {
            string where = SetupSettings.Active && SetupSettings.SayWhere.Value ? Where(result, Setups.Of, Setups.ItemName, Label) : string.Empty;
            MessagePlace place = Settings.Message.Value;
            string text;
            if (where.Length == 0)
            {
                if (place == MessagePlace.Off)
                {
                    return;
                }
                text = single != null ? DescribeOne(result, single, stayed, whyNothing) : Describe(result, personal);
            }
            else
            {
                text = place == MessagePlace.Off ? where : WithWhere(result, personal, where, Setups.Of);
                if (single != null && stayed > 0)
                {
                    text += $"\n{stayed} {single} stay with you";   // one stack by a click: what didn't fit (review M3)
                }
            }
            MessageHud.MessageType type = place == MessagePlace.Center ? MessageHud.MessageType.Center : MessageHud.MessageType.TopLeft;
            text = Fit(type, text);   // so the hold knows the message by its lines as shown
            Show(type, text);
            MessageHold.Start(type, text);
        }

        /// <summary>A line about a quick stack, where "Message after a quick stack" puts it (or nowhere).</summary>
        internal static void AsSet(string text)
        {
            switch (Settings.Message.Value)
            {
                case MessagePlace.TopLeft:
                    TopLeft(text);
                    break;
                case MessagePlace.Center:
                    Center(text);
                    break;
            }
        }

        /// <summary>A set-up chest as the where-lines name it: by its own name (MyLittleUI's, or the setup window's), else by
        /// what it takes.</summary>
        internal static string Label(Container chest, ItemPicks picks)
        {
            string name = SetupName.Read(chest);
            return string.IsNullOrEmpty(name) ? Setups.Describe(picks, 2) : name;
        }

        /// <summary>
        /// What each set-up chest took by its setup, card A41's "Say where things went": one chest on one line, "Into
        /// set-up chests: 12 Wolf Pelt, 3 Deer Hide → Pelt chest"; several as "Into set-up chests:" and a line per chest, up to
        /// <paramref name="mostChests"/>, then "and 2 more chests"; up to <paramref name="mostKinds"/> kinds a chest, then
        /// "and 3 more". Only what its setup takes: what a chest took because it already held it ("A set-up chest also takes
        /// what it holds") isn't its setup's doing. A chest is named by <paramref name="labelOf"/>. Empty when no set-up
        /// chest took anything by its setup.
        /// </summary>
        internal static string Where(StackResult result, System.Func<Container, ItemPicks> setupOf, System.Func<string, string> itemName,
                                     System.Func<Container, ItemPicks, string> labelOf, int mostChests = MostChests, int mostKinds = MostKinds)
        {
            var lines = new List<string>();
            int more = 0;
            foreach (Container chest in result.ChestsUsed)
            {
                ItemPicks picks = setupOf(chest);
                if (picks == null || !result.MovedInto.TryGetValue(chest, out Dictionary<string, int> into))
                {
                    continue;
                }
                var things = new List<string>();
                int moreKinds = 0;
                foreach (KeyValuePair<string, int> each in into)
                {
                    if (picks.Fit(each.Key) > 0)
                    {
                        if (things.Count < mostKinds)
                        {
                            things.Add($"{each.Value} {itemName(each.Key)}");
                        }
                        else
                        {
                            moreKinds++;
                        }
                    }
                }
                if (things.Count == 0)
                {
                    continue;
                }
                if (lines.Count >= mostChests)
                {
                    more++;
                    continue;
                }
                string what = string.Join(", ", things) + (moreKinds > 0 ? $" and {moreKinds} more" : string.Empty);
                lines.Add($"{what} → {labelOf(chest, picks)}");
            }
            if (lines.Count == 0)
            {
                return string.Empty;
            }
            if (lines.Count == 1 && more == 0)
            {
                return "Into set-up chests: " + lines[0];
            }
            var text = new StringBuilder("Into set-up chests:");
            foreach (string line in lines)
            {
                text.Append('\n').Append(line);
            }
            if (more > 0)
            {
                text.Append('\n').Append("and ").Append(Count(more, "more chest", "more chests"));
            }
            return text.ToString();
        }

        /// <summary>
        /// The message with the where-lines (<paramref name="where"/>, not empty): when everything moved went into set-up
        /// chests by their setups, the where-lines alone; else first "15 items into 2 other chests" ("...other chests of
        /// yours" for the personal quick stack), counting what didn't go by a setup and the chests that got it, then the
        /// where-lines. What was skipped ("1 chest in use") is never lost: on the first line when there is one, else last.
        /// </summary>
        internal static string WithWhere(StackResult result, bool personal, string where, System.Func<Container, ItemPicks> setupOf)
        {
            int bySetup = 0;
            int otherChests = 0;
            bool listedToo = false;
            foreach (Container chest in result.ChestsUsed)
            {
                if (!result.MovedInto.TryGetValue(chest, out Dictionary<string, int> into))
                {
                    continue;
                }
                ItemPicks picks = setupOf(chest);
                bool other = false;
                bool itsSetup = false;
                foreach (KeyValuePair<string, int> each in into)
                {
                    if (picks != null && picks.Fit(each.Key) > 0)
                    {
                        bySetup += each.Value;
                        itsSetup |= each.Value > 0;
                    }
                    else if (each.Value > 0)
                    {
                        other = true;
                    }
                }
                if (other)
                {
                    otherChests++;
                    listedToo |= itsSetup;   // a set-up chest that also took what it holds ("A set-up chest also takes...")
                }
            }
            int others = result.Moved - bySetup;
            string skipped = Skipped(result);
            if (others <= 0 || otherChests == 0)
            {
                return skipped.Length > 0 ? $"{where}\n({skipped})" : where;
            }
            // "Other" only while none of those chests is one of the set-up chests named below (review round 1).
            string chests = (listedToo ? Count(otherChests, "chest", "chests") : Count(otherChests, "other chest", "other chests")) +
                            (personal ? " of yours" : string.Empty);
            string first = $"{Count(others, "item", "items")} into {chests}";
            if (skipped.Length > 0)
            {
                first += $" ({skipped})";
            }
            return first + "\n" + where;
        }

        /// <summary>"34 items into 5 chests" ("34 items into 3 of your chests" for the personal quick stack), with what
        /// was skipped: "; 2 chests in use". Or why nothing moved.</summary>
        internal static string Describe(StackResult result, bool personal = false)
        {
            string text;
            if (result.NoItems)
            {
                text = personal ? "Personal quick stack: nothing to put away" : "Quick stack: nothing to put away";
            }
            else if (result.Moved == 0)
            {
                text = personal ? "Personal quick stack: none of your chests nearby takes what you carry" : "Quick stack: no chest nearby takes what you carry";
            }
            else
            {
                string chests = personal
                    ? $"{result.ChestsUsed.Count} of your chests"
                    : Count(result.ChestsUsed.Count, "chest", "chests");
                text = $"{Count(result.Moved, "item", "items")} into {chests}";
            }
            string skipped = Skipped(result);
            return skipped.Length > 0 ? $"{text} ({skipped})" : text;
        }

        /// <summary>The line after one stack by a click (card A82): "7 Bread into 1 chest" ("5 Bread into 1 chest, 2 stay
        /// with you" when not all of it fitted), with what was skipped; or why none of it went ("Bread: no chest nearby holds
        /// any").</summary>
        internal static string DescribeOne(StackResult result, string name, int stayed, string whyNothing)
        {
            string text = result.Moved > 0
                ? $"{result.Moved} {name} into {Count(result.ChestsUsed.Count, "chest", "chests")}" + (stayed > 0 ? $", {stayed} stay with you" : string.Empty)
                : $"{name}: {whyNothing ?? "no chest nearby takes it"}";
            string skipped = Skipped(result);
            return skipped.Length > 0 ? $"{text} ({skipped})" : text;
        }

        // What a quick stack had to leave out: "2 chests in use; 1 chest not handed to you in time", or "" (the log says why, card A69).
        private static string Skipped(StackResult result)
        {
            var skipped = new List<string>();
            if (result.ChestsInUse > 0)
            {
                skipped.Add($"{Count(result.ChestsInUse, "chest", "chests")} in use");
            }
            if (result.ChestsNoAnswer > 0)
            {
                skipped.Add($"{Count(result.ChestsNoAnswer, "chest", "chests")} not handed to you in time");
            }
            if (result.ChestsOutOfReach > 0)
            {
                skipped.Add($"{Count(result.ChestsOutOfReach, "chest", "chests")} beyond the server's reach");
            }
            if (result.ChestsReserved > 0)
            {
                skipped.Add($"{Count(result.ChestsReserved, "chest", "chests")} reserved meanwhile");
            }
            return string.Join("; ", skipped);
        }

        private static string Count(int number, string one, string many) => $"{number} {(number == 1 ? one : many)}";

        private static void Show(MessageHud.MessageType where, string text) => Say(where, Fit(where, text));

        /// <summary>
        /// A message fitted to the screen (card A82): as the game will show it (localised), every line wider than the room
        /// for that kind of message broken at spaces into lines that fit. A message that fits already is sent exactly as it
        /// was. Fitting a fitted message changes nothing. Never fails: without a HUD, or if measuring goes wrong, the text
        /// goes as it is.
        /// </summary>
        internal static string Fit(MessageHud.MessageType where, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            try
            {
                System.Func<string, float> width = Measure(where);
                float room = Room(where);
                if (width == null || !(room > 0f))
                {
                    return text;
                }
                string shown = Localize(text) ?? text;
                string fitted = Wrap(shown, width, room);
                return ReferenceEquals(fitted, shown) ? text : fitted;
            }
            catch (System.Exception)
            {
                return text;   // a message as it was beats no message
            }
        }

        /// <summary>
        /// Breaks every line of <paramref name="text"/> wider than <paramref name="room"/> (as <paramref name="width"/>
        /// measures it) at spaces into lines that fit, each as full as it can be; a single word wider than the room (a name
        /// in a language written without spaces) is broken between its letters, never inside a letter of two halves.
        /// Spaces are kept as they were, but the one each break replaces. Returns the very same string when every line fits
        /// already. Plain code, for the scratch test harness too.
        /// </summary>
        internal static string Wrap(string text, System.Func<string, float> width, float room)
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
        private static List<string> Pieces(string word, System.Func<string, float> width, float room)
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

        // The room for a line of that kind, from the HUD's own canvas: the middle message centred where its text is, the
        // top-left one from where its text starts; 0 without a HUD.
        private static float GameRoom(MessageHud.MessageType where)
        {
            TMP_Text text = MessageHold.TextOf(where);
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
