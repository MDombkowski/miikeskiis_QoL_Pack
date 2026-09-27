using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AddToCart
{
    /// <summary>
    /// What the mod remembers per character: how often, and when last, each thumbnail was fetched (the Recent and
    /// Frequent sorts), each tab's Group and Sort (the setting "Remember each tab's group and sort"), and which of each
    /// tab's headings on the left he has folded away. It lives in the character's own custom data (Player.m_customData),
    /// which the game saves with the character; a game without the mod keeps those lines untouched.
    ///
    /// Until 0.3.0 it also kept the favorites of recipes and Take items; those now live in the game's own favorites
    /// (QoLMods.Catalogue.GameLists), and Favorites moves the old ones across once, leaving the old line in place.
    /// </summary>
    internal static class Memory
    {
        private const string HistoryKey = "modprojects.addtocart.history";
        private const string FavoritesKey = "modprojects.addtocart.favorites";
        private const string ViewsKey = "modprojects.addtocart.views";
        private const string FoldedKey = "modprojects.addtocart.folded";

        private static Player readFor;
        private static readonly Dictionary<string, Fetches> History = new Dictionary<string, Fetches>();
        private static readonly List<string> OldFavorites = new List<string>();
        private static readonly Dictionary<string, (string Group, string Sort)> Views = new Dictionary<string, (string, string)>();
        private static readonly Dictionary<string, HashSet<string>> Folded = new Dictionary<string, HashSet<string>>();

        internal struct Fetches
        {
            /// <summary>How many fetches it was in.</summary>
            internal int Count;

            /// <summary>When it was last fetched: seconds since 1970, UTC. 0 if never.</summary>
            internal long Last;
        }

        internal static Fetches Of(string entryKey)
        {
            Read();
            return History.TryGetValue(entryKey, out Fetches fetches) ? fetches : default;
        }

        /// <summary>True once anything has been fetched with this character: until then Recent and Frequent would
        /// change nothing, and aren't offered.</summary>
        internal static bool AnyHistory
        {
            get
            {
                Read();
                return History.Count > 0;
            }
        }

        /// <summary>Notes a fetch of these thumbnails (the cart's lines), and writes it to the character.</summary>
        internal static void Fetched(IEnumerable<string> entryKeys)
        {
            if (!Read())
            {
                return;
            }
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (string key in entryKeys)
            {
                History.TryGetValue(key, out Fetches fetches);
                fetches.Count++;
                fetches.Last = now;
                History[key] = fetches;
            }
            var text = new StringBuilder();
            foreach (KeyValuePair<string, Fetches> each in History)
            {
                text.Append(each.Key).Append('\t').Append(each.Value.Count.ToString(CultureInfo.InvariantCulture))
                    .Append('\t').Append(each.Value.Last.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            readFor.m_customData[HistoryKey] = text.ToString();
        }

        /// <summary>0.2.0's own favorites (thumbnail keys: "recipe:…", "convert:…", "take:…", "piece:…"), for moving
        /// them into the game's favorites once.</summary>
        internal static IList<string> FavoritesOf020()
        {
            Read();
            return OldFavorites;
        }

        /// <summary>Whether this character's 0.2.0 favorites have been moved across, and marking that they have.</summary>
        internal static bool Moved(string marker)
        {
            Player player = Player.m_localPlayer;
            return player == null || player.m_customData.ContainsKey(marker);
        }

        internal static void MarkMoved(string marker, string note)
        {
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                player.m_customData[marker] = note;
            }
        }

        /// <summary>The Group and Sort a tab was left on, or false.</summary>
        internal static bool View(string tab, out string group, out string sort)
        {
            group = sort = null;
            if (!Read() || !Views.TryGetValue(tab, out (string Group, string Sort) view))
            {
                return false;
            }
            (group, sort) = view;
            return true;
        }

        /// <summary>Keeps a tab's Group and Sort with the character.</summary>
        internal static void KeepView(string tab, string group, string sort)
        {
            if (!Read() || tab == null || group == null || sort == null)
            {
                return;
            }
            if (Views.TryGetValue(tab, out (string Group, string Sort) old) && old.Group == group && old.Sort == sort)
            {
                return;
            }
            Views[tab] = (group, sort);
            var text = new StringBuilder();
            foreach (KeyValuePair<string, (string Group, string Sort)> each in Views)
            {
                text.Append(each.Key).Append('\t').Append(each.Value.Group).Append('\t').Append(each.Value.Sort).Append('\n');
            }
            readFor.m_customData[ViewsKey] = text.ToString();
        }

        /// <summary>The headings of a tab's list on the left that he has folded away (a copy).</summary>
        internal static HashSet<string> FoldedOf(string tab)
        {
            if (!Read() || tab == null || !Folded.TryGetValue(tab, out HashSet<string> titles))
            {
                return new HashSet<string>();
            }
            return new HashSet<string>(titles);
        }

        /// <summary>Keeps which of a tab's headings are folded away, with the character.</summary>
        internal static void KeepFolded(string tab, IEnumerable<string> titles)
        {
            if (!Read() || tab == null)
            {
                return;
            }
            var kept = new HashSet<string>(titles);
            if (Folded.TryGetValue(tab, out HashSet<string> old) && old.SetEquals(kept))
            {
                return;
            }
            Folded[tab] = kept;
            string text = FoldedText(Folded);
            if (text.Length == 0)
            {
                readFor.m_customData.Remove(FoldedKey);
            }
            else
            {
                readFor.m_customData[FoldedKey] = text;
            }
        }

        /// <summary>The folded headings as the character keeps them: a line per tab, the tab and then its folded headings,
        /// tab-separated, in a fixed order; a tab with none folded has no line.</summary>
        internal static string FoldedText(IDictionary<string, HashSet<string>> folded)
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, HashSet<string>> each in folded.OrderBy(each => each.Key, StringComparer.Ordinal))
            {
                List<string> titles = each.Value.Where(title => !string.IsNullOrEmpty(title)).OrderBy(title => title, StringComparer.Ordinal).ToList();
                if (!string.IsNullOrEmpty(each.Key) && titles.Count > 0)
                {
                    text.Append(each.Key).Append('\t').Append(string.Join("\t", titles)).Append('\n');
                }
            }
            return text.ToString();
        }

        /// <summary>Reads what FoldedText wrote; lines it can't read are skipped.</summary>
        internal static Dictionary<string, HashSet<string>> ParseFolded(string text)
        {
            var folded = new Dictionary<string, HashSet<string>>();
            foreach (string line in (text ?? string.Empty).Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length >= 2 && parts[0].Length > 0)
                {
                    folded[parts[0]] = new HashSet<string>(parts.Skip(1).Where(title => title.Length > 0));
                }
            }
            return folded;
        }

        // Reads the character's lines once per character (again after a change of character). False with no character.
        private static bool Read()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }
            if (player == readFor)
            {
                return true;
            }
            readFor = player;
            History.Clear();
            OldFavorites.Clear();
            Views.Clear();
            Folded.Clear();
            if (player.m_customData.TryGetValue(HistoryKey, out string history))
            {
                foreach (string line in history.Split('\n'))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length == 3 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) &&
                        long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long last))
                    {
                        History[parts[0]] = new Fetches { Count = count, Last = last };
                    }
                }
            }
            if (player.m_customData.TryGetValue(FavoritesKey, out string favorites))
            {
                foreach (string key in favorites.Split('\n'))
                {
                    if (key.Length > 0)
                    {
                        OldFavorites.Add(key);
                    }
                }
            }
            if (player.m_customData.TryGetValue(ViewsKey, out string views))
            {
                foreach (string line in views.Split('\n'))
                {
                    string[] parts = line.Split('\t');
                    if (parts.Length == 3 && parts[0].Length > 0)
                    {
                        Views[parts[0]] = (parts[1], parts[2]);
                    }
                }
            }
            if (player.m_customData.TryGetValue(FoldedKey, out string folded))
            {
                foreach (KeyValuePair<string, HashSet<string>> each in ParseFolded(folded))
                {
                    Folded[each.Key] = each.Value;
                }
            }
            return true;
        }
    }
}
