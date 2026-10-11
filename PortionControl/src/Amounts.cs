using System;
using System.Collections.Generic;
using System.Globalization;

namespace PortionControl
{
    /// <summary>
    /// The amounts the buttons put on the cursor, worked out from the stack's size alone: the lists in the settings, the
    /// percentages (rounded down or up, his setting), All -1, the slider buttons, and a right-click's split into stacks.
    /// No Unity and no game objects here, so a console harness can check every rule.
    /// </summary>
    internal static class Amounts
    {
        /// <summary>The most fixed amounts shown (two rows of six).</summary>
        internal const int MostFixed = 12;

        /// <summary>The most percentages shown (two rows of four).</summary>
        internal const int MostPercentages = 8;

        /// <summary>
        /// Reads a list typed in the settings ("1, 2, 5, 10"): whole numbers from <paramref name="min"/> to
        /// <paramref name="max"/>, in the order typed, each once, at most <paramref name="most"/>. Anything else (a word, a
        /// fraction, 0, a number out of range) is skipped, so a typo loses only itself. Commas, semicolons and spaces all
        /// separate; a "%" after a number is allowed.
        /// </summary>
        internal static List<int> ParseList(string text, int min, int max, int most)
        {
            var list = new List<int>();
            if (string.IsNullOrEmpty(text))
            {
                return list;
            }
            foreach (string part in text.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string number = part.Trim().TrimEnd('%');
                if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int value) &&
                    value >= min && value <= max && !list.Contains(value))
                {
                    list.Add(value);
                    if (list.Count == most)
                    {
                        break;
                    }
                }
            }
            return list;
        }

        /// <summary>A share of the stack in percent, rounded down (his default: 99% of 20 is 19, 25% of 50 is 12) or up
        /// (as BetterSplitDialog rounds: 20% of 12 is 3), and never below 1 or above the stack.</summary>
        internal static int Percent(int stack, int percent, bool roundUp)
        {
            if (stack < 1)
            {
                return 0;
            }
            long share = roundUp ? ((long)stack * percent + 99) / 100 : (long)stack * percent / 100;
            return (int)Math.Max(1, Math.Min(stack, share));
        }

        /// <summary>Each percentage with its amount, in the order given; a percentage that comes to the same amount as one
        /// before it is left out (40% and 80% of a stack of 2), so no two buttons do the same.</summary>
        internal static List<KeyValuePair<int, int>> PercentAmounts(int stack, IList<int> percents, bool roundUp)
        {
            var shown = new List<KeyValuePair<int, int>>();
            var amounts = new HashSet<int>();
            foreach (int percent in percents)
            {
                int amount = Percent(stack, percent, roundUp);
                if (amount >= 1 && amounts.Add(amount))
                {
                    shown.Add(new KeyValuePair<int, int>(percent, amount));
                }
            }
            return shown;
        }

        /// <summary>All -1: the whole stack but one. The window never opens on a stack of 1, so this is at least 1.</summary>
        internal static int AllButOne(int stack) => Math.Max(1, stack - 1);

        /// <summary>The slider's 50%: half the stack, rounded up, as the game opens the window.</summary>
        internal static int Half(int stack) => Math.Max(1, (stack + 1) / 2);

        /// <summary>Whether a fixed amount can be taken from a stack of this size: not more than the stack (his answer 10
        /// on A17 greys out anything bigger).</summary>
        internal static bool Fits(int amount, int stack) => amount >= 1 && amount <= stack;

        /// <summary>
        /// A right-click's split of a stack into stacks of <paramref name="amount"/> (his feedback of 2026-09-30): how many
        /// whole shares it makes, and what is left over (50 into 7s: 7 shares, 1 over; 50 into 10s: 5 shares, none over).
        /// Nothing for an amount below 1 or above the stack.
        /// </summary>
        internal static (int Shares, int Leftover) Split(int stack, int amount)
        {
            if (stack < 1 || amount < 1 || amount > stack)
            {
                return (0, 0);
            }
            return (stack / amount, stack % amount);
        }
    }
}
