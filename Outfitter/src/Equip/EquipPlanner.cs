using System;
using System.Collections.Generic;
using Outfitter.Loadouts;
using QoLMods.Chests;

namespace Outfitter.Equip
{
    /// <summary>A chest as a plan sees it: its contents (this computer's copy, or, once claimed, the chest itself) and
    /// where it stands.</summary>
    internal sealed class ChestView
    {
        /// <summary>The chest (a Container in the game; anything in the harness).</summary>
        internal object Chest;

        internal Inventory Contents;

        /// <summary>Metres from the centre the chests were found around.</summary>
        internal float Distance;

        /// <summary>Within the caller's "near first" range (a stand's chests).</summary>
        internal bool Near;

        /// <summary>A friend on the pack has it open: taken from through his game, by key only (he picks the copy), and
        /// never put into.</summary>
        internal bool Shared;

        /// <summary>How strongly its setup takes an item (0: not at all, or no setup); null: no setup.</summary>
        internal Func<ItemDrop.ItemData, int> Fit;

        internal bool SetUpFor(ItemDrop.ItemData item) => Fit != null && Fit(item) > 0;
    }

    /// <summary>What a plan is made from.</summary>
    internal sealed class PlanInput
    {
        internal Loadout Loadout;
        internal EquipRules Rules;
        internal GridShape Grid;

        /// <summary>His inventory (every row, EquipmentAndQuickSlots' too).</summary>
        internal Inventory Inventory;

        /// <summary>What he wears an item as now (None: not worn).</summary>
        internal Func<ItemDrop.ItemData, WornAs> WornAs;

        /// <summary>The hand items the game has put away for a moment: his worn gear all the same.</summary>
        internal ItemDrop.ItemData HiddenRight;
        internal ItemDrop.ItemData HiddenLeft;

        internal Func<ItemDrop.ItemData, bool> Favourite;
        internal Func<ItemDrop.ItemData, string> PrefabOf;

        /// <summary>The game's own item for a prefab name (its template), or null when this game has no such item.</summary>
        internal Func<string, ItemDrop.ItemData> Template;

        internal readonly List<ChestView> Chests = new List<ChestView>();

        internal TakenOffGoes TakenOff = TakenOffGoes.Away;

        /// <summary>What he takes off that stays with him whatever <see cref="TakenOff"/> says (EquipRequest.KeepsTakenOff:
        /// a stand's pieces going home); null: none.</summary>
        internal Func<ItemDrop.ItemData, bool> KeepsTakenOff;

        /// <summary>Whether a worn item he takes off stays with him (by <see cref="TakenOff"/>, or by
        /// <see cref="KeepsTakenOff"/>).</summary>
        internal bool Keeps(ItemDrop.ItemData item) => EquipRequest.Keeping(TakenOff, KeepsTakenOff, item);

        /// <summary>Items of his that never go away (EquipRequest.Stays: a stand's own gear); null: none.</summary>
        internal Func<ItemDrop.ItemData, bool> Stays;

        /// <summary>What an earlier round of the same equip took off and kept with him (EquipResult.TakenOff): unworn now,
        /// never sent away by a later round (review round 1 of job 30, M1); null: none.</summary>
        internal ICollection<ItemDrop.ItemData> KeptEarlier;

        /// <summary>How much of each of his stacks the routing really places in a chest (the exchange's Route); null: all
        /// the plan sends away. The fit counts the room only of what leaves (R3, finding 2).</summary>
        internal Func<ItemDrop.ItemData, int> Leaves;

        /// <summary>What an earlier plan of the same equip chose for each food slot (EquipPlan.Foods: the exchange keeps
        /// its first plan's), so its later plans, once the chests are claimed, in later rounds and dressing him, fill each
        /// slot with the same food; null: choose now (FoodPick).</summary>
        internal IDictionary<LoadoutItem, FoodChoice> FoodsChosen;
    }

    /// <summary>One thing the loadout asks for: a piece of gear, or a stackable item counted across stacks.</summary>
    internal sealed class Want
    {
        /// <summary>The loadout's items it stands for: one for gear, every saved stack of the kind for stackables
        /// counted per item; a food slot alone (a want of the food the plan chose for it).</summary>
        internal readonly List<LoadoutItem> Items = new List<LoadoutItem>();

        internal LoadoutItem First => Items[0];

        /// <summary>The prefab it asks for: its item's, or for a food slot the food chosen (empty when none was).</summary>
        internal string Prefab = string.Empty;

        /// <summary>A food slot's want (its one item is the slot).</summary>
        internal bool IsFoodSlot => Items.Count > 0 && Items[0].Food != null;

        /// <summary>The game's item for its prefab; null: not in this game.</summary>
        internal ItemDrop.ItemData Template;

        /// <summary>A piece that never stacks.</summary>
        internal bool Gear;

        /// <summary>How it is worn, if it is (the first of its items that is).</summary>
        internal WornAs Worn;

        /// <summary>How many in all.</summary>
        internal int Count;

        /// <summary>His items answering it, and how much of each.</summary>
        internal readonly List<Use> Have = new List<Use>();

        internal int HaveCount;

        /// <summary>What comes from chests for it.</summary>
        internal readonly List<Take> Takes = new List<Take>();

        internal int TakeCount;

        internal int Missing;

        internal string Why;

        /// <summary>The kind's key for the chest service: its $ name, with "@quality" when the kind has qualities.</summary>
        internal string Key;
    }

    /// <summary>How much of one of his stacks answers a want.</summary>
    internal sealed class Use
    {
        internal ItemDrop.ItemData Item;
        internal int Amount;
    }

    /// <summary>What comes from one chest for one want.</summary>
    internal sealed class Take
    {
        internal ChestView Chest;

        /// <summary>For gear from a chest of his: the very copy chosen (found again by its fields once the chest is
        /// his). Null for stackable items, and for gear from a friend's open chest (taken by key).</summary>
        internal ItemDrop.ItemData Copy;

        internal string Key;

        internal int Amount;

        internal Want Want;
    }

    /// <summary>One of his stacks, or part of it, going into the chests.</summary>
    internal sealed class Away
    {
        internal ItemDrop.ItemData Item;

