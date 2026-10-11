// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;

namespace QoLMods.Chests
{
    /// <summary>What a quick stack did.</summary>
    internal sealed class StackResult
    {
        /// <summary>How many items went into chests, in all. His inventory lost exactly this.</summary>
        internal int Moved;

        /// <summary>What went, by the item's name ("$item_wood").</summary>
        internal readonly Dictionary<string, int> MovedByName = new Dictionary<string, int>();

        /// <summary>How many of them came from his hotbar (the inventory's top row).</summary>
        internal int FromHotbar;

        /// <summary>The chests that received something, in the order they first did.</summary>
        internal readonly List<Container> ChestsUsed = new List<Container>();

        /// <summary>What went into each chest that received something, by the item's name ("$item_wood").</summary>
        internal readonly Dictionary<Container, Dictionary<string, int>> MovedInto = new Dictionary<Container, Dictionary<string, int>>();

        /// <summary>Chests that would have taken something (they hold one of his items, their setup takes one, or they hold
        /// a trophy for his trophy) but someone else had open (or opened meanwhile).</summary>
        internal int ChestsInUse;

        /// <summary>Chests whose owner's game didn't answer in time.</summary>
        internal int ChestsNoAnswer;

        /// <summary>Chests that would have taken something but that this game can't get at all now (ChestClaims.CantClaim:
        /// beyond the server's reach with no way to claim them), left out rather than asked (card A69).</summary>
        internal int ChestsOutOfReach;

        /// <summary>Chests that turned out reserved (Reserve Stock's mark) once they were his.</summary>
        internal int ChestsReserved;

        /// <summary>None of his items was chosen by the rules (nothing to put away).</summary>
        internal bool NoItems;

        /// <summary>No chest took any of the items chosen (none holds them, or none has room).</summary>
        internal bool NoChests => Moved == 0 && !NoItems;
    }

    /// <summary>
    /// A quick stack: his items into the chests near him, by the rules (card A52), as safely in multiplayer as a take
    /// (ChestFetch). It plans from the copies this computer has, claims the chests the plan uses (ChestClaims; a chest he
    /// owns already needs nothing, so a stack that only touches those happens in the frame it's asked for), and in the frame
    /// they are his, reloads each, reads its reserve mark again, chooses his items again, plans again from what the chests
    /// really hold, nearest first as before, and puts (ChestPut: into the chest first, measured, then out of his stack). Every chest it changed is
    /// sent at once and held for a few seconds, in a finally (ChestClaims.Hold), so a friend's copy catches up before
    /// anyone opens it. A chest someone else has open is skipped (the game never hands an open chest over), and so are
    /// gravestones, the world's own chests and ships someone else is aboard (ChestFinder.WhyNot). What a skipped chest was
    /// meant to take is planned again among the others, for up to three rounds.
    /// </summary>
    internal static class ChestStacker
    {
        private const int Rounds = 3;

        /// <summary>The words for an error during a put, in the module's warning.</summary>
        internal const string Task = "putting items into chests";

        /// <summary>
        /// Starts a quick stack for <paramref name="user"/> under <paramref name="rules"/>: into <paramref name="only"/>
        /// (Place stacks: the chest he has open), or null for every chest near him the rules allow. <paramref name="done"/>
        /// gets the result inside the user's Guard, in this frame when every chest the plan uses is his already, else a
        /// fraction of a second later. Returns false, doing nothing, only when the plan needs chests claimed from other
        /// players while another claim of the service is under way: ask again a moment later.
        /// </summary>
        internal static bool Start(ChestUser user, StackRules rules, IList<Container> only, Action<StackResult> done)
        {
            var stack = new Stack(user, rules, only, done);
            return stack.NextRound(first: true);
        }

        private sealed class Stack
        {
            private readonly ChestUser user;
            private readonly StackRules rules;
            private readonly List<Container> only;
            private readonly Action<StackResult> done;
            private readonly StackResult result = new StackResult();
            private readonly HashSet<Container> failed = new HashSet<Container>();

            // The chests this stack couldn't have (someone else has one open, its owner didn't answer): a set-up one among
            // them is still the home of what its setup takes (StackRouting.Tiers' away), in every plan from the moment it's
            // known. All of them are in failed too, so never asked for again: never a chest later found reserved.
            private readonly HashSet<Container> away = new HashSet<Container>();

