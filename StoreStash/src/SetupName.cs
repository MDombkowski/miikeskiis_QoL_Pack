using System;
using System.Reflection;
using QoLMods.Chests;

namespace StoreStash
{
    /// <summary>
    /// A chest's own name (card A41's second round, decision 27): kept in the chest's record under the game's "text" key,
    /// where MyLittleUI keeps the names it gives chests (up to 32 characters), so a name given in either shows in both, and
    /// friends with MyLittleUI see it. A cart's or ship's chest keeps it in the vehicle's record, which is the chest's
    /// (ChestHooks.NetView). The game's own chests never use the key. The setup window shows it in its title and renames
    /// the chest there (SetupWindow, while "Rename the chest in the setup window" is on).
    /// </summary>
    internal static class SetupName
    {
        /// <summary>The longest name: MyLittleUI's rename box takes 32 characters.</summary>
        internal const int MaxLength = 32;

        /// <summary>The chest's name as its record holds it, trimmed; empty when it has none (or isn't loaded): to compare
        /// with, never to show (Read). Its first 1,024 units only (review round 6: the window reads it every frame, and a name
        /// another game wrote may be far longer; no name a player gives is). Only the scratch test harness sets another (it
        /// has no records).</summary>
        internal static Func<Container, string> Raw = chest =>
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            string name = Capped(view != null && view.IsValid() ? view.GetZDO().GetString(ZDOVars.s_text, string.Empty) : string.Empty);
            return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        };

        /// <summary>The chest's name as it may be shown (Plain, over Raw); empty when it has none (or isn't loaded). Only the
        /// scratch test harness sets another (it has no records).</summary>
        internal static Func<Container, string> Read = chest => Plain(Capped(Raw(chest)));

        // At most this much of a record's name is looked at to show it: only MaxLength characters ever show, and the window
        // reads the name every frame (review round 5: a name of a hundred thousand characters, written by another game, would
        // otherwise cost that much each frame). Never cut between the two halves of an emoji.
        private const int LookedAt = 1024;

        private static string Capped(string name) =>
            name == null || name.Length <= LookedAt ? name : name.Substring(0, char.IsHighSurrogate(name[LookedAt - 1]) ? LookedAt - 1 : LookedAt);

        /// <summary>
        /// A name as it may be shown, whatever another game wrote into the record (the last look; LESSONS_LEARNED, text
        /// another player's game wrote): Setups.Shown (no tag, no "$" or "\", a line break as a space, no invisible
        /// formatting character, no half an emoji), trimmed, at most MaxLength characters, cut only where a character ends
        /// (Cut); and a name of nothing but blanks (white space, joiners, the blank fillers games use: Blank) is no name
        /// (review rounds 5 and 6: it showed blank, not the chest's kind). A name MyLittleUI or this window gave keeps every
        /// other character: punctuation, accents, emoji.
        /// </summary>
        internal static string Plain(string name)
        {
            string text = Setups.Shown(name).Trim();
            text = text.Length > MaxLength ? Cut(text, MaxLength) : text;
            foreach (char each in text)
            {
                if (!Blank(each))
                {
                    return text;
                }
            }
            return string.Empty;
        }

        // A character that shows as nothing: white space (a no-break space too), the two joiners, and the blank fillers
        // games use for "invisible" names (review round 6): the Hangul fillers U+3164, U+115F, U+1160 and U+FFA0, the
        // empty Braille pattern U+2800, and the combining grapheme joiner U+034F.
        private static bool Blank(char each) =>
            char.IsWhiteSpace(each) || each == (char)0x200D || each == (char)0x200C || each == (char)0x3164 || each == (char)0x115F ||
            each == (char)0x1160 || each == (char)0xFFA0 || each == (char)0x2800 || each == (char)0x034F;

