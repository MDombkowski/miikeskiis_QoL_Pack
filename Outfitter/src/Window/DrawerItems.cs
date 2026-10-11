using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Outfitter.Equip;
using Outfitter.Loadouts;
using QoLMods.Catalogue;

namespace Outfitter.Window
{
    /// <summary>One kind of item the drawer lists: a prefab at one quality, as a loadout counts it, with how many the
    /// chests nearby and he hold.</summary>
    internal sealed class DrawerKind
    {
        /// <summary>Unique: the prefab and the quality ("SwordIron@3").</summary>
        internal string Key;

        internal string Prefab;

        internal int Quality = 1;

        /// <summary>The game's own name for it ("$item_sword_iron"): his favourites and the catalogue know it by this.</summary>
        internal string SharedName;

        /// <summary>As he reads it, "Iron Sword, level 3" for an item with levels, as Add to Cart's Take tab names it.</summary>
        internal string Name;

        /// <summary>The item has levels (gear, fish): its level is part of its name and shows in a corner above 1.</summary>
        internal bool HasLevels;

        internal int InChests;

        /// <summary>What stays in the chests by "Leave one of each item in chests" (one per chest holding it), not counted
        /// in <see cref="InChests"/>: a kind kept only so is still listed, with nothing to take (greyed).</summary>
        internal int Kept;

        /// <summary>What he carries and wears of it; 0 while the drawer doesn't list what he carries.</summary>
        internal int WithHim;

        /// <summary>The first copy found: the nearest chest's, else his.</summary>
        internal ItemDrop.ItemData Sample;

        /// <summary>His own first copy, or null.</summary>
        internal ItemDrop.ItemData Own;

        internal bool Favourite;

        /// <summary>Where it sits (its family, member, biome …) and what the filter searches: the catalogue part's.</summary>
        internal Listing Listing;

        internal int Count => InChests + WithHim;
    }

    /// <summary>One chest as the drawer reads it: its items, and which of them the equip may take from it (null: any;
    /// with "Take the loadout's items from: SetUpChestsOnly", what its setup takes).</summary>
    internal sealed class DrawerChest
    {
        internal IEnumerable<ItemDrop.ItemData> Items;

        internal Func<ItemDrop.ItemData, bool> Gives;
    }

    /// <summary>What one look at the chests nearby (and at him) found.</summary>
    internal sealed class DrawerSurvey
    {
        internal readonly List<DrawerKind> Kinds = new List<DrawerKind>();

        internal readonly Dictionary<string, DrawerKind> ByKey = new Dictionary<string, DrawerKind>();

        /// <summary>The chests counted.</summary>
        internal int Chests;

        /// <summary>What he carries is listed too ("The drawer lists what I carry").</summary>
        internal bool Carried;

        /// <summary>Every kind's key, with whether he carries some and whether it's a favourite: while this stays the
        /// same, so do the thumbnails and the list on the left, and only the counts may change.</summary>
        internal string Keys = string.Empty;

        internal DrawerKind Find(string key) => key != null && ByKey.TryGetValue(key, out DrawerKind kind) ? kind : null;
    }

    /// <summary>What the drawer shows: the pick on the left, Group and Sort, his favourites first, the filter, the
    /// families open on the left and the headings folded away. Kept for the session, per character.</summary>
    internal sealed class DrawerView
    {
        internal Selection Pick = Selection.All;
        internal string Group = Browse.Auto;
        internal string Sort = Browse.Progression;
        internal bool FavouritesFirst = true;
        internal string Query = string.Empty;
        internal readonly HashSet<string> Open = new HashSet<string>();
        internal readonly HashSet<string> Folded = new HashSet<string>();
    }

    /// <summary>One row of the list on the left: a heading that folds its rows away, or a pick with how many kinds it
    /// holds.</summary>
    internal sealed class DrawerRow
    {
        internal string Label;

        /// <summary>A heading (orange, in capitals): a click folds its rows away, or brings them back.</summary>
        internal bool Heading;

        /// <summary>A heading's rows show; a family's members show.</summary>
        internal bool Open;

        internal Selection Pick;
        internal int Count;

        /// <summary>A family's member, shown under it.</summary>
        internal bool Member;

        /// <summary>A family with members to show: its name among the open families; else null.</summary>
        internal string OpenId;

