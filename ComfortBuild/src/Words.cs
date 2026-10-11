using System.Text;

namespace ComfortBuild
{
    /// <summary>
    /// Everything the mod says, as plain text builders (no game objects, so the wording is tested in the harness).
    /// Numbers are coloured with <paramref name="hex"/>, the setting "Colour of the number" as RRGGBB. Labels follow the
    /// game's own style: Title Case, "and" spelled out.
    /// </summary>
    internal static class Words
    {
        /// <summary>The switch at the top of the Comfort tab's list.</summary>
        internal const string GreyOutSwitch = "Grey Out Unneeded";

        /// <summary>A group in the Comfort tab's list: "Chairs 2/3", what it gives here and the most the pieces he can
        /// build give (only with the setting on, and only when he can build something in it).</summary>
        internal static string GroupLabel(string label, int now, int most, bool values, string hex)
        {
            if (!values || most <= 0)
            {
                return label;
            }
            return $"{label}  <color=#{hex}>{now}/{most}</color>";
        }

        /// <summary>The line above the list: his comfort where he stands, as if sheltered.</summary>
        internal static string Here(int level, bool sheltered, string hex) =>
            sheltered ? $"Comfort here: <color=#{hex}>{level}</color>" : $"Comfort here: <color=#{hex}>{level}</color> if sheltered";

        /// <summary>
        /// The comfort line beside the build panel (Line), saying what the piece would add where he stands and, when
        /// not all of it, why.
        /// </summary>
        /// <param name="comfort">The piece's own comfort (Piece.m_comfort).</param>
        /// <param name="group">What follows its comfort: its group ("Chairs"), or how a piece with no group counts
        /// (Taxonomy.ComfortKind).</param>
        /// <param name="grouped">False for a piece with no group, which counts once per kind.</param>
        /// <param name="gain">What it would add here (ComfortMath.Gain).</param>
        /// <param name="inTheWay">The name of the piece near him that keeps it from adding its whole value, or null.</param>
        /// <param name="inTheWayGives">What that piece gives.</param>
        /// <param name="unlit">The name of a piece of its kind near him that gives nothing now (a fire that is out, a cold
        /// hot tub) and, lit or heated, would give at least as much as this piece (only when this piece still adds
        /// something), or null.</param>
        /// <param name="unlitGives">What that piece would give, lit or heated.</param>
        /// <param name="lit">"lit" for a fire, "heated" for the hot tub.</param>
        /// <param name="sheltered">Whether he stands under shelter.</param>
        internal static string PieceLine(int comfort, string group, bool grouped, int gain, string inTheWay, int inTheWayGives,
                                         string unlit, int unlitGives, string lit, bool sheltered, string hex)
        {
            var text = new StringBuilder();
            text.Append($"<color=#{hex}>Comfort {comfort}</color> ({group}). ");
            if (gain >= comfort)
            {
                text.Append($"Adds {gain} here.");
            }
            else if (gain > 0)
            {
                text.Append(inTheWay != null
                    ? $"Adds {gain} here: the {inTheWay} near you gives {inTheWayGives}."
                    : $"Adds {gain} here.");
            }
            else if (!grouped)
            {
                text.Append("Adds nothing here: one already stands near you.");
            }
            else
            {
                text.Append(inTheWay != null
                    ? $"Adds nothing here: the {inTheWay} near you already gives {inTheWayGives}."
                    : "Adds nothing here.");
            }
            if (unlit != null && gain > 0)
            {
                text.Append($" The {unlit} near you gives nothing now; {lit}, it gives {unlitGives}.");
            }
            if (!sheltered)
            {
                text.Append(" Not sheltered here: counted as if you were.");
            }
            return text.ToString();
        }
    }
}