        /// <summary>At most <paramref name="most"/> characters of <paramref name="text"/>, cut only where a character ends
        /// (never between the two halves of an emoji, or before a combining accent), never ending on a joiner or on half a
        /// flag (review round 2 of the last look); trimmed at the end.</summary>
        internal static string Cut(string text, int most)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= most)
            {
                return text ?? string.Empty;
            }
            int cut = 0;
            foreach (int start in System.Globalization.StringInfo.ParseCombiningCharacters(text))
            {
                if (start > most)
                {
                    break;
                }
                cut = start;   // the last place a character starts, at most `most` in
            }
            string kept = Strip(text.Substring(0, cut));
            if (AllBlank(kept))
            {
                // Nothing that shows left where a character ends (a first character longer than `most` on its own, perhaps
                // after what the strips take or blank fillers: a letter with dozens of accents, another mod's name; review
                // rounds 3, 5 and 7): cut where a pair of halves ends instead, rather than show nothing.
                kept = Strip(text.Substring(0, char.IsHighSurrogate(text[most - 1]) ? most - 1 : most));
            }
            return kept;
        }

        // Whether every character of a text is Blank (an empty text is).
        private static bool AllBlank(string text)
        {
            foreach (char each in text)
            {
                if (!Blank(each))
                {
                    return false;
                }
            }
            return true;
        }

        // Off the end, again until none is left: white space, a joiner with nothing left to join (U+200D, U+200C), and half a
        // flag (a flag is two regional letters, each a pair of halves: U+1F1E6 to U+1F1FF). 34-B's harness found a joiner just
        // before half a flag left behind when each was taken off once.
        private static string Strip(string kept)
        {
            while (true)
            {
                kept = kept.TrimEnd();
                if (kept.Length > 0 && (kept[kept.Length - 1] == (char)0x200D || kept[kept.Length - 1] == (char)0x200C))
                {
                    kept = kept.Substring(0, kept.Length - 1);
                    continue;
                }
                int letters = 0;
                for (int i = kept.Length; i >= 2 && kept[i - 2] == (char)0xD83C && kept[i - 1] >= (char)0xDDE6 && kept[i - 1] <= (char)0xDDFF; i -= 2)
                {
                    letters++;
                }
                if (letters % 2 == 1)
                {
                    kept = kept.Substring(0, kept.Length - 2);
                    continue;
                }
                return kept;
            }
        }

        /// <summary>
        /// What to write when he leaves the window's name box, from what he <paramref name="typed"/>, what the box
        /// <paramref name="shown"/> when he began, the chest's name now (<paramref name="current"/>, as Raw gives it: the
        /// record's own, so a name made only of characters the window doesn't show can still be cleared) and its
        /// <paramref name="kind"/> ("Black Metal Chest"). Null for nothing: he left the box as it showed (a click in and
        /// out), nothing he typed can be shown (ShowsNothing: the name stays as it is), or it comes to the name the chest
        /// already has. Empty to clear the name: nothing typed, or the kind itself,
        /// in any case ("black metal chest" on a Black Metal Chest). Else what he typed as it will be shown (Plain: at most
        /// MaxLength characters), so the box shows what he typed after Enter, and MyLittleUI the same (review round 2 of
        /// the last look). Plain values only, for the scratch harness.
        /// </summary>
        internal static string ToWrite(string typed, string shown, string current, string kind)
        {
            typed = typed ?? string.Empty;
            if (typed == (shown ?? string.Empty))
            {
                return null;
            }
            string name = Plain(typed);
            if (ShowsNothing(typed))
            {
                return null;   // nothing he typed can be shown: the name stays as it is, and the window says why (review round 3)
            }
            if (name.Length == 0 || string.Equals(name, Plain(kind), StringComparison.OrdinalIgnoreCase))
            {
                name = string.Empty;
            }
            return name == (current ?? string.Empty) ? null : name;
        }

        /// <summary>Whether he typed something of which nothing can be shown (only tags, "&lt;", "$" and the like: Plain
        /// leaves nothing): ToWrite then writes nothing, rather than clearing the name or dropping it unsaid (review round
        /// 3).</summary>
        internal static bool ShowsNothing(string typed) => !string.IsNullOrWhiteSpace(typed) && Plain(typed).Length == 0;

        /// <summary>
        /// Gives a chest this computer owns a name (empty: none), as MyLittleUI's rename does, and sends its record at once;
        /// then lets other mods' hover texts catch up (ChestChanged). False, writing nothing, when the chest isn't his (or
        /// isn't loaded); true, sending nothing, when it has that name already.
        /// </summary>
        internal static bool Write(Container chest, string name)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return false;
            }
            name = Cut((name ?? string.Empty).Trim(), MaxLength);
            ZDO record = view.GetZDO();
            if (record.GetString(ZDOVars.s_text, string.Empty) == name)
            {
                return true;
            }
            record.Set(ZDOVars.s_text, name);
            ZDOMan.instance?.ForceSendZDO(record.m_uid);
            ChestChanged(chest);
            return true;
        }

        /// <summary>MyLittleUI's plugin ID: its hover text keeps a chest's name until the chest changes (ChestChanged).</summary>
        internal const string MyLittleUiGuid = "shudnal.MyLittleUI";

        /// <summary>Whether MyLittleUI is loaded. Only the scratch test harness sets another (it loads no plugins).</summary>
        internal static Func<bool> MyLittleUiLoaded = () => BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(MyLittleUiGuid);

        // The game's private members, by name (as GameHooks and the chest service reach theirs).
        private static readonly FieldInfo Loading = typeof(Container).GetField("m_loading", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo OnChanged =
            typeof(Container).GetMethod("OnContainerChanged", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        private static bool warned;

        /// <summary>
        /// The game's own "this chest changed" (Container.OnContainerChanged, private), called as when the chest reloads
        /// from its record: with its loading flag set, so the game saves nothing (it saves only when not loading) and the
        /// chest's items aren't saved, changed or moved, while every mod's patch on it runs. MyLittleUI's hover text keeps
        /// a chest's text until then (a postfix there empties its cache), so its hover shows the new name at once. A
        /// courtesy: when the game has changed (either member missing), or another mod's patch fails, nothing more happens
        /// (the latter written to the log once).
        /// </summary>
        internal static void ChestChanged(Container chest)
        {
            // Only for MyLittleUI's hover (review round 1): another mod's patch on OnContainerChanged might move items, which
            // the loading flag would then leave unsaved.
            if (chest == null || !MyLittleUiLoaded() || Loading == null || OnChanged == null || Loading.FieldType != typeof(bool) ||
                (bool)Loading.GetValue(chest))
            {
                return;
            }
            Loading.SetValue(chest, true);
            try
            {
                OnChanged.Invoke(chest, null);
            }
            catch (Exception error)
            {
                if (!warned)
                {
                    warned = true;
                    Exception inner = error is TargetInvocationException && error.InnerException != null ? error.InnerException : error;
                    Module.Log.LogWarning($"Renamed a chest, but another mod's hover text may show its old name until the chest changes: {inner.GetType().Name}: {inner.Message}");
                }
            }
            finally
            {
                Loading.SetValue(chest, false);
            }
        }
    }
}
