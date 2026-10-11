using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TopperUpper.Loose
{
    /// <summary>An item a sweep found it may put away.</summary>
    internal sealed class Found
    {
        /// <summary>Its place in the list the world noted at the sweep's start (ILooseWorld.Begin).</summary>
        internal int Index;

        /// <summary>Its shared name ("$item_stone").</summary>
        internal string Name;

        internal Spot At;
        internal ItemKey Key;
    }

    /// <summary>One kind of item found in a sweep, put away over as many frames as it needs.</summary>
    internal sealed class Job
    {
        internal string Name;

        /// <summary>What's still to put away, nearest him first.</summary>
        internal readonly List<Found> Queue = new List<Found>();

        /// <summary>The middle of the items found, and how far the farthest lies from it.</summary>
        internal Spot Centre;

        internal float Radius;

        /// <summary>Frames it has had, and the most it may have.</summary>
        internal int Frames;

        internal int MostFrames;

        /// <summary>The world's own notes for this job (the game's: the chests near it).</summary>
        internal object Tag;
    }

    /// <summary>What one frame of a put-away did.</summary>
    internal sealed class PutOutcome
    {
        /// <summary>What went where: the chest (the world's own object), the item's shared name, how many.</summary>
        internal readonly List<(object Chest, string Name, int Amount)> Moved = new List<(object Chest, string Name, int Amount)>();

        /// <summary>The frame's items still lying there and still allowed, that didn't go anywhere (or only in part).</summary>
        internal readonly List<Found> Unmoved = new List<Found>();
    }

    /// <summary>What a whole sweep put away, for the log.</summary>
    internal sealed class SweepReport
    {
        internal int Looked;
        internal int Found;
        internal int Units;
        internal int Chests;
        internal int PutFrames;
        internal int Waiting;
    }

    /// <summary>The game, as a sweep sees it (GameWorld in the game; a model of it in the scratch harness).</summary>
    internal interface ILooseWorld
    {
        /// <summary>Now, in seconds (the game's Time.time).</summary>
        double Now { get; }

        /// <summary>Where he is.</summary>
        Spot You { get; }

        /// <summary>A sweep starts: notes the items loaded now and what a sweep reads once (players, tames, stations, the
        /// settings). Returns how many items it noted.</summary>
        int Begin();

        /// <summary>Looks at one item noted by Begin: Left.None, with <paramref name="found"/> filled, if it may be put
        /// away now.</summary>
        Left Look(int index, Waits waits, out Found found);

        /// <summary>One frame's put-away: these items (all of the job's kind) into at most <paramref name="chests"/>
        /// chests, each looked at again first.</summary>
        PutOutcome Put(Job job, IList<Found> items, int chests);

        /// <summary>The one line for one chest that got something in this sweep.</summary>
        void Say(object chest, IList<KeyValuePair<string, int>> what);

        /// <summary>The sweep is over.</summary>
        void Finished(SweepReport report);
    }

    /// <summary>The settings' numbers for the pacing, read each frame.</summary>
    internal sealed class Pace
    {
        /// <summary>The most items a sweep gathers; the rest wait for the next sweep.</summary>
        internal const int MostFound = 1024;

        /// <summary>A job gets the frames its items need at the cap, and this many more for chests that fill.</summary>
        internal const int SpareFrames = 8;

        internal int Scan = 64;
        internal int Puts = 40;
        internal int Chests = 4;
        internal double Every = 2.0;
        internal double RetryAfter = 10.0;
    }

    /// <summary>Items that found no chest, left alone until a time (never asked about again meanwhile).</summary>
    internal sealed class Waits
    {
        /// <summary>The most remembered; past it the memory starts again (an item then only gets looked at sooner).</summary>
        internal const int Most = 4096;

        private readonly Dictionary<ItemKey, double> until = new Dictionary<ItemKey, double>();

        internal int Count => until.Count;

        internal bool Has(ItemKey key, double now) => until.TryGetValue(key, out double end) && end > now;

        internal void Add(ItemKey key, double end)
        {
            if (until.Count >= Most && !until.ContainsKey(key))
            {
                until.Clear();
            }
            until[key] = end;
        }

        internal void Prune(double now)
        {
            if (until.Count == 0)
            {
                return;
            }
            var done = new List<ItemKey>();
            foreach (KeyValuePair<ItemKey, double> each in until)
            {
                if (each.Value <= now)
                {
                    done.Add(each.Key);
                }
            }
            foreach (ItemKey key in done)
            {
                until.Remove(key);
            }
        }

        internal void Clear() => until.Clear();
    }

    /// <summary>The finished frames' puts, by chest (by reference: a game object's own equality may call two destroyed ones
    /// the same) and then by item, in the order they came.</summary>
    internal sealed class Tally
    {
        private readonly Dictionary<object, Dictionary<string, int>> byChest = new Dictionary<object, Dictionary<string, int>>(ByReference.Instance);
        private readonly List<object> order = new List<object>();
        private readonly Dictionary<object, List<string>> names = new Dictionary<object, List<string>>(ByReference.Instance);

        internal int Units { get; private set; }

        internal int Chests => order.Count;

        internal void Add(object chest, string name, int amount)
        {
            if (chest == null || name == null || amount <= 0)
            {
                return;
            }
            if (!byChest.TryGetValue(chest, out Dictionary<string, int> items))
            {
                byChest[chest] = items = new Dictionary<string, int>(StringComparer.Ordinal);
                names[chest] = new List<string>();
                order.Add(chest);
            }
            if (!items.TryGetValue(name, out int sum))
            {
                names[chest].Add(name);
            }
            items[name] = sum + amount;
            Units += amount;
        }

        /// <summary>Each chest once, in the order it first got something, with its items in the order they came.</summary>
        internal IEnumerable<(object Chest, List<KeyValuePair<string, int>> What)> Each()
        {
            foreach (object chest in order)
            {
                var what = new List<KeyValuePair<string, int>>();
                foreach (string name in names[chest])
                {
                    what.Add(new KeyValuePair<string, int>(name, byChest[chest][name]));
                }
                yield return (chest, what);
            }
        }

        internal void Clear()
        {
            byChest.Clear();
            order.Clear();
            names.Clear();
            Units = 0;
        }

        private sealed class ByReference : IEqualityComparer<object>
        {
            internal static readonly ByReference Instance = new ByReference();

            public new bool Equals(object a, object b) => ReferenceEquals(a, b);

            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }

    /// <summary>What a sweep is doing.</summary>
    internal enum Phase
    {
        /// <summary>Waiting for the next sweep.</summary>
        Idle,

        /// <summary>Looking over the items noted, a few a frame.</summary>
        Gathering,

        /// <summary>Putting away what was found, one kind's frame at a time.</summary>
        Putting,
    }

    /// <summary>
    /// The loose-item pickup's work, spread over frames (the freeze research's section 8: batch per chest, a budget per
    /// frame, let drops settle, no per-item log line, never ask again). Every few seconds a sweep notes the items loaded
    /// and looks them over, at most "Items looked at per frame" a frame, keeping those that may be put away (Qualify).
    /// Then, from the next frame, it puts them away one kind at a time (a broken stone deposit is one kind), at most "Items
    /// put away per frame" into at most "Chests put into per frame" chests each frame, through one put-away call (Stow: one
    /// plan, one send and hold per chest). A frame never both looks and puts. Items a frame couldn't put anywhere are tried
    /// at the next chests next frame; the items of a frame that moved nothing (no chest within their reach has room) wait a
    /// while before they're looked at again, and the kind's other items go on; a kind that has had its frames leaves the
    /// rest waiting too. When everything is done, each chest that got something gets one line ("Stone x87"), whatever
    /// number of frames it took.
    /// </summary>
    internal sealed class Sweep
    {
        private readonly ILooseWorld world;
        private readonly Waits waits = new Waits();
        private readonly List<Found> found = new List<Found>();
        private readonly Tally tally = new Tally();
        private List<Job> jobs = new List<Job>();
        private int jobAt;
        private int total;
        private int scanned;
        private int putFrames;
        private double nextStart;

        internal Sweep(ILooseWorld world)
        {
            this.world = world;
        }

        internal Phase Phase { get; private set; }

        /// <summary>The items remembered as having found no chest (for the harness).</summary>
        internal Waits Waits => waits;

        /// <summary>One frame's turn.</summary>
        internal void Tick(Pace pace)
        {
            double now = world.Now;
            if (Phase == Phase.Idle)
            {
                if (now < nextStart)
                {
                    return;
                }
                waits.Prune(now);
                found.Clear();
                tally.Clear();
                jobs = new List<Job>();
                jobAt = 0;
                putFrames = 0;
                scanned = 0;
                total = Math.Max(0, world.Begin());
                Phase = Phase.Gathering;
            }
            if (Phase == Phase.Gathering)
            {
                int end = Math.Min(total, scanned + Math.Max(1, pace.Scan));
                while (scanned < end)
                {
                    if (found.Count >= Pace.MostFound)
                    {
                        scanned = total;   // enough for one sweep: the rest next time
                        break;
                    }
                    if (world.Look(scanned, waits, out Found item) == Left.None && item != null)
                    {
                        found.Add(item);
                    }
                    scanned++;
                }
                if (scanned < total)
                {
                    return;
                }
                jobs = Group(found, world.You);
                jobAt = 0;
                Phase = Phase.Putting;
                return;   // the puts start next frame
            }
            while (jobAt < jobs.Count && jobs[jobAt].Queue.Count == 0)
            {
                jobAt++;
            }
            if (jobAt >= jobs.Count)
            {
                Finish(now, pace);
                return;
            }
            Job job = jobs[jobAt];
            int cap = Math.Max(1, pace.Puts);
            if (job.Frames == 0)
            {
                job.MostFrames = (job.Queue.Count + cap - 1) / cap + Pace.SpareFrames;
            }
            int take = Math.Min(cap, job.Queue.Count);
            List<Found> chunk = job.Queue.GetRange(0, take);
            job.Queue.RemoveRange(0, take);
            job.Frames++;
            putFrames++;
            PutOutcome outcome = world.Put(job, chunk, Math.Max(1, pace.Chests)) ?? new PutOutcome();
            int moved = 0;
            foreach ((object chest, string name, int amount) in outcome.Moved)
            {
                if (amount > 0)
                {
                    tally.Add(chest, name, amount);
                    moved += amount;
                }
            }
            if (moved == 0)
            {
                // No chest within reach of these has room: they wait a while; the rest of the kind goes on next frame.
                Wait(outcome.Unmoved, now, pace);
            }
            else if (job.Frames >= job.MostFrames)
            {
                // It has had its frames: what's left waits a while.
                Wait(outcome.Unmoved, now, pace);
                Wait(job.Queue, now, pace);
                job.Queue.Clear();
            }
            else
            {
                job.Queue.InsertRange(0, outcome.Unmoved);   // next frame, at the next chests
            }
        }

        /// <summary>Everything forgotten (switched off, a new world, the undo step): the next sweep starts afresh.</summary>
        internal void Forget()
        {
            Phase = Phase.Idle;
            found.Clear();
            tally.Clear();
            jobs = new List<Job>();
            jobAt = 0;
            scanned = 0;
            total = 0;
            putFrames = 0;
            nextStart = 0;
            waits.Clear();
        }

        private void Wait(List<Found> items, double now, Pace pace)
        {
            foreach (Found item in items)
            {
                waits.Add(item.Key, now + Math.Max(0.0, pace.RetryAfter));
            }
        }

        private void Finish(double now, Pace pace)
        {
            foreach ((object chest, List<KeyValuePair<string, int>> what) in tally.Each())
            {
                world.Say(chest, what);
            }
            world.Finished(new SweepReport
            {
                Looked = total,
                Found = found.Count,
                Units = tally.Units,
                Chests = tally.Chests,
                PutFrames = putFrames,
                Waiting = waits.Count,
            });
            found.Clear();
            tally.Clear();
            jobs = new List<Job>();
            jobAt = 0;
            nextStart = now + Math.Max(0.0, pace.Every);
            Phase = Phase.Idle;
        }

        /// <summary>The items found, one job per kind (by shared name): the kinds whose nearest item is nearest him first,
        /// and each kind's items nearest him first.</summary>
        internal static List<Job> Group(IList<Found> items, Spot you)
        {
            var byName = new Dictionary<string, Job>(StringComparer.Ordinal);
            var jobs = new List<Job>();
            foreach (Found item in items)
            {
                if (item?.Name == null)
                {
                    continue;
                }
                if (!byName.TryGetValue(item.Name, out Job job))
                {
                    byName[item.Name] = job = new Job { Name = item.Name };
                    jobs.Add(job);
                }
                job.Queue.Add(item);
            }
            var nearest = new Dictionary<Job, float>();
            foreach (Job job in jobs)
            {
                job.Queue.Sort((a, b) =>
                {
                    int by = a.At.DistanceSquared(you).CompareTo(b.At.DistanceSquared(you));
                    return by != 0 ? by : a.Index.CompareTo(b.Index);
                });
                double x = 0, y = 0, z = 0;
                foreach (Found item in job.Queue)
                {
                    x += item.At.X;
                    y += item.At.Y;
                    z += item.At.Z;
                }
                int n = job.Queue.Count;
                job.Centre = new Spot((float)(x / n), (float)(y / n), (float)(z / n));
                float far = 0f;
                foreach (Found item in job.Queue)
                {
                    far = Math.Max(far, item.At.Distance(job.Centre));
                }
                job.Radius = far;
                nearest[job] = job.Queue[0].At.DistanceSquared(you);
            }
            jobs.Sort((a, b) =>
            {
                int by = nearest[a].CompareTo(nearest[b]);
                return by != 0 ? by : string.CompareOrdinal(a.Name, b.Name);
            });
            return jobs;
        }
    }

    /// <summary>A chest's one line: "Stone x87, Flint x3" (the game's own " x" for a stack, as its hover text writes it),
    /// the most first, at most four kinds and then how many more.</summary>
    internal static class SayText
    {
        internal const int MostKinds = 4;

        internal static string Of(IList<KeyValuePair<string, int>> what, Func<string, string> nameOf)
        {
            if (what == null || what.Count == 0)
            {
                return string.Empty;
            }
            var sorted = new List<KeyValuePair<string, int>>(what);
            sorted.Sort((a, b) => a.Value != b.Value ? b.Value.CompareTo(a.Value) : string.CompareOrdinal(a.Key, b.Key));
            var parts = new List<string>();
            for (int i = 0; i < sorted.Count && i < MostKinds; i++)
            {
                string name = nameOf != null ? nameOf(sorted[i].Key) : sorted[i].Key;
                parts.Add($"{name} x{sorted[i].Value}");
            }
            string text = string.Join(", ", parts);
            if (sorted.Count > MostKinds)
            {
                text += $" and {sorted.Count - MostKinds} more";
            }
            return text;
        }
    }

    /// <summary>
    /// "Skip the log line for a repeat ownership request": when another game asks this one for an item lying on the ground
    /// (ItemDrop.RPC_RequestOwn), the game writes "Player … wants to pickup …" and then, if this game owns it, hands it
    /// over; if it doesn't, it writes one more line and does nothing else ("but they are already the owner", "but neither I
    /// nor the requesting player are the owners"). In his log three asks in four were repeats of that kind (the freeze
    /// research, section 3). Skipping the method only when this game holds the item's record and doesn't own it leaves
    /// every ownership outcome exactly the game's: the handover itself still runs, and still writes its line.
    /// </summary>
    internal static class RepeatAsk
    {
        /// <summary>Whether the game's handling of one ask is skipped: the switch on, the item's record here, and this game
        /// not its owner.</summary>
        internal static bool Skip(bool on, bool hasRecord, bool isOwner) => on && hasRecord && !isOwner;
    }
}
