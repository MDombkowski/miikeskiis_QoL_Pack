using System;
using System.Collections.Generic;

namespace TopperUpper.Logic
{
    /// <summary>
    /// A smelter-family station's numbers (the game's Smelter fields), as plain values, so the catch-up's simulation runs
    /// without the game (the scratch harness drives it with the vanilla stations' numbers).
    /// </summary>
    internal sealed class SmelterKind
    {
        /// <summary>Its queue's size (m_maxOre); 0 for a station with no input (the Frigid Kiln).</summary>
        internal int MaxOre;

        /// <summary>Its fuel's capacity (m_maxFuel); 0 for one that burns none (the charcoal kiln, the windmill).</summary>
        internal int MaxFuel;

        /// <summary>Fuel spent per product (m_fuelPerProduct).</summary>
        internal int FuelPerProduct = 1;

        /// <summary>Seconds per product at full power (m_secPerProduct).</summary>
        internal float SecPerProduct = 10f;

        /// <summary>Holds its products as one stack until it's full or the kind changes (m_spawnStack: the windmill).</summary>
        internal bool SpawnStack;

        /// <summary>Has a conversion with no input (m_noSourceConversion: the Frigid Kiln, whose fuel is its input).</summary>
        internal bool Sourceless;

        /// <summary>Works only under a roof (m_requiresRoof: the spinning wheel).</summary>
        internal bool RequiresRoof;

        /// <summary>What an input (by prefab name, as the queue holds it) becomes, by product key, as the game's own
        /// GetItemConversion finds it (the first conversion with no input or with that input); null for none.</summary>
        internal Func<string, string> ProductOf = input => null;

        /// <summary>A product's most in one stack (its m_maxStackSize), for the held stack.</summary>
        internal Func<string, int> MaxStackOf = product => 1;

        /// <summary>
        /// Sets Sourceless, ProductOf and MaxStackOf from the station's conversions in the game's order (an input prefab
        /// name, or null for none; the product's key; the product's most in one stack), read once: ProductOf as the game's
        /// GetItemConversion finds it, the first conversion with no input or with that input (so nothing after the first
        /// with no input is ever reached); Sourceless when any has no input (m_noSourceConversion).
        /// </summary>
        internal SmelterKind WithConversions(IEnumerable<(string From, string To, int MaxStack)> conversions)
        {
            var byInput = new Dictionary<string, string>(StringComparer.Ordinal);
            var stacks = new Dictionary<string, int>(StringComparer.Ordinal);
            bool noInput = false;
            string noInputProduct = null;
            foreach ((string from, string to, int most) in conversions)
            {
                if (to != null && !stacks.ContainsKey(to))
                {
                    stacks[to] = most;
                }
                if (noInput)
                {
                    continue;   // never reached past the first conversion with no input
                }
                if (from == null)
                {
                    noInput = true;
                    noInputProduct = to;
                }
                else if (!byInput.ContainsKey(from))
                {
                    byInput[from] = to;
                }
            }
            Sourceless = noInput;
            ProductOf = input => input != null && byInput.TryGetValue(input, out string to) ? to : noInput ? noInputProduct : null;
            MaxStackOf = product => product != null && stacks.TryGetValue(product, out int most) ? most : 1;
            return this;
        }
    }

    /// <summary>A smelter-family station's record (its network record's values), as plain values.</summary>
    internal sealed class SmelterState
    {
        internal float Fuel;

        /// <summary>The queue, first in first out, by input prefab name (item0, item1 … up to "queued").</summary>
        internal List<string> Queue = new List<string>();

        internal float BakeTimer;
        internal float Accumulator;
        internal string SpawnOre = string.Empty;
        internal int SpawnAmount;

        internal SmelterState Copy() => new SmelterState
        {
            Fuel = Fuel,
            Queue = new List<string>(Queue),
            BakeTimer = BakeTimer,
            Accumulator = Accumulator,
            SpawnOre = SpawnOre ?? string.Empty,
            SpawnAmount = SpawnAmount,
        };
    }

