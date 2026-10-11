using System;
using System.Collections.Generic;

namespace StoreStash
{
    /// <summary>Which chest of a group keeps a kind of thing when a tidy gathers it (card A42's setting). A kind already all
    /// in one chest that takes it stays there while that chest has room for it, whatever the rule: the rule picks a chest
    /// only for a kind spread over several (so a press again, or a press from somewhere else, never moves it back).</summary>
    internal enum Keeper
    {
        /// <summary>Of the chests that can hold all of it, the one already holding the most of it (as the quick stack's
        /// "...the one holding the most of it" counts: every unit of it, whatever its quality), then the bigger, then the
        /// nearer.</summary>
        HoldingMost,

        /// <summary>Of the chests that can hold all of it, the one with the most cells, then the one holding more of it, then
        /// the nearer.</summary>
        Biggest,

        /// <summary>Of the chests that can hold all of it, the one nearest to him as he tidies.</summary>
        Nearest,
    }

    /// <summary>
    /// Stack Overflow's tidy, planned from counts alone (card A42, runbook job 27): which stacks move between chests set up
    /// alike, so each kind of thing sits in as few of them as possible. It knows nothing of Unity, so the scratch harness
    /// checks it on its own and against the chest service. Each move it weighs is first worked out from counts alone, as the
    /// chest service's move would carry it out (ChestMove and ChestPut.Move: the source's stacks of the item in the take
    /// order, each into the target as the game's adding puts it, as much as there is room for: Preview), and each move it
    /// takes is carried out on its own copy of the chests exactly so (Apply), before the next is chosen, so the plan never
    /// counts on a move that won't happen, and what it says moved is what will. A plan that runs its course never leaves a
    /// kind in more chests than it started in: if it would, it ends at its last point where none was (Outcome.RolledBack).
    /// One stopped by the most stacks a tidy moves keeps what it did, and a press again goes on from there.
    /// </summary>
    internal static class TidyPlan
    {
        /// <summary>One stack in a chest, as the plan sees it.</summary>
        internal sealed class Stack
        {
            /// <summary>The item's shared name ("$item_leatherscraps"): a kind of thing is all of one name, any quality.</summary>
            internal string Name;

            internal int Quality = 1;

            internal int WorldLevel;

            internal int Amount;

            /// <summary>The item's stack size (1 for gear, which never shares a cell).</summary>
            internal int MaxStack = 1;

            /// <summary>Its cell: the take order goes by it last.</summary>
            internal int X;

            internal int Y;

            /// <summary>Where the game's adding puts a new stack of it: from the top row (weapons, tools, shields, utility,
            /// misc, trinkets: Inventory.TopFirst) or from the bottom row.</summary>
            internal bool TopFirst;

            /// <summary>The stack in his hand (dragged, or being split): never moved, as the chest service never moves it.</summary>
            internal bool Pinned;

            internal Stack Copy() => (Stack)MemberwiseClone();

            /// <summary>Whether the game stacks these two together: the same name, quality and world level.</summary>
            internal bool Alike(Stack other) => Name == other.Name && Quality == other.Quality && WorldLevel == other.WorldLevel;
        }

        /// <summary>One chest of a group, as the plan sees it: its grid, its stacks in the game's own list order, and what its
        /// setup takes.</summary>
        internal sealed class Chest
        {
            internal int Width;

            internal int Height;

            /// <summary>Its stacks, in the inventory's own list order (the game tops up the first part-stack it finds in it).</summary>
            internal readonly List<Stack> Stacks = new List<Stack>();

            /// <summary>Whether its setup takes a thing, by name; null: it takes nothing (no setup the plan can read).</summary>
            internal Func<string, bool> Takes;

            internal int Cells => Math.Max(0, Width * Height);

            /// <summary>Its empty cells, as the game counts them (Inventory.GetEmptySlots: its cells less its stacks).</summary>
            internal int EmptyCells => Math.Max(0, Width * Height - Stacks.Count);

            internal Chest Copy()
            {
                var copy = new Chest { Width = Width, Height = Height, Takes = Takes };
                foreach (Stack stack in Stacks)
                {
                    copy.Stacks.Add(stack.Copy());
                }
                return copy;
            }
        }

        /// <summary>How a tidy tidies: the settings it reads, each time it plans.</summary>
        internal sealed class Rules
        {
            internal Keeper Keeper = Keeper.HoldingMost;

            /// <summary>Other things may be moved out of a chest to make room ("Move other things out of the way"). Off: a kind
            /// moves only into the free room of a chest the keeper rule prefers.</summary>
            internal bool MakeRoom = true;

            /// <summary>Part-stacks of one thing in two of its chests are topped up into the one that keeps it, to free a cell
            /// ("Merge part-stacks between chests").</summary>
            internal bool Merge = true;

            /// <summary>Cells a tidy leaves free in each chest ("Leave free cells in each chest"), where the room allows.</summary>
            internal int LeaveFree;

            /// <summary>The cells left free are kept while the group has room for them, a kind split over more chests if it
            /// must ("Keep free cells even if a kind splits"); off, a kind is never split to keep them: it goes into a chest
            /// that keeps them if one can hold all of it, and uses them before it's split.</summary>
            internal bool FreeFirst;

            /// <summary>Things no setup in the group takes are kept together among the group's chests too; off, they stay where
            /// they are ("Things no setup in the group takes").</summary>
            internal bool Strays;

            /// <summary>The most stacks a tidy moves ("Most stacks moved per tidy"); then it stops, and a press again goes on.</summary>
            internal int MostStacks = 500;
        }

        /// <summary>One move, in the order to carry it out: up to Amount of a thing, by its name (any quality, every world
        /// level: the chest service's key), from one chest to another (by their places in the list the plan was given), into
        /// the target's part-stacks only when TopUpOnly.</summary>
        internal sealed class Order
        {
            internal int From;
            internal int To;
            internal string Name;
            internal int Amount;
            internal bool TopUpOnly;
        }

        /// <summary>What a plan does, worked out as it was made.</summary>
        internal sealed class Outcome
        {
            internal readonly List<Order> Orders = new List<Order>();

            /// <summary>Stacks moved: each stack an order takes anything from counts once for that order.</summary>
            internal int Stacks;

            /// <summary>Items moved, in all.</summary>
            internal int Items;

            /// <summary>The chests an order takes from or puts into, by their places.</summary>
            internal readonly SortedSet<int> Touched = new SortedSet<int>();

            /// <summary>What went into each chest, by its place, then the thing's name.</summary>
            internal readonly SortedDictionary<int, SortedDictionary<string, int>> Into = new SortedDictionary<int, SortedDictionary<string, int>>();

            /// <summary>A move was cut short, or left out, because the most stacks a tidy moves was reached: a press again goes
            /// on.</summary>
            internal bool OutOfMoves;

            /// <summary>The plan, run its course, would have left a kind in more chests than it started in, so it ends at its
            /// last point where none was (the moves after it dropped).</summary>
            internal bool RolledBack;

