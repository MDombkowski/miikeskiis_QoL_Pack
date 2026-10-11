using System;
using System.Collections.Generic;
using System.Globalization;

namespace Outfitter.Loadouts
{
    /// <summary>How a food slot finds its food (his words, 2026-10-06): by the food's stats, or from a list of foods he
    /// ticks (his "Food Pool").</summary>
    internal enum FoodMode
    {
        ByStats,
        FromList,
    }

    /// <summary>A food's stat a slot looks at: the game's three (ItemDrop.ItemData.SharedData's m_food, m_foodStamina,
    /// m_foodEitr), or all three together (their sum).</summary>
    internal enum FoodStat
    {
        Health,
        Stamina,
        Eitr,
        All,
    }

    /// <summary>Which end of the stat a slot prefers (his "Maximize" and "Minimize").</summary>
    internal enum FoodPrefer
    {
        Most,
        Least,
    }

    /// <summary>What a slot puts first when it chooses its food (his words, 2026-10-07: "a toggle for each slot, to either
    /// have the slot prioritize the amount or to prioritize the stat"): Stat, the food it prefers as long as at least a few
    /// are nearby (the setting "Food slots by stat: at least"), even when another would fill its whole count; Amount, a
    /// food that fills its whole count first (0.4.x's way), preferring by stat among those.</summary>
    internal enum FoodFirst
    {
        Stat,
        Amount,
    }

    /// <summary>An optional range of one of a food's stats: whole numbers, both ends inclusive, a missing end open (no
    /// end at all: the stat isn't asked).</summary>
    internal struct FoodRange
    {
        internal int? From;
        internal int? To;

        internal FoodRange(int? from, int? to)
        {
            From = from;
            To = to;
        }

        /// <summary>Whether either end is given (the stat is asked).</summary>
        internal bool Given => From.HasValue || To.HasValue;

        /// <summary>Whether a stat's value is in the range (an open end holds anything on its side).</summary>
        internal bool Holds(float value) => (!From.HasValue || value >= From.Value) && (!To.HasValue || value <= To.Value);

        /// <summary>Each end given is a whole number from 0 to <see cref="FoodRule.MaxStat"/>.</summary>
        internal bool Valid => In(From) && In(To);

        private static bool In(int? end) => !end.HasValue || (end.Value >= 0 && end.Value <= FoodRule.MaxStat);

        internal bool SameAs(FoodRange other) => From == other.From && To == other.To;

        /// <summary>The range in words, after the stat's name: "20 to 50", "20" (one value), "20 or more", "up to 50".</summary>
        internal string Words()
        {
            string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
            if (From.HasValue && To.HasValue)
            {
                return From.Value == To.Value ? Number(From.Value) : $"{Number(From.Value)} to {Number(To.Value)}";
            }
            if (From.HasValue)
            {
                return $"{Number(From.Value)} or more";
            }
            return To.HasValue ? $"up to {Number(To.Value)}" : string.Empty;
        }
    }

    /// <summary>
    /// A food slot's rule (card A34, decision 72; his words, 2026-10-06): instead of one item, a loadout's cell may ask for
    /// "a food that ...": by its stats (a range for any of Health, Stamina and Eitr; a food fills the slot only when it meets
    /// every range given) or from a list of foods he ticks, preferring the most or the least of one stat (or of all three
    /// together). The slot's LoadoutItem keeps its cell and its count (how many to bring); its prefab stays empty, since the
    /// food is chosen at each equip (EquipPlanner, FoodPick). A rule keeps its ranges and its list both, whichever mode is
    /// on, so switching modes loses neither. The list names prefabs, never items: a friend's food this game lacks stays a
    /// name. Pure, so the scratch harness reaches it.
    /// </summary>
    internal sealed class FoodRule
    {
        /// <summary>The most foods a list may name (StoreStash's chest setups list as many, by biome).</summary>
        internal const int MaxFoods = 64;

        /// <summary>The highest end a range may have: far above any food (the game's best give about 100).</summary>
        internal const int MaxStat = 9999;

        /// <summary>The most a slot may bring (at most one stack of the food comes, whatever it says).</summary>
        internal const int MaxCount = 100;

        internal FoodMode Mode;

        internal FoodRange Health;

        internal FoodRange Stamina;

        internal FoodRange Eitr;

        /// <summary>The foods ticked (FromList), by prefab name, in the order he ticked them.</summary>
        internal readonly List<string> Foods = new List<string>();

        internal FoodPrefer Prefer;

        /// <summary>The stat <see cref="Prefer"/> looks at.</summary>
        internal FoodStat Of = FoodStat.All;

        /// <summary>What it puts first when it chooses (0.5.0): its stat (every slot from before 0.5.0 too, at his word), or
        /// a whole count.</summary>
        internal FoodFirst First = FoodFirst.Stat;

        /// <summary>A stat's range (All has none: an open one).</summary>
        internal FoodRange RangeOf(FoodStat stat)
        {
            switch (stat)
            {
                case FoodStat.Health:
                    return Health;
                case FoodStat.Stamina:
                    return Stamina;
                case FoodStat.Eitr:
                    return Eitr;
                default:
                    return new FoodRange();
            }
        }