            // The chests another player is about to open (ChestFinder.WaitedOn), found afresh each round: homes too, while
            // they last. Never a reserved one.
            private readonly HashSet<Container> waited = new HashSet<Container>();
            private int round;

            internal Stack(ChestUser user, StackRules rules, IList<Container> only, Action<StackResult> done)
            {
                this.user = user;
                this.rules = rules;
                if (only != null)
                {
                    this.only = new List<Container>();
                    foreach (Container chest in only)
                    {
                        if (!this.only.Contains(chest))
                        {
                            this.only.Add(chest);   // each once, in the order given
                        }
                    }
                }
                this.done = done;
            }

            // Plans a round from this computer's copies and claims what it uses. False only when the first round needs a
            // claim while another is under way (nothing done then).
            internal bool NextRound(bool first)
            {
                Player player = Player.m_localPlayer;
                if (player == null || !user.IsOn || round >= Rounds)
                {
                    Finish();
                    return true;
                }
                List<ItemDrop.ItemData> items = StackItems.Choose(player, rules);
                if (items.Count == 0)
                {
                    result.NoItems = first;
                    Finish();
                    return true;
                }
                List<Container> chests = Nearby(player, items);
                lastNearby = chests;
                List<Container> planned = Planned(items, chests);
                if (planned.Count == 0)
                {
                    Finish();
                    return true;
                }
                bool allMine = planned.TrueForAll(Mine);
                if (!allMine && ChestClaims.Busy)
                {
                    if (first)
                    {
                        return false;   // asked again by the module a moment later: nothing has changed
                    }
                    Finish();
                    return true;
                }
                round++;
                lastAsked = planned;
                if (allMine)
                {
                    // His own chests need no claim, so this happens now, even while another module's claim is under way.
                    var mine = new ClaimResult();
                    mine.Held.AddRange(planned);
                    user.Guard(Task, () => Settled(mine));
                    return true;
                }
                if (!ChestClaims.Start(user, planned, null, null, Settled, Task))
                {
                    if (first)
                    {
                        round--;
                        return false;
                    }
                    Finish();
                }
                return true;
            }

            // The chests to plan with: the ones asked for, or those near him that the rules allow, nearest first, less those
            // this stack failed to get; a chest someone else has open is counted, if it would have taken something, and left
            // out.
            private List<Container> Nearby(Player player, List<ItemDrop.ItemData> items)
            {
                List<Container> found = only ?? ChestFinder.Find(ChestFinder.Where(player), rules.Chests);
                var chests = new List<Container>();
                foreach (Container chest in found)
                {
                    if (chest == null || failed.Contains(chest))
                    {
                        continue;
                    }
                    ZNetView view = ChestHooks.NetView(chest);
                    if (view == null || !view.IsValid())
                    {
                        continue;
                    }
                    if (ChestFinder.OpenElsewhere(chest))
                    {
                        // The game never hands an open chest to anyone else, and a friend's open chest can't be put into
                        // through his game either (ChestShare only takes).
                        if (round == 0 && WouldTake(chest, items))
                        {
                            result.ChestsInUse++;
                        }
                        failed.Add(chest);
                        away.Add(chest);
                        continue;
                    }
                    if (ChestClaims.CantClaim(chest, user))
                    {
                        // This game can't get it at all now (card A69): asked, it would only wait out its time. Like an
                        // open chest, it stays the home of what it would take.
                        if (round == 0 && WouldTake(chest, items))
                        {
                            result.ChestsOutOfReach++;
                        }
                        failed.Add(chest);
                        away.Add(chest);
                        continue;
                    }
                    chests.Add(chest);
                }
                waited.Clear();
                if (only == null && rules.Setups != null)
                {
                    // Chests the finder leaves alone for a moment, since another player asked to open them: homes of what
                    // their setups take (only setups make a chest away from a home), counted in use as an open one is.
                    foreach (Container chest in ChestFinder.WaitedOn(ChestFinder.Where(player), rules.Chests))
                    {
                        waited.Add(chest);
                        if (round == 0 && WouldTake(chest, items))
                        {
                            result.ChestsInUse++;
                        }
                    }
                }
                return chests;
            }

            // Whether a chest would have been meant for one of these items: it holds one, its setup takes one, or it holds a
            // trophy for a trophy (any rank below AnyChest). A chest that would only take them as any chest ("only where
            // held" off) isn't counted, or every open chest nearby would be.
            private bool WouldTake(Container chest, List<ItemDrop.ItemData> items)
            {
                Inventory contents = chest.GetInventory();
                foreach (ItemDrop.ItemData item in items)
                {
                    int rank = StackRouting.Rank(chest, contents, item, rules);
                    if (rank >= 0 && rank < StackRouting.AnyChest)
                    {
                        return true;
                    }
                }
                return false;
            }

