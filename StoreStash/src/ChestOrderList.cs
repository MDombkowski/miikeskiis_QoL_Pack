using System;
using System.Collections.Generic;
using System.Text;
using QoLMods.Chests;

namespace StoreStash
{
    /// <summary>
    /// The setting "Which chest first" (card A41's second round, decision 28) as a list: the chest service's four rules
    /// (ChestOrder), each on or off, in his order. Read from the setting's text whatever it holds (Parse), written back the
    /// same way (Format: the rules' names in order, a minus before one that's off), moved a row at a time (the drawer's drag,
    /// Up and Down), and handed to the quick stack as the rules that are on (Effective). The two rules that only sort set-up
    /// chests among themselves (Specific, Most) always stay below SetUp and move with it. Pure, for the scratch harness.
    /// </summary>
    internal sealed class ChestOrderList
    {
        /// <summary>The rules in their default order: SetUp, Specific and Most as StoreStash 0.4.0 had them, then Personal.</summary>
        internal static readonly ChestOrder[] Defaults = { ChestOrder.SetUp, ChestOrder.Specific, ChestOrder.Most, ChestOrder.Personal };

        /// <summary>The setting's default text: "SetUp, Specific, Most, -Personal".</summary>
        internal static readonly string DefaultText = Parse(null).Format();

        private readonly List<ChestOrder> rules = new List<ChestOrder>();
        private readonly HashSet<ChestOrder> off = new HashSet<ChestOrder>();

        private ChestOrderList()
        {
        }

        /// <summary>How many rows: always every rule, once.</summary>
        internal int Count => rules.Count;

        /// <summary>The rule on a row, top to bottom.</summary>
        internal ChestOrder this[int row] => rules[row];

        /// <summary>Whether a rule is on by default: all but Personal (new, and off, so nothing changes for anyone).</summary>
        internal static bool OnByDefault(ChestOrder rule) => rule != ChestOrder.Personal;

        /// <summary>
        /// The list a text says, whatever it holds: the rules' names (any case, spaces ignored) separated by commas or
        /// semicolons, a minus before one that's off. Unknown names and repeats are ignored; a rule left out takes its
        /// default place as it is by default: SetUp just above whichever of Specific and Most comes first (else above
        /// Personal, else last: review round 4), Specific just above Most (or, without Most, right below SetUp), Most just
        /// below Specific (34-B's harness found Most put above Specific), Personal last; then Specific and Most are put back
        /// below SetUp.
        /// </summary>
        internal static ChestOrderList Parse(string text)
        {
            var list = new ChestOrderList();
            if (!string.IsNullOrEmpty(text))
            {
                foreach (string part in text.Split(',', ';'))
                {
                    string token = part.Trim();
                    bool on = true;
                    if (token.StartsWith("-", StringComparison.Ordinal))
                    {
                        on = false;
                        token = token.Substring(1);
                    }
                    else if (token.StartsWith("+", StringComparison.Ordinal))
                    {
                        token = token.Substring(1);
                    }
                    if (!TryRule(token, out ChestOrder rule) || list.rules.Contains(rule))
                    {
                        continue;
                    }
                    list.rules.Add(rule);
                    if (!on)
                    {
                        list.off.Add(rule);
                    }
                }
            }
            for (int d = 0; d < Defaults.Length; d++)
            {
                ChestOrder rule = Defaults[d];
                if (list.rules.Contains(rule))
                {
                    continue;
                }
                int at = list.rules.Count;   // Personal: last
                if (SortsSetUp(rule))
                {
                    // Beside its partner, as by default: Specific just above Most wherever Most is ("Most, SetUp" reads
                    // "SetUp, Specific, Most"), else right below SetUp; Most just below Specific, in the list by now (the loop
                    // goes in the default order), so "Personal, SetUp, Specific" reads "Personal, SetUp, Specific, Most".
                    int most = rule == ChestOrder.Specific ? list.rules.IndexOf(ChestOrder.Most) : -1;
                    at = most >= 0 ? most : list.rules.IndexOf(Defaults[d - 1]) + 1;
                }
                else if (rule == ChestOrder.SetUp)
                {
                    // Just above whichever of Specific and Most comes first in the list, else above Personal, else last
                    // (review round 4: "Most, Personal, Specific" keeps Most above Personal, as written).
                    int specific = list.rules.IndexOf(ChestOrder.Specific), most = list.rules.IndexOf(ChestOrder.Most);
                    int first = specific < 0 ? most : most < 0 ? specific : Math.Min(specific, most);
                    int personal = list.rules.IndexOf(ChestOrder.Personal);
                    at = first >= 0 ? first : personal >= 0 ? personal : list.rules.Count;
                }
                list.rules.Insert(at, rule);
                if (!OnByDefault(rule))
                {
                    list.off.Add(rule);
                }
            }
            list.Normalise();
            return list;
        }