    /// <summary>
    /// The game's own smelter, one second at a time (Smelter.UpdateSmelter, Valheim 1.0.17, the decompile of 2026-10-10),
    /// written over plain values with the game's own float arithmetic, so the catch-up makes exactly what the station
    /// would have. Each second: nothing (the second still spent) without fuel when it burns fuel, without a queue when it
    /// takes input, without its roof or with its smoke blocked; else its fuel goes down by power ÷ (seconds per product ÷
    /// fuel per product), below 0.0001 to 0, and its bake timer up by the power; at seconds per product the timer goes back
    /// to 0 and the first in the queue becomes a product (the windmill's into its held stack). After each second, with
    /// the queue empty or the fuel out, the held stack comes out. Products that come out are counted in Made, by product.
    /// </summary>
    internal sealed class SmelterRun
    {
        private readonly SmelterKind kind;

        internal readonly SmelterState State;

        /// <summary>Products that came out, by product key.</summary>
        internal readonly Dictionary<string, int> Made = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Products finished (out, or into the held stack).</summary>
        internal int Finished;

        /// <summary>Of the queue it started with, how many are finished (first in, first out); for a station with no input,
        /// every product.</summary>
        internal int OriginalsFinished;

        private int originalsLeft;

        /// <summary>Its roof, as UpdateRoof sees it (only for a station that needs one).</summary>
        internal bool HaveRoof = true;

        /// <summary>Its smoke blocked, as UpdateSmoke sees it.</summary>
        internal bool BlockedSmoke;

        internal SmelterRun(SmelterKind kind, SmelterState start)
        {
            this.kind = kind;
            State = start.Copy();
            originalsLeft = State.Queue.Count;
        }

        /// <summary>Whether a second now would do anything (the game's own test at the top of its loop): fuel when it burns
        /// fuel, something queued when it takes input, its roof, its smoke clear. Without feeding, a station that isn't
        /// working stays so.</summary>
        internal bool Working =>
            !((kind.MaxFuel != 0 && !(State.Fuel > 0f)) || (kind.MaxOre != 0 && State.Queue.Count == 0) || !(kind.SecPerProduct > 0f) ||
              (kind.RequiresRoof && !HaveRoof) || BlockedSmoke);

        /// <summary>One whole second of the game's loop at <paramref name="power"/> (1, or the windmill's power), then the
        /// game's check after its loop.</summary>
        internal void Second(float power)
        {
            float fuel = State.Fuel;
            string ore = State.Queue.Count > 0 ? State.Queue[0] : string.Empty;
            if (!((kind.MaxFuel != 0 && !(fuel > 0f)) || (kind.MaxOre != 0 && ore == string.Empty) || !(kind.SecPerProduct > 0f) ||
                  (kind.RequiresRoof && !HaveRoof) || BlockedSmoke))
            {
                float num2 = 1f * power;
                if (kind.MaxFuel > 0)
                {
                    float num3 = kind.SecPerProduct / (float)kind.FuelPerProduct;
                    fuel -= num2 / num3;
                    if (fuel < 0.0001f)
                    {
                        fuel = 0f;
                    }
                    State.Fuel = fuel;
                }
                float bakeTimer = State.BakeTimer;
                bakeTimer += num2;
                State.BakeTimer = bakeTimer;
                if (bakeTimer >= kind.SecPerProduct)
                {
                    State.BakeTimer = 0f;
                    RemoveOneOre();
                    QueueProcessed(ore);
                }
            }
            if (State.Queue.Count == 0 || ((float)kind.MaxFuel > 0f && State.Fuel == 0f))
            {
                SpawnProcessed();
            }
        }

        private void RemoveOneOre()
        {
            if (State.Queue.Count > 0)
            {
                State.Queue.RemoveAt(0);
            }
        }