            // The chests a plan over these copies puts something into.
            private List<Container> Planned(List<ItemDrop.ItemData> items, List<Container> chests)
            {
                var planned = new List<Container>();
                foreach (StackPlanner.Put put in Plan(items, chests, Away()))
                {
                    if (!planned.Contains(chests[put.Chest]))
                    {
                        planned.Add(chests[put.Chest]);
                    }
                }
                return planned;
            }

            // The chests a plan can't use that may still be homes: those this stack couldn't have, and those waited on.
            private HashSet<Container> Away()
            {
                var all = new HashSet<Container>(away);
                all.UnionWith(waited);
                return all;
            }

            // A plan over these chests; awayNow are the chests it can't use whose setups still make them homes (Tiers).
            private List<StackPlanner.Put> Plan(List<ItemDrop.ItemData> items, List<Container> chests, ICollection<Container> awayNow)
            {
                var contents = new List<Inventory>(chests.Count);
                var models = new List<StackPlanner.Chest>(chests.Count);
                foreach (Container chest in chests)
                {
                    Inventory inventory = chest.GetInventory();
                    contents.Add(inventory);
                    models.Add(Model(inventory));
                }
                var stacks = new List<StackPlanner.Item>(items.Count);
                foreach (ItemDrop.ItemData item in items)
                {
                    stacks.Add(Model(item));
                }
                // Each item's chests in tiers, as the caller's order says (StackRules.Order; by default the set-up chests that
                // take it are filled before the rest, card A41). A set-up chest this plan can't use (open elsewhere, no answer,
                // full when the chests were asked for) still counts as the home of what it takes.
                List<IList<IList<int>>> tiers = StackRouting.Tiers(items, chests, contents, rules, awayNow)
                    .ConvertAll(each => (IList<IList<int>>)each.ConvertAll(tier => (IList<int>)tier));
                return StackPlanner.Plan(stacks, models, i => tiers[i], rules.TopUpFirst);
            }

