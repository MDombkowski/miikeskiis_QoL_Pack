using System;
using System.Collections.Generic;

namespace HouseTrained
{
    /// <summary>
    /// A list of creatures as the player types it into a setting (decision 13): names separated by commas, each the
    /// creature's prefab name ("Lox", "Goblin"), the name the game shows ("Lox", "Fuling") or its $ token
    /// ("$enemy_lox"), matched loosely: case, spaces and underscores, and the token's "$", don't matter. A name that
    /// matches nothing is simply never matched. Plain values only, so the harness checks it without the game.
    /// </summary>
    internal sealed class Names
    {
        private readonly HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The list as one line of names, as the setting holds it.</summary>
        internal static Names From(string line)
        {
            var list = new Names();
            if (string.IsNullOrWhiteSpace(line))
            {
                return list;
            }
            foreach (string part in line.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = Normal(part);
                if (name.Length > 0)
                {
                    list.names.Add(name);
                }
            }
            return list;
        }

        internal bool IsEmpty => names.Count == 0;

        internal int Count => names.Count;

        /// <summary>Whether a part looks after this tame: every tame, or only one on the list.</summary>
        internal static bool Chooses(WhichTames which, Names list, string prefab, string token, string shown) =>
            which == WhichTames.EveryTame || (list != null && list.HasAny(prefab, token, shown));

        /// <summary>Whether any of these names of one creature is on the list: its prefab name, its token, the name the
        /// game shows.</summary>
        internal bool HasAny(string prefab, string token = null, string shown = null) =>
            names.Count > 0 && (Has(prefab) || Has(token) || Has(shown));

        private bool Has(string name) => !string.IsNullOrEmpty(name) && names.Contains(Normal(name));

        /// <summary>A name as it is compared: lower case, without "$" in front, a "(Clone)" at the end, spaces or
        /// underscores ("Lox_Calf", "Lox Calf" and "loxcalf" are one name).</summary>
        internal static string Normal(string name)
        {
            if (name == null)
            {
                return string.Empty;
            }
            string text = name.Trim();
            if (text.EndsWith("(Clone)", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - "(Clone)".Length);
            }
            var kept = new System.Text.StringBuilder(text.Length);
            foreach (char letter in text)
            {
                if (letter == ' ' || letter == '_' || letter == '\t' || letter == '$')
                {
                    continue;
                }
                kept.Append(char.ToLowerInvariant(letter));
            }
            return kept.ToString();
        }
    }
}
