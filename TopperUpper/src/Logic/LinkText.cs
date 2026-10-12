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
    /// linked again; one moved by a building mod is lost. A chest may be a station's input and its output at once (card
    /// B11, his ask: "there could be a chest that does both"): it then has a link of each kind. Another player's game
    /// wrote the text in a friend's world, so it is read strictly: plain prefab names and whole numbers only; a text with
    /// another version word or anything it can't read takes no links and is never rewritten until he links anew.
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
                // A second link of the same role for the same chest (an older version's switch of a chest that is both,
                // review m1) is the same link: read once, so it never counts twice toward MostEach.
                if (!links.Exists(each => each.Output == link.Output && each.SamePlace(link)))
                {
                    links.Add(link);
                }
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

        /// <summary>What pressing E (<paramref name="chest"/>'s Output false) or Left Shift + E (true) on a chest while
        /// linking does to the links: links it in that role if it hasn't it, or unlinks that role if it has; its other role
        /// stays as it was, so a chest may be both input and output (card B11). Returns what became of it, and changes
        /// nothing when that role is full (MostEach) or the text would be too long.</summary>
        internal static LinkChange Toggle(List<Link> links, Link chest)
        {
            // Every link of that role there goes: a friend's older Topper-Upper (0.1.0, 0.2.0) switching a chest that is both
            // could leave two of the same (review m1).
            if (links.RemoveAll(each => each.SamePlace(chest) && each.Output == chest.Output) > 0)
            {
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
            var next = new List<Link>(links) { chest };
            if (Write(next).Length > MaxLength)
            {
                return LinkChange.Full;
            }
            links.Clear();
            links.AddRange(next);
            return LinkChange.Linked;
        }

        /// <summary>The roles a chest of this prefab at this place has among the links: an input, an output, both or
        /// neither.</summary>
        internal static void RolesOf(IList<Link> links, string prefab, double x, double y, double z, out bool input, out bool output)
        {
            input = false;
            output = false;
            if (links == null)
            {
                return;
            }
            foreach (Link link in links)
            {
                if (link.Is(prefab, x, y, z))
                {
                    if (link.Output)
                    {
                        output = true;
                    }
                    else
                    {
                        input = true;
                    }
                }
            }
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

    /// <summary>What a press on a chest did to a station's links (in the role pressed).</summary>
    internal enum LinkChange
    {
        Linked,
        Unlinked,
        Full,
    }

    /// <summary>The words for linking (card B11), on plain values, so they can be checked without the game: every line
    /// short, each on its own, so the message fits the screen even before the kit fits it (card B14).</summary>
    internal static class LinkWords
    {
        /// <summary>What a chest is to the station: "its input", "its output", "its input and its output", or null.</summary>
        internal static string Role(bool input, bool output) =>
            input && output ? "its input and its output" : input ? "its input" : output ? "its output" : null;

        /// <summary>The message when linking starts.</summary>
        internal static string Start(string station, string use, string alt, string done) =>
            $"Linking the {station}'s chests\n" +
            $"{use} on a chest: it takes from that chest\n" +
            $"{alt} + {use}: it puts what it makes there\n" +
            $"{done} at the {station} when you're done";

        /// <summary>The message after a press: what the chest is now, then the station's links in all.</summary>
        internal static string After(string station, bool output, LinkChange change, bool input, bool isOutput, string summary)
        {
            string role = Role(input, isOutput);
            string said = change == LinkChange.Unlinked
                ? $"No longer the {station}'s {(output ? "output" : "input")}"
                : $"Linked as the {station}'s {(output ? "output" : "input")}";
            if (role != null && (input && isOutput || change == LinkChange.Unlinked))
            {
                said += $"\nThis chest: {role}";
            }
            return string.IsNullOrEmpty(summary) ? said : said + "\n" + summary;
        }
    }
}
