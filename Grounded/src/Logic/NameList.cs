using System;
using System.Collections.Generic;

namespace Grounded
{
    /// <summary>
    /// One list of prefab names from a comma-separated setting (the rugs, the floors), compared in lower case. The text
    /// is parsed again only when it changes, so a list edited in a configuration manager counts at once. Bar None's
    /// piece lists have the same shape. Plain code, no Unity: the scratch harness runs it.
    /// </summary>
    internal sealed class NameList
    {
        private readonly Func<string> source;
        private string parsedFrom;
        private HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        internal NameList(Func<string> source)
        {
            this.source = source;
        }

        /// <summary>Whether this prefab name (lower case, as Kinds.PrefabName gives it) is on the list.</summary>
        internal bool HasName(string lowerName)
        {
            if (lowerName == null)
            {
                return false;
            }
            string text = source() ?? string.Empty;
            if (!string.Equals(text, parsedFrom, StringComparison.Ordinal))
            {
                names = Parse(text);
                parsedFrom = text;
            }
            return names.Contains(lowerName);
        }

        /// <summary>The names in a list's text: split at commas, spaces around each trimmed, lower case, empty ones left
        /// out.</summary>
        internal static HashSet<string> Parse(string text)
        {
            var parsed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string part in (text ?? string.Empty).Split(','))
            {
                string name = part.Trim().ToLowerInvariant();
                if (name.Length > 0)
                {
                    parsed.Add(name);
                }
            }
            return parsed;
        }
    }
}