            /// <summary>Kinds the plan tidies that are still in more than one chest when it ends (a kind too big for one chest
            /// counts only past the fewest it needs).</summary>
            internal int StillSpread;

            /// <summary>Kinds still in a chest that isn't theirs when it ends (with "Move other things out of the way" on): the
            /// chests were too full to move them.</summary>
            internal int OutOfPlace;

            /// <summary>Kinds still in a chest whose setup doesn't take them when it ends (with "ignoring biomes" a chest of the
            /// group may hold what its own setup doesn't take): there was no room where they belong. He's told (review round
            /// 1, major 2). A kind still spread too is counted only in StillSpread, so it's told once.</summary>
            internal int Misplaced;

            /// <summary>The chests after the plan, as the plan carried its orders out: for the harness, and for a plan again.</summary>
            internal List<Chest> After;
        }

        /// <summary>What one order did, or would do: the items moved, the source's stacks it took from, and those it took
        /// whole (each a cell freed in the source).</summary>
        internal struct Moved
        {
            internal int Items;
            internal int Stacks;
            internal int Emptied;
        }

        /// <summary>
        /// The game's order of taking a thing's stacks from a chest, as the chest service's move takes them
        /// (ChestStock.TakeOrder): the lower quality first, then the smaller stack, the lower world level, the first cell.
        /// </summary>
        internal static int TakeOrder(Stack a, Stack b)
        {
            if (a.Quality != b.Quality)
            {
                return a.Quality.CompareTo(b.Quality);
            }
            if (a.Amount != b.Amount)
            {
                return a.Amount.CompareTo(b.Amount);
            }
            if (a.WorldLevel != b.WorldLevel)
            {
                return a.WorldLevel.CompareTo(b.WorldLevel);
            }
            return a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X);
        }

        /// <summary>The room a chest's part-stacks of a thing have left (ChestPut.FreeStackSpace): none for a thing that
        /// doesn't stack.</summary>
        internal static int FreeStackSpace(Chest chest, Stack like)
        {
            if (like.MaxStack <= 1)
            {
                return 0;
            }
            int room = 0;
            foreach (Stack stack in chest.Stacks)
            {
                if (stack.Alike(like) && stack.Amount < stack.MaxStack)
                {
                    room += stack.MaxStack - stack.Amount;
                }
            }
            return room;
        }

        /// <summary>
        /// Carries one order out on the plan's copy of two chests, exactly as the chest service's move does (ChestMove.Moved
        /// and ChestPut.Move): the source's stacks of the thing (any quality, never the one in his hand), sorted once in the
        /// take order; from each, as much as is left to move and as the target has room for (its part-stacks of the same
        /// item, and with TopUpOnly off a full stack in each empty cell); added as the game adds (Inventory.AddItem: the first
        /// part-stacks in the list topped up, the rest as a new stack in the first empty cell from the top or the bottom),
        /// then taken off the source's stack (gone from the list when it's all gone). A stack with no room goes on to the
        /// next. Returns what moved.
        /// </summary>
        internal static Moved Apply(Chest from, Chest to, string name, int amount, bool topUpOnly)
        {
            var moved = new Moved();
            if (from == null || to == null || ReferenceEquals(from, to) || string.IsNullOrEmpty(name) || amount <= 0)
            {
                return moved;
            }
            List<Stack> stacks = InTakeOrder(from, name);
            int left = amount;
            foreach (Stack stack in stacks)
            {
                if (left <= 0)
                {
                    break;
                }
                int part = Transfer(from, to, stack, Math.Min(left, stack.Amount), topUpOnly);
                if (part > 0)
                {
                    left -= part;
                    moved.Items += part;
                    moved.Stacks++;
                    moved.Emptied += stack.Amount <= 0 ? 1 : 0;
                }
            }
            return moved;
        }

        // A chest's stacks of a thing that may move (never the one in his hand), in the take order.
        private static List<Stack> InTakeOrder(Chest chest, string name)
        {
            var stacks = new List<Stack>();
            foreach (Stack stack in chest.Stacks)
            {
                if (stack.Name == name && !stack.Pinned)
                {
                    stacks.Add(stack);
                }
            }
            stacks.Sort(TakeOrder);
            return stacks;
        }

        // One stack's share of a move (ChestPut.Transfer): no more than fits, into the target as the game adds, then out of
        // the source.
        private static int Transfer(Chest from, Chest to, Stack stack, int want, bool topUpOnly)
        {
            int part = Math.Min(want, stack.Amount);
            int room = FreeStackSpace(to, stack) + (topUpOnly ? 0 : to.EmptyCells * Math.Max(1, stack.MaxStack));
            part = Math.Min(part, room);
            if (part <= 0)
            {
                return 0;
            }
            int rest = part;
            if (stack.MaxStack > 1)
            {
                // Inventory.AddItem: one at a time onto the first part-stack of the same item in the list, which comes to the
                // same as filling each in the list's order.
                foreach (Stack into in to.Stacks)
                {
                    if (rest <= 0)
                    {
                        break;
                    }
                    if (into.Alike(stack) && into.Amount < into.MaxStack)
                    {
                        int add = Math.Min(rest, into.MaxStack - into.Amount);
                        into.Amount += add;
                        rest -= add;
                    }
                }
            }
            if (rest > 0)
            {
                // The rest, as the copy itself, into the first empty cell (there is one: the room counted one).
                Stack copy = stack.Copy();
                copy.Amount = rest;
                copy.Pinned = false;
                if (!FindEmptyCell(to, stack.TopFirst, out copy.X, out copy.Y))
                {
                    // Can't happen while the room counted an empty cell; never made up: undone as the game would refuse it.
                    UndoTopUp(to, stack, part - rest);
                    return 0;
                }
                to.Stacks.Add(copy);
            }
            stack.Amount -= part;
            if (stack.Amount <= 0)
            {
                from.Stacks.Remove(stack);
            }
            return part;
        }

        // Takes back what a refused add topped up (never reached: see Transfer).
        private static void UndoTopUp(Chest to, Stack like, int amount)
        {
            for (int i = to.Stacks.Count - 1; i >= 0 && amount > 0; i--)
            {
                Stack stack = to.Stacks[i];
                if (stack.Alike(like))
                {
                    int take = Math.Min(amount, stack.Amount);
                    stack.Amount -= take;
                    amount -= take;
                }
            }
        }

        /// <summary>The first empty cell as the game finds it (Inventory.FindEmptySlot): row by row from the top, or from the
        /// bottom row up, each row left to right.</summary>
        internal static bool FindEmptyCell(Chest chest, bool topFirst, out int x, out int y)
        {
            var taken = new HashSet<int>();
            foreach (Stack stack in chest.Stacks)
            {
                taken.Add(stack.Y * 4096 + stack.X);
            }
            for (int row = 0; row < chest.Height; row++)
            {
                y = topFirst ? row : chest.Height - 1 - row;
                for (x = 0; x < chest.Width; x++)
                {
                    if (!taken.Contains(y * 4096 + x))
                    {
                        return true;
                    }
                }
            }
            x = -1;
            y = -1;
            return false;
        }