        /// <summary>The favourites' row (a star before it).</summary>
        internal bool Favourites;
    }

    /// <summary>One heading of the results and the kinds under it, in order.</summary>
    internal sealed class DrawerSection
    {
        internal string Heading;

        /// <summary>A star before the heading: his favourites.</summary>
        internal bool Star;

        internal readonly List<DrawerKind> Kinds = new List<DrawerKind>();
    }

    /// <summary>The results as the drawer draws them: the headings, the Group and Sort lists, the line above.</summary>
    internal sealed class DrawerShown
    {
        internal readonly List<DrawerSection> Sections = new List<DrawerSection>();
        internal List<GroupOption> GroupOptions;
        internal List<SortOption> SortOptions;
        internal int GroupIndex;
        internal int SortIndex;

        /// <summary>The sort in force (one no longer offered goes back to Progression).</summary>
        internal string SortId = Browse.Progression;

        /// <summary>What is shown: All, Favourites, With you, a family or a member.</summary>
        internal string Whole = string.Empty;

        internal int Total;

        /// <summary>A pick on the left narrows what shows ("Show All ×").</summary>
        internal bool Narrowed;

        /// <summary>What to say when nothing shows; null when something does.</summary>
        internal string Empty;
    }

    /// <summary>
    /// The nearby items drawer's data (job 30, Session 2), kept free of Unity's UI so the scratch harness reaches it: what
    /// the chests nearby and he hold, one kind per prefab and quality, as a loadout counts them (Capture.PrefabOf), and
    /// what the drawer shows of it, by the browsing rules of the catalogue part (Browse), as Add to Cart's Take tab
    /// shows its items: All, his favourites and what he carries, then the Take families; Group, Sort, his favourites
    /// first, and the filter. His favourites are the item marks' (StoreStash's, Left Shift + right-click), by item type.
    /// </summary>
    internal static class DrawerItems
    {
        internal const string AllLabel = "All";
        internal const string FavouritesLabel = "Favourites";
        internal const string WithYouLabel = "With you";

        /// <summary>The heading over Favourites and With you on the left.</summary>
        internal const string YoursTitle = "Yours";

        /// <summary>The sort by how many the chests and he hold (most first).</summary>
        internal const string CountSort = "count";

        internal static readonly Selection Favourites = new Selection(Pick.Favorites);

        /// <summary>What he carries: one of "his lists" to the browsing rules, the only one the drawer has.</summary>
        internal static readonly Selection WithYou = Browse.ListPick(0, WithYouLabel);

        /// <summary>His orders of the Take families and their members, as agreed (Add to Cart's defaults).</summary>
        private static readonly Orders Order = Orders.Default;

        /// <summary>A kind's listing (key, name, the game's name), made by the catalogue part as Add to Cart's Take tab
        /// makes it. The scratch harness sets another: the catalogue reads the game's item list.</summary>
        internal static Func<string, string, string, Listing> ListingOf =
            (key, name, sharedName) => Listings.Item(Shelf.Take, key, name, sharedName, null, string.Empty, 1);

        /// <summary>The listings made so far, by kind: a kind keeps its listing from one look to the next.</summary>
        private static readonly Dictionary<string, Listing> Made = new Dictionary<string, Listing>();

        /// <summary>Drops the listings made (a new world has its own catalogue).</summary>
        internal static void Forget() => Made.Clear();

        /// <summary>
        /// One look: every item in <paramref name="chests"/> (each chest's items, nearest chest first) at this world's
        /// level or above, as the chest service counts them, and everything in <paramref name="carried"/> (null: what he
        /// carries isn't listed). One kind per prefab and quality; an item whose prefab isn't known is left out, as a
        /// loadout leaves it out. <paramref name="favourite"/> says which item types are his favourites.
        /// </summary>
        internal static DrawerSurvey Read(IEnumerable<IEnumerable<ItemDrop.ItemData>> chests, IEnumerable<ItemDrop.ItemData> carried,
                                          int worldLevel, Func<string, bool> favourite) =>
            Read(chests?.Select(items => new DrawerChest { Items = items }), carried, worldLevel, favourite, leaveOne: false);