        /// <summary>How much of it: the whole stack, or the extra beyond the loadout's count.</summary>
        internal int Amount;
    }

    /// <summary>A plan: what stays, what goes, what comes, what is put on, and what is missing.</summary>
    internal sealed class EquipPlan
    {
        internal readonly List<Want> Wants = new List<Want>();

        /// <summary>What he wears that comes off (and goes away, or stays with him by the request).</summary>
        internal readonly List<ItemDrop.ItemData> TakeOff = new List<ItemDrop.ItemData>();

        /// <summary>What goes into chests.</summary>
        internal readonly List<Away> Aways = new List<Away>();

        /// <summary>Everything that comes from chests, in the order to take it, which is the order room is given out in:
        /// the worn pieces replacing one of his that goes away first, then the other worn pieces (gear, arrows), then the
        /// other gear, then the other stackable items, each in the loadout's order.</summary>
        internal readonly List<Take> Takes = new List<Take>();

        /// <summary>What he wears in a place whose loadout piece is missing, kept on ("Keep wearing mine").</summary>
        internal readonly List<ItemDrop.ItemData> KeptOn = new List<ItemDrop.ItemData>();

        /// <summary>The loadout sets a hand: both hands are emptied first, then the right-hand item put on, then the left.</summary>
        internal bool HandsChange;

        /// <summary>Cells his inventory lacks for everything first planned to come, carried out as the game adds it (what
        /// didn't fit is missing).</summary>
        internal int CellsShort;

        internal int MissingCount
        {
            get
            {
                int missing = 0;
                foreach (Want want in Wants)
                {
                    missing += want.Missing;
                }
                return missing;
            }
        }

        internal int FetchCount
        {
            get
            {
                int fetched = 0;
                foreach (Take take in Takes)
                {
                    fetched += take.Amount;
                }
                return fetched;
            }
        }

        /// <summary>The chests the plan takes from.</summary>
        internal readonly List<ChestView> TakeChests = new List<ChestView>();

        /// <summary>No chest is needed: nothing comes and nothing goes away (what he has may still be put on, taken off
        /// and moved to its cells).</summary>
        internal bool NoChests;

        /// <summary>What each of the loadout's food slots takes (FoodPick), by the slot's LoadoutItem: a food, or none and
        /// why. Empty in Swap gear only, which ignores food slots as it ignores food.</summary>
        internal readonly Dictionary<LoadoutItem, FoodChoice> Foods = new Dictionary<LoadoutItem, FoodChoice>();

        /// <summary>The food a slot takes in this plan (the game's item for it, its icon and name), or null: no food fills
        /// it, it isn't a slot of this plan's loadout (by reference: the very LoadoutItem the plan's input held), or the
        /// mode ignores food slots.</summary>
        internal ItemDrop.ItemData FoodFor(LoadoutItem slot) =>
            slot != null && Foods.TryGetValue(slot, out FoodChoice choice) && choice != null ? choice.Template : null;
    }

    /// <summary>
    /// The plan for putting a loadout on (the Outfitter plan, "Equipping", step 2): which of his items already answer
    /// each loadout item, what he takes off and what goes away (by the mode), which copy of each missing piece of gear
    /// comes from which chest and how many of each stackable item come from where, whether it all fits, and what is
    /// missing. It reads and changes nothing in the game: made before the claim from this computer's copies, and again
    /// once the chests are his. Where what goes away goes is the exchange's (the quick stack's routing). Pure apart from
    /// the game's plain item classes, so the scratch harness reaches it.
    /// </summary>
    internal static class EquipPlanner
    {
        internal static EquipPlan Plan(PlanInput input)
        {
            var plan = new EquipPlan();
            EquipRules rules = input.Rules;
            var carried = new List<ItemDrop.ItemData>(input.Inventory.GetAllItems());
            var used = new Dictionary<ItemDrop.ItemData, int>();   // how much of each of his stacks answers a want

            // Each food slot's food first, from the totals (card A34, decision 72); Swap gear only ignores them, as food.
            if (rules.Mode != EquipMode.SwapGearOnly)
            {
                foreach (KeyValuePair<LoadoutItem, FoodChoice> food in FoodPick.Choose(input))
                {
                    plan.Foods[food.Key] = food.Value;
                }
            }
            MakeWants(input, plan);

            // Gear first, the worn pieces first: each of his items answers one want at most.
            foreach (Want want in Ordered(plan.Wants))
            {
                if (want.Template == null && want.IsFoodSlot)
                {
                    continue;   // no food fills it: missing whole, the choice's why (FoodWant)
                }
                if (want.Template == null)
                {
                    want.Missing = want.Count;
                    want.Why = "not in your game";
                    continue;
                }
                if (want.Gear)
                {
                    ItemDrop.ItemData best = BestCarried(input, want, carried, used);
                    if (best != null)
                    {
                        used[best] = 1;
                        want.Have.Add(new Use { Item = best, Amount = 1 });
                        want.HaveCount = 1;
                    }
                }
                else
                {
                    OwnCells(input, want, carried, used);
                }
            }
            // Then what's left of any stack answers a stackable item still short (one saved stack's surplus tops up
            // another: R3, finding 3).
            foreach (Want want in Ordered(plan.Wants))
            {
                if (want.Template != null && !want.Gear && want.HaveCount < want.Count)
                {
                    UseStacks(input, want, carried, used);
                }
            }

            // What comes from chests.
            var chosen = new HashSet<ItemDrop.ItemData>();
            var taken = new Dictionary<ChestView, Dictionary<string, int>>();
            foreach (Want want in Ordered(plan.Wants))
            {
                int shortBy = want.Count - want.HaveCount;
                if (want.Template == null || shortBy <= 0)
                {
                    continue;
                }
                if (want.Gear)
                {
                    PlanGear(input, want, chosen);
                }
                else
                {
                    PlanStackable(input, want, shortBy, taken);
                }
                want.Missing = MissingOf(input, want);
                if (want.Missing > 0 && want.Why == null)
                {
                    want.Why = NotEnough(rules, want.TakeCount > 0);
                }
            }

            // What goes, what "Keep wearing mine" keeps on, and whether what comes fits. A worn piece the fit leaves behind
            // keeps his own on in its place, which then frees no cell: fitted again until nothing more is kept (takes only
            // ever shrink, so a few passes at most).
            HashSet<ItemDrop.ItemData> keep = KeptOn(input, plan, carried, used);
            DecideWhatGoes(input, plan, carried, used, keep);
            var planned = new List<KeyValuePair<Take, int>>();
            foreach (Take take in TakeOrder(input, plan))
            {
                planned.Add(new KeyValuePair<Take, int>(take, take.Amount));
            }
            Fit(input, plan);
            for (int pass = 0; pass < 8; pass++)
            {
                HashSet<ItemDrop.ItemData> more = KeptOn(input, plan, carried, used);
                if (more.IsSubsetOf(keep))
                {
                    break;
                }
                keep.UnionWith(more);
                DecideWhatGoes(input, plan, carried, used, keep);
                Fit(input, plan);
            }
            plan.CellsShort = CellsShort(input, plan, planned);

            plan.Takes.AddRange(TakeOrder(input, plan));
            foreach (Take take in plan.Takes)
            {
                if (!plan.TakeChests.Contains(take.Chest))
                {
                    plan.TakeChests.Add(take.Chest);
                }
            }
            plan.HandsChange = HandsChange(input, plan);
            plan.NoChests = plan.Takes.Count == 0 && plan.Aways.Count == 0;
            return plan;
        }