        /// <summary>
        /// What an order would do, from counts alone (nothing is changed or copied): how much it moves, how many of the
        /// source's stacks it takes from, and how many new cells the target takes for it, as Apply would carry it out. Each
        /// item's part-stacks in the target count only by their room left (which of them is topped up first changes no
        /// count), and a new stack's own room is there for the next stack of the same item, as the game's adding finds it.
        /// <paramref name="cut"/>, if given, is told the items moved and the new cells taken after each stack the order
        /// takes from, in the take order (the order of exactly that much does the same up to there, and stops).
        /// </summary>
        internal static Moved Preview(Chest from, Chest to, string name, int amount, bool topUpOnly, out int newCells, Action<int, int> cut = null)
        {
            var moved = new Moved();
            newCells = 0;
            if (from == null || to == null || ReferenceEquals(from, to) || string.IsNullOrEmpty(name) || amount <= 0)
            {
                return moved;
            }
            var room = new Dictionary<(int, int), int>();
            foreach (Stack stack in to.Stacks)
            {
                if (stack.Name == name && stack.MaxStack > 1 && stack.Amount < stack.MaxStack)
                {
                    var item = (stack.Quality, stack.WorldLevel);
                    room.TryGetValue(item, out int had);
                    room[item] = had + stack.MaxStack - stack.Amount;
                }
            }
            int empty = to.EmptyCells;
            int left = amount;
            foreach (Stack stack in InTakeOrder(from, name))
            {
                if (left <= 0)
                {
                    break;
                }
                var item = (stack.Quality, stack.WorldLevel);
                int parts = stack.MaxStack > 1 && room.TryGetValue(item, out int r) ? r : 0;
                int fit = parts + (topUpOnly ? 0 : empty * Math.Max(1, stack.MaxStack));
                int part = Math.Min(Math.Min(left, stack.Amount), fit);
                if (part <= 0)
                {
                    continue;
                }
                int topped = Math.Min(part, parts);
                int rest = part - topped;
                if (stack.MaxStack > 1)
                {
                    room[item] = parts - topped;
                }
                if (rest > 0)
                {
                    empty--;
                    newCells++;
                    if (stack.MaxStack > 1)
                    {
                        room[item] = room[item] + Math.Max(0, stack.MaxStack - rest);
                    }
                }
                left -= part;
                moved.Items += part;
                moved.Stacks++;
                moved.Emptied += part >= stack.Amount ? 1 : 0;
                cut?.Invoke(moved.Items, newCells);
            }
            return moved;
        }

        /// <summary>
        /// The plan for one group of chests set up alike (<paramref name="chests"/>, nearest first), as the rules say. The
        /// chests given are never changed: the plan works on its own copy (Outcome.After).
        /// </summary>
        internal static Outcome Plan(IList<Chest> chests, Rules rules)
        {
            rules = rules ?? new Rules();
            var planner = new Planner(chests, rules, true);
            planner.Run();
            // A plan that kept a kind split to bring another together is kept only when it beats the same press without
            // splitting (the last look after review round 2: a tie moved about three times the stacks for nothing).
            if (planner.KeptSplit && !(rules.FreeFirst && rules.LeaveFree > 0))
            {
                var plain = new Planner(chests, rules, false);
                plain.Run();
                if (plain.Pairs <= planner.Pairs)
                {
                    return plain.Outcome;
                }
            }
            return planner.Outcome;
        }

        /// <summary>One kind of thing the plan gathers: its name, the chests it may go into (best first, by the keeper rule),
        /// the cells it needs merged, and the chests it's given, in the order they fill (more than one only for a kind too big
        /// for any one chest: the roomiest first), with the cells each.</summary>
        internal sealed class Kind
        {
            internal string Name;
            internal int Index;
            internal readonly List<int> Rank = new List<int>();
            internal readonly HashSet<int> Takers = new HashSet<int>();
            internal int Need;
            internal readonly List<int> Homes = new List<int>();
            internal readonly Dictionary<int, int> HomeCells = new Dictionary<int, int>();
        }

        private sealed class Planner
        {
            private readonly List<Chest> start = new List<Chest>();
            private List<Chest> chests = new List<Chest>();
            private readonly Rules rules;
            private readonly List<Kind> kinds = new List<Kind>();
            private readonly Dictionary<string, Kind> byName = new Dictionary<string, Kind>(StringComparer.Ordinal);
            private readonly HashSet<string> stay = new HashSet<string>(StringComparer.Ordinal);
            private readonly int[] fixedCells;
            private int[,] cells;     // each kind's movable stacks in each chest
            private int[,] units;     // each kind's units in each chest, the stack in his hand too (as the keeper rule counts)
            private int[] before;     // how many chests held each kind when the plan began
            private int[] holding;    // ...and now
            private int worse;        // kinds now in more chests than they began in
            private int lastGood;     // the orders up to the last point where no kind was
            private readonly int limit;
            private int steps;
            private readonly bool splits;   // a split that pays may be kept (else every spread kind is rolled back)

            internal readonly Outcome Outcome = new Outcome();

            /// <summary>The plan ended with a kind split to bring another together (kept because it paid).</summary>
            internal bool KeptSplit;

            /// <summary>The chests each kind is in, added up, when the plan ends (the stack in his hand not counted).</summary>
            internal int Pairs
            {
                get
                {
                    int pairs = 0;
                    foreach (Kind kind in kinds)
                    {
                        pairs += holding[kind.Index];
                    }
                    return pairs;
                }
            }

            internal Planner(IList<Chest> given, Rules rules)
                : this(given, rules, true)
            {
            }

            internal Planner(IList<Chest> given, Rules rules, bool splits)
            {
                this.rules = rules;
                this.splits = splits;
                foreach (Chest chest in given)
                {
                    Chest copy = chest != null ? chest.Copy() : new Chest();
                    start.Add(copy);
                    chests.Add(copy.Copy());
                }
                fixedCells = new int[chests.Count];
                int stacks = 0;
                foreach (Chest chest in chests)
                {
                    stacks += chest.Stacks.Count;
                }
                // Every turn moves something or ends the plan, and the most stacks a tidy moves ends it too: far more turns
                // than any group needs, only so a fault can never loop for ever.
                limit = 8 * stacks + 64;
                Outcome.After = chests;
            }

            internal void Run()
            {
                FindKinds();
                if (rules.MakeRoom)
                {
                    GiveHomes();
                }
                while (steps++ < limit && !Outcome.OutOfMoves)
                {
                    if (Pass())
                    {
                        continue;
                    }
                    if (!rules.MakeRoom || !Swap())
                    {
                        break;
                    }
                }
                // A kind in more chests than it began in is kept only where the plan split it on purpose and that pays: each such
                // kind in no more chests than it was given, and the group's kinds in fewer chests in all (a smaller kind split to
                // bring a bigger one together: review round 2, major 1); Plan then keeps it only if it beats the same press without
                // splitting. Else the plan ends at its last point where no kind was in more chests than it began in. Stopped by
                // the most stacks a tidy moves, it keeps what it did (a kind may be half moved): a press again goes on.
                if (worse > 0 && !Outcome.OutOfMoves)
                {
                    if (splits && SplitPays())
                    {
                        KeptSplit = true;
                    }
                    else
                    {
                        RollBack();
                    }
                }
                Consolidate();
                Count();
            }

