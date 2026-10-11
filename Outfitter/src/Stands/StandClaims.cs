using System;
using System.Collections.Generic;
using UnityEngine;

namespace Outfitter.Stands
{
    /// <summary>What became of the stands a claim asked for.</summary>
    internal sealed class StandClaimResult
    {
        /// <summary>The stands this computer owns now, checked in the frame the result is handed over: safe to change until
        /// the end of that frame.</summary>
        internal readonly List<IStand> Held = new List<IStand>();

        /// <summary>Stands whose owner didn't hand them over in time (someone else is using it, or the owner's game is busy).</summary>
        internal int NoAnswer;

        /// <summary>Stands destroyed or unloaded meanwhile.</summary>
        internal int Gone;
    }

    /// <summary>
    /// Makes this computer the owner of armour stands, as the chest service's ChestClaims does for chests (the plan's "The
    /// stand claim"). Only a stand's owner may change it: a change made anywhere else stays local and is overwritten. The
    /// game's stand has no lock: its owner hands it to whoever asks (ArmorStand.RPC_RequestOwn), and the record comes with
    /// the ownership (the owner's newest copy, sent with the new owner). So: a stand this computer owns, at once; nobody's,
    /// and this computer is the server (single player, or the host), taken; nobody's on a client, waited on until the server
    /// gives it an owner (every two seconds, to a player nearby), then asked like any other; another game's, asked and
    /// waited on until it's this computer's; handed to a third game meanwhile, asked again. One claim at a time, settled in
    /// the frame the last stand is in.
    ///
    /// A stand a job uses, and one just changed, is held: until then, another computer's request to take it over, to take
    /// everything off it, to drop one place or to destroy one place's item is quietly dropped on this computer (the
    /// prefixes in StandPatches), as the game drops a request that reaches a computer no longer owning the stand: his press
    /// does nothing, and his next one after the hold works. This computer's own requests always pass. A hold ends by the
    /// clock, or when the stands are switched off.
    /// </summary>
    internal static class StandClaims
    {
        /// <summary>How long to wait for a stand's owner to hand it over, in seconds.</summary>
        internal const float AnswerWait = 6f;

        /// <summary>How long a stand nobody owns is waited on for the server to give it an owner.</summary>
        internal const float OwnerWait = 5f;

        /// <summary>How many times a stand handed to a third game meanwhile is asked again.</summary>
        internal const int AskAgain = 2;

        /// <summary>However many owners it's asked of, a stand is waited on at most this long from the claim's start (review
        /// round 1, n3: each new ask would otherwise start its wait again).</summary>
        internal const float Deadline = 10f;

        internal static Func<float> Clock = () => Time.time;

        /// <summary>This computer's session ID (the RPCs' sender); the harness sets another.</summary>
        internal static Func<long> MyUid = () => ZDOMan.GetSessionID();

        /// <summary>Whether this computer is the server (single player, or the host); the harness sets another.</summary>
        internal static Func<bool> IsServer = () => ZNet.instance != null && ZNet.instance.IsServer();

        private static Claim current;

        // Stand key → when its hold ends.
        private static readonly Dictionary<object, float> Holds = new Dictionary<object, float>();

        internal static bool Busy => current != null;

        /// <summary>
        /// Starts claiming <paramref name="stands"/>; <paramref name="done"/> gets the result, inside Outfitter's guard, in the
        /// frame the last stand is in (at once, inside this call, when every stand is this computer's already). False, doing
        /// nothing, while another claim is under way.
        /// </summary>
        internal static bool Start(IEnumerable<IStand> stands, Action<StandClaimResult> done)
        {
            if (current != null || done == null)
            {
                return false;
            }
            var claim = new Claim(done);
            float now = Clock();
            foreach (IStand stand in stands)
            {
                claim.Add(stand, now);
            }
            current = claim;
            Tick();
            return true;
        }

        /// <summary>Every frame, from the stands' Update: the claim under way, and the holds that are over forgotten.</summary>
        internal static void Tick()
        {
            float now = Clock();
            if (current != null && current.Settle(now))
            {
                Claim finished = current;
                current = null;   // first, so done may start the next claim
                finished.Finish();
            }
            if (Holds.Count > 0)
            {
                List<object> over = null;
                foreach (KeyValuePair<object, float> each in Holds)
                {
                    if (now > each.Value)
                    {
                        (over ??= new List<object>()).Add(each.Key);
                    }
                }
                if (over != null)
                {
                    foreach (object key in over)
                    {
                        Holds.Remove(key);
                    }
                }
            }
        }

        /// <summary>Holds a stand this computer owns for <paramref name="seconds"/> from now (longer if already held
        /// longer).</summary>
        internal static void Hold(IStand stand, float seconds)
        {
            if (stand == null || !stand.IsOwner)
            {
                return;
            }
            float until = Clock() + Math.Max(0f, seconds);
            if (!Holds.TryGetValue(stand.Key, out float now) || now < until)
            {
                Holds[stand.Key] = until;
            }
        }

