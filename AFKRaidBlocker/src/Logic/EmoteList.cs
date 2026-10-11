using System;
using System.Collections.Generic;

namespace AFKRaidBlocker
{
    /// <summary>
    /// The emotes that count, from the setting's text: names separated by commas, spaces or semicolons, in any case, with
    /// or without the chat's slash ("sit, /rest, Relax"). The game keeps the emote a player started, by its lower-case
    /// name, until he moves. Read again only when the text changes.
    /// </summary>
    internal sealed class EmoteList
    {
        private static readonly char[] Separators = { ',', ';', ' ', '\t' };

        private readonly HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
        private string parsedFrom;

        internal void Read(string text)
        {
            text = text ?? "";
            if (string.Equals(text, parsedFrom, StringComparison.Ordinal))
            {
                return;
            }
            parsedFrom = text;
            names.Clear();
            foreach (string part in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                string name = part.Trim().TrimStart('/').ToLowerInvariant();
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
        }

        /// <summary>Whether the emote the game keeps for him (its name, "" for none) is one that counts.</summary>
        internal bool Has(string emote) =>
            !string.IsNullOrEmpty(emote) && (names.Contains(emote) || names.Contains(emote.ToLowerInvariant()));

        internal int Count => names.Count;
    }
}