            // ---- The kinds, their chests in order, the cells they need, and the counts the plan keeps as it moves.

            private void FindKinds()
            {
                var names = new SortedSet<string>(StringComparer.Ordinal);
                foreach (Chest chest in chests)
                {
                    foreach (Stack stack in chest.Stacks)
                    {
                        if (!stack.Pinned && !string.IsNullOrEmpty(stack.Name))
                        {
                            names.Add(stack.Name);
                        }
                    }
                }
                foreach (string name in names)
                {
                    var kind = new Kind { Name = name };
                    for (int c = 0; c < chests.Count; c++)
                    {
                        if (chests[c].Takes != null && chests[c].Takes(name))
                        {
                            kind.Rank.Add(c);
                        }
                    }
                    if (kind.Rank.Count == 0)
                    {
                        if (!rules.Strays)
                        {
                            stay.Add(name);   // no setup in the group takes it: left where it is
                            continue;
                        }
                        for (int c = 0; c < chests.Count; c++)
                        {
                            kind.Rank.Add(c);
                        }
                    }
                    kind.Takers.UnionWith(kind.Rank);
                    kinds.Add(kind);
                }
                // The biggest first (the hardest to place), then by name, so the same chests always plan the same.
                cells = new int[kinds.Count, chests.Count];
                units = new int[kinds.Count, chests.Count];
                for (int k = 0; k < kinds.Count; k++)
                {
                    kinds[k].Index = k;
                    byName[kinds[k].Name] = kinds[k];
                }
                for (int c = 0; c < chests.Count; c++)
                {
                    foreach (Stack stack in chests[c].Stacks)
                    {
                        if (stay.Contains(stack.Name) || stack.Pinned)
                        {
                            fixedCells[c]++;   // what can't move takes its cell where it is
                        }
                        if (byName.TryGetValue(stack.Name, out Kind kind))
                        {
                            units[kind.Index, c] += stack.Amount;
                            if (!stack.Pinned)
                            {
                                cells[kind.Index, c]++;
                            }
                        }
                    }
                }
                before = new int[kinds.Count];
                holding = new int[kinds.Count];
                foreach (Kind kind in kinds)
                {
                    kind.Need = NeedOf(kind.Name);
                    Rank(kind);
                    for (int c = 0; c < chests.Count; c++)
                    {
                        before[kind.Index] += cells[kind.Index, c] > 0 ? 1 : 0;
                    }
                    holding[kind.Index] = before[kind.Index];
                }
                kinds.Sort((a, b) => a.Need != b.Need ? b.Need.CompareTo(a.Need) : string.CompareOrdinal(a.Name, b.Name));
            }

            // A kind's chests in the keeper rule's order, by what they hold now.
            private void Rank(Kind kind) => kind.Rank.Sort((a, b) => Prefer(kind, a, b));

            // Which of two chests keeps a kind, by the keeper rule: below zero for the first.
            private int Prefer(Kind kind, int a, int b)
            {
                int heldA = units[kind.Index, a], heldB = units[kind.Index, b];
                int cellsA = chests[a].Cells, cellsB = chests[b].Cells;
                switch (rules.Keeper)
                {
                    case Keeper.Biggest:
                        if (cellsA != cellsB)
                        {
                            return cellsB.CompareTo(cellsA);
                        }
                        if (heldA != heldB)
                        {
                            return heldB.CompareTo(heldA);
                        }
                        break;
                    case Keeper.Nearest:
                        break;
                    default:
                        if (heldA != heldB)
                        {
                            return heldB.CompareTo(heldA);
                        }
                        if (cellsA != cellsB)
                        {
                            return cellsB.CompareTo(cellsA);
                        }
                        break;
                }
                return a.CompareTo(b);   // the nearer (the list is nearest first)
            }

            // The cells a kind needs once its part-stacks are merged: each item (quality and world level) in as few full
            // stacks as its stack size allows; gear a cell a piece. The stack in his hand isn't counted: it stays.
            private int NeedOf(string name)
            {
                var totals = new Dictionary<(int, int), int>();
                var sizes = new Dictionary<(int, int), int>();
                int gear = 0;
                foreach (Chest chest in chests)
                {
                    foreach (Stack stack in chest.Stacks)
                    {
                        if (stack.Name != name || stack.Pinned)
                        {
                            continue;
                        }
                        if (stack.MaxStack <= 1)
                        {
                            gear++;
                            continue;
                        }
                        var item = (stack.Quality, stack.WorldLevel);
                        totals.TryGetValue(item, out int total);
                        totals[item] = total + stack.Amount;
                        sizes[item] = stack.MaxStack;
                    }
                }
                int need = gear;
                foreach (KeyValuePair<(int, int), int> each in totals)
                {
                    int size = Math.Max(1, sizes[each.Key]);
                    need += (each.Value + size - 1) / size;
                }
                return need;
            }

            /// <summary>
            /// The cells each kind takes in each chest once all of it is there (review round 1, major 1): its stacks already
            /// in it stay as they are (nothing merges them before the moves), and the rest of it comes in topping up the
            /// part-stacks of the same item there first (the stack in his hand's too), then filling new cells. Counted once for
            /// every kind and chest, from each item's units and part-stack room in each chest (nothing moves while homes are
            /// given), so a big group's homes cost one look at its stacks per kind.
            /// </summary>
            private int[,] CellsWhole()
            {
                var whole = new int[kinds.Count, chests.Count];
                foreach (Kind kind in kinds)
                {
                    var items = new Dictionary<(int, int), ItemCount>();
                    var gearIn = new int[chests.Count];
                    int gear = 0;
                    for (int c = 0; c < chests.Count; c++)
                    {
                        foreach (Stack stack in chests[c].Stacks)
                        {
                            if (stack.Name != kind.Name)
                            {
                                continue;
                            }
                            if (stack.MaxStack <= 1)
                            {
                                if (!stack.Pinned)
                                {
                                    gear++;
                                    gearIn[c]++;
                                }
                                continue;
                            }
                            var key = (stack.Quality, stack.WorldLevel);
                            if (!items.TryGetValue(key, out ItemCount item))
                            {
                                items[key] = item = new ItemCount { Size = stack.MaxStack, In = new int[chests.Count], Room = new int[chests.Count] };
                            }
                            if (!stack.Pinned)
                            {
                                item.Total += stack.Amount;
                                item.In[c] += stack.Amount;
                            }
                            if (stack.Amount < stack.MaxStack)
                            {
                                item.Room[c] += stack.MaxStack - stack.Amount;   // the stack in his hand's room too
                            }
                        }
                    }
                    for (int c = 0; c < chests.Count; c++)
                    {
                        int more = gear - gearIn[c];
                        foreach (ItemCount item in items.Values)
                        {
                            int rest = item.Total - item.In[c] - item.Room[c];
                            int size = Math.Max(1, item.Size);
                            more += rest > 0 ? (rest + size - 1) / size : 0;
                        }
                        whole[kind.Index, c] = cells[kind.Index, c] + more;
                    }
                }
                return whole;
            }

