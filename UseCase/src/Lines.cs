using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QoLMods.Catalogue;

namespace UseCase
{
    /// <summary>What the player knows, for the spoiler rule. The game's own lists in game (Knowledge); made up in tests.</summary>
    internal interface IKnowledge
    {
        /// <summary>The game's known list (recipes by what they make, pieces by their $ name; one list for both).</summary>
        bool KnowsRecipe(string name);

        /// <summary>An item he has held, or a trader has shown him.</summary>
        bool KnowsMaterial(string name);

        /// <summary>A place on the mod's own seen list.</summary>
        bool HasSeen(string key);

        /// <summary>A global key the world has.</summary>
        bool WorldHas(string key);

        /// <summary>The game shows every recipe itself (the no-cost cheat, the world's "all recipes unlocked").</summary>
        bool GameShowsAllRecipes { get; }

        /// <summary>The game shows every piece itself (the no-cost cheat, the world's "all pieces unlocked").</summary>
        bool GameShowsAllPieces { get; }
    }

    /// <summary>A snapshot of the settings that shape the panel's words, read once each time a setting changes.</summary>
    internal sealed class View
    {
        /// <summary>Each kind's switch; as the settings are out of the box: all on but the ballista's targets (selling too
        /// since card A63).</summary>
        internal readonly bool[] KindOn = Enum.GetValues(typeof(UseKind)).Cast<UseKind>().Select(kind => kind != UseKind.BallistaTargets).ToArray();
        internal bool SelfUses;
        internal bool AnythingRules;
        internal KeptLitPieces KeptLit = KeptLitPieces.ShownAsTheGameHasThem;
        internal OtherMods OtherMods = OtherMods.Shown;
        internal bool SplitBuildingByTool;
        internal bool Headings = true;
        internal List<UseKind> Order = Enum.GetValues(typeof(UseKind)).Cast<UseKind>().ToList();
        internal InsideOrder Inside = InsideOrder.Progression;
        internal bool Amounts = true;
        internal UpgradeNumber Upgrade = UpgradeNumber.PerLevel;
        internal ProcessingNumber Processing = ProcessingNumber.StationHolds;
        internal bool Makes;
        internal bool Station;
        internal bool HideUnknown = true;
        internal UnknownLine Unknown = UnknownLine.HiddenWithAmount;
        internal PlacesUnknownUntil Places = PlacesUnknownUntil.Seen;
        internal bool HideTamingAndBait;
        internal bool EverythingWhenTheGameShowsAll = true;
        internal OutOfSeason OutOfSeason = OutOfSeason.ListedMarked;
        internal string Bullet = "•";
        internal string Title = "Used In";
        internal Loose NeverList = new Loose();
        internal LongLists Long = LongLists.Columns;
        internal int Cap = 8;

        /// <summary>A $ token in his language (Localization in game).</summary>
        internal Func<string, string> Localize = token => token;
    }

    /// <summary>One row of the panel: a section's heading, or a line.</summary>
    internal sealed class Row
    {
        internal string Text;
        internal bool Heading;

        /// <summary>How many of the list's lines it stands for: a line 1, a heading 0, "and 22 more" 22, a folded
        /// section's heading its count.</summary>
        internal int Stands;

        internal Row(string text, bool heading, int stands = -1)
        {
            Text = text;
            Heading = heading;
            Stands = stands >= 0 ? stands : heading ? 0 : 1;
        }

        public override string ToString() => Heading ? "[" + Text + "]" : Text;
    }

    /// <summary>The panel's words for one resource: a title, and its rows in order.</summary>
    internal sealed class PanelText
    {
        internal string Title = string.Empty;
        internal readonly List<Row> Rows = new List<Row>();

        /// <summary>Each section's heading and lines, whole, before the long-list choice made Rows from them.</summary>
        internal readonly List<(Row Heading, List<Row> Lines)> Sections = new List<(Row, List<Row>)>();
    }

