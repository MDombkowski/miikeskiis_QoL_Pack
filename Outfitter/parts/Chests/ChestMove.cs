// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;

namespace QoLMods.Chests
{
    /// <summary>One move between two chests: up to Amount of an item, by key (ChestStock: its name for any quality, or
    /// name@quality for one), from one chest to another, every world level included.</summary>
    internal sealed class MoveOrder
    {
        // Set by the modules that move (Stack Overflow, job 27), not yet by any in this plugin: silenced, as the kit's hints.
#pragma warning disable CS0649
        internal Container From;
        internal Container To;
        internal string Key;
        internal int Amount;

        /// <summary>Only into the target's part-stacks of the item, never into an empty cell of it.</summary>
        internal bool TopUpOnly;
#pragma warning restore CS0649
    }

    /// <summary>What a batch of moves did.</summary>
    internal sealed class MoveResult
    {
        /// <summary>How many items moved, in all: the targets gained exactly what the sources lost.</summary>
        internal int Moved;

        /// <summary>What moved, by the orders' keys.</summary>
        internal readonly Dictionary<string, int> MovedByKey = new Dictionary<string, int>();

        /// <summary>Orders that moved nothing because one of their two chests couldn't be had (in use, no answer, gone) or
        /// was reserved, or because they can't move anything as written (a chest missing, the same chest twice, no key, no
        /// amount): those are never claimed for.</summary>
        internal int OrdersSkipped;

        /// <summary>Orders that stopped short because the target had no more room for the item (in its part-stacks, for a
        /// top-up-only order).</summary>
        internal int OrdersShort;

        internal int ChestsInUse;
        internal int ChestsNoAnswer;
        internal int ChestsReserved;

        internal int MovedOf(string key) => MovedByKey.TryGetValue(key, out int moved) ? moved : 0;
    }

    /// <summary>
    /// Moves items between chests (card A52, for Stack Overflow's tidy, job 27), as safely in multiplayer as a take: every
    /// chest the orders name is claimed in one claim (ChestClaims), and in the frame they are his, each is reloaded and read
    /// again, and each order whose two chests are both his moves stack by stack into the target first, measured, and out of
    /// the source after (ChestPut.Move); an order with a chest that couldn't be had moves nothing. Every world level moves
    /// (ChestStock.Take and Count count only the world's level, the craft rule, so a move isn't built on them). Every chest
    /// changed is sent and held in a finally.
    /// The rare case, written down: a move changes two chests' records, which may reach the server in different sends, so a
    /// crash between them can copy or lose that one move.
    /// </summary>
    internal static class ChestMove
    {
        /// <summary>The words for an error during a move, in the module's warning.</summary>
        internal const string Task = "moving items between chests";

        /// <summary>
        /// Starts the moves. The orders' chests must be ones ChestFinder.Find gave under the caller's rules, which leave out
        /// the world's own chests, gravestones, chests he couldn't open by hand and ships someone else is aboard: they aren't
        /// looked at again here, and claiming a ship's chest takes the ship's steering. <paramref name="done"/> gets the
        /// result inside the user's Guard, in this frame when every chest is his already, else a fraction of a second later.
        /// A reserved chest (Reserve Stock's mark) neither gives nor takes, unless <paramref name="useReserved"/>. Returns
        /// false, doing nothing, when chests must be claimed from other players while another claim of the service is under
        /// way.
        /// </summary>
        internal static bool Start(ChestUser user, IList<MoveOrder> orders, bool useReserved, Action<MoveResult> done)
        {
            var moves = new List<MoveOrder>();
            var chests = new List<Container>();
            int unfit = 0;
            foreach (MoveOrder order in orders)
            {
                if (order == null || order.From == null || order.To == null || order.From == order.To || string.IsNullOrEmpty(order.Key) ||
                    order.Amount <= 0)
                {
                    unfit++;   // can't move anything: its chests aren't claimed for it
                    continue;
                }
                moves.Add(order);
                foreach (Container chest in new[] { order.From, order.To })
                {
                    if (!chests.Contains(chest))
                    {
                        chests.Add(chest);
                    }
                }
            }
            if (chests.TrueForAll(Mine))
            {
                var mine = new ClaimResult();
                mine.Held.AddRange(chests);
                user.Guard(Task, () => Moved(user, moves, unfit, useReserved, mine, done));
                return true;
            }
            return ChestClaims.Start(user, chests, null, null, held => Moved(user, moves, unfit, useReserved, held, done), Task);
        }

