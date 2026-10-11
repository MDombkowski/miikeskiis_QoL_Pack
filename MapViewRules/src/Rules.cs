using System.Collections.Generic;
using UnityEngine;

namespace MapViewRules
{
    /// <summary>What the rules say about a pin at one zoom of one map.</summary>
    internal struct Decision
    {
        /// <summary>Draw neither its icon nor its name: the pin isn't drawn at all, and can't be clicked.</summary>
        internal bool HideIcon;

        /// <summary>Draw its icon without its name (the large map only; the minimap never draws names).</summary>
        internal bool HideName;

        /// <summary>Draw its name without its icon (the large map only): the icon stays, unseen, so the name is placed
        /// and the pin clicked where they always are.</summary>
        internal bool NameAlone;

        /// <summary>Its icon's size as a share of its normal size: 1 is full size.</summary>
        internal float Size;
    }

    /// <summary>
    /// The sections a pin's rules come from, most specific first and All pins last: its override, if the override list
    /// names it; its pickable group, if it has one; its own section; All pins. Each value comes from the first of them
    /// that sets it. Pins with the same sections share one chain, which decides once per drawing pass.
    /// </summary>
    internal sealed class Chain
    {
        private readonly RuleSection[] levels;
        private int decidedPass = -1;
        private bool decidedLarge;
        private Decision decided;

        internal Chain(RuleSection[] levels)
        {
            this.levels = levels;
        }

        internal IList<RuleSection> Levels => levels;

        /// <summary>The decision for a zoom number of the large map or the minimap, worked out once per pass.</summary>
        internal Decision Decide(bool large, float level, int pass)
        {
            if (pass != decidedPass || large != decidedLarge)
            {
                decided = Work(levels, large, level);
                decidedPass = pass;
                decidedLarge = large;
            }
            return decided;
        }

        /// <summary>
        /// The rules at one zoom number (0 fully in, 100 fully out). Icons hide past their zoom, and names with them;
        /// names alone hide past theirs (the large map only); "past" is by the whole number, as shown. On the large map,
        /// icons and names each have a switch: a name whose icon is switched off is drawn alone, and a pin with both off
        /// isn't drawn. Past the shrinking zoom, an icon's size goes evenly from full size down to the smallest size,
        /// reached fully zoomed out.
        /// </summary>
        internal static Decision Work(IList<RuleSection> levels, bool large, float level)
        {
            bool shown = true;
            bool namesShown = true;
            int iconsPast = 100;
            int namesPast = 100;
            int shrinkFrom = 100;
            int smallest = 100;
            bool haveShown = false, haveNamesShown = false, haveIcons = false, haveNames = false, haveShrink = false, haveSmallest = false;
            foreach (RuleSection section in levels)
            {
                if (!haveShown && (large ? section.LargeShown : section.SmallShown) is bool show)
                {
                    shown = show;
                    haveShown = true;
                }
                if (large && !haveNamesShown && section.LargeNamesShown is bool showNames)
                {
                    namesShown = showNames;
                    haveNamesShown = true;
                }
                if (!haveIcons && (large ? section.LargeIconsPast : section.SmallIconsPast) is int icons)
                {
                    iconsPast = icons;
                    haveIcons = true;
                }
                if (large && !haveNames && section.LargeNamesPast is int names)
                {
                    namesPast = names;
                    haveNames = true;
                }
                if (!haveShrink && (large ? section.LargeShrinkFrom : section.SmallShrinkFrom) is int shrink)
                {
                    shrinkFrom = shrink;
                    haveShrink = true;
                }
                if (!haveSmallest && (large ? section.LargeSmallest : section.SmallSmallest) is int size)
                {
                    smallest = size;
                    haveSmallest = true;
                }
            }

            // Hiding compares the whole number the readout shows and the button fills in: at the readout's 33, a setting of
            // 33 still shows, and 34 hides. Shrinking follows the exact zoom, so it is smooth.
            int whole = ZoomLevel.Whole(level);
            var decision = new Decision { Size = 1f };
            bool pastIcons = whole > iconsPast;
            if (!large)
            {
                decision.HideIcon = !shown || pastIcons;
            }
            else
            {
                bool icon = shown && !pastIcons;
                bool name = namesShown && !pastIcons && whole <= namesPast;
                decision.HideIcon = !icon && !name;
                decision.HideName = !name;
                decision.NameAlone = !icon && name;
            }
            if (!decision.HideIcon && !decision.NameAlone && shrinkFrom < 100 && level > shrinkFrom)
            {
                float smallestShare = Mathf.Clamp(smallest, RuleSection.SmallestSize, 100) / 100f;
                float along = (level - shrinkFrom) / (100f - shrinkFrom);
                decision.Size = Mathf.Lerp(1f, smallestShare, along);
            }
            return decision;
        }
    }

    /// <summary>One chain for each combination of sections in use.</summary>
    internal static class Chains
    {
        private static readonly Dictionary<Combination, Chain> made = new Dictionary<Combination, Chain>();

        /// <summary>The chain of a pin with these sections: its override (or null), its group (or null) and its own.</summary>
        internal static Chain Get(RuleSection over, RuleSection group, RuleSection own)
        {
            var combination = new Combination(over, group, own);
            if (made.TryGetValue(combination, out Chain chain))
            {
                return chain;
            }
            var levels = new List<RuleSection>(4);
            if (over != null)
            {
                levels.Add(over);
            }
            if (group != null)
            {
                levels.Add(group);
            }
            if (own != null && own != Sections.All)
            {
                levels.Add(own);
            }
            levels.Add(Sections.All);
            chain = new Chain(levels.ToArray());
            made[combination] = chain;
            return chain;
        }

        private readonly struct Combination : System.IEquatable<Combination>
        {
            private readonly RuleSection over;
            private readonly RuleSection group;
            private readonly RuleSection own;

            internal Combination(RuleSection over, RuleSection group, RuleSection own)
            {
                this.over = over;
                this.group = group;
                this.own = own;
            }

            public bool Equals(Combination other) => over == other.over && group == other.group && own == other.own;

            public override bool Equals(object obj) => obj is Combination other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = over?.GetHashCode() ?? 0;
                    hash = hash * 31 + (group?.GetHashCode() ?? 0);
                    return hash * 31 + (own?.GetHashCode() ?? 0);
                }
            }
        }
    }
}
