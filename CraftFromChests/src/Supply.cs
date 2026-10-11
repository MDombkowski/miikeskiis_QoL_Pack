using System;
using System.Collections.Generic;
using System.Linq;
using QoLMods.Chests;
using UnityEngine;

namespace CraftFromChests
{
    /// <summary>
    /// Items wanted from the chests near him, and the chests that give them. A request made through Supply.Start claims
    /// those chests from their owners (the chest service), reloads each one this computer then owns, and plans again
    /// from what it really holds, in up to three rounds when a chest is refused. A chest a friend on the pack has open is
    /// taken from through his game instead, in the same round (ChestShare): what it gives is in his inventory by the time
    /// the round settles (Arrived), and is used from there. The chests are only asked for, never held
    /// before a take, so a friend can still open one meanwhile (it's then his, and no longer counted). Nothing is taken
    /// until the caller takes, in one frame: it reloads the chests (Refresh), counts only those still his, and takes from
    /// them; a craft as its bar ends, a station as soon as the last round is in. After a take the chests are sent and held
    /// (Send). A request made by FromOwned is the same over the chests he owns already, for a piece placed in the frame
    /// it is made.
    /// </summary>
    internal sealed class Request
    {
        /// <summary>What was asked for, by the chest service's keys: an item's name for any quality, or name@quality.</summary>
        internal readonly Dictionary<string, int> Wanted = new Dictionary<string, int>();

        /// <summary>What no held chest was planned to give yet.</summary>
        internal readonly Dictionary<string, int> Left = new Dictionary<string, int>();

        /// <summary>The chests held for this request, nearest first within each round.</summary>
        internal readonly List<Container> Ready = new List<Container>();

        internal readonly ChestRules Rules;

        /// <summary>Runs in the frame the last round is in (inside the module's Guard), unless the request was cancelled.</summary>
        internal readonly Action<Request> Done;

        /// <summary>Chests a friend had open, or whose owner didn't answer in time, over every round: for telling him why.</summary>
        internal int InUse;

        internal int NoAnswer;

        /// <summary>What friends' open chests gave, by key (ChestShare): in his inventory already, or, what didn't fit, at
        /// his feet (Dropped). Used from his inventory, as what he carries.</summary>
        internal readonly Dictionary<string, int> Arrived = new Dictionary<string, int>();

        /// <summary>How many of what friends' open chests gave were dropped at his feet: his inventory had no room.</summary>
        internal int Dropped;

        /// <summary>Less was asked of a friend's open chest than was wanted: his inventory had no room for it.</summary>
        internal bool NoRoom;

        /// <summary>How much of an item (by key) friends' open chests gave for this request.</summary>
        internal int ArrivedOf(string key) => Arrived.TryGetValue(key, out int amount) ? amount : 0;

        /// <summary>How much of an item (by name, any key of it) friends' open chests gave for this request.</summary>
        internal int ArrivedByName(string name)
        {
            int total = 0;
            foreach (KeyValuePair<string, int> each in Arrived)
            {
                ChestStock.Parse(each.Key, out string itemName, out _);
                if (itemName == name)
                {
                    total += each.Value;
                }
            }
            return total;
        }

        internal bool Finished;

        internal bool Cancelled;

        internal int Round;

        internal readonly HashSet<Container> Tried = new HashSet<Container>();

        private readonly HashSet<Container> changed = new HashSet<Container>();

        internal Request(IDictionary<string, int> wanted, ChestRules rules, Action<Request> done)
        {
            Rules = rules;
            Done = done;
            foreach (KeyValuePair<string, int> want in wanted)
            {
                if (want.Value > 0)
                {
                    Wanted[want.Key] = want.Value;
                    Left[want.Key] = want.Value;
                }
            }
        }

        /// <summary>True once the held chests were planned to give everything asked for.</summary>
        internal bool Covered => Left.Values.All(amount => amount <= 0);

        /// <summary>How much of an item (by key) the held chests still may give, counting only those this computer still
        /// owns, and not one reserved since it was planned (unless the rules use reserved chests like any other): what a
        /// take in this frame could have. The craft's own check counts with it, so it must never count what Remove would
        /// refuse.</summary>
        internal int Count(string key)
        {
            int total = 0;
            foreach (Container chest in Ready)
            {
                if (Mine(chest) && Usable(chest))
                {
                    total += ChestStock.Count(chest, key, Rules.LeaveOne);
                }
            }
            return total;
        }

