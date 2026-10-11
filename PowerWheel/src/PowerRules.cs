using System;
using System.Collections.Generic;

namespace PowerWheel
{
    /// <summary>What the game says about one power for this character, gathered by Powers (or made up by a test).</summary>
    internal struct PowerFacts
    {
        /// <summary>The power's status effect name, such as "GP_Moder": what the game stores as the character's power.</summary>
        internal string Name;

        /// <summary>It is the character's power now.</summary>
        internal bool IsCurrent;

        /// <summary>The character took it at its stone: the game wrote its name into the character's own record
        /// (Player.SetGuardianPower adds it to the unique keys).</summary>
        internal bool TakenAtStone;

        /// <summary>Its boss has been beaten in this world (the world's "defeated_…" key); null when no boss is known for
        /// it (another mod's power).</summary>
        internal bool? BossBeaten;

        /// <summary>Its boss's trophy has been seen on its stone in this world by this character's game (Trophies).</summary>
        internal bool TrophyPlaced;
    }

    /// <summary>What a pick of a power comes to (PowerRules.Decide).</summary>
    internal enum PickResult
    {
        /// <summary>It becomes the character's power.</summary>
        Picked,

        /// <summary>It is the character's power already: nothing changes.</summary>
        AlreadyCurrent,

        /// <summary>Not earned (by the settings' rule): nothing changes.</summary>
        Locked,

        /// <summary>The power cools down and "Picking while it cools down" is off: nothing changes.</summary>
        CoolingDown,
    }

    /// <summary>How a power was chosen in the wheel.</summary>
    internal enum Via
    {
        /// <summary>A left click (or the gamepad's confirm, or the game's gamepad hover, flick or double tap).</summary>
        Click,

        /// <summary>The key let go.</summary>
        Release,

        /// <summary>A right click, when "Right click" picks and uses.</summary>
        RightClick,
    }

    /// <summary>What a choice in the wheel does, by the settings for each way of choosing.</summary>
    internal enum Outcome
    {
        Nothing,
        Pick,
        PickAndUse,
    }

    /// <summary>
    /// Card A75's rules, kept apart from the game so they can be checked outside it: which powers there are and in which
    /// order, which a character has earned, which the wheel shows and lets him pick, what each way of choosing does, the
    /// words the mod shows, and the wheel's layout as the game will make it (the padding to a full ring, and where it will
    /// start) from the game's own rules in Valheim.UI.RadialBase (1.0.17).
    /// </summary>
    internal static class PowerRules
    {
        /// <summary>Every forsaken power's status effect starts so; the mod lists them from the game's ObjectDB at run
        /// time, so a power the game adds later (or another mod's) appears by itself.</summary>
        internal const string Prefix = "GP_";

        /// <summary>The game's powers in the order the game is played: listed first, in this order; any other after
        /// them, alphabetically (his rule 7: progression is the default order).</summary>
        internal static readonly string[] Progression =
        {
            "GP_Eikthyr", "GP_TheElder", "GP_Bonemass", "GP_Moder", "GP_Yagluth", "GP_Queen", "GP_Fader",
        };

