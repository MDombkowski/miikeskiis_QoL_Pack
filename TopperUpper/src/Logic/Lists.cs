using System;
using System.Collections.Generic;
using System.Globalization;

namespace TopperUpper.Logic
{
    /// <summary>
    /// An "item:amount" list as a setting writes it ("Resin:50, BlackMetalScrap:30"): the reserves and the maximums. Items
    /// by the game's prefab names, as the pack names things so the rules hold in every language; case doesn't matter.
    /// Separated by commas, semicolons or new lines; an amount may follow a colon or an equals sign. An item named twice
    /// keeps its last amount. Entries it can't read are kept in Bad, for one line in the log.
    /// </summary>
    internal sealed class ItemAmounts
    {
        private readonly Dictionary<string, int> amounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The entries it couldn't read, as written.</summary>
        internal readonly List<string> Bad = new List<string>();

        /// <summary>The items named, by prefab name as written, with their amounts.</summary>
        internal IReadOnlyDictionary<string, int> Amounts => amounts;

        internal int Count => amounts.Count;

        internal static ItemAmounts Parse(string text)
        {
            var list = new ItemAmounts();
            foreach (string raw in Pieces(text))
            {
                int at = raw.IndexOfAny(new[] { ':', '=' });
                string name = at > 0 ? raw.Substring(0, at).Trim() : string.Empty;
                string number = at > 0 ? raw.Substring(at + 1).Trim() : string.Empty;
                if (name.Length == 0 || !PlainName(name) ||
                    !int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount < 0)
                {
                    list.Bad.Add(raw);
                    continue;
                }
                list.amounts[name] = amount;
            }
            return list;
        }

        /// <summary>The amount for a prefab name (any case), or null if it isn't named.</summary>
        internal int? Of(string prefab) => prefab != null && amounts.TryGetValue(prefab, out int amount) ? amount : (int?)null;

        /// <summary>Splits a list into its trimmed, non-empty entries.</summary>
        internal static IEnumerable<string> Pieces(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }
            foreach (string piece in text.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = piece.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }

        /// <summary>A prefab name as the game writes them: letters, digits, underscores and the odd dash or dot.</summary>
        internal static bool PlainName(string name)
        {
            foreach (char c in name)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.'))
                {
                    return false;
                }
            }
            return name.Length > 0;
        }
    }

    /// <summary>
    /// An ordered list of prefab names as a setting writes it ("SilverOre, IronScrap, IronOre"): what each station takes
    /// first, the kiln's woods, the never-take list. Case doesn't matter; a name given twice counts at its first place.
    /// </summary>
    internal sealed class NameList
    {
        private readonly List<string> names = new List<string>();
        private readonly Dictionary<string, int> places = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        internal readonly List<string> Bad = new List<string>();

        internal IReadOnlyList<string> Names => names;

        internal int Count => names.Count;

        internal static NameList Parse(string text)
        {
            var list = new NameList();
            foreach (string name in ItemAmounts.Pieces(text))
            {
                if (!ItemAmounts.PlainName(name))
                {
                    list.Bad.Add(name);
                    continue;
                }
                if (!list.places.ContainsKey(name))
                {
                    list.places[name] = list.names.Count;
                    list.names.Add(name);
                }
            }
            return list;
        }

        internal bool Contains(string name) => name != null && places.ContainsKey(name);

        /// <summary>
        /// The given names (a station's inputs, in the game's own order) in this list's order: those it names first, as it
        /// names them, then, unless <paramref name="onlyListed"/>, the rest in the order given (the catalogue's convention:
        /// names left out go last). Names it lists that aren't given are ignored. A stable order: equal places keep the
        /// order given.
        /// </summary>
        internal List<T> Order<T>(IList<T> given, Func<T, string> nameOf, bool onlyListed)
        {
            var listed = new List<KeyValuePair<int, T>>();
            var rest = new List<T>();
            foreach (T each in given)
            {
                string name = nameOf(each);
                if (name != null && places.TryGetValue(name, out int place))
                {
                    listed.Add(new KeyValuePair<int, T>(place, each));
                }
                else if (!onlyListed)
                {
                    rest.Add(each);
                }
            }
            // Insertion by place keeps it stable (List.Sort isn't).
            var ordered = new List<T>(listed.Count + rest.Count);
            for (int place = 0; place < names.Count; place++)
            {
                foreach (KeyValuePair<int, T> each in listed)
                {
                    if (each.Key == place)
                    {
                        ordered.Add(each.Value);
                    }
                }
            }
            ordered.AddRange(rest);
            return ordered;
        }
    }
}
