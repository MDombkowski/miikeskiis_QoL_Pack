// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>What became of the chests a claim asked for.</summary>
    internal sealed class ClaimResult
    {
        /// <summary>The chests this computer owns now, checked in the same frame the result is handed over: safe to
        /// change until the end of that frame.</summary>
        internal readonly List<Container> Held = new List<Container>();

        /// <summary>Chests someone has open: their owner said no, or a friend opened one meanwhile.</summary>
        internal int InUse;

        /// <summary>Chests whose owner didn't answer in time, or whose ownership didn't arrive in time.</summary>
        internal int NoAnswer;

        /// <summary>Chests that were destroyed or unloaded meanwhile.</summary>
        internal int Gone;
    }

    /// <summary>
    /// Makes this computer the owner of a set of chests, the way the game itself does when he opens a chest, so that
    /// taking from them is saved and can't be undone by, or undo, another player's change.
    ///
    /// Only a chest's owner may change it: a change made anywhere else stays local and is overwritten at the next reload
    /// (Container.OnContainerChanged saves only on the owner). So for each chest another player owns, the service sends
    /// the game's own open request to that owner. The owner's own game answers it, mods or not: no if someone has the
    /// chest open, else it sends its newest copy, hands ownership over, and says yes (Container.RPC_RequestOpen). The
    /// yes arrives before the ownership does (it travels with the chest's record, a little later), so the service
    /// waits for the ownership too, as the game's own chest window does (InventoryGui.UpdateContainer). A chest this
    /// computer owns already needs nothing. A chest nobody owns is waited on until the server gives it an owner (every
    /// two seconds it gives one to a player nearby), then asked like any other; only the server itself (single player,
    /// or the host) takes it at once, since only the server hands ownership out.
    ///
    /// After taking, each chest is held "in use" on this computer for a few seconds (Hold), so a friend who opens it
    /// straight away is told it's in use rather than shown his copy from before the fetch: his game reloads a chest's
    /// contents only once a second, and never while it is open, so a change he made to that old copy would bring the
    /// taken items back.
    ///
    /// One claim at a time. The using modules call Tick every frame from their Update.
    /// </summary>
    internal static class ChestClaims
    {
        /// <summary>How long to wait for an owner's answer, or for an unowned chest to get an owner, in seconds.</summary>
        internal const float AnswerWait = 3f;

        /// <summary>After a yes, how long to wait for the ownership to arrive. Through a dedicated server it comes with
        /// the server's next send.</summary>
        internal const float OwnershipWait = 5f;

        /// <summary>How long a chest just taken from stays "in use" on this computer.</summary>
        internal const float HoldFor = 3f;

        /// <summary>An answer that arrives this long after its request is still caught, never shown as a chest window.</summary>
        private const float CatchFor = AnswerWait + OwnershipWait + 5f;

        // Chests the service has asked and not yet heard from: when last asked, and how many answers are still due, so a
        // late answer to an earlier request can't use up a newer one's place.
        private static readonly Dictionary<ZDOID, Asked> AskedChests = new Dictionary<ZDOID, Asked>();
        private static readonly Dictionary<Container, float> Holds = new Dictionary<Container, float>();
        private static Claim current;
        private static int lastTick = -1;

        /// <summary>True while a claim is waiting for answers.</summary>
        internal static bool Busy => current != null;

        /// <summary>
        /// Starts claiming <paramref name="chests"/> for <paramref name="user"/>; <paramref name="done"/> gets the result,
        /// inside the user's Guard, in the frame the last answer is in (at once if none is needed). Returns false, doing
        /// nothing, while another claim is under way.
        /// </summary>
        internal static bool Start(ChestUser user, IEnumerable<Container> chests, Action<ClaimResult> done)
        {
            if (current != null)
            {
                return false;
            }
            var claim = new Claim(user, done, Game.instance.GetPlayerProfile().GetPlayerID());
            foreach (Container chest in chests)
            {
                claim.Add(chest);
            }
            current = claim;
            Tick(force: true);
            return true;
        }

        /// <summary>Every frame, from each using module's Update. Does its work once a frame, however many call it.</summary>
        internal static void Tick() => Tick(force: false);

        private static void Tick(bool force)
        {
            if (!force && Time.frameCount == lastTick)
            {
                return;
            }
            lastTick = Time.frameCount;
            float now = Time.time;

            if (current != null && current.Settle(now))
            {
                Claim finished = current;
                current = null;   // first, so the done callback may start the next claim
                finished.Finish();
            }

            if (AskedChests.Count > 0)
            {
                var stale = new List<ZDOID>();
                foreach (KeyValuePair<ZDOID, Asked> each in AskedChests)
                {
                    if (now - each.Value.Time > CatchFor)
                    {
                        stale.Add(each.Key);
                    }
                }
                foreach (ZDOID id in stale)
                {
                    AskedChests.Remove(id);
                }
            }

            // A hold ends after HoldFor, but not while that chest is the one open in his own inventory window: the game
            // itself clears the mark when he closes it.
            if (Holds.Count > 0)
            {
                var ended = new List<Container>();
                foreach (KeyValuePair<Container, float> each in Holds)
                {
                    if (each.Key == null || (now - each.Value > HoldFor && !ChestHooks.OpenOnMyScreen(each.Key)))
                    {
                        ended.Add(each.Key);
                    }
                }
                foreach (Container chest in ended)
                {
                    Holds.Remove(chest);
                    if (chest != null)
                    {
                        ChestHooks.SetInUse(chest, false);
                    }
                }
            }
        }

        /// <summary>Marks a chest this computer owns and has just changed as in use for HoldFor seconds, without the lid
        /// or the sound the game's own SetInUse makes. Its owner (this computer) then refuses every open request.</summary>
        internal static void Hold(Container chest)
        {
            ZNetView view = ChestHooks.NetView(chest);
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return;
            }
            if (!chest.IsInUse())
            {
                ChestHooks.SetInUse(chest, true);
                Holds[chest] = Time.time;
            }
            else if (Holds.ContainsKey(chest))
            {
                Holds[chest] = Time.time;   // held already: start again
            }
            // Open on his own screen: the game clears the mark when he closes it.
        }

        /// <summary>Ends every hold at once (the ones other modules made too, which only shortens them): the undo step of a
        /// module that uses the service. A chest open in his own window keeps its mark; the game clears it on closing.</summary>
        internal static void ReleaseHolds()
        {
            foreach (Container chest in new List<Container>(Holds.Keys))
            {
                if (chest != null && !ChestHooks.OpenOnMyScreen(chest))
                {
                    ChestHooks.SetInUse(chest, false);
                }
            }
            Holds.Clear();
        }

        /// <summary>From the patch on Container.Interact: he pressed E on this chest himself, so its answer is his, not
        /// the service's. A claim still waiting on it then times out.</summary>
        internal static void Forget(Container chest)
        {
            try
            {
                ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
                if (view != null && view.IsValid())
                {
                    AskedChests.Remove(view.GetZDO().m_uid);
                }
            }
            catch (Exception error)
            {
                ZLog.LogWarning($"[QoL Mods chest service] Couldn't hand a chest back to the game: {error}");
            }
        }

        /// <summary>
        /// From the patch on Container.RPC_OpenResponse: true if this is the answer to the service's own request (the
        /// service takes it, and the game must not show the chest), false if it belongs to the game. Never throws.
        /// </summary>
        internal static bool TakeAnswer(Container chest, bool granted)
        {
            bool ours = false;
            try
            {
                ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
                if (view == null || !view.IsValid() || !AskedChests.TryGetValue(view.GetZDO().m_uid, out Asked asked))
                {
                    return false;
                }
                if (--asked.Due <= 0)
                {
                    AskedChests.Remove(view.GetZDO().m_uid);
                }
                if (Time.time - asked.Time > CatchFor)
                {
                    return false;
                }
                ours = true;
                current?.Answer(chest, granted, Time.time);
                return true;
            }
            catch (Exception error)
            {
                ZLog.LogWarning($"[QoL Mods chest service] An answer from a chest's owner couldn't be handled: {error}");
                return ours;
            }
        }

        private enum State
        {
            Held,
            NoOwner,
            Asked,
            Granted,
            InUse,
            NoAnswer,
            Gone,
        }

        private sealed class Asked
        {
            internal float Time;
            internal int Due;
        }

        private sealed class Entry
        {
            internal Container Chest;
            internal State State;
            internal float Since;
        }

        private sealed class Claim
        {
            private readonly ChestUser user;
            private readonly Action<ClaimResult> done;
            private readonly long playerId;
            private readonly List<Entry> entries = new List<Entry>();

            internal Claim(ChestUser user, Action<ClaimResult> done, long playerId)
            {
                this.user = user;
                this.done = done;
                this.playerId = playerId;
            }

            internal void Add(Container chest)
            {
                var entry = new Entry { Chest = chest, Since = Time.time };
                entries.Add(entry);
                ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
                if (view == null || !view.IsValid())
                {
                    entry.State = State.Gone;
                }
                else if (view.IsOwner())
                {
                    entry.State = State.Held;
                }
                else if (!view.GetZDO().HasOwner())
                {
                    if (ZNet.instance != null && ZNet.instance.IsServer())
                    {
                        view.ClaimOwnership();   // only the server hands ownership out, so the server may take it
                        entry.State = State.Held;
                    }
                    else
                    {
                        entry.State = State.NoOwner;   // wait for the server to give it an owner, then ask that owner
                    }
                }
                else
                {
                    // Asked even if its record says it's open: that mark can outlive a friend who left with it open, and
                    // the owner's own answer is the truth.
                    Ask(entry, view);
                }
            }

            private void Ask(Entry entry, ZNetView view)
            {
                entry.State = State.Asked;
                entry.Since = Time.time;
                if (!AskedChests.TryGetValue(view.GetZDO().m_uid, out Asked asked))
                {
                    asked = AskedChests[view.GetZDO().m_uid] = new Asked();
                }
                asked.Time = Time.time;
                asked.Due++;
                view.InvokeRPC("RPC_RequestOpen", playerId);
            }

            internal void Answer(Container chest, bool granted, float now)
            {
                foreach (Entry entry in entries)
                {
                    if (entry.Chest == chest && entry.State == State.Asked)
                    {
                        entry.State = granted ? State.Granted : State.InUse;
                        entry.Since = now;
                    }
                }
            }

            /// <summary>Moves each entry on; true once none is waiting any more.</summary>
            internal bool Settle(float now)
            {
                bool waiting = false;
                foreach (Entry entry in entries)
                {
                    if (entry.State != State.NoOwner && entry.State != State.Asked && entry.State != State.Granted)
                    {
                        continue;
                    }
                    ZNetView view = entry.Chest != null ? ChestHooks.NetView(entry.Chest) : null;
                    if (view == null || !view.IsValid())
                    {
                        entry.State = State.Gone;
                        continue;
                    }
                    if (entry.State == State.NoOwner && view.GetZDO().HasOwner())
                    {
                        if (view.IsOwner())
                        {
                            entry.State = State.Held;   // the server gave it to this computer
                            continue;
                        }
                        Ask(entry, view);
                    }
                    else if (entry.State == State.Granted && view.IsOwner())
                    {
                        entry.State = State.Held;
                        continue;
                    }
                    // An unowned chest waits as long as ownership may take: the server hands owners out every 2 s.
                    float wait = entry.State == State.Asked ? AnswerWait : OwnershipWait;
                    if (now - entry.Since > wait)
                    {
                        entry.State = State.NoAnswer;   // a late answer is still caught (CatchFor), and harmless
                    }
                    else
                    {
                        waiting = true;
                    }
                }
                return !waiting;
            }

            internal void Finish()
            {
                var result = new ClaimResult();
                foreach (Entry entry in entries)
                {
                    ZNetView view = entry.Chest != null ? ChestHooks.NetView(entry.Chest) : null;
                    bool valid = view != null && view.IsValid();
                    switch (entry.State)
                    {
                        case State.Held when valid && view.IsOwner():
                            result.Held.Add(entry.Chest);
                            break;
                        case State.Held when valid:
                        case State.InUse:
                            result.InUse++;   // held, then handed on to a friend who opened it meanwhile
                            break;
                        case State.NoAnswer:
                            result.NoAnswer++;
                            break;
                        default:
                            result.Gone++;
                            break;
                    }
                }
                user.Guard("taking from chests", () => done(result));
            }
        }
    }
}
