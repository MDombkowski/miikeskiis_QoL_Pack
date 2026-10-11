using System;
using System.Collections.Generic;
using System.Text;

namespace MapViewRules
{
    /// <summary>
    /// The override list: the kinds and pin names the player gives rules of their own, typed as one setting. A name
    /// matches in any word order and case, so "elite draugr spawner" finds the pin "Draugr Elite Spawner", and
    /// "cloudberry" the kind Cloudberry.
    /// </summary>
    internal static class Words
    {
        /// <summary>The most names the list takes; each becomes a section of ten settings.</summary>
        internal const int MostOverrides = 40;

        private const int LongestName = 60;

        /// <summary>A text's words in lower case, sorted and without repeats, joined by single spaces: two texts with the
        /// same words in any order and case give the same signature. Empty for a text without letters or digits.</summary>
        internal static string Signature(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var words = new List<string>();
            var word = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    word.Append(char.ToLowerInvariant(c));
                }
                else if (word.Length > 0)
                {
                    words.Add(word.ToString());
                    word.Clear();
                }
            }
            if (word.Length > 0)
            {
                words.Add(word.ToString());
            }
            words.Sort(StringComparer.Ordinal);
            var result = new StringBuilder();
            string last = null;
            foreach (string each in words)
            {
                if (each == last)
                {
                    continue;
                }
                if (result.Length > 0)
                {
                    result.Append(' ');
                }
                result.Append(each);
                last = each;
            }
            return result.ToString();
        }

        /// <summary>A name's words in the order typed, each starting with a capital and the rest in lower case, one space
        /// apart: "elite draugr  SPAWNER" is "Elite Draugr Spawner", "Pickable:Cloudberry" "Pickable Cloudberry". Names an
        /// override's section the same whatever the case and spacing it was typed in; another word order is another name.</summary>
        internal static string Title(string name)
        {
            var text = new StringBuilder(name?.Length ?? 0);
            bool inWord = false;
            foreach (char c in name ?? string.Empty)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    inWord = false;
                    continue;
                }
                if (!inWord && text.Length > 0)
                {
                    text.Append(' ');
                }
                text.Append(inWord ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c));
                inWord = true;
            }
            return text.ToString();
        }

        /// <summary>
        /// The names in the override list, in its order: separated by commas, semicolons or new lines, trimmed, without
        /// characters a settings section can't hold (= ' " [ ] \ and tabs), without empty ones and without a second
        /// name with the same words. At most MostOverrides.
        /// </summary>
        internal static List<string> ParseList(string text)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return names;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string raw in text.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = Clean(raw);
                string signature = Signature(name);
                if (signature.Length == 0 || !seen.Add(signature))
                {
                    continue;
                }
                names.Add(name);
                if (names.Count == MostOverrides)
                {
                    break;
                }
            }
            return names;
        }

        // Drops what BepInEx refuses in a section name, collapses runs of spaces and keeps it to a sensible length.
        private static string Clean(string raw)
        {
            var text = new StringBuilder();
            bool space = false;
            foreach (char c in raw)
            {
                if (char.IsWhiteSpace(c))   // before the control characters: a tab is both, and parts two words
                {
                    space = text.Length > 0;
                    continue;
                }
                if (c == '=' || c == '\'' || c == '"' || c == '[' || c == ']' || c == '\\' || char.IsControl(c))
                {
                    continue;
                }
                if (space)
                {
                    text.Append(' ');
                    space = false;
                }
                text.Append(c);
            }
            string name = text.ToString();
            return name.Length > LongestName ? name.Substring(0, LongestName).TrimEnd() : name;
        }
    }
}
