using System.Collections.Generic;
using System.Text;
using QoLMods.Catalogue;
using QoLMods.Chests;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// What he's told, as the game's own messages are: top left, or in the middle of the screen. Every message is fitted to
    /// the screen first, by the kit's Say.Fit (card A82's fitter, written here first and the whole pack's since card B14):
    /// a line too wide for the screen is broken at spaces into lines that fit, measured by the game's own text.
    /// </summary>
    internal static class Messages
    {
        /// <summary>The most set-up chests the where-lines name, one line each; then "and 2 more chests".</summary>
        internal const int MostChests = 5;

        /// <summary>The most kinds of things a where-line names for one chest; then "and 3 more".</summary>
        internal const int MostKinds = 4;

        /// <summary>Shows a message to him. Only the scratch test harness sets another (to read what he's told).</summary>
        internal static System.Action<MessageHud.MessageType, string> Say = (where, text) => QoLMods.Kit.Say.To(where, text);

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
            text = QoLMods.Kit.Say.Fit(type, text);   // so the hold knows the message by its lines as shown
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

        private static void Show(MessageHud.MessageType where, string text) => Say(where, QoLMods.Kit.Say.Fit(where, text));
    }
}