    /// <summary>
    /// Turns a resource's uses into the panel's words, per the settings (View) and what the player knows. Plain logic,
    /// with no game in it: every rule of the card that isn't drawing is here, tested by the harness.
    /// </summary>
    internal static class Lines
    {
        private static readonly StringComparer Names = StringComparer.Create(CultureInfo.InvariantCulture, true);

        /// <summary>The words for a resource, or null when nothing is left to list (no panel then).</summary>
        /// <param name="unfolded">For the folded choice: the key is held, so every section shows whole.</param>
        internal static PanelText Build(IEnumerable<Use> uses, View view, IKnowledge know, bool unfolded = false)
        {
            // Sections by key: a kind, or with Building split by tool, one per tool.
            var sections = new Dictionary<string, Section>();
            foreach (Use use in uses)
            {
                if (!Shows(use, view))
                {
                    continue;
                }
                string key = SectionKey(use, view);
                if (!sections.TryGetValue(key, out Section section))
                {
                    sections[key] = section = new Section
                    {
                        Kind = use.Kind,
                        Heading = HeadingOf(use, view),
                        ToolOrder = view.SplitBuildingByTool && use.Kind == UseKind.Building ? use.ToolOrder : 0,
                    };
                }
                section.Uses.Add(use);
            }
            if (sections.Count == 0)
            {
                return null;
            }

            var text = new PanelText { Title = view.Title ?? string.Empty };
            foreach (Section section in sections.Values.OrderBy(each => Place(view.Order, each.Kind)).ThenBy(each => each.ToolOrder))
            {
                List<Row> lines = LinesOf(section, view, know);
                if (lines.Count == 0)
                {
                    continue;
                }
                text.Sections.Add((new Row(section.Heading, true), lines));
            }
            if (text.Sections.Count == 0)
            {
                return null;
            }
            foreach ((Row heading, List<Row> lines) in text.Sections)
            {
                if (view.Long == LongLists.FoldedUntilKeyHeld && !unfolded)
                {
                    // Folded, a section is its heading and a count, headings on or off: there is nothing else to show.
                    text.Rows.Add(new Row($"{heading.Text} ({lines.Sum(line => line.Stands)})", true, lines.Sum(line => line.Stands)));
                    continue;
                }
                if (view.Headings)
                {
                    text.Rows.Add(heading);
                }
                if (view.Long == LongLists.CapPerSection && view.Cap > 0 && lines.Count > view.Cap + 1)
                {
                    text.Rows.AddRange(lines.Take(view.Cap));
                    int more = lines.Skip(view.Cap).Sum(line => line.Stands);
                    text.Rows.Add(new Row($"and {more} more", false, more));
                }
                else
                {
                    text.Rows.AddRange(lines);
                }
            }
            return text;
        }

        /// <summary>Whether a use is listed at all, before the spoiler rule: its kind's switch and the lists' rules.</summary>
        internal static bool Shows(Use use, View view)
        {
            if (!view.KindOn[(int)use.Kind])
            {
                return false;
            }
            if (use.SelfUse && !view.SelfUses)
            {
                return false;
            }
            if (use.KeptLit && use.Kind == UseKind.Refueling && view.KeptLit == KeptLitPieces.LeftOutOfRefueling)
            {
                return false;
            }
            if (!use.InSeason && view.OutOfSeason == OutOfSeason.Hidden && (use.Kind == UseKind.Building || use.Kind == UseKind.Crafting || use.Kind == UseKind.Upgrading))
            {
                return false;
            }
            if (use.Mod.Length > 0 && view.OtherMods == OtherMods.Hidden)
            {
                return false;
            }
            if (!view.NeverList.IsEmpty && view.NeverList.HasAny(use.Prefab, use.Token, view.Localize(use.Token), use.OtherName))
            {
                return false;
            }
            return true;
        }

