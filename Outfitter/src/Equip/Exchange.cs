using System;
using System.Collections.Generic;
using Outfitter.Loadouts;
using QoLMods.Chests;
using QoLMods.ItemMarks;
using UnityEngine;

namespace Outfitter.Equip
{
    /// <summary>
    /// The chests' part of an equip (the Outfitter plan, "Equipping", steps 4 to 6): one claim for every chest the plan
    /// uses, taking and putting alike; then, in the frame the chests are his, each reloaded and Reserve Stock's mark read
    /// again, the plan made again from what they really hold, and in this order: every equip waiting in the game's queue
    /// cleared, what comes off taken off (only what a chest will take, what the loadout puts something else in place of,
    /// what stays with him anyway, or what the loadout carries unworn: the rest stays on, R1 finding 14; R2, finding 3),
    /// what goes away put away (ChestPut.Put, routed as the quick stack routes: StackRouting.Tiers, StackPlanner.Plan; the
    /// plan counts the room only of what the routing places: PlanRouted), what comes taken (ChestStock.Take: gear by its
    /// very stack, stackable items by key), what came off and is still with him marked to go back on where nothing else
    /// went (StayOn, OffHands), and every chest changed sent and held, in a finally. Then he is dressed (Dress).
    /// A friend's open chest is asked through his game a round later, once the put-away has made room (R1, finding 12), and
    /// no later than that: the next round asks it whatever that round puts away (R2, finding 2), and the last round never
    /// defers it (R4); meanwhile the round's own plan counts it as a source, so the put-away makes its room (R3, finding 4).
    /// Up to three rounds for what a chest in use, a silent owner or a friend's chest left undone, taking and putting alike
    /// (R1, finding 7). The moves are the chest service's own, measured, on chests this computer owns, so nothing is lost or
    /// copied; what no chest takes stays with him. An equip given up (Cancel) moves nothing more.
    /// </summary>
    internal sealed class Exchange
    {
        private const int Rounds = 3;

        /// <summary>The words for an error during it, in the module's warning.</summary>
        internal const string Task = "putting a loadout on";

        private readonly EquipRequest request;
        private readonly EquipRules rules;
        private readonly IWearer wearer;
        private readonly Action<Exchange> settled;
        private readonly HashSet<Container> failed = new HashSet<Container>();
        private readonly HashSet<Container> notOwner = new HashSet<Container>();
        private readonly HashSet<Container> away = new HashSet<Container>();
        private List<Container> asked = new List<Container>();
        private List<Container> nearby = new List<Container>();
        private int round;
        private bool cancelled;
        private bool sharesDeferred;
        private bool deferredBefore;
        private List<ChestView> pendingShared = new List<ChestView>();

        internal readonly EquipResult Result = new EquipResult();

        /// <summary>What was to come off but stays on: no chest takes it and nothing takes its place (R1, finding 14); and
        /// what came off to go away or be replaced but is still with him (no chest took it, or what was to replace it didn't
        /// come: R3, finding 2), to go back on. Dressing leaves it on or puts it back where nothing else went, a hand item
        /// into its hand when the game holds it beside the other hand's.</summary>
        internal readonly HashSet<ItemDrop.ItemData> StayOn = new HashSet<ItemDrop.ItemData>();

        /// <summary>The hand items this exchange took off, and which hand each was in (a hand item the game had put away
        /// counted in its hand): dressing puts one back from here (StayOn).</summary>
        internal readonly Dictionary<ItemDrop.ItemData, WornAs> OffHands = new Dictionary<ItemDrop.ItemData, WornAs>();

        /// <summary>What this exchange took off to go away (card B18): a later round's plan still sends it away as the worn
        /// piece it was (PlanInput.OffToGo), so the swap modes route it again and the result lists it if it stays.</summary>
        internal readonly HashSet<ItemDrop.ItemData> OffToGo = new HashSet<ItemDrop.ItemData>();

        /// <summary>Set once the last round is over.</summary>
        internal bool Over { get; private set; }

        /// <summary>The plan made once the chests were his, last round.</summary>
        internal EquipPlan LastPlan { get; private set; }

