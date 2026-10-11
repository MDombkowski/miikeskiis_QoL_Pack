using System;
using System.Collections.Generic;
using System.Linq;

namespace SpareKeys
{
    /// <summary>What the list on the left can pick.</summary>
    internal enum PickKind
    {
        Everything,
        Free,
        Clashes,
        Shared,
        Maybe,
        Game,
        Console,
        Pack,
        Mod,
    }

    /// <summary>A pick of the list on the left: a kind, and for a mod its owner id.</summary>
    internal readonly struct Pick : IEquatable<Pick>
    {
        internal readonly PickKind Kind;
        internal readonly string Id;

        internal Pick(PickKind kind, string id = null)
        {
            Kind = kind;
            Id = id ?? string.Empty;
        }

        public bool Equals(Pick other) => Kind == other.Kind && Id == other.Id;

        public override bool Equals(object obj) => obj is Pick other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 397) ^ Id.GetHashCode();
    }

    /// <summary>A row of the list on the left: what it picks, its label, how many rows it shows (with the search).</summary>
    internal sealed class RailRow
    {
        internal Pick Pick;
        internal string Label;
        internal int Count;
    }

    /// <summary>A row of the results: a heading, a key (with the modifiers held) or one use of a key.</summary>
    internal sealed class Row
    {
        internal string Heading;
        internal KeyState State;
        internal Use Use;

        /// <summary>For a use, its own key's state with its own modifiers held: its colour.</summary>
        internal Status UseStatus;

        /// <summary>The words the search looks in, lower-cased.</summary>
        internal string Search = string.Empty;

        internal bool IsHeading => Heading != null;
    }

    /// <summary>
    /// The browsing rules (PROJECT.md rule 7, the browsing philosophy) for the key map: the list on the left picks what
    /// shows (Everything, Free Keys, Clashes, Maybe Used, The Game, Console Binds, the pack, each mod), only rows with
    /// something in them are offered, with counts that follow the search; the headings follow the pick (Everything by
    /// state, Free Keys by where they sit, The Game by where its keys work, the pack by module, a mod by its settings'
    /// sections); keyboard order inside each heading (rows from the top left), the keyboard's own "progression". Plain
    /// code, so the harness checks it.
    /// </summary>
    internal static class Browse
    {
        /// <summary>The keys Everything and Free Keys list: every key the game reads, but the gamepad's only with "Gamepad
        /// buttons" on and then only those something uses, and Number Pad = (on no common keyboard) only when used.</summary>
        internal static IEnumerable<KeyInfo> ListedKeys(KeyTable table)
        {
            foreach (KeyInfo key in KeyCatalog.All)
            {
                if (key.IsGamepad && !table.Options.Gamepad)
                {
                    continue;
                }
                bool rare = key.Id == (int)UnityEngine.KeyCode.KeypadEquals || key.IsGamepad;
                if (rare && table.UsesOf(key.Id).Count == 0)
                {
                    continue;
                }
                yield return key;
            }
        }

        /// <summary>The list on the left, with the search (and a key clicked on the picture) applied to its counts; rows
        /// with nothing in them left out (Everything and Free Keys always stay).</summary>
        internal static List<RailRow> Rail(KeyTable table, int[] layer, string query, int onlyKey = -1)
        {
            var rail = new List<RailRow>();
            void Add(Pick pick, string label, bool always = false)
            {
                int count = Rows(pick, table, layer, query, onlyKey).Count(row => !row.IsHeading);
                if (count > 0 || always)
                {
                    rail.Add(new RailRow { Pick = pick, Label = label, Count = count });
                }
            }
            Add(new Pick(PickKind.Everything), "Everything", always: true);
            Add(new Pick(PickKind.Free), "Free Keys", always: true);
            Add(new Pick(PickKind.Clashes), "Clashes");
            Add(new Pick(PickKind.Shared), "Shared Keys");
            Add(new Pick(PickKind.Maybe), "Maybe Used");
            Add(new Pick(PickKind.Game), "The Game");
            Add(new Pick(PickKind.Console), "Console Binds");
            Add(new Pick(PickKind.Pack), "miikeskii's QoL Pack");
            foreach (IGrouping<string, Use> mod in table.Uses.Where(use => !IsBuiltIn(use.Group)).GroupBy(use => use.Group)
                                                           .OrderBy(group => group.First().Owner, StringComparer.OrdinalIgnoreCase))
            {
                Add(new Pick(PickKind.Mod, mod.Key), mod.First().Owner);
            }
            return rail;
        }

        private static bool IsBuiltIn(string group) => group == Use.GameOwner || group == Use.ConsoleOwner || group == Use.PackGroup;

        /// <summary>The rows a pick shows with these modifiers held, the search applied (a heading only over rows it keeps);
        /// <paramref name="onlyKey"/>: only the rows of the key clicked on the picture (-1: every key).</summary>
        internal static List<Row> Rows(Pick pick, KeyTable table, int[] layer, string query, int onlyKey = -1)
        {
            layer = layer ?? Combo.NoMods;
            List<KeyValuePair<string, List<Row>>> groups;
            switch (pick.Kind)
            {
                case PickKind.Everything:
                    groups = ByState(ListedKeys(table).Select(key => table.StateOf(key.Id, layer)));
                    break;
                case PickKind.Free:
                    groups = ByRegion(ListedKeys(table).Select(key => table.StateOf(key.Id, layer)).Where(state => KeyTable.IsFree(state.Status)));
                    break;
                case PickKind.Clashes:
                    groups = table.Clashes().GroupBy(state => Combo.LayerText(state.Layer))
                                  .Select(group => Pair("With " + Title(group.Key), group.Select(KeyRow))).ToList();
                    break;
                case PickKind.Shared:
                    groups = table.SharedKeys().GroupBy(state => Combo.LayerText(state.Layer))
                                  .Select(group => Pair("With " + Title(group.Key), group.Select(KeyRow))).ToList();
                    break;
                case PickKind.Maybe:
                    groups = UseGroups(table, table.Uses.Where(use => table.Options.IsMaybe(use)), use => use.Owner);
                    break;
                case PickKind.Game:
                    groups = UseGroups(table, table.Uses.Where(use => use.IsGame), PlaceHeading, PlaceRank);
                    break;
                case PickKind.Console:
                    groups = UseGroups(table, table.Uses.Where(use => use.Group == Use.ConsoleOwner), _ => string.Empty);
                    break;
                case PickKind.Pack:
                    groups = UseGroups(table, table.Uses.Where(use => use.Group == Use.PackGroup), use => use.Owner, heading => heading);
                    break;
                default:
                    groups = UseGroups(table, table.Uses.Where(use => use.Group == pick.Id), use => SectionHeading(use), null);
                    break;
            }
            string[] words = Words(query);
            var rows = new List<Row>();
            foreach (KeyValuePair<string, List<Row>> group in groups)
            {
                List<Row> kept = group.Value.Where(row => Matches(row, words) && (onlyKey < 0 || KeyOf(row) == onlyKey)).ToList();
                if (kept.Count == 0)
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(group.Key))
                {
                    rows.Add(new Row { Heading = group.Key });
                }
                rows.AddRange(kept);
            }
            return rows;
        }

        private static KeyValuePair<string, List<Row>> Pair(string heading, IEnumerable<Row> rows) =>
            new KeyValuePair<string, List<Row>>(heading, rows.ToList());

        private static readonly Status[] StateOrder =
        {
            Status.Clash, Status.MaybeClash, Status.Shared, Status.Used, Status.Maybe, Status.WithModifier, Status.Free, Status.NotOffered,
        };

        private static List<KeyValuePair<string, List<Row>>> ByState(IEnumerable<KeyState> states)
        {
            List<KeyState> all = states.ToList();
            return StateOrder.Select(status => Pair(StateHeading(status), all.Where(state => state.Status == status).Select(KeyRow)))
                             .Where(pair => pair.Value.Count > 0).ToList();
        }

        private static List<KeyValuePair<string, List<Row>>> ByRegion(IEnumerable<KeyState> states) =>
            states.GroupBy(state => KeyCatalog.Get(state.Key).Region).OrderBy(group => group.Key)
                  .Select(group => Pair(RegionHeading(group.Key), group.Select(KeyRow))).ToList();

        private static List<KeyValuePair<string, List<Row>>> UseGroups(KeyTable table, IEnumerable<Use> uses, Func<Use, string> heading,
                                                                        Func<string, IComparable> headingOrder = null)
        {
            // Headings in the order given (or their first appearance), keyboard order inside each.
            IEnumerable<IGrouping<string, Use>> groups = uses.GroupBy(heading);
            if (headingOrder != null)
            {
                groups = groups.OrderBy(group => headingOrder(group.Key));
            }
            return groups.Select(group => Pair(group.Key, group.OrderBy(use => KeyTable.Order(use.Main)).ThenBy(use => use.Mods.Length)
                                                               .Select(use => UseRow(table, use)))).ToList();
        }

        internal static Row KeyRow(KeyState state)
        {
            var row = new Row { State = state };
            row.Search = string.Join(" ", new[] { KeyCatalog.NameOf(state.Key), KeyCatalog.Get(state.Key)?.Cap ?? string.Empty, StateWord(state.Status) }
                .Concat(state.Fires.Concat(state.Maybe).Concat(state.Elsewhere).Select(UseWords))).ToLowerInvariant();
            return row;
        }

        internal static Row UseRow(KeyTable table, Use use)
        {
            return new Row
            {
                Use = use,
                UseStatus = table.StateOf(use.Main, use.Mods).Status,
                Search = (use.Keys + " " + KeyCatalog.Get(use.Main)?.Cap + " " + UseWords(use)).ToLowerInvariant(),
            };
        }

        private static string UseWords(Use use) => string.Join(" ", use.Owner, use.What, use.Section, use.When, use.Detail, use.Keys);

        internal static string[] Words(string query) =>
            (query ?? string.Empty).ToLowerInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        private static bool Matches(Row row, string[] words) => words.All(word => row.Search.Contains(word));

        /// <summary>The key a row is about: its key, or its use's main key.</summary>
        internal static int KeyOf(Row row) => row.State != null ? row.State.Key : row.Use != null ? row.Use.Main : -1;

        // ---- Words ----

        // A user counted with more modifiers held than its own: one known to read its key loosely (the game's keys, a
        // KeyCode setting, the pack's own keys, console binds) still fires; another mod's shortcut may (BepInEx's own check
        // would refuse the extra key, a mod's own check may not).
        private static string Carried(Use use) => use.Loose ? $" (its {use.Keys} still fires)" : $" (its {use.Keys} may still fire)";

        /// <summary>
        /// Whether a key picked for this setting would also fire under the modifiers that make a key blue ("free alone,
        /// taken with a modifier"): true for a KeyCode setting and for the pack's own keys (both read their key loosely),
        /// so the panel beside F1 says blue isn't safe for it, as the Controls panel says for the game's keys.
        /// </summary>
        internal static bool BlueFiresToo(bool keyCodeSetting, string pluginId) =>
            keyCodeSetting || (pluginId != null && pluginId.StartsWith("modprojects.", StringComparison.OrdinalIgnoreCase));

        /// <summary>A state in a word or two, for the list's narrow column ("free alone").</summary>
        internal static string ShortWord(Status status)
        {
            switch (status)
            {
                case Status.Free:
                    return "free";
                case Status.WithModifier:
                    return "free alone";
                case Status.Used:
                    return "used";
                case Status.Shared:
                    return "shared";
                case Status.Clash:
                    return "a clash";
                case Status.Maybe:
                    return "maybe used";
                case Status.MaybeClash:
                    return "maybe clash";
                case Status.NotOffered:
                    return "not offered";
                default:
                    return "held";
            }
        }

        internal static string StateWord(Status status)
        {
            switch (status)
            {
                case Status.Free:
                    return "free";
                case Status.WithModifier:
                    return "free here, used with other modifiers held";
                case Status.Used:
                    return "used";
                case Status.Shared:
                    return "shared: each works only where the others never do, so one press never does two things";
                case Status.Clash:
                    return "a clash";
                case Status.Maybe:
                    return "maybe used";
                case Status.MaybeClash:
                    return "maybe a clash";
                case Status.NotOffered:
                    return "not offered";
                default:
                    return "held";
            }
        }

        internal static string StateHeading(Status status)
        {
            switch (status)
            {
                case Status.Clash:
                    return "Clashes";
                case Status.MaybeClash:
                    return "Maybe Clashes";
                case Status.Shared:
                    return "Shared";
                case Status.Used:
                    return "Used";
                case Status.Maybe:
                    return "Maybe Used";
                case Status.WithModifier:
                    return "Free, Used With Other Modifiers";
                case Status.Free:
                    return "Free";
                default:
                    return "Not Offered";
            }
        }

        internal static string RegionHeading(Region region)
        {
            switch (region)
            {
                case Region.Function:
                    return "Function Keys";
                case Region.Digits:
                    return "Digits";
                case Region.Letters:
                    return "Letters";
                case Region.Symbols:
                    return "Symbols";
                case Region.Editing:
                    return "Space, Enter and Their Neighbours";
                case Region.Modifiers:
                    return "Shift, Ctrl and Alt";
                case Region.Navigation:
                    return "Navigation and Arrows";
                case Region.NumberPad:
                    return "Number Pad";
                case Region.Mouse:
                    return "Mouse";
                case Region.Gamepad:
                    return "Gamepad";
                default:
                    return "Others";
            }
        }

        private static readonly string[] PlaceHeadings =
        {
            "In the World", "Build Mode", "Inventory and Chests", "The Map", "Anywhere", "In a Window", "Menus", "While Typing", "Debug Mode",
        };

        /// <summary>The Game's heading for one of its keys: by its first screen, build mode for one that needs a build
        /// tool in hand, debug mode for a debug-mode key.</summary>
        internal static string PlaceHeading(Use use)
        {
            if (use.DebugMode)
            {
                return PlaceHeadings[8];
            }
            Place place = use.Place;
            switch (place.Where)
            {
                case Where.World:
                    return Whens.NeedsBuildTool(place.Hand) ? PlaceHeadings[1] : PlaceHeadings[0];
                case Where.Inventory:
                    return PlaceHeadings[2];
                case Where.Map:
                    return PlaceHeadings[3];
                case Where.Window:
                    return PlaceHeadings[5];
                case Where.Menu:
                    return PlaceHeadings[6];
                case Where.Typing:
                    return PlaceHeadings[7];
                default:
                    return PlaceHeadings[4];
            }
        }

        private static IComparable PlaceRank(string heading) => Array.IndexOf(PlaceHeadings, heading);

        private static string SectionHeading(Use use) =>
            string.IsNullOrEmpty(use.Section) ? "Its Keys" : use.Section;

        /// <summary>"nothing held" as a heading's words: "Nothing Held", "Left Alt Held".</summary>
        internal static string Title(string words)
        {
            var parts = words.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0 && parts[i] != "+" && parts[i] != "and")
                {
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
                }
            }
            return string.Join(" ", parts);
        }

        /// <summary>
        /// A key's users in a line: who fires it with these modifiers held, and who uses it with other modifiers or only
        /// holds it, each with its when (unless <paramref name="whens"/> is off). "F8: Plant Easily: Enable mod key (in the
        /// world) · Cartur's Map Pins: Auto pinning key (in the world)".
        /// </summary>
        internal static string Users(KeyState state, int most = 6, bool whens = true, Use self = null)
        {
            var parts = new List<string>();
            foreach (Use use in state.Fires.Where(use => !state.IsShadowed(use)))
            {
                string how = !state.IsCarried(use) ? string.Empty
                    : KeyTable.GivesWayToSelf(state, use, self) ? (KeyTable.IsGamesUse(use) ? " (not where this takes it)" : " (gives way to this one)")
                    : TakenOver(state, use) ? $" (its {use.Keys} still fires, but not where these keys take it)"
                    : Carried(use);
                parts.Add(UseText(use, whens) + how);
            }
            foreach (Use use in state.Maybe)
            {
                parts.Add(UseText(use, whens) + " (maybe)");
            }
            if (parts.Count == 0 && state.Elsewhere.Count > 0)
            {
                parts.AddRange(state.Elsewhere.Select(use => $"{use.Keys}: {UseText(use, whens)}"));
            }
            if (parts.Count > most)
            {
                int more = parts.Count - most;
                parts = parts.Take(most).ToList();
                parts.Add($"and {more} more");
            }
            return string.Join(" · ", parts);
        }

        // The game's Use carried under a pack key that takes it over at its own things (Reserve Stock's Left Alt + E).
        private static bool TakenOver(KeyState state, Use use) => KeyTable.IsGamesUse(use) && state.Fires.Any(other => other.OverGame);

        /// <summary>
        /// Why a key is a clash or maybe a clash, in a sentence (empty for any other state): the two users judged and their
        /// whens. "Reserve Stock's Key and Topper-Upper's Light key can both work at once (at a chest; anywhere), so one
        /// press does both."
        /// </summary>
        internal static string Why(KeyState state)
        {
            Use[] pair = state.Pair;
            if (pair == null)
            {
                return string.Empty;
            }
            switch (state.Status)
            {
                case Status.Clash:
                    return Place.Overlap(pair[0].Place, pair[1].Place)
                        ? $"{Name(pair[0])} and {Name(pair[1])} can work at the same moment: one press does both" +
                          (pair[0].Known && pair[1].Known ? "." : $" ({Name(!pair[0].Known ? pair[0] : pair[1])}: when not known, so anywhere).")
                        : $"{Name(pair[0])} and {Name(pair[1])} never work at the same moment, but shared keys are set to count as clashes.";
                case Status.MaybeClash:
                    Use unknown = !pair[0].Known ? pair[0] : pair[1];
                    return $"Maybe a clash: when {Name(unknown)} works isn't known (tell Spare Keys in \"Whens for other mods' keys\").";
                default:
                    return string.Empty;
            }
        }

        private static string Name(Use use) =>
            string.IsNullOrEmpty(use.What) ? use.Owner : use.Owner + (use.Owner.EndsWith("s", StringComparison.Ordinal) ? "' " : "'s ") + use.What;

        private static string WhenOf(Use use) => !use.Known ? "when not known, so anywhere" : string.IsNullOrEmpty(use.When) ? use.Place.Words : use.When;

        /// <summary>
        /// A key under the pointer, in a line or two: its state with the modifiers held, who uses it (each with its when,
        /// unless <paramref name="whens"/> is off), why it clashes, and (for a key in use, with <paramref name="suggest"/>)
        /// up to three free keys to try instead.
        /// "F8, nothing held: a clash. Plant Easily: Enable mod key (in the world) · … Try instead: F4, F3, F12."
        /// </summary>
        internal static string Describe(KeyState state, KeyTable table, Layout layout, bool suggest, bool whens = true)
        {
            string name = Combo.Describe(state.Key, Combo.NoMods);
            string held = Combo.LayerText(state.Layer);
            switch (state.Status)
            {
                case Status.NotOffered:
                    return $"{name}: not offered ({state.Reason}).";
                case Status.Held:
                    IReadOnlyList<Use> own = table.UsesOf(state.Key);
                    return $"{name}: held now." + (own.Count > 0 ? " On its own: " + string.Join(" · ", own.Take(6).Select(use => UseText(use, whens))) + "." : string.Empty);
            }
            string text = $"{name}, {held}: {StateWord(state.Status)}.";
            string users = Users(state, 6, whens);
            if (users.Length > 0)
            {
                text += " " + users + ".";
            }
            string why = whens ? Why(state) : string.Empty;
            if (why.Length > 0)
            {
                text += " " + why;
            }
            if (state.Fires.Count > 0 && state.Elsewhere.Count > 0)
            {
                text += " With other modifiers: " + string.Join(" · ", state.Elsewhere.Take(4).Select(use => $"{use.Keys}: {UseText(use, whens)}")) + ".";
            }
            if (suggest && state.Status != Status.Free && state.Status != Status.WithModifier)
            {
                List<Combo> instead = table.Suggest(state.Key, state.Layer, layout);
                if (instead.Count > 0)
                {
                    text += " Try instead: " + string.Join(", ", instead.Select(combo => combo.Text)) + ".";
                }
            }
            return text;
        }

        /// <summary>
        /// A key under the pointer beside a "press a key", judged for the setting being picked (KeyTable.StateFor): taken
        /// (who works at the same moment), free for it (used only where it never works), maybe taken, or as Describe says.
        /// "J, Left Alt held: taken: StoreStash's Setup key works at the same moment (at a chest)."
        /// </summary>
        internal static string DescribeFor(KeyState state, Use self, KeyTable table, Layout layout, bool suggest, bool whens = true)
        {
            bool gaveWay = state.Fires.Count > 0 && (state.Status == Status.Free || state.Status == Status.WithModifier || state.Status == Status.Maybe);
            if (self == null || (state.Pair == null && state.Status != Status.Shared && !gaveWay))
            {
                return Describe(state, table, layout, suggest, whens);
            }
            string name = Combo.Describe(state.Key, Combo.NoMods);
            string held = Combo.LayerText(state.Layer);
            string users = Users(state, 6, whens, self);
            if (gaveWay)
            {
                return $"{name}, {held}: free for this: what fires here gives way to it. {users}.";
            }
            switch (state.Status)
            {
                case Status.Shared:
                    return $"{name}, {held}: free for this: used, but only where this never works. {users}.";
                case Status.MaybeClash:
                    Use unknown = !state.Pair[1].Known ? state.Pair[1] : state.Pair[0];
                    return $"{name}, {held}: maybe taken: when {Name(unknown)} works isn't known. {users}.";
                default:
                    Use other = state.Pair[1];
                    string text = $"{name}, {held}: taken: {Name(other)} works at the same moment" + (whens ? $" ({WhenOf(other)})." : ".");
                    if (suggest)
                    {
                        List<Combo> instead = table.Suggest(state.Key, state.Layer, layout);
                        if (instead.Count > 0)
                        {
                            text += " Try instead: " + string.Join(", ", instead.Select(combo => combo.Text)) + ".";
                        }
                    }
                    return text;
            }
        }

        /// <summary>One user in words: "Reserve Stock: Key (at a chest)"; without its when when <paramref name="whens"/>
        /// is off.</summary>
        internal static string UseText(Use use, bool whens = true) =>
            use.Who + (!whens || string.IsNullOrEmpty(use.When) ? (use.Held ? " (held with something else)" : string.Empty) : $" ({use.When})");
    }
}