        /// <summary>False for a chest reserved with Reserve Stock (it may have been reserved after it was planned), unless
        /// the rules use reserved chests like any other.</summary>
        internal bool Usable(Container chest) => Rules.UseReserved || !ChestReserve.IsReserved(chest);

        /// <summary>Takes up to <paramref name="amount"/> of an item (by key) out of the held chests, his personal ones first
        /// while "My personal chests first" is on (card A80, the order the plan used: Settings.InTakeOrder), then nearest
        /// first, for something the game uses up in this frame. Only from chests this computer owns. Returns how many went;
        /// call Send afterwards.</summary>
        internal int Remove(string key, int amount)
        {
            int removed = 0;
            foreach (Container chest in Settings.InTakeOrder(Ready))
            {
                if (removed >= amount)
                {
                    break;
                }
                if (!Mine(chest))
                {
                    continue;   // no longer his, or claimed far and not changeable this moment (card A64): never taken from
                }
                // Marked first: if another mod's handler throws inside the take, the chest has changed, and Send (in the
                // caller's finally) still sends and holds it.
                bool marked = changed.Add(chest);
                int got = ChestStock.Remove(chest, key, amount - removed, Rules.LeaveOne, Rules.UseReserved);
                if (got > 0)
                {
                    removed += got;
                }
                else if (marked)
                {
                    changed.Remove(chest);
                }
            }
            return removed;
        }

        /// <summary>Moves up to <paramref name="amount"/> of an item (by key) from the held chests into his inventory
        /// (ChestStock.Take: only what arrives leaves the chest), in the order Remove takes. Returns how many moved; call Send
        /// afterwards.</summary>
        internal int MoveTo(Inventory inventory, string key, int amount)
        {
            int moved = 0;
            foreach (Container chest in Settings.InTakeOrder(Ready))
            {
                if (moved >= amount)
                {
                    break;
                }
                if (!Mine(chest))
                {
                    continue;
                }
                bool marked = changed.Add(chest);   // first, as in Remove
                int got = ChestStock.Take(chest, inventory, key, amount - moved, Rules.LeaveOne, Rules.UseReserved);
                if (got > 0)
                {
                    moved += got;
                }
                else if (marked)
                {
                    changed.Remove(chest);
                }
            }
            return moved;
        }

        /// <summary>After a take: sends the changed chests to the other players now, and holds them a few seconds more,
        /// until every friend's copy has reloaded (ChestClaims.Hold); the counts in the menus are made again. Never
        /// throws: it runs in the callers' finally, where a throw would hide the error that brought it there.</summary>
        internal void Send()
        {
            foreach (Container chest in changed)
            {
                try
                {
                    ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
                    if (view != null && view.IsValid())
                    {
                        ChestClaims.Hold(chest);   // first: holding needs nothing from the network
                        ZDOMan.instance.ForceSendZDO(view.GetZDO().m_uid);
                    }
                }
                catch (Exception error)
                {
                    Module.Log.LogWarning($"A chest taken from couldn't be sent or held: {error}");
                }
            }
            if (changed.Count > 0)
            {
                Stock.Invalidate();
            }
            changed.Clear();
        }

        /// <summary>Reloads every chest of the request this computer owns from its record, before counting and taking in this
        /// frame: one that went to a friend and came back since it was planned holds what the friend left in it.</summary>
        internal void Refresh()
        {
            foreach (Container chest in Ready)
            {
                if (Mine(chest))
                {
                    ChestHooks.Reload(chest);
                }
            }
        }

        /// <summary>How many of the held chests another computer owns now: a friend opened one since it was planned.</summary>
        internal int TakenOver => Ready.Count(chest =>
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            return view != null && view.IsValid() && !view.IsOwner();
        });

        /// <summary>How many of the held chests this computer still owns were reserved since they were planned (and aren't
        /// used for it): for saying why a craft was refused.</summary>
        internal int ReservedSince => Ready.Count(chest => Mine(chest) && !Usable(chest));

