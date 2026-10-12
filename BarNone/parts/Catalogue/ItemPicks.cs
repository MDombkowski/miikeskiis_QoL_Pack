// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Text;

namespace QoLMods.Catalogue
{
    /// <summary>What an ItemPick names: a whole family, a member of one, one of Gear's kinds (a member's sub-heading:
    /// Arrows, Axes, Mining), or one item (later ones more specific); or one of a family's quick picks (card A81:
    /// Taxonomy.QuickPicks, "Metal Ingots"), last so the values before it never change.</summary>
    internal enum PickLevel
    {
        Family,
        Member,
        Kind,
        Item,
        Group,
    }

    /// <summary>
    /// One pick of a setup (card A41): a family ("Creature Parts"), a member of one ("Hides and Fabrics" › "Hides and
    /// Pelts"), one of Gear's kinds ("Gear" › "Ammo" › "Arrows"), one item by the game's own name ("$item_wolffang"), or
    /// one of a family's quick picks ("Metals" › "Metal Ingots", card A81). Families, members, kinds and quick picks are
    /// the catalogue's labels, as Add to Cart's Take tab shows them (Taxonomy): they are a setup's words in a chest's
    /// record, so a label renamed in Taxonomy goes into ItemPicks.Renamed, old to new.
    /// </summary>
    internal readonly struct ItemPick : IEquatable<ItemPick>
    {
        internal readonly PickLevel Level;
        internal readonly string Family;
        internal readonly string Member;
        internal readonly string Kind;

        /// <summary>For an item pick: the item's shared name ("$item_wolffang"); empty otherwise.</summary>
        internal readonly string Item;

        /// <summary>For a quick pick: its label ("Metal Ingots"); empty otherwise.</summary>
        internal readonly string Group;

        private ItemPick(PickLevel level, string family, string member, string kind, string item, string group = null)
        {
            Level = level;
            Family = family ?? string.Empty;
            Member = member ?? string.Empty;
            Kind = kind ?? string.Empty;
            Item = item ?? string.Empty;
            Group = group ?? string.Empty;
        }

        internal static ItemPick OfFamily(string family) => new ItemPick(PickLevel.Family, family, null, null, null);

        internal static ItemPick OfMember(string family, string member) => new ItemPick(PickLevel.Member, family, member, null, null);

        internal static ItemPick OfKind(string family, string member, string kind) => new ItemPick(PickLevel.Kind, family, member, kind, null);

        internal static ItemPick OfItem(string sharedName) => new ItemPick(PickLevel.Item, null, null, null, sharedName);

        /// <summary>One of a family's quick picks, by its label (Taxonomy.QuickPicks).</summary>
        internal static ItemPick OfGroup(string family, string label) => new ItemPick(PickLevel.Group, family, null, null, null, label);

        /// <summary>How specific the pick is: an item 5, a kind 4, a member 3, a family 2 (biomes alone are 1: see
        /// ItemPicks.Fit). A quick pick inside one member (Food's "Stamina Foods", in Ready to Eat) is 4, as a kind is
        /// inside its member; one across members ("Metal Ingots") is 3, as a member: so "Metal Ingots, except Iron" and
        /// "Iron, except Metal Ingots" both leave the Iron bar out (the except wins a tie).</summary>
        internal int Strength
        {
            get
            {
                switch (Level)
                {
                    case PickLevel.Family:
                        return 2;
                    case PickLevel.Member:
                        return 3;
                    case PickLevel.Kind:
                        return 4;
                    case PickLevel.Item:
                        return 5;
                    default:
                        QuickPick quick = Taxonomy.QuickPickOf(Family, Group);
                        return quick != null && quick.Member.Length > 0 ? 4 : 3;
                }
            }
        }

        /// <summary>Whether this pick names the item <paramref name="sharedName"/>, whose home is <paramref name="home"/>.</summary>
        internal bool Covers(string sharedName, ItemHome home)
        {
            if (Level == PickLevel.Item)
            {
                return string.Equals(Item, sharedName, StringComparison.Ordinal);
            }
            if (home == null || !string.Equals(home.Family, Family, StringComparison.Ordinal))
            {
                return false;
            }
            if (Level == PickLevel.Group)
            {
                return home.InGroup(Group);
            }
            if (Level >= PickLevel.Member && !string.Equals(home.Section, Member, StringComparison.Ordinal))
            {
                return false;
            }
            return Level < PickLevel.Kind || string.Equals(home.Sub, Kind, StringComparison.Ordinal);
        }

        /// <summary>The pick as its record writes it, after its sign: "f:Creature Parts", "m:Hides and Fabrics/Hides and
        /// Pelts", "k:Gear/Ammo/Arrows", "i:$item_wolffang", "g:Metals/Metal Ingots".</summary>
        internal string Token()
        {
            switch (Level)
            {
                case PickLevel.Family:
                    return "f:" + Family;
                case PickLevel.Member:
                    return "m:" + Family + "/" + Member;
                case PickLevel.Kind:
                    return "k:" + Family + "/" + Member + "/" + Kind;
                case PickLevel.Group:
                    return "g:" + Family + "/" + Group;
                default:
                    return "i:" + Item;
            }
        }

        /// <summary>A pick from its token (without its sign), or false for one this version doesn't know.</summary>
        internal static bool TryParse(string token, out ItemPick pick)
        {
            pick = default;
            if (token == null || token.Length < 3 || token[1] != ':')
            {
                return false;
            }
            string body = token.Substring(2);
            if (body.Length == 0)
            {
                return false;
            }
            switch (token[0])
            {
                case 'f':
                    if (body.IndexOf('/') >= 0)
                    {
                        return false;
                    }
                    pick = OfFamily(body);
                    return true;
                case 'm':
                {
                    string[] parts = body.Split('/');
                    if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
                    {
                        return false;
                    }
                    pick = OfMember(parts[0], parts[1]);
                    return true;
                }
                case 'k':
                {
                    string[] parts = body.Split('/');
                    if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0 || parts[2].Length == 0)
                    {
                        return false;
                    }
                    pick = OfKind(parts[0], parts[1], parts[2]);
                    return true;
                }
                case 'g':
                {
                    string[] parts = body.Split('/');
                    if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
                    {
                        return false;
                    }
                    pick = OfGroup(parts[0], parts[1]);
                    return true;
                }
                case 'i':
                    // An item's name is everything after "i:" (another mod's may hold a slash).
                    pick = OfItem(body);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>What a person reads: the family, member, kind or quick pick's label, or the item's name in his language.</summary>
        internal string Label(Func<string, string> itemName)
        {
            switch (Level)
            {
                case PickLevel.Family:
                    return Family;
                case PickLevel.Member:
                    return Member;
                case PickLevel.Kind:
                    return Kind;
                case PickLevel.Group:
                    return Group;
                default:
                    string name = itemName != null ? itemName(Item) : null;
                    return string.IsNullOrEmpty(name) ? Item : name;
            }
        }

        public bool Equals(ItemPick other) =>
            Level == other.Level && Family == other.Family && Member == other.Member && Kind == other.Kind && Item == other.Item && Group == other.Group;

        public override bool Equals(object obj) => obj is ItemPick other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Level;
                hash = hash * 397 ^ Family.GetHashCode();
                hash = hash * 397 ^ Member.GetHashCode();
                hash = hash * 397 ^ Kind.GetHashCode();
                hash = hash * 397 ^ Item.GetHashCode();
                return hash * 397 ^ Group.GetHashCode();
            }
        }