        /// <summary>Why each of the loadout's items was short, as the last plan that saw every chest nearby said it (made
        /// before the round's claim, rebuilt each round): the plan made once the chests are his sees only those, so its
        /// "none nearby" can hide a better reason ("no room in your inventory", "only broken ones nearby": the world
        /// harness, 37-B).</summary>
        internal readonly Dictionary<LoadoutItem, string> Reasons = new Dictionary<LoadoutItem, string>();

        /// <summary>The chests found, with their copies, as the first plan saw them (for the window's preview).</summary>
        internal static List<ChestView> Found(EquipRequest request, IWearer wearer)
        {
            var views = new List<ChestView>();
            Vector3 centre = request.Centre ?? wearer.Position;
            foreach (Container chest in ChestFinder.Find(centre, Settings.ChestRules()))
            {
                ChestView view = View(request, chest, centre);
                if (view != null)
                {
                    views.Add(view);
                }
            }
            return views;
        }

        /// <summary>The window's preview's "chest" of the items held for an equip elsewhere (EquipRequest.Lent: a stand's
        /// pieces, job 30's Session 3): copies of them, at the centre and near first, so the plan takes them before any
        /// chest. It is no Container, so no route ever puts into it, and only Outfit.Preview plans with it: nothing is ever
        /// claimed or taken from it. Null for none.</summary>
        internal static ChestView LentView(List<ItemDrop.ItemData> lent)
        {
            if (lent == null || lent.Count == 0)
            {
                return null;
            }
            var contents = new Inventory("Outfitter_Lent", null, lent.Count, 1);
            foreach (ItemDrop.ItemData item in lent)
            {
                if (item != null && item.m_stack > 0)
                {
                    contents.AddItem(item.Clone());
                }
            }
            return new ChestView { Chest = LentStock, Contents = contents, Distance = 0f, Near = true, Shared = false };
        }

        /// <summary>What LentView's "chest" is: no Container, so the preview's window can tell its takes from the chests'
        /// (<see cref="IsLent"/>).</summary>
        internal static readonly object LentStock = new object();

        /// <summary>Whether a take of a plan comes from the stands' pieces held for it (LentView), not from a chest.</summary>
        internal static bool IsLent(Take take) => take?.Chest != null && ReferenceEquals(take.Chest.Chest, LentStock);

        internal Exchange(EquipRequest request, EquipRules rules, IWearer wearer, Action<Exchange> settled)
        {
            this.request = request;
            this.rules = rules;
            this.wearer = wearer;
            this.settled = settled;
        }

        /// <summary>Gives the equip up: a claim still on its way settles moving nothing, and no later round starts (R1,
        /// finding 16).</summary>
        internal void Cancel()
        {
            cancelled = true;
            Over = true;
        }

