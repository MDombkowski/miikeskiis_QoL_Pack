// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>A take asked of the player who has a chest open (ChestShare), for a claim (ChestClaims.Start): the chest,
    /// and how much of each item (by key) to take from it, in the plan's order (levels first).</summary>
    internal sealed class ShareTake
    {
        internal readonly Container Chest;

        internal readonly List<KeyValuePair<string, int>> Wants = new List<KeyValuePair<string, int>>();

        /// <summary>What asking came to (ChestShare.Ask sets it as the claim starts): NotSent at once, with NoRoom, when his
        /// inventory had no room for any of it.</summary>
        internal ShareResult Result;

        internal ShareTake(Container chest)
        {
            Chest = chest;
        }

        /// <summary>Adds to what is wanted of an item (by key), keeping the order in which items were first added.</summary>
        internal void Add(string key, int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            for (int i = 0; i < Wants.Count; i++)
            {
                if (Wants[i].Key == key)
                {
                    Wants[i] = new KeyValuePair<string, int>(key, Wants[i].Value + amount);
                    return;
                }
            }
            Wants.Add(new KeyValuePair<string, int>(key, amount));
        }
    }

    internal enum ShareStatus
    {
        /// <summary>Nothing was asked: nothing fit in his inventory, or the chest was gone.</summary>
        NotSent,

        /// <summary>Asked; no reply yet.</summary>
        Waiting,

        /// <summary>The owner took what it could (Given; possibly nothing, the chest being emptied or reserved meanwhile).</summary>
        Given,

        /// <summary>No reply within ChestShare.ReplyWait. A late reply is still delivered (ChestShare), but not counted here.</summary>
        NoAnswer,

        /// <summary>That game no longer owned the chest: its player closed it, and it is free to claim as usual.</summary>
        NotOwner,

        /// <summary>The owner's game refused: the game's own privacy check, or not one of the game's chests.</summary>
        Refused,

        /// <summary>The chest was gone, or not loaded, on either computer.</summary>
        Gone,
    }

    /// <summary>What a take through a chest's owner came to.</summary>
    internal sealed class ShareResult
    {
        internal Container Chest;

        internal ShareStatus Status;

        /// <summary>What was asked, by key: what was wanted, less what his inventory had no room for.</summary>
        internal readonly Dictionary<string, int> Asked = new Dictionary<string, int>();

        /// <summary>What the owner gave, by key. The chest lost exactly this, and it is all his: in his inventory, or, what
        /// didn't fit, at his feet (Dropped).</summary>
        internal readonly Dictionary<string, int> Given = new Dictionary<string, int>();

        /// <summary>How many of the items given didn't fit in his inventory and were dropped at his feet.</summary>
        internal int Dropped;

        /// <summary>Less was asked than wanted (or nothing): his inventory had no room for it.</summary>
        internal bool NoRoom;

        internal int GivenOf(string key) => Given.TryGetValue(key, out int amount) ? amount : 0;

        internal int GivenInAll
        {
            get
            {
                int total = 0;
                foreach (int amount in Given.Values)
                {
                    total += amount;
                }
                return total;
            }
        }
    }

    /// <summary>
    /// Sharing a chest someone has open (card A37): what lies in a chest a friend on the pack has open stays usable, by
    /// asking his game to take it out of the real chest and hand it over. The game itself never hands an open chest to
    /// anyone else (Container.RPC_RequestOpen answers "in use"), so without this a friend's open chest drops out of every
    /// count until it closes; it still does for a friend whose game doesn't share (no pack, or an older one).
    ///
    /// Who shares: when his game joins, the pack says hello to everyone through the server (a routed message, which a game
    /// without the pack ignores), and every game on the pack answers him directly. A friend seen at an open chest whom his
    /// game hasn't heard from is asked once more, directly (AskAgainAfter). Nothing is written into the world. A chest is
    /// shareable (CanShare) while its record says someone has it open, its owner said hello, and the owner's character
    /// stands at it: the player who has it open. Counting (ChestStock.Survey) counts shareable chests like any other.
    ///
    /// A take through the owner is a request addressed to that owner's game (the items by key, in the plan's order), which
    /// takes them from the live chest with the service's own ChestStock.Take (leave-one, lowest levels first, the world
    /// level, Reserve Stock's mark, never more than asked), sparing the stack its player is dragging or splitting, then sends
    /// and holds the chest as after any take: the owner's open window shows it leave at once. The reply carries the items
    /// in the game's own format (Inventory.Save) straight back to the asker, whose game adds them to his inventory, measured,
    /// dropping at his feet whatever didn't fit. He asks only for what fits (Room), so that is rare.
    ///
    /// Timeouts: a take not answered within ReplyWait counts as no answer. A reply that comes later is still delivered,
    /// with a word: the items left the chest, so they are his. A reply is delivered once, and only from the game asked.
    ///
    /// The rare cases, kept as small as can be: a player whose game crashes or leaves in the moment between the owner's
    /// take and the reply's arrival loses what was in flight (the server drops a message for a player who has gone); and
    /// if the owner's game crashes in the moment after its take, before the chest's record has gone out (the reply goes at
    /// once, the record with the next send, a twentieth of a second later), the server keeps the chest as it was while the
    /// asker has the items. The owner takes and replies in the one frame the request arrives, so each moment is one trip
    /// through the server; and nothing is ever asked ahead of a click, a craft or a fetch. Faults are logged (a reply that
    /// can't be sent, an item his game doesn't know).
    ///
    /// Sharing is on while any module using the service is on (Uses): a player who switched every one of them off gets
    /// nothing taken from his open chests, and his game tells the others so (a hello saying it doesn't share).
    /// </summary>
    internal static class ChestShare
    {
        /// <summary>The protocol this copy speaks. A friend's game counts as sharing only from this version on.</summary>
        internal const int Protocol = 1;

        /// <summary>How long to wait for a friend's game to answer a take, in seconds.</summary>
        internal const float ReplyWait = 4f;

        /// <summary>A friend seen at an open chest whose game hasn't said hello is asked again after this long, in seconds.</summary>
        internal const float AskAgainAfter = 20f;

        /// <summary>How long a take given up on is remembered, for saying its reply arrived late, in seconds.</summary>
        private const float RememberFor = 120f;

        /// <summary>At most this many kinds of item in one take, and this many of one: a request past either is refused.</summary>
        private const int MostKeys = 64;

        private const int MostOfOne = 100000;

        /// <summary>The scratch inventory a take goes into on the owner's game: four columns (a temporary inventory's), this
        /// many rows. A take that fills it stops, measured, as Take stops at a full inventory.</summary>
        private const int ScratchRows = 64;

        internal const string HelloName = "modprojects.chests.hello";
        internal const string TakeName = "modprojects.chests.take";
        private const string ReplyPrefix = "modprojects.chests.given.";

        /// <summary>This copy's own reply name: a request says where to reply, so the copy of the service that asked gets
        /// the answer even with two plugins carrying the service at once.</summary>
        internal static readonly string ReplyName = ReplyPrefix + typeof(ChestShare).Assembly.GetName().Name.ToLowerInvariant();

        // What the game provides, as seams: only the scratch test harness, which has no network, engine or item prefabs,
        // ever sets another.

        /// <summary>Sends a routed message to one game (or ZRoutedRpc.Everybody).</summary>
        internal static Action<long, string, ZPackage> Send = (target, name, package) => ZRoutedRpc.instance.InvokeRoutedRPC(target, name, package);

        /// <summary>Whether the player of that game stands within reach of the chest (ChestClaims.PlayerWithinReach).</summary>
        internal static Func<Container, long, bool> OwnerAt = ChestClaims.PlayerWithinReach;

        /// <summary>Whether the owner's game may share this chest with that player (MayShare).</summary>
        internal static Func<Container, long, bool> Shareable = MayShare;

        /// <summary>Items into the game's own format, and back.</summary>
        internal static Func<List<ItemDrop.ItemData>, ZPackage> Pack = PackItems;

        internal static Func<ZPackage, List<ItemDrop.ItemData>> Unpack = UnpackItems;

        /// <summary>Drops some of an item at his feet: what didn't fit in his inventory.</summary>
        internal static Action<ItemDrop.ItemData, int> DropAtFeet = DropNearHim;

        /// <summary>A line for him, top left, as the game says what he picks up.</summary>
        internal static Action<string> Tell = TellHim;

        /// <summary>Whether MultiUserChest is loaded (ChestHooks, at the first turn): its owner of an open chest says "yes"
        /// without handing it over, so a claim calls such a chest in use after a moment (ChestClaims).</summary>
        internal static bool MultiUserChest;

        // The modules using the service (ChestHooks.Resolve): sharing is on while any of them is.
        private static readonly List<ChestUser> Users = new List<ChestUser>();

        private static ZRoutedRpc registeredOn;
        private static bool answering;
        private static bool helloSaid;
        private static bool saidSharing;
        private static bool toldNotAnswering;

        // Faults already logged, by what failed: each is logged once (those that lose items, every time: Lost).
        private static readonly HashSet<string> ToldFaults = new HashSet<string>();
        private static long nextNumber = 1;

        // Games that said hello, by their network ID, and the protocol each speaks.
        private static readonly Dictionary<long, int> Peers = new Dictionary<long, int>();

        // Friends at an open chest asked for a hello, and when.
        private static readonly Dictionary<long, float> Queried = new Dictionary<long, float>();

        // Takes sent and not yet answered (or given up on, for a while), by their number.
        private static readonly Dictionary<long, Pending> Sent = new Dictionary<long, Pending>();

        // Numbers of takes whose reply has been delivered: each at most once.
        private static readonly HashSet<long> Delivered = new HashSet<long>();

        // Replies that arrived while he had no character (dead, respawning): delivered as soon as he has one.
        private static readonly List<List<ItemDrop.ItemData>> Undelivered = new List<List<ItemDrop.ItemData>>();

        private sealed class Pending
        {
            internal ShareResult Result;
            internal float SentAt;
            internal long Owner;
            internal bool GivenUp;
        }

        /// <summary>A module that uses the service (from ChestHooks.Resolve): sharing his open chests is on while any is on.</summary>
        internal static void Uses(ChestUser user)
        {
            if (user != null && !Users.Contains(user))
            {
                Users.Add(user);
            }
        }

        /// <summary>True while any module using the service is on: then his game shares his open chests.</summary>
        internal static bool Sharing
        {
            get
            {
                foreach (ChestUser user in Users)
                {
                    if (user.IsOn)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Every frame, from ChestClaims.Tick: registers on each new connection's messages, says hello once he is
        /// in the world, delivers what arrived while he had no character, and gives up on takes not answered in time.</summary>
        internal static void Tick()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null)
            {
                return;
            }
            if (rpc != registeredOn)
            {
                Register(rpc);
            }
            Player player = Player.m_localPlayer;
            if (answering && player != null && ZNet.instance != null && ZDOMan.instance != null)
            {
                bool sharing = Sharing;
                if (!helloSaid || sharing != saidSharing)
                {
                    // Once he is in the world, and again whenever sharing turns on or off (every module switched off, or one
                    // back on): the others then count, or stop counting, his open chests.
                    helloSaid = true;
                    saidSharing = sharing;
                    SayHello(ZRoutedRpc.Everybody, reply: false);
                }
            }
            if (Undelivered.Count > 0 && player != null)
            {
                var waiting = new List<List<ItemDrop.ItemData>>(Undelivered);
                Undelivered.Clear();
                foreach (List<ItemDrop.ItemData> items in waiting)
                {
                    Deliver(items);
                }
            }
            if (Sent.Count > 0)
            {
                float now = ChestClaims.Clock();
                List<long> forget = null;
                foreach (KeyValuePair<long, Pending> each in Sent)
                {
                    Pending pending = each.Value;
                    if (!pending.GivenUp && now - pending.SentAt > ReplyWait)
                    {
                        pending.GivenUp = true;
                        pending.Result.Status = ShareStatus.NoAnswer;
                    }
                    if (pending.GivenUp && now - pending.SentAt > RememberFor)
                    {
                        (forget = forget ?? new List<long>()).Add(each.Key);
                    }
                }
                if (forget != null)
                {
                    foreach (long number in forget)
                    {
                        Sent.Remove(number);
                    }
                }
            }
        }

        // A new connection: its message handlers, and everything known of the last one forgotten.
        private static void Register(ZRoutedRpc rpc)
        {
            registeredOn = rpc;
            Peers.Clear();
            Queried.Clear();
            helloSaid = false;
            answering = false;
            foreach (Pending pending in Sent.Values)
            {
                if (!pending.GivenUp)
                {
                    pending.GivenUp = true;   // asked over the last connection: no reply can come over this one
                    pending.Result.Status = ShareStatus.NoAnswer;
                }
            }
            try
            {
                rpc.Register<ZPackage>(HelloName, OnHello);
                rpc.Register<ZPackage>(TakeName, OnTake);
                answering = true;
            }
            catch (ArgumentException)
            {
                // Another plugin carrying the chest service registered them first (the bundle beside an older single
                // release): that copy shares this computer's open chests. This one counts no friend's open chest.
                if (!toldNotAnswering)
                {
                    toldNotAnswering = true;
                    ZLog.Log($"[QoL Mods chest service] Another plugin's copy of the chest service shares open chests on this computer; " +
                             $"{typeof(ChestShare).Assembly.GetName().Name}'s copy leaves friends' open chests out of its counts.");
                }
            }
            try
            {
                rpc.Register<ZPackage>(ReplyName, OnGiven);
            }
            catch (ArgumentException)
            {
                // The same plugin twice: its first copy gets the replies.
            }
        }

        // A hello: the protocol this game speaks, or 0 while it doesn't share (Sharing).
        private static void SayHello(long target, bool reply)
        {
            var package = new ZPackage();
            package.Write(Sharing ? Protocol : 0);
            package.Write(reply);
            Send(target, HelloName, package);
        }

        // A hello: remember that game, and answer it directly unless this was its answer.
        private static void OnHello(long sender, ZPackage package)
        {
            try
            {
                int protocol = package.ReadInt();
                bool reply = package.ReadBool();
                if (sender == ZNet.GetUID())
                {
                    return;   // this game's own hello, to everybody
                }
                bool wasSharing = Peers.TryGetValue(sender, out int before) && before >= Protocol;
                if (!Peers.ContainsKey(sender) || wasSharing != protocol >= Protocol)
                {
                    ZLog.Log(protocol >= Protocol
                        ? $"[QoL Mods chest service] The game {sender} shares its open chests (protocol {protocol})."
                        : $"[QoL Mods chest service] The game {sender} runs the pack but doesn't share its open chests now.");
                }
                Peers[sender] = protocol;
                Queried.Remove(sender);
                if (!reply)
                {
                    SayHello(sender, reply: true);
                }
            }
            catch (Exception error)
            {
                Fault("A friend's hello couldn't be read", error);
            }
        }

        /// <summary>
        /// True if a friend's game shares this chest: its record says someone has it open, this computer doesn't own it,
        /// its owner's game said hello, and the owner's character stands at it (the player who has it open; a friend who
        /// left or crashed has no character there). A friend at an open chest whose game hasn't said hello is asked, at
        /// most once every AskAgainAfter seconds; until he answers, the chest isn't shareable: nobody is waited on.
        /// </summary>
        internal static bool CanShare(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid() || view.IsOwner())
            {
                return false;
            }
            ZDO record = view.GetZDO();
            if (record.GetInt(ZDOVars.s_inUse) != 1)
            {
                return false;
            }
            long owner = record.GetOwner();
            if (owner == 0L || !OwnerAt(chest, owner))
            {
                return false;
            }
            if (Peers.TryGetValue(owner, out int protocol))
            {
                return protocol >= Protocol;
            }
            Query(owner);
            return false;
        }

        // Asks one friend's game for a hello, at most once every AskAgainAfter seconds: one that missed his first hello.
        private static void Query(long uid)
        {
            if (!answering || registeredOn == null || registeredOn != ZRoutedRpc.instance)
            {
                return;
            }
            float now = ChestClaims.Clock();
            if (Queried.TryGetValue(uid, out float asked) && now - asked < AskAgainAfter)
            {
                return;
            }
            Queried[uid] = now;
            try
            {
                SayHello(uid, reply: false);
            }
            catch (Exception error)
            {
                Fault("A friend's game couldn't be asked whether it shares", error);
            }
        }

        /// <summary>
        /// Asks the owner of a shareable chest for up to what <paramref name="take"/> wants, no more of each than fits in
        /// his inventory (<paramref name="room"/>, shared by every take of one claim, so they fit together). Returns the
        /// take's result, Waiting until the reply comes (ChestClaims waits for it), or NotSent.
        /// </summary>
        internal static ShareResult Ask(ShareTake take, ChestRules rules, Room room, long playerId)
        {
            var result = new ShareResult { Chest = take.Chest, Status = ShareStatus.NotSent };
            take.Result = result;
            ZNetView view = take.Chest != null ? ChestHooks.NetView(take.Chest) : null;
            if (view == null || !view.IsValid())
            {
                result.Status = ShareStatus.Gone;
                return result;
            }
            if (view.IsOwner())
            {
                result.Status = ShareStatus.NotOwner;   // his now: taken the usual way in a later round
                return result;
            }
            long owner = view.GetZDO().GetOwner();
            if (owner == 0L)
            {
                result.Status = ShareStatus.NotOwner;   // nobody's now: never asked of everybody
                return result;
            }
            List<KeyValuePair<string, int>> asked = room != null
                ? room.Fit(take.Chest, take.Wants, rules.LeaveOne, rules.UseReserved, rules.LeaveOneOfGear)
                : new List<KeyValuePair<string, int>>();
            int wanted = 0, fits = 0;
            foreach (KeyValuePair<string, int> want in take.Wants)
            {
                wanted += Math.Max(0, want.Value);
            }
            foreach (KeyValuePair<string, int> each in asked)
            {
                fits += each.Value;
            }
            result.NoRoom = fits < wanted;
            if (asked.Count > MostKeys)
            {
                // The owner refuses more kinds in one request: the rest stays in the chest, for another chest or take.
                asked.RemoveRange(MostKeys, asked.Count - MostKeys);
            }
            foreach (KeyValuePair<string, int> each in asked)
            {
                result.Asked[each.Key] = each.Value;
            }
            if (asked.Count == 0)
            {
                return result;   // nothing fits: nothing asked
            }
            long number = nextNumber++;
            var package = new ZPackage();
            package.Write(Protocol);
            package.Write(number);
            package.Write(view.GetZDO().m_uid);
            package.Write(playerId);
            package.Write(ReplyName);
            // 4, gear too (card A83): an older owner's game ignores it and keeps back only what stacks. It never gives more
            // than asked, and this game asks only for what its own count (gear kept back) allows; but if the chest changed
            // since this game counted it (the friend who has it open took a piece), it can hand over its last piece of gear.
            // Nothing is lost or copied; only "Leave one of gear too" is broken then (review of card A83, minor 5).
            package.Write((rules.LeaveOne ? 1 : 0) | (rules.UseReserved ? 2 : 0) | (rules.LeaveOneOfGear ? 4 : 0));
            package.Write(asked.Count);
            foreach (KeyValuePair<string, int> each in asked)
            {
                package.Write(each.Key);
                package.Write(each.Value);
            }
            // Noted before the send: a send that throws still leaves it to be given up on after ReplyWait.
            Sent[number] = new Pending { Result = result, SentAt = ChestClaims.Clock(), Owner = owner };
            result.Status = ShareStatus.Waiting;
            Send(owner, TakeName, package);
            return result;
        }

        // ---- On the owner's game ----

        // Reply codes, as they travel. NotSharing: every module using the service is off on the owner's game; the asker
        // then stops counting that game's open chests (as its hello would have said, had one been sent).
        private const int Gave = 0, NotMine = 1, NotLoaded = 2, NoAccess = 3, Unreadable = 4, NotSharing = 5;

        /// <summary>A friend asks this game for items from a chest it owns: take them from the live chest by the service's
        /// rules and send them back, in the one frame the request arrives. Never throws into the game.</summary>
        private static void OnTake(long sender, ZPackage package)
        {
            long number = 0;
            ZDOID id = ZDOID.None;
            string replyTo = null;
            var gave = new List<KeyValuePair<string, int>>();
            var items = new List<ItemDrop.ItemData>();
            int code = Unreadable;
            try
            {
                int protocol = package.ReadInt();
                number = package.ReadLong();
                id = package.ReadZDOID();
                long playerId = package.ReadLong();
                replyTo = package.ReadString();
                int flags = package.ReadInt();
                int count = package.ReadInt();
                if (protocol < 1 || replyTo == null || !replyTo.StartsWith(ReplyPrefix, StringComparison.Ordinal) || count < 0 || count > MostKeys)
                {
                    replyTo = replyTo != null && replyTo.StartsWith(ReplyPrefix, StringComparison.Ordinal) ? replyTo : null;
                    return;   // not a request this game can answer (the finally replies if it knows where)
                }
                var wants = new List<KeyValuePair<string, int>>();
                for (int i = 0; i < count; i++)
                {
                    string key = package.ReadString();
                    int amount = package.ReadInt();
                    wants.Add(new KeyValuePair<string, int>(key, Mathf.Clamp(amount, 0, MostOfOne)));
                }
                code = Sharing ? Answer(id, playerId, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, wants, gave, items) : NotSharing;
            }
            catch (Exception error)
            {
                Fault("A friend's request for a chest couldn't be answered", error);
            }
            finally
            {
                if (items.Count > 0)
                {
                    code = Gave;   // whatever left the chest travels back as given, even if something threw mid-take
                }
                // Always answered once it can be.
                if (replyTo != null)
                {
                    try
                    {
                        Reply(sender, replyTo, number, id, code, gave, items);
                    }
                    catch (Exception error)
                    {
                        if (items.Count > 0)
                        {
                            Lost($"A reply with {items.Count} stacks for a friend's request couldn't be sent: they left the chest and are lost", error);
                        }
                        else
                        {
                            Fault("A reply to a friend's request (nothing taken) couldn't be sent", error);
                        }
                    }
                }
            }
        }

        // Takes what is asked from the chest, if this game still owns it and it may be shared with that player, into
        // items (what left the chest, measured) and gave (by key). Returns the reply code.
        private static int Answer(ZDOID id, long playerId, bool leaveOne, bool useReserved, bool gearToo, List<KeyValuePair<string, int>> wants,
                                  List<KeyValuePair<string, int>> gave, List<ItemDrop.ItemData> items)
        {
            Container chest = Find(id);
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid())
            {
                return NotLoaded;
            }
            if (!view.IsOwner())
            {
                return NotMine;
            }
            if (!Shareable(chest, playerId))
            {
                return NoAccess;
            }
            // What the chest really holds right now: a chest he doesn't have open may have changed since this computer last
            // looked (a chest he has open is never reloaded; his copy is the truth).
            ChestHooks.Reload(chest);
            // A temporary inventory: its changes run no handler and show nothing (no "cheated" line on this screen).
            var scratch = new Inventory(true);
            scratch.SetHeight(ScratchRows);
            try
            {
                foreach (KeyValuePair<string, int> want in wants)
                {
                    // Measured in the scratch inventory, not from Take's answer: another mod's handler of the chest's change
                    // can throw inside Take after the items left the chest.
                    int before = CountKey(scratch, want.Key);
                    try
                    {
                        ChestStock.Take(chest, scratch, want.Key, want.Value, leaveOne, useReserved, ChestHooks.InHisHand, gearToo);
                    }
                    finally
                    {
                        gave.Add(new KeyValuePair<string, int>(want.Key, CountKey(scratch, want.Key) - before));
                    }
                }
            }
            finally
            {
                // What arrived in the scratch inventory left the chest: it travels back, whatever happened after.
                items.AddRange(scratch.GetAllItems());
                if (items.Count > 0)
                {
                    // As after any take: sent to everyone now, and held until every friend's copy has reloaded.
                    ChestClaims.Hold(chest);
                    ZDOMan.instance.ForceSendZDO(view.GetZDO().m_uid);
                }
            }
            return Gave;
        }

        // How many of the items a key names lie in this inventory (a take puts only items the game counts into the scratch).
        private static int CountKey(Inventory inventory, string key)
        {
            ChestStock.Parse(key, out string name, out int quality);
            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_shared.m_name == name && (quality < 0 || item.m_quality == quality))
                {
                    count += item.m_stack;
                }
            }
            return count;
        }

        private static void Reply(long sender, string replyTo, long number, ZDOID id, int code, List<KeyValuePair<string, int>> gave,
                                  List<ItemDrop.ItemData> items)
        {
            var package = new ZPackage();
            package.Write(Protocol);
            package.Write(number);
            package.Write(id);
            package.Write(code);
            package.Write(gave.Count);
            foreach (KeyValuePair<string, int> each in gave)
            {
                package.Write(each.Key);
                package.Write(each.Value);
            }
            package.Write(Pack(items));
            Send(sender, replyTo, package);
        }

        // The chest with this record, loaded on this computer.
        private static Container Find(ZDOID id)
        {
            foreach (Container chest in ChestFinder.Loaded())
            {
                ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
                if (view != null && view.IsValid() && view.GetZDO().m_uid == id)
                {
                    return chest;
                }
            }
            return null;
        }

        /// <summary>What the owner's game checks before sharing a chest with a friend: one of the game's own chests (not
        /// another mod's kind, not a gravestone) that a player built, the game's own privacy check for that friend, as
        /// Container.RPC_RequestOpen checks it, and a chest its marker keeps from that friend's mods (card A80,
        /// ChestAccess: never from the marker himself). His own rules (range, ward, reserved …) he checks himself before
        /// asking.</summary>
        internal static bool MayShare(Container chest, long playerId)
        {
            if (chest.GetType() != typeof(Container) || chest.GetComponentInParent<TombStone>() != null || ChestFinder.IsObliterator(chest))
            {
                return false;
            }
            Piece piece = chest.GetComponentInParent<Piece>();
            return piece != null && piece.IsPlacedByPlayer() && ChestHooks.CheckAccess(chest, playerId) && ChestAccess.ModsMayUse(chest, playerId);
        }

        // ---- Back on the asker's game ----

        /// <summary>The owner's reply: the items go into his inventory (or wait for his character), measured, what didn't
        /// fit dropped at his feet; the take's result is filled in unless it was given up on. Each reply once. Never throws
        /// into the game.</summary>
        private static void OnGiven(long sender, ZPackage package)
        {
            try
            {
                package.ReadInt();   // protocol
                long number = package.ReadLong();
                package.ReadZDOID();
                int code = package.ReadInt();
                int count = package.ReadInt();
                var gave = new List<KeyValuePair<string, int>>();
                for (int i = 0; i < count; i++)   // never more than a request may name (OnTake refuses more)
                {
                    gave.Add(new KeyValuePair<string, int>(package.ReadString(), package.ReadInt()));
                }
                List<ItemDrop.ItemData> items = Unpack(package.ReadPackage());
                Sent.TryGetValue(number, out Pending pending);
                if (pending != null && sender != pending.Owner)
                {
                    Fault("A reply came from a game that wasn't asked", new InvalidOperationException($"{sender}, not {pending.Owner}"));
                    return;   // only the game asked answers a take
                }
                if (!Delivered.Add(number))
                {
                    return;   // delivered already
                }
                if (code == NotSharing && Peers.TryGetValue(sender, out int was) && was >= Protocol)
                {
                    Peers[sender] = 0;   // that game stopped sharing without a hello saying so (every module there switched off)
                    ZLog.Log($"[QoL Mods chest service] The game {sender} runs the pack but doesn't share its open chests now.");
                }
                int dropped = Deliver(items);
                if (pending == null || pending.GivenUp)
                {
                    if (items.Count > 0)
                    {
                        Tell($"Arrived late from a chest a friend has open: {Describe(items)}.");
                    }
                    Sent.Remove(number);
                    return;
                }
                Sent.Remove(number);
                ShareResult result = pending.Result;
                result.Dropped = dropped;
                foreach (KeyValuePair<string, int> each in gave)
                {
                    if (each.Value > 0)
                    {
                        result.Given.TryGetValue(each.Key, out int sum);
                        result.Given[each.Key] = sum + each.Value;
                    }
                }
                result.Status = code == Gave ? ShareStatus.Given
                    : code == NotMine ? ShareStatus.NotOwner
                    : code == NotLoaded ? ShareStatus.Gone
                    : ShareStatus.Refused;
            }
            catch (Exception error)
            {
                Fault("A friend's reply with items couldn't be read", error);
            }
        }

        /// <summary>Adds the items to his inventory, measured as ChestStock.Take measures, and drops at his feet whatever
        /// didn't fit; returns how many were dropped. With no character (dead, respawning) they wait for one (Tick).</summary>
        internal static int Deliver(List<ItemDrop.ItemData> items)
        {
            if (items.Count == 0)
            {
                return 0;
            }
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                Undelivered.Add(items);
                return 0;
            }
            Inventory inventory = player.GetInventory();
            int dropped = 0;
            foreach (ItemDrop.ItemData item in items)
            {
                int want = item.m_stack;
                int before = ChestStock.CountSame(inventory, item);
                try
                {
                    inventory.AddItem(item.Clone());
                }
                catch (Exception error)
                {
                    // Another mod's handler of his inventory's change: what arrived is measured below, and the next item
                    // still goes in; nothing is lost to it.
                    Fault("Another mod failed while items from a friend's open chest went into his inventory", error);
                }
                int added = Mathf.Clamp(ChestStock.CountSame(inventory, item) - before, 0, want);
                if (added < want)
                {
                    dropped += want - added;
                    try
                    {
                        DropAtFeet(item, want - added);
                    }
                    catch (Exception error)
                    {
                        Lost($"{want - added} {item.m_shared.m_name} from a friend's open chest didn't fit and couldn't be dropped at his feet: they are lost", error);
                    }
                }
            }
            if (dropped > 0)
            {
                Tell($"No room in your inventory: {dropped} of what a friend's open chest gave lie at your feet.");
            }
            return dropped;
        }

        // "10 $item_wood, 2 $item_iron", which the game's message line translates.
        private static string Describe(List<ItemDrop.ItemData> items)
        {
            var parts = new List<string>();
            foreach (ItemDrop.ItemData item in items)
            {
                parts.Add($"{item.m_stack} {item.m_shared.m_name}");
            }
            return string.Join(", ", parts);
        }

        // ---- The game's own item format ----

        // Items as a chest saves them (Inventory.Save): the game's item version (read from an empty inventory saved the
        // game's way, so it follows the game), how many, then each item as the game saves it.
        private static ZPackage PackItems(List<ItemDrop.ItemData> items)
        {
            var empty = new ZPackage();
            new Inventory(true).Save(empty);
            empty.SetPos(0);
            var package = new ZPackage();
            package.Write(empty.ReadInt());
            package.Write((ushort)items.Count);
            foreach (ItemDrop.ItemData item in items)
            {
                item.Save(package);
            }
            return package;
        }

        // Items as a chest loads them (Inventory.Load): a scratch inventory, wider and taller than the owner's scratch, so
        // every item's cell exists; the game makes each from its prefab.
        private static List<ItemDrop.ItemData> UnpackItems(ZPackage package)
        {
            var carrier = new Inventory("QoL Mods chest service", null, 8, ScratchRows);
            carrier.Load(package);
            return new List<ItemDrop.ItemData>(carrier.GetAllItems());
        }

        private static void DropNearHim(ItemDrop.ItemData item, int amount)
        {
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                Transform at = player.transform;
                ItemDrop.DropItem(item, amount, at.position + at.forward + Vector3.up, at.rotation);
            }
        }

        private static void TellHim(string text) => QoLMods.Kit.Say.TopLeft(text);   // fitted to the screen (card B14)

        // A fault that loses nothing: logged once for each kind.
        private static void Fault(string what, Exception error)
        {
            if (ToldFaults.Add(what))
            {
                ZLog.LogWarning($"[QoL Mods chest service] {what}: {error}");
            }
        }

        // A fault that loses items: logged every time.
        private static void Lost(string what, Exception error) => ZLog.LogWarning($"[QoL Mods chest service] {what}: {error}");

        /// <summary>
        /// Room in his inventory for what takes through friends will bring, worked out before asking, so no more is asked
        /// than fits: from this computer's copy of the chest, the stacks the owner's ChestStock.Take will pick (the same
        /// test and the same order), each placed as the game's Inventory.AddItem places it (into his stacks of the same
        /// item, quality and world level, then empty cells). One Room serves every take of a claim, so they fit together.
        /// Plain arithmetic over the game's items; the scratch test harness checks it against the game's own AddItem.
        /// </summary>
        internal sealed class Room
        {
            private int emptyCells;

            // Free room in his stacks, by item, quality and world level.
            private readonly Dictionary<string, int> free = new Dictionary<string, int>();

            // What earlier takes from each chest (by this computer's copy) have already been planned to take.
            private readonly Dictionary<ItemDrop.ItemData, int> planned = new Dictionary<ItemDrop.ItemData, int>();

            internal Room(Inventory inventory)
            {
                emptyCells = inventory.GetEmptySlots();
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    int max = item.m_shared.m_maxStackSize;
                    if (max > 1 && item.m_stack < max)
                    {
                        string group = Group(item);
                        free.TryGetValue(group, out int sum);
                        free[group] = sum + max - item.m_stack;
                    }
                }
            }

            /// <summary>What of <paramref name="wants"/> (in order) this chest can give and his inventory can hold, as the
            /// owner's take would pick it from this computer's copy.</summary>
            internal List<KeyValuePair<string, int>> Fit(Container chest, List<KeyValuePair<string, int>> wants, bool leaveOne, bool useReserved,
                                                         bool gearToo = false)
            {
                var fits = new List<KeyValuePair<string, int>>();
                if (!useReserved && ChestReserve.IsReserved(chest))
                {
                    return fits;
                }
                List<ItemDrop.ItemData> inChest = chest.GetInventory().GetAllItems();
                foreach (KeyValuePair<string, int> want in wants)
                {
                    ChestStock.Parse(want.Key, out string name, out int quality);
                    var stacks = new List<ItemDrop.ItemData>();
                    int total = 0;
                    bool keeps = false;
                    foreach (ItemDrop.ItemData item in inChest)
                    {
                        int left = item.m_stack - PlannedOf(item);
                        if (left > 0 && item.m_shared.m_name == name && (quality < 0 || item.m_quality == quality) && item.m_worldLevel >= Game.m_worldLevel)
                        {
                            stacks.Add(item);
                            total += left;
                            keeps |= ChestStock.KeepsOne(item, gearToo);
                        }
                    }
                    int allowed = Mathf.Min(want.Value, total - (leaveOne && keeps ? 1 : 0));
                    stacks.Sort((a, b) => ChestStock.TakeOrder(a, b, a.m_stack - PlannedOf(a), b.m_stack - PlannedOf(b)));
                    int got = 0;
                    foreach (ItemDrop.ItemData stack in stacks)
                    {
                        if (got >= allowed)
                        {
                            break;
                        }
                        int part = Mathf.Min(stack.m_stack - PlannedOf(stack), allowed - got);
                        int placed = Place(stack, part);
                        planned[stack] = PlannedOf(stack) + placed;
                        got += placed;
                        if (placed < part)
                        {
                            break;   // his inventory has no room for more of it: the owner's take stops there too
                        }
                    }
                    if (got > 0)
                    {
                        fits.Add(new KeyValuePair<string, int>(want.Key, got));
                    }
                }
                return fits;
            }

            /// <summary>How many of <paramref name="amount"/> like this item his inventory takes, as Inventory.AddItem fills
            /// it: his stacks of the same item, quality and world level first, then empty cells, a full stack each (one
            /// each for an item that doesn't stack). Uses up the room it fills.</summary>
            internal int Place(ItemDrop.ItemData like, int amount)
            {
                int max = like.m_shared.m_maxStackSize;
                if (amount <= 0)
                {
                    return 0;
                }
                if (max <= 1)
                {
                    int cells = Mathf.Min(amount, emptyCells);
                    emptyCells -= cells;
                    return cells;
                }
                string group = Group(like);
                free.TryGetValue(group, out int room);
                int placed = Mathf.Min(room, amount);
                room -= placed;
                while (placed < amount && emptyCells > 0)
                {
                    emptyCells--;
                    int put = Mathf.Min(max, amount - placed);
                    placed += put;
                    room += max - put;
                }
                free[group] = room;
                return placed;
            }

            private int PlannedOf(ItemDrop.ItemData item) => planned.TryGetValue(item, out int amount) ? amount : 0;

            private static string Group(ItemDrop.ItemData item) => $"{item.m_shared.m_name}\n{item.m_quality}\n{item.m_worldLevel}";
        }
    }
}
