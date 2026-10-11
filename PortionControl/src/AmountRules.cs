using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PortionControl
{
    /// <summary>What one rule says: its fixed amounts and its percentages, each null where the rule doesn't set that kind
    /// (the next rule down, or the general setting, decides it), and empty where it asks for none ("0", "0%").</summary>
    internal sealed class AmountList
    {
        internal List<int> Fixed;

        internal List<int> Percents;
    }

    /// <summary>
    /// His amounts by item, category and stack size (his feedback of 2026-09-30, A17): three settings, each a list of
    /// rules written "key: amounts; key: amounts", a key being an item's shared name ("$item_coal"), a category or a
    /// category's member ("Wood", "Metals/Iron", the pack's families as the catalogue part names them), or a stack size
    /// ("50"). Amounts are written as the general settings are, a % after a percentage: "10, 20, 50, 25%". The most
    /// specific rule wins, each kind of button apart: item, member, category, stack size, then the general settings.
    /// A key with no amounts yet is kept (a ticked item waiting for its list) and decides nothing. No Unity here, so a
    /// console harness can check every rule.
    /// </summary>
    internal static class AmountRules
    {
        /// <summary>Between a family and its member in a category's key.</summary>
        internal const char MemberMark = '/';

        /// <summary>
        /// Reads a rules setting into its keys, in the order written, each with its amounts as typed. Entries are
        /// separated by ";" (or a new line), a key from its amounts by the first ":". An entry with no key is skipped;
        /// a key written twice keeps its last amounts.
        /// </summary>
        internal static List<KeyValuePair<string, string>> ReadEntries(string text)
        {
            var entries = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrEmpty(text))
            {
                return entries;
            }
            foreach (string part in text.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = part.IndexOf(':');
                string key = (colon >= 0 ? part.Substring(0, colon) : part).Trim();
                string amounts = colon >= 0 ? part.Substring(colon + 1).Trim() : string.Empty;
                if (key.Length == 0)
                {
                    continue;
                }
                int known = entries.FindIndex(entry => string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
                if (known >= 0)
                {
                    entries[known] = new KeyValuePair<string, string>(entries[known].Key, amounts);
                }
                else
                {
                    entries.Add(new KeyValuePair<string, string>(key, amounts));
                }
            }
            return entries;
        }

        /// <summary>Writes rules back as ReadEntries reads them: "key: amounts; key: amounts". The characters that
        /// separate entries and keys are taken out of what was typed, so a typo can't make a new key.</summary>
        internal static string WriteEntries(IEnumerable<KeyValuePair<string, string>> entries)
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, string> entry in entries)
            {
                string key = Clean(entry.Key, true);
                if (key.Length == 0)
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append("; ");
                }
                text.Append(key).Append(": ").Append(Clean(entry.Value, false));
            }
            return text.ToString();
        }

        private static string Clean(string text, bool key)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            var clean = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == ';' || c == ':' || c == '\n' || c == '\r')
                {
                    clean.Append(key ? ' ' : ',');
                    continue;
                }
                clean.Append(c);
            }
            return clean.ToString().Trim();
        }

        /// <summary>
        /// Reads one rule's amounts: whole numbers are fixed amounts (1 to 99999, up to Amounts.MostFixed), a number with
        /// a % is a percentage (1 to 100, up to Amounts.MostPercentages), each once, in the order typed. "0" asks for no
        /// fixed amounts and "0%" for no percentages (anything else of that kind beside it wins). A word or a fraction is
        /// skipped, so a typo loses only itself.
        /// </summary>
        internal static AmountList ReadAmounts(string text)
        {
            var list = new AmountList();
            if (string.IsNullOrEmpty(text))
            {
                return list;
            }
            bool noFixed = false;
            bool noPercents = false;
            foreach (string part in JoinPercentSigns(text).Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = part.Trim();
                bool percent = token.EndsWith("%", StringComparison.Ordinal);
                if (percent)
                {
                    token = token.Substring(0, token.Length - 1).Trim();
                }
                if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                {
                    continue;
                }
                if (percent)
                {
                    if (value == 0)
                    {
                        noPercents = true;
                    }
                    else if (value <= 100)
                    {
                        Add(ref list.Percents, value, Amounts.MostPercentages);
                    }
                }
                else if (value == 0)
                {
                    noFixed = true;
                }
                else if (value <= 99999)
                {
                    Add(ref list.Fixed, value, Amounts.MostFixed);
                }
            }
            if (list.Fixed == null && noFixed)
            {
                list.Fixed = new List<int>();
            }
            if (list.Percents == null && noPercents)
            {
                list.Percents = new List<int>();
            }
            return list;
        }

        // "25 %" is 25%: a % after spaces belongs to the number before it.
        private static string JoinPercentSigns(string text)
        {
            var joined = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c == '%')
                {
                    while (joined.Length > 0 && (joined[joined.Length - 1] == ' ' || joined[joined.Length - 1] == '\t'))
                    {
                        joined.Length--;
                    }
                }
                joined.Append(c);
            }
            return joined.ToString();
        }

        private static void Add(ref List<int> into, int value, int most)
        {
            if (into == null)
            {
                into = new List<int>();
            }
            if (into.Count < most && !into.Contains(value))
            {
                into.Add(value);
            }
        }

        /// <summary>A category's key: the family alone, or "Family/Member".</summary>
        internal static string CategoryKey(string family, string member) =>
            string.IsNullOrEmpty(member) ? family : family + MemberMark + member;

        /// <summary>
        /// The lists a stack's split window offers: for each kind of button apart, the most specific rule that sets it
        /// (the item's, its category member's, its category's, its stack size's), else the general setting's.
        /// </summary>
        internal static (List<int> Fixed, List<int> Percents) For(Parsed rules, string sharedName, string family, string member,
                                                                 int stackSize, List<int> generalFixed, List<int> generalPercents)
        {
            List<int> fixeds = null;
            List<int> percents = null;
            if (rules != null)
            {
                foreach (AmountList rule in new[]
                {
                    rules.Find(rules.Items, sharedName),
                    string.IsNullOrEmpty(member) ? null : rules.Find(rules.Categories, CategoryKey(family, member)),
                    rules.Find(rules.Categories, family),
                    rules.Find(rules.StackSizes, stackSize.ToString(CultureInfo.InvariantCulture)),
                })
                {
                    if (rule == null)
                    {
                        continue;
                    }
                    fixeds = fixeds ?? rule.Fixed;
                    percents = percents ?? rule.Percents;
                }
            }
            return (fixeds ?? generalFixed, percents ?? generalPercents);
        }

        /// <summary>The three settings, read once each time one of them changes.</summary>
        internal sealed class Parsed
        {
            internal readonly Dictionary<string, AmountList> Items = new Dictionary<string, AmountList>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<string, AmountList> Categories = new Dictionary<string, AmountList>(StringComparer.OrdinalIgnoreCase);
            internal readonly Dictionary<string, AmountList> StackSizes = new Dictionary<string, AmountList>(StringComparer.Ordinal);

            internal Parsed(string items, string categories, string stackSizes)
            {
                Fill(Items, items);
                Fill(Categories, categories);
                foreach (KeyValuePair<string, string> entry in ReadEntries(stackSizes))
                {
                    // "50" or "Stacks of 50": the number is the key.
                    string digits = new string(Array.FindAll(entry.Key.ToCharArray(), char.IsDigit));
                    if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int size) && size > 0)
                    {
                        StackSizes[size.ToString(CultureInfo.InvariantCulture)] = ReadAmounts(entry.Value);
                    }
                }
            }

            private static void Fill(Dictionary<string, AmountList> into, string text)
            {
                foreach (KeyValuePair<string, string> entry in ReadEntries(text))
                {
                    into[entry.Key] = ReadAmounts(entry.Value);
                }
            }

            internal AmountList Find(Dictionary<string, AmountList> from, string key) =>
                key != null && from.TryGetValue(key, out AmountList list) ? list : null;
        }
    }
}