        // Smelter.QueueProcessed: one product, out at once, or into the held stack (the windmill).
        private void QueueProcessed(string ore)
        {
            Finished++;
            if (kind.Sourceless || kind.MaxOre == 0)
            {
                OriginalsFinished++;
            }
            else if (originalsLeft > 0)
            {
                originalsLeft--;
                OriginalsFinished++;
            }
            if (!kind.SpawnStack)
            {
                Spawn(ore, 1);
                return;
            }
            string text = State.SpawnOre ?? string.Empty;
            int num = State.SpawnAmount;
            if (text.Length > 0 || kind.Sourceless)
            {
                if (text != ore)
                {
                    SpawnProcessed();
                    State.SpawnOre = ore;
                    State.SpawnAmount = 1;
                    return;
                }
                num++;
                string product = kind.ProductOf(ore);
                if (product == null || num >= kind.MaxStackOf(product))
                {
                    Spawn(ore, num);
                    State.SpawnOre = string.Empty;
                    State.SpawnAmount = 0;
                }
                else
                {
                    State.SpawnAmount = num;
                }
            }
            else
            {
                State.SpawnOre = ore;
                State.SpawnAmount = 1;
            }
        }

        private void SpawnProcessed()
        {
            if (State.SpawnAmount > 0)
            {
                Spawn(State.SpawnOre, State.SpawnAmount);
                State.SpawnOre = string.Empty;
                State.SpawnAmount = 0;
            }
        }

        private void Spawn(string ore, int stack)
        {
            string product = kind.ProductOf(ore);
            if (product == null || stack <= 0)
            {
                return;
            }
            Made.TryGetValue(product, out int sum);
            Made[product] = sum + stack;
            ProductOut?.Invoke(product, stack);
        }

        /// <summary>Told of each product as it comes out (the feeding's linked output room, StopFeedingTheStation).</summary>
        internal Action<string, int> ProductOut;
    }

    /// <summary>
    /// Topper-Upper's feeding of one station as the catch-up runs it, by its own rules (FeedRule, Rooms): his order (the
    /// lanes), the room as the top-up level says, reserves, maximums counting what's made, never-take, leave-one (in what
    /// the chests can give), fuel only while there's something to process, and a full linked output stopping the station
    /// when the settings say so. It starts from the counts the game side makes (Counts: what's held near him, what counts
    /// toward each maximum, what the station's chests can give) and keeps them as the real loop's counts would stand at
    /// each look: what was taken comes off; what was queued counts toward its maximum (for a station with no input, the
    /// product its fuel is making now, Stations.CountMade's way, from the fuel left at each look); and what came out lies
    /// in the chests near the station, counted, and (with OutputsBack) there to be taken again (the windmill's Oat, both
    /// a product and an input). A group (the queue, the fuel) that got nothing when due, stopped by something that can't
    /// come back, isn't planned again.
    /// </summary>
    internal sealed class FeedBook
    {
        /// <summary>What the station takes into its queue, in his order (Stations.InputLanes); each lane's Prefab is the
        /// queue's name for it.</summary>
        internal IList<Lane> OreLanes = new List<Lane>();

        /// <summary>Its fuel's lane (the fuel item), or null for a station that burns none. For a station with no input,
        /// its Product and UnitsPerProduct are set (the Frigid Kiln: 5 Ice per Liquid Frost); PendingUnits is filled in at
        /// each look from the fuel in it.</summary>
        internal Lane FuelLane;

        internal StationTopUp TopUpAt = StationTopUp.HalfEmpty;
        internal bool FuelOnlyWhileWorking = true;

        /// <summary>What's held near him when it starts (the count reserves read), by item key.</summary>
        internal readonly Dictionary<string, int> HeldAtStart = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Each item's reserve, by key; missing: none.</summary>
        internal readonly Dictionary<string, int> Reserves = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>What the station's chests can give of each input when it starts (leave-one taken off), by input key;
        /// missing: none.</summary>
        internal readonly Dictionary<string, int> CanGiveAtStart = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>What counts toward each product's maximum when it starts (held, queued, lying at outputs), by key.</summary>
        internal readonly Dictionary<string, int> CountedAtStart = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Each product's maximum, by key; missing: none.</summary>
        internal readonly Dictionary<string, int> Maximums = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>The station lies outside the count its feed uses: a product with a maximum gets nothing (as a feed, m1).</summary>
        internal bool Uncounted;

