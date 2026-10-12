// The QoL Mods list picker, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ListPicker\. Every mod that uses
// it carries a copy in its own parts\ListPicker\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace QoLMods.ListPicker
{
    /// <summary>
    /// How one list setting's text is written, exactly as its module reads it: what separates the entries, whether each
    /// carries an amount ("Wood:20"), whether their order means something, and how two names are compared. The picker
    /// reads and writes the text by these rules only, so what it writes is what the module reads, and every entry it
    /// doesn't touch (another mod's things, a name it doesn't know) is kept as written.
    /// </summary>
    internal sealed class ListFormat
    {
        // Each module sets only the fields its own lists need (in its own build the others are never assigned).
#pragma warning disable CS0649
        /// <summary>The characters that separate entries when the text is read.</summary>
        internal string Separators = ",;";

        /// <summary>Spaces (and tabs and line breaks) separate entries too, so a name never holds one (Quick Fix's
        /// lists).</summary>
        internal bool WhitespaceSeparates;

        /// <summary>Each entry may carry an amount after a colon, "name:amount" (reserves, a cost).</summary>
        internal bool Amounts;

        /// <summary>The characters that may stand between a name and its amount (the first one in the entry counts):
        /// a colon, and for Topper-Upper's lists an equals sign too. A changed amount keeps the entry's own.</summary>
        internal string AmountMarks = ":";

        /// <summary>The largest amount the window's amount box writes.</summary>
        internal int MostAmount = 99999;

        /// <summary>The amount a new entry is written with.</summary>
        internal int NewAmount = 1;

        /// <summary>The smallest amount the window's amount box writes.</summary>
        internal int LeastAmount = 1;

        /// <summary>What an entry without an amount counts as, said beside it (Hoarstone's cost: 1); null when the
        /// module ignores such an entry.</summary>
        internal int? AmountLeftOut;

        /// <summary>A name given twice: the module adds the amounts up (Hoarstone's cost); else the last one counts
        /// (Topper-Upper's reserves). Only for what the window shows: the text keeps every entry as written.</summary>
        internal bool AmountsAddUp;

        /// <summary>The entries' order means something (what a station takes first): the window moves them.</summary>
        internal bool Ordered;

        /// <summary>Two names are the same whatever their case.</summary>
        internal bool IgnoreCase = true;

        /// <summary>Two names are the same whatever their spaces.</summary>
        internal bool IgnoreSpaces;

        /// <summary>What joins the entries of a text that shows no join of its own yet (one entry, or none).</summary>
        internal string Joiner = ",";

        /// <summary>How the module itself turns a name into what it compares (House Trained drops spaces, underscores and
        /// "$"; Use Case reads "&amp;" as "and"); null: IgnoreCase and IgnoreSpaces alone.</summary>
        internal System.Func<string, string> Normal;
#pragma warning restore CS0649
    }

    /// <summary>One entry of a list setting, as written: its name, and its amount when the list has amounts.</summary>
    internal sealed class ListEntry
    {
        /// <summary>The name as written (trimmed).</summary>
        internal string Name = string.Empty;

        /// <summary>The amount as written after the colon (trimmed); null when the entry has no colon.</summary>
        internal string Amount;

        /// <summary>The character written between the name and the amount (the entry's own, or the format's first).</summary>
        internal char Mark = ':';

        /// <summary>The whole entry exactly as it was read (trimmed), written back unchanged; null once the window
        /// changed it, or for a new entry.</summary>
        internal string Written;

        /// <summary>The amount it counts as: its own when it reads as a whole number the format allows, else the format's
        /// for one left out (null for one that can't be read).</summary>
        internal int? Count(ListFormat format)
        {
            if (Amount == null)
            {
                return format.AmountLeftOut;
            }
            // An amount the module would refuse (outside the least and most it takes) counts as none, as the module leaves it out.
            return int.TryParse(Amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) &&
                   value >= format.LeastAmount && value <= format.MostAmount
                ? value
                : (int?)null;
        }

        /// <summary>The entry as the text holds it.</summary>
        internal string Text(ListFormat format)
        {
            if (Written != null)
            {
                return Written;
            }
            return format.Amounts && Amount != null ? Name + Mark + Amount : Name;
        }
    }

    /// <summary>
    /// A list setting's text read into entries and written back (pure: no game, no Unity, so a harness runs it as it is).
    /// Reading splits at the format's separators and trims each entry; an empty piece (",,") is no entry. Writing joins
    /// the entries in their order with the join the text already uses (", " where it has a comma and a space, "," where it
    /// has a bare comma, and so on), each untouched entry exactly as it was read. So unknown names, odd amounts and the
    /// order all survive every change the window makes.
    /// </summary>
    internal static class ListText
    {
        /// <summary>The entries of a text, in order.</summary>
        internal static List<ListEntry> Read(string text, ListFormat format)
        {
            var entries = new List<ListEntry>();
            if (string.IsNullOrEmpty(text))
            {
                return entries;
            }
            var piece = new StringBuilder();
            foreach (char c in text)
            {
                if (IsSeparator(c, format))
                {
                    Add(entries, piece.ToString(), format);
                    piece.Length = 0;
                }
                else
                {
                    piece.Append(c);
                }
            }
            Add(entries, piece.ToString(), format);
            return entries;
        }

        private static bool IsSeparator(char c, ListFormat format) =>
            format.Separators.IndexOf(c) >= 0 || (format.WhitespaceSeparates && char.IsWhiteSpace(c));

        private static void Add(List<ListEntry> entries, string piece, ListFormat format)
        {
            string written = piece.Trim();
            if (written.Length == 0)
            {
                return;
            }
            var entry = new ListEntry { Written = written, Name = written, Mark = MarkOf(format) };
            int mark = format.Amounts ? written.IndexOfAny(format.AmountMarks.ToCharArray()) : -1;
            if (mark >= 0)
            {
                entry.Name = written.Substring(0, mark).Trim();
                entry.Amount = written.Substring(mark + 1).Trim();
                entry.Mark = written[mark];
            }
            entries.Add(entry);
        }

        /// <summary>The text of these entries, joined as <paramref name="before"/> (the text they were read from) joins
        /// its own.</summary>
        internal static string Write(IList<ListEntry> entries, ListFormat format, string before)
        {
            string joiner = JoinerOf(before, format);
            var text = new StringBuilder();
            foreach (ListEntry entry in entries)
            {
                string one = entry.Text(format);
                if (one.Length == 0)
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append(joiner);
                }
                text.Append(one);
            }
            return text.ToString();
        }

        /// <summary>
        /// The join a text uses between its entries: its first separator with the spaces right after it (", " or ","
        /// or "; "), or a single space for a list split by spaces alone; the format's own for a text with no join yet.
        /// </summary>
        internal static string JoinerOf(string text, ListFormat format)
        {
            if (!string.IsNullOrEmpty(text))
            {
                string trimmed = text.Trim();
                for (int i = 0; i < trimmed.Length; i++)
                {
                    if (format.Separators.IndexOf(trimmed[i]) < 0)
                    {
                        continue;
                    }
                    int end = i + 1;
                    while (end < trimmed.Length && trimmed[end] == ' ')
                    {
                        end++;
                    }
                    return trimmed.Substring(i, end - i);
                }
                if (format.WhitespaceSeparates)
                {
                    foreach (char c in trimmed)
                    {
                        if (char.IsWhiteSpace(c))
                        {
                            return " ";
                        }
                    }
                }
            }
            return format.Joiner;
        }

        private static char MarkOf(ListFormat format) =>
            string.IsNullOrEmpty(format.AmountMarks) ? ':' : format.AmountMarks[0];

        /// <summary>Whether the text shows a join of its own between two entries (a separator, or for a list split by
        /// spaces, a space).</summary>
        internal static bool HasJoin(string text, ListFormat format)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string trimmed = text.Trim();
            foreach (char c in trimmed)
            {
                if (format.Separators.IndexOf(c) >= 0 || (format.WhitespaceSeparates && char.IsWhiteSpace(c)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A name as the format compares it.</summary>
        internal static string Key(string name, ListFormat format)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }
            if (format.Normal != null)
            {
                return format.Normal(name) ?? string.Empty;
            }
            string key = name.Trim();
            if (format.IgnoreSpaces)
            {
                key = key.Replace(" ", string.Empty).Replace("\t", string.Empty);
            }
            return format.IgnoreCase ? key.ToLowerInvariant() : key;
        }

        /// <summary>A new entry for a name, with the format's amount when the list has amounts.</summary>
        internal static ListEntry New(string name, ListFormat format) => new ListEntry
        {
            Name = name ?? string.Empty,
            Amount = format.Amounts ? format.NewAmount.ToString(CultureInfo.InvariantCulture) : null,
            Mark = MarkOf(format),
        };

        /// <summary>
        /// Whether the entries name a thing: <paramref name="names"/> are the keys the thing goes by (ListText.Key of each
        /// name the module would take for it: its prefab name, and for some lists its name as shown or its $ token).
        /// </summary>
        internal static bool Holds(IList<ListEntry> entries, ICollection<string> names, ListFormat format)
        {
            foreach (ListEntry entry in entries)
            {
                if (names.Contains(Key(entry.Name, format)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// A click on a thing: takes out every entry that names it, or, when none does, adds it at the end under
        /// <paramref name="name"/> (with the format's amount). Returns true when it added.
        /// </summary>
        internal static bool Toggle(List<ListEntry> entries, ICollection<string> names, string name, ListFormat format)
        {
            int removed = entries.RemoveAll(entry => names.Contains(Key(entry.Name, format)));
            if (removed > 0)
            {
                return false;
            }
            entries.Add(New(name, format));
            return true;
        }

        /// <summary>
        /// Sets one entry's amount from what was typed, when it reads as a whole number the format allows; its name and
        /// mark kept as written. Returns false (and changes nothing) for anything else, so a half-typed amount is never
        /// written for the module to refuse.
        /// </summary>
        internal static bool SetAmount(ListEntry entry, string typed, ListFormat format)
        {
            string amount = (typed ?? string.Empty).Trim();
            if (!int.TryParse(amount, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
                value < format.LeastAmount || value > format.MostAmount)
            {
                return false;
            }
            amount = value.ToString(CultureInfo.InvariantCulture);
            if (amount == entry.Amount)
            {
                return true;
            }
            entry.Amount = amount;
            entry.Written = null;
            return true;
        }

        /// <summary>Moves one entry to another place in the list (the others keep their order).</summary>
        internal static void Move(List<ListEntry> entries, int from, int to)
        {
            if (from < 0 || from >= entries.Count)
            {
                return;
            }
            to = Math.Max(0, Math.Min(entries.Count - 1, to));
            if (to == from)
            {
                return;
            }
            ListEntry entry = entries[from];
            entries.RemoveAt(from);
            entries.Insert(to, entry);
        }
    }
}
