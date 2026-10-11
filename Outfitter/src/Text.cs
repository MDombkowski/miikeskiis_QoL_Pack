using System.Text;

namespace Outfitter
{
    /// <summary>
    /// Text Outfitter shows that came from a player: a loadout's name (his own typing, or a friend's code), a prefab name
    /// from a code. The game's text (TextMeshPro) reads a tag even without its "&gt;", and its localisation reads a "$"
    /// word as a lookup (LESSONS_LEARNED, job 24), so a name keeps only letters, digits, spaces and a few plain marks:
    /// nothing a tag, a lookup or an invisible character could hide in. A strict list rather than StoreStash's filter for
    /// chests' names (src\Setups.cs), which keeps every character it can make safe: a loadout's name needs no more.
    /// </summary>
    internal static class Text
    {
        private const string Marks = "-_.,'!?()&+#";

        /// <summary>The text as Outfitter shows it: only letters, digits, single spaces and the marks above, trimmed, at
        /// most <paramref name="max"/> characters; empty when nothing is left.</summary>
        internal static string Plain(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || max <= 0)
            {
                return string.Empty;
            }
            var plain = new StringBuilder(System.Math.Min(text.Length, max));
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c) || Marks.IndexOf(c) >= 0)
                {
                    bool gap = space && plain.Length > 0;
                    if (plain.Length + (gap ? 2 : 1) > max)
                    {
                        break;   // never past max, a space and a letter included (R1, finding 25)
                    }
                    if (gap)
                    {
                        plain.Append(' ');
                    }
                    space = false;
                    plain.Append(c);
                }
                else if (char.IsWhiteSpace(c))
                {
                    space = true;
                }
                // Anything else (tags' brackets, "$", "\", control and invisible characters, half an emoji) goes.
            }
            return plain.ToString();
        }

        /// <summary>A prefab name as a code may carry it: English letters, digits, "_", "-", ".", "(", ")" and spaces (the
        /// game's 1,519 item prefabs use only letters, digits, "_" and, once, a space; other mods' items may use the
        /// rest), at most 64 characters, not starting or ending with a space; null when it holds anything else (the code is
        /// then refused).</summary>
        internal static string PrefabName(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 64 || text[0] == ' ' || text[text.Length - 1] == ' ')
            {
                return null;
            }
            foreach (char c in text)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ||
                          c == '_' || c == '-' || c == '.' || c == '(' || c == ')' || c == ' ';
                if (!ok)
                {
                    return null;
                }
            }
            return text;
        }
    }
}