        /// <summary>What a want lacks: its count, less what he has and what comes for it. A food slot putting its stat first
        /// lacks nothing once it has at least "Food slots by stat: at least" of its food (or its count, if fewer): it took that
        /// food for being enough (0.5.0, his 3 Honey glazed chickens for a slot of 10; review 1's M1).</summary>
        internal static int MissingOf(PlanInput input, Want want)
        {
            int got = want.HaveCount + want.TakeCount;
            int missing = want.Count - got;
            if (missing > 0 && want.IsFoodSlot && want.Template != null && want.First.Food.First == FoodFirst.Stat &&
                got >= FoodPick.Enough(want.First, want.Template, input.Rules.FoodStatAtLeast))
            {
                return 0;
            }
            return missing;
        }

        // Why a want is short after the chests were looked through.
        private static string NotEnough(EquipRules rules, bool some) =>
            rules.FetchFrom == FetchFrom.SetUpChestsOnly
                ? (some ? "not enough in the chests set up for it" : "none in the chests set up for it")
                : (some ? "not enough nearby" : "none nearby");

        // Whether the hands change: a hand piece comes from a chest, one of his goes into a hand it isn't in, or a hand item
        // he holds comes off. Then both hands are emptied and the right-hand item put on before the left (the plan's
        // "Hands"), what "Keep wearing mine" kept going back on in its hand.
        private static bool HandsChange(PlanInput input, EquipPlan plan)
        {
            foreach (Want want in plan.Wants)
            {
                if (want.Worn != WornAs.RightHand && want.Worn != WornAs.LeftHand)
                {
                    continue;
                }
                if (want.TakeCount > 0)
                {
                    return true;
                }
                foreach (Use use in want.Have)
                {
                    if (WornNow(input, use.Item) != want.Worn)
                    {
                        return true;
                    }
                }
            }
            foreach (ItemDrop.ItemData item in plan.TakeOff)
            {
                WornAs now = WornNow(input, item);
                if (now == WornAs.RightHand || now == WornAs.LeftHand)
                {
                    return true;
                }
            }
            return false;
        }

        // One want per piece of gear; stackable items merged per kind ("Topping up counts" PerItem) or one per saved stack;
        // a food slot a want of its own, of the food chosen for it (none in Swap gear only).
        private static void MakeWants(PlanInput input, EquipPlan plan)
        {
            EquipRules rules = input.Rules;
            var byKind = new Dictionary<string, Want>();
            foreach (LoadoutItem item in input.Loadout.Items)
            {
                if (item.Food != null)
                {
                    if (plan.Foods.TryGetValue(item, out FoodChoice food))
                    {
                        plan.Wants.Add(FoodWant(item, food));
                    }
                    continue;
                }
                ItemDrop.ItemData template = input.Template(item.Prefab);
                if (template != null && rules.Mode == EquipMode.SwapGearOnly && !Matching.IsGear(template.m_shared.m_itemType, rules))
                {
                    continue;   // "ignore any non-equippable items from saved loadout"
                }
                bool gear = template != null && Matching.Single(template);
                if (!gear && template != null && rules.TopUp == TopUp.PerItem)
                {
                    bool levels = template.m_shared.m_maxQuality > 1;
                    string kind = item.Prefab + "@" + (levels ? item.Quality : 0);
                    if (byKind.TryGetValue(kind, out Want same))
                    {
                        same.Items.Add(item);
                        same.Count += Math.Max(1, item.Count);
                        if (same.Worn == WornAs.None)
                        {
                            same.Worn = WornIfFits(item, template);
                        }
                        continue;
                    }
                }
                var want = new Want
                {
                    Template = template,
                    Prefab = item.Prefab,
                    Gear = gear,
                    Worn = WornIfFits(item, template),
                    Count = gear ? 1 : Math.Max(1, item.Count),
                };
                want.Items.Add(item);
                if (template != null)
                {
                    string name = template.m_shared.m_name;
                    want.Key = template.m_shared.m_maxQuality > 1 ? name + "@" + item.Quality : name;
                    if (!gear && rules.TopUp == TopUp.PerItem)
                    {
                        byKind[item.Prefab + "@" + (template.m_shared.m_maxQuality > 1 ? item.Quality : 0)] = want;
                    }
                }
                plan.Wants.Add(want);
            }
        }

        // A food slot's want: an ordinary stackable want of the food chosen for it (its count, at most one stack, topped up
        // from what he carries first, the rest from the chests), never worn, kept tied to the slot (the window marks a
        // missing slot by its LoadoutItem); with no food, missing whole, the choice's why.
        private static Want FoodWant(LoadoutItem slot, FoodChoice food)
        {
            if (food?.Template == null)
            {
                var none = new Want { Count = Math.Max(1, slot.Count), Why = food?.Why ?? "no food nearby meets it" };
                none.Missing = none.Count;
                none.Items.Add(slot);
                return none;
            }
            return SlotWant(slot, food.Prefab, food.Template);
        }