        /// <summary>
        /// As the other, counting what the equip's plan would take (review round 1 of Session 2, m6): from each chest only
        /// what its <see cref="DrawerChest.Gives"/> lets go, and with <paramref name="leaveOne"/> ("Leave one of each item in
        /// chests") one of each stackable kind left in each chest, counted in <see cref="DrawerKind.Kept"/>. A kind kept only
        /// so is still listed, with nothing to take: his words were "all the items that are in all the containers"
        /// (review round 2, j1).
        /// </summary>
        internal static DrawerSurvey Read(IEnumerable<DrawerChest> chests, IEnumerable<ItemDrop.ItemData> carried, int worldLevel,
                                          Func<string, bool> favourite, bool leaveOne)
        {
            var survey = new DrawerSurvey { Carried = carried != null };
            var byKind = new Dictionary<(string Prefab, int Quality), DrawerKind>();
            if (chests != null)
            {
                foreach (DrawerChest chest in chests)
                {
                    if (chest?.Items == null)
                    {
                        continue;
                    }
                    survey.Chests++;
                    // With "leave one", a chest's stackables are summed by kind first, so one of each stays in it.
                    Dictionary<(string, int), (ItemDrop.ItemData First, int Count)> stacks = null;
                    foreach (ItemDrop.ItemData item in chest.Items)
                    {
                        if (!Usable(item) || item.m_worldLevel < worldLevel || (chest.Gives != null && !chest.Gives(item)))
                        {
                            continue;
                        }
                        if (leaveOne && item.m_shared.m_maxStackSize > 1)
                        {
                            string prefab = Capture.PrefabOf(item);
                            if (string.IsNullOrEmpty(prefab))
                            {
                                continue;
                            }
                            (string, int) key = (prefab, Math.Max(1, item.m_quality));
                            stacks = stacks ?? new Dictionary<(string, int), (ItemDrop.ItemData, int)>();
                            stacks[key] = stacks.TryGetValue(key, out (ItemDrop.ItemData First, int Count) had)
                                ? (had.First, had.Count + item.m_stack)
                                : (item, item.m_stack);
                            continue;
                        }
                        if (KindOf(survey, byKind, item) is DrawerKind kind)
                        {
                            kind.InChests += item.m_stack;
                        }
                    }
                    if (stacks != null)
                    {
                        foreach ((ItemDrop.ItemData First, int Count) each in stacks.Values)
                        {
                            if (KindOf(survey, byKind, each.First) is DrawerKind kind)
                            {
                                kind.InChests += each.Count - 1;
                                kind.Kept++;
                            }
                        }
                    }
                }
            }
            if (carried != null)
            {
                foreach (ItemDrop.ItemData item in carried)
                {
                    if (Usable(item) && KindOf(survey, byKind, item) is DrawerKind kind)
                    {
                        kind.WithHim += item.m_stack;
                        if (kind.Own == null)
                        {
                            kind.Own = item;
                        }
                    }
                }
            }
            foreach (DrawerKind kind in survey.Kinds)
            {
                kind.Favourite = favourite != null && favourite(kind.SharedName);
                kind.Listing = ListingFor(kind);
            }
            survey.Keys = KeysOf(survey);
            return survey;
        }

        /// <summary>Asks again which kinds are his favourites (a click may have changed one), without reading the chests;
        /// true if any changed (Keys then changes too).</summary>
        internal static bool Refavour(DrawerSurvey survey, Func<string, bool> favourite)
        {
            bool changed = false;
            foreach (DrawerKind kind in survey.Kinds)
            {
                bool now = favourite != null && favourite(kind.SharedName);
                changed |= now != kind.Favourite;
                kind.Favourite = now;
            }
            if (changed)
            {
                survey.Keys = KeysOf(survey);
            }
            return changed;
        }

        private static string KeysOf(DrawerSurvey survey)
        {
            var keys = new StringBuilder();
            foreach (DrawerKind kind in survey.Kinds.OrderBy(each => each.Key, StringComparer.Ordinal))
            {
                keys.Append(kind.Key).Append(kind.WithHim > 0 ? "+" : "-").Append(kind.Favourite ? "*" : "-").Append('\n');
            }
            return keys.ToString();
        }

        private static bool Usable(ItemDrop.ItemData item) => item?.m_shared != null && item.m_stack > 0;