        // Each power's boss's world key, set when it dies (the bosses' m_defeatSetGlobalKey, from the game's assets; card
        // A75's research). A power not listed has no boss known to the mod.
        private static readonly Dictionary<string, string> BossKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "GP_Eikthyr", "defeated_eikthyr" },
            { "GP_TheElder", "defeated_gdking" },
            { "GP_Bonemass", "defeated_bonemass" },
            { "GP_Moder", "defeated_dragon" },
            { "GP_Yagluth", "defeated_goblinking" },
            { "GP_Queen", "defeated_queen" },
            { "GP_Fader", "defeated_fader" },
        };

        /// <summary>The share of the distance from the wheel's centre to its icons within which the pointer counts as in
        /// the centre: there it points at the power you have, so letting go changes nothing.</summary>
        internal const float CentreShare = 0.4f;

        internal const string LockedSubtitle = "Locked";

        internal const string NoPowersText = "You have no forsaken powers yet";

        internal static bool IsPower(string name) =>
            !string.IsNullOrEmpty(name) && name.Length > Prefix.Length && name.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>The world key its boss sets when it dies, or null when no boss is known for it.</summary>
        internal static string BossKeyOf(string name) => name != null && BossKeys.TryGetValue(name, out string key) ? key : null;

        /// <summary>The powers among <paramref name="names"/>, each once: the game's in progression order, then any other
        /// alphabetically.</summary>
        internal static List<string> Order(IEnumerable<string> names)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var known = new List<string>();
            var others = new List<string>();
            foreach (string name in names)
            {
                if (!IsPower(name) || !seen.Add(name))
                {
                    continue;
                }
                if (Array.IndexOf(Progression, name) >= 0)
                {
                    known.Add(name);
                }
                else
                {
                    others.Add(name);
                }
            }
            known.Sort((a, b) => Array.IndexOf(Progression, a).CompareTo(Array.IndexOf(Progression, b)));
            others.Sort(StringComparer.Ordinal);
            known.AddRange(others);
            return known;
        }

        /// <summary>Whether the character has earned the power by the rule. The power he has now always counts; another
        /// mod's power, with no boss known, counts under the boss rule once taken at its stone.</summary>
        internal static bool Earned(in PowerFacts facts, EarnedChoice rule)
        {
            if (facts.IsCurrent)
            {
                return true;
            }
            switch (rule)
            {
                case EarnedChoice.TakenAtStone:
                    return facts.TakenAtStone;
                case EarnedChoice.BossBeatenInWorld:
                    return facts.BossBeaten ?? facts.TakenAtStone;
                case EarnedChoice.TrophyAtStone:
                    // A power taken at its stone had its trophy there: the game offers it only then.
                    return facts.TrophyPlaced || facts.TakenAtStone;
                default:
                    return facts.TakenAtStone || facts.BossBeaten == true;
            }
        }

        /// <summary>A locked power shown as a padlock alone, with no icon or name (card B06).</summary>
        internal static bool Hidden(bool locked, LockedLookChoice look) => locked && look == LockedLookChoice.PadlockOnly;

        /// <summary>The name a hidden power shows in the wheel's centre.</summary>
        internal const string HiddenTitle = "Locked";

        /// <summary>Why a hidden power is locked, under "Locked" in the wheel's centre, by the rule.</summary>
        internal static string LockedReason(EarnedChoice rule, bool bossKnown = true)
        {
            switch (bossKnown ? rule : EarnedChoice.TakenAtStone)
            {
                case EarnedChoice.TrophyAtStone:
                    return "Its trophy isn't on its stone yet";
                case EarnedChoice.TakenAtStone:
                    return "Not taken at its stone yet";
                case EarnedChoice.BossBeatenInWorld:
                    return "Its boss isn't beaten yet";
                default:
                    return "Its boss isn't beaten or it isn't taken at its stone yet";
            }
        }

        /// <summary>Whether the wheel shows the power.</summary>
        internal static bool Shown(in PowerFacts facts, ShowChoice show, EarnedChoice rule) =>
            show != ShowChoice.EarnedOnly || Earned(facts, rule);

        /// <summary>Whether the power may be picked: earned, or any power when the settings say so.</summary>
        internal static bool Pickable(in PowerFacts facts, ShowChoice show, EarnedChoice rule) =>
            show == ShowChoice.AllAnyPickable || Earned(facts, rule);

        /// <summary>Shown, but dimmed with a padlock: it can't be picked.</summary>
        internal static bool Locked(in PowerFacts facts, ShowChoice show, EarnedChoice rule) =>
            Shown(facts, show, rule) && !Pickable(facts, show, rule);

        /// <summary>What picking the power comes to, from facts read again at the pick. The cooldown is one timer for every
        /// power and a pick never touches it: "allowed" lets him switch while it runs (the new power waits for the same
        /// timer).</summary>
        internal static PickResult Decide(in PowerFacts facts, ShowChoice show, EarnedChoice rule, bool coolingDown, bool pickWhileCooling)
        {
            if (facts.IsCurrent)
            {
                return PickResult.AlreadyCurrent;
            }
            if (!Shown(facts, show, rule) || !Pickable(facts, show, rule))
            {
                return PickResult.Locked;
            }
            if (coolingDown && !pickWhileCooling)
            {
                return PickResult.CoolingDown;
            }
            return PickResult.Picked;
        }

        /// <summary>What a way of choosing does, by its setting.</summary>
        internal static Outcome OutcomeOf(Via via, LetGoChoice letGo, LeftClickChoice leftClick, RightClickChoice rightClick)
        {
            switch (via)
            {
                case Via.Release:
                    return letGo == LetGoChoice.Picks ? Outcome.Pick : letGo == LetGoChoice.PicksAndUses ? Outcome.PickAndUse : Outcome.Nothing;
                case Via.RightClick:
                    return rightClick == RightClickChoice.PicksAndUses ? Outcome.PickAndUse : Outcome.Nothing;
                default:
                    return leftClick == LeftClickChoice.PicksAndUses ? Outcome.PickAndUse : Outcome.Pick;
            }
        }

        /// <summary>Whether a choice uses the power after its pick: only with "picks and uses", for a power just picked or
        /// the one he has, but never when the key is let go on the power he has (on it, in the wheel's centre, or without
        /// moving at all: holding the key just to look never fires the power). A tap, or a click, uses it.</summary>
        internal static bool Uses(Outcome outcome, PickResult result, Via via) =>
            outcome == Outcome.PickAndUse &&
            (result == PickResult.Picked || (result == PickResult.AlreadyCurrent && via != Via.Release));

        /// <summary>The line under a power's name in the wheel's centre: "Locked" for a locked power, else the shared
        /// cooldown ("Ready" or the time left) when it is shown, else nothing.</summary>
        internal static string Subtitle(bool locked, bool showCooldown, float cooldown, string readyText, string timeLeftText)
        {
            if (locked)
            {
                return LockedSubtitle;
            }
            if (!showCooldown)
            {
                return string.Empty;
            }
            return cooldown > 0f ? timeLeftText : readyText;
        }

        internal static string PickedText(string shownName) => "Power selected: " + shownName;

        /// <summary>Why a power can't be picked, by the rule; a null <paramref name="shownName"/> (a power shown as a
        /// padlock alone) names no power.</summary>
        internal static string LockedText(string shownName, EarnedChoice rule, bool bossKnown = true)
        {
            string which = shownName ?? "This power";
            // Another mod's power, its boss unknown, is earned under every rule only at its stone (Earned's fallback).
            switch (bossKnown ? rule : EarnedChoice.TakenAtStone)
            {
                case EarnedChoice.TakenAtStone:
                    return which + " is locked: take it at its stone first";
                case EarnedChoice.BossBeatenInWorld:
                    return which + " is locked: beat its boss in this world first";
                case EarnedChoice.TrophyAtStone:
                    return which + " is locked: place its boss's trophy on its stone first";
                default:
                    return which + " is locked: beat its boss or take it at its stone first";
            }
        }

        /// <summary>The powers whose trophies were seen on their stones, as kept in a character's data: names joined by
        /// commas. Read leniently (blanks and repeats dropped).</summary>
        internal static HashSet<string> ReadTrophies(string kept)
        {
            var powers = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(kept))
            {
                return powers;
            }
            foreach (string part in kept.Split(','))
            {
                string power = part.Trim();
                if (power.Length > 0)
                {
                    powers.Add(power);
                }
            }
            return powers;
        }

        /// <summary>The powers as kept in a character's data, in a fixed order so an unchanged list writes the same.</summary>
        internal static string WriteTrophies(IEnumerable<string> powers)
        {
            var sorted = new List<string>();
            foreach (string power in powers)
            {
                if (!string.IsNullOrEmpty(power) && power.IndexOf(',') < 0 && !sorted.Contains(power))
                {
                    sorted.Add(power);
                }
            }
            sorted.Sort(StringComparer.Ordinal);
            return string.Join(",", sorted);
        }

        /// <summary>How many segments the game's wheel gives a page of <paramref name="count"/> elements (RadialBase's
        /// SetElementsPerLayer over RadialDataSO.MaxElementsRange, 8 and 12 in its code).</summary>
        internal static int SegmentsFor(int count, int[] range)
        {
            if (range == null || range.Length == 0)
            {
                return count;
            }
            int segments = range[0];
            for (int i = 0; i < range.Length - 1; i++)
            {
                if (count > range[i])
                {
                    segments = range[i + 1];
                }
            }
            return segments;
        }

        /// <summary>
        /// Empty slots to add after the powers so the page fills its ring. Below a full ring the game's ConstructRadial
        /// moves the first element to the middle of the list and turns the ring (CenterBackButton, written for pages
        /// with a back button), which would shuffle the powers; a full ring is laid out as listed, the first at the top
        /// and on clockwise. A page with more than the largest ring is laid out in layers, as the game's own long pages.
        /// </summary>
        internal static int PadsFor(int count, int[] range)
        {
            int segments = SegmentsFor(count, range);
            return count > 0 && count < segments ? segments - count : 0;
        }

        /// <summary>As Unity's Mathf.RoundToInt, which the game's wheel rounds with (halves to even).</summary>
        internal static int RoundToInt(float value) => (int)Math.Round((double)value);

        /// <summary>
        /// What the game's wheel does to a page's starting element (RadialBase.StartItemIndex) when the page has another
        /// number of segments per ring than the last page shown: it takes the index as one in the last page's segments
        /// and turns it into the same angle in the new page's (RadialBase.UpdateSegmentInfo, called from
        /// SetElementsPerLayer only when the number changes; the segment size before any page was shown is 0). Meant
        /// for a page refreshed in place; for a page opened fresh it moves the start onto another element.
        /// </summary>
        internal static int AfterGameConversion(int start, int lastSegments, int newSegments)
        {
            if (newSegments <= 0 || newSegments == lastSegments)
            {
                return start;
            }
            float segmentSize = lastSegments > 0 ? RoundToInt(360f / lastSegments) : 0f;
            if (segmentSize == 0f)
            {
                segmentSize = RoundToInt(360f / newSegments);
            }
            if (start > 0)
            {
                start = RoundToInt(start * segmentSize);
            }
            segmentSize = RoundToInt(360f / newSegments);
            if (start > 0)
            {
                start = RoundToInt(start / segmentSize);
            }
            return start;
        }

        /// <summary>
        /// The StartItemIndex to give the game so that the page starts on element <paramref name="wanted"/> after the
        /// game's conversion (AfterGameConversion). -1 for the first element (the game starts there when given none) or
        /// for none. When no index lands there, <paramref name="wanted"/> itself (the wheel then moves the mouse pointer
        /// onto the element, which the game reads as the selection).
        /// </summary>
        internal static int StartIndexFor(int wanted, int lastSegments, int newSegments)
        {
            if (wanted <= 0)
            {
                return -1;
            }
            if (AfterGameConversion(wanted, lastSegments, newSegments) == wanted)
            {
                return wanted;
            }
            int limit = 4 * Math.Max(Math.Max(lastSegments, newSegments), wanted) + 8;
            for (int start = 1; start <= limit; start++)
            {
                if (AfterGameConversion(start, lastSegments, newSegments) == wanted)
                {
                    return start;
                }
            }
            return wanted;
        }

        /// <summary>Whether the pointer, <paramref name="dx"/> and <paramref name="dy"/> from the wheel's centre, is in its
        /// centre: within CentreShare of the distance to the icons.</summary>
        internal static bool InCentre(float dx, float dy, float iconDistance)
        {
            if (!(iconDistance > 0f))
            {
                return false;
            }
            float radius = CentreShare * iconDistance;
            return dx * dx + dy * dy < radius * radius;
        }
    }
}
