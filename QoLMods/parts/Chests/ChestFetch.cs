// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Linq;

namespace QoLMods.Chests
{
    /// <summary>What a fetch did.</summary>
    internal sealed class FetchResult
    {
        /// <summary>What was asked for, by item key (ChestStock: a name for any quality, or name@quality).</summary>
        internal readonly Dictionary<string, int> Wanted = new Dictionary<string, int>();

        /// <summary>What arrived in his inventory, by item key. The chests lost exactly this. What a friend's open chest gave
        /// that didn't fit lies at his feet instead (Dropped), and is counted here too.</summary>
        internal readonly Dictionary<string, int> Moved = new Dictionary<string, int>();

        /// <summary>How many of what friends' open chests gave didn't fit in his inventory and were dropped at his feet.</summary>
        internal int Dropped;

        /// <summary>Chests skipped because a friend had them open.</summary>
        internal int ChestsInUse;

        /// <summary>Chests skipped because their owner's game didn't answer in time.</summary>
        internal int ChestsNoAnswer;

        /// <summary>Chests that would have given something but that this game can't get at all now (ChestClaims.CantClaim:
        /// beyond the server's reach with no way to claim them), left out rather than asked (card A69).</summary>
        internal int ChestsOutOfReach;

        /// <summary>Chests skipped because they turned out reserved (Reserve Stock's mark) once they were his.</summary>
        internal int ChestsReserved;

        /// <summary>Chests that were taken from (friends' open chests among them).</summary>
        internal int ChestsUsed;

        /// <summary>Of those, chests a friend had open, taken from through his game (ChestShare).</summary>
        internal int ChestsShared;

        /// <summary>His inventory ran out of room for something.</summary>
        internal bool InventoryFull;

        internal int MovedOf(string item) => Moved.TryGetValue(item, out int moved) ? moved : 0;

        internal int ShortOf(string item) => (Wanted.TryGetValue(item, out int wanted) ? wanted : 0) - MovedOf(item);
    }

    /// <summary>
    /// Takes items from the chests near the player into his inventory, safely in multiplayer: it plans from the counts
    /// it can see, claims the chests in the plan (ChestClaims), reloads each one it now owns, plans again from what they
    /// really hold, and moves it, all in one frame. A chest a friend on the pack has open is taken from through his game
    /// instead (ChestShare), in the same round: what it gives lands in his inventory before the round settles. What a
    /// skipped chest couldn't give is looked for in other chests, for up to three rounds. One fetch at a time.
    /// </summary>
    internal static class ChestFetch
    {
        private const int Rounds = 3;

        /// <summary>True while a fetch (or any claim of the chest service) is under way.</summary>
        internal static bool Busy => ChestClaims.Busy;

        /// <summary>
        /// Starts fetching up to <paramref name="wanted"/> of each item, by key. <paramref name="done"/> gets the result inside the
        /// user's Guard, usually a fraction of a second later. Returns false, doing nothing, while one is under way.
        /// </summary>
        internal static bool Start(ChestUser user, IDictionary<string, int> wanted, ChestRules rules, Action<FetchResult> done)
        {
            if (Busy)
            {
                return false;
            }
            new Fetch(user, wanted, rules, done).NextRound();
            return true;
        }

        private sealed class Fetch
        {
            private readonly ChestUser user;
            private readonly ChestRules rules;
            private readonly Action<FetchResult> done;
            private readonly FetchResult result = new FetchResult();
            private readonly Dictionary<string, int> left = new Dictionary<string, int>();
            private readonly HashSet<Container> tried = new HashSet<Container>();
            private readonly HashSet<Container> used = new HashSet<Container>();
            private int shared;
            private int round;

            internal Fetch(ChestUser user, IDictionary<string, int> wanted, ChestRules rules, Action<FetchResult> done)
            {
                this.user = user;
                this.rules = rules;
                this.done = done;
                foreach (KeyValuePair<string, int> want in wanted)
                {
                    if (want.Value > 0)
                    {
                        result.Wanted[want.Key] = want.Value;
                        left[want.Key] = want.Value;
                    }
                }
            }

