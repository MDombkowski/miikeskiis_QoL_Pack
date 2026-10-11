using QoLMods.ItemStands;

namespace AddToCart
{
    /// <summary>A row of the item stand filter: none picked, anything that fits a stand, or one kind of stand.</summary>
    internal enum StandPick
    {
        None,
        Any,
        Wall,
        Flat,
        Armor,
    }

    /// <summary>
    /// The item stand filter (his 0.16.0 ask, "browse all standable items together in one filter or tab"): a heading in
    /// the list on the left, FITS A STAND, with a row for each StandPick, picked like any other row. A filter rather than a
    /// fourth tab: each tab is one kind of thing a click puts in the cart (a piece, a recipe, an item to take), and
    /// standable things are recipes on Craft and items on Take, so the rows sit on both. Only items fit; a piece never.
    /// </summary>
    internal static class StandRows
    {
        /// <summary>The heading, as the list shows it (in capitals).</summary>
        internal const string Heading = "Fits a Stand";

        /// <summary>The rows under the heading, in order.</summary>
        internal static readonly StandPick[] Rows = { StandPick.Any, StandPick.Wall, StandPick.Flat, StandPick.Armor };

        /// <summary>Whether an item that fits so is in the pick (everything is, with none picked).</summary>
        internal static bool Passes(StandPick pick, Fit fit)
        {
            switch (pick)
            {
                case StandPick.None:
                    return true;
                case StandPick.Any:
                    return fit != Fit.None;
                case StandPick.Wall:
                    return (fit & Fit.Wall) != 0;
                case StandPick.Flat:
                    return (fit & Fit.Flat) != 0;
                case StandPick.Armor:
                    return (fit & Fit.Armor) != 0;
                default:
                    return false;
            }
        }

        /// <summary>A row's name in the list.</summary>
        internal static string Label(StandPick pick)
        {
            switch (pick)
            {
                case StandPick.Any:
                    return "Any stand";
                case StandPick.Wall:
                    return "Wall item stand";
                case StandPick.Flat:
                    return "Flat item stand";
                case StandPick.Armor:
                    return "Armor stand";
                default:
                    return string.Empty;
            }
        }

        /// <summary>What the line above the results says is shown.</summary>
        internal static string Title(StandPick pick)
        {
            switch (pick)
            {
                case StandPick.Any:
                    return "Fits a stand";
                case StandPick.Wall:
                    return "Fits the wall item stand";
                case StandPick.Flat:
                    return "Fits the flat item stand";
                case StandPick.Armor:
                    return "Fits the armor stand";
                default:
                    return string.Empty;
            }
        }

        /// <summary>The picture beside a row: the mark those items carry (none for Any).</summary>
        internal static Fit MarkOf(StandPick pick) =>
            pick == StandPick.Wall ? Fit.Wall : pick == StandPick.Flat ? Fit.Flat : pick == StandPick.Armor ? Fit.Armor : Fit.None;
    }
}