        /// <summary>
        /// Plans a round from this computer's copies and claims what it uses. False only when that needs chests claimed
        /// from other players while another claim of the chest service is under way (nothing done: ask again a moment
        /// later). A round that needs no chest (everything is with him) settles at once.
        /// </summary>
        internal bool NextRound()
        {
            if (Over)
            {
                return true;
            }
            if (round >= Rounds || !Module.IsOn || cancelled || wearer.Gone)
            {
                Finish();
                return true;
            }
            Vector3 centre = request.Centre ?? wearer.Position;
            var views = new List<ChestView>();
            nearby = new List<Container>();
            foreach (Container chest in ChestFinder.Find(centre, Settings.ChestRules()))
            {
                if (failed.Contains(chest))
                {
                    continue;
                }
                if (ChestClaims.CantClaim(chest, Module.Chests))
                {
                    // This game can't get it at all now (card A69): asked, it would only wait out its time. No put, no take.
                    away.Add(chest);
                    continue;
                }
                ChestView view = View(request, chest, centre);
                if (view == null || (view.Shared && notOwner.Contains(chest)))
                {
                    // Someone else has it open, and his game can't share it (or said it doesn't hold the chest, though it
                    // still shows as open): no put, no take (the world harness, 37-B).
                    away.Add(chest);
                    continue;
                }
                views.Add(view);
                if (!view.Shared)
                {
                    nearby.Add(chest);
                }
            }
            EquipPlan plan = PlanRouted(views, nearby, Away(), out List<Planned> puts);
            Reasons.Clear();
            foreach (Want want in plan.Wants)
            {
                if (want.Missing > 0 && want.Why != null)
                {
                    Reasons[want.First] = want.Why;
                }
            }
            var claim = new List<Container>();
            var shares = new Dictionary<Container, ShareTake>();
            // A friend's game sizes what it gives by his room when the claim starts, before anything is put away: with
            // something to put away, friends are asked in the next round (R1, finding 12), but only once: a round that puts
            // something away every time ("Keep wearing mine" keeping the piece a friend's chest would replace) would never
            // ask them (R2, finding 2).
            bool deferShares = puts.Count > 0 && !deferredBefore && round + 1 < Rounds;   // never in the last round (R4)
            sharesDeferred = false;
            var deferredViews = new List<ChestView>();
            foreach (Take take in plan.Takes)
            {
                var chest = (Container)take.Chest.Chest;
                if (take.Chest.Shared)
                {
                    if (deferShares)
                    {
                        sharesDeferred = true;
                        if (!deferredViews.Contains(take.Chest))
                        {
                            deferredViews.Add(take.Chest);
                        }
                        continue;
                    }
                    if (!shares.TryGetValue(chest, out ShareTake share))
                    {
                        shares[chest] = share = new ShareTake(chest);
                    }
                    share.Add(take.Key, take.Amount);
                }
                else if (!claim.Contains(chest))
                {
                    claim.Add(chest);
                }
            }
            foreach (Planned put in puts)
            {
                if (!claim.Contains(put.Chest))
                {
                    claim.Add(put.Chest);
                }
            }
            bool allMine = claim.TrueForAll(Mine) && shares.Count == 0;
            if (round > 0 && claim.Count == 0 && shares.Count == 0)
            {
                Finish();   // a later round with nothing left to fetch or put away
                return true;
            }
            if (!allMine && ChestClaims.Busy)
            {
                if (round == 0)
                {
                    return false;   // asked again a moment later: nothing has changed
                }
                Finish();
                return true;
            }
            round++;
            asked = claim;
            // The one deferral is spent only by a round that starts (a busy first try spends nothing: R3, finding 4); set
            // before the claim, whose answer may come inside Start.
            bool deferredWas = deferredBefore;
            deferredBefore |= sharesDeferred;
            pendingShared = sharesDeferred ? deferredViews : new List<ChestView>();
            if (allMine)
            {
                // His own chests (or none) need no claim: it all happens now, even while another module's claim is under way.
                var mine = new ClaimResult();
                mine.Held.AddRange(claim);
                Module.Guard(Task, () => Settled(mine));
                return true;
            }
            if (!ChestClaims.Start(Module.Chests, claim, shares.Values, Settings.ChestRules(), Settled, Task))
            {
                round--;
                deferredBefore = deferredWas;
                pendingShared = new List<ChestView>();
                if (round == 0)
                {
                    return false;
                }
                Finish();
            }
            return true;
        }

        /// <summary>The plan for these chests and where what goes away goes: planned, routed, and when the routing can't
        /// place all of it (no chest with room, or one not had), planned again counting the room only of what it places
        /// (R3, finding 2), then routed again.</summary>
        private EquipPlan PlanRouted(List<ChestView> views, List<Container> chests, ICollection<Container> awayNow, out List<Planned> puts)
        {
            EquipPlan plan = EquipPlanner.Plan(Input(views));
            if (foods == null)
            {
                foods = new Dictionary<LoadoutItem, FoodChoice>(plan.Foods);   // the first plan's: every later one keeps it
            }
            puts = Route(plan, chests, awayNow);
            var placed = new Dictionary<ItemDrop.ItemData, int>();
            foreach (Planned put in puts)
            {
                placed.TryGetValue(put.Away.Item, out int already);
                placed[put.Away.Item] = already + put.Amount;
            }
            bool all = true;
            foreach (Away goes in plan.Aways)
            {
                placed.TryGetValue(goes.Item, out int went);
                all &= went >= goes.Amount;
            }
            if (all)
            {
                return plan;
            }
            PlanInput again = Input(views);
            again.Leaves = item => placed.TryGetValue(item, out int went) ? went : 0;
            plan = EquipPlanner.Plan(again);
            puts = Route(plan, chests, awayNow);
            return plan;
        }

        /// <summary>A plan for the equip's questions, from these views (the chests' copies), routed as the round will route
        /// it, so "no room" is said as it will be.</summary>
        internal EquipPlan PlanFound(List<ChestView> views)
        {
            var chests = new List<Container>();
            foreach (ChestView view in views)
            {
                if (!view.Shared && view.Chest is Container chest)
                {
                    chests.Add(chest);
                }
            }
            return PlanRouted(views, chests, Away(), out _);
        }