        /// <summary>What comes out goes where the feed takes from (no linked chests; put away near the station), so a
        /// product that is also an input can be taken again.</summary>
        internal bool OutputsBack;

        /// <summary>For "stop feeding the station" with outputs linked: each product's room in the linked outputs when it
        /// starts; null otherwise. A product whose room has filled with what came out gets no more input.</summary>
        internal Dictionary<string, int> LinkedRoomAtStart;

        /// <summary>Items never taken (by key).</summary>
        internal Func<string, bool> Never = key => false;

        /// <summary>Taken so far, by input key.</summary>
        internal readonly Dictionary<string, int> Taken = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Products queued so far (to be made from what was put in), by product key.</summary>
        internal readonly Dictionary<string, int> Queued = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Products out so far, by product key.</summary>
        internal readonly Dictionary<string, int> Out = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>How many looks put something in.</summary>
        internal int Feeds;

        private bool queueDone;
        private bool fuelDone;
        private string sourcelessProduct;
        private int pendingAtStart = -1;
        private int pendingNow;

        /// <summary>A fresh copy of the starting values, nothing spent (for a second run of the same catch-up).</summary>
        internal FeedBook Fresh()
        {
            var copy = new FeedBook
            {
                OreLanes = OreLanes,
                FuelLane = FuelLane,
                TopUpAt = TopUpAt,
                FuelOnlyWhileWorking = FuelOnlyWhileWorking,
                Uncounted = Uncounted,
                OutputsBack = OutputsBack,
                LinkedRoomAtStart = LinkedRoomAtStart,
                Never = Never,
            };
            Copy(HeldAtStart, copy.HeldAtStart);
            Copy(Reserves, copy.Reserves);
            Copy(CanGiveAtStart, copy.CanGiveAtStart);
            Copy(CountedAtStart, copy.CountedAtStart);
            Copy(Maximums, copy.Maximums);
            return copy;
        }

        /// <summary>What's held near him now: at the start, plus what came out (put away near the station), less what was
        /// taken.</summary>
        internal int Held(string key) => Get(HeldAtStart, key) + Get(Out, key) - Get(Taken, key);

        internal int AboveReserve(string input) =>
            FeedRule.AboveReserve(Held(input), input != null && Reserves.TryGetValue(input, out int reserve) ? reserve : (int?)null);

        internal int CanGive(string input) => Math.Max(0, Get(CanGiveAtStart, input) + (OutputsBack ? Get(Out, input) : 0) - Get(Taken, input));

        /// <summary>What counts toward a product's maximum now, as Counts would count it at this look.</summary>
        internal int Counted(string product)
        {
            int counted = Get(CountedAtStart, product) - Get(Taken, product);
            if (product != null && product == sourcelessProduct && pendingAtStart >= 0)
            {
                // Out into the chests, and in progress from the fuel in it now (FeedRule.Pending, as CountMade counts it).
                return counted + Get(Out, product) + pendingNow - pendingAtStart;
            }
            return counted + Get(Queued, product);
        }

        internal int Wanted(string product)
        {
            if (LinkedRoomAtStart != null && LinkedRoomAtStart.TryGetValue(product, out int room) && room - Get(Out, product) <= 0)
            {
                return 0;   // its linked output is full: the station waits (StopFeedingTheStation)
            }
            if (product == null || !Maximums.TryGetValue(product, out int most))
            {
                return FeedRule.Unlimited;
            }
            return Uncounted ? 0 : FeedRule.Wanted(Counted(product), most);
        }

        internal void NoteOut(string product, int count) => Add(Out, product, count);

        /// <summary>Whether no look can ever put anything in again: the queue and the fuel each have nothing to feed, or got
        /// nothing for a reason that can't come back.</summary>
        internal bool Exhausted(SmelterKind kind) =>
            (queueDone || kind.Sourceless || kind.MaxOre <= 0 || OreLanes.Count == 0) && (fuelDone || FuelLane == null || kind.MaxFuel <= 0);