        /// <summary>A food slot's want of one food, as the plan makes it (and as FoodPick counts what he carries and what
        /// the chests could give of it).</summary>
        internal static Want SlotWant(LoadoutItem slot, string prefab, ItemDrop.ItemData template)
        {
            var want = new Want
            {
                Template = template,
                Prefab = prefab,
                Gear = false,
                Worn = WornAs.None,
                Count = FoodPick.Target(slot, template),
                Key = template.m_shared.m_maxQuality > 1 ? template.m_shared.m_name + "@" + slot.Quality : template.m_shared.m_name,
            };
            want.Items.Add(slot);
            return want;
        }

        // The place a loadout's item is worn in, when the item can be worn there (a share code or a hand-edited file may
        // say anything: R1, findings 4 and 19); else not worn.
        private static WornAs WornIfFits(LoadoutItem item, ItemDrop.ItemData template) =>
            template != null && Matching.WornFits(item.Worn, template.m_shared.m_itemType) ? item.Worn : WornAs.None;

        /// <summary>The place a want stands for, for "Keep wearing mine": where it's worn, or for an item his game lacks
        /// (whose kind can't be checked, and which is never put on) the place it was saved in, so his own stays on there.</summary>
        internal static WornAs PlaceOf(Want want) => want.Template == null ? want.First.Worn : want.Worn;

        /// <summary>Whether an item is the kind a want asks for (its prefab): the $ name compared first, which costs
        /// nothing, and the prefab's name only then (reading a prefab's name asks the engine: R1, finding 8).</summary>
        internal static bool SameKind(PlanInput input, ItemDrop.ItemData item, Want want) =>
            want.Template != null && item.m_shared.m_name == want.Template.m_shared.m_name && input.PrefabOf(item) == want.Prefab;

        // Gear before stackables; among gear, the worn pieces first; otherwise the loadout's order.
        private static List<Want> Ordered(List<Want> wants)
        {
            var ordered = new List<Want>(wants);
            int Rank(Want w) => w.Gear ? (w.Worn != WornAs.None ? 0 : 1) : 2;
            var index = new Dictionary<Want, int>();
            for (int i = 0; i < wants.Count; i++)
            {
                index[wants[i]] = i;
            }
            ordered.Sort((a, b) => Rank(a) != Rank(b) ? Rank(a).CompareTo(Rank(b)) : index[a].CompareTo(index[b]));
            return ordered;
        }

        // The copy of his own that answers a piece of gear: one he wears there already, else the best copy, else the one
        // already in its saved cell.
        private static ItemDrop.ItemData BestCarried(PlanInput input, Want want, List<ItemDrop.ItemData> carried,
                                                     Dictionary<ItemDrop.ItemData, int> used)
        {
            LoadoutItem li = want.First;
            ItemDrop.ItemData best = null;
            foreach (ItemDrop.ItemData item in carried)
            {
                if (used.ContainsKey(item) || item.m_stack <= 0 || !SameKind(input, item, want) || !Matching.Answers(item, li, input.Rules))
                {
                    continue;
                }
                if (want.Worn != WornAs.None && Matching.Broken(item))
                {
                    continue;   // a piece to wear that the game won't put on
                }
                if (best == null || Better(input, want, item, best))
                {
                    best = item;
                }
            }
            return best;
        }

        private static bool Better(PlanInput input, Want want, ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            bool wornA = WornNow(input, a) != WornAs.None && WornNow(input, a) == want.Worn;
            bool wornB = WornNow(input, b) != WornAs.None && WornNow(input, b) == want.Worn;
            if (wornA != wornB)
            {
                return wornA;
            }
            int compare = Matching.Compare(a, b, input.Rules);
            if (compare != 0)
            {
                return compare < 0;
            }
            bool placedA = InSavedCell(input, want.First, a), placedB = InSavedCell(input, want.First, b);
            return placedA && !placedB;
        }

        private static bool InSavedCell(PlanInput input, LoadoutItem li, ItemDrop.ItemData item) =>
            Places.CellFor(li, input.Grid, out int x, out int y) && item.m_gridPos.x == x && item.m_gridPos.y == y;

        /// <summary>What he wears an item as now, a hand item the game has put away counted in its hand.</summary>
        internal static WornAs WornNow(PlanInput input, ItemDrop.ItemData item)
        {
            WornAs worn = input.WornAs(item);
            if (worn != WornAs.None)
            {
                return worn;
            }
            if (item != null && ReferenceEquals(item, input.HiddenRight))
            {
                return WornAs.RightHand;
            }
            if (item != null && ReferenceEquals(item, input.HiddenLeft))
            {
                return WornAs.LeftHand;
            }
            return WornAs.None;
        }

        // First, the stacks in a stackable item's own saved cells answer it, up to its count: every saved stack keeps its
        // own cell's ("Topping up counts" PerSavedStack: the world harness, 37-B).
        private static void OwnCells(PlanInput input, Want want, List<ItemDrop.ItemData> carried, Dictionary<ItemDrop.ItemData, int> used)
        {
            foreach (ItemDrop.ItemData item in carried)
            {
                if (want.HaveCount >= want.Count)
                {
                    return;
                }
                if (item.m_stack <= 0 || !SameKind(input, item, want) || !Matching.Answers(item, want.First, input.Rules))
                {
                    continue;
                }
                bool own = false;
                foreach (LoadoutItem li in want.Items)
                {
                    own |= InSavedCell(input, li, item);
                }
                used.TryGetValue(item, out int already);
                int amount = Math.Min(item.m_stack - already, want.Count - want.HaveCount);
                if (!own || amount <= 0)
                {
                    continue;
                }
                used[item] = already + amount;
                want.Have.Add(new Use { Item = item, Amount = amount });
                want.HaveCount += amount;
            }
        }