        /// <summary>Sets a stat's range (nothing for All).</summary>
        internal void SetRange(FoodStat stat, FoodRange range)
        {
            switch (stat)
            {
                case FoodStat.Health:
                    Health = range;
                    return;
                case FoodStat.Stamina:
                    Stamina = range;
                    return;
                case FoodStat.Eitr:
                    Eitr = range;
                    return;
            }
        }

        /// <summary>A copy of its own: changing one never changes the other.</summary>
        internal FoodRule Clone()
        {
            var copy = new FoodRule { Mode = Mode, Health = Health, Stamina = Stamina, Eitr = Eitr, Prefer = Prefer, Of = Of, First = First };
            copy.Foods.AddRange(Foods);
            return copy;
        }

        /// <summary>Whether two rules say the same, field by field (the list in its order).</summary>
        internal bool SameAs(FoodRule other)
        {
            if (other == null || Mode != other.Mode || Prefer != other.Prefer || Of != other.Of || First != other.First || !Health.SameAs(other.Health) ||
                !Stamina.SameAs(other.Stamina) || !Eitr.SameAs(other.Eitr) || Foods.Count != other.Foods.Count)
            {
                return false;
            }
            for (int i = 0; i < Foods.Count; i++)
            {
                if (!string.Equals(Foods[i], other.Foods[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Why the rule can't be kept, in words, or null when it can: its choices one of each kind's, each range's
        /// ends whole numbers from 0 to <see cref="MaxStat"/>, at most <see cref="MaxFoods"/> foods, each a prefab name
        /// (Text.PrefabName) and none twice. A range whose "from" is above its "to" is kept: nothing meets it.</summary>
        internal string WhyNotValid()
        {
            if (!Enum.IsDefined(typeof(FoodMode), Mode) || !Enum.IsDefined(typeof(FoodPrefer), Prefer) || !Enum.IsDefined(typeof(FoodStat), Of) ||
                !Enum.IsDefined(typeof(FoodFirst), First))
            {
                return "That isn't a food slot's choice.";
            }
            if (!Health.Valid || !Stamina.Valid || !Eitr.Valid)
            {
                return $"A food slot's ranges run from 0 to {MaxStat}.";
            }
            if (Foods.Count > MaxFoods)
            {
                return $"A food slot lists at most {MaxFoods} foods.";
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string food in Foods)
            {
                if (food == null || Text.PrefabName(food) != food)
                {
                    return "That isn't a food's name.";
                }
                if (!seen.Add(food))
                {
                    return "A food is listed twice.";
                }
            }
            return null;
        }

        /// <summary>The stat a slot starts preferring (the plan: "By stats starts on the first stat given a range, a list
        /// on all three"): for By stats, the first of Health, Stamina and Eitr given a range, else all three; for a list,
        /// all three. The window sets <see cref="Of"/> from it when he changes the mode or gives a first range.</summary>
        internal FoodStat StartingStat()
        {
            if (Mode == FoodMode.ByStats)
            {
                foreach (FoodStat stat in new[] { FoodStat.Health, FoodStat.Stamina, FoodStat.Eitr })
                {
                    if (RangeOf(stat).Given)
                    {
                        return stat;
                    }
                }
            }
            return FoodStat.All;
        }

        /// <summary>A new slot's rule for an empty cell: by stats with no range (any food), the most of all three.</summary>
        internal static FoodRule AnyFood() => new FoodRule { Mode = FoodMode.ByStats, Prefer = FoodPrefer.Most, Of = FoodStat.All };

        /// <summary>A new slot's rule for a cell holding a food: from a list, that food ticked, the most of all three.</summary>
        internal static FoodRule OnlyThis(string prefab)
        {
            var rule = new FoodRule { Mode = FoodMode.FromList, Prefer = FoodPrefer.Most, Of = FoodStat.All };
            rule.Foods.Add(prefab);
            return rule;
        }

        /// <summary>
        /// The rule in few words, for the window and the messages: what meets it ("Stamina 20 to 50", "Health 30 or more,
        /// Eitr up to 10", "any food"; a list: its one food's name, two names, or "any of 5 foods"; "no food ticked"), then
        /// what it prefers ("most Stamina", "least Health", "most in all"). <paramref name="nameOf"/> names a listed food
        /// (its prefab when null).
        /// </summary>
        internal string Describe(Func<string, string> nameOf = null)
        {
            string what;
            if (Mode == FoodMode.FromList)
            {
                string Name(string prefab) => nameOf?.Invoke(prefab) ?? prefab;
                switch (Foods.Count)
                {
                    case 0:
                        what = "no food ticked";
                        break;
                    case 1:
                        what = Name(Foods[0]);
                        break;
                    case 2:
                        what = $"{Name(Foods[0])} or {Name(Foods[1])}";
                        break;
                    default:
                        what = $"any of {Foods.Count} foods";
                        break;
                }
            }
            else
            {
                var ranges = new List<string>();
                foreach (FoodStat stat in new[] { FoodStat.Health, FoodStat.Stamina, FoodStat.Eitr })
                {
                    FoodRange range = RangeOf(stat);
                    if (range.Given)
                    {
                        ranges.Add($"{stat} {range.Words()}");
                    }
                }
                what = ranges.Count > 0 ? string.Join(", ", ranges) : "any food";
            }
            string prefer = Prefer == FoodPrefer.Least ? "least" : "most";
            return $"{what}, {prefer} {(Of == FoodStat.All ? "in all" : Of.ToString())}";
        }
    }
}