        private static DrawerKind KindOf(DrawerSurvey survey, Dictionary<(string, int), DrawerKind> byKind, ItemDrop.ItemData item)
        {
            string prefab = Capture.PrefabOf(item);
            if (string.IsNullOrEmpty(prefab))
            {
                return null;
            }
            int quality = Math.Max(1, item.m_quality);
            if (byKind.TryGetValue((prefab, quality), out DrawerKind kind))
            {
                return kind;
            }
            bool levels = item.m_shared.m_maxQuality > 1;
            kind = new DrawerKind
            {
                Key = prefab + "@" + quality.ToString(CultureInfo.InvariantCulture),
                Prefab = prefab,
                Quality = quality,
                SharedName = item.m_shared.m_name,
                Name = Messages.Localize(item.m_shared.m_name) + (levels ? ", level " + quality.ToString(CultureInfo.InvariantCulture) : string.Empty),
                HasLevels = levels,
                Sample = item,
            };
            byKind[(prefab, quality)] = kind;
            survey.ByKey[kind.Key] = kind;
            survey.Kinds.Add(kind);
            return kind;
        }

        private static Listing ListingFor(DrawerKind kind)
        {
            if (!Made.TryGetValue(kind.Key, out Listing listing))
            {
                listing = ListingOf(kind.Key, kind.Name, kind.SharedName);
                Made[kind.Key] = listing;
            }
            return listing;
        }

        // ---- What the drawer shows

        /// <summary>A pick that no longer exists goes back to All: With you, once what he carries isn't listed.</summary>
        internal static void Resolve(DrawerView view, DrawerSurvey survey)
        {
            if (view.Pick.Equals(WithYou) && !survey.Carried)
            {
                view.Pick = Selection.All;
            }
        }

        /// <summary>
        /// The list on the left, as Add to Cart's Take tab draws it: All; a heading over his favourites and what he carries
        /// (With you, while it's listed); then the families, each opening to its members (only what holds something,
        /// unless he's filtering). Each row counts the kinds it holds that the filter matches; a heading folded away
        /// hides its rows.
        /// </summary>
        internal static List<DrawerRow> Rail(DrawerSurvey survey, DrawerView view)
        {
            var rows = new List<DrawerRow>();
            List<Listing> listings = survey.Kinds.Where(kind => kind.Listing != null).Select(kind => kind.Listing).ToList();
            List<Listing> matching = listings.Where(listing => Browse.Matches(listing, view.Query)).ToList();
            Func<Listing, bool> starred = Starred(survey);
            Func<Listing, int, bool> withHim = WithHim(survey);
            int Count(Selection pick) => matching.Count(listing => Browse.InPick(listing, pick, starred, withHim));

            rows.Add(new DrawerRow { Label = AllLabel, Pick = Selection.All, Count = Count(Selection.All) });
            if (Title(rows, YoursTitle, view))
            {
                rows.Add(new DrawerRow { Label = FavouritesLabel, Pick = Favourites, Count = Count(Favourites), Favourites = true });
                if (survey.Carried)
                {
                    rows.Add(new DrawerRow { Label = WithYouLabel, Pick = WithYou, Count = Count(WithYou) });
                }
            }
            foreach (KeyValuePair<string, List<ShownRow>> block in Browse.Shown(Browse.Tree(Shelf.Take, listings, Order), Count, view.Query.Length > 0,
                                                                                true, view.Open))
            {
                if (!Title(rows, block.Key, view))
                {
                    continue;
                }
                foreach (ShownRow row in block.Value)
                {
                    rows.Add(new DrawerRow
                    {
                        Label = row.Row.Label,
                        Pick = row.Row.Pick,
                        Count = row.Count,
                        Member = row.Member,
                        OpenId = row.OpenId,
                        Open = row.Open,
                    });
                }
            }
            return rows;
        }

        private static bool Title(List<DrawerRow> rows, string title, DrawerView view)
        {
            bool open = !view.Folded.Contains(title);
            rows.Add(new DrawerRow { Label = title, Heading = true, Open = open });
            return open;
        }