        /// <summary>True while a hold on this stand lasts.</summary>
        internal static bool IsHeld(object key) => key != null && Holds.TryGetValue(key, out float until) && Clock() <= until;

        /// <summary>
        /// From the prefixes on the stand's requests (run on its owner): true when the stand is held and the request is
        /// another computer's, which is then dropped (the game must skip it). Never while the stands are off. Never throws.
        /// </summary>
        internal static bool Drop(object key, long sender)
        {
            try
            {
                return Stands.Active && sender != MyUid() && IsHeld(key);
            }
            catch (Exception error)
            {
                if (!warnedDrop)
                {
                    warnedDrop = true;
                    Module.Warn($"A request for a held armour stand couldn't be looked at (said once): {error}");
                }
                return false;
            }
        }

        private static bool warnedDrop;

        /// <summary>Gives up the claim under way (its done isn't called) and forgets every hold: switched off, or after an
        /// error (the stands' undo step).</summary>
        internal static void Forget()
        {
            current = null;
            Holds.Clear();
        }

        private enum State
        {
            Held,
            NoOwner,
            Asked,
            NoAnswer,
            Gone,
        }

        private sealed class Entry
        {
            internal IStand Stand;
            internal State State;
            internal float Since;
            internal float Started;
            internal long AskedOf;
            internal int Asks;
        }

        private sealed class Claim
        {
            private readonly Action<StandClaimResult> done;
            private readonly List<Entry> entries = new List<Entry>();

            internal Claim(Action<StandClaimResult> done)
            {
                this.done = done;
            }

            internal void Add(IStand stand, float now)
            {
                var entry = new Entry { Stand = stand, Since = now, Started = now };
                entries.Add(entry);
                if (stand == null || !stand.Valid)
                {
                    entry.State = State.Gone;
                }
                else if (stand.IsOwner)
                {
                    entry.State = State.Held;
                }
                else if (!stand.HasOwner && IsServer())
                {
                    stand.TakeOwnership();   // only the server hands ownership out, so the server may take it
                    entry.State = stand.IsOwner ? State.Held : State.NoOwner;
                }
                else if (!stand.HasOwner)
                {
                    entry.State = State.NoOwner;   // the server gives it an owner soon: then asked
                }
                else
                {
                    Ask(entry, now);
                }
            }

            private static void Ask(Entry entry, float now)
            {
                entry.State = State.Asked;
                entry.Since = now;
                entry.AskedOf = entry.Stand.Owner;
                entry.Asks++;
                entry.Stand.RequestOwn();
            }

            /// <summary>Moves each entry on; true once none is waiting.</summary>
            internal bool Settle(float now)
            {
                bool waiting = false;
                foreach (Entry entry in entries)
                {
                    if (entry.State != State.NoOwner && entry.State != State.Asked)
                    {
                        continue;
                    }
                    IStand stand = entry.Stand;
                    if (!stand.Valid)
                    {
                        entry.State = State.Gone;
                        continue;
                    }
                    if (stand.IsOwner)
                    {
                        entry.State = State.Held;   // handed over, with its newest record
                        continue;
                    }
                    if (now - entry.Started > Deadline)
                    {
                        entry.State = State.NoAnswer;
                        continue;
                    }
                    if (entry.State == State.NoOwner)
                    {
                        if (stand.HasOwner)
                        {
                            Ask(entry, now);   // the server gave it to another game: ask that one
                            waiting = true;
                            continue;
                        }
                        if (now - entry.Since > OwnerWait)
                        {
                            entry.State = State.NoAnswer;
                            continue;
                        }
                        waiting = true;
                        continue;
                    }
                    if (!stand.HasOwner && IsServer())
                    {
                        stand.TakeOwnership();   // its owner left meanwhile: the server takes it
                        if (stand.IsOwner)
                        {
                            entry.State = State.Held;
                            continue;
                        }
                    }
                    // Asked: handed to a third game meanwhile (or let go and given out again), so ask that one.
                    if (stand.HasOwner && stand.Owner != entry.AskedOf && entry.Asks <= AskAgain)
                    {
                        Ask(entry, now);
                        waiting = true;
                        continue;
                    }
                    if (now - entry.Since > AnswerWait)
                    {
                        entry.State = State.NoAnswer;
                        continue;
                    }
                    waiting = true;
                }
                return !waiting;
            }

            internal void Finish()
            {
                var result = new StandClaimResult();
                foreach (Entry entry in entries)
                {
                    switch (entry.State)
                    {
                        case State.Held when entry.Stand.Valid && entry.Stand.IsOwner:
                            result.Held.Add(entry.Stand);
                            break;
                        case State.Held:
                        case State.NoAnswer:
                            result.NoAnswer++;   // held, then handed on meanwhile
                            break;
                        default:
                            result.Gone++;
                            break;
                    }
                }
                Module.Guard("using an armour stand", () => done(result));
            }
        }
    }
}