        // Then what's left of his stacks of the kind answers what it's still short of: the worn one first, then the
        // biggest; another saved stack's own count stays its own, only its surplus counts here.
        private static void UseStacks(PlanInput input, Want want, List<ItemDrop.ItemData> carried, Dictionary<ItemDrop.ItemData, int> used)
        {
            var stacks = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in carried)
            {
                if (item.m_stack > 0 && SameKind(input, item, want) && Matching.Answers(item, want.First, input.Rules) &&
                    (!used.TryGetValue(item, out int already) || already < item.m_stack))
                {
                    stacks.Add(item);
                }
            }
            int Rank(ItemDrop.ItemData item)
            {
                foreach (LoadoutItem li in want.Items)
                {
                    if (InSavedCell(input, li, item))
                    {
                        return 0;
                    }
                }
                // A stack that never goes away (a favourite, by the settings) counts before the rest: it stays with him
                // anyway, so counting it first leaves nothing that a second press would put away (job 30, Session 3: the
                // food agent's toppingup-favourite-second-press). A stack in one of EquipmentAndQuickSlots' quick slots
                // counts last, so a quick slot is drawn on only when the rest fall short (review round 1, m1).
                return WornNow(input, item) != WornAs.None ? 1 : InQuickSlot(input, item) ? 4 : Protected(input, item) ? 2 : 3;
            }
            stacks.Sort((a, b) => Rank(a) != Rank(b) ? Rank(a).CompareTo(Rank(b)) : b.m_stack.CompareTo(a.m_stack));
            int left = want.Count - want.HaveCount;
            foreach (ItemDrop.ItemData item in stacks)
            {
                if (left <= 0)
                {
                    break;
                }
                used.TryGetValue(item, out int already);
                int amount = Math.Min(item.m_stack - already, left);
                if (amount <= 0)
                {
                    continue;
                }
                Use mine = want.Have.Find(use => ReferenceEquals(use.Item, item));
                if (mine != null)
                {
                    mine.Amount += amount;   // one Use per stack
                }
                else
                {
                    want.Have.Add(new Use { Item = item, Amount = amount });
                }
                used[item] = already + amount;
                want.HaveCount += amount;
                left -= amount;
            }
        }

        // The copy of a piece of gear to fetch: the best quality (by the rule), then the chests near the caller's point,
        // then the set-up ones ("Take the loadout's items from"), then the least worn (by the rule), then the nearest; a
        // friend's open chest last of equals, since his game picks the copy.
        private static void PlanGear(PlanInput input, Want want, HashSet<ItemDrop.ItemData> chosen)
        {
            LoadoutItem li = want.First;
            EquipRules rules = input.Rules;
            ChestView bestChest = null;
            ItemDrop.ItemData best = null;
            bool sawBroken = false;
            foreach (ChestView chest in input.Chests)
            {
                if (chest.Contents == null)
                {
                    continue;
                }
                foreach (ItemDrop.ItemData item in chest.Contents.GetAllItems())
                {
                    if (chosen.Contains(item) || item.m_stack <= 0 || (item.m_worldLevel < rules.WorldLevel && !IsLent(chest)) ||
                        !SameKind(input, item, want) || !Matching.Answers(item, li, rules) || !Allowed(rules, chest, item))
                    {
                        continue;
                    }
                    if (want.Worn != WornAs.None && Matching.Broken(item))
                    {
                        sawBroken = true;
                        continue;
                    }
                    if (best == null || BetterCopy(rules, item, chest, best, bestChest))
                    {
                        best = item;
                        bestChest = chest;
                    }
                }
            }
            if (best == null)
            {
                if (sawBroken)
                {
                    want.Why = "only broken ones nearby";
                }
                else if (rules.FetchFrom == FetchFrom.SetUpChestsOnly)
                {
                    want.Why = "none in the chests set up for it";
                }
                return;
            }
            chosen.Add(best);
            var take = new Take
            {
                Chest = bestChest,
                Copy = bestChest.Shared ? null : best,
                Key = KeyOf(best),
                Amount = 1,
                Want = want,
            };
            want.Takes.Add(take);
            want.TakeCount = 1;
        }

        // A broken copy last of all (it can't be used until mended), then the order above.
        private static bool BetterCopy(EquipRules rules, ItemDrop.ItemData a, ChestView chestA, ItemDrop.ItemData b, ChestView chestB)
        {
            // The pieces his stands hold for this equip (the window's preview through a stand) come first, as they come to
            // him first in the real equip and then answer as carried (review round 1 of job 30's Session 3, m2).
            if (IsLent(chestA) != IsLent(chestB))
            {
                return IsLent(chestA);
            }
            bool brokenA = Matching.Broken(a), brokenB = Matching.Broken(b);
            if (brokenA != brokenB)
            {
                return brokenB;
            }
            if (rules.Quality != GearQuality.Exact && a.m_quality != b.m_quality)
            {
                return a.m_quality > b.m_quality;
            }
            int tierA = Tier(rules, chestA, a), tierB = Tier(rules, chestB, b);
            if (tierA != tierB)
            {
                return tierA < tierB;
            }
            int compare = Matching.Compare(a, b, rules);
            if (compare != 0)
            {
                return compare < 0;
            }
            if (chestA.Shared != chestB.Shared)
            {
                return !chestA.Shared;
            }
            return chestA.Distance < chestB.Distance;
        }

        // A chest's tier for an item: near the caller's point first, then set up for it (when that comes first).
        private static int Tier(EquipRules rules, ChestView chest, ItemDrop.ItemData item)
        {
            if (IsLent(chest))
            {
                return -1;   // his stands' pieces held for the equip, before any chest (m2)
            }
            int tier = chest.Near ? 0 : 2;
            if (rules.FetchFrom != FetchFrom.AnyChest && !chest.SetUpFor(item))
            {
                tier++;
            }
            return tier;
        }

        /// <summary>Whether a chest may give this item at all ("Take the loadout's items from": SetUpChestsOnly).</summary>
        internal static bool Allowed(EquipRules rules, ChestView chest, ItemDrop.ItemData item) =>
            rules.FetchFrom != FetchFrom.SetUpChestsOnly || chest.SetUpFor(item) || IsLent(chest);