            // One item (a quality and world level) of a kind: its stack size, its units that may move in all and in each
            // chest, and its part-stacks' room in each chest.
            private sealed class ItemCount
            {
                internal int Size;
                internal int Total;
                internal int[] In;
                internal int[] Room;
            }

            // ---- Where each kind goes ("Move other things out of the way" on).

            // Each kind, the biggest first, gets the chest it goes into:
            //   where it already sits all together, if that chest's room still holds it (so a press again never moves it);
            //   else the first chest by the keeper rule whose room holds all of it, counted as it would really lie there
            //   (CellsWhole);
            // first with the cells left free kept, then using them. Too big for any one chest, it's split over as few as the
            // room allows (Split). With "Keep free cells even if a kind splits", a kind that no chest holds whole with its cells
            // left free is split to keep them while the group has room for that, before they're used. Room: a chest's cells
            // less what can't move there and what's given to the kinds before.
            private void GiveHomes()
            {
                var used = new int[chests.Count];
                int[,] whole = CellsWhole();
                foreach (Kind kind in kinds)
                {
                    int Room(int c, bool keepFree) =>
                        Math.Max(0, chests[c].Cells - (keepFree ? Math.Max(0, rules.LeaveFree) : 0) - fixedCells[c] - used[c]);
                    if (Whole(kind, used, Room, whole, true))
                    {
                        continue;
                    }
                    if (rules.FreeFirst && rules.LeaveFree > 0)
                    {
                        int room = 0;
                        foreach (int c in kind.Rank)
                        {
                            room += Room(c, true);
                        }
                        if (room >= kind.Need)
                        {
                            Split(kind, used, Room, true);
                            continue;
                        }
                    }
                    if (!Whole(kind, used, Room, whole, false))
                    {
                        Split(kind, used, Room, false);
                    }
                }
            }

            // A kind into one chest whose room holds all of it: where it sits all together, else the first by the keeper rule.
            private bool Whole(Kind kind, int[] used, Func<int, bool, int> room, int[,] whole, bool keepFree)
            {
                int together = Together(kind);
                if (together >= 0 && room(together, keepFree) >= cells[kind.Index, together])
                {
                    Give(kind, together, cells[kind.Index, together], used);
                    return true;
                }
                foreach (int c in kind.Rank)
                {
                    int needs = whole[kind.Index, c];
                    if (room(c, keepFree) >= needs)
                    {
                        Give(kind, c, needs, used);
                        return true;
                    }
                }
                return false;
            }

            // The one chest holding all of a kind that may move, if its setup takes the kind; else -1.
            private int Together(Kind kind)
            {
                int only = -1;
                for (int c = 0; c < chests.Count; c++)
                {
                    if (cells[kind.Index, c] > 0)
                    {
                        if (only >= 0)
                        {
                            return -1;
                        }
                        only = c;
                    }
                }
                return only >= 0 && kind.Takers.Contains(only) ? only : -1;
            }

            // A kind split over several chests: the fewest, the roomiest first (counting the cells left free in, unless it's
            // split to keep them); between two as roomy, the one already holding more of it, then the keeper rule's order.
            // They fill in that order, each up to its room with its cells left free kept, then (unless it's split to keep them)
            // using them, so the next plan, finding the roomiest full, deals the same again (the harness's
            // overflow-too-big-moves-again).
            private void Split(Kind kind, int[] used, Func<int, bool, int> room, bool keepFree)
            {
                var order = new List<int>(kind.Rank);
                order.Sort((a, b) =>
                {
                    int roomA = room(a, keepFree), roomB = room(b, keepFree);
                    if (roomA != roomB)
                    {
                        return roomB.CompareTo(roomA);
                    }
                    int heldA = cells[kind.Index, a], heldB = cells[kind.Index, b];
                    return heldA != heldB ? heldB.CompareTo(heldA) : kind.Rank.IndexOf(a).CompareTo(kind.Rank.IndexOf(b));
                });
                var chosen = new List<int>();
                int enough = 0;
                foreach (int c in order)
                {
                    if (enough >= kind.Need || room(c, keepFree) <= 0)
                    {
                        break;
                    }
                    chosen.Add(c);
                    enough += room(c, keepFree);
                }
                var share = new int[chests.Count];
                int left = kind.Need;
                foreach (bool keep in keepFree ? new[] { true } : new[] { true, false })
                {
                    foreach (int c in chosen)
                    {
                        int more = Math.Min(left, room(c, keep) - share[c]);
                        if (more > 0)
                        {
                            share[c] += more;
                            left -= more;
                        }
                    }
                }
                foreach (int c in chosen)
                {
                    if (share[c] > 0)
                    {
                        Give(kind, c, share[c], used);
                    }
                }
            }

            // A chest given to a kind, with its cells there; its homes fill in the order they're given.
            private void Give(Kind kind, int chest, int given, int[] used)
            {
                used[chest] += given;
                kind.HomeCells[chest] = given;
                kind.Homes.Add(chest);
            }

            // ---- The moves.

            // One pass over every kind: whatever can move toward its place now. True if anything moved.
            private bool Pass()
            {
                bool any = false;
                foreach (Kind kind in kinds)
                {
                    if (Outcome.OutOfMoves)
                    {
                        break;
                    }
                    if (rules.MakeRoom)
                    {
                        any |= Gather(kind);
                    }
                    else
                    {
                        Rank(kind);   // by what the chests hold now (review round 1, minor 1)
                        any |= Drift(kind);
                    }
                }
                return any;
            }

            // A kind with homes: into each home in order, from every chest that isn't one of its homes (nearest first), then
            // from its later homes (the last first), then from its earlier homes. Into a home, as much as the cells it was
            // given there allow; past them, only into its part-stacks, from a chest that isn't a home always, and from another
            // home only with "Merge part-stacks between chests". From an earlier home: what it holds past the cells it was
            // given there (a kind split to make room for a bigger one: review round 2, major 1), no more stacks than that so
            // it never falls below them; and with "Merge part-stacks between chests", top-ups that free a cell (either way
            // round, so a plan right after finds none left).
            private bool Gather(Kind kind)
            {
                bool any = false;
                for (int h = 0; h < kind.Homes.Count; h++)
                {
                    int home = kind.Homes[h];
                    foreach (int from in Sources(kind, h))
                    {
                        if (Outcome.OutOfMoves)
                        {
                            return any;
                        }
                        int at = kind.Homes.IndexOf(from);
                        int room = Math.Max(0, kind.HomeCells[home] - cells[kind.Index, home]);
                        if (at >= 0 && at < h)
                        {
                            int past = cells[kind.Index, from] - kind.HomeCells[from];
                            bool moved = past > 0 && room > 0 && MoveInto(kind, from, home, Math.Min(room, past), false, mostStacks: past);
                            any |= moved || (rules.Merge && MoveInto(kind, from, home, 0, true));
                        }
                        else
                        {
                            any |= MoveInto(kind, from, home, room, at < 0 || rules.Merge);
                        }
                    }
                }
                return any;
            }