        /// <summary>Whether the player has met what a line names; always true while spoilers aren't hidden.</summary>
        internal static bool Known(Use use, View view, IKnowledge know)
        {
            if (!view.HideUnknown)
            {
                return true;
            }
            switch (use.KnownBy)
            {
                case KnownBy.Recipe:
                    return (view.EverythingWhenTheGameShowsAll && know.GameShowsAllRecipes) || know.KnowsRecipe(use.KnownKey);
                case KnownBy.Piece:
                    return (view.EverythingWhenTheGameShowsAll && know.GameShowsAllPieces) || know.KnowsRecipe(use.KnownKey);
                case KnownBy.Seen:
                    switch (view.Places)
                    {
                        case PlacesUnknownUntil.NeverHidden:
                            return true;
                        case PlacesUnknownUntil.BossBeaten:
                            return use.BeatenKey.Length > 0 ? know.WorldHas(use.BeatenKey) : know.HasSeen(use.KnownKey);
                        default:
                            return know.HasSeen(use.KnownKey) || (use.BeatenKey.Length > 0 && know.WorldHas(use.BeatenKey));
                    }
                case KnownBy.Material:
                    return !view.HideTamingAndBait || use.KnownKey.Length == 0 || know.KnowsMaterial(use.KnownKey);
                case KnownBy.GlobalKey:
                    return use.KnownKey.Length == 0 || know.WorldHas(use.KnownKey);
                default:
                    return true;
            }
        }

        private static List<Row> LinesOf(Section section, View view, IKnowledge know)
        {
            var known = new List<Use>();
            var unknown = new List<Use>();
            foreach (Use use in section.Uses)
            {
                (Known(use, view, know) ? known : unknown).Add(use);
            }
            var rows = new List<Row>();
            var said = new HashSet<string>(StringComparer.Ordinal);
            foreach (Use use in Ordered(known, view))
            {
                string line = view.Bullet.Length > 0 ? view.Bullet + " " + Words(use, view) : Words(use, view);
                if (said.Add(line))
                {
                    rows.Add(new Row(line, false));
                }
            }
            // Unknown lines go last in their section, so where they fall among the known ones gives nothing away.
            switch (view.Unknown)
            {
                case UnknownLine.LeftOut:
                    break;
                case UnknownLine.CountedAtTheEnd:
                    if (unknown.Count > 0)
                    {
                        rows.Add(new Row($"{unknown.Count} more you haven't found", false, unknown.Count));
                    }
                    break;
                default:
                    foreach (Use use in Ordered(unknown, view))
                    {
                        string amount = view.Unknown == UnknownLine.HiddenWithAmount ? AmountPart(use, view) : string.Empty;
                        rows.Add(new Row((view.Bullet.Length > 0 ? view.Bullet + " " : string.Empty) + "???" + amount, false));
                    }
                    break;
            }
            return rows;
        }

        private static IEnumerable<Use> Ordered(List<Use> uses, View view)
        {
            Comparison<Use> order;
            switch (view.Inside)
            {
                case InsideOrder.GameMenus:
                    order = (a, b) =>
                    {
                        int byTool = a.ToolOrder.CompareTo(b.ToolOrder);
                        return byTool != 0 ? byTool : a.GameOrder.CompareTo(b.GameOrder);
                    };
                    break;
                case InsideOrder.AToZ:
                    order = (a, b) => Names.Compare(NameOf(a, view), NameOf(b, view));
                    break;
                default:
                    order = (a, b) =>
                    {
                        if (a.Sort == null || b.Sort == null)
                        {
                            int nulls = (a.Sort == null).CompareTo(b.Sort == null);
                            return nulls != 0 ? nulls : Names.Compare(NameOf(a, view), NameOf(b, view));
                        }
                        return Browse.CompareProgression(a.Sort, b.Sort);
                    };
                    break;
            }
            // Stable: things the order calls equal stay in the game's own order (the catalogue's Browse.Sorted does the same).
            return uses.OrderBy(use => use.ToolOrder).ThenBy(use => use.GameOrder).OrderBy(use => use, Comparer<Use>.Create(order));
        }