        // In the frame the claim settled: every chest in held.Held is his.
        private static void Moved(ChestUser user, List<MoveOrder> orders, int unfit, bool useReserved, ClaimResult held, Action<MoveResult> done)
        {
            var result = new MoveResult { ChestsInUse = held.InUse, ChestsNoAnswer = held.NoAnswer, OrdersSkipped = unfit };
            if (!user.IsOn)
            {
                result.OrdersSkipped += orders.Count;
                done(result);   // switched off while waiting: move nothing
                return;
            }
            var usable = new HashSet<Container>();
            foreach (Container chest in held.Held)
            {
                ChestHooks.Reload(chest);
                if (!useReserved && ChestReserve.IsReserved(chest))
                {
                    result.ChestsReserved++;
                    continue;
                }
                usable.Add(chest);
            }
            var changed = new List<Container>();
            try
            {
                foreach (MoveOrder order in orders)
                {
                    if (!usable.Contains(order.From) || !usable.Contains(order.To))
                    {
                        result.OrdersSkipped++;
                        continue;
                    }
                    ChestStock.Parse(order.Key, out string name, out int quality);
                    var stacks = new List<ItemDrop.ItemData>();
                    foreach (ItemDrop.ItemData item in order.From.GetInventory().GetAllItems())
                    {
                        // Never the stack in his hand (dragged or being split out of the source's window).
                        if (item.m_shared.m_name == name && (quality < 0 || item.m_quality == quality) && !ChestHooks.InHisHand(item))
                        {
                            stacks.Add(item);
                        }
                    }
                    stacks.Sort((a, b) => ChestStock.TakeOrder(a, b, a.m_stack, b.m_stack));
                    int left = order.Amount;
                    // Marked first, as a put's chests are (ChestStacker): a throw mid-move still sends and holds both.
                    var marked = new List<Container>();
                    foreach (Container chest in new[] { order.From, order.To })
                    {
                        if (!changed.Contains(chest))
                        {
                            changed.Add(chest);
                            marked.Add(chest);
                        }
                    }
                    bool full = false;
                    foreach (ItemDrop.ItemData stack in stacks)
                    {
                        if (left <= 0)
                        {
                            break;
                        }
                        int want = Math.Min(left, stack.m_stack);
                        int moved = ChestPut.Move(order.From, order.To, stack, want, order.TopUpOnly, useReserved);
                        left -= moved;
                        result.Moved += moved;
                        result.MovedByKey[order.Key] = result.MovedOf(order.Key) + moved;
                        if (moved < want)
                        {
                            // No more room for this stack; the next may still fit (another world level or quality, whose
                            // part-stacks in the target are its own).
                            full = true;
                        }
                    }
                    if (full && left > 0)
                    {
                        result.OrdersShort++;
                    }
                    if (left == order.Amount)
                    {
                        // Nothing moved: neither chest changed for this order.
                        foreach (Container chest in marked)
                        {
                            changed.Remove(chest);
                        }
                    }
                }
            }
            finally
            {
                foreach (Container chest in changed)
                {
                    try   // never throws here: a throw would hide the error that brought it
                    {
                        ChestClaims.Hold(chest);
                        ZDOMan.instance.ForceSendZDO(ChestHooks.NetView(chest).GetZDO().m_uid);
                    }
                    catch (Exception error)
                    {
                        user.Log.LogWarning($"[QoL Mods chest service] A chest moved between couldn't be sent or held: {error}");
                    }
                }
            }
            done(result);
        }

        // His now and changeable now: one claimed beyond the server's hand-out only once its newest copy is in and
        // between the server's rounds (ChestClaims.MayChange, card A64).
        private static bool Mine(Container chest) => ChestClaims.MayChange(chest);
    }
}
