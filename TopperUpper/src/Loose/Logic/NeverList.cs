using System;
using System.Collections.Generic;

namespace TopperUpper.Loose
{
    /// <summary>
    /// The kinds never picked up ("Never pick up"): words separated by commas, each an item type as the game names them
    /// (Trophy, Ammo, Fish …) or an item's prefab name (BoneFragments), in any case. An item is on the list when its type or
    /// its prefab name is. A word that isn't a plain name is kept apart (Bad) so the log can say so; it matches nothing.
    /// </summary>
    internal sealed class NeverList
    {
        private readonly HashSet<string> words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The entries it couldn't read, as written.</summary>
        internal readonly List<string> Bad = new List<string>();

        /// <summary>Every word read, as written (for telling which aren't a type or an item the game knows).</summary>
        internal readonly List<string> Words = new List<string>();

        internal bool IsEmpty => words.Count == 0;

        /// <summary>Reads the list: entries separated by commas, semicolons or new lines, trimmed; empty ones skipped.</summary>
        internal static NeverList Parse(string text)
        {
            var list = new NeverList();
            if (string.IsNullOrEmpty(text))
            {
                return list;
            }
            foreach (string raw in text.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string piece = raw.Trim();
                if (piece.Length == 0)
                {
                    continue;
                }
                if (!PlainName(piece))
                {
                    list.Bad.Add(piece);
                }
                else if (list.words.Add(piece))
                {
                    list.Words.Add(piece);
                }
            }
            return list;
        }

        /// <summary>A name as the game writes prefab names and item types: letters, digits, underscores, the odd dash or dot.</summary>
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

        /// <summary>Whether an item of this prefab and type (its ItemType's name) is on the list.</summary>
        internal bool Has(string prefab, string type) =>
            words.Count > 0 && ((prefab != null && words.Contains(prefab)) || (type != null && words.Contains(type)));
    }

    /// <summary>A tame (or tameable) animal that eats things lying on the ground, as one sweep reads it: where it is, how
    /// far from it food is left, and what it eats (by shared name).</summary>
    internal struct Eater
    {
        internal Spot At;
        internal float Reach;
        internal ICollection<string> Eats;
    }

    /// <summary>
    /// The fodder rule ("Leave food near a tame that eats it"): food lying within reach of an animal that eats it stays on
    /// the ground, so a pen's feed isn't put away. An animal looks for food within its own search range (MonsterAI's
    /// m_consumeSearchRange) and walks about, so the reach is that range and a margin.
    /// </summary>
    internal static class Fodder
    {
        /// <summary>Added to an animal's own search range, in metres: it moves about its pen.</summary>
        internal const float Margin = 5f;

        /// <summary>How far from an animal that searches <paramref name="searchRange"/> metres its food is left.</summary>
        internal static float ReachOf(float searchRange) => Math.Max(0f, searchRange) + Margin;

        /// <summary>Whether an item of this shared name at this place lies within reach of an animal that eats it.</summary>
        internal static bool Near(IList<Eater> eaters, Spot at, string name)
        {
            if (eaters == null || name == null)
            {
                return false;
            }
            for (int i = 0; i < eaters.Count; i++)
            {
                Eater eater = eaters[i];
                if (eater.Eats != null && eater.Eats.Contains(name) && at.Within(eater.At, eater.Reach))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