        /// <summary>
        /// One look of Topper-Upper's loop at the station, as Stations.Feed does it: the queue topped up when due, then the
        /// fuel when due (with "fuel only while there's something to process", only while something is queued, after this
        /// look's queue). Returns whether anything went in.
        /// </summary>
        internal bool Look(SmelterKind kind, SmelterState state)
        {
            if (kind.Sourceless && FuelLane != null)
            {
                sourcelessProduct = FuelLane.Product;
                int units = Math.Max(1, FuelLane.UnitsPerProduct);
                pendingNow = FeedRule.Pending(state.Fuel, units);
                if (pendingAtStart < 0)
                {
                    pendingAtStart = pendingNow;   // what the starting count held of it
                }
            }
            bool gave = false;
            if (!queueDone && !kind.Sourceless && kind.MaxOre > 0 && OreLanes.Count > 0)
            {
                int room = Rooms.Queue(state.Queue.Count, kind.MaxOre);
                if (Rooms.StationDue(room, kind.MaxOre, TopUpAt))
                {
                    List<Grant> grants = FeedRule.Plan(room, OreLanes, AboveReserve, Wanted, CanGive, Never);
                    if (grants.Count == 0 && Stuck(OreLanes, false))
                    {
                        queueDone = true;
                    }
                    foreach (Grant grant in grants)
                    {
                        for (int i = 0; i < grant.Amount; i++)
                        {
                            state.Queue.Add(grant.Lane.Prefab);
                        }
                        Spend(grant, grant.Amount, grant.Products);
                        gave = true;
                    }
                }
            }
            if (!fuelDone && FuelLane != null && kind.MaxFuel > 0)
            {
                bool working = kind.Sourceless || state.Queue.Count > 0 || !FuelOnlyWhileWorking;
                int room = Rooms.Fuel(state.Fuel, kind.MaxFuel);
                if (working && Rooms.StationDue(room, kind.MaxFuel, TopUpAt))
                {
                    var lane = new Lane
                    {
                        Input = FuelLane.Input,
                        Prefab = FuelLane.Prefab,
                        Product = kind.Sourceless ? FuelLane.Product : null,
                        UnitsPerProduct = kind.Sourceless ? Math.Max(1, FuelLane.UnitsPerProduct) : 1,
                        PendingUnits = kind.Sourceless ? state.Fuel : 0f,
                    };
                    var lanes = new List<Lane> { lane };
                    List<Grant> grants = FeedRule.Plan(room, lanes, AboveReserve, Wanted, CanGive, Never);
                    if (grants.Count == 0 && Stuck(lanes, kind.Sourceless))
                    {
                        fuelDone = true;
                    }
                    foreach (Grant grant in grants)
                    {
                        state.Fuel += grant.Amount;   // as Adds.SmelterFuel writes it: the fuel plus the units
                        Spend(grant, grant.Amount, grant.Products);
                        gave = true;
                    }
                }
            }
            if (gave)
            {
                Feeds++;
            }
            return gave;
        }

        /// <summary>A fermenter's fill, as Stations.Fermenter does it: one mead base by his order and rules (room 1), or
        /// null when none may go in. Spends it.</summary>
        internal Lane FillFermenter()
        {
            if (queueDone)
            {
                return null;
            }
            List<Grant> grants = FeedRule.Plan(1, OreLanes, AboveReserve, Wanted, CanGive, Never);
            if (grants.Count == 0)
            {
                queueDone = true;   // nothing comes back into a fermenter's chests while it ferments
                return null;
            }
            Spend(grants[0], 1, grants[0].Lane.ProductsPerBatch);
            Feeds++;
            return grants[0].Lane;
        }