        /// <summary>The window's preview's "chest" of what his stands hold for the equip (Exchange.LentView): no chest's
        /// rules apply to it (not "Take the loadout's items from", not the world's level, not "Leave one"), as none applies
        /// to what the stands give in the real equip.</summary>
        internal static bool IsLent(ChestView chest) => chest != null && ReferenceEquals(chest.Chest, Exchange.LentStock);

        /// <summary>The chest service's key for an item: its $ name, with "@quality" when its kind has qualities
        /// (ChestStock.KeyOf).</summary>
        internal static string KeyOf(ItemDrop.ItemData item) =>
            item.m_shared.m_maxQuality > 1 ? item.m_shared.m_name + "@" + item.m_quality : item.m_shared.m_name;

        // How many of a stackable item come from which chests: by tier, then nearest; one left in each chest by
        // "Leave one of each item in chests"; never more than a chest holds of the kind at the world's level.
        private static void PlanStackable(PlanInput input, Want want, int needed, Dictionary<ChestView, Dictionary<string, int>> taken)
        {
            EquipRules rules = input.Rules;
            var chests = new List<ChestView>();
            foreach (ChestView chest in input.Chests)
            {
                if (chest.Contents != null && Allowed(rules, chest, want.Template))
                {
                    chests.Add(chest);
                }
            }
            chests.Sort((a, b) =>
            {
                int tierA = Tier(rules, a, want.Template), tierB = Tier(rules, b, want.Template);
                return tierA != tierB ? tierA.CompareTo(tierB) : a.Distance.CompareTo(b.Distance);
            });
            foreach (ChestView chest in chests)
            {
                if (needed <= 0)
                {
                    break;
                }
                bool lent = IsLent(chest);
                int count = CountKey(chest.Contents, want, lent ? 0 : rules.WorldLevel);
                if (rules.LeaveOne && count > 0 && !lent)
                {
                    count--;
                }
                if (!taken.TryGetValue(chest, out Dictionary<string, int> fromChest))
                {
                    taken[chest] = fromChest = new Dictionary<string, int>();
                }
                fromChest.TryGetValue(want.Key, out int already);
                int amount = Math.Min(count - already, needed);
                if (amount <= 0)
                {
                    continue;
                }
                fromChest[want.Key] = already + amount;
                want.Takes.Add(new Take { Chest = chest, Key = want.Key, Amount = amount, Want = want });
                want.TakeCount += amount;
                needed -= amount;
            }
        }

        /// <summary>How many of a want's key a chest holds at the world's level.</summary>
        internal static int CountKey(Inventory contents, Want want, int worldLevel)
        {
            int count = 0;
            foreach (ItemDrop.ItemData item in contents.GetAllItems())
            {
                if (OfKey(item, want, worldLevel))
                {
                    count += item.m_stack;
                }
            }
            return count;
        }

        // A chest's stack of a want's key, as the chest service counts and takes it (ChestStock.Take): the same $ name,
        // the same quality where the kind has qualities, at least the world's level.
        private static bool OfKey(ItemDrop.ItemData item, Want want, int worldLevel)
        {
            ItemDrop.ItemData template = want.Template;
            return item.m_stack > 0 && item.m_shared.m_name == template.m_shared.m_name && item.m_worldLevel >= worldLevel &&
                   (template.m_shared.m_maxQuality <= 1 || item.m_quality == want.First.Quality);
        }

        // What he takes off and what goes away, by the mode; what "Keep wearing mine" keeps on (keep) stays.
        private static void DecideWhatGoes(PlanInput input, EquipPlan plan, List<ItemDrop.ItemData> carried, Dictionary<ItemDrop.ItemData, int> used,
                                           HashSet<ItemDrop.ItemData> keep)
        {
            EquipRules rules = input.Rules;
            plan.TakeOff.Clear();
            plan.Aways.Clear();
            plan.KeptOn.Clear();
            foreach (ItemDrop.ItemData item in carried)
            {
                if (keep.Contains(item))
                {
                    plan.KeptOn.Add(item);
                }
            }
            var wornAs = new Dictionary<ItemDrop.ItemData, WornAs>();
            foreach (Want want in plan.Wants)
            {
                foreach (Use use in want.Have)
                {
                    wornAs[use.Item] = want.Worn;
                }
            }
            foreach (ItemDrop.ItemData item in carried)
            {
                if (item.m_stack <= 0)
                {
                    continue;
                }
                WornAs now = WornNow(input, item);
                bool worn = now != WornAs.None;
                used.TryGetValue(item, out int answering);
                if (answering > 0)
                {
                    // It answers the loadout: it comes off only when the loadout doesn't wear it (it stays, unworn).
                    if (worn && wornAs.TryGetValue(item, out WornAs wanted) && wanted == WornAs.None && !keep.Contains(item))
                    {
                        plan.TakeOff.Add(item);
                    }
                    int extra = item.m_stack - answering;
                    if (extra > 0 && rules.ExtraGoesAway && !worn && !Protected(input, item))
                    {
                        plan.Aways.Add(new Away { Item = item, Amount = extra });
                    }
                    continue;
                }
                if (keep.Contains(item))
                {
                    continue;
                }
                if (!worn && input.KeptEarlier != null && input.KeptEarlier.Contains(item))
                {
                    continue;   // taken off and kept by an earlier round (a stand's piece going home): it stays with him
                }
                bool goes;
                if (!worn && OfAWantedKind(input, plan, item))
                {
                    // More of a kind the loadout holds than its count (another stack, a second copy): "More than the
                    // loadout's count" decides, in every mode (R1, finding 3).
                    goes = rules.ExtraGoesAway && !Protected(input, item);
                }
                else if (rules.Mode == EquipMode.FullyReplace)
                {
                    goes = !Protected(input, item);
                }
                else
                {
                    // The swap modes put away only what he wore, and never a favourite (R1, finding 2).
                    goes = worn && Matching.IsGear(item.m_shared.m_itemType, rules) && !Protected(input, item);
                }
                if (!goes)
                {
                    continue;
                }
                if (worn)
                {
                    plan.TakeOff.Add(item);
                    if (input.Keeps(item))
                    {
                        continue;   // it stays with him (the request: a stand hangs it up, job 30)
                    }
                }
                plan.Aways.Add(new Away { Item = item, Amount = item.m_stack });
            }
        }