        // In the frame the claim settled: every chest in held.Held is this computer's now, and what friends' open chests
        // gave is in his inventory already.
        private void Settled(ClaimResult held)
        {
            Result.ChestsInUse += held.InUse;
            Result.ChestsNoAnswer += held.NoAnswer;
            bool sharesFailed = false;
            foreach (ShareResult share in held.Shared)
            {
                Result.Fetched += share.GivenInAll;
                Result.Dropped += share.Dropped;
                switch (share.Status)
                {
                    case ShareStatus.NoAnswer:
                        Result.ChestsNoAnswer++;
                        sharesFailed = true;
                        break;
                    case ShareStatus.Refused:
                        Result.ChestsInUse++;
                        sharesFailed = true;
                        break;
                    case ShareStatus.NotOwner:
                        sharesFailed = true;   // closed meanwhile: a later round asks it the usual way, and never as open again
                        notOwner.Add(share.Chest);
                        break;
                }
                if (share.Status != ShareStatus.NotOwner)
                {
                    failed.Add(share.Chest);
                }
            }
            bool anyFailed = held.InUse + held.NoAnswer + held.Gone > 0;
            foreach (Container chest in asked)
            {
                if (!held.Held.Contains(chest))
                {
                    failed.Add(chest);
                    away.Add(chest);   // a set-up chest not had stays the home of what it takes
                }
            }
            if (cancelled || !Module.IsOn || wearer.Unavailable)
            {
                Finish();   // given up, switched off, or gone, while waiting: nothing moves
                return;
            }

            // Plan again from what the chests really hold now, nearest first as they were found. A chest reserved
            // meanwhile (its mark came with the ownership) takes and gives nothing.
            Vector3 centre = request.Centre ?? wearer.Position;
            var views = new List<ChestView>();
            var chests = new List<Container>();
            bool reservedMeanwhile = false;
            foreach (Container chest in NearestFirst(held.Held))
            {
                ChestHooks.Reload(chest);
                if (ChestReserve.IsReserved(chest))
                {
                    Result.ChestsReserved++;
                    failed.Add(chest);
                    reservedMeanwhile = true;
                    continue;
                }
                ChestView view = View(request, chest, centre);
                if (view == null)
                {
                    continue;
                }
                view.Shared = false;   // it's his now
                views.Add(view);
                chests.Add(chest);
            }
            // A friend's open chest deferred this round is still a source: planned from, never taken from here (his game
            // gives in the next round), so "Keep wearing mine" doesn't keep the piece it brings and the put-away makes its
            // room (R3, finding 4).
            foreach (ChestView shared in pendingShared)
            {
                if (shared.Chest is Container friends && !failed.Contains(friends) && !notOwner.Contains(friends))
                {
                    views.Add(shared);
                }
            }
            HashSet<Container> awayNow = Away();
            foreach (Container chest in nearby)
            {
                if (!held.Held.Contains(chest))
                {
                    awayNow.Add(chest);
                }
            }
            EquipPlan plan = PlanRouted(views, chests, awayNow, out List<Planned> puts);
            LastPlan = plan;
            var routed = new HashSet<ItemDrop.ItemData>();
            foreach (Planned put in puts)
            {
                routed.Add(put.Away.Item);
            }
            var goesAway = new Dictionary<ItemDrop.ItemData, Away>();
            foreach (Away goes in plan.Aways)
            {
                goesAway[goes.Item] = goes;
            }
            var answering = new HashSet<ItemDrop.ItemData>();
            foreach (Want want in plan.Wants)
            {
                foreach (Use use in want.Have)
                {
                    answering.Add(use.Item);
                }
            }
            var cameOff = new List<ItemDrop.ItemData>();
            var changed = new List<Container>();
            var moved = new Dictionary<Away, int>();
            Inventory inventory = wearer.Inventory;
            try
            {
                // Nothing leaves him, or comes off, while the game still has something queued to go on or off: an equip
                // queued before he pressed Equip would undo the loadout seconds later (the fresh read's rule 7; R1, 15).
                foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
                {
                    if (wearer.Queued(item))
                    {
                        wearer.ClearQueued(item);
                    }
                }
                foreach (ItemDrop.ItemData item in plan.TakeOff)
                {
                    // What doesn't go away comes off whatever the chests do: it stays with him (the request), or the
                    // loadout carries it unworn (R2, finding 3). With "What I wear that the loadout doesn't" at ComesOff
                    // (card B18), what goes away comes off too when no chest takes it, and stays with him.
                    bool replaced = Replaced(plan, item);
                    bool goes = goesAway.ContainsKey(item);
                    bool offWithoutChest = rules.OffWithoutChestFor(item);
                    if (ComesOff.InExchange(goes, routed.Contains(item), replaced, offWithoutChest))
                    {
                        if (goes)
                        {
                            OffToGo.Add(item);   // a later round still puts it away as the worn piece it was
                        }
                        WornAs was = wearer.WornAs(item);
                        if (was == WornAs.None)
                        {
                            was = ReferenceEquals(item, wearer.HiddenRight) ? WornAs.RightHand : ReferenceEquals(item, wearer.HiddenLeft) ? WornAs.LeftHand : WornAs.None;
                        }
                        wearer.Unequip(item, rules.Effects);
                        StayOn.Remove(item);
                        if (was == WornAs.RightHand || was == WornAs.LeftHand)
                        {
                            OffHands[item] = was;
                        }
                        if (ComesOff.BackOnIfStillHere(goes, replaced, offWithoutChest))
                        {
                            cameOff.Add(item);
                        }
                        // What he took off and keeps, for a caller hanging it up (all of it, or the pieces the request
                        // keeps: a stand's going home, job 30); never what the loadout itself carries.
                        if (request.Keeps(item) && !answering.Contains(item) && !Result.TakenOff.Contains(item))
                        {
                            Result.TakenOff.Add(item);
                        }
                    }
                    else
                    {
                        StayOn.Add(item);   // no chest takes it, and nothing takes its place: it stays on
                    }
                }

                // Put away first, so the takes have the cells.
                foreach (Planned put in puts)
                {
                    if (StayOn.Contains(put.Away.Item))
                    {
                        continue;
                    }
                    bool marked = !changed.Contains(put.Chest);
                    if (marked)
                    {
                        changed.Add(put.Chest);
                    }
                    int stored = ChestPut.Put(put.Chest, inventory, put.Away.Item, put.Amount, put.TopUpOnly, useReserved: false);
                    if (stored > 0)
                    {
                        Result.PutAway += stored;
                        moved.TryGetValue(put.Away, out int already);
                        moved[put.Away] = already + stored;
                    }
                    else if (marked)
                    {
                        changed.Remove(put.Chest);
                    }
                }
                foreach (Take take in plan.Takes)
                {
                    if (take.Chest.Shared)
                    {
                        continue;   // given through the friend's game, before this frame
                    }
                    var chest = (Container)take.Chest.Chest;
                    bool marked = !changed.Contains(chest);
                    if (marked)
                    {
                        changed.Add(chest);
                    }
                    int took = TakeFrom(chest, take, inventory);
                    if (took > 0)
                    {
                        Result.Fetched += took;
                    }
                    else if (marked)
                    {
                        changed.Remove(chest);
                    }
                }

                // What came off to go away, or for something else to take its place, and is still with him (no chest took
                // it, or what was to replace it didn't come) goes back on where nothing else went: dressing puts it on, a
                // hand item through its hand (R1, finding 14; R3, finding 2).
                foreach (ItemDrop.ItemData item in cameOff)
                {
                    if (inventory.ContainsItem(item))   // all of it, or what's left of a stack of arrows
                    {
                        StayOn.Add(item);
                    }
                }
            }
            finally
            {
                // Send the changed chests to the other players now, and hold them here for a few seconds, until every
                // friend's copy has reloaded: even if another mod's handler threw in the middle of a move.
                foreach (Container chest in changed)
                {
                    try   // never throws here: a throw would hide the error that brought it
                    {
                        ChestClaims.Hold(chest);
                        ZDOMan.instance.ForceSendZDO(ChestHooks.NetView(chest).GetZDO().m_uid);
                    }
                    catch (Exception error)
                    {
                        Module.Warn($"A chest Outfitter put into or took from couldn't be sent or held: {error}");
                    }
                }
            }

            // What was to go away and stayed with him, by this round (no chest had room, or none takes it); what a later
            // round puts away leaves the list (R1, finding 13).
            Result.StayedWithHim.Clear();
            bool awaysLeft = false;
            foreach (Away goes in plan.Aways)
            {
                moved.TryGetValue(goes, out int went);
                if (went < goes.Amount && inventory.ContainsItem(goes.Item))
                {
                    awaysLeft = true;
                    if (!Result.StayedWithHim.Contains(goes.Item))
                    {
                        Result.StayedWithHim.Add(goes.Item);
                    }
                }
            }
            settled(this);   // the equip dresses him now

            bool deferred = sharesDeferred;
            sharesDeferred = false;
            bool friendsToGive = deferred && plan.Takes.Exists(take => take.Chest.Shared);
            pendingShared = new List<ChestView>();
            if ((plan.MissingCount > 0 || awaysLeft || friendsToGive) && (anyFailed || reservedMeanwhile || sharesFailed || deferred))
            {
                NextRound();
            }
            else
            {
                Finish();
            }
        }