        // Whether every lane got nothing for a reason that can't change as the time goes on: never taken; none to give or
        // none above its reserve (unless what comes out can be taken again, and comes out of this station); or its product
        // at its maximum, or its linked output full (unless the product is itself taken, or is counted from the fuel in a
        // station with no input, which falls as it burns).
        private bool Stuck(IList<Lane> lanes, bool sourceless)
        {
            foreach (Lane lane in lanes)
            {
                if (lane?.Input == null || Never(lane.Input))
                {
                    continue;
                }
                bool comesBack = OutputsBack && IsProduct(lane.Input);
                if (!comesBack && (CanGive(lane.Input) <= 0 || AboveReserve(lane.Input) <= 0))
                {
                    continue;
                }
                if (lane.Product != null && !sourceless && !IsInput(lane.Product) &&
                    FeedRule.MostUnits(Wanted(lane.Product), lane.UnitsPerProduct, lane.ProductsPerBatch, lane.PendingUnits) <= 0)
                {
                    continue;
                }
                return false;
            }
            return true;
        }

        private bool IsProduct(string key)
        {
            foreach (Lane lane in OreLanes)
            {
                if (lane?.Product == key)
                {
                    return true;
                }
            }
            return FuelLane?.Product == key;
        }

        private bool IsInput(string key)
        {
            foreach (Lane lane in OreLanes)
            {
                if (lane?.Input == key)
                {
                    return true;
                }
            }
            return FuelLane?.Input == key;
        }

        private void Spend(Grant grant, int amount, int products)
        {
            Add(Taken, grant.Input, amount);
            if (grant.Product != null)
            {
                Add(Queued, grant.Product, products);
            }
        }

        private static void Copy(Dictionary<string, int> from, Dictionary<string, int> into)
        {
            foreach (KeyValuePair<string, int> each in from)
            {
                into[each.Key] = each.Value;
            }
        }

        private static int Get(Dictionary<string, int> from, string key) => key != null && from.TryGetValue(key, out int value) ? value : 0;

        private static void Add(Dictionary<string, int> into, string key, int amount)
        {
            if (key == null || amount <= 0)
            {
                return;
            }
            into[key] = Get(into, key) + amount;
        }
    }

    /// <summary>How long each part of a smelter's catch-up runs, in whole seconds, and at what power.</summary>
    internal struct CatchUpTimes
    {
        /// <summary>The time with players on made up (at most the cap): fed by Topper-Upper's rules, at the chosen wind.</summary>
        internal int Fed;

        /// <summary>The game's own catch-up of what's in it (up to an hour of world time, the accumulator included).</summary>
        internal int Gift;

        /// <summary>The power while fed (the windmill's chosen wind; 1 for any other station).</summary>
        internal float FedPower;

        /// <summary>The power of the game's own catch-up (the windmill's power now, as the game itself uses it; else 1).</summary>
        internal float GiftPower;

        /// <summary>How often Topper-Upper's loop looks at a station, in seconds ("Look at each station every").</summary>
        internal int LookEvery;
    }

    /// <summary>What a smelter's catch-up comes to.</summary>
    internal sealed class SmelterOutcome
    {
        /// <summary>The station's record at the end.</summary>
        internal SmelterState State;

        /// <summary>Products that came out, by product key: to be put away.</summary>
        internal Dictionary<string, int> Made;

        /// <summary>The feeding's book: what was taken, what was queued.</summary>
        internal FeedBook Book;

        /// <summary>Seconds run fed, run as the game's own catch-up after it, and run beyond that so the station never
        /// finishes less of what was in it than the game alone would.</summary>
        internal int FedSeconds;
        internal int GiftSeconds;
        internal int ExtraSeconds;

        /// <summary>What the game alone would have finished of what was in it, and what the catch-up finished of it.</summary>
        internal int GameAloneFinished;
        internal int OriginalsFinished;

        internal int TotalSeconds => FedSeconds + GiftSeconds + ExtraSeconds;
    }