        /// <summary>True if the chest is still loaded, this computer owns it and may change it now: one the chest service
        /// claimed itself, beyond the server's hand-out, only once its newest copy is in and between the server's rounds
        /// (ChestClaims.MayChange, card A64; a craft's bar waits for that, ChestClaims.Confirming).</summary>
        internal static bool Mine(Container chest) => ChestClaims.MayChange(chest);
    }

    /// <summary>Runs the requests, one at a time, through the chest service (which runs one claim at a time for every
    /// module of the plugin: a request waits while Add to Cart fetches).</summary>
    internal static class Supply
    {
        private const int Rounds = 3;

        private static readonly List<Request> Queue = new List<Request>();
        private static Request current;

        /// <summary>Starts a request for the items (by key) from the chests near him; <paramref name="done"/> runs in the
        /// frame its last round is in, usually this one when the chests are his already.</summary>
        internal static Request Start(IDictionary<string, int> wanted, Action<Request> done)
        {
            var request = new Request(wanted, Settings.Rules(), done);
            Queue.Add(request);
            Pump();
            return request;
        }

        // The chests he owns near him, found and reloaded once a frame: a click that places a grid of plants pays once a
        // plant, each from a fresh plan over the same chests (their contents are the live ones, taken from as it goes).
        private static List<Container> owned;
        private static int ownedFrame = -1;

        /// <summary>A request over the chests he owns right now, planned and reloaded in this frame and finished at once:
        /// for a piece placed in this frame. Its Covered says whether they give everything.</summary>
        internal static Request FromOwned(IDictionary<string, int> wanted, Vector3 centre)
        {
            var request = new Request(wanted, Settings.Rules(), null);
            if (owned == null || ownedFrame != Time.frameCount)
            {
                owned = new List<Container>();
                foreach (Container chest in ChestFinder.Find(centre, request.Rules))
                {
                    if (Request.Mine(chest))
                    {
                        ChestHooks.Reload(chest);
                        owned.Add(chest);
                    }
                }
                ownedFrame = Time.frameCount;
            }
            Plan(request, Settings.InTakeOrder(owned.Where(chest => Request.Mine(chest) && request.Usable(chest))));
            request.Finished = true;
            return request;
        }

        /// <summary>The chests near him that he doesn't own and that would give these items (what the chests he owns can't),
        /// as this computer's copies show them, nearest first (a friend's open chest last), leaving out those
        /// <paramref name="skip"/> names: for asking for them before they're needed. A chest a friend on the pack has open
        /// goes into <paramref name="shares"/> instead, with what it would give, to be taken through his game; with no
        /// <paramref name="shares"/> (asking ahead of a click), such a chest is left out: nothing is taken ahead.</summary>
        internal static List<Container> ToAskFor(IDictionary<string, int> wanted, Vector3 centre, Func<Container, bool> skip = null,
                                                 List<ShareTake> shares = null)
        {
            ChestRules rules = Settings.Rules();
            HashSet<Container> sharing = new HashSet<Container>();
            List<Container> chests = Candidates(centre, rules, null, sharing)
                                     .Where(chest => !Request.Mine(chest) && (skip == null || !skip(chest)) && (shares != null || !sharing.Contains(chest)))
                                     .ToList();
            var stock = chests.Select(chest => Takeable(chest, wanted, rules)).ToList();
            var used = new List<Container>();
            var through = new Dictionary<Container, ShareTake>();
            foreach (FetchPlanner.Take take in FetchPlanner.PlanLevelsFirst(wanted, stock))
            {
                Container chest = chests[take.Source];
                if (sharing.Contains(chest))
                {
                    if (!through.TryGetValue(chest, out ShareTake share))
                    {
                        through[chest] = share = new ShareTake(chest);
                        shares.Add(share);
                    }
                    share.Add(take.Item, take.Amount);
                }
                else if (!used.Contains(chest))
                {
                    used.Add(chest);
                }
            }
            return used;
        }

        /// <summary>His personal chests near him that he doesn't own yet and that hold some of these items, as this computer's
        /// copies show them, nearest first, leaving out those <paramref name="skip"/> names and any someone has open: for
        /// building's asking ahead while "My personal chests first" is on (card A80), so a click finds them his and takes
        /// from them first, even while chests he holds already would give it all.</summary>
        internal static List<Container> PersonalNotMine(IDictionary<string, int> wanted, Vector3 centre, Func<Container, bool> skip)
        {
            ChestRules rules = Settings.Rules();
            var sharing = new HashSet<Container>();
            long me = ChestClaims.PlayerId();
            PersonalKinds kinds = ChestMine.Kinds;
            return Candidates(centre, rules, null, sharing)
                   .Where(chest => !ChestFinder.OpenElsewhere(chest) && !OwnedHere(chest) && (skip == null || !skip(chest)) &&
                                   ChestMine.IsPersonal(chest, me, kinds) && Takeable(chest, wanted, rules).Values.Any(amount => amount > 0))
                   .ToList();
        }

        // True for a chest this computer owns, whether or not it may be changed this moment (one claimed beyond the server's
        // reach waits for its round): asking for it would settle at once and do nothing (card A80's review).
        private static bool OwnedHere(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            return view != null && view.IsValid() && view.IsOwner();
        }

        /// <summary>True if a chest near him that another player asked to open while it was held (left alone for that
        /// player a few seconds more: ChestClaims) holds some of these items: why they can't be had just now.</summary>
        internal static bool WaitedOnHolds(IDictionary<string, int> items)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }
            ChestRules rules = Settings.Rules();
            float most = rules.Range * rules.Range;
            Vector3 centre = player.transform.position;
            foreach (Container chest in ChestFinder.Loaded())
            {
                if (ChestClaims.IsWaitedOn(chest) && (chest.transform.position - centre).sqrMagnitude <= most &&
                    (rules.UseReserved || !ChestReserve.IsReserved(chest)) &&
                    items.Any(item => item.Value > 0 && ChestStock.Count(chest, item.Key, rules.LeaveOne) > 0))
                {
                    return true;
                }
            }
            return false;
        }

        internal static void Update()
        {
            Pump();
            if (current != null)
            {
                // A request still in its rounds (a station's, a craft's) keeps the chests it holds already, so one claimed
                // beyond the server's hand-out is claimed again when the server lets it go (card A64).
                foreach (Container chest in current.Ready)
                {
                    ChestClaims.KeepForCraft(chest);
                }
            }
        }

        /// <summary>Drops every request, taking nothing: the undo step, and a claim still under way finds it cancelled.</summary>
        internal static void Cancel()
        {
            foreach (Request request in Queue)
            {
                request.Cancelled = true;
                request.Finished = true;
            }
            Queue.Clear();
            if (current != null)
            {
                current.Cancelled = true;
                current.Finished = true;
                current = null;
            }
        }

        private static void Pump()
        {
            if (current == null && Queue.Count > 0 && !ChestClaims.Busy)
            {
                current = Queue[0];
                Queue.RemoveAt(0);
                NextRound(current);
            }
        }

        private static void NextRound(Request request)
        {
            Player player = Player.m_localPlayer;
            if (request.Cancelled || player == null || !Module.IsOn || request.Round >= Rounds || request.Covered)
            {
                Finish(request);
                return;
            }
            request.Round++;
            var sharing = new HashSet<Container>();
            List<Container> chests = Candidates(player.transform.position, request.Rules, request.Tried, sharing);
            var stock = chests.Select(chest => Takeable(chest, request.Left, request.Rules)).ToList();
            var claim = new List<Container>();
            var shares = new Dictionary<Container, ShareTake>();
            foreach (FetchPlanner.Take take in FetchPlanner.PlanLevelsFirst(request.Left, stock))
            {
                Container chest = chests[take.Source];
                if (sharing.Contains(chest))
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
                Finish(request);
                return;
            }
            request.Tried.UnionWith(claim);
            request.Tried.UnionWith(shares.Keys);
            if (!ChestClaims.Start(Module.Chests, claim, shares.Values, request.Rules, held => Held(request, held)))
            {
                // Another module's claim began in between: wait for it, and run this round again.
                request.Round--;
                request.Tried.ExceptWith(claim);
                request.Tried.ExceptWith(shares.Keys);
                Queue.Insert(0, request);
                current = null;
            }
        }

        // In the frame the claim settled: every chest in held.Held is this computer's now, and what friends' open chests
        // gave is in his inventory (or at his feet) already.
        private static void Held(Request request, ClaimResult held)
        {
            request.InUse += held.InUse;
            request.NoAnswer += held.NoAnswer;
            foreach (ShareResult share in held.Shared)
            {
                Shared(request, share);
            }
            if (request.Cancelled || Player.m_localPlayer == null || !Module.IsOn)
            {
                Finish(request);
                return;
            }
            foreach (Container chest in held.Held)
            {
                ChestHooks.Reload(chest);   // what it really holds now; a chest reserved meanwhile gives nothing
            }
            Plan(request, Settings.InTakeOrder(held.Held.Where(request.Usable)));
            if (request.Covered)
            {
                Finish(request);
            }
            else
            {
                NextRound(request);
            }
        }

        // What one take through a friend's open chest came to: what it gave is his, so no longer wanted of the chests.
        private static void Shared(Request request, ShareResult share)
        {
            foreach (KeyValuePair<string, int> given in share.Given)
            {
                if (given.Value > 0 && request.Left.ContainsKey(given.Key))
                {
                    request.Left[given.Key] -= given.Value;
                    request.Arrived[given.Key] = request.ArrivedOf(given.Key) + given.Value;
                }
            }
            request.Dropped += share.Dropped;
            request.NoRoom |= share.NoRoom;
            switch (share.Status)
            {
                case ShareStatus.NoAnswer:
                    request.NoAnswer++;
                    break;
                case ShareStatus.NotOwner:
                    request.Tried.Remove(share.Chest);   // closed meanwhile: a later round asks it the usual way
                    break;
                case ShareStatus.Refused:
                    request.InUse++;
                    break;
            }
            if (share.GivenInAll > 0)
            {
                Stock.Invalidate();
            }
        }

        // Plans what is left over these chests (all this computer's, in the order a take uses them: Settings.InTakeOrder),
        // and adds those the plan uses to Ready in that same order, so a take (Remove, MoveTo), which goes through Ready
        // in order, takes from each chest what this round's plan gave it (card A80's harness: added item by item, a chest
        // giving only the second item came after a farther one giving the first, and the take drew on the wrong one
        // first). Across rounds a take still puts his personal chests first (Settings.InTakeOrder over the whole of
        // Ready), so a personal chest found in a later round may give before an earlier round's other chest, and then
        // each gives more or less than its own round planned: the totals, the counts and leave-one hold either way.
        private static void Plan(Request request, List<Container> chests)
        {
            var stock = chests.Select(chest => Takeable(chest, request.Left, request.Rules)).ToList();
            var used = new HashSet<int>();
            foreach (FetchPlanner.Take take in FetchPlanner.PlanLevelsFirst(request.Left, stock))
            {
                used.Add(take.Source);
                request.Left[take.Item] -= take.Amount;
            }
            for (int source = 0; source < chests.Count; source++)
            {
                if (used.Contains(source) && !request.Ready.Contains(chests[source]))
                {
                    request.Ready.Add(chests[source]);
                }
            }
        }

        private static void Finish(Request request)
        {
            if (request == current)
            {
                current = null;
            }
            if (request.Finished)
            {
                return;
            }
            request.Finished = true;
            // Switched off meanwhile (Left Alt + O, or its settings): nothing is taken.
            if (!request.Cancelled && request.Done != null && Module.IsOn)
            {
                Module.Guard("taking from chests", () => request.Done(request));
            }
        }

        // The chests the rules allow, nearest first; then those friends on the pack have open, taken from through their
        // games (added to sharing too); then a chest someone else has open, last: that mark can be stale, and its owner's
        // answer is the truth, but a refusal shouldn't use up a round that a free chest could fill. Within each of the
        // three, his personal chests first while "My personal chests first" is on (card A80, Settings.InTakeOrder: the
        // order the take follows too). A chest this game can't get at all (beyond the server's reach with no way to claim
        // it, ChestClaims.CantClaim) is left out: asked, it would only wait out its time and keep every other claim waiting
        // (card A69); he's told why instead.
        private static List<Container> Candidates(Vector3 centre, ChestRules rules, HashSet<Container> skip, HashSet<Container> sharing)
        {
            var free = new List<Container>();
            var shared = new List<Container>();
            var openElsewhere = new List<Container>();
            foreach (Container chest in ChestFinder.Find(centre, rules))
            {
                if ((skip == null || !skip.Contains(chest)) && !ChestClaims.CantClaim(chest, Module.Chests))
                {
                    (!ChestFinder.OpenElsewhere(chest) ? free : ChestShare.CanShare(chest) ? shared : openElsewhere).Add(chest);
                }
            }
            sharing.UnionWith(shared);
            List<Container> ordered = Settings.InTakeOrder(free);
            ordered.AddRange(Settings.InTakeOrder(shared));
            ordered.AddRange(Settings.InTakeOrder(openElsewhere));
            return ordered;
        }

        // What this chest may give of each item still wanted, by the same keys.
        private static Dictionary<string, int> Takeable(Container chest, IDictionary<string, int> wanted, ChestRules rules)
        {
            var amounts = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> want in wanted)
            {
                if (want.Value > 0)
                {
                    amounts[want.Key] = ChestStock.Count(chest, want.Key, rules.LeaveOne);
                }
            }
            return amounts;
        }
    }
}