        /// <summary>The fixed order a setup's record writes its picks in: families, members, kinds, items, then quick
        /// picks; each by its token, character by character.</summary>
        internal static int Compare(ItemPick a, ItemPick b)
        {
            int order = a.Level.CompareTo(b.Level);
            return order != 0 ? order : string.CompareOrdinal(a.Token(), b.Token());
        }
    }

    /// <summary>
    /// A setup (card A41): which items something takes, by the catalogue's own classification, so it reads exactly as
    /// Add to Cart's Take tab browses. Its picks (families, members, Gear's kinds, quick picks, single items) say what it
    /// takes, its excepts take things out again ("Creature Parts except Needle"), and its biomes narrow the families,
    /// members, kinds and quick picks picked to the items from those biomes (an item's biome of origin, Biomes; "Other"
    /// for the few with none). A family, member, kind or quick pick may have biomes of its own (card A81: "Mistlands
    /// Weapons" beside Food from any biome), which narrow it in place of the setup's. With no family, member, kind or
    /// quick pick picked, the biomes alone take everything from them. A single item picked by name is taken from any
    /// biome, on top of the rest. Where a pick and an except both name an item, the more specific one decides, and an
    /// except wins a tie.
    ///
    /// It is written as one line of text (Text), always in one fixed order, so two identical setups are identical text,
    /// and read back from it (Parse), keeping any part a newer version wrote that this one doesn't know and ignoring it
    /// when matching (except that an unknown pick, "+...", counts as a family picked, so the biomes never widen to
    /// everything from them): so a later version must never add a part that changes what a setup takes without a new
    /// version word, which an older version keeps whole and never follows. Card A81 added two kinds of picks, both
    /// "+..." (a quick pick, "+g:Metals/Metal Ingots", and a pick with biomes of its own, "+n:Mistlands:m:Gear/Weapons"):
    /// an older version ignores them, so it takes less, never more. Its one new except, a quick pick left out
    /// ("-g:Food/Prepared Dishes"), would make an older version take more, so a setup holding one is written with the
    /// version word "picks2" (VersionWithGroupExcepts), which an older version keeps whole and follows in nothing; every
    /// other setup keeps "picks1". A text longer than a setup may be (MaxParts, MaxLength) is kept whole and followed in
    /// nothing too (TooLong): cut short, it would lose its biomes and excepts first, which are written last, and take
    /// more than it says. The chest service keeps a chest's setup in the chest's record
    /// (QoLMods\parts\Chests\ChestSetup.cs); StoreStash writes and follows it.
    /// </summary>
    internal sealed class ItemPicks
    {
        /// <summary>The first word of every setup's text that leaves no quick pick out; a setup starting with a "picks"
        /// word this version doesn't know was written by a newer version, which this one can't follow (Readable false).</summary>
        internal const string Version = "picks1";

        /// <summary>The first word of a setup that leaves a quick pick out (card A81): an older version, which knows only
        /// "picks1", keeps it whole and follows it in nothing, rather than taking what it leaves out.</summary>
        internal const string VersionWithGroupExcepts = "picks2";

        /// <summary>The most parts a setup may have, and its longest text: far more than any setup needs, small beside a
        /// chest's own items in its record.</summary>
        internal const int MaxParts = 256;

        internal const int MaxLength = 4000;

        /// <summary>The fit of biomes alone: weaker than any pick.</summary>
        internal const int BiomesOnlyStrength = 1;

        /// <summary>The fit of a single item picked by name: stronger than any group.</summary>
        internal const int ItemStrength = 5;

        private const char Separator = '|';

        // A pick with biomes of its own: "+n:Mistlands,Ashlands:m:Gear/Weapons" (card A81).
        private const string NarrowedSign = "n:";

        private const char BiomeSeparator = ',';

        /// <summary>Labels renamed in Taxonomy since setups were first written, each by its place, old to new: "f:Old
        /// family" to the new family's label; "m:Family/Old member" to the new member's, the family named as renamed;
        /// "k:Family/Member/Old kind" to the new kind's, the family and member named as renamed; "g:Family/Old quick pick"
        /// to the new quick pick's, the family named as renamed; "b:Old biome" to the new biome's (a pick's own biomes
        /// too). A setup names them by their labels, so one written with the old label is read with the new; by place,
        /// since a member and a kind may share a label (Nature's "Farming", and the Tools' kind "Farming"). A new label
        /// longer than the old can take a record near MaxLength past it (TooLong). Empty so far (the scratch harness pins
        /// every label, so a rename can't go unnoticed).</summary>
        private static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly List<ItemPick> takes = new List<ItemPick>();
        private readonly List<ItemPick> excepts = new List<ItemPick>();
        private readonly List<string> biomes = new List<string>();
        private readonly List<string> unknown = new List<string>();

        // The biomes of their own of the picks taken that have some (never empty, never an item's).
        private readonly Dictionary<ItemPick, List<string>> own = new Dictionary<ItemPick, List<string>>();

        /// <summary>What it takes.</summary>
        internal IReadOnlyList<ItemPick> Takes => takes;

        /// <summary>What it takes out again.</summary>
        internal IReadOnlyList<ItemPick> Excepts => excepts;

        /// <summary>The biomes it's narrowed to, as the catalogue's labels (Taxonomy.Biomes, or "Other"); none: every biome.
        /// A pick with biomes of its own follows those instead (OwnBiomesOf).</summary>
        internal IReadOnlyList<string> InBiomes => biomes;

        /// <summary>False for a setup written by a newer version, or longer than a setup may be (TooLong): it takes
        /// nothing here, and isn't changed.</summary>
        internal bool Readable { get; private set; } = true;

        /// <summary>A text longer than a setup may be (MaxParts, MaxLength), from another mod or version: kept whole,
        /// taking nothing (Readable false). Unlike a newer version's, clearing it is allowed.</summary>
        internal bool TooLong { get; private set; }

        /// <summary>Nothing picked and no biome: no setup at all (excepts alone take nothing, and aren't kept).</summary>
        internal bool IsEmpty => Readable && takes.Count == 0 && biomes.Count == 0 && unknown.Count == 0;

        /// <summary>How many parts it has (picks, excepts, biomes and parts kept from a newer version). A pick's own
        /// biomes are part of its pick.</summary>
        internal int Parts => takes.Count + excepts.Count + biomes.Count + unknown.Count;

        private string newerText;

        /// <summary>Whether a word is the first word of a setup this version can read ("picks1", "picks2").</summary>
        internal static bool IsVersion(string word) => word == Version || word == VersionWithGroupExcepts;

        /// <summary>Whether a text starts as a setup this version can read does (a line pasted from the clipboard): with one
        /// of its version words, as the clipboard's check read "picks1" before card A81 (Parse then tells a word that only
        /// starts so, "picks10", from a setup).</summary>
        internal static bool LooksLikeSetup(string text) =>
            text != null && (text.StartsWith(Version, StringComparison.Ordinal) || text.StartsWith(VersionWithGroupExcepts, StringComparison.Ordinal));

        /// <summary>A setup from its text; null for an empty text (no setup). Parts it doesn't know are kept. A text
        /// from a newer version, or longer than a setup may be, is kept whole and takes nothing (Readable false).</summary>
        internal static ItemPicks Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }
            var picks = new ItemPicks();
            string[] parts = text.Split(Separator);
            if (!IsVersion(parts[0]))
            {
                // Written by a newer version (or not a setup at all): kept whole, taking nothing, never rewritten.
                return picks.Unread(text, tooLong: false);
            }
            if (text.Length > MaxLength)
            {
                return picks.Unread(text, tooLong: true);
            }
            // Picks taken plain, following the setup's biomes ("+m:..."): one written beside the same pick with biomes of its
            // own (an older pack's change: it can't read the narrowed one, and took the group whole) takes it plain. And one
            // left out beside it ("-m:...", an older pack's change too: it left the group out) stays left out, whichever
            // comes first (review M1): this version never writes a pick both ways, so the other was an older pack's choice.
            var plain = new HashSet<ItemPick>();
            var narrowedParts = new List<(ItemPick Pick, string Part)>();
            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0)
                {
                    continue;
                }
                bool kept;
                if (part[0] == '+' && TryNarrowed(part.Substring(1), out ItemPick narrowed, out List<string> ownBiomes))
                {
                    ItemPick each = Aliased(narrowed);
                    narrowedParts.Add((each, part));
                    kept = picks.Has(each, except: true) || (picks.Add(each) && (plain.Contains(each) || picks.KeepOwn(each, ownBiomes)));
                }
                else if ((part[0] == '+' || part[0] == '-') && ItemPick.TryParse(part.Substring(1), out ItemPick pick))
                {
                    ItemPick each = Aliased(pick);
                    kept = picks.Add(each, part[0] == '-');
                    if (part[0] == '+')
                    {
                        plain.Add(each);
                        picks.own.Remove(each);
                    }
                }
                else if (part.StartsWith("b:", StringComparison.Ordinal) && part.Length > 2)
                {
                    string biome = part.Substring(2);
                    kept = picks.KeepBiome(RenamedBiome(biome));   // another version's biome too, as it is
                }
                else
                {
                    kept = picks.KeepUnknown(part);
                }
                if (!kept)
                {
                    // Past MaxParts or MaxLength: never cut short (that would drop its biomes and excepts, written last).
                    return new ItemPicks().Unread(text, tooLong: true);
                }
            }
            // A narrowed part a left-out overruled still counts as a group picked to the older pack that wrote the left-out
            // (it keeps it as a part it doesn't know): were it the only group, dropping it here would let the biomes alone
            // take everything from them, or leave left-outs alone, no setup at all. Then it stays as that pack keeps it, a
            // part read as a group and taking nothing.
            if (!picks.TakesGroups)
            {
                foreach ((ItemPick each, string part) in narrowedParts)
                {
                    if (picks.Has(each, except: true) && !picks.KeepUnknown(part))
                    {
                        return new ItemPicks().Unread(text, tooLong: true);
                    }
                }
            }
            return picks;
        }

        // A pick with biomes of its own, from its part without its sign: "n:Mistlands,Ashlands:m:Gear/Weapons". Its biomes
        // are read as they are (another version's labels too), once each; never an item's (an item has one biome). False
        // for anything else, which is then kept as a part this version doesn't know.
        private static bool TryNarrowed(string body, out ItemPick pick, out List<string> ownBiomes)
        {
            pick = default;
            ownBiomes = null;
            if (!body.StartsWith(NarrowedSign, StringComparison.Ordinal))
            {
                return false;
            }
            string rest = body.Substring(NarrowedSign.Length);
            int colon = rest.IndexOf(':');
            if (colon <= 0 || !ItemPick.TryParse(rest.Substring(colon + 1), out pick) || pick.Level == PickLevel.Item)
            {
                return false;
            }
            ownBiomes = new List<string>();
            foreach (string biome in rest.Substring(0, colon).Split(BiomeSeparator))
            {
                if (biome.Length == 0)
                {
                    return false;
                }
                string label = RenamedBiome(biome);
                if (!ownBiomes.Contains(label))
                {
                    ownBiomes.Add(label);
                }
            }
            return true;
        }

        private static string RenamedBiome(string biome) => Renamed.TryGetValue("b:" + biome, out string renamed) ? renamed : biome;

        // This one as a text it can't follow: kept whole, taking nothing, never rewritten.
        private ItemPicks Unread(string text, bool tooLong)
        {
            Readable = false;
            TooLong = tooLong;
            newerText = text;
            takes.Clear();
            excepts.Clear();
            biomes.Clear();
            unknown.Clear();
            own.Clear();
            return this;
        }

        // A part this version doesn't know, kept as it is (once); false when it doesn't fit (MaxParts, MaxLength).
        private bool KeepUnknown(string part)
        {
            if (unknown.Contains(part))
            {
                return true;
            }
            if (!Readable || Parts >= MaxParts || LengthWith(1 + part.Length) > MaxLength)
            {
                return false;
            }
            unknown.Add(part);
            return true;
        }

        // A pick's own biomes read from a record (the pick already taken), as they are, beside any it was read with
        // already (the same pick written twice); false when they don't fit.
        private bool KeepOwn(ItemPick pick, List<string> ownBiomes)
        {
            foreach (string biome in ownBiomes)
            {
                if (biome.IndexOf(Separator) >= 0 || biome.IndexOf(BiomeSeparator) >= 0 || biome.IndexOf(':') >= 0)
                {
                    return false;
                }
            }
            List<string> had = own.TryGetValue(pick, out List<string> before) ? before : null;
            var merged = had != null ? new List<string>(had) : new List<string>();
            foreach (string biome in ownBiomes)
            {
                if (!merged.Contains(biome))
                {
                    merged.Add(biome);
                }
            }
            own[pick] = merged;
            if (LengthWith(0) > MaxLength)
            {
                Restore(pick, had);
                return false;
            }
            return true;
        }

        private void Restore(ItemPick pick, List<string> had)
        {
            if (had == null)
            {
                own.Remove(pick);
            }
            else
            {
                own[pick] = had;
            }
        }

        // The length its text would have with one more part of this many characters (its separator included): every
        // part counted, its version word too, even while it's empty (when Text() is "").
        private int LengthWith(int more)
        {
            int length = VersionWord.Length + more;
            foreach (ItemPick pick in takes)
            {
                length += 1 + TakePart(pick).Length;   // "|" and "+..."
            }
            foreach (ItemPick pick in excepts)
            {
                length += 2 + pick.Token().Length;
            }
            foreach (string biome in biomes)
            {
                length += 3 + biome.Length;   // "|b:" and the label
            }
            foreach (string part in unknown)
            {
                length += 1 + part.Length;
            }
            return length;
        }

        /// <summary>Whether it picks a family, member, kind or quick pick (or a newer version picked something this one
        /// doesn't know): then its biomes narrow those, and don't take everything from them.</summary>
        internal bool TakesGroups
        {
            get
            {
                foreach (ItemPick pick in takes)
                {
                    if (pick.Level != PickLevel.Item)
                    {
                        return true;
                    }
                }
                return UnknownTakes();
            }
        }

        /// <summary>Whether a newer version picked something this one doesn't know ("+..."): it counts as a family picked,
        /// so the biomes never take everything from them beside it.</summary>
        internal bool NewerPicks => UnknownTakes();

        private bool UnknownTakes()
        {
            foreach (string part in unknown)
            {
                if (part[0] == '+')
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The version word its text starts with: "picks2" while it leaves a quick pick out (an older version
        /// would take what that leaves out), else "picks1".</summary>
        internal string VersionWord
        {
            get
            {
                foreach (ItemPick pick in excepts)
                {
                    if (pick.Level == PickLevel.Group)
                    {
                        return VersionWithGroupExcepts;
                    }
                }
                return Version;
            }
        }

        // A pick taken, as its record writes it: "+m:Gear/Weapons", or with biomes of its own
        // "+n:Mistlands,Ashlands:m:Gear/Weapons" (in the order a player meets them).
        private string TakePart(ItemPick pick)
        {
            if (!own.TryGetValue(pick, out List<string> ownBiomes) || ownBiomes.Count == 0)
            {
                return "+" + pick.Token();
            }
            var sorted = new List<string>(ownBiomes);
            sorted.Sort(CompareBiomes);
            return "+" + NarrowedSign + string.Join(BiomeSeparator.ToString(), sorted) + ":" + pick.Token();
        }

        /// <summary>The setup as its record writes it: "picks1|+m:Hides and Fabrics/Hides and Pelts|-i:$item_needle|b:Plains",
        /// in one fixed order (picks, excepts, biomes in the order a player meets them, then parts kept from a newer
        /// version); empty for an empty setup, which is no setup.</summary>
        internal string Text()
        {
            if (!Readable)
            {
                return newerText;
            }
            return IsEmpty ? string.Empty : Written(withBiomes: true);
        }

        /// <summary>The setup's text with no biomes at all, its own and its picks' (Stack Overflow's "Chests set up alike
        /// means: ignoring biomes", card A42): its version word, picks, excepts and parts kept from a newer version, in
        /// Text's order; biomes alone leave only the version word.</summary>
        internal string TextWithoutBiomes()
        {
            if (!Readable)
            {
                return newerText;
            }
            return Written(withBiomes: false);
        }

        private string Written(bool withBiomes)
        {
            var ordered = new List<string> { VersionWord };
            var sortedTakes = new List<ItemPick>(takes);
            sortedTakes.Sort(ItemPick.Compare);
            foreach (ItemPick pick in sortedTakes)
            {
                ordered.Add(withBiomes ? TakePart(pick) : "+" + pick.Token());
            }
            var sortedExcepts = new List<ItemPick>(excepts);
            sortedExcepts.Sort(ItemPick.Compare);
            foreach (ItemPick pick in sortedExcepts)
            {
                ordered.Add("-" + pick.Token());
            }
            if (withBiomes)
            {
                var sortedBiomes = new List<string>(biomes);
                sortedBiomes.Sort(CompareBiomes);
                foreach (string biome in sortedBiomes)
                {
                    ordered.Add("b:" + biome);
                }
            }
            var sortedUnknown = new List<string>(unknown);
            sortedUnknown.Sort(string.CompareOrdinal);
            ordered.AddRange(sortedUnknown);
            return string.Join(Separator.ToString(), ordered);
        }

        /// <summary>A copy, to change without changing this one.</summary>
        internal ItemPicks Copy() => Parse(Text()) ?? new ItemPicks();

        /// <summary>Whether two setups take the same things: the same text.</summary>
        internal static bool Same(ItemPicks a, ItemPicks b) => (a?.Text() ?? string.Empty) == (b?.Text() ?? string.Empty);

        /// <summary>Whether a pick is among what it takes (or, with <paramref name="except"/>, what it takes out), with or
        /// without biomes of its own.</summary>
        internal bool Has(ItemPick pick, bool except = false) => (except ? excepts : takes).Contains(pick);

        /// <summary>Adds a pick to what it takes, or with <paramref name="except"/> to what it takes out (and from the other
        /// list if it's there: an item is taken or excepted, not both; a pick left out loses any biomes of its own). False
        /// when it's full or can't be written.</summary>
        internal bool Add(ItemPick pick, bool except = false)
        {
            if (!Readable || !Writable(pick))
            {
                return false;
            }
            List<ItemPick> into = except ? excepts : takes;
            List<ItemPick> other = except ? takes : excepts;
            if (into.Contains(pick))
            {
                return true;
            }
            if (!other.Contains(pick) && (Parts >= MaxParts || LengthWith(2 + pick.Token().Length) > MaxLength))
            {
                return false;
            }
            // Moved from one list to the other: the same length, or shorter (a pick taken with biomes of its own).
            other.Remove(pick);
            own.Remove(pick);
            into.Add(pick);
            return true;
        }

        /// <summary>Takes a pick off either list (and its own biomes with it).</summary>
        internal bool Remove(ItemPick pick)
        {
            if (!Readable)
            {
                return false;
            }
            own.Remove(pick);
            return takes.Remove(pick) | excepts.Remove(pick);
        }

        /// <summary>Narrows it to one more biome: a label of Taxonomy.Biomes, or "Other" (IsBiome).</summary>
        internal bool AddBiome(string biome) => IsBiome(biome) && KeepBiome(biome);

        /// <summary>Whether a label is a biome a setup can be narrowed to: one of Taxonomy.Biomes, or "Other".</summary>
        internal static bool IsBiome(string label) => label == Taxonomy.OtherBiome || Array.IndexOf(Taxonomy.Biomes, label) >= 0;

        // A biome kept as it is: any label read from a record (another version's too), or one IsBiome allowed.
        private bool KeepBiome(string biome)
        {
            if (!Readable || string.IsNullOrEmpty(biome) || biome.IndexOf(Separator) >= 0 || biomes.Contains(biome) || Parts >= MaxParts ||
                LengthWith(3 + biome.Length) > MaxLength)
            {
                return biomes.Contains(biome);
            }
            biomes.Add(biome);
            return true;
        }

        internal bool RemoveBiome(string biome) => Readable && biomes.Remove(biome);

        /// <summary>A pick's biomes of its own (card A81), which narrow it in place of the setup's biomes; empty while it
        /// has none (it follows the setup's), or isn't taken.</summary>
        internal IReadOnlyList<string> OwnBiomesOf(ItemPick pick) =>
            own.TryGetValue(pick, out List<string> ownBiomes) ? ownBiomes : (IReadOnlyList<string>)NoBiomes;

        private static readonly string[] NoBiomes = new string[0];

        /// <summary>Whether any pick taken has biomes of its own.</summary>
        internal bool AnyOwnBiomes => own.Count > 0;

        /// <summary>Gives a pick it takes (a family, member, kind or quick pick, never a single item) one more biome of
        /// its own (IsBiome). False when it isn't taken, is an item, the biome is its own already, or the setup is full.</summary>
        internal bool AddOwnBiome(ItemPick pick, string biome)
        {
            if (!Readable || pick.Level == PickLevel.Item || !takes.Contains(pick) || !IsBiome(biome))
            {
                return false;
            }
            bool had = own.TryGetValue(pick, out List<string> ownBiomes);
            if (had && ownBiomes.Contains(biome))
            {
                return false;
            }
            // "n:" and ":" for the first, "," for each after.
            if (LengthWith(biome.Length + (had ? 1 : NarrowedSign.Length + 1)) > MaxLength)
            {
                return false;
            }
            if (!had)
            {
                own[pick] = ownBiomes = new List<string>();
            }
            ownBiomes.Add(biome);
            return true;
        }

        /// <summary>Gives a pick it takes exactly these biomes of its own (none: it follows the setup's), as a record may
        /// hold them (another version's labels too): to put a pick back as it was. False, nothing changed, when it isn't
        /// taken, is an item, a label can't be written, or they don't fit.</summary>
        internal bool SetOwnBiomes(ItemPick pick, IEnumerable<string> ownBiomes)
        {
            if (!Readable || pick.Level == PickLevel.Item || !takes.Contains(pick))
            {
                return false;
            }
            List<string> had = own.TryGetValue(pick, out List<string> before) ? before : null;
            own.Remove(pick);
            foreach (string biome in ownBiomes ?? NoBiomes)
            {
                bool clean = !string.IsNullOrEmpty(biome) && biome.IndexOf(Separator) < 0 && biome.IndexOf(BiomeSeparator) < 0 && biome.IndexOf(':') < 0;
                bool already = own.TryGetValue(pick, out List<string> now) && now.Contains(biome);
                if (!clean || (!already && !KeepOwnBiome(pick, biome)))
                {
                    Restore(pick, had);
                    return false;
                }
            }
            return true;
        }

        /// <summary>Takes one of a pick's own biomes off; with its last gone, it follows the setup's biomes again.</summary>
        internal bool RemoveOwnBiome(ItemPick pick, string biome)
        {
            if (!Readable || !own.TryGetValue(pick, out List<string> ownBiomes) || !ownBiomes.Remove(biome))
            {
                return false;
            }
            if (ownBiomes.Count == 0)
            {
                own.Remove(pick);
            }
            return true;
        }

        /// <summary>
        /// Everything in <paramref name="other"/> added to this one (pasting to add): its picks, its excepts (never one of
        /// what this one takes), its biomes and the parts a newer version wrote. A pick both take keeps biomes of its own
        /// only while both give it some (then both lists); otherwise it follows the setup's biomes. False when not all of
        /// it fitted (MaxParts, MaxLength), or either can't be read.
        /// </summary>
        internal bool AddAll(ItemPicks other)
        {
            if (other == null || !other.Readable || !Readable)
            {
                return false;
            }
            bool all = true;
            foreach (ItemPick pick in other.takes)
            {
                bool had = takes.Contains(pick);
                bool hadOwn = own.ContainsKey(pick);
                if (!Add(pick))
                {
                    all = false;
                    continue;
                }
                IReadOnlyList<string> theirs = other.OwnBiomesOf(pick);
                if (had && (!hadOwn || theirs.Count == 0))
                {
                    own.Remove(pick);   // one of them follows the setup's biomes: so does the pick
                    continue;
                }
                foreach (string biome in theirs)
                {
                    bool already = own.TryGetValue(pick, out List<string> mine) && mine.Contains(biome);
                    if (!already && !KeepOwnBiome(pick, biome))
                    {
                        all = false;
                    }
                }
            }
            foreach (ItemPick pick in other.excepts)
            {
                if (!takes.Contains(pick))
                {
                    all &= Add(pick, except: true);
                }
            }
            foreach (string biome in other.biomes)
            {
                all &= KeepBiome(biome);
            }
            foreach (string part in other.unknown)
            {
                all &= KeepUnknown(part);
            }
            return all;
        }

        // One more biome of a pick's own, as a record wrote it (another version's label too); false when it doesn't fit.
        private bool KeepOwnBiome(ItemPick pick, string biome)
        {
            bool had = own.TryGetValue(pick, out List<string> ownBiomes);
            if (LengthWith(biome.Length + (had ? 1 : NarrowedSign.Length + 1)) > MaxLength)
            {
                return false;
            }
            if (!had)
            {
                own[pick] = ownBiomes = new List<string>();
            }
            ownBiomes.Add(biome);
            return true;
        }

        // A pick read from a record, with a label renamed since it was written read as its new name.
        private static ItemPick Aliased(ItemPick pick)
        {
            if (Renamed.Count == 0 || pick.Level == PickLevel.Item)
            {
                return pick;
            }
            string family = Renamed.TryGetValue("f:" + pick.Family, out string newFamily) ? newFamily : pick.Family;
            if (pick.Level == PickLevel.Family)
            {
                return ItemPick.OfFamily(family);
            }
            if (pick.Level == PickLevel.Group)
            {
                return ItemPick.OfGroup(family, Renamed.TryGetValue("g:" + family + "/" + pick.Group, out string newGroup) ? newGroup : pick.Group);
            }
            string member = Renamed.TryGetValue("m:" + family + "/" + pick.Member, out string newMember) ? newMember : pick.Member;
            if (pick.Level == PickLevel.Member)
            {
                return ItemPick.OfMember(family, member);
            }
            string kind = Renamed.TryGetValue("k:" + family + "/" + member + "/" + pick.Kind, out string newKind) ? newKind : pick.Kind;
            return ItemPick.OfKind(family, member, kind);
        }

        // A pick whose words hold the text's separators, or are empty, can't be written in a record.
        private static bool Writable(ItemPick pick)
        {
            switch (pick.Level)
            {
                case PickLevel.Item:
                    return pick.Item.Length > 0 && pick.Item.IndexOf(Separator) < 0;
                case PickLevel.Group:
                    return Clean(pick.Family) && Clean(pick.Group);
                default:
                    return Clean(pick.Family) && (pick.Level < PickLevel.Member || Clean(pick.Member)) && (pick.Level < PickLevel.Kind || Clean(pick.Kind));
            }
        }

        private static bool Clean(string label) => label.Length > 0 && label.IndexOf(Separator) < 0 && label.IndexOf('/') < 0;

        /// <summary>
        /// How it takes an item, by the item's shared name, its home and its biome label: 0 if it doesn't take it; else
        /// how specifically, the strength of the pick that takes it (an item 5, a kind or a quick pick inside a member 4,
        /// a member or a quick pick across members 3, a family 2), or 1 for biomes alone. A setup written by a newer
        /// version takes nothing.
        /// </summary>
        internal int Fit(string sharedName, ItemHome home, string biome)
        {
            if (!Readable || IsEmpty)
            {
                return 0;
            }
            string label = string.IsNullOrEmpty(biome) ? Taxonomy.OtherBiome : biome;
            bool inBiome = biomes.Count == 0 || biomes.Contains(label);
            // A family, member, kind or quick pick takes an item only from its own biomes, else the setup's; a single item
            // picked by name, from any.
            int take = 0;
            bool groups = UnknownTakes();
            foreach (ItemPick pick in takes)
            {
                bool byName = pick.Level == PickLevel.Item;
                groups |= !byName;
                bool from = byName || (own.Count > 0 && own.TryGetValue(pick, out List<string> ownBiomes) ? ownBiomes.Contains(label) : inBiome);
                if (pick.Strength > take && from && pick.Covers(sharedName, home))
                {
                    take = pick.Strength;
                }
            }
            // With no family, member, kind or quick pick picked, the biomes alone take everything from them.
            if (take == 0 && !groups && biomes.Count > 0 && inBiome)
            {
                take = BiomesOnlyStrength;
            }
            if (take == 0)
            {
                return 0;
            }
            // Of the picks and the excepts naming it, the most specific decides; an except wins a tie.
            int except = 0;
            foreach (ItemPick pick in excepts)
            {
                if (pick.Strength > except && pick.Covers(sharedName, home))
                {
                    except = pick.Strength;
                }
            }
            return except >= take ? 0 : take;
        }

        /// <summary>How it takes an item in the game (the catalogue's home and biome for it); 0 if not.</summary>
        internal int Fit(string sharedName) =>
            string.IsNullOrEmpty(sharedName) ? 0 : Fit(sharedName, GameFacts.HomeOf(sharedName) ?? ItemHome.Unknown, Biomes.LabelOf(sharedName));

        /// <summary>
        /// What it takes, in words, the biomes right after what they narrow: "Hides and Pelts · Plains, plus Wolf Fang,
        /// except Needle"; biomes alone, "Everything · Plains"; things by name alone, "Wolf Fang, Needle"; a pick with
        /// biomes of its own after the rest of the groups, each after a semicolon, "Food · Plains; Weapons · Mistlands".
        /// At most <paramref name="most"/> families, members, kinds and quick picks are named, then "and 3 more"; the same
        /// for the things by name, and for the excepts.
        /// </summary>
        internal string Describe(Func<string, string> itemName, int most = 6)
        {
            if (!Readable)
            {
                return TooLong ? "a setup longer than this version can follow" : "a setup from a newer version of the pack";
            }
            var sortedTakes = new List<ItemPick>(takes);
            sortedTakes.Sort(ItemPick.Compare);
            var groups = sortedTakes.FindAll(pick => pick.Level != PickLevel.Item && !own.ContainsKey(pick));
            var narrowed = sortedTakes.FindAll(pick => own.ContainsKey(pick));
            var byName = sortedTakes.FindAll(pick => pick.Level == PickLevel.Item);
            var sortedExcepts = new List<ItemPick>(excepts);
            sortedExcepts.Sort(ItemPick.Compare);

            var text = new StringBuilder();
            // What the biomes narrow: the families, members, kinds and quick picks picked, or everything.
            for (int i = 0; i < groups.Count && i < most; i++)
            {
                text.Append(i == 0 ? string.Empty : ", ").Append(groups[i].Label(itemName));
            }
            if (groups.Count > most)
            {
                text.Append($" and {groups.Count - most} more");
            }
            if (UnknownTakes())
            {
                text.Append(groups.Count > 0 ? ", and what a newer version picked" : "What a newer version picked");
            }
            else if (groups.Count == 0 && narrowed.Count == 0 && biomes.Count > 0)
            {
                text.Append("Everything");
            }
            if (biomes.Count > 0 && (groups.Count > 0 || narrowed.Count == 0 || UnknownTakes()))
            {
                var sortedBiomes = new List<string>(biomes);
                sortedBiomes.Sort(CompareBiomes);
                text.Append(" · ").Append(string.Join(", ", sortedBiomes));
            }
            // Then each pick with biomes of its own.
            for (int i = 0; i < narrowed.Count && i < most; i++)
            {
                var ownBiomes = new List<string>(own[narrowed[i]]);
                ownBiomes.Sort(CompareBiomes);
                text.Append(text.Length > 0 ? "; " : string.Empty).Append(narrowed[i].Label(itemName)).Append(" · ").Append(string.Join(", ", ownBiomes));
            }
            if (narrowed.Count > most)
            {
                text.Append($"; and {narrowed.Count - most} more");
            }
            // Then the things picked by name (from any biome), then those left out, each named up to so many.
            bool head = text.Length > 0;
            for (int i = 0; i < byName.Count && i < most; i++)
            {
                text.Append(i == 0 ? (head ? ", plus " : string.Empty) : ", ").Append(byName[i].Label(itemName));
            }
            if (byName.Count > most)
            {
                text.Append($" and {byName.Count - most} more");
            }
            if (!head && byName.Count == 0)
            {
                text.Append("Nothing");
            }
            for (int i = 0; i < sortedExcepts.Count && i < most; i++)
            {
                text.Append(i == 0 ? ", except " : ", ").Append(sortedExcepts[i].Label(itemName));
            }
            if (sortedExcepts.Count > most)
            {
                text.Append($" and {sortedExcepts.Count - most} more");
            }
            return text.ToString();
        }

        /// <summary>Biomes in the order a player meets them, Other after them, anything else (another version's) by name.</summary>
        internal static int CompareBiomes(string a, string b)
        {
            int order = Taxonomy.BiomeRank(a).CompareTo(Taxonomy.BiomeRank(b));
            if (order != 0)
            {
                return order;
            }
            bool aOther = a == Taxonomy.OtherBiome;
            bool bOther = b == Taxonomy.OtherBiome;
            if (aOther != bOther)
            {
                return aOther ? -1 : 1;
            }
            return string.CompareOrdinal(a, b);
        }
    }
}