        /// <summary>The text the setting keeps: "SetUp, Specific, Most, -Personal".</summary>
        internal string Format()
        {
            var text = new StringBuilder();
            foreach (ChestOrder rule in rules)
            {
                if (text.Length > 0)
                {
                    text.Append(", ");
                }
                if (off.Contains(rule))
                {
                    text.Append('-');
                }
                text.Append(rule);
            }
            return text.ToString();
        }

        internal bool IsOn(ChestOrder rule) => !off.Contains(rule);

        /// <summary>Switches a rule on or off; true when that changed it.</summary>
        internal bool SetOn(ChestOrder rule, bool on) => on ? off.Remove(rule) : off.Add(rule);

        /// <summary>
        /// The rules the quick stack follows, first to last (StackRules.Order): those that are on, without Specific and
        /// Most while SetUp is off (they sort set-up chests only). The chest service drops the set-up rules too while chest
        /// setups are off.
        /// </summary>
        internal List<ChestOrder> Effective()
        {
            var effective = new List<ChestOrder>(rules.Count);
            bool setUp = IsOn(ChestOrder.SetUp);
            foreach (ChestOrder rule in rules)
            {
                if (IsOn(rule) && (setUp || !SortsSetUp(rule)))
                {
                    effective.Add(rule);
                }
            }
            return effective;
        }

        /// <summary>
        /// Moves a row to a gap between rows (0: above the first, Count: below the last): SetUp takes along the rows of
        /// Specific and Most right below it, and every other row moves alone. Specific and Most never go above SetUp: one
        /// moved there comes back right below it; one SetUp's move leaves above it goes below it again in the order the two
        /// had (the last look: a drag of SetUp never swaps them). True when the order changed.
        /// </summary>
        internal bool Move(int from, int gap)
        {
            if (from < 0 || from >= rules.Count || gap < 0 || gap > rules.Count)
            {
                return false;
            }
            int size = UnitSize(from);
            if (gap >= from && gap <= from + size)
            {
                return false;   // into itself, or the gaps right beside it: nowhere new
            }
            var before = new List<ChestOrder>(rules);
            List<ChestOrder> unit = rules.GetRange(from, size);
            rules.RemoveRange(from, size);
            rules.InsertRange(gap > from ? gap - size : gap, unit);
            Normalise(unit.Contains(ChestOrder.SetUp) ? before : null);
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i] != before[i])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A row one place up (SetUp with its two below it); true when the order changed.</summary>
        internal bool Up(int row) => row > 0 && Move(row, row - 1);

        /// <summary>A row one place down (SetUp with its two below it, past the next row); true when the order changed.</summary>
        internal bool Down(int row) => row >= 0 && row < rules.Count && Move(row, row + UnitSize(row) + 1);

        /// <summary>Whether Up would change anything (the drawer greys a button that wouldn't).</summary>
        internal bool CanUp(int row) => Copy().Up(row);

        internal bool CanDown(int row) => Copy().Down(row);

        internal ChestOrderList Copy()
        {
            var copy = new ChestOrderList();
            copy.rules.AddRange(rules);
            copy.off.UnionWith(off);
            return copy;
        }

        /// <summary>A rule's words in the settings window: plain characters, the window's font may lack others.</summary>
        internal static string Words(ChestOrder rule)
        {
            switch (rule)
            {
                case ChestOrder.SetUp:
                    return "Chests set up for it";
                case ChestOrder.Specific:
                    return "...the most specific setup";
                case ChestOrder.Most:
                    return "...the one holding the most of it";
                case ChestOrder.Personal:
                    return "My personal chests";
                default:
                    return rule.ToString();
            }
        }

        /// <summary>Whether a rule's row is drawn a little to the right, under SetUp's: the two that sort set-up chests.</summary>
        internal static bool Indented(ChestOrder rule) => SortsSetUp(rule);

