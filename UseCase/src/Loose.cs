using System;
using System.Collections.Generic;

namespace UseCase
{
    /// <summary>
    /// Names as the player types them into the two ignore lists: an English name ("Sconce"), a prefab name
    /// ("piece_walltorch") or a $ token ("$piece_walltorch"), matched loosely as the catalogue's Orders.Match matches
    /// his order lines: case, spaces, "&amp;" for "and" and a trailing full stop don't matter, and here neither does
    /// the token's "$" (card A45, decision 8).
    /// </summary>
    internal sealed class Loose
    {
        private readonly HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The list as one line of names separated by commas, as the setting holds it.</summary>
        internal static Loose From(string line)
        {
            var list = new Loose();
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

        /// <summary>Whether any of these names of one thing is on the list.</summary>
        internal bool HasAny(string a, string b = null, string c = null, string d = null) =>
            names.Count > 0 && (Has(a) || Has(b) || Has(c) || Has(d));

        private bool Has(string name) => !string.IsNullOrEmpty(name) && names.Contains(Normal(name));

        /// <summary>A name as it is compared: lower case, "&amp;" as "and", spaces single, no "$" in front and no full stop
        /// at the end.</summary>
        internal static string Normal(string name)
        {
            if (name == null)
            {
                return string.Empty;
            }
            string text = name.Replace("&", " and ").ToLowerInvariant();
            string[] words = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            string joined = string.Join(" ", words).TrimEnd('.');
            return joined.StartsWith("$") ? joined.Substring(1) : joined;
        }

        /// <summary>The line with one more name at its end, unless the list already has it.</summary>
        internal static string WithName(string line, string name)
        {
            if (string.IsNullOrWhiteSpace(name) || From(line).HasAny(name))
            {
                return line ?? string.Empty;
            }
            return string.IsNullOrWhiteSpace(line) ? name : line.TrimEnd().TrimEnd(',') + ", " + name;
        }
    }
}