    /// <summary>
    /// A smelter's catch-up (card A93), worked out on plain values. First the time with players on (Fed seconds, at most
    /// the cap): each second the game's own loop at the chosen power, Topper-Upper's loop looking every few seconds and
    /// feeding by its rules (FeedBook). Then, if the game's own catch-up (Gift: up to an hour of world time, which it
    /// always gives what's in a station) is longer, the rest of it unfed at the game's own power. Then, should the catch-up
    /// still have finished less of what was in the station than the game alone would have (a windmill whose chosen wind
    /// was weaker than the wind now), more unfed seconds at the game's power until it hasn't (at most another Gift): what
    /// was already in it always comes out as it would without the mod.
    /// </summary>
    internal static class CatchUpSim
    {
        /// <summary>The game's own catch-up, at most (Smelter.UpdateSmelter's accumulator), in seconds.</summary>
        internal const double GameCatchUpMost = 3600.0;

        /// <summary>
        /// A smelter's two times, in whole seconds: <paramref name="fed"/>, the time with players on since it last ran
        /// (<paramref name="played"/>), at most <paramref name="cap"/>; <paramref name="gift"/>, the game's own catch-up of
        /// what's in it: its accumulator plus the world time since it last ran (<paramref name="elapsed"/>), at most an
        /// hour, as Smelter.UpdateSmelter adds them up (in single precision, spent a whole second at a time).
        /// </summary>
        internal static void Seconds(double played, double cap, float accumulator, double elapsed, out int fed, out int gift)
        {
            fed = (int)Math.Floor(Math.Max(0.0, Math.Min(played, Math.Max(0.0, cap))));
            float sum = accumulator + (float)elapsed;
            if (sum > (float)GameCatchUpMost)
            {
                sum = (float)GameCatchUpMost;
            }
            gift = sum >= 1f ? (int)Math.Floor(sum) : 0;
        }

        /// <summary>
        /// What a catch-up takes out of the chests and what it puts into them, by key: what the station took, and what came
        /// out of it. With OutputsBack (what comes out lies where the feed takes from), an item both made and taken again in
        /// that time (the windmill's Oat) never leaves the chests: only the difference is taken, or put away. The chests
        /// never give more than they held at the start (the book saw to that at every look).
        /// </summary>
        internal static void Flows(FeedBook book, IDictionary<string, int> made, out Dictionary<string, int> take, out Dictionary<string, int> give)
        {
            take = new Dictionary<string, int>(StringComparer.Ordinal);
            give = new Dictionary<string, int>(StringComparer.Ordinal);
            if (book != null)
            {
                foreach (KeyValuePair<string, int> each in book.Taken)
                {
                    if (each.Value > 0)
                    {
                        take[each.Key] = each.Value;
                    }
                }
            }
            if (made != null)
            {
                foreach (KeyValuePair<string, int> each in made)
                {
                    if (each.Value > 0)
                    {
                        give[each.Key] = each.Value;
                    }
                }
            }
            if (book == null || !book.OutputsBack)
            {
                return;
            }
            foreach (string key in new List<string>(take.Keys))
            {
                if (give.TryGetValue(key, out int came))
                {
                    int both = Math.Min(take[key], came);
                    take[key] -= both;
                    give[key] -= both;
                    if (take[key] == 0)
                    {
                        take.Remove(key);
                    }
                    if (give[key] == 0)
                    {
                        give.Remove(key);
                    }
                }
            }
        }