            // The chests a home takes a kind from: those holding it that aren't its homes, nearest first, then its homes after
            // this one, the last first, then its homes before this one (with "Merge part-stacks between chests", or holding more
            // than the cells they were given).
            private List<int> Sources(Kind kind, int homeAt)
            {
                var sources = new List<int>();
                for (int c = 0; c < chests.Count; c++)
                {
                    if (!kind.HomeCells.ContainsKey(c) && cells[kind.Index, c] > 0)
                    {
                        sources.Add(c);
                    }
                }
                for (int h = kind.Homes.Count - 1; h > homeAt; h--)
                {
                    int c = kind.Homes[h];
                    if (cells[kind.Index, c] > 0)
                    {
                        sources.Add(c);
                    }
                }
                for (int h = 0; h < homeAt; h++)
                {
                    int c = kind.Homes[h];
                    if (cells[kind.Index, c] > 0 && (rules.Merge || cells[kind.Index, c] > kind.HomeCells[c]))
                    {
                        sources.Add(c);
                    }
                }
                return sources;
            }

            // Without making room: a kind moves only toward the chests the keeper rule prefers, into their free room (the
            // cells left free kept), the best first; from a chest whose setup doesn't take it (a stray there) always, and into
            // part-stacks alone with "Merge part-stacks between chests". A kind already all together in a chest that takes it
            // stays there.
            private bool Drift(Kind kind)
            {
                if (Together(kind) >= 0)
                {
                    return false;
                }
                bool any = false;
                for (int t = 0; t < kind.Rank.Count; t++)
                {
                    int to = kind.Rank[t];
                    // Its sources: chests whose setup doesn't take it, nearest first, then the ones after this in its rank,
                    // the last first.
                    var sources = new List<int>();
                    for (int c = 0; c < chests.Count; c++)
                    {
                        if (!kind.Takers.Contains(c) && cells[kind.Index, c] > 0)
                        {
                            sources.Add(c);
                        }
                    }
                    for (int r = kind.Rank.Count - 1; r > t; r--)
                    {
                        if (cells[kind.Index, kind.Rank[r]] > 0)
                        {
                            sources.Add(kind.Rank[r]);
                        }
                    }
                    foreach (int from in sources)
                    {
                        if (Outcome.OutOfMoves)
                        {
                            return any;
                        }
                        int room = Math.Max(0, chests[to].EmptyCells - Math.Max(0, rules.LeaveFree));
                        bool outside = !kind.Takers.Contains(from);
                        // Into a chest holding none of it: all of it from that chest or nothing, so nothing moved for another's
                        // sake ever leaves a kind in a chest more (the harness's overflow-off-spreads).
                        any |= MoveInto(kind, from, to, room, outside || rules.Merge, wholeIfNew: true);
                    }
                }
                return any;
            }

            /// <summary>
            /// The biggest move of a kind from one chest into another that takes no more than <paramref name="newCells"/>
            /// new cells in the target: all of it if that fits, else as far as the take order gets within them; failing that,
            /// with <paramref name="topUp"/>, into the target's part-stacks only (when that frees a cell or empties the source
            /// of the kind). With <paramref name="wholeIfNew"/>, into a target that holds none of it only all of it the source
            /// holds, or nothing. <paramref name="mostStacks"/>: the most of the source's stacks it may take from. Weighed from
            /// counts alone (Preview), carried out and written down only when it moves something. True if anything moved.
            /// </summary>
            private bool MoveInto(Kind kind, int from, int to, int newCells, bool topUp, bool wholeIfNew = false, int mostStacks = int.MaxValue)
            {
                if (from == to || cells[kind.Index, from] == 0)
                {
                    return false;
                }
                Chest source = chests[from], target = chests[to];
                bool partRoom = HasPartRoom(source, target, kind.Name);
                if (!partRoom && (newCells <= 0 || target.EmptyCells <= 0))
                {
                    return false;   // the target can take none of it now: nothing is weighed (review round 1, major 3)
                }
                if (newCells > 0)
                {
                    int best = 0;
                    int taken = 0;
                    Preview(source, target, kind.Name, int.MaxValue, false, out int _, (items, grew) =>
                    {
                        if (grew <= newCells && ++taken <= mostStacks)
                        {
                            best = items;
                        }
                    });
                    if (wholeIfNew && cells[kind.Index, to] == 0 && best < Units(source, kind.Name))
                    {
                        best = 0;
                    }
                    if (best > 0)
                    {
                        return Do(kind, from, to, best, false);
                    }
                }
                if (topUp && partRoom)
                {
                    // Into its part-stacks only: worth it when a stack of the source's goes whole (a cell freed there).
                    int total = Units(source, kind.Name);
                    Moved tried = Preview(source, target, kind.Name, total, true, out int _);
                    if (tried.Emptied > 0)
                    {
                        return Do(kind, from, to, total, true);
                    }
                }
                return false;
            }

