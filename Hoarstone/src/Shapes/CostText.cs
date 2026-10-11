using System;
using System.Collections.Generic;
using System.Globalization;

namespace Hoarstone.Shapes
{
    /// <summary>
    /// The Hoarstone's cost as its setting writes it: items by their prefab names, each with an amount after a colon,
    /// separated by commas ("TrophyForestTroll:1, Root:10"). An amount left out is 1. Pure text work: whether a name is a
    /// real item is the game's to say, when the cost is used.
    /// </summary>
    internal static class CostText
    {
        /// <summary>His cost (2026-10-10): a Troll, an Abomination and a Stone Golem trophy, Ectoplasm ×10, Root ×10,
        /// Ymir Flesh ×3.</summary>
        internal const string Default = "TrophyForestTroll:1, TrophyAbomination:1, TrophySGolem:1, Ectoplasm:10, Root:10, YmirRemains:3";

        /// <summary>The Ward's own cost, used when the setting gives nothing usable, so a Hoarstone is never free by
        /// mistake.</summary>
        internal const string WardCost = "FineWood:5, GreydwarfEye:5, SurtlingCore:1";

        /// <summary>The most of one item a cost may ask: a piece's requirement is a whole number the game counts in an
        /// inventory.</summary>
        internal const int MostOfOne = 9999;

        /// <summary>Reads a cost. Each entry read becomes a (name, amount) in the order written, the same name twice
        /// added together; each part that can't be read is named in <paramref name="problems"/> and left out.</summary>
        internal static List<(string Name, int Amount)> Parse(string text, List<string> problems)
        {
            var result = new List<(string Name, int Amount)>();
            if (string.IsNullOrEmpty(text))
            {
                return result;
            }
            foreach (string raw in text.Split(new[] { ',', ';' }))
            {
                string part = raw.Trim();
                if (part.Length == 0)
                {
                    continue;
                }
                string name = part;
                int amount = 1;
                int colon = part.IndexOf(':');
                if (colon >= 0)
                {
                    name = part.Substring(0, colon).Trim();
                    string number = part.Substring(colon + 1).Trim();
                    if (!int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out amount) || amount < 1 || amount > MostOfOne)
                    {
                        problems?.Add(part);
                        continue;
                    }
                }
                if (name.Length == 0 || name.IndexOfAny(new[] { ' ', '\t', ':' }) >= 0)
                {
                    problems?.Add(part);
                    continue;
                }
                int at = result.FindIndex(e => string.Equals(e.Name, name, StringComparison.Ordinal));
                if (at >= 0)
                {
                    result[at] = (name, Math.Min(MostOfOne, result[at].Amount + amount));
                }
                else
                {
                    result.Add((name, amount));
                }
            }
            return result;
        }
    }
}
