using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TopperUpper.Logic
{
    /// <summary>One chest linked to a station: an input (it takes from it) or an output (it puts into it), named by its
    /// prefab and its place to the centimetre, never by its network ID, which the game renumbers at every load.</summary>
    internal sealed class Link
    {
        internal bool Output;
        internal string Prefab;
        internal int X;
        internal int Y;
        internal int Z;

        /// <summary>Whether a chest of this prefab at this place (metres) is this link's chest: the same prefab, and every
        /// coordinate within LinkText.Near (5 cm).</summary>
        internal bool Is(string prefab, double x, double y, double z) =>
            string.Equals(prefab, Prefab, StringComparison.Ordinal) &&
            Math.Abs(x * 100.0 - X) <= LinkText.Near && Math.Abs(y * 100.0 - Y) <= LinkText.Near && Math.Abs(z * 100.0 - Z) <= LinkText.Near;

        internal bool SamePlace(Link other) =>
            other != null && string.Equals(other.Prefab, Prefab, StringComparison.Ordinal) &&
            Math.Abs(other.X - X) <= LinkText.Near && Math.Abs(other.Y - Y) <= LinkText.Near && Math.Abs(other.Z - Z) <= LinkText.Near;
    }

    /// <summary>
    /// A station's links as one line of text in its own network record (card A77, decision 5): "l1|i:piece_chest_wood@120,
    /// 3050,-4510|o:piece_chest@…", a version word, then each link, input or output, its chest's prefab and its place in
    /// centimetres. A chest found within 5 cm of that place, of that prefab, is the one; a chest rebuilt in the same spot is
    /// linked again; one moved by a building mod is lost. Another player's game wrote the text in a friend's world, so it
    /// is read strictly: plain prefab names and whole numbers only; a text with another version word or anything it can't
    /// read takes no links and is never rewritten until he links anew.
    /// </summary>
    internal static class LinkText
    {
        /// <summary>The version word this copy writes and reads.</summary>
        internal const string Version = "l1";

        /// <summary>How near a chest must be to a link's place, in centimetres, on every axis.</summary>
        internal const int Near = 5;

        /// <summary>The most links of each kind a station keeps.</summary>
        internal const int MostEach = 12;

        /// <summary>The longest text it writes or reads.</summary>
        internal const int MaxLength = 1500;

        /// <summary>The links a text holds; empty for none, for another version's, or for one it can't read
        /// (<paramref name="readable"/> false then, for anything but an empty text).</summary>
        internal static List<Link> Parse(string text, out bool readable)
        {
            var links = new List<Link>();
            readable = true;
            if (string.IsNullOrEmpty(text))
            {
                return links;
            }
            if (text.Length > MaxLength)
            {
                readable = false;
                return links;
            }
            string[] parts = text.Split('|');
            if (parts[0] != Version)
            {
                readable = false;
                return links;
            }
            for (int i = 1; i < parts.Length; i++)
            {
                Link link = ParseOne(parts[i]);
                if (link == null)
                {
                    readable = false;
                    return new List<Link>();
                }
                links.Add(link);
            }
            return links;
        }

        internal static List<Link> Parse(string text) => Parse(text, out _);

        /// <summary>The text for these links ("" for none): inputs first, then outputs, each in the order given.</summary>
        internal static string Write(IList<Link> links)
        {
            if (links == null || links.Count == 0)
            {
                return string.Empty;
            }
            var text = new StringBuilder(Version);
            foreach (bool output in new[] { false, true })
            {
                foreach (Link link in links)
                {
                    if (link.Output == output)
                    {
                        text.Append('|').Append(output ? "o:" : "i:").Append(link.Prefab).Append('@')
                            .Append(link.X.ToString(CultureInfo.InvariantCulture)).Append(',')
                            .Append(link.Y.ToString(CultureInfo.InvariantCulture)).Append(',')
                            .Append(link.Z.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
            return text.ToString();
        }

        /// <summary>A link to a chest of this prefab at this place (metres), rounded to the centimetre.</summary>
        internal static Link At(bool output, string prefab, double x, double y, double z) => new Link
        {
            Output = output,
            Prefab = prefab,
            X = (int)Math.Round(x * 100.0, MidpointRounding.AwayFromZero),
            Y = (int)Math.Round(y * 100.0, MidpointRounding.AwayFromZero),
            Z = (int)Math.Round(z * 100.0, MidpointRounding.AwayFromZero),
        };

        /// <summary>What pressing the link key's E (<paramref name="output"/> false) or Left Shift + E (true) on a chest does to
        /// the links: links it in that role, switches it over from the other role, or unlinks it if it already has that
        /// role. Returns what became of it, and changes nothing when that role is full (MostEach) or the text would be too
        /// long.</summary>
        internal static LinkChange Toggle(List<Link> links, Link chest)
        {
            int found = links.FindIndex(each => each.SamePlace(chest));
            if (found >= 0 && links[found].Output == chest.Output)
            {
                links.RemoveAt(found);
                return LinkChange.Unlinked;
            }
            int same = 0;
            foreach (Link each in links)
            {
                if (each.Output == chest.Output)
                {
                    same++;
                }
            }
            if (same >= MostEach)
            {
                return LinkChange.Full;
            }
            var next = new List<Link>(links);
            if (found >= 0)
            {
                next.RemoveAt(found);
            }
            next.Add(chest);
            if (Write(next).Length > MaxLength)
            {
                return LinkChange.Full;
            }
            links.Clear();
            links.AddRange(next);
            return found >= 0 ? LinkChange.Switched : LinkChange.Linked;
        }

        /// <summary>The link a chest of this prefab at this place answers to, or null.</summary>
        internal static Link Find(IList<Link> links, string prefab, double x, double y, double z)
        {
            if (links == null)
            {
                return null;
            }
            foreach (Link link in links)
            {
                if (link.Is(prefab, x, y, z))
                {
                    return link;
                }
            }
            return null;
        }

        internal static int CountOf(IList<Link> links, bool output)
        {
            int count = 0;
            if (links != null)
            {
                foreach (Link link in links)
                {
                    if (link.Output == output)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        // "i:piece_chest@120,3050,-4510", or null.
        private static Link ParseOne(string part)
        {
            if (part.Length < 3 || part[1] != ':' || (part[0] != 'i' && part[0] != 'o'))
            {
                return null;
            }
            int at = part.IndexOf('@');
            if (at < 3)
            {
                return null;
            }
            string prefab = part.Substring(2, at - 2);
            if (!ItemAmounts.PlainName(prefab))
            {
                return null;
            }
            string[] numbers = part.Substring(at + 1).Split(',');
            if (numbers.Length != 3 ||
                !int.TryParse(numbers[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(numbers[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y) ||
                !int.TryParse(numbers[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int z))
            {
                return null;
            }
            return new Link { Output = part[0] == 'o', Prefab = prefab, X = x, Y = y, Z = z };
        }
    }

    /// <summary>What a press on a chest did to a station's links.</summary>
    internal enum LinkChange
    {
        Linked,
        Switched,
        Unlinked,
        Full,
    }
}