            // In the frame the claim settled: every chest in held.Held is this computer's now.
            private void Settled(ClaimResult held)
            {
                result.ChestsInUse += held.InUse;
                result.ChestsNoAnswer += held.NoAnswer;
                bool anyFailed = held.InUse + held.NoAnswer + held.Gone > 0;
                if (anyFailed)
                {
                    // Known now, before the plan below: a set-up chest asked for and not had (opened meanwhile, no answer)
                    // stays the home of what it takes, so with Overflow off that stays with him. A chest gone is Unity's
                    // null, and never a home.
                    foreach (Container chest in AskedButNotHeld(held))
                    {
                        failed.Add(chest);
                        away.Add(chest);
                    }
                }
                Player player = Player.m_localPlayer;
                if (player == null || !user.IsOn || player.IsTeleporting())
                {
                    Finish();   // switched off, or gone, while waiting: put nothing
                    return;
                }

                // Plan again from what the chests really hold now, and from what he carries now, nearest first as Nearby found
                // them (a claim hands them back in the order the plan first used them). A chest reserved meanwhile (its mark
                // came with the ownership) takes nothing, unless the rules put into reserved chests.
                var chests = new List<Container>();
                bool reservedMeanwhile = false;
                foreach (Container chest in NearestFirst(held.Held))
                {
                    ChestHooks.Reload(chest);
                    if (!rules.Chests.UseReserved && ChestReserve.IsReserved(chest))
                    {
                        result.ChestsReserved++;
                        failed.Add(chest);   // never away: a reserved chest is no one's home, as the finder leaves it out
                        reservedMeanwhile = true;
                        continue;
                    }
                    chests.Add(chest);
                }

                // Every chest found nearby that this plan can't use counts as away for it: the claim asked only for the
                // chests the first plan put into, so a set-up chest that plan found full (or had nothing for) is missing
                // here, and is still the home of what it takes. Never one reserved meanwhile: it's among those held.
                HashSet<Container> awayNow = Away();
                foreach (Container chest in lastNearby)
                {
                    if (!held.Held.Contains(chest))
                    {
                        awayNow.Add(chest);
                    }
                }
                var changed = new List<Container>();
                try
                {
                    List<ItemDrop.ItemData> items = StackItems.Choose(player, rules);
                    Inventory inventory = player.GetInventory();
                    foreach (StackPlanner.Put put in Plan(items, chests, awayNow))
                    {
                        Container chest = chests[put.Chest];
                        ItemDrop.ItemData item = items[put.Item];
                        string name = item.m_shared.m_name;
                        bool fromHotbar = item.m_gridPos.y == 0;
                        // Marked first: if another mod's handler throws inside the put, the chest has changed, and the
                        // finally below still sends and holds it.
                        bool marked = !changed.Contains(chest);
                        if (marked)
                        {
                            changed.Add(chest);
                        }
                        int moved = ChestPut.Put(chest, inventory, item, put.Amount, put.TopUpOnly, rules.Chests.UseReserved);
                        if (moved > 0)
                        {
                            result.Moved += moved;
                            result.MovedByName.TryGetValue(name, out int sum);
                            result.MovedByName[name] = sum + moved;
                            if (fromHotbar)
                            {
                                result.FromHotbar += moved;
                            }
                            if (!result.ChestsUsed.Contains(chest))
                            {
                                result.ChestsUsed.Add(chest);
                            }
                            if (!result.MovedInto.TryGetValue(chest, out Dictionary<string, int> into))
                            {
                                result.MovedInto[chest] = into = new Dictionary<string, int>();
                            }
                            into.TryGetValue(name, out int already);
                            into[name] = already + moved;
                        }
                        else if (marked)
                        {
                            changed.Remove(chest);
                        }
                    }
                }
                finally
                {
                    // Send the changed chests to the other players now rather than at the game's next turn, and hold them
                    // here for a few seconds, until every friend's copy has reloaded: even if another mod's handler threw
                    // in the middle of a put.
                    foreach (Container chest in changed)
                    {
                        try   // never throws here: a throw would hide the error that brought it
                        {
                            ChestClaims.Hold(chest);   // first: holding needs nothing from the network
                            ZDOMan.instance.ForceSendZDO(ChestHooks.NetView(chest).GetZDO().m_uid);
                        }
                        catch (Exception error)
                        {
                            user.Log.LogWarning($"[QoL Mods chest service] A chest put into couldn't be sent or held: {error}");
                        }
                    }
                }

                // What a chest that couldn't be had (or was reserved meanwhile) was meant to take may fit elsewhere: plan
                // again without it.
                if (anyFailed || reservedMeanwhile)
                {
                    NextRound(first: false);
                }
                else
                {
                    Finish();
                }
            }

            // The chests a claim asked for that it didn't get: the planned ones not in its Held.
            private IEnumerable<Container> AskedButNotHeld(ClaimResult held)
            {
                foreach (Container chest in lastAsked)
                {
                    if (!held.Held.Contains(chest))
                    {
                        yield return chest;
                    }
                }
            }

            // The chests the last round planned for (and claimed, when not his already).
            private List<Container> lastAsked = new List<Container>();

            // The chests the last round planned with, nearest first.
            private List<Container> lastNearby = new List<Container>();

            // The chests a claim holds, in the nearest-first order the last round planned with.
            private List<Container> NearestFirst(List<Container> held)
            {
                var ordered = new List<Container>(held.Count);
                foreach (Container chest in lastNearby)
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
                        ordered.Add(chest);   // never: a claim holds only chests the plan chose from lastNearby
                    }
                }
                return ordered;
            }

            private bool finished;

            private void Finish()
            {
                if (finished)
                {
                    return;
                }
                finished = true;
                done(result);
            }

            // His now and changeable now: one claimed beyond the server's hand-out only once its newest copy is in and
            // between the server's rounds (ChestClaims.MayChange, card A64).
            private static bool Mine(Container chest) => ChestClaims.MayChange(chest);

            private static StackPlanner.Item Model(ItemDrop.ItemData item) => new StackPlanner.Item
            {
                Name = item.m_shared.m_name,
                Quality = item.m_quality,
                WorldLevel = item.m_worldLevel,
                Amount = item.m_stack,
                MaxStack = item.m_shared.m_maxStackSize,
            };

            private static StackPlanner.Chest Model(Inventory inventory)
            {
                var chest = new StackPlanner.Chest { Cells = inventory.GetWidth() * inventory.GetHeight() };
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    chest.Stacks.Add(Model(item));
                }
                return chest;
            }

        }
    }
}
