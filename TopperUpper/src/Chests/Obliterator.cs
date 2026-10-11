using System.Collections.Generic;

namespace TopperUpper
{
    /// <summary>
    /// The Obliterator (the game's Incinerator) is a lever over an ordinary container whose contents are destroyed at its
    /// next pull, ten of anything becoming one coal (the research's 1.8). The pack's chest service leaves it out of every
    /// chest it finds or allows since 44-B (ChestFinder.IsObliterator, in WhyNot), and every list of chests here comes
    /// through that (Find, MayUse, and WhyNot for a linked chest). This mod asks its own test as well wherever it counts,
    /// takes, puts or links (code review M2): a harmless repeat, so the rule never rests on one place. Remembered per
    /// container.
    /// </summary>
    internal static class Obliterator
    {
        private static readonly Dictionary<Container, bool> Known = new Dictionary<Container, bool>();

        /// <summary>Whether this container belongs to an Obliterator (an Incinerator above it).</summary>
        internal static bool Is(Container chest)
        {
            if (chest == null)
            {
                return false;
            }
            if (!Known.TryGetValue(chest, out bool is_))
            {
                if (Known.Count > 4096)
                {
                    Known.Clear();
                }
                is_ = chest.GetComponentInParent<Incinerator>() != null;
                Known[chest] = is_;
            }
            return is_;
        }
    }
}