        /// <summary>
        /// The results, as Add to Cart's Take tab makes them: what the pick on the left holds that the filter matches;
        /// his favourites first under a heading of their own (unless the pick is Favourites or With you); the rest under
        /// Group's headings (Auto follows the pick), each in Sort's order (Progression, Name, Count where counts differ,
        /// the food sorts while only food shows).
        /// </summary>
        internal static DrawerShown Results(DrawerSurvey survey, DrawerView view)
        {
            var shown = new DrawerShown();
            Func<Listing, bool> starred = Starred(survey);
            Func<Listing, int, bool> withHim = WithHim(survey);
            List<DrawerKind> listed = survey.Kinds.Where(kind => kind.Listing != null).ToList();
            List<Listing> rows = listed.Where(kind => Browse.InPick(kind.Listing, view.Pick, starred, withHim) && Browse.Matches(kind.Listing, view.Query))
                                       .Select(kind => kind.Listing).ToList();

            shown.GroupOptions = Browse.GroupOptions(Shelf.Take, view.Pick, rows, Order);
            Func<Listing, double> count = listing => survey.Find(listing.Key)?.Count ?? 0;
            // Offered only when it would change something: some of what shows differs by it (Add to Cart's rule).
            SortOption[] extra = rows.Select(count).Distinct().Skip(1).Any() ? new[] { new SortOption { Id = CountSort, Label = "Count" } } : null;
            shown.SortOptions = Browse.SortOptions(rows, extra);
            GroupOption group = shown.GroupOptions.FirstOrDefault(option => option.Id == view.Group) ?? shown.GroupOptions[0];
            shown.GroupIndex = shown.GroupOptions.IndexOf(group);
            shown.SortId = shown.SortOptions.Any(option => option.Id == view.Sort) ? view.Sort : Browse.Progression;
            shown.SortIndex = shown.SortOptions.FindIndex(option => option.Id == shown.SortId);
            Comparison<Listing> order = Browse.Sorter(shown.SortId, shown.SortId == CountSort ? count : null);
            shown.Whole = Label(view.Pick);
            shown.Narrowed = view.Pick.Pick != Pick.All;
            shown.Total = rows.Count;

            List<Listing> rest = rows;
            if (view.FavouritesFirst && view.Pick.Pick != Pick.Favorites && view.Pick.Pick != Pick.List)
            {
                List<Listing> favourites = rows.Where(starred).ToList();
                if (favourites.Count > 0)
                {
                    shown.Sections.Add(Section(survey, FavouritesLabel, Browse.Sorted(favourites, order), true));
                    rest = rows.Where(listing => !starred(listing)).ToList();
                }
            }
            foreach (KeyValuePair<string, List<Listing>> section in Browse.Sections(rest, group.Dimension, shown.Whole, order))
            {
                shown.Sections.Add(Section(survey, section.Key, section.Value, view.Pick.Pick == Pick.Favorites && group.Id == Browse.None));
            }
            if (shown.Total == 0)
            {
                shown.Empty = EmptyLine(survey, view, listed);
            }
            return shown;
        }

        private static DrawerSection Section(DrawerSurvey survey, string heading, IEnumerable<Listing> listings, bool star)
        {
            var section = new DrawerSection { Heading = heading, Star = star };
            foreach (Listing listing in listings)
            {
                DrawerKind kind = survey.Find(listing.Key);
                if (kind != null)
                {
                    section.Kinds.Add(kind);
                }
            }
            return section;
        }

        // When nothing shows: why, and where the filter would find something (Add to Cart's words, the cart's own left out).
        private static string EmptyLine(DrawerSurvey survey, DrawerView view, List<DrawerKind> listed)
        {
            if (listed.Count == 0)
            {
                return survey.Carried ? "Nothing in the chests in range, and you carry nothing." : "The chests in range hold nothing.";
            }
            bool filtering = view.Query.Length > 0;
            string text = filtering ? $"Nothing here for \"{view.Query}\"."
                : view.Pick.Equals(Favourites) ? "None of your favourites is in the chests nearby" + (survey.Carried ? " or with you." : ".")
                : view.Pick.Equals(WithYou) ? "You carry nothing."
                : "Nothing here.";
            if (filtering && view.Pick.Pick != Pick.All)
            {
                // A pick on the left narrowed the search to nothing: say what All has (Add to Cart, review 1).
                int elsewhere = listed.Count(kind => Browse.Matches(kind.Listing, view.Query));
                if (elsewhere > 0)
                {
                    text += $" All has {elsewhere} item{(elsewhere == 1 ? string.Empty : "s")} for it (Show All ×).";
                }
            }
            return text;
        }

