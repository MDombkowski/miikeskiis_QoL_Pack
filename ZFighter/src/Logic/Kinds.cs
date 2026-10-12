using System.Collections.Generic;

namespace ZFighter
{
    /// <summary>
    /// A piece's kind for "Which one shows on top: by kind", from the game's own usage tags on the piece
    /// (Piece.m_usage: Floor 8, Wall 0x10, Roof 0x20, Architecture 0x40 (beams and poles), Stairs 0x20000, Doors
    /// 0x40000; Piece.cs). Trim shows over what it trims: floors lowest, then walls, roofs, stairs, beams and poles,
    /// doors, and everything else (furniture, decor) on top. Plain code: the scratch harness runs it.
    /// </summary>
    internal static class Kinds
    {
        internal const int Floor = 0x8;
        internal const int Wall = 0x10;
        internal const int Roof = 0x20;
        internal const int Architecture = 0x40;
        internal const int Stairs = 0x20000;
        internal const int Doors = 0x40000;

        /// <summary>The kind's rank, 0 for a floor up to 6 for anything that is none of the building kinds. A piece
        /// tagged with several kinds counts as the lowest of them (a floor that is also building is a floor).</summary>
        internal static int Rank(int usage)
        {
            if ((usage & Floor) != 0) return 0;
            if ((usage & Wall) != 0) return 1;
            if ((usage & Roof) != 0) return 2;
            if ((usage & Stairs) != 0) return 3;
            if ((usage & Architecture) != 0) return 4;
            if ((usage & Doors) != 0) return 5;
            return 6;
        }

        private static readonly string[] SlantedWords = { "roof", "stair", "_26", "_45", "_67", "arch", "ramp", "slope" };

        private static readonly string[] AngleWords = { "_26", "_45", "_67" };

        /// <summary>
        /// The axis across a slanted piece's slope, which may grow on its own (Nudge.Factors), or -1 when it isn't known:
        /// x (0) for a plain roof (sloping along z, the ridge along x: wood_roof, darkwood_roof_45), z (2) for a beam, log
        /// or wall made at 26, 45 or 67 degrees (sloping along x, its thickness along z: wood_beam_26, wood_log_45). The
        /// pieces that slope two ways (roof corners, ridge caps) and the wall-roofs keep one factor for all three. Read
        /// from the game's pieces' real faces by 66-B's second review.
        /// </summary>
        internal static int AcrossAxis(string prefabName)
        {
            string name = (prefabName ?? string.Empty).ToLowerInvariant();
            if (name.Contains("corner") || name.Contains("_top") || name.Contains("wall_roof"))
            {
                return -1;
            }
            if (name.Contains("roof"))
            {
                return 0;
            }
            foreach (string word in AngleWords)
            {
                if (name.Contains(word) && (name.Contains("beam") || name.Contains("log") || name.Contains("wall") || name.Contains("pole")))
                {
                    return 2;
                }
            }
            return -1;
        }

        /// <summary>Whether a piece's faces are slanted against its own axes, so it must be nudged by one factor on every
        /// side (Nudge): a roof by its usage tag, and by its prefab's name a roof, stairs, an arch, a ramp, or a beam,
        /// pole, log or wall made at 26, 45 or 67 degrees (wood_beam_26, darkwood_roof_45, wood_wall_roof).</summary>
        internal static bool Slanted(string prefabName, int usage)
        {
            if ((usage & Roof) != 0)
            {
                return true;
            }
            string name = (prefabName ?? string.Empty).ToLowerInvariant();
            foreach (string word in SlantedWords)
            {
                if (name.Contains(word))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// One list of prefab names from a comma-separated setting, compared in lower case, parsed again only when its text
    /// changes (Grounded's and Bar None's lists have the same shape). Plain code: the scratch harness runs it.
    /// </summary>
    internal sealed class NameList
    {
        private readonly System.Func<string> source;
        private string parsedFrom;
        private HashSet<string> names = new HashSet<string>(System.StringComparer.Ordinal);

        internal NameList(System.Func<string> source)
        {
            this.source = source;
        }

        /// <summary>The list's text as it was last read, to notice a change.</summary>
        internal string Text => source() ?? string.Empty;

        /// <summary>Whether this prefab name (any case) is on the list.</summary>
        internal bool Has(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
            string text = Text;
            if (!string.Equals(text, parsedFrom, System.StringComparison.Ordinal))
            {
                names = Parse(text);
                parsedFrom = text;
            }
            return names.Contains(name.ToLowerInvariant());
        }

        /// <summary>The names in a list's text: split at commas, the spaces around each trimmed, in lower case, empty ones
        /// left out.</summary>
        internal static HashSet<string> Parse(string text)
        {
            var parsed = new HashSet<string>(System.StringComparer.Ordinal);
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
