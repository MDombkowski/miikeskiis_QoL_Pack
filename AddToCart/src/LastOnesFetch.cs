using System;
using System.Collections.Generic;
using QoLMods.Chests;

namespace AddToCart
{
    /// <summary>
    /// "Take it anyway", the answer to the question before a fetch takes the last one of something from a chest ("True
    /// counts", card A83), as fetches through the chest service, one after the other, each in the frame the one before is
    /// done:
    /// 1. the whole order cut by leave-one, key by key (min of the two: a recipe's fish by name never takes the level a Take
    ///    line wanted), with leave-one on;
    /// 2. what the question named still missing, with leave-one on again: spares a chest gained, or one the first fetch
    ///    couldn't have, since the question was asked (review of card A83, minor 4); usually nothing, and then nothing is
    ///    claimed;
    /// 3. what is still missing of that, with leave-one off: now every chest holding the item keeps only its last one, so
    ///    each one taken empties one chest, never more than the question named.
    /// Asked in one go with leave-one off, the service would take from the nearest chests first and could empty several
    /// where one was enough. No game window here, so the harness can drive it.
    /// </summary>
    internal static class LastOnesFetch
    {
        /// <summary>
        /// Starts it. <paramref name="whole"/> is the fetch's order from the whole counts, <paramref name="kept"/> the same
        /// counted as leave-one allows, <paramref name="lastOnes"/> what the question named (CartMath.LastOnes of the two).
        /// <paramref name="first"/> are the rules of the fetches with leave-one on, <paramref name="second"/> of the one with
        /// it off. <paramref name="done"/> gets them all as one result (Wanted: the whole order), and true if the last ones
        /// were left because the chest service was busy with another fetch; inside the user's Guard, as ChestFetch gives
        /// it. Returns false, doing nothing, while a fetch is under way.
        /// </summary>
        internal static bool Start(ChestUser user, IDictionary<string, int> whole, IDictionary<string, int> kept, List<LastOne> lastOnes,
                                   ChestRules first, ChestRules second, Action<FetchResult, bool> done)
        {
            if (ChestFetch.Busy)
            {
                return false;
            }
            var run = new Run(user, new Dictionary<string, int>(whole), new List<LastOne>(lastOnes), first, second, done);
            Dictionary<string, int> cut = CartMath.WholeCutByKept(run.Whole, kept);
            if (cut.Count == 0)
            {
                return run.Spares();   // leave-one allows none of any of it (a chest's only helmet)
            }
            return ChestFetch.Start(user, cut, first, result => run.After(result, run.Spares));
        }

        private sealed class Run
        {
            internal readonly Dictionary<string, int> Whole;
            private readonly ChestUser user;
            private readonly List<LastOne> named;
            private readonly ChestRules first;
            private readonly ChestRules second;
            private readonly Action<FetchResult, bool> done;
            private readonly List<FetchResult> results = new List<FetchResult>();

            internal Run(ChestUser user, Dictionary<string, int> whole, List<LastOne> named, ChestRules first, ChestRules second,
                         Action<FetchResult, bool> done)
            {
                this.user = user;
                Whole = whole;
                this.named = named;
                this.first = first;
                this.second = second;
                this.done = done;
            }

            // A fetch done: on to the next step, unless his inventory is full or the module is off.
            internal void After(FetchResult result, Func<bool> next)
            {
                results.Add(result);
                if (result.InventoryFull || !user.IsOn)
                {
                    Finish(false);   // nothing more fits, or switched off meanwhile
                    return;
                }
                if (!next())
                {
                    Finish(true);
                }
            }

            // Step 2: what the question named still missing, from spares only (leave-one on). Returns false only when
            // the service was busy (nothing started).
            internal bool Spares()
            {
                Dictionary<string, int> order = CartMath.LastOnesOrder(named, Moved());
                if (order.Count == 0)
                {
                    Finish(false);
                    return true;
                }
                return ChestFetch.Start(user, order, first, result => After(result, LastOnes));
            }

            // Step 3: what is still missing of it, the last ones themselves (leave-one off).
            private bool LastOnes()
            {
                Dictionary<string, int> order = CartMath.LastOnesOrder(named, Moved());
                if (order.Count == 0)
                {
                    Finish(false);
                    return true;
                }
                return ChestFetch.Start(user, order, second, result =>
                {
                    results.Add(result);
                    Finish(false);
                });
            }

            private Dictionary<string, int> Moved()
            {
                var moved = new Dictionary<string, int>();
                foreach (FetchResult result in results)
                {
                    foreach (KeyValuePair<string, int> each in result.Moved)
                    {
                        moved.TryGetValue(each.Key, out int sum);
                        moved[each.Key] = sum + each.Value;
                    }
                }
                return moved;
            }

            private void Finish(bool busy) => done(Merge(Whole, results.ToArray()), busy);
        }

        /// <summary>
        /// The fetches as one result: what moved and what was dropped added up, wanted the whole order. The chests counted
        /// (used, in use, no answer, out of reach, reserved) are the largest of them, since the later ones mostly ask the
        /// same chests again: a chest used only by a later fetch may go uncounted in "from N chests".
        /// </summary>
        internal static FetchResult Merge(IDictionary<string, int> whole, params FetchResult[] fetches)
        {
            var merged = new FetchResult();
            foreach (KeyValuePair<string, int> want in whole)
            {
                if (want.Value > 0)
                {
                    merged.Wanted[want.Key] = want.Value;
                }
            }
            foreach (FetchResult each in fetches ?? new FetchResult[0])
            {
                if (each == null)
                {
                    continue;
                }
                foreach (KeyValuePair<string, int> moved in each.Moved)
                {
                    merged.Moved[moved.Key] = merged.MovedOf(moved.Key) + moved.Value;
                }
                merged.Dropped += each.Dropped;
                merged.ChestsInUse = Math.Max(merged.ChestsInUse, each.ChestsInUse);
                merged.ChestsNoAnswer = Math.Max(merged.ChestsNoAnswer, each.ChestsNoAnswer);
                merged.ChestsOutOfReach = Math.Max(merged.ChestsOutOfReach, each.ChestsOutOfReach);
                merged.ChestsReserved = Math.Max(merged.ChestsReserved, each.ChestsReserved);
                merged.ChestsUsed = Math.Max(merged.ChestsUsed, each.ChestsUsed);
                merged.ChestsShared = Math.Max(merged.ChestsShared, each.ChestsShared);
                merged.InventoryFull |= each.InventoryFull;
            }
            return merged;
        }
    }
}
