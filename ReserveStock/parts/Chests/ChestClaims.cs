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

        /// <summary>The takes through the players who had chests open (ChestShare), one per chest asked that way: what each
        /// gave is in his inventory already (or, what didn't fit, at his feet).</summary>
        internal readonly List<ShareResult> Shared = new List<ShareResult>();
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
    /// But the server hands a chest only to a player whose "active area" holds it: a square about 96 m each way around
    /// the middle of the 64 m zone he stands in (64 m on a server set to the lowest simulation distance), so as little as
    /// 64 m (32 m) away on one side (ZDOMan.ReleaseNearbyZDOS, ZNetScene.InActiveArea). A chest loaded on his screen
    /// beyond that, with no player near it, has no owner and never gets one, so nobody answered and his fetches, crafts
    /// and building from it failed ("owners didn't answer in time", card A64, 2026-10-06). So, for a module that allows it
    /// (ChestUser.ClaimsFar, its setting), a chest beyond his area that no game can answer for (nobody's; owned by a game
    /// no longer in the world; or the dedicated server's, which keeps no copy of objects away from the world's centre: what
    /// a server-side mod such as ServersideQoL leaves behind) is claimed by this computer itself, as the game's own portals
    /// claim a far portal's record (TeleportWorld.SetConnectedPortal). Never a cart's or ship's chest (the claim would hand
    /// him the vehicle). The claim changes only the owner, never the contents, one owner revision ahead of the game's own
    /// claim (a hand-out the server makes in the same instant can't tie with it), and the server is asked for its newest
    /// copy just after (ZDOMan.RequestZDO). Such a chest may be changed only when MayChange says so: ConfirmWait after the
    /// claim, so whatever another machine wrote before the claim reached the server (ServersideQoL writes chests nobody
    /// owns) has reached this computer first; the game's revisions don't merge, so a write from an old copy would be thrown
    /// away on one side and copy or lose items. And only between the server's rounds: the server lets a player's chest
    /// beyond his area go at its round every two seconds, and a change still on its way then could cross a write the
    /// server's mod makes the moment it's nobody's. So the chest is claimed again as soon as the server lets it go (in the
    /// frame that's first seen: the server's let-go of this very claim, owner nobody, the owner revision one more, the data
    /// as he last owned it), and that claim is used from its confirm (the round trip and a margin, at least ConfirmWait) for
    /// UseFor, with most of the round to spare, timed on real time with the game's own measure of the round trip; a claim
    /// that didn't follow such a let-go waits for one (or, for a chest within his own area, for a whole round without one:
    /// the server is then leaving it with him). With a connection too slow to leave time between the rounds
    /// (FarPossible), such chests are left out, as before. On out of the box since card A69, one switch for every module
    /// (ChestSettings, "Chests beyond the server's reach"); one rare case stays open (below). A module that changes a chest a
    /// while after its claim, or skips the claim because every chest is his already, asks MayChange, never IsOwner; a
    /// craft's bar waits just short of its end while Confirming says a chest of it soon may be changed. While it's his, the
    /// server's requests for it are dropped (a player's hand still gets it); as soon as a module holds it after a change
    /// (Hold), it is let go at the next frame (owner nobody), so the server-side mod does its work while nobody owns it; one
    /// claimed and never changed is let go after KeepClaimedFor. A chest another player's game owns is always asked, as
    /// before. One case stays open: a change reaching the server after its next round (the let-go heard late: the server
    /// sends far records last, so a busy connection can delay it by more than the margin) while the server's mod writes
    /// that very chest the moment it's nobody's; then one change is copied or lost. The last look put it at about one
    /// incident in 30 to 1000 evenings on his friend's server, more for chests ServersideQoL feeds stations from.
    ///
    /// After taking, each chest is held on this computer for a few seconds (Hold). While it is held, another player's
    /// request to open it, to stack into it or to take all from it is quietly dropped (ChestPatches), as the game itself
    /// drops a request that reaches a computer no longer owning the chest: his press does nothing, and his next one, once
    /// the hold has ended, gets the chest. Without that, a friend who opened it straight away could see, and save back,
    /// his copy from before the take: his game reloads a chest's contents only once a second, and never while it is
    /// open. Every other computer's request is dropped during a hold, but only one that could be a player's hand on the
    /// chest (ByHand) marks it as asked for. The server's requests (a server-side mod: ServersideQoL re-sorts a chest after
    /// every change and feeds stations from chests, through the game's open request) come again every second or two and
    /// get the chest once the holds end. Marking those left a chest out of every count after each take on his friend's
    /// server, and stopped his runs of crafts; letting them through instead gave the chest to the server, which answers
    /// no request for it, until the server's two-second hand-out (card A27). A craft whose bar is running keeps the
    /// chests it planned (KeepForCraft): computers with no player within reach are kept out of those too, so a server can't
    /// take one mid-bar, while a player's hand still gets it at once. A chest someone has asked for is left out of every new claim until its hold ends and a little after
    /// (LeaveAloneFor; ChestFinder.WhyNot), so his next press gets it before taking again can hold it again. Nobody is
    /// told a chest is in use because of it, and no chest window opens
    /// late on anyone's screen (card A27); the game still says a chest is in use when someone really has it open. A hold
    /// ends by the clock, whatever becomes of the module that made it: an error in one module never cuts another
    /// module's holds short.
    ///
    /// A claim can also carry takes through the players who have chests open (ChestShare): each is asked of that
    /// player's game when the claim starts, and the claim settles once those are answered or given up on too.
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

        /// <summary>How long a chest just taken from stays held on this computer: until every friend's copy of it has
        /// reloaded (once a second, plus the time the change takes to reach them).</summary>
        internal const float HoldFor = 3f;

        /// <summary>After a hold during which another player asked for the chest, how much longer new claims leave it
        /// alone: his next press, answered at once now, gets it before a take can hold it again.</summary>
        internal const float LeaveAloneFor = 2f;

        /// <summary>How near a player must stand for a request to be his own hand on the chest (the E key, or a button in
        /// its window): the game's reach, 5 m (Player.m_maxInteractDistance), with room for how far he moves while the
        /// request travels.</summary>
        internal const float Reach = 10f;

        /// <summary>Whether a request for this chest from this computer could be a player's own hand on it, which marks the
        /// chest as asked for (see Drop). Only the scratch test harness, which can't find players, ever sets another.</summary>
        internal static Func<Container, long, bool> ByHand = PlayerWithinReach;

        /// <summary>How long a craft's keep lasts unless renewed: its user renews it every frame while it needs it.</summary>
        internal const float KeepFor = 1f;

        /// <summary>With MultiUserChest loaded: after its owner's yes, how long to wait for a chest whose record says someone
        /// has it open before calling it in use (that mod's owner says yes without handing the chest over), rather than
        /// waiting OwnershipWait and calling it no answer (card A37). The mark can outlive a player who left with the chest
        /// open, so the chest is still asked: a new owner hands it over at once.</summary>
        internal const float MultiUserChestWait = 1.5f;

        /// <summary>The least time after a claim here before the chest may be changed (see Usable): long enough for the claim
        /// to reach the server and the server's newest copy to come back. More with a slow connection (ConfirmWaitNow).</summary>
        internal const float ConfirmWait = 0.5f;

        /// <summary>A chest still nobody's this long after a claim began has been passed over by the server's two-second
        /// hand-out (its area for him may be smaller than his game reckons): claimed here like one beyond his area.</summary>
        internal const float HandOutRound = 2.5f;

        /// <summary>The server's round: every two seconds it lets go a player's chests beyond his area (ZDOMan.ReleaseZDOS).</summary>
        internal const float ServerRound = 2f;

        /// <summary>What the record queues on both sides, the server serving its players one a frame, and a frame or two add
        /// to the round trip, in seconds (review round 4: 0.1 to 0.3 s with a few players, more when busy).</summary>
        internal const float Margin = 0.5f;

        /// <summary>The round trip assumed when the game reports none (a crossplay server's connection reports 0).</summary>
        internal const float UnknownPing = 0.25f;

        /// <summary>How long after its confirm a claim made at the server's round may be used (the last look): a change made
        /// then has most of the round to spare even when the let-go was heard late (the server sends far records last).</summary>
        internal const float UseFor = 0.5f;

        /// <summary>The shortest stretch between the server's rounds worth claiming for (see FarPossible): with a slower
        /// connection than that leaves, chests beyond the server's reach are left out, as before card A64.</summary>
        internal const float LeastWindow = 0.3f;

        /// <summary>How long after a claim here the server is asked for its newest copy: the claim itself goes with the next
        /// send of records, and the question must not reach the server before it.</summary>
        internal const float AskDelay = 0.1f;

        /// <summary>How soon after the chest was seen let go a claim must be made to count as made at that let-go.</summary>
        internal const float FreshLetGo = 0.15f;

        /// <summary>How long a claim here may wait to become usable before it counts as no answer.</summary>
        internal const float ConfirmTimeout = 6f;

        /// <summary>How far from the world's centre (each way) a dedicated server can have objects loaded, around its own
        /// reference position there, at the greatest simulation distance (6 rings of 64 m zones and half a zone, 416 m),
        /// with room to spare: beyond it, a chest the server owns has no copy there to answer with.</summary>
        internal const float ServerCopyReach = 450f;

        /// <summary>How long a chest this computer claimed itself is kept (claimed again when the server lets it go, while a
        /// craft keeps it) before it is let go unused: from its claim, and from each claim or keep on it since.</summary>
        internal const float KeepClaimedFor = 10f;

        /// <summary>Whether a chest lies beyond the area the server hands chests out in for this player (the game's active
        /// area, around the middle of his zone, by the agreed simulation distance). Only the scratch test harness, which has
        /// no zones, ever sets another.</summary>
        internal static Func<Container, bool> BeyondHandOut = OutsideHandOut;

        /// <summary>Whether the server leaves a chest with this player once he owns it (inside his own active area, or well
        /// inside Far Fetched's grant: KeptWithHimHere): for Usable's "left with him", where a wrong yes could let a change
        /// cross the server's round, so it errs the other way from BeyondHandOut (card A69, review 1, M1). Only the scratch
        /// test harness ever sets another.</summary>
        internal static Func<Container, bool> KeptWithHim = KeptWithHimHere;

        /// <summary>Whether a chest's owner (a game's session ID) is a game no longer in this world: neither the server, nor
        /// this computer, nor a player in the server's list. False whenever that can't be told (the list not in yet, a
        /// player between lives). Only the scratch test harness, which has no network, ever sets another.</summary>
        internal static Func<long, bool> OwnerGone = NotInTheGame;

        /// <summary>Whether a chest's owner is the dedicated server, with the chest beyond the server's own area around the
        /// world's centre: the server keeps no copy of it there, so it answers no request for it. Only the scratch test
        /// harness ever sets another.</summary>
        internal static Func<Container, long, bool> ServerCantAnswer = ServerKeepsNoCopy;

        /// <summary>Whether a chest belongs to a cart or a ship (its record is the vehicle's). Only the scratch test harness,
        /// which has no engine, ever sets another.</summary>
        internal static Func<Container, bool> OnVehicle = ChestOnVehicle;

        /// <summary>Asks the server for its newest copy of a record (ZDOMan.RequestZDO; it sends it only if it is newer than
        /// what it last exchanged with this computer). Only the scratch test harness, which has no network, sets another.</summary>
        internal static Action<ZDOID> AskForNewest = id => ZDOMan.instance?.RequestZDO(id);

        /// <summary>The clock the timing of claims here runs on: real time, since the game's own time can fall behind it in
        /// a long frame, and the server's rounds keep real time. Only the scratch test harness sets another.</summary>
        internal static Func<float> FarClock = () => Time.realtimeSinceStartup;

        /// <summary>The round trip to the server in seconds (PingSeconds). Only the scratch test harness sets another.</summary>
        internal static Func<float> Ping = PingSeconds;

        private static int farFrame = -1;
        private static float farNow;

        // FarClock, read once a frame: a check and the change after it in the same frame see the same moment.
        private static float FarNow()
        {
            int frame = Frame();
            if (frame != farFrame)
            {
                farFrame = frame;
                farNow = FarClock();
            }
            return farNow;
        }

        private sealed class FarClaim
        {
            internal float ClaimedAt;    // the latest claim here (Usable)
            internal ushort ClaimRev;    // the record's owner revision after that claim: the server's let-go makes it one more
            internal uint DataWhileOwned; // the record's data revision as last seen while this computer owned it
            internal ChestUser User;     // the module whose claim made it (its setting allows claims here)
            internal bool AfterLetGo;    // that claim was made as the server let the chest go: its next round is two seconds off
            internal float LetGoSeenAt;  // when that let-go was seen here
            internal float UnownedSince; // the first frame it was seen nobody's (or another's) since this computer last owned it
            internal float Until;        // let go unused after this, unless kept or claimed again
            internal float CraftUntil;   // a craft's bar keeps it claimed until then (KeepForCraft)
            internal bool LetGo;         // changed and held (Hold): let go at the next frame
            internal float AskAt;        // when to ask the server for its newest copy (0: asked)
        }

        // Chests this computer claimed itself (see the class).
        private static readonly Dictionary<Container, FarClaim> Far = new Dictionary<Container, FarClaim>();

        private static bool toldOfClaiming;

        private static bool toldOfOthers;

        // Chests a module plans to take from soon (a craft whose bar is running), and until when (see KeepForCraft).
        private static readonly Dictionary<Container, float> Kept = new Dictionary<Container, float>();

        /// <summary>The clock the holds and claims run on: the game's time. Only the scratch test harness, which can't run
        /// Unity's clock, ever sets another.</summary>
        internal static Func<float> Clock = () => Time.time;

        /// <summary>The frame number, for doing the work once a frame. Only the scratch test harness sets another.</summary>
        internal static Func<int> Frame = () => Time.frameCount;

        /// <summary>His player ID, which the game's open request and a chest's privacy go by. Only the scratch test harness
        /// sets another.</summary>
        internal static Func<long> PlayerId = () => Game.instance.GetPlayerProfile().GetPlayerID();

        private static bool firstTurn = true;

        /// <summary>An answer that arrives this long after its request is still caught, never shown as a chest window.</summary>
        private const float CatchFor = AnswerWait + OwnershipWait + 5f;

        // Chests the service has asked and not yet heard from: when last asked, and how many answers are still due, so a
        // late answer to an earlier request can't use up a newer one's place.
        private static readonly Dictionary<ZDOID, Asked> AskedChests = new Dictionary<ZDOID, Asked>();
        private static readonly Dictionary<Container, float> Holds = new Dictionary<Container, float>();
        // Held chests a player within reach has asked for meanwhile: left out of new claims until LeaveAloneFor after the
        // hold.
        private static readonly HashSet<Container> Wanted = new HashSet<Container>();
        private static Claim current;
        private static int lastTick = -1;

        /// <summary>True while a claim is waiting for answers.</summary>
        internal static bool Busy => current != null;

        /// <summary>
        /// Starts claiming <paramref name="chests"/> for <paramref name="user"/>; <paramref name="done"/> gets the result,
        /// inside the user's Guard, in the frame the last answer is in (at once if none is needed). Returns false, doing
        /// nothing, while another claim is under way.
        /// </summary>
        internal static bool Start(ChestUser user, IEnumerable<Container> chests, Action<ClaimResult> done) => Start(user, chests, null, null, done);

        /// <summary>
        /// The same, and asks the players who have <paramref name="shares"/>' chests open for what each names
        /// (ChestShare.Ask, under <paramref name="rules"/>: leave-one and reserved chests), no more of each than fits in his
        /// inventory, all of them together; the claim settles once those are answered or given up on too
        /// (ClaimResult.Shared). <paramref name="task"/> names the work in an error message ("putting into chests" for a
        /// put), and in the log line naming the chests it couldn't have (ChestExplain.LogClaim, since card A69), unless
        /// <paramref name="quiet"/> (a claim made ahead of need, again and again: building's look ahead). Returns false,
        /// doing nothing, while another claim is under way.
        /// </summary>
        internal static bool Start(ChestUser user, IEnumerable<Container> chests, IEnumerable<ShareTake> shares, ChestRules rules,
                                   Action<ClaimResult> done, string task = "taking from chests", bool quiet = false)
        {
            if (current != null)
            {
                return false;
            }
            long playerId = PlayerId();
            var claim = new Claim(user, done, playerId, task, quiet);
            foreach (Container chest in chests)
            {
                claim.Add(chest);
            }
            if (shares != null)
            {
                Player player = Player.m_localPlayer;
                var room = player != null ? new ChestShare.Room(player.GetInventory()) : null;
                foreach (ShareTake share in shares)
                {
                    claim.Share(ChestShare.Ask(share, rules ?? new ChestRules(), room, playerId));
                }
            }
            current = claim;
            Tick(force: true);
            return true;
        }

        /// <summary>Every frame, from each using module's Update. Does its work once a frame, however many call it.</summary>
        internal static void Tick() => Tick(force: false);

        private static void Tick(bool force)
        {
            if (!force && Frame() == lastTick)
            {
                return;
            }
            lastTick = Frame();
            float now = Clock();
            if (firstTurn)
            {
                firstTurn = false;
                ChestHooks.AtFirstTurn();   // every plugin has started by now
            }
            ChestShare.Tick();   // first: a take through a friend given up on counts in this frame's settling
            ServerReach.Tick();  // the hello to Far Fetched on the server, when due
            PerformanceMods.Tick();  // Skarif's sending order, as the setting says

            if (Far.Count > 0)
            {
                KeepFar();   // before settling: a craft's chest the server let go is claimed again for it
            }

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

            // A hold ends by the clock (IsHeld); here the ones that are over, and the marks of those asked for, are only
            // forgotten.
            if (Holds.Count > 0 || Kept.Count > 0)
            {
                ForgetWhatIsOver();
            }
        }

        // Forgets the holds that are over (and the marks of those asked for): ended, and past the time a chest asked for is
        // left alone after its hold.
        private static void ForgetWhatIsOver()
        {
            // Runs every frame while a craft keeps chests: nothing is allocated unless something is over.
            List<Container> over = null;
            foreach (KeyValuePair<Container, float> each in Holds)
            {
                if (each.Key == null || Clock() - each.Value > HoldFor + (Wanted.Contains(each.Key) ? LeaveAloneFor : 0f))
                {
                    (over = over ?? new List<Container>()).Add(each.Key);
                }
            }
            if (over != null)
            {
                foreach (Container chest in over)
                {
                    Holds.Remove(chest);
                    Wanted.Remove(chest);
                }
            }
            List<Container> lapsed = null;
            foreach (KeyValuePair<Container, float> each in Kept)
            {
                if (each.Key == null || Clock() > each.Value)
                {
                    (lapsed = lapsed ?? new List<Container>()).Add(each.Key);
                }
            }
            if (lapsed != null)
            {
                foreach (Container chest in lapsed)
                {
                    Kept.Remove(chest);
                }
            }
        }

        // Each chest this computer claimed itself (see the class), every frame: asks the server for its newest copy once the
        // claim has gone out (AskDelay); lets go, the frame after a change, one a module held (LetGo); notes the first frame it
        // is seen nobody's (the server's round, or a write that came with that); forgets one another player's game owns now,
        // and one gone; lets go one kept past KeepClaimedFor unused; and, while a craft's bar keeps it, claims again one the
        // server let go.
        private static void KeepFar()
        {
            float now = FarNow();
            List<Container> over = null;
            foreach (KeyValuePair<Container, FarClaim> each in Far)
            {
                Container chest = each.Key;
                FarClaim far = each.Value;
                ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
                bool keep;
                if (view == null || !view.IsValid())
                {
                    keep = false;
                }
                else
                {
                    if (far.AskAt > 0f && now >= far.AskAt)
                    {
                        far.AskAt = 0f;
                        AskForNewest(view.GetZDO().m_uid);
                    }
                    if (chest.IsInUse() && view.IsOwner())
                    {
                        keep = false;   // he has it open here: his, as the game's own open makes it; never let go under him
                    }
                    else if (far.LetGo)
                    {
                        // Changed and sent a frame ago (every payment of that frame made): let go, so the server's mod does its
                        // work while nobody owns it, and the next use claims it again with the server's newest copy.
                        if (view.IsOwner())
                        {
                            view.GetZDO().SetOwner(0L);
                        }
                        keep = false;
                    }
                    else if (view.IsOwner())
                    {
                        far.UnownedSince = 0f;
                        far.DataWhileOwned = view.GetZDO().DataRevision;
                        keep = now <= far.Until || now <= far.CraftUntil;
                        if (!keep)
                        {
                            view.GetZDO().SetOwner(0L);   // unused: let go, so nobody's game is waited on for it
                        }
                    }
                    else
                    {
                        if (far.UnownedSince <= 0f)
                        {
                            far.UnownedSince = now;   // first seen nobody's (or another's) in this frame
                        }
                        if (!NoGameAnswers(chest, view))
                        {
                            keep = false;   // another player's game has it: theirs, as usual
                        }
                        else
                        {
                            keep = now <= far.Until || now <= far.CraftUntil;
                            if (keep && now <= far.CraftUntil && FarPossible() && (far.User == null || far.User.ClaimsFar) &&
                                !ServerReach.Covers(chest, loose: true))
                            {
                                ClaimFar(chest, view, far.User);   // let go by the server mid-bar: claimed again for the craft
                            }
                        }
                    }
                }
                if (!keep)
                {
                    (over = over ?? new List<Container>()).Add(chest);
                }
            }
            if (over != null)
            {
                foreach (Container chest in over)
                {
                    Far.Remove(chest);
                }
            }
        }

        // True if no game can answer a request for this chest: nobody owns it, its owner left the game, or a dedicated
        // server owns it far from the world's centre, where it keeps no copy (and doesn't have it open: a server-side mod
        // marks a chest in use while it works on it). This computer's own ownership counts as no.
        private static bool NoGameAnswers(Container chest, ZNetView view)
        {
            if (view.IsOwner())
            {
                return false;
            }
            ZDO record = view.GetZDO();
            long owner = record.GetOwner();
            if (owner == 0L || OwnerGone(owner))
            {
                return true;
            }
            return ServerCantAnswer(chest, owner) && record.GetInt(ZDOVars.s_inUse) != 1;
        }

        // Claims a chest no game can answer for: the owner only, never the contents, with one owner revision more than the
        // game's own claim gives (a hand-out the server makes in the same instant can't tie with it), sent at once; the
        // server is asked for its newest copy just after (AskDelay), once the claim has gone. A claim made in the frame the
        // chest was first seen let go (FreshLetGo), with nothing written into it since this computer's last claim, is made
        // at the server's round: its next round is ServerRound off, and that is what Usable times changes by. The server's
        // round lets go exactly the claim this computer made, so the record then has no owner and an owner revision one
        // more than that claim left (ZDO.SetOwner on the server); anything else (another game's claim and let-go, a copy
        // that crossed the claim) is no round. Any other claim waits for a let-go of its own, or for the server to leave
        // the chest with him.
        private static void ClaimFar(Container chest, ZNetView view, ChestUser user)
        {
            float now = FarNow();
            bool known = Far.TryGetValue(chest, out FarClaim far);
            if (!known)
            {
                Far[chest] = far = new FarClaim();
            }
            ZDO record = view.GetZDO();
            bool afterLetGo = known && far.ClaimedAt > 0f && !record.HasOwner() && far.UnownedSince > 0f &&
                              now - far.UnownedSince <= FreshLetGo && record.OwnerRevision == (ushort)(far.ClaimRev + 1) &&
                              record.DataRevision == far.DataWhileOwned;
            float letGo = far.UnownedSince;
            view.ClaimOwnership();
            record.OwnerRevision = (ushort)(record.OwnerRevision + 1);
            ZDOMan.instance?.ForceSendZDO(record.m_uid);
            far.ClaimedAt = now;
            far.ClaimRev = record.OwnerRevision;
            far.DataWhileOwned = record.DataRevision;
            far.User = user ?? far.User;
            far.UnownedSince = 0f;
            far.AfterLetGo = afterLetGo;
            if (afterLetGo)
            {
                far.LetGoSeenAt = letGo;
            }
            far.Until = Math.Max(far.Until, now + KeepClaimedFor);
            far.LetGo = false;
            far.AskAt = now + AskDelay;
            if (!toldOfClaiming)
            {
                toldOfClaiming = true;
                ZLog.Log("[QoL Mods chest service] A chest beyond the area the server hands chests out in for this player " +
                         "(about 64 to 128 m around him) had no game to answer for it: this computer claimed it itself, as the " +
                         "game's own portals do, and changes it only once the server's newest copy is in and between the " +
                         "server's rounds. Said once.");
            }
        }

        // The round trip to the server in seconds (the game's own measure, read at most twice a second; UnknownPing when the
        // connection reports none); 0 on the server.
        private static float pingRead = -1f;
        private static float pingSeconds;

        internal static float PingSeconds()
        {
            float now = FarNow();
            if (pingRead >= 0f && now - pingRead < 0.5f)
            {
                return pingSeconds;
            }
            pingRead = now;
            ZNet net = ZNet.instance;
            if (net == null || net.IsServer())
            {
                pingSeconds = 0f;
                return 0f;
            }
            net.GetNetStats(out _, out _, out int ping, out _, out _);
            // Only Steam's connections report it; a crossplay server's reports 0, which can't be true: assumed then.
            pingSeconds = ping > 0 ? ping / 1000f : UnknownPing;
            return pingSeconds;
        }

        // How long after a claim here before the chest may be changed: ConfirmWait, or the round trip and the margin if
        // that's longer.
        private static float ConfirmWaitNow() => Math.Max(ConfirmWait, Ping() + Margin);

        // How long after the let-go a claim followed it may be used: a change made then still reaches the server before
        // its next round.
        private static float SafeEndNow() => ServerRound - (Ping() + Margin);

        // A claim the server hasn't let go this long after the claim (or after the let-go it followed), on a chest within his
        // own hand-out now, is one it is leaving with him: his changes can't cross its round. Beyond his hand-out the round
        // always comes (only later, when his position or the notice is late), so such a chest waits for it.
        private static float LeftWithHimAfter() => ServerRound + Ping() + Margin + 0.2f;

        /// <summary>Whether the connection leaves time enough between the server's rounds for a claim here (LeastWindow):
        /// with a slower one, chests beyond the server's reach are left out, as before card A64.</summary>
        internal static bool FarPossible() => SafeEndNow() - ConfirmWaitNow() >= LeastWindow;

        // Whether a chest claimed here may be changed now (see the class).
        private static bool Usable(Container chest, FarClaim far, float now)
        {
            if (now - far.ClaimedAt < ConfirmWaitNow())
            {
                return false;
            }
            if (far.AfterLetGo && now - far.LetGoSeenAt <= Math.Min(SafeEndNow(), ConfirmWaitNow() + UseFor))
            {
                return true;
            }
            float since = far.AfterLetGo ? now - far.LetGoSeenAt : now - far.ClaimedAt;
            return since >= LeftWithHimAfter() && KeptWithHim(chest);
        }

        /// <summary>
        /// True if this computer owns the chest and may change it now. For a chest this computer claimed itself (see the
        /// class): only once the server's newest copy has had time to come in, and only between the server's rounds. What a
        /// module asks before it counts or changes a chest a while after its claim settled (a craft as its bar ends, a piece
        /// placed at a click), or when it skips the claim because every chest is his already. Changes nothing.
        /// </summary>
        internal static bool MayChange(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return false;
            }
            if (Far.TryGetValue(chest, out FarClaim far))
            {
                return Usable(chest, far, FarNow());
            }
            // A chest he owns beyond any active area the game gives is his only through Far Fetched on the server, which
            // lets it go two of its rounds after it leaves his grant: changed only while every recent position of his
            // puts it inside the grant, so a change never crosses that let-go (card A69, Far Fetched's review, M1).
            return ZNet.instance == null || ZNet.instance.IsServer() || !BeyondAreaSquare(ChestFinder.Where(chest)) ||
                   ServerReach.SteadilyCovers(chest);
        }

        /// <summary>True for a chest this computer claimed itself (see the class) and still keeps as such.</summary>
        internal static bool ClaimedHere(Container chest) => chest != null && Far.ContainsKey(chest);

        /// <summary>True if a claim of this chest now would claim it here, beyond the server's hand-out (see the class): for a
        /// module that leaves such chests out of a claim made ahead of need (Craft from Chests' building looks ahead every few
        /// seconds; claims here only at a click). Changes nothing.</summary>
        internal static bool NeedsFarClaim(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            return view != null && view.IsValid() && ZNet.instance != null && !ZNet.instance.IsServer() && !view.IsOwner() &&
                   !OnVehicle(chest) && NoGameAnswers(chest, view) && BeyondHandOut(chest);
        }

        /// <summary>True while one of these chests is one this computer claimed itself that may not be changed yet but soon
        /// will (its confirm, or waiting for the server's next round), or that the server has let go and that a craft's keep
        /// is about to claim again: a craft's bar waits just short of its end meanwhile (Craft from Chests).</summary>
        internal static bool Confirming(List<Container> chests)
        {
            float now = FarNow();
            foreach (Container chest in chests)
            {
                if (chest == null || !Far.TryGetValue(chest, out FarClaim far) || far.LetGo)
                {
                    continue;
                }
                ZNetView view = ChestHooks.NetView(chest);
                if (view == null || !view.IsValid())
                {
                    continue;
                }
                if (view.IsOwner()
                    ? !Usable(chest, far, now)
                    : now <= Math.Max(far.Until, far.CraftUntil) && NoGameAnswers(chest, view) && FarPossible() &&
                      (far.User == null || far.User.ClaimsFar))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How far past the edge of the active area's square a chest must be to count as surely beyond it, in
        /// metres: the server measures from where this game last told it he stands, which lags behind him.</summary>
        internal const float AreaMargin = 8f;

        /// <summary>
        /// Whether the chest lies surely beyond the area the server hands chests out in for this player: outside the
        /// largest active area the game ever gives (1.5 zones each way from the middle of the zone he stands in, whatever
        /// the simulation distance; ZNetScene.PointInsideActiveArea), with a margin, and not a spot Far Fetched on the
        /// server hands him (ServerReach, card A70). Where it says no, the server hands the chest out (or keeps it with him),
        /// and claims wait for that rather than claim it here: the server measures by its own simulation distance, which
        /// can be larger than his game's, and a chest claimed here that the server then keeps with him would never be
        /// usable (card A69, review 1, M1). False whenever the world isn't there to ask.
        /// </summary>
        internal static bool OutsideHandOut(Container chest)
        {
            if (chest == null || ZNet.instance == null || ZNetScene.instance == null || ZoneSystem.instance == null)
            {
                return false;
            }
            return BeyondAreaSquare(ChestFinder.Where(chest)) && !ServerReach.Covers(chest, loose: true);
        }

        /// <summary>Whether a spot lies outside the largest active area the game ever gives this player (1.5 zones each way
        /// from the middle of the zone he stands in), by AreaMargin. False whenever the world isn't there to ask.</summary>
        internal static bool BeyondAreaSquare(Vector3 at)
        {
            if (ZNet.instance == null || ZoneSystem.instance == null)
            {
                return false;
            }
            Vector3 middle = ZoneSystem.GetZonePos(ZoneSystem.GetZone(ZNet.instance.GetReferencePosition()));
            float edge = 1.5f * ZoneSystem.instance.m_zoneSize + AreaMargin;
            return Mathf.Abs(at.x - middle.x) > edge || Mathf.Abs(at.z - middle.z) > edge;
        }

        /// <summary>Whether the server leaves this chest with this player once he owns it: inside his active area by his
        /// own game's test (the game's, by the agreed simulation distance), or inside Far Fetched's grant from every one of
        /// his positions of the last few seconds (ServerReach.SteadilyCovers; the last look). Errs toward no (see
        /// KeptWithHim).</summary>
        internal static bool KeptWithHimHere(Container chest)
        {
            if (chest == null || ZNet.instance == null || ZNetScene.instance == null || ZoneSystem.instance == null)
            {
                return false;
            }
            return !ZNetScene.instance.OutsideActiveArea(ChestFinder.Where(chest)) || ServerReach.SteadilyCovers(chest);
        }

        /// <summary>
        /// True if this game can't get the chest at all now (since card A69): on a client, a chest it doesn't own that no
        /// game can answer for, beyond the server's hand-out, which a claim here can't take either: "Chests beyond the
        /// server's reach" is off for <paramref name="user"/>, it's a cart's or a ship's, or the connection is too slow
        /// (FarPossible). Such a chest is left out of claims (asking it would only wait out its time) and named when he's
        /// told why something couldn't be had. Changes nothing.
        /// </summary>
        internal static bool CantClaim(Container chest, ChestUser user)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid() || ZNet.instance == null || ZNet.instance.IsServer() || view.IsOwner() ||
                !NoGameAnswers(chest, view) || !BeyondHandOut(chest))
            {
                return false;
            }
            return (user != null && !user.ClaimsFar) || OnVehicle(chest) || !FarPossible();
        }

        /// <summary>
        /// Where a chest stands for this game now, in words that follow "a chest 112 m away: ", for telling him and the log
        /// why it couldn't be used at once (since card A69). Changes nothing.
        /// </summary>
        internal static string Situation(Container chest, ChestUser user)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid())
            {
                return "it's gone or no longer loaded";
            }
            if (view.IsOwner())
            {
                if (MayChange(chest))
                {
                    return "your game has it";
                }
                if (!ClaimedHere(chest))
                {
                    return "the server just handed it to you, and your game uses it after a few seconds (to be sure the server keeps it with you)";
                }
                // Claimed here inside the hand-out only when the server had passed it over (card A69: a chest 55 m away
                // was called "beyond the server's reach", which it wasn't).
                if (BeyondHandOut(chest))
                {
                    return "your game claimed it beyond the server's reach, and may use it in a moment (the server allows it half a " +
                           "second in every two)";
                }
                return KeptWithHim(chest)
                    ? "the server didn't hand it to you, so your game claimed it itself, and may use it in a moment"
                    : "the server didn't hand it to you, so your game claimed it itself, and may use it only in the half second " +
                      "the server allows in every two";
            }
            ZDO record = view.GetZDO();
            if (chest.IsInUse() || record.GetInt(ZDOVars.s_inUse) == 1)
            {
                return "someone has it open";
            }
            if (IsWaitedOn(chest))
            {
                return "another player is about to open it";
            }
            bool far = ZNet.instance != null && !ZNet.instance.IsServer() && BeyondHandOut(chest);
            if (!NoGameAnswers(chest, view))
            {
                return $"{OwnerName(record.GetOwner())} has it, and is asked for it";
            }
            if (!far)
            {
                return record.HasOwner()
                    ? $"{OwnerName(record.GetOwner())} has it but can't answer, and the server hasn't handed it to you yet (it does so every two seconds)"
                    : "nobody has it, and the server hasn't handed it to you yet (it does so every two seconds)";
            }
            string beyond = (ServerReach.Granted > 0f
                ? $"it's beyond the server's reach (Far Fetched on the server hands you chests within {ServerReach.Granted:0} m, in the zones your game has loaded)"
                : "it's beyond the server's reach (the server hands chests only within about 64 to 128 m of you)") + " and nobody nearer has it";
            if (OnVehicle(chest))
            {
                return beyond + ", and it's a cart's or a ship's, which your game never claims";
            }
            if (user != null && !user.ClaimsFar)
            {
                return beyond + ", and the setting \"Chests beyond the server's reach\" is off (F1, under \"" + ChestSettings.Section + "\")";
            }
            if (!FarPossible())
            {
                return beyond + $", and your connection ({Ping() * 1000f:0} ms) is too slow to claim it";
            }
            return beyond + ", so your game claims it itself and may use it only in the half second the server allows in every two";
        }

        // Whose game a chest's owner is, for him to read: a player's name, the server, or a game no longer in the world.
        private static string OwnerName(long owner)
        {
            ZNet net = ZNet.instance;
            if (owner == 0L || net == null)
            {
                return "nobody";
            }
            ZNetPeer server = net.IsServer() ? null : net.GetServerPeer();
            if (server != null && owner == server.m_uid)
            {
                return "the server (a server mod such as ServersideQoL)";
            }
            foreach (ZNet.PlayerInfo player in net.GetPlayerList())
            {
                if (player.m_characterID.UserID == owner)
                {
                    return $"{player.m_name}'s game";
                }
            }
            return "a game no longer in the world";
        }

        /// <summary>True if a chest's owner is a game no longer in this world (see OwnerGone). Only a client asks: the
        /// server knows its own peers, and hands such a chest out itself.</summary>
        internal static bool NotInTheGame(long owner)
        {
            ZNet net = ZNet.instance;
            if (owner == 0L || net == null || net.IsServer() || owner == ZDOMan.GetSessionID())
            {
                return false;
            }
            ZNetPeer server = net.GetServerPeer();
            if (server == null || owner == server.m_uid)
            {
                return false;
            }
            bool listed = false;
            foreach (ZNet.PlayerInfo player in net.GetPlayerList())
            {
                long uid = player.m_characterID.UserID;
                if (uid == 0L)
                {
                    return false;   // a player between lives: his game may be the owner
                }
                if (uid == owner)
                {
                    return false;
                }
                listed |= uid == ZDOMan.GetSessionID();
            }
            return listed;   // the list is in (he is on it), and the owner isn't
        }

        /// <summary>True if a chest's owner is a dedicated server (one with no player of its own: a friend hosting from his
        /// game is on the server's list of players, and has the chests near him loaded) and the chest lies more than
        /// ServerCopyReach from the world's centre, where such a server, having no player to load objects around, keeps no
        /// copy of objects and so answers no request for them (see ServerCantAnswer). False on the server itself, and
        /// whenever that can't be told.</summary>
        internal static bool ServerKeepsNoCopy(Container chest, long owner)
        {
            ZNet net = ZNet.instance;
            if (chest == null || owner == 0L || net == null || net.IsServer())
            {
                return false;
            }
            ZNetPeer server = net.GetServerPeer();
            if (server == null || owner != server.m_uid)
            {
                return false;
            }
            foreach (ZNet.PlayerInfo player in net.GetPlayerList())
            {
                long uid = player.m_characterID.UserID;
                if (uid == server.m_uid || uid == 0L)
                {
                    return false;   // a game hosting the world, with its own player (or one between lives, who may be it)
                }
            }
            Vector3 at = ChestFinder.Where(chest);
            return Mathf.Max(Mathf.Abs(at.x), Mathf.Abs(at.z)) > ServerCopyReach;
        }

        /// <summary>True for a cart's or a ship's chest: its record is the vehicle's (Container.m_rootObjectOverride), and
        /// owning it would make this computer move the vehicle.</summary>
        internal static bool ChestOnVehicle(Container chest)
        {
            if (chest == null)
            {
                return false;
            }
            ZNetView view = ChestHooks.NetView(chest);
            return (view != null && view.gameObject != chest.gameObject) || chest.GetComponentInParent<Ship>() != null ||
                   chest.GetComponentInParent<Vagon>() != null;
        }

        /// <summary>Holds a chest this computer owns and has just changed, for HoldFor seconds: other players' requests for it
        /// are dropped until then (see the class). Held already: the hold starts again. No lid, no sound, and nothing anyone
        /// else can see.</summary>
        internal static void Hold(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return;
            }
            if (Far.TryGetValue(chest, out FarClaim far))
            {
                far.LetGo = true;   // claimed here: let go at the next frame instead (see the class), not held
                return;
            }
            if (!IsWaitedOn(chest))
            {
                Wanted.Remove(chest);   // a new hold: whoever asked during an earlier one has had his chance
            }
            Holds[chest] = Clock();   // still asked for (an older plan took again): he waits for this hold's end too
        }

        /// <summary>True while a hold on this chest lasts: HoldFor seconds after its last take, by the clock.</summary>
        internal static bool IsHeld(Container chest) => chest != null && Holds.TryGetValue(chest, out float since) && Clock() - since <= HoldFor;

        /// <summary>The undo step of a module that uses the service, kept for them all. Nothing is ended early: a hold ends
        /// by the clock by itself, and ending one sooner could let a friend open that chest before his copy has reloaded
        /// (or cut short a hold another module made, or the time a chest asked for is left alone). Only what is over is
        /// forgotten.</summary>
        internal static void ReleaseHolds() => ForgetWhatIsOver();

        /// <summary>True from another player's dropped request for this chest until LeaveAloneFor after its hold ends:
        /// every new claim leaves it alone (ChestFinder.WhyNot), so that player's next press gets it.</summary>
        internal static bool IsWaitedOn(Container chest) =>
            chest != null && Wanted.Contains(chest) && Holds.TryGetValue(chest, out float since) && Clock() - since <= HoldFor + LeaveAloneFor;

        /// <summary>
        /// From the patches on the game's three requests for a chest (Container.RPC_RequestOpen, RPC_RequestStack,
        /// RPC_RequestTakeAll), which run on its owner: true if the chest is held and the request is another computer's,
        /// which is then dropped (the game must skip it); false to let the game answer it at once, as always. Never for a
        /// chest open on this computer, unless MultiUserChest is loaded: the game answers "in use" and hands nothing over. Only a
        /// request that could be a player's hand on the chest (ByHand) also marks it as asked for; the server's (a
        /// server-side mod) and a game's out of reach mark nothing. Never throws.
        /// </summary>
        internal static bool Drop(Container chest, long uid)
        {
            try
            {
                if (chest == null || uid == ZNet.GetUID())
                {
                    return false;
                }
                if (chest.IsInUse() && !ChestShare.MultiUserChest)
                {
                    // He has it open here (a friend took from it through his game, card A37): the game's own answer, "in use",
                    // is the truth and hands nothing over. Held or not, nothing is dropped and nothing is marked.
                    return false;
                }
                if (Far.TryGetValue(chest, out FarClaim far) && !ByHand(chest, uid) && BeyondHandOut(chest))
                {
                    // Claimed here beyond his hand-out (see the class): handed over, the server's mod would get a chest the
                    // server keeps no copy of, and nobody could open it after. Dropped; and if nothing here is using it right
                    // now (no claim under way for it, no craft's bar), it is let go at once, so the server's mod writes it
                    // while nobody owns it.
                    ZNetView view = ChestHooks.NetView(chest);
                    if (!far.LetGo && (current == null || !current.Has(chest)) && FarNow() > far.CraftUntil &&
                        view != null && view.IsValid() && view.IsOwner())
                    {
                        view.GetZDO().SetOwner(0L);
                        Far.Remove(chest);
                    }
                    return true;
                }
                bool held = IsHeld(chest);
                if (!held && !IsKeptForCraft(chest))
                {
                    return false;
                }
                bool hand = ByHand(chest, uid);
                if (!held && hand)
                {
                    return false;   // only kept for a craft: a player's hand gets it at once, as before any take
                }
                // Dropped, whoever asks; but only a hand marks the chest as asked for (left out of the counts and of new
                // claims until LeaveAloneFor after the hold, so his next press wins). The server's requests come again
                // every second or two and get the chest once the holds end, so a run of crafts keeps it meanwhile: let
                // through, it went to the server, which answers nothing for it, until the server handed it back.
                if (hand)
                {
                    Wanted.Add(chest);
                }
                else if (!toldOfOthers)
                {
                    toldOfOthers = true;
                    ZLog.Log("[QoL Mods chest service] A request for a chest just taken from, or planned for a craft, came from a " +
                             "computer with no player within reach of it (the server's mod, or another mod's claim): dropped, as " +
                             "every other computer's is then, and it asks again; the chest stays in every count.");
                }
                return true;   // dropped: the game must skip it
            }
            catch (Exception error)
            {
                ZLog.LogWarning($"[QoL Mods chest service] A request for a held chest couldn't be looked at: {error}");
                return false;
            }
        }

        /// <summary>
        /// Keeps a chest this computer owns, planned for a take soon (a craft whose bar is running), from computers with no
        /// player within reach of it for KeepFor seconds: their requests are dropped, as during a hold, and nothing is
        /// marked. A player's hand still gets it at once, as before any take (nothing is held before a take). Without it, a
        /// server that re-sorts chests (ServersideQoL) took a planned chest in the middle of a long bar, once an earlier
        /// take's hold had ended, and handed it back only at its next two-second hand-out: the craft came up short. Renew
        /// it every frame while it's needed; it lapses by itself. Not Reserve Stock's mark (ChestReserve).
        /// </summary>
        internal static void KeepForCraft(Container chest)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (chest != null && Far.TryGetValue(chest, out FarClaim far))
            {
                far.CraftUntil = FarNow() + KeepFor;   // claimed here: claimed again for the whole bar, even if let go meanwhile
            }
            if (view != null && view.IsValid() && view.IsOwner())
            {
                Kept[chest] = Clock() + KeepFor;
            }
        }

        /// <summary>True while a craft's keep on this chest lasts (see KeepForCraft).</summary>
        internal static bool IsKeptForCraft(Container chest) => chest != null && Kept.TryGetValue(chest, out float until) && Clock() <= until;

        // True if a player of that computer is here, within reach of the chest: each player's character is owned by his
        // own game. The server has no character, and a player out of reach can't touch the chest.
        internal static bool PlayerWithinReach(Container chest, long uid)
        {
            Vector3 at = chest.transform.position;
            foreach (Player player in Player.GetAllPlayers())
            {
                ZNetView view = player != null ? player.GetComponent<ZNetView>() : null;
                if (view != null && view.IsValid() && view.GetZDO().GetOwner() == uid)
                {
                    return (player.transform.position - at).sqrMagnitude <= Reach * Reach;
                }
            }
            return false;
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
                if (Clock() - asked.Time > CatchFor)
                {
                    return false;
                }
                ours = true;
                current?.Answer(chest, granted, Clock());
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
            Confirming,   // claimed here (see the class): held once its ConfirmWait has passed
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
            private readonly string task;
            private readonly bool quiet;
            private readonly List<Entry> entries = new List<Entry>();
            private readonly List<ShareResult> shares = new List<ShareResult>();

            internal Claim(ChestUser user, Action<ClaimResult> done, long playerId, string task, bool quiet)
            {
                this.user = user;
                this.done = done;
                this.playerId = playerId;
                this.task = task;
                this.quiet = quiet;
            }

            internal void Share(ShareResult share) => shares.Add(share);

            internal void Add(Container chest)
            {
                var entry = new Entry { Chest = chest, Since = Clock() };
                entries.Add(entry);
                ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
                if (view == null || !view.IsValid())
                {
                    entry.State = State.Gone;
                }
                else if (view.IsOwner())
                {
                    if (Far.TryGetValue(chest, out FarClaim far))
                    {
                        if (!MayChange(chest) && ServerReach.Covers(chest, loose: true))
                        {
                            // Claimed here before Far Fetched's grant reached it: Far Fetched would keep it with him without
                            // a let-go, so it would never become usable. Let go now; Far Fetched hands it back within two
                            // seconds, clean (card A69, review 1, M1).
                            view.GetZDO().SetOwner(0L);
                            Far.Remove(chest);
                            entry.State = State.NoOwner;
                            return;
                        }
                        far.Until = Math.Max(far.Until, FarNow() + KeepClaimedFor);   // claimed here, and wanted again
                        far.LetGo = false;
                    }
                    entry.State = MayChange(chest) ? State.Held : State.Confirming;   // claimed here: only once it may be changed
                }
                else if (!view.GetZDO().HasOwner() && ZNet.instance != null && ZNet.instance.IsServer())
                {
                    view.ClaimOwnership();   // only the server hands ownership out, so the server may take it
                    entry.State = State.Held;
                }
                else if (MayClaimFar(chest, view, passedOver: false))
                {
                    ClaimHere(entry, view);   // the server will never hand it to him, and no game can answer for it
                }
                else if (!view.GetZDO().HasOwner())
                {
                    entry.State = State.NoOwner;   // wait for the server to give it an owner, then ask that owner
                }
                else
                {
                    // Asked even if its record says it's open: that mark can outlive a friend who left with it open, and
                    // the owner's own answer is the truth.
                    Ask(entry, view);
                }
            }

            /// <summary>True if this claim may claim the chest itself (see the class): on a client, for a module that allows
            /// it, never a cart's or ship's chest, only one no game can answer for, and only beyond his hand-out; or, one
            /// nobody owns that a whole round of the server's hand-out has passed over (<paramref name="passedOver"/>: the
            /// server's area for him can be smaller than his game reckons).</summary>
            private bool MayClaimFar(Container chest, ZNetView view, bool passedOver)
            {
                if (ZNet.instance == null || ZNet.instance.IsServer() || !user.ClaimsFar || !NoGameAnswers(chest, view) || OnVehicle(chest) ||
                    !FarPossible())
                {
                    return false;
                }
                // A chest passed over by a round of the hand-out (nobody's, or its owner can't answer: NoGameAnswers above) is
                // claimed here, but never inside Far Fetched's grant (it hands it out itself, and would keep a chest claimed
                // here with him: never usable), and never by a claim made ahead of need, again and again (card A69, review 1,
                // M1 and m6; the last look, 2: between the server's real active area and the square BeyondHandOut uses).
                return BeyondHandOut(chest) || (passedOver && !quiet && !ServerReach.CoversExactly(chest));
            }

            // Claims the chest here and waits out its ConfirmWait before it counts as held.
            private void ClaimHere(Entry entry, ZNetView view)
            {
                ClaimFar(entry.Chest, view, user);
                entry.State = State.Confirming;
                entry.Since = Clock();
            }

            // Held, unless it is a chest this computer claimed itself (an earlier claim, or a craft's keep) that may not be
            // changed yet: then waited out as one claimed here.
            private static void HeldOrConfirming(Entry entry, float now)
            {
                if (MayChange(entry.Chest))
                {
                    entry.State = State.Held;
                    return;
                }
                entry.State = State.Confirming;
                entry.Since = now;
            }

            /// <summary>True if this claim asks for the chest (for Drop: a chest under way isn't let go early).</summary>
            internal bool Has(Container chest)
            {
                foreach (Entry entry in entries)
                {
                    if (entry.Chest == chest)
                    {
                        return true;
                    }
                }
                return false;
            }

            private void Ask(Entry entry, ZNetView view)
            {
                entry.State = State.Asked;
                entry.Since = Clock();
                if (!AskedChests.TryGetValue(view.GetZDO().m_uid, out Asked asked))
                {
                    asked = AskedChests[view.GetZDO().m_uid] = new Asked();
                }
                asked.Time = Clock();
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
                    bool farHeld = entry.State == State.Held && entry.Chest != null && Far.ContainsKey(entry.Chest);
                    if (entry.State != State.NoOwner && entry.State != State.Asked && entry.State != State.Granted &&
                        entry.State != State.Confirming && !farHeld)
                    {
                        continue;
                    }
                    ZNetView view = entry.Chest != null ? ChestHooks.NetView(entry.Chest) : null;
                    if (view == null || !view.IsValid())
                    {
                        entry.State = State.Gone;
                        continue;
                    }
                    if (farHeld)
                    {
                        if (!view.IsOwner() && MayClaimFar(entry.Chest, view, passedOver: true))
                        {
                            ClaimHere(entry, view);   // claimed here, then let go by the server while others were awaited
                            waiting = true;
                        }
                        else if (view.IsOwner() && !MayChange(entry.Chest))
                        {
                            entry.State = State.Confirming;   // claimed again meanwhile (a craft's keep): waited out again
                            entry.Since = now;
                            waiting = true;
                        }
                        continue;   // otherwise as it is: Finish counts one another game has now as in use
                    }
                    if (entry.State == State.Confirming)
                    {
                        if (view.IsOwner())
                        {
                            if (MayChange(entry.Chest))
                            {
                                entry.State = State.Held;   // claimed here, its newest copy in, and between the server's rounds
                                continue;
                            }
                        }
                        else if (MayClaimFar(entry.Chest, view, passedOver: true))
                        {
                            ClaimFar(entry.Chest, view, user);   // let go by the server meanwhile: claimed again, waited out again
                        }
                        else if (!view.GetZDO().HasOwner() && ServerReach.Covers(entry.Chest, loose: true))
                        {
                            Far.Remove(entry.Chest);   // no longer a claim here: Far Fetched's to hand back
                            entry.State = State.NoOwner;   // let go inside Far Fetched's grant: it hands it back (card A69)
                            entry.Since = now;
                            waiting = true;
                            continue;
                        }
                        else
                        {
                            entry.State = State.InUse;   // another player's game has it now (or claims here can't be made now)
                            continue;
                        }
                        if (now - entry.Since > ConfirmTimeout)
                        {
                            entry.State = State.NoAnswer;
                        }
                        else
                        {
                            waiting = true;
                        }
                        continue;
                    }
                    if (entry.State == State.NoOwner && view.GetZDO().HasOwner())
                    {
                        if (view.IsOwner())
                        {
                            HeldOrConfirming(entry, now);   // the server gave it to this computer
                            waiting |= entry.State == State.Confirming;   // claimed here meanwhile: waited out (harness, h38)
                            continue;
                        }
                        if (MayClaimFar(entry.Chest, view, passedOver: false))
                        {
                            ClaimHere(entry, view);   // the server's own now, and it keeps no copy here to answer with
                            waiting = true;
                            continue;
                        }
                        Ask(entry, view);
                    }
                    else if (entry.State == State.NoOwner && MayClaimFar(entry.Chest, view, now - entry.Since > HandOutRound))
                    {
                        ClaimHere(entry, view);   // he moved away meanwhile, or the server's hand-out passed it over
                        waiting = true;
                        continue;
                    }
                    else if (entry.State == State.Asked && !view.IsOwner() && MayClaimFar(entry.Chest, view, now - entry.Since > HandOutRound))
                    {
                        ClaimHere(entry, view);   // its owner left the game while it was asked
                        waiting = true;
                        continue;
                    }
                    else if ((entry.State == State.Granted || entry.State == State.Asked) && view.IsOwner())
                    {
                        // Ours, whoever handed it over: its owner's yes, or the server's hand-out every two seconds (a
                        // server that took the chest answers nothing for it). A late answer is still caught (CatchFor).
                        HeldOrConfirming(entry, now);
                        waiting |= entry.State == State.Confirming;
                        continue;
                    }
                    if (entry.State == State.Granted && ChestShare.MultiUserChest && now - entry.Since > MultiUserChestWait &&
                        view.GetZDO().GetInt(ZDOVars.s_inUse) == 1)
                    {
                        entry.State = State.InUse;   // MultiUserChest's owner said yes and kept it: someone has it open
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
                foreach (ShareResult share in shares)
                {
                    waiting |= share.Status == ShareStatus.Waiting;   // ChestShare.Tick gives up on it after its wait
                }
                return !waiting;
            }

            internal void Finish()
            {
                var result = new ClaimResult();
                List<Container> missed = null;
                foreach (Entry entry in entries)
                {
                    if (entry.State == State.NoAnswer)
                    {
                        (missed = missed ?? new List<Container>()).Add(entry.Chest);
                    }
                    ZNetView view = entry.Chest != null ? ChestHooks.NetView(entry.Chest) : null;
                    bool valid = view != null && view.IsValid();
                    switch (entry.State)
                    {
                        case State.Held when valid && MayChange(entry.Chest):
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
                result.Shared.AddRange(shares);
                if (missed != null && !quiet)
                {
                    try
                    {
                        ChestExplain.LogClaim(user, task, missed);   // why each couldn't be had (card A69)
                    }
                    catch (Exception error)
                    {
                        // Only a log line: it must never cost the module its work.
                        ZLog.LogWarning($"[QoL Mods chest service] The chests a claim couldn't have couldn't be described: {error.Message}");
                    }
                }
                user.Guard(task, () => done(result));
            }
        }
    }
}