            internal void NextRound()
            {
                Player player = Player.m_localPlayer;
                if (player == null || !user.IsOn || round >= Rounds || !AnythingLeft())
                {
                    Finish();
                    return;
                }
                round++;

                // Plan from the copies this computer has, to know which chests to claim, nearest first. Then a chest a friend
                // on the pack has open, taken from through his game. A chest whose record says someone else has it open is
                // asked all the same, but last: that mark can be stale, and its owner's answer is the truth, but a refusal
                // shouldn't use up a round that a free chest could fill.
                var chests = new List<Container>();
                var sharing = new List<Container>();
                var openElsewhere = new List<Container>();
                foreach (Container chest in ChestFinder.Find(ChestFinder.Where(player), rules))   // his place, as the finder sees it
                {
                    if (tried.Contains(chest))
                    {
                        continue;
                    }
                    if (ChestClaims.CantClaim(chest, user))
                    {
                        // Asked, it would only wait out its time (card A69): left out, and counted once if it holds
                        // something wanted.
                        tried.Add(chest);
                        if (Takeable(chest).Values.Any(amount => amount > 0))
                        {
                            result.ChestsOutOfReach++;
                        }
                        continue;
                    }
                    (!ChestFinder.OpenElsewhere(chest) ? chests : ChestShare.CanShare(chest) ? sharing : openElsewhere).Add(chest);
                }
                var sharedChests = new HashSet<Container>(sharing);
                chests.AddRange(sharing);
                chests.AddRange(openElsewhere);
                var stock = new List<Dictionary<string, int>>();
                foreach (Container chest in chests)
                {
                    stock.Add(Takeable(chest));
                }
                var claim = new List<Container>();
                var shares = new Dictionary<Container, ShareTake>();
                foreach (FetchPlanner.Take take in Plan(stock))
                {
                    Container chest = chests[take.Source];
                    if (sharedChests.Contains(chest))
                    {
                        if (!shares.TryGetValue(chest, out ShareTake share))
                        {
                            shares[chest] = share = new ShareTake(chest);
                        }
                        share.Add(take.Item, take.Amount);
                    }
                    else if (!claim.Contains(chest))
                    {
                        claim.Add(chest);
                    }
                }
                if (claim.Count == 0 && shares.Count == 0)
                {
                    Finish();
                    return;
                }
                tried.UnionWith(claim);
                tried.UnionWith(shares.Keys);
                ChestClaims.Start(user, claim, shares.Values, rules, TakeFromHeld);
            }