            // Whether the target has a part-stack, with room, of an item (name, quality, world level) the source holds.
            private static bool HasPartRoom(Chest source, Chest target, string name)
            {
                foreach (Stack into in target.Stacks)
                {
                    if (into.Name != name || into.MaxStack <= 1 || into.Amount >= into.MaxStack)
                    {
                        continue;
                    }
                    foreach (Stack stack in source.Stacks)
                    {
                        if (!stack.Pinned && stack.Alike(into))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            private static int Units(Chest chest, string name)
            {
                int total = 0;
                foreach (Stack stack in chest.Stacks)
                {
                    if (stack.Name == name && !stack.Pinned)
                    {
                        total += stack.Amount;
                    }
                }
                return total;
            }

            // An order carried out on the plan's copy and written down, within the most stacks a tidy moves: an order that
            // would pass it is cut to the stacks left (the take order's first ones), and the plan then stops.
            private bool Do(Kind kind, int from, int to, int amount, bool topUpOnly)
            {
                int stacksLeft = Math.Max(0, rules.MostStacks) - Outcome.Stacks;
                if (stacksLeft <= 0)
                {
                    Outcome.OutOfMoves = true;
                    return false;
                }
                Moved tried = Preview(chests[from], chests[to], kind.Name, amount, topUpOnly, out int _);
                if (tried.Stacks > stacksLeft)
                {
                    int most = 0;
                    int taken = 0;
                    Preview(chests[from], chests[to], kind.Name, amount, topUpOnly, out int _, (items, grew) =>
                    {
                        taken++;
                        if (taken <= stacksLeft)
                        {
                            most = items;
                        }
                    });
                    amount = most;
                    Outcome.OutOfMoves = true;
                    if (amount <= 0)
                    {
                        return false;
                    }
                }
                Chest target = chests[to];
                int had = units[kind.Index, to];
                Moved moved = Apply(chests[from], target, kind.Name, amount, topUpOnly);
                if (moved.Items <= 0)
                {
                    return false;
                }
                Record(from, to, kind.Name, amount, topUpOnly, moved);
                Recount(kind, from);
                Recount(kind, to);
                if (worse == 0)
                {
                    lastGood = Outcome.Orders.Count;
                }
                return true;
            }

            // An order written into the outcome, with what it moved.
            private void Record(int from, int to, string name, int amount, bool topUpOnly, Moved moved)
            {
                Outcome.Orders.Add(new Order { From = from, To = to, Name = name, Amount = amount, TopUpOnly = topUpOnly });
                Outcome.Stacks += moved.Stacks;
                Outcome.Items += moved.Items;
                Outcome.Touched.Add(from);
                Outcome.Touched.Add(to);
                if (!Outcome.Into.TryGetValue(to, out SortedDictionary<string, int> into))
                {
                    Outcome.Into[to] = into = new SortedDictionary<string, int>(StringComparer.Ordinal);
                }
                into.TryGetValue(name, out int had);
                into[name] = had + moved.Items;
            }

            // A chest's counts of a kind after a move, and whether the kind is now in more chests than it began in.
            private void Recount(Kind kind, int c)
            {
                int k = kind.Index;
                bool held = cells[k, c] > 0;
                cells[k, c] = 0;
                units[k, c] = 0;
                foreach (Stack stack in chests[c].Stacks)
                {
                    if (stack.Name == kind.Name)
                    {
                        units[k, c] += stack.Amount;
                        if (!stack.Pinned)
                        {
                            cells[k, c]++;
                        }
                    }
                }
                bool holds = cells[k, c] > 0;
                if (held == holds)
                {
                    return;
                }
                bool wasWorse = holding[k] > before[k];
                holding[k] += holds ? 1 : -1;
                bool isWorse = holding[k] > before[k];
                worse += (isWorse ? 1 : 0) - (wasWorse ? 1 : 0);
            }

            // The plan would leave a kind in more chests than it began in (review round 1, major 1): it ends at its last point
            // where none was. The orders after it are dropped, and the rest carried out again on fresh copies.
            private void RollBack()
            {
                Outcome.RolledBack = true;
                Outcome.Orders.RemoveRange(lastGood, Outcome.Orders.Count - lastGood);
                var kept = new List<Order>(Outcome.Orders);
                Outcome.Orders.Clear();
                Outcome.Stacks = 0;
                Outcome.Items = 0;
                Outcome.Touched.Clear();
                Outcome.Into.Clear();
                chests = new List<Chest>();
                foreach (Chest chest in start)
                {
                    chests.Add(chest.Copy());
                }
                foreach (Order order in kept)
                {
                    Moved moved = Apply(chests[order.From], chests[order.To], order.Name, order.Amount, order.TopUpOnly);
                    Record(order.From, order.To, order.Name, order.Amount, order.TopUpOnly, moved);
                }
                Outcome.After = chests;
                RecountAll();
            }

            // Every count again, from the chests as they are now (after a roll back's replay on fresh copies).
            private void RecountAll()
            {
                worse = 0;
                foreach (Kind kind in kinds)
                {
                    int k = kind.Index;
                    holding[k] = 0;
                    for (int c = 0; c < chests.Count; c++)
                    {
                        cells[k, c] = 0;
                        units[k, c] = 0;
                        foreach (Stack stack in chests[c].Stacks)
                        {
                            if (stack.Name == kind.Name)
                            {
                                units[k, c] += stack.Amount;
                                cells[k, c] += stack.Pinned ? 0 : 1;
                            }
                        }
                        holding[k] += cells[k, c] > 0 ? 1 : 0;
                    }
                    worse += holding[k] > before[k] ? 1 : 0;
                }
            }

            // Whether the kinds in more chests than they began in were split on purpose and it pays: each in no more chests than
            // the plan gave it, and the group's kinds in fewer chests in all than when it began (review round 2, major 1), or
            // split to keep free cells because "Keep free cells even if a kind splits" says so. Without it, a full chest holding
            // a smaller kind beside a bigger one keeps the bigger kind's strays out for ever.
            private bool SplitPays()
            {
                int now = 0;
                int was = 0;
                foreach (Kind kind in kinds)
                {
                    int k = kind.Index;
                    now += holding[k];
                    was += before[k];
                    if (holding[k] > before[k] && holding[k] > kind.Homes.Count)
                    {
                        return false;
                    }
                }
                return now < was || (rules.FreeFirst && rules.LeaveFree > 0);
            }

            // ---- The last step ("Move other things out of the way" on): whatever is still spread past the chests the plan
            // gave it, a chest's stacks of it all into another chest already holding it that takes it, when all of them fit in
            // its free cells less those other kinds were given there and haven't filled yet (and less those left free, with
            // "Keep free cells even if a kind splits"); a top-up taking no cell always fits (review round 2, major 1). Its own
            // chests first, then by the keeper rule's order; the chest holding the fewest stacks of it gives first. It only
            // ever brings a kind together, so it needs no roll back.

            private void Consolidate()
            {
                if (!rules.MakeRoom)
                {
                    return;
                }
                for (int round = 0; round < 64 && !Outcome.OutOfMoves; round++)
                {
                    bool any = false;
                    foreach (Kind kind in kinds)
                    {
                        if (Outcome.OutOfMoves)
                        {
                            break;
                        }
                        int k = kind.Index;
                        // The chests the plan gave it, unless the plan was rolled back: then its homes were never reached, and
                        // one chest is what a kind needs.
                        if (holding[k] <= (Outcome.RolledBack ? 1 : Math.Max(1, kind.Homes.Count)))
                        {
                            continue;
                        }
                        Rank(kind);
                        var from = new List<int>();
                        for (int c = 0; c < chests.Count; c++)
                        {
                            if (cells[k, c] > 0)
                            {
                                from.Add(c);
                            }
                        }
                        from.Sort((a, b) => cells[k, a] != cells[k, b] ? cells[k, a].CompareTo(cells[k, b]) : b.CompareTo(a));
                        var into = new List<int>();
                        foreach (int home in kind.Homes)
                        {
                            into.Add(home);
                        }
                        foreach (int c in kind.Rank)
                        {
                            if (!into.Contains(c))
                            {
                                into.Add(c);
                            }
                        }
                        any |= Gathered(kind, from, into);
                    }
                    if (!any)
                    {
                        break;
                    }
                }
            }

            // One move of the last step: the first chest (of those given) whose stacks of the kind all fit into another chest
            // already holding it.
            private bool Gathered(Kind kind, List<int> from, List<int> into)
            {
                int k = kind.Index;
                foreach (int source in from)
                {
                    int total = Units(chests[source], kind.Name);
                    foreach (int target in into)
                    {
                        if (target == source || cells[k, target] == 0 || !kind.Takers.Contains(target))
                        {
                            continue;
                        }
                        int free = FreeFor(kind, target);
                        if (free <= 0 && !HasPartRoom(chests[source], chests[target], kind.Name))
                        {
                            continue;   // no cell to spare and no part-stack to top up: nothing weighed (the last look, nit 3)
                        }
                        Moved tried = Preview(chests[source], chests[target], kind.Name, total, false, out int grew);
                        if (tried.Items != total || (grew > 0 && grew > free))
                        {
                            continue;   // a top-up taking no cell always fits (the last look, minor 2)
                        }
                        if (tried.Stacks > Math.Max(0, rules.MostStacks) - Outcome.Stacks)
                        {
                            Outcome.OutOfMoves = true;   // all of it or none: the most stacks a tidy moves is reached, a press again goes on
                            return false;
                        }
                        if (Do(kind, source, target, total, false))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            // A chest's empty cells a kind may take at the end: less those other kinds were given there and haven't filled yet,
            // and, with "Keep free cells even if a kind splits", less those left free (off, a kind uses them rather than stay
            // split: decision 35, the last look's nit 1).
            private int FreeFor(Kind kind, int c)
            {
                int promised = 0;
                foreach (Kind other in kinds)
                {
                    // A plan rolled back promised nothing: its homes were never to be reached.
                    if (!Outcome.RolledBack && other != kind && other.HomeCells.TryGetValue(c, out int given))
                    {
                        promised += Math.Max(0, given - cells[other.Index, c]);
                    }
                }
                int keep = rules.FreeFirst ? Math.Max(0, rules.LeaveFree) : 0;
                return chests[c].EmptyCells - keep - promised;
            }

            // ---- A swap ("Move other things out of the way" on): when nothing can move straight to its place, one stack of
            // something that isn't at home in a full chest a kind waits for goes into a chest of the group whose setup takes
            // it and that has room (its own homes first), so the kind can come in.

            private bool Swap()
            {
                foreach (Kind kind in kinds)
                {
                    foreach (int home in kind.Homes)
                    {
                        if (!Waits(kind, home) || chests[home].EmptyCells > 0)
                        {
                            continue;
                        }
                        // The things in it that aren't at home there, each by its first stack in the take order, the smallest
                        // first.
                        var outcasts = new List<Stack>();
                        var seen = new HashSet<string>(StringComparer.Ordinal);
                        foreach (Stack stack in chests[home].Stacks)
                        {
                            if (stack.Pinned || stay.Contains(stack.Name) || !byName.ContainsKey(stack.Name) || IsHome(stack.Name, home) || !seen.Add(stack.Name))
                            {
                                continue;
                            }
                            outcasts.Add(InTakeOrder(chests[home], stack.Name)[0]);
                        }
                        outcasts.Sort(TakeOrder);
                        foreach (Stack first in outcasts)
                        {
                            Kind outcast = byName[first.Name];
                            foreach (int spare in Spares(outcast, home))
                            {
                                Moved tried = Preview(chests[home], chests[spare], first.Name, first.Amount, false, out int _);
                                if (tried.Items >= first.Amount && Do(outcast, home, spare, first.Amount, false))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                return false;
            }

            // Whether a kind still has something to bring into this home of its: some of it outside its homes, or in a later
            // home, while this one has cells left for it.
            private bool Waits(Kind kind, int home)
            {
                if (kind.HomeCells[home] - cells[kind.Index, home] <= 0)
                {
                    return false;
                }
                int at = kind.Homes.IndexOf(home);
                for (int c = 0; c < chests.Count; c++)
                {
                    bool later = kind.HomeCells.ContainsKey(c) && kind.Homes.IndexOf(c) > at;
                    if ((!kind.HomeCells.ContainsKey(c) || later) && c != home && cells[kind.Index, c] > 0)
                    {
                        return true;
                    }
                }
                return false;
            }

            private bool IsHome(string name, int chest) => !byName.TryGetValue(name, out Kind kind) || kind.HomeCells.ContainsKey(chest);

            // Chests that may take one stack of something in the way for a swap (review round 1, major 2): only chests whose
            // setup takes it (its Rank), with an empty cell; first those with more empty cells than the kinds given them still
            // wait to bring in (a cell nobody needs), then the rest; within each, its own homes first, then the roomiest, then
            // the nearest.
            private List<int> Spares(Kind outcast, int except)
            {
                var spares = new List<int>();
                var free = new Dictionary<int, int>();
                foreach (int c in outcast.Rank)
                {
                    if (c == except || chests[c].EmptyCells <= 0)
                    {
                        continue;
                    }
                    int promised = 0;
                    foreach (Kind kind in kinds)
                    {
                        if (kind.HomeCells.TryGetValue(c, out int given))
                        {
                            promised += Math.Max(0, given - cells[kind.Index, c]);
                        }
                    }
                    free[c] = chests[c].EmptyCells - promised;
                    spares.Add(c);
                }
                spares.Sort((a, b) =>
                {
                    bool spareA = free[a] > 0, spareB = free[b] > 0;
                    if (spareA != spareB)
                    {
                        return spareA ? -1 : 1;
                    }
                    bool homeA = outcast.HomeCells.ContainsKey(a), homeB = outcast.HomeCells.ContainsKey(b);
                    if (homeA != homeB)
                    {
                        return homeA ? -1 : 1;
                    }
                    int roomA = chests[a].EmptyCells, roomB = chests[b].EmptyCells;
                    return roomA != roomB ? roomB.CompareTo(roomA) : a.CompareTo(b);
                });
                return spares;
            }

            // ---- How it ended.

            private void Count()
            {
                foreach (Kind kind in kinds)
                {
                    int holdingNow = 0;
                    bool outside = false;
                    bool refused = false;
                    for (int c = 0; c < chests.Count; c++)
                    {
                        bool holds = false;
                        foreach (Stack stack in chests[c].Stacks)
                        {
                            if (stack.Name == kind.Name && !stack.Pinned)
                            {
                                holds = true;
                                break;
                            }
                        }
                        if (holds)
                        {
                            holdingNow++;
                            outside |= rules.MakeRoom ? !kind.HomeCells.ContainsKey(c) : !kind.Takers.Contains(c);
                            refused |= !kind.Takers.Contains(c);
                        }
                    }
                    int fewest = rules.MakeRoom ? Math.Max(1, kind.Homes.Count) : 1;
                    if (holdingNow > fewest)
                    {
                        Outcome.StillSpread++;
                    }
                    if (outside)
                    {
                        Outcome.OutOfPlace++;
                    }
                    if (refused && holdingNow <= fewest)
                    {
                        Outcome.Misplaced++;   // a kind spread too is told once, as spread (the last look, nit 2)
                    }
                }
            }
        }
    }
}