        /// <summary>The pick's name, as the line above the results says it.</summary>
        internal static string Label(Selection pick) =>
            pick.Pick == Pick.Favorites ? FavouritesLabel : pick.Pick == Pick.List ? WithYouLabel : Browse.Label(pick, null);

        /// <summary>The line under the results while the pointer is over a kind: "Iron Sword, level 3: 2 in the chests
        /// nearby, 1 with you".</summary>
        internal static string HoverLine(DrawerKind kind, bool carried) =>
            $"{kind.Name}: {kind.InChests} in the chests nearby" +
            (kind.Kept > 0 ? $" (and {kind.Kept} kept: one stays in each chest)" : string.Empty) +
            (carried ? $", {kind.WithHim} with you" : string.Empty);

        /// <summary>Where a kind sits, as Add to Cart's hover says it for a Take item: "Gear › Weapons › Swords · Swamp".</summary>
        internal static string Describe(Listing listing)
        {
            if (listing == null)
            {
                return string.Empty;
            }
            string section = listing.Section.Length > 0 && listing.Section != listing.Biome ? $" › {listing.Section}" : string.Empty;
            string sub = listing.Sub.Length > 0 ? $" › {listing.Sub}" : string.Empty;
            return $"{listing.Family}{section}{sub} · {listing.Biome}";
        }

        /// <summary>The line saying what was looked at: "From 12 chests within 150 m, and what you carry."</summary>
        internal static string ChestsLine(int chests, int reserved, float range, bool carried) =>
            ChestsLine(chests, reserved, range, carried, leaveOne: false, setUpOnly: false);

        /// <summary>As the other, saying when one of each stays in every chest, and when only what a chest is set up for
        /// counts.</summary>
        internal static string ChestsLine(int chests, int reserved, float range, bool carried, bool leaveOne, bool setUpOnly)
        {
            string within = $"{chests} chest{(chests == 1 ? string.Empty : "s")} within {range.ToString("0", CultureInfo.InvariantCulture)} m";
            string line = (setUpOnly ? $"From what {within} {(chests == 1 ? "is" : "are")} set up for" : $"From {within}") +
                          (leaveOne ? " (one of each stays in each)" : string.Empty) +
                          (carried ? ", and what you carry." : ".");
            if (reserved > 0)
            {
                line += $" {reserved} reserved chest{(reserved == 1 ? " is" : "s are")} left alone.";
            }
            return line;
        }

        /// <summary>An amount short enough for a thumbnail's corner: 1234 as "1.2k".</summary>
        internal static string Short(int amount) =>
            amount >= 10000 ? $"{amount / 1000}k"
            : amount >= 1000 ? (amount / 100 / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "k"
            : amount.ToString(CultureInfo.InvariantCulture);

        /// <summary>With a food sort on, the number it sorts by, for a thumbnail's corner (Add to Cart's call: "a tile then
        /// shows that number"); null otherwise.</summary>
        internal static string FoodCorner(Listing listing, string sortId)
        {
            if (listing == null)
            {
                return null;
            }
            switch (sortId)
            {
                case "health":
                    return listing.Health > 0f ? Round(listing.Health) : null;
                case "stamina":
                    return listing.Stamina > 0f ? Round(listing.Stamina) : null;
                case "eitr":
                    return listing.Eitr > 0f ? Round(listing.Eitr) : null;
                case "duration":
                    return listing.Duration > 0f ? Round(listing.Duration / 60f) + "m" : null;
                default:
                    return null;
            }
        }

        // As Mathf.RoundToInt rounds (Add to Cart's corner): half to even.
        private static string Round(float value) => ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);

        private static Func<Listing, bool> Starred(DrawerSurvey survey) => listing => survey.Find(listing.Key)?.Favourite == true;

        // With you is the browsing rules' list 0: the kinds he carries some of.
        private static Func<Listing, int, bool> WithHim(DrawerSurvey survey) =>
            (listing, list) => list == WithYou.List && (survey.Find(listing.Key)?.WithHim ?? 0) > 0;
    }
}