        // Whether something else goes into the place this worn item is in: a body place the loadout fills with another
        // piece, or a hand whose items change (the hands are emptied, then filled, when they change).
        private bool Replaced(EquipPlan plan, ItemDrop.ItemData item)
        {
            WornAs place = wearer.WornAs(item);
            if (place == WornAs.None)
            {
                place = ReferenceEquals(item, wearer.HiddenRight) ? WornAs.RightHand : ReferenceEquals(item, wearer.HiddenLeft) ? WornAs.LeftHand : WornAs.None;
            }
            if (place == WornAs.RightHand || place == WornAs.LeftHand)
            {
                return plan.HandsChange && HandFilled(plan, place, item);
            }
            foreach (Want want in plan.Wants)
            {
                if (want.Worn != place || want.HaveCount + want.TakeCount <= 0)
                {
                    continue;
                }
                foreach (Use use in want.Have)
                {
                    if (!ReferenceEquals(use.Item, item))
                    {
                        return true;
                    }
                }
                if (want.TakeCount > 0)
                {
                    return true;
                }
            }
            return false;
        }

        // Whether the hands' change leaves this held item out of its hand: the loadout puts another item into that hand, or
        // into the other one an item the game won't hold beside it (a two-handed weapon, a bow or a staff with anything, a
        // shield beside a two-handed weapon or a tool, a second torch: Dress.Beside; R2, finding 1).
        private static bool HandFilled(EquipPlan plan, WornAs hand, ItemDrop.ItemData item)
        {
            foreach (Want want in plan.Wants)
            {
                if ((want.Worn != WornAs.RightHand && want.Worn != WornAs.LeftHand) || want.HaveCount + want.TakeCount <= 0)
                {
                    continue;
                }
                bool other = false;
                foreach (Use use in want.Have)
                {
                    other |= !ReferenceEquals(use.Item, item);
                }
                if (!(other || want.TakeCount > 0))
                {
                    continue;
                }
                if (want.Worn == hand)
                {
                    return true;
                }
                ItemDrop.ItemData.ItemType mine = item.m_shared.m_itemType, theirs = want.Template.m_shared.m_itemType;
                if (!(hand == WornAs.LeftHand ? Dress.Beside(theirs, mine) : Dress.Beside(mine, theirs)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>An item that empties both hands when put on (the game's EquipItem: a two-handed weapon, a bow, a
        /// left-handed staff, a tool).</summary>
        internal static bool BothHands(ItemDrop.ItemData.ItemType type) =>
            type == ItemDrop.ItemData.ItemType.TwoHandedWeapon || type == ItemDrop.ItemData.ItemType.Bow ||
            type == ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft || type == ItemDrop.ItemData.ItemType.Tool;

        // One take: a piece of gear by its very stack (found again by its fields now the chest is reloaded, else the best
        // copy left that answers), a stackable item by key, leaving one by the setting.
        private int TakeFrom(Container chest, Take take, Inventory inventory)
        {
            if (take.Want.Gear)
            {
                ItemDrop.ItemData copy = FindCopy(chest.GetInventory(), take);
                if (copy == null)
                {
                    return 0;
                }
                return ChestStock.Take(chest, inventory, EquipPlanner.KeyOf(copy), 1, leaveOne: false, useReserved: false,
                                       spare: stack => !ReferenceEquals(stack, copy));
            }
            return ChestStock.Take(chest, inventory, take.Key, take.Amount, rules.LeaveOne, useReserved: false);
        }

        private ItemDrop.ItemData FindCopy(Inventory contents, Take take)
        {
            ItemDrop.ItemData chosen = take.Copy;
            ItemDrop.ItemData best = null;
            var input = new PlanInput { PrefabOf = Capture.PrefabOf };   // all SameKind reads
            foreach (ItemDrop.ItemData item in contents.GetAllItems())
            {
                if (item.m_stack <= 0 || item.m_worldLevel < rules.WorldLevel || !EquipPlanner.SameKind(input, item, take.Want) ||
                    !Matching.Answers(item, take.Want.First, rules) || (take.Want.Worn != WornAs.None && Matching.Broken(item)))
                {
                    continue;
                }
                if (chosen != null && SameCopy(item, chosen))
                {
                    return item;
                }
                if (best == null || Matching.Compare(item, best, rules) < 0)
                {
                    best = item;
                }
            }
            return best;
        }

        // The same copy after a reload: its fields, as the game saves them (durability to a hundredth).
        private static bool SameCopy(ItemDrop.ItemData a, ItemDrop.ItemData b) =>
            ReferenceEquals(a, b) ||
            (a.m_quality == b.m_quality && a.m_variant == b.m_variant && a.m_worldLevel == b.m_worldLevel && a.m_crafterID == b.m_crafterID &&
             a.m_gridPos.x == b.m_gridPos.x && a.m_gridPos.y == b.m_gridPos.y && Math.Abs(a.m_durability - b.m_durability) < 0.011f &&
             a.m_shared.m_name == b.m_shared.m_name);

        private void Finish()
        {
            Over = true;
        }

        /// <summary>The plan's input, his inventory as it is now and these chests.</summary>
        internal PlanInput Input(List<ChestView> views)
        {
            var input = new PlanInput
            {
                Loadout = request.Loadout,
                Rules = rules,
                Grid = wearer.Grid,
                Inventory = wearer.Inventory,
                WornAs = wearer.WornAs,
                HiddenRight = wearer.HiddenRight,
                HiddenLeft = wearer.HiddenLeft,
                Favourite = FavouriteOf(),
                PrefabOf = Capture.PrefabOf,
                Template = Codes.Template,
                TakenOff = request.TakenOff,
                KeepsTakenOff = request.KeepsTakenOff,
                Stays = request.Stays,
                KeptEarlier = Result.TakenOff,
                OffToGo = OffToGo,
                FoodsChosen = foods,
            };
            input.Chests.AddRange(views);
            return input;
        }

        // The food each of the loadout's food slots takes, as this equip's first plan chose it from every chest nearby (the
        // question he answered says it): its plans once the chests are claimed (which see only those), in later rounds and
        // for dressing him (his inventory alone) fill each slot with the same food, so it is fetched, kept and arranged as
        // one; a slot whose food then can't be had is missing, as any item (card A34, decision 72).
        private Dictionary<LoadoutItem, FoodChoice> foods;

        /// <summary>Which of his items are favourites (the item marks part); the scratch harness sets another. While the
        /// favourites' file can't be read, every item counts as one: nothing that might be a favourite goes away
        /// (MODULES.md, the item marks: a module acting on "not marked" checks Readable first).</summary>
        internal static Func<Func<ItemDrop.ItemData, bool>> FavouriteOf = () =>
            Favorites.List.Readable ? (Func<ItemDrop.ItemData, bool>)Favorites.IsFavorite : Everything;

        private static bool Everything(ItemDrop.ItemData item) => true;

        /// <summary>A chest as a plan sees it, or null when someone else has it open and his game can't share it.</summary>
        internal static ChestView View(EquipRequest request, Container chest, Vector3 centre)
        {
            bool openElsewhere = ChestFinder.OpenElsewhere(chest);
            if (openElsewhere && !ChestShare.CanShare(chest))
            {
                return null;
            }
            Vector3 at = ChestFinder.Where(chest);
            return new ChestView
            {
                Chest = chest,
                Contents = chest.GetInventory(),
                Distance = Vector3.Distance(at, centre),
                Near = request.NearFirst.HasValue && Vector3.Distance(at, request.NearFirst.Value) <= request.NearRange,
                Shared = openElsewhere,
                Fit = Setups.Reading(chest),
            };
        }

        /// <summary>One put a route plans: that much of one of his stacks into one chest.</summary>
        internal sealed class Planned
        {
            internal Container Chest;
            internal Away Away;
            internal int Amount;
            internal bool TopUpOnly;
        }

        /// <summary>Where what goes away goes: the quick stack's own routing over these chests (the set-up chests first,
        /// then those holding it, then, by the setting, any chest with room, nearest first), and its planning by counts,
        /// for exactly the amounts the plan sends away.</summary>
        internal List<Planned> Route(EquipPlan plan, List<Container> chests, ICollection<Container> awayNow)
        {
            var planned = new List<Planned>();
            if (plan.Aways.Count == 0 || chests.Count == 0)
            {
                return planned;
            }
            var items = new List<ItemDrop.ItemData>();
            var models = new List<StackPlanner.Item>();
            foreach (Away goes in plan.Aways)
            {
                items.Add(goes.Item);
                models.Add(new StackPlanner.Item
                {
                    Name = goes.Item.m_shared.m_name,
                    Quality = goes.Item.m_quality,
                    WorldLevel = goes.Item.m_worldLevel,
                    Amount = goes.Amount,
                    MaxStack = goes.Item.m_shared.m_maxStackSize,
                });
            }
            var contents = new List<Inventory>();
            var chestModels = new List<StackPlanner.Chest>();
            foreach (Container chest in chests)
            {
                Inventory inventory = chest.GetInventory();
                contents.Add(inventory);
                var model = new StackPlanner.Chest { Cells = inventory.GetWidth() * inventory.GetHeight() };
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    model.Stacks.Add(new StackPlanner.Item
                    {
                        Name = item.m_shared.m_name,
                        Quality = item.m_quality,
                        WorldLevel = item.m_worldLevel,
                        Amount = item.m_stack,
                        MaxStack = item.m_shared.m_maxStackSize,
                    });
                }
                chestModels.Add(model);
            }
            StackRules putRules = PutRules();
            List<IList<IList<int>>> tiers = StackRouting.Tiers(items, chests, contents, putRules, awayNow)
                .ConvertAll(each => (IList<IList<int>>)each.ConvertAll(tier => (IList<int>)tier));
            foreach (StackPlanner.Put put in StackPlanner.Plan(models, chestModels, i => tiers[i], putRules.TopUpFirst))
            {
                planned.Add(new Planned { Chest = chests[put.Chest], Away = plan.Aways[put.Item], Amount = put.Amount, TopUpOnly = put.TopUpOnly });
            }
            return planned;
        }

        /// <summary>The quick stack's rules as Outfitter's settings say for putting away.</summary>
        internal static StackRules PutRules() => new StackRules
        {
            Chests = Settings.ChestRules(),
            Trophies = TrophyRule.AnyTrophyChest,
            OnlyWhereHeld = !Settings.PutAnyChest.Value,
            TopUpFirst = true,
            LeaveOthersMarked = Settings.LeaveOthersMarked.Value,
            Setups = Settings.PutSetUpFirst.Value
                ? new SetupRules
                {
                    Of = Setups.Reading,
                    KeepHeld = Settings.KeepHeld.Value,
                    Overflow = Settings.PutOverflow.Value,
                    Others = Settings.PutOthers.Value,
                }
                : null,
            AllRows = true,
        };

        private HashSet<Container> Away()
        {
            var all = new HashSet<Container>(away);
            foreach (Container chest in ChestFinder.WaitedOn(request.Centre ?? wearer.Position, Settings.ChestRules()))
            {
                all.Add(chest);
            }
            return all;
        }

        private List<Container> NearestFirst(List<Container> held)
        {
            var ordered = new List<Container>(held.Count);
            foreach (Container chest in nearby)
            {
                if (held.Contains(chest) && !ordered.Contains(chest))
                {
                    ordered.Add(chest);
                }
            }
            foreach (Container chest in held)
            {
                if (!ordered.Contains(chest))
                {
                    ordered.Add(chest);
                }
            }
            return ordered;
        }

        // A chest this computer owns now: no claim needed.
        // His now and changeable now: one claimed beyond the server's hand-out only once its newest copy is in and
        // between the server's rounds (ChestClaims.MayChange, card A64).
        private static bool Mine(Container chest) => ChestClaims.MayChange(chest);
    }
}