        // What never goes away: quest items, and favourites by the setting, in every mode (while the favourites' file
        // can't be read, every item counts as one: Exchange.FavouriteOf), but for favourite gear while "My favourite gear in
        // an equip" swaps it (job 30, Session 3: his gear is all favourites, and none of it swapped), and for any favourite
        // while the loadout's "Favourites go too" is on (0.5.0, his words: "overrides all favorited items 'stickiness'");
        // in Fully replace also EquipmentAndQuickSlots' quick-slot items, unless a setting says so.
        private static bool Protected(PlanInput input, ItemDrop.ItemData item)
        {
            if (item.m_shared.m_questItem)
            {
                return true;
            }
            if (input.Stays != null && input.Stays(item))
            {
                return true;   // the request's own (a stand's gear, which goes back on the stand: job 30)
            }
            if (!input.Rules.FavouritesGoFor(input.Loadout) && input.Rules.LeaveFavourites && input.Favourite != null && input.Favourite(item) &&
                !(input.Rules.FavouriteGearSwaps && Matching.IsGear(item.m_shared.m_itemType, input.Rules)))
            {
                return true;
            }
            if (input.Rules.Mode == EquipMode.FullyReplace && !input.Rules.QuickSlotsInFullyReplace)
            {
                string slot = input.Grid.SlotAt(item.m_gridPos.x, item.m_gridPos.y);
                if (slot != null && input.Grid.Slots[slot].Quick)
                {
                    return true;
                }
            }
            return false;
        }

        // Whether an item sits in one of EquipmentAndQuickSlots' quick slots.
        internal static bool InQuickSlot(PlanInput input, ItemDrop.ItemData item)
        {
            string slot = input.Grid?.SlotAt(item.m_gridPos.x, item.m_gridPos.y);
            return slot != null && input.Grid.Slots[slot].Quick;
        }

        // Whether an item is a kind one of the loadout's wants asks for (so not answering it means it's more than its count).
        private static bool OfAWantedKind(PlanInput input, EquipPlan plan, ItemDrop.ItemData item)
        {
            foreach (Want want in plan.Wants)
            {
                if (SameKind(input, item, want))
                {
                    return true;
                }
            }
            return false;
        }

        // "Keep wearing mine when a piece is missing": for each place the loadout wears something in that can't be had at
        // all (a piece of gear, arrows, or an item his game lacks), what he wears in that place stays on (and stays with
        // him); never in a place another of the loadout's pieces fills.
        private static HashSet<ItemDrop.ItemData> KeptOn(PlanInput input, EquipPlan plan, List<ItemDrop.ItemData> carried, Dictionary<ItemDrop.ItemData, int> used)
        {
            var kept = new HashSet<ItemDrop.ItemData>();
            if (!input.Rules.KeepWornWhenMissing)
            {
                return kept;
            }
            var missing = new HashSet<WornAs>();
            var filled = new HashSet<WornAs>();
            foreach (Want want in plan.Wants)
            {
                WornAs place = PlaceOf(want);
                if (place == WornAs.None)
                {
                    continue;
                }
                if (want.HaveCount + want.TakeCount > 0)
                {
                    filled.Add(place);
                }
                else
                {
                    missing.Add(place);
                }
            }
            foreach (ItemDrop.ItemData item in carried)
            {
                WornAs now = WornNow(input, item);
                if (now != WornAs.None && missing.Contains(now) && !filled.Contains(now) && !used.ContainsKey(item))
                {
                    kept.Add(item);
                }
            }
            return kept;
        }

        // The order what comes is taken in, and room given out in: first the worn pieces that replace one of his going away
        // whole (its cell frees as they come, so with a cell of his visible rows it always finds one; worn armour in
        // EquipmentAndQuickSlots' equipment cells frees none the game would add to), then the other worn pieces (gear,
        // arrows), then the other gear, then the other stackable items, each in the loadout's order.
        private static List<Take> TakeOrder(PlanInput input, EquipPlan plan)
        {
            var freed = new HashSet<WornAs>();
            foreach (KeyValuePair<ItemDrop.ItemData, int> leaves in Leaving(input, plan))
            {
                WornAs now = WornNow(input, leaves.Key);
                if (now != WornAs.None && leaves.Value >= leaves.Key.m_stack)
                {
                    freed.Add(now);
                }
            }
            int Rank(Want want) => want.Worn != WornAs.None ? (freed.Contains(want.Worn) ? 0 : 1) : want.Gear ? 2 : 3;
            var order = new List<Take>();
            for (int rank = 0; rank < 4; rank++)
            {
                foreach (Want want in plan.Wants)
                {
                    if (Rank(want) == rank)
                    {
                        order.AddRange(want.Takes);
                    }
                }
            }
            return order;
        }

        // Whether what's planned to come fits, once what goes away has gone: the room is given out in the order things come
        // (TakeOrder), each take carried out as the chest service and the game carry it out (Arrives); what finds no room is
        // left behind and missing ("no room in your inventory"), so everything a plan keeps arrives, as long as what goes
        // away leaves (with the routing's placements in PlanInput.Leaves, only what it places counts: R3, finding 2). A
        // stackable item's take may come in part. Room counted ahead and then given to something else isn't counted again
        // (R1's harness, 37-B).
        private static void Fit(PlanInput input, EquipPlan plan)
        {
            Room room = RoomOf(input, plan);
            var takenFrom = new Dictionary<ItemDrop.ItemData, int>();
            foreach (Take take in TakeOrder(input, plan))
            {
                int arrives = Arrives(input, take, take.Amount, room, takenFrom);
                if (arrives >= take.Amount)
                {
                    continue;
                }
                Want want = take.Want;
                want.TakeCount -= take.Amount - arrives;
                take.Amount = arrives;
                if (arrives <= 0)
                {
                    want.Takes.Remove(take);
                }
                want.Missing = MissingOf(input, want);
                want.Why = "no room in your inventory";
            }
        }