        /// <summary>
        /// Why a row does nothing now, or little, in a few words after it, or "" when it counts: the set-up rules while chest
        /// setups are off (their switch, or StoreStash's own), Specific and Most while SetUp is off, and his personal chests
        /// while "Use my personal chests" is off (the quick stack key leaves his Personal Chests and marked chests alone
        /// then; with "every chest I built" counted, it still uses those).
        /// </summary>
        internal static string Note(ChestOrder rule, bool setupsOn, bool setUpOn, bool personalChestsUsed)
        {
            if (rule == ChestOrder.Personal)
            {
                return personalChestsUsed ? string.Empty : "(\"Use my personal chests\" is off: the quick stack key leaves your Personal Chests and marked chests alone)";
            }
            if (!setupsOn)
            {
                return "(chest setups are off)";
            }
            return SortsSetUp(rule) && !setUpOn ? "(only with \"Chests set up for it\" on)" : string.Empty;
        }

        /// <summary>The list as StoreStash 0.4.0's three switches left it ("Set-up chests first", "The most specific setup
        /// first", "The chest holding most of it first"): the default list, with each switch that was off off.</summary>
        internal static ChestOrderList FromSwitches(bool first, bool specificFirst, bool mostFirst)
        {
            ChestOrderList list = Parse(null);
            list.SetOn(ChestOrder.SetUp, first);
            list.SetOn(ChestOrder.Specific, specificFirst);
            list.SetOn(ChestOrder.Most, mostFirst);
            return list;
        }

        // The rules that tell set-up chests apart only, and so stay below SetUp.
        private static bool SortsSetUp(ChestOrder rule) => rule == ChestOrder.Specific || rule == ChestOrder.Most;

        // SetUp with the rows of Specific and Most right below it; any other row alone.
        private int UnitSize(int row)
        {
            if (rules[row] != ChestOrder.SetUp)
            {
                return 1;
            }
            int size = 1;
            while (row + size < rules.Count && SortsSetUp(rules[row + size]))
            {
                size++;
            }
            return size;
        }

        // Specific and Most found above SetUp go right below it, in the order they had. When SetUp itself moved and left
        // them there (reference: the rows before the move), each goes below it among the sort rules right below it in the
        // order the two had before the move, so they never swap (the last look).
        private void Normalise(IList<ChestOrder> reference = null)
        {
            int setUp = rules.IndexOf(ChestOrder.SetUp);
            var above = new List<ChestOrder>();
            for (int i = 0; i < setUp; i++)
            {
                if (SortsSetUp(rules[i]))
                {
                    above.Add(rules[i]);
                }
            }
            if (above.Count == 0)
            {
                return;
            }
            foreach (ChestOrder rule in above)
            {
                rules.Remove(rule);
            }
            if (reference == null)
            {
                rules.InsertRange(rules.IndexOf(ChestOrder.SetUp) + 1, above);
                return;
            }
            foreach (ChestOrder rule in above)
            {
                int at = rules.IndexOf(ChestOrder.SetUp) + 1;
                while (at < rules.Count && SortsSetUp(rules[at]) && reference.IndexOf(rules[at]) < reference.IndexOf(rule))
                {
                    at++;
                }
                rules.Insert(at, rule);
            }
        }

        /// <summary>The text "Which chest first" should hold for <paramref name="text"/> (its list, written the one way:
        /// Format), or null when it holds that already: a hand-written text meaning the default is put right, so it no longer
        /// reads as changed (the last look). Null too for a text naming a rule this version doesn't know (a newer version's):
        /// kept as written at start (review round 2 of the last look), until he changes the list in F1, which writes the rules
        /// this version knows (review round 3).</summary>
        internal static string PutRight(string text)
        {
            if (HasUnknown(text))
            {
                return null;
            }
            string right = Parse(text).Format();
            return right == text ? null : right;
        }

        // Whether a text names a rule this version doesn't know (Parse ignores it).
        private static bool HasUnknown(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            foreach (string part in text.Split(',', ';'))
            {
                string token = part.Trim();
                if (token.StartsWith("-", StringComparison.Ordinal) || token.StartsWith("+", StringComparison.Ordinal))
                {
                    token = token.Substring(1);
                }
                if (token.Trim().Length > 0 && !TryRule(token, out _))
                {
                    return true;
                }
            }
            return false;
        }

        // A rule by its name, in any case, spaces ignored ("set up" is SetUp); never by number.
        private static bool TryRule(string token, out ChestOrder rule)
        {
            var name = new StringBuilder(token.Length);
            foreach (char letter in token)
            {
                if (!char.IsWhiteSpace(letter))
                {
                    name.Append(letter);
                }
            }
            foreach (ChestOrder each in Defaults)
            {
                if (string.Equals(name.ToString(), each.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    rule = each;
                    return true;
                }
            }
            rule = default;
            return false;
        }
    }
}