        /// <summary>A known line's words, without its bullet: "Fire Arrow · 8 (makes 20)".</summary>
        internal static string Words(Use use, View view)
        {
            string name = NameOf(use, view);
            var notes = new List<string>();
            if (use.Note.Length > 0)
            {
                notes.Add(view.Localize(use.Note));
            }
            if (view.Makes && use.Makes > 1 && use.Kind != UseKind.Buying)
            {
                notes.Add($"makes {use.Makes}");
            }
            if (view.Station && use.Station.Length > 0)
            {
                notes.Add(use.Station);
            }
            if (use.Season.Length > 0)
            {
                notes.Add(use.Season);
            }
            if (view.OtherMods == OtherMods.MarkedWithTheirMod && use.Mod.Length > 0)
            {
                notes.Add(use.Mod);
            }
            string words = name + AmountPart(use, view);
            return notes.Count > 0 ? $"{words} ({string.Join(", ", notes)})" : words;
        }

        /// <summary>The thing's name as the line shows it: a ware with its stack ("Iron Arrow x20", as the trader's list
        /// writes it).</summary>
        internal static string NameOf(Use use, View view)
        {
            string name = view.Localize(use.Token);
            return use.Kind == UseKind.Buying && use.Makes > 1 ? $"{name} x{use.Makes}" : name;
        }

        /// <summary>The " · X" part, or empty where the line has no number or numbers are off.</summary>
        internal static string AmountPart(Use use, View view)
        {
            if (!view.Amounts)
            {
                return string.Empty;
            }
            switch (use.Kind)
            {
                case UseKind.Upgrading:
                    if (view.Upgrade == UpgradeNumber.EachLevel && use.Levels != null && use.Levels.Length > 0)
                    {
                        return " · " + string.Join(" / ", use.Levels);
                    }
                    return use.Amount > 0 ? (use.Upgrader ? $" · {use.Amount}" : $" · {use.Amount} per level") : string.Empty;
                case UseKind.Processing:
                    int amount = view.Processing == ProcessingNumber.PerProduct && use.PerProduct > 0 ? use.PerProduct : use.Amount;
                    return amount > 0 ? $" · {amount}" : string.Empty;
                default:
                    return use.Amount > 0 ? $" · {use.Amount}" : string.Empty;
            }
        }

        private static string SectionKey(Use use, View view) =>
            view.SplitBuildingByTool && use.Kind == UseKind.Building ? "tool:" + use.Tool : "kind:" + (int)use.Kind;

        private static string HeadingOf(Use use, View view) =>
            view.SplitBuildingByTool && use.Kind == UseKind.Building && use.Tool.Length > 0 ? view.Localize(use.Tool) : Heading(use.Kind);

        /// <summary>A section's heading: his four words, then the game's other uses, in the game's style (title case).</summary>
        internal static string Heading(UseKind kind)
        {
            switch (kind)
            {
                case UseKind.LockedDoors: return "Locked Doors";
                case UseKind.FishingBait: return "Fishing Bait";
                case UseKind.GuardianStones: return "Guardian Stones";
                case UseKind.BallistaTargets: return "Ballista Targets";
                default: return kind.ToString();
            }
        }

        /// <summary>The default section order line, as its setting shows it.</summary>
        internal static string DefaultOrderLine => string.Join(", ", Enum.GetValues(typeof(UseKind)).Cast<UseKind>().Select(Heading));

        /// <summary>The section order from its setting's line: the names it gives first, the rest after in the default
        /// order; names it doesn't know are ignored (the catalogue's Orders.Apply), and "Refuelling" is his "Refueling".</summary>
        internal static List<UseKind> OrderFrom(string line)
        {
            List<UseKind> kinds = Enum.GetValues(typeof(UseKind)).Cast<UseKind>().ToList();
            List<string> headings = kinds.Select(Heading).ToList();
            string spelled = (line ?? string.Empty).Replace("Refuelling", "Refueling").Replace("refuelling", "refueling");
            (List<string> order, _) = Orders.Apply(spelled, headings, new Dictionary<string, List<string>>());
            return order.Select(name => kinds[headings.IndexOf(name)]).ToList();
        }

        private static int Place(List<UseKind> order, UseKind kind)
        {
            int at = order.IndexOf(kind);
            return at < 0 ? 999 : at;
        }

        private sealed class Section
        {
            internal UseKind Kind;
            internal string Heading;
            internal int ToolOrder;
            internal readonly List<Use> Uses = new List<Use>();
        }
    }
}