        // How many cells short everything first planned to come was (before any was left behind), carried out on the room
        // left once what goes away has gone, with no limit on cells.
        private static int CellsShort(PlanInput input, EquipPlan plan, List<KeyValuePair<Take, int>> planned)
        {
            Room room = RoomOf(input, plan);
            int free = room.Free;
            room.Free = int.MaxValue;
            var takenFrom = new Dictionary<ItemDrop.ItemData, int>();
            foreach (KeyValuePair<Take, int> each in planned)
            {
                Arrives(input, each.Key, each.Value, room, takenFrom);
            }
            long cells = (long)int.MaxValue - room.Free;
            return (int)Math.Max(0L, cells - free);
        }

        // Room for what comes: free cells, and how much more of a kind his stacks can take, by what the game stacks
        // together (Inventory.FindFreeStackItem: the same name, quality and world level).
        private sealed class Room
        {
            internal int Free;

            private readonly Dictionary<string, int> stacks = new Dictionary<string, int>();

            private static string Kind(ItemDrop.ItemData item) => item.m_shared.m_name + "\n" + item.m_quality + "\n" + item.m_worldLevel;

            internal int Space(ItemDrop.ItemData item) => stacks.TryGetValue(Kind(item), out int space) ? space : 0;

            internal void Add(ItemDrop.ItemData item, int space)
            {
                if (space > 0)
                {
                    stacks[Kind(item)] = Space(item) + space;
                }
            }

            internal void Use(ItemDrop.ItemData item, int amount)
            {
                if (amount > 0)
                {
                    stacks[Kind(item)] = Space(item) - amount;
                }
            }
        }

        // The room his inventory will have once what goes away has gone: its free cells (the visible rows and
        // EquipmentAndQuickSlots' quick slots, never its equipment cells; a cell whose stack goes away whole counts as free,
        // one going in part doesn't), and the room in his stacks that stay.
        private static Room RoomOf(PlanInput input, EquipPlan plan)
        {
            GridShape grid = input.Grid;
            Dictionary<ItemDrop.ItemData, int> leaving = Leaving(input, plan);
            var room = new Room();
            for (int y = 0; y < grid.Height; y++)
            {
                for (int x = 0; x < grid.Width; x++)
                {
                    bool usable = y < grid.Rows || (grid.SlotAt(x, y) is string slot && grid.Slots[slot].Quick);
                    if (!usable)
                    {
                        continue;
                    }
                    ItemDrop.ItemData there = input.Inventory.GetItemAt(x, y);
                    if (there == null || (leaving.TryGetValue(there, out int goes) && goes >= there.m_stack))
                    {
                        room.Free++;
                    }
                }
            }
            foreach (ItemDrop.ItemData item in input.Inventory.GetAllItems())
            {
                if (Matching.Single(item))
                {
                    continue;
                }
                leaving.TryGetValue(item, out int goes);
                int stays = item.m_stack - goes;
                if (stays > 0)
                {
                    room.Add(item, item.m_shared.m_maxStackSize - stays);
                }
            }
            return room;
        }

        // How much of each of his stacks leaves: what the plan sends away, or as much of it as the routing places, when the
        // input says (PlanInput.Leaves).
        private static Dictionary<ItemDrop.ItemData, int> Leaving(PlanInput input, EquipPlan plan)
        {
            var leaving = new Dictionary<ItemDrop.ItemData, int>();
            foreach (Away away in plan.Aways)
            {
                leaving.TryGetValue(away.Item, out int already);
                leaving[away.Item] = already + away.Amount;
            }
            if (input.Leaves != null)
            {
                foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(leaving.Keys))
                {
                    leaving[item] = Math.Max(0, Math.Min(leaving[item], input.Leaves(item)));
                }
            }
            return leaving;
        }

        // How much of a take arrives, carried out on the room as ChestStock.Take carries it out: a piece of gear needs a
        // free cell; a stackable item comes as the chest's stacks of its key in the order Take picks them (lowest level,
        // then smallest, first: ChestStock.TakeOrder), each added as the game adds (Inventory.AddItem: into his stacks with
        // room first, the rest into a free cell, whose leftover room takes what comes next), stopping at the first that
        // finds no cell for its rest. What arrives uses the room up.
        private static int Arrives(PlanInput input, Take take, int amount, Room room, Dictionary<ItemDrop.ItemData, int> takenFrom)
        {
            if (amount <= 0)
            {
                return 0;
            }
            if (take.Want.Gear)
            {
                if (room.Free <= 0)
                {
                    return 0;
                }
                room.Free--;
                return 1;
            }
            var stacks = new List<KeyValuePair<ItemDrop.ItemData, int>>();
            if (take.Chest.Contents != null)
            {
                foreach (ItemDrop.ItemData item in take.Chest.Contents.GetAllItems())
                {
                    if (!OfKey(item, take.Want, input.Rules.WorldLevel))
                    {
                        continue;
                    }
                    takenFrom.TryGetValue(item, out int gone);
                    if (item.m_stack - gone > 0)
                    {
                        stacks.Add(new KeyValuePair<ItemDrop.ItemData, int>(item, item.m_stack - gone));
                    }
                }
            }
            stacks.Sort((a, b) => ChestStock.TakeOrder(a.Key, b.Key, a.Value, b.Value));
            int moved = 0;
            foreach (KeyValuePair<ItemDrop.ItemData, int> stack in stacks)
            {
                if (moved >= amount)
                {
                    break;
                }
                ItemDrop.ItemData item = stack.Key;
                int part = Math.Min(stack.Value, amount - moved);
                int merged = Math.Min(part, room.Space(item));
                room.Use(item, merged);
                int arrived = part;
                int rest = part - merged;
                if (rest > 0)
                {
                    if (room.Free > 0)
                    {
                        room.Free--;
                        room.Add(item, item.m_shared.m_maxStackSize - rest);
                    }
                    else
                    {
                        arrived = merged;
                    }
                }
                takenFrom.TryGetValue(item, out int before);
                takenFrom[item] = before + arrived;
                moved += arrived;
                if (arrived < part)
                {
                    break;
                }
            }
            return moved;
        }
    }
}