            // In the frame the claim settled: every chest in held.Held is this computer's now, and what friends' open chests
            // gave is in his inventory (or at his feet) already.
            private void TakeFromHeld(ClaimResult held)
            {
                result.ChestsInUse += held.InUse;
                result.ChestsNoAnswer += held.NoAnswer;
                foreach (ShareResult share in held.Shared)
                {
                    Shared(share);
                }
                Player player = Player.m_localPlayer;
                if (player == null || !user.IsOn)
                {
                    Finish();   // switched off while waiting: take nothing
                    return;
                }

                // Plan again from what the chests really hold now. A chest reserved meanwhile (its mark came with the
                // ownership) gives nothing, unless the rules use reserved chests like any other.
                var stock = new List<Dictionary<string, int>>();
                foreach (Container chest in held.Held)
                {
                    ChestHooks.Reload(chest);
                    if (!rules.UseReserved && ChestReserve.IsReserved(chest))
                    {
                        result.ChestsReserved++;
                        stock.Add(new Dictionary<string, int>());
                    }
                    else
                    {
                        stock.Add(Takeable(chest));
                    }
                }
                Inventory inventory = player.GetInventory();
                try
                {
                    foreach (FetchPlanner.Take take in Plan(stock))
                    {
                        Container chest = held.Held[take.Source];
                        // Marked first: if another mod's handler throws inside the take, the chest has changed, and the
                        // finally below still sends and holds it.
                        bool marked = used.Add(chest);
                        int moved = ChestStock.Take(chest, inventory, take.Item, take.Amount, rules.LeaveOne, rules.UseReserved,
                                                    gearToo: rules.LeaveOneOfGear);
                        if (moved > 0)
                        {
                            left[take.Item] -= moved;
                            result.Moved[take.Item] = result.MovedOf(take.Item) + moved;
                        }
                        else if (marked)
                        {
                            used.Remove(chest);
                        }
                        if (moved < take.Amount)
                        {
                            result.InventoryFull = true;   // the chest held it (just reloaded), so his inventory had no room
                        }
                    }
                }
                finally
                {
                    // Send the changed chests to the other players now rather than at the game's next turn, and hold them
                    // here for a few seconds, until every friend's copy has reloaded (ChestClaims, Hold): even if another
                    // mod's handler threw in the middle of a take.
                    foreach (Container chest in held.Held)
                    {
                        if (!used.Contains(chest))
                        {
                            continue;
                        }
                        try   // never throws here: a throw would hide the error that brought it
                        {
                            ChestClaims.Hold(chest);   // first: holding needs nothing from the network
                            ZDOMan.instance.ForceSendZDO(ChestHooks.NetView(chest).GetZDO().m_uid);
                        }
                        catch (Exception error)
                        {
                            user.Log.LogWarning($"[QoL Mods chest service] A chest taken from couldn't be sent or held: {error}");
                        }
                    }
                }

                if (result.InventoryFull)
                {
                    Finish();
                }
                else
                {
                    NextRound();
                }
            }

            // What one take through a friend's open chest came to: what it gave is fetched (the chest lost it, and it's his).
            private void Shared(ShareResult share)
            {
                foreach (KeyValuePair<string, int> given in share.Given)
                {
                    if (given.Value > 0 && left.ContainsKey(given.Key))
                    {
                        left[given.Key] -= given.Value;
                        result.Moved[given.Key] = result.MovedOf(given.Key) + given.Value;
                    }
                }
                if (share.GivenInAll > 0)
                {
                    shared++;
                }
                result.Dropped += share.Dropped;
                result.InventoryFull |= share.NoRoom;
                switch (share.Status)
                {
                    case ShareStatus.NoAnswer:
                        result.ChestsNoAnswer++;
                        break;
                    case ShareStatus.NotOwner:
                        tried.Remove(share.Chest);   // closed meanwhile: a later round asks it the usual way
                        break;
                    case ShareStatus.Refused:
                        result.ChestsInUse++;
                        break;
                    case ShareStatus.Given when share.GivenInAll == 0 && !rules.UseReserved && ChestReserve.IsReserved(share.Chest):
                        result.ChestsReserved++;
                        break;
                }
            }

            // The plan, level keys first (FetchPlanner.PlanLevelsFirst), and taken in that order.
            private List<FetchPlanner.Take> Plan(List<Dictionary<string, int>> stock) => FetchPlanner.PlanLevelsFirst(left, stock);

            // What this chest may give of each item still wanted, by the same keys the order uses.
            private Dictionary<string, int> Takeable(Container chest)
            {
                var amounts = new Dictionary<string, int>();
                foreach (KeyValuePair<string, int> want in left)
                {
                    if (want.Value > 0)
                    {
                        amounts[want.Key] = ChestStock.Count(chest, want.Key, rules.LeaveOne, rules.LeaveOneOfGear);
                    }
                }
                return amounts;
            }

            private bool AnythingLeft()
            {
                foreach (int amount in left.Values)
                {
                    if (amount > 0)
                    {
                        return true;
                    }
                }
                return false;
            }

            private void Finish()
            {
                result.ChestsUsed = used.Count + shared;
                result.ChestsShared = shared;
                done(result);
            }
        }
    }
}