        internal static SmelterOutcome Smelter(SmelterKind kind, SmelterState start, FeedBook book, CatchUpTimes times, bool haveRoof, bool blockedSmoke)
        {
            // The game alone (what's in it, for its own catch-up's time, at its own power) is needed only when the chosen
            // power is the weaker (a windmill): fed at least as strongly, with more put in behind it, the station finishes
            // what was in it no later than the game alone would. That holds for a station that burns no fuel, as every
            // windmill (power other than 1 is only ever a windmill's); a fuel-burning one at a stronger power wastes more
            // fuel a product and may run out first, but extra seconds couldn't help it then either (no fuel).
            int aloneFinished = -1;
            if (times.FedPower < times.GiftPower)
            {
                var alone = new SmelterRun(kind, start) { HaveRoof = haveRoof, BlockedSmoke = blockedSmoke };
                for (int s = 0; s < times.Gift; s++)
                {
                    alone.Second(times.GiftPower);
                    if (!alone.Working)
                    {
                        break;
                    }
                }
                aloneFinished = alone.OriginalsFinished;
            }

            var run = new SmelterRun(kind, start) { HaveRoof = haveRoof, BlockedSmoke = blockedSmoke };
            // A station that needs a roof and has none isn't fed (Stations.NeedsRoof): its items would only sit in it.
            bool feeding = book != null && !(kind.RequiresRoof && !haveRoof);
            if (book != null)
            {
                run.ProductOut = book.NoteOut;
            }
            int every = Math.Max(1, times.LookEvery);
            for (int s = 0; s < times.Fed; s++)
            {
                if (feeding && s % every == 0)
                {
                    book.Look(kind, run.State);
                }
                run.Second(times.FedPower);
                if (!run.Working && (!feeding || book.Exhausted(kind)))
                {
                    break;   // idle, with nothing left that could ever be fed: the rest of the time changes nothing
                }
            }
            int gift = Math.Max(0, times.Gift - times.Fed);
            for (int s = 0; s < gift; s++)
            {
                run.Second(times.GiftPower);   // at least once: the game's check after its loop lets a held stack out
                if (!run.Working)
                {
                    break;   // unfed, a station that isn't working stays so
                }
            }
            int extra = 0;
            while (run.OriginalsFinished < aloneFinished && extra < times.Gift && run.Working)
            {
                run.Second(times.GiftPower);
                extra++;
            }
            run.State.Accumulator = 0f;   // the time is spent: the record's StartTime is set to now with it
            return new SmelterOutcome
            {
                State = run.State,
                Made = run.Made,
                Book = book,
                FedSeconds = times.Fed,
                GiftSeconds = gift,
                ExtraSeconds = extra,
                GameAloneFinished = aloneFinished,
                OriginalsFinished = run.OriginalsFinished,
            };
        }

        /// <summary>
        /// A fermenter's catch-up: it was ready <paramref name="played"/> seconds of play ago (at most the cap). It's tapped
        /// (what was in it, as the game finished it on world time), then filled by the book (one base by his rules) and
        /// fermented for <paramref name="duration"/> seconds, tapped again and filled again, as long as each batch is ready
        /// within the time (ready when its time is past the duration, as the game's GetStatus has it). Returns what was
        /// tapped, in order, and what's in it at the end with how long it has fermented.
        /// </summary>
        internal static FermentOutcome Fermenter(string readyContent, double played, double duration, FeedBook book)
        {
            var outcome = new FermentOutcome();
            outcome.Tapped.Add(readyContent);
            if (book == null || duration <= 0.0 || played < 0.0)
            {
                return outcome;
            }
            double at = 0.0;
            for (int guard = 0; guard < 100000; guard++)
            {
                Lane filled = book.FillFermenter();
                if (filled == null)
                {
                    return outcome;   // nothing more to put in: it stays empty
                }
                if (at + duration < played)
                {
                    outcome.Tapped.Add(filled.Prefab);
                    outcome.Fills.Add(filled);
                    at += duration;
                    continue;
                }
                outcome.Fills.Add(filled);
                outcome.Content = filled;
                outcome.Fermented = Math.Max(0.0, played - at);
                return outcome;
            }
            return outcome;
        }
    }

    /// <summary>What a fermenter's catch-up comes to.</summary>
    internal sealed class FermentOutcome
    {
        /// <summary>What was tapped, in order, by input prefab name (the first is what was ready in it).</summary>
        internal readonly List<string> Tapped = new List<string>();

        /// <summary>The bases put in, in order.</summary>
        internal readonly List<Lane> Fills = new List<Lane>();

        /// <summary>What's in it at the end (null: empty), and for how many seconds of play it has fermented.</summary>
        internal Lane Content;
        internal double Fermented;
    }
}
