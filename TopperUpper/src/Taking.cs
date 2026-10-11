using System;
using System.Collections.Generic;
using QoLMods.Chests;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// The chests one feed takes from, in the one frame it happens (the research's 2.4): only chests this game already
    /// holds and may change now (ChestClaims.MayChange, never IsOwner alone: a chest the service claimed beyond the
    /// server's hand-out may be changed only between the server's rounds), never the Obliterator's (code review M2), never
    /// one kept for a craft whose bar is running (Craft from Chests; m4), never one carrying Reserve Stock's mark unless the
    /// settings use reserved chests (ChestUse). Each chest is reloaded from its record before anything is counted or taken
    /// from it, and only one whose copy here holds the item at all is (m5: a light's feed reaches 150 m of chests). Nothing
    /// is ever claimed, and no ownership is ever asked for. The caller takes one at a time (TakeOne), hands what it took to
    /// the light or station (Carry), and calls Finish in a finally: every chest changed is held and sent then, even if
    /// another mod's handler threw in the middle.
    /// </summary>
    internal sealed class Taking
    {
        private readonly List<Container> chests = new List<Container>();
        private readonly HashSet<Container> changed = new HashSet<Container>();
        private readonly HashSet<Container> fresh = new HashSet<Container>();
        private readonly bool leaveOne;
        private readonly bool useReserved;

        /// <param name="candidates">The chests it may use, nearest first (ChestFinder.Find, or a station's linked inputs).</param>
        internal Taking(IEnumerable<Container> candidates, bool leaveOne, bool useReserved)
        {
            this.leaveOne = leaveOne;
            this.useReserved = useReserved;
            foreach (Container chest in candidates)
            {
                if (chest == null || chests.Contains(chest))
                {
                    continue;
                }
                bool reserved = ChestReserve.IsReserved(chest);
                if (ChestUse.WhyNot(ChestClaims.MayChange(chest), Obliterator.Is(chest), ChestClaims.IsKeptForCraft(chest), reserved, useReserved) != null)
                {
                    if (reserved && !useReserved)
                    {
                        ReservedLeftOut++;
                    }
                    continue;
                }
                chests.Add(chest);
            }
        }

        /// <summary>The chests it may take from, nearest first.</summary>
        internal IReadOnlyList<Container> Chests => chests;

        /// <summary>How many of the candidates were left out for Reserve Stock's mark.</summary>
        internal int ReservedLeftOut { get; private set; }

        /// <summary>How many of an item (by shared name) these chests can give, leave-one taken off.</summary>
        internal int CanGive(string key)
        {
            int total = 0;
            foreach (Container chest in chests)
            {
                if (Holds(chest, key))
                {
                    total += ChestStock.Count(chest, key, leaveOne);
                }
            }
            return total;
        }

        /// <summary>Takes one of an item (by shared name) out of the nearest chest that can give it, for something the game
        /// uses up where it stands (ChestStock.Remove). False when none can.</summary>
        internal bool TakeOne(string key)
        {
            if (finished)
            {
                return false;
            }
            foreach (Container chest in chests)
            {
                if (!Holds(chest, key))
                {
                    continue;
                }
                // Marked first: if another mod's handler throws inside the take, the chest has changed, and Finish (in the
                // caller's finally) still sends and holds it.
                bool marked = changed.Add(chest);
                if (ChestStock.Remove(chest, key, 1, leaveOne, useReserved) == 1)
                {
                    return true;
                }
                if (marked)
                {
                    changed.Remove(chest);
                }
            }
            return false;
        }

        // Whether a chest holds the item, from its newest copy: reloaded once per feed, and only one whose copy here holds
        // it at all (a chest this game holds is the copy everyone else's follows; one that came back from a friend and
        // reloads later is only missed this time).
        private bool Holds(Container chest, string key)
        {
            Inventory inventory = chest.GetInventory();
            if (inventory == null || !inventory.ContainsItemByName(key))
            {
                return false;
            }
            if (fresh.Add(chest))
            {
                ChestHooks.Reload(chest);   // the newest copy, in the frame of the take
                inventory = chest.GetInventory();
            }
            return inventory != null && inventory.ContainsItemByName(key);
        }

        /// <summary>How many chests were taken from.</summary>
        internal int ChangedCount => changed.Count;

        /// <summary>After the take: every chest changed is held for a few seconds (ChestClaims.Hold: other players' requests
        /// for it are dropped until their copies have reloaded) and its record sent at once. Never throws: it runs in the
        /// caller's finally. Once only: nothing is taken after it.</summary>
        internal void Finish()
        {
            if (finished)
            {
                return;
            }
            finished = true;
            foreach (Container chest in changed)
            {
                try
                {
                    ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
                    if (view != null && view.IsValid())
                    {
                        ChestClaims.Hold(chest);   // first: holding needs nothing from the network
                        ZDOMan.instance?.ForceSendZDO(view.GetZDO().m_uid);
                    }
                }
                catch (Exception error)
                {
                    Module.Warn($"A chest taken from couldn't be sent or held: {error}");
                }
            }
        }

        private bool finished;
    }

    /// <summary>
    /// Carrying out a feed's plan (FeedRule.Plan) through the chests (Carry: one at a time out of the chests, then once
    /// per lane into the light or station: Adds), noting it in the shared ledger, and saying what it did.
    /// </summary>
    internal static class Feeding
    {
        /// <summary>Gives each grant what the chests still give: <paramref name="add"/> hands one lane's units to the light
        /// or station. Call Taking.Finish in a finally around it.</summary>
        internal static List<Given> Carry(IList<Grant> grants, Taking taking, Action<Grant, int> add) =>
            Logic.Carry.Out(grants, taking.TakeOne, add);

        /// <summary>The ledger every count reads is told what a feed took and queued (Counts.Ledger; code review m6), so
        /// every count, around him or around any station, sees it before it's made again.</summary>
        internal static void Count(List<Given> given)
        {
            foreach (Given each in given)
            {
                Counts.Took(each.Grant.Input, each.Amount, each.Grant.Product, each.Products);
            }
        }

        /// <summary>"Added 3 Resin to the Sconce", as the settings say (top left, centre, or not at all), and a log line with
        /// "Write what's fed to the log".</summary>
        internal static void Tell(Fed fed, List<Given> given, int chests, bool byHand)
        {
            if (given.Count == 0)
            {
                return;
            }
            var parts = new List<string>();
            foreach (Given each in given)
            {
                parts.Add($"{each.Amount} {Names.Item(each.Grant.Input)}");
            }
            string what = string.Join(", ", parts);
            string text = $"Added {what} to the {Names.Of(fed)}";
            switch (Settings.Say.Value)
            {
                case SayAdded.TopLeft:
                    MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, text);
                    break;
                case SayAdded.Centre:
                    MessageHud.instance?.ShowMessage(MessageHud.MessageType.Center, text);
                    break;
            }
            if (Settings.LogFeeds.Value)
            {
                Vector3 at = fed.Position;
                Module.Log.LogInfo($"Fed {Names.Of(fed)} ({fed.Prefab} at {at.x:0},{at.y:0},{at.z:0}): {what}, from {chests} chest(s)" +
                                   (byHand ? ", sent to the friend's game that looks after it" : string.Empty) + ".");
            }
        }
    }
}
