using System;
using System.Collections.Generic;
using QoLMods.Catalogue;
using QoLMods.Chests;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// Stack Overflow's tidy (card A42, runbook job 27, inside StoreStash at his pick): chests set up alike act as partitions
    /// of one chest, and a tidy moves things between them so each kind sits in as few of them as possible (his boar pelts
    /// example: the 3 stacks go into the chest with 8, other things moving out of its way if they must). A press of the key
    /// (pointing at a set-up chest or with its window open), the button, or by itself (after a quick stack, or on a timer):
    /// it finds the group (the chests a quick stack may use, set up alike: AlikeKey), plans from this computer's copies
    /// (TidyPlan), claims the chests the plan touches from their owners in one claim (none for his own, so at his own chests
    /// it all happens in the frame he pressed), and in the frame they're his reloads each, plans again over them alone, and
    /// carries the plan out through the chest service's move (ChestMove: into the target first, measured, then out of the
    /// source, every chest sent and held). The chests it changed are then sorted (Sorting, as they would be as they open),
    /// and he's told what moved. A press while one is under way does nothing.
    /// </summary>
    internal static class Tidy
    {
        /// <summary>How long a press waits for another module's claim of the chest service to end before giving up.</summary>
        internal const float WaitFor = 5f;

        /// <summary>How often a waiting press asks again (each ask plans afresh).</summary>
        internal const float AskEvery = 0.25f;

        /// <summary>How long a tidy under way may take before a new press is let through anyway (a claim settles within about
        /// 13 seconds at most).</summary>
        internal const float GiveUpAfter = 45f;

        /// <summary>"Ask first": how long the second press may come after the first.</summary>
        internal const float ConfirmFor = 10f;

        /// <summary>A tidy by itself waits this long after a quick stack before it starts, so the quick stack's own claim is
        /// over and its message read.</summary>
        internal const float AfterQuickStackDelay = 0.5f;

        /// <summary>The words for an error during a tidy, in the module's warning.</summary>
        internal const string Task = "tidying chests set up alike (Stack Overflow)";

        /// <summary>The game's clock. Only the scratch test harness sets another.</summary>
        internal static Func<float> Clock = () => Time.time;

        /// <summary>Whether this computer owns a chest now. Only the scratch test harness sets another.</summary>
        internal static Func<Container, bool> Mine = ChestClaims.MayChange;   // one claimed far: once it may be changed (card A64)

        /// <summary>Whether another player has a chest open (the chest service's own reading of its record). Only the scratch
        /// test harness sets another.</summary>
        internal static Func<Container, bool> OpenElsewhere = ChestFinder.OpenElsewhere;

        /// <summary>Whether a chest is a cart's, and whether a ship's. Only the scratch test harness sets others.</summary>
        internal static Func<Container, bool> IsCart = chest => chest.GetComponentInParent<Vagon>() != null;

        internal static Func<Container, bool> IsShip = chest => chest.GetComponentInParent<Ship>() != null;

        /// <summary>Where he stands, the centre a tidy looks around. Only the scratch test harness sets another.</summary>
        internal static Func<Vector3> Centre = () => ChestFinder.Where(Player.m_localPlayer);

        /// <summary>Whether he can tidy now: in the world, not dead or between places. Only the scratch test harness sets
        /// another.</summary>
        internal static Func<bool> Present = () =>
        {
            Player player = Player.m_localPlayer;
            return player != null && !player.IsTeleporting() && !player.IsDead();
        };

        private enum From
        {
            Key,
            Button,
            AfterQuickStack,
            Timer,
        }

        // One tidy asked for: by whom, and the chest it starts from (the one pointed at or open; for a tidy after a quick
        // stack, the set-up chests it put things into).
        private sealed class Request
        {
            internal From Source;
            internal Container Chest;
            internal List<Container> Chests;

            /// <summary>The second press "Ask first" waited for: it goes on, though it may wait for the chest service past the
            /// ten seconds (review round 2, nit 1).</summary>
            internal bool Confirmed;

            internal bool Silent => Source == From.AfterQuickStack || Source == From.Timer;

            internal bool EveryGroup => Source == From.Timer || (Source != From.AfterQuickStack && OverflowSettings.Covers.Value == TidyCovers.EveryGroupInRange);
        }

        /// <summary>A group of chests set up alike, nearest first, and the text they're alike by.</summary>
        internal sealed class Group
        {
            internal string Key;
            internal readonly List<Container> Chests = new List<Container>();

            /// <summary>Chests set up alike that someone else has open: left out this time.</summary>
            internal int InUse;
        }

        private static Request pending;
        private static float pendingSince = -1f;
        private static float askedAt = -1f;
        private static float runningSince = -1f;
        private static int started;
        private static Request waitingAuto;
        private static float autoAfter = -1f;
        private static float lastTimer = -1f;
        private static string confirming;
        private static float confirmUntil = -1f;

        internal static bool Running => runningSince >= 0f && Clock() - runningSince < GiveUpAfter;

        /// <summary>Every frame: a press waiting for another module's claim asks again once the chest service is free (planning
        /// again only then: review round 1, major 3), or gives up after WaitFor; a tidy by itself starts when it's due.</summary>
        internal static void Update()
        {
            if (!OverflowSettings.Active)
            {
                pending = null;
                waitingAuto = null;
                lastTimer = -1f;
                return;
            }
            if (pending != null)
            {
                if (Clock() - askedAt < AskEvery)
                {
                    return;
                }
                if (ChestClaims.Busy && Clock() - pendingSince <= WaitFor)
                {
                    askedAt = Clock();   // still busy: nothing planned again until it's free
                    return;
                }
                TryStart(pending);
                return;
            }
            if (waitingAuto != null && Clock() >= autoAfter && !Running)
            {
                Request auto = waitingAuto;
                waitingAuto = null;
                Ask(auto);
                return;
            }
            Timer();
        }

        /// <summary>The undo step, and switching off: a press still waiting is dropped, and nothing waits to run by itself.</summary>
        internal static void Cancel()
        {
            pending = null;
            runningSince = -1f;
            waitingAuto = null;
            confirming = null;
        }

        /// <summary>A press of the tidy key (Keys): with a chest's window open, that chest's group; else the set-up chest he
        /// points at (pointing at nothing, nothing happens, as E on nothing); with "What a tidy covers" every group, wherever
        /// he points.</summary>
        internal static void KeyPressed()
        {
            InventoryGui gui = InventoryGui.instance;
            Container open = gui != null && InventoryGui.IsVisible() ? GameHooks.OpenChest(gui) : null;
            if (open != null && gui.m_splitDialog != null && gui.m_splitDialog.IsActive)
            {
                return;   // the split window has the keys
            }
            Container chest = open != null ? open : Pointed(Player.m_localPlayer);   // Unity's own test: never ?? on its objects
            if (chest == null && OverflowSettings.Covers.Value != TidyCovers.EveryGroupInRange)
            {
                return;
            }
            Ask(new Request { Source = From.Key, Chest = chest });
        }

        /// <summary>A click of the tidy button in an open chest's window.</summary>
        internal static void ButtonClicked(Container chest)
        {
            if (chest != null)
            {
                Ask(new Request { Source = From.Button, Chest = chest });
            }
        }

        /// <summary>After a quick stack (QuickStack.Done), with "Tidy after each quick stack" on: the groups of the set-up chests
        /// it put things into, a moment later.</summary>
        internal static void AfterQuickStack(StackResult result)
        {
            if (!OverflowSettings.Active || !OverflowSettings.AfterQuickStack.Value || result == null || result.Moved <= 0)
            {
                return;
            }
            var chests = new List<Container>();
            foreach (Container chest in result.ChestsUsed)
            {
                if (chest != null && AlikeKey(chest) != null)
                {
                    chests.Add(chest);
                }
            }
            if (chests.Count == 0)
            {
                return;
            }
            // A quick stack again before the last one's tidy ran: one tidy for both (the harness's A42-after-each-quick-stack).
            if (waitingAuto != null && waitingAuto.Source == From.AfterQuickStack)
            {
                foreach (Container chest in chests)
                {
                    if (!waitingAuto.Chests.Contains(chest))
                    {
                        waitingAuto.Chests.Add(chest);
                    }
                }
            }
            else
            {
                waitingAuto = new Request { Source = From.AfterQuickStack, Chests = chests };
            }
            autoAfter = Clock() + AfterQuickStackDelay;
        }

        // "Tidy every so many minutes": every group in reach, when it's due (the first time a full period after it's on).
        private static void Timer()
        {
            int minutes = OverflowSettings.EveryMinutes.Value;
            if (minutes <= 0)
            {
                lastTimer = -1f;
                return;
            }
            if (lastTimer < 0f || Clock() < lastTimer)
            {
                lastTimer = Clock();
                return;
            }
            if (Clock() - lastTimer < minutes * 60f || Running || !Present())
            {
                return;
            }
            lastTimer = Clock();
            Ask(new Request { Source = From.Timer });
        }

        /// <summary>The set-up chest he points at, or null.</summary>
        internal static Container Pointed(Player player)
        {
            GameObject hover = player != null ? player.GetHoverObject() : null;
            Container chest = hover != null ? hover.GetComponentInParent<Container>() : null;
            return Setups.Settable(chest) ? chest : null;
        }

        private static void Ask(Request request)
        {
            if (!OverflowSettings.Active || Running || pending != null)
            {
                return;
            }
            pending = request;
            pendingSince = Clock();
            TryStart(request);
        }

        // ---- Which chests.

        /// <summary>The rules a tidy finds chests by: quick stack's "Which chests" and its search radius (or the tidy's own),
        /// leaving alone what a tidy leaves alone (WhyLeftAlone). Reserved chests, the world's own, gravestones, chests he
        /// couldn't open by hand and ships someone else is aboard are the chest service's own rules (ChestFinder).</summary>
        internal static ChestRules FindRules() => new ChestRules
        {
            Range = OverflowSettings.Range.Value > 0f ? OverflowSettings.Range.Value : ChestSettings.RangeOf(Settings.Range),
            OnlyBuiltByMe = Settings.Chests.Value == WhichChests.OnlyChestsIBuilt,
            Skip = chest => WhyLeftAlone(chest) != null,
        };

        /// <summary>Why a tidy leaves a chest alone besides the chest service's rules, as words that follow "the chest is";
        /// null when it doesn't.</summary>
        internal static string WhyLeftAlone(Container chest)
        {
            if (!OverflowSettings.Carts.Value && IsCart(chest))
            {
                return "a cart's (\"Carts' chests take part\" is off)";
            }
            if (!OverflowSettings.Ships.Value && IsShip(chest))
            {
                return "a ship's (\"Ships' chests take part\" is off)";
            }
            long me = ChestClaims.PlayerId();
            if (OverflowSettings.OthersMarked.Value && ChestMine.MarkedByOther(chest, me))
            {
                return "marked as someone else's";
            }
            if (!Settings.PersonalChests.Value && Personal.LeftAloneByP(chest, me))
            {
                return "one of your personal chests (\"Use my personal chests\" is off)";
            }
            // A chest Craft from Chests keeps for a craft whose bar is running (review round 1, minor 6): its materials stay.
            if (ChestClaims.IsKeptForCraft(chest))
            {
                return "kept for a craft that's under way";
            }
            return null;
        }

        // The alike texts already worked out, by the setting and the record's text (review round 1, nit 5: the button and
        // the hover ask every frame).
        private static readonly Dictionary<string, string> AlikeTexts = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The text a chest's setup is compared by (card A42, decision 1): its setup's own, or with its biomes left
        /// out ("Chests set up alike means"); null for a chest without a setup a tidy can follow (none, or a newer version's,
        /// or one too long).</summary>
        internal static string AlikeKey(Container chest)
        {
            ItemPicks picks = Setups.Of(chest);
            if (picks == null || !picks.Readable)
            {
                return null;
            }
            bool ignoringBiomes = OverflowSettings.Alike.Value == AlikeMeans.IgnoringBiomes;
            string record = ChestSetup.Read(chest);
            string cached = (ignoringBiomes ? "b\n" : "=\n") + record;
            if (!AlikeTexts.TryGetValue(cached, out string key))
            {
                if (AlikeTexts.Count >= 512)
                {
                    AlikeTexts.Clear();
                }
                AlikeTexts[cached] = key = KeyOf(picks, ignoringBiomes);
            }
            return key;
        }

        /// <summary>A setup's text as chests are compared by it: whole, or with its biomes left out, every other part kept
        /// (its excepts too: review round 1, nit 7); biomes alone then read as everything ("picks1"). A pick's own biomes
        /// are biomes too (card A81: "Mistlands Weapons" is alike "Weapons" from the Plains, ignoring biomes).</summary>
        internal static string KeyOf(ItemPicks picks, bool ignoringBiomes) => ignoringBiomes ? picks.TextWithoutBiomes() : picks.Text();

        /// <summary>
        /// The groups of chests set up alike among <paramref name="found"/> (nearest first, as ChestFinder.Find gives them):
        /// every chest with a setup a tidy can follow, by its alike text, each group nearest first, in the order their
        /// nearest chests come; a chest someone else has open is left out and counted.
        /// </summary>
        internal static List<Group> GroupsOf(IList<Container> found)
        {
            var groups = new List<Group>();
            var byKey = new Dictionary<string, Group>(StringComparer.Ordinal);
            foreach (Container chest in found)
            {
                string key = chest != null ? AlikeKey(chest) : null;
                if (key == null)
                {
                    continue;
                }
                if (!byKey.TryGetValue(key, out Group group))
                {
                    byKey[key] = group = new Group { Key = key };
                    groups.Add(group);
                }
                if (OpenElsewhere(chest))
                {
                    group.InUse++;
                }
                else
                {
                    group.Chests.Add(chest);
                }
            }
            return groups;
        }

        // The groups a request covers, from the chests around him; null with a reason he's told when there's none.
        private static List<Group> GroupsFor(Request request, out string why)
        {
            why = null;
            ChestRules rules = FindRules();
            List<Group> all = GroupsOf(ChestFinder.Find(Centre(), rules));
            var covered = new List<Group>();
            if (request.EveryGroup)
            {
                foreach (Group group in all)
                {
                    if (group.Chests.Count >= 2)
                    {
                        covered.Add(group);
                    }
                }
                if (covered.Count == 0)
                {
                    why = Words.NoGroups;
                }
                return covered.Count > 0 ? covered : null;
            }
            if (request.Source == From.AfterQuickStack)
            {
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (Container chest in request.Chests)
                {
                    string key = chest != null ? AlikeKey(chest) : null;
                    if (key != null)
                    {
                        keys.Add(key);
                    }
                }
                foreach (Group group in all)
                {
                    if (keys.Contains(group.Key) && group.Chests.Count >= 2)
                    {
                        covered.Add(group);
                    }
                }
                return covered.Count > 0 ? covered : null;
            }
            Container from = request.Chest;
            why = WhyNotThis(from, rules);
            if (why != null)
            {
                return null;
            }
            string own = AlikeKey(from);
            foreach (Group group in all)
            {
                if (group.Key == own)
                {
                    if (group.Chests.Count < 2)
                    {
                        why = Words.Alone(group.InUse);
                        return null;
                    }
                    covered.Add(group);
                    return covered;
                }
            }
            why = Words.Alone(0);   // not found nearby at all (it can't be: he's at it), said as alone
            return null;
        }

        // Why the chest a press starts from can't be tidied, in words; null when it can.
        private static string WhyNotThis(Container chest, ChestRules rules)
        {
            if (chest == null)
            {
                return Words.NoChest;
            }
            if (!Setups.Settable(chest))
            {
                return Words.CantBeSetUp;   // a gravestone, the world's own chest, a cart's while carts' setups are off (nit 8)
            }
            ItemPicks picks = Setups.Of(chest);
            if (picks == null)
            {
                return Words.NoSetup(SetupSettings.Key.Value.MainKey != KeyCode.None ? Settings.KeyText(SetupSettings.Key.Value) : null);
            }
            if (!picks.Readable)
            {
                return Words.Unreadable;
            }
            if (ChestReserve.IsReserved(chest))
            {
                return Words.LeftAlone("reserved (Reserve Stock)");
            }
            string why = WhyLeftAlone(chest);
            if (why == null)
            {
                why = ChestFinder.Judge(chest, rules, ChestClaims.PlayerId());
            }
            return why != null ? Words.LeftAlone(why) : null;
        }

        // ---- The plan.

        /// <summary>A chest as the plan sees it: its grid, its stacks in the inventory's own order, and what its setup takes.</summary>
        internal static TidyPlan.Chest Model(Container chest)
        {
            Inventory inventory = chest.GetInventory();
            ItemPicks picks = Setups.Of(chest);
            var model = new TidyPlan.Chest
            {
                Width = inventory.GetWidth(),
                Height = inventory.GetHeight(),
                Takes = picks != null && picks.Readable ? (Func<string, bool>)(name => picks.Fit(name) > 0) : null,
            };
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                model.Stacks.Add(new TidyPlan.Stack
                {
                    Name = item.m_shared.m_name,
                    Quality = item.m_quality,
                    WorldLevel = item.m_worldLevel,
                    Amount = item.m_stack,
                    MaxStack = item.m_shared.m_maxStackSize,
                    X = item.m_gridPos.x,
                    Y = item.m_gridPos.y,
                    TopFirst = TopFirst(item),
                    // The stack in his hand, and one marked as worn (the chest service never moves either: ChestPut.Movable).
                    Pinned = ChestHooks.InHisHand(item) || item.m_equipped,
                });
            }
            return model;
        }

        /// <summary>The game's own rule for where a new stack goes (Inventory.TopFirst, private): weapons, tools, shields,
        /// utility, misc and trinkets from the top row, everything else from the bottom.</summary>
        internal static bool TopFirst(ItemDrop.ItemData item)
        {
            if (item.IsWeapon())
            {
                return true;
            }
            ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
            return type == ItemDrop.ItemData.ItemType.Tool || type == ItemDrop.ItemData.ItemType.Shield || type == ItemDrop.ItemData.ItemType.Utility ||
                   type == ItemDrop.ItemData.ItemType.Misc || type == ItemDrop.ItemData.ItemType.Trinket;
        }

        /// <summary>The plan for each group, sharing the most stacks a tidy moves: its orders in the order to carry them out,
        /// as the chest service's moves (each chest by itself), and what each did.</summary>
        internal sealed class Plans
        {
            internal readonly List<MoveOrder> Orders = new List<MoveOrder>();
            internal readonly List<Container> Touched = new List<Container>();
            internal readonly List<Container> Chests = new List<Container>();
            internal int Stacks;
            internal int Items;
            internal int StillSpread;
            internal int Misplaced;
            internal bool OutOfMoves;
            internal int InUse;

            /// <summary>What each chest held as the plan was made (its items, in order, and their stacks), when it may be used
            /// again in the same frame: only if every chest still holds exactly these once reloaded.</summary>
            internal Dictionary<Container, List<KeyValuePair<ItemDrop.ItemData, int>>> Seen;
        }

        internal static Plans PlanAll(IList<Group> groups, TidyPlan.Rules rules)
        {
            var plans = new Plans();
            int most = rules.MostStacks;
            foreach (Group group in groups)
            {
                plans.Chests.AddRange(group.Chests);
                plans.InUse += group.InUse;
                if (plans.OutOfMoves)
                {
                    continue;
                }
                var models = new List<TidyPlan.Chest>();
                foreach (Container chest in group.Chests)
                {
                    models.Add(Model(chest));
                }
                rules.MostStacks = Math.Max(0, most - plans.Stacks);
                TidyPlan.Outcome outcome = TidyPlan.Plan(models, rules);
                foreach (TidyPlan.Order order in outcome.Orders)
                {
                    plans.Orders.Add(new MoveOrder
                    {
                        From = group.Chests[order.From],
                        To = group.Chests[order.To],
                        Key = KeyFor(order.Name),
                        Amount = order.Amount,
                        TopUpOnly = order.TopUpOnly,
                    });
                }
                foreach (int c in outcome.Touched)
                {
                    if (!plans.Touched.Contains(group.Chests[c]))
                    {
                        plans.Touched.Add(group.Chests[c]);
                    }
                }
                plans.Stacks += outcome.Stacks;
                plans.Items += outcome.Items;
                plans.StillSpread += outcome.StillSpread;
                plans.Misplaced += outcome.Misplaced;
                plans.OutOfMoves |= outcome.OutOfMoves;
            }
            rules.MostStacks = most;
            return plans;
        }

        /// <summary>A thing's name as the chest service's key for every quality of it (ChestStock.Parse reads a last "@" and a
        /// number as a quality: a name with one of its own gets "@-1", any quality, after it).</summary>
        internal static string KeyFor(string name)
        {
            int at = name.LastIndexOf('@');
            return at > 0 && int.TryParse(name.Substring(at + 1), out int _) ? name + "@-1" : name;
        }

        // ---- The tidy.

        private static void TryStart(Request request)
        {
            askedAt = Clock();
            if (!Present())
            {
                pending = null;
                return;
            }
            List<Group> groups = GroupsFor(request, out string why);
            if (groups == null)
            {
                pending = null;
                if (!request.Silent && why != null)
                {
                    Say(why);
                }
                return;
            }
            TidyPlan.Rules rules = RulesFor(request);
            Plans plans = PlanAll(groups, rules);
            if (plans.Orders.Count == 0)
            {
                pending = null;
                if (!request.Silent)
                {
                    SayAfter(Words.Nothing(groups, plans.StillSpread, rules.MakeRoom, plans.InUse, 0, plans.Misplaced));
                }
                return;
            }
            string target = ConfirmKey(request, groups);
            int askOver = OverflowSettings.AskOver.Value;
            if (!request.Silent && askOver > 0 && plans.Stacks > askOver && !request.Confirmed)
            {
                if (confirming == target && Clock() <= confirmUntil)
                {
                    request.Confirmed = true;
                }
                else
                {
                    pending = null;
                    confirming = target;
                    confirmUntil = Clock() + ConfirmFor;
                    Say(Words.AskFirst(plans.Stacks, plans.Touched.Count, ConfirmFor));
                    return;
                }
            }
            runningSince = Clock();
            int number = ++started;
            if (plans.Touched.TrueForAll(chest => Mine(chest)))
            {
                // Every chest his already: no claim, so even while another module's claim runs, in this frame.
                pending = null;
                confirming = null;
                var held = new ClaimResult();
                held.Held.AddRange(plans.Touched);
                // Every chest of the groups his too: the plan just made stands, unless the reload finds a chest changed (his
                // copy can be up to a second older than its record, just after a friend's change or a server's re-sort hands it
                // back: the last look, minor 1).
                Plans first = groups.TrueForAll(group => group.Chests.TrueForAll(chest => Mine(chest))) ? plans : null;
                if (first != null)
                {
                    first.Seen = new Dictionary<Container, List<KeyValuePair<ItemDrop.ItemData, int>>>();
                    foreach (Container chest in first.Chests)
                    {
                        first.Seen[chest] = Held(chest);
                    }
                }
                Module.Guard(Task, () => Claimed(number, request, groups, held, first));
                return;
            }
            if (ChestClaims.Start(Module.Chests, plans.Touched, null, null, held => Claimed(number, request, groups, held), Task))
            {
                pending = null;
                confirming = null;   // only now: a confirmed press waiting on the chest service keeps it (review round 1, minor 2)
                return;
            }
            runningSince = -1f;   // another module's claim is under way: asked again in a moment, for a while
            if (Clock() - pendingSince > WaitFor)
            {
                pending = null;
                if (!request.Silent)
                {
                    Say(Words.Busy);
                }
            }
        }

        /// <summary>How a tidy tidies, as the settings say now: a tidy by itself never asks first, so it moves no more than
        /// "Ask first when a tidy would move more than" allows, while that's on (review round 1, minor 6).</summary>
        internal static TidyPlan.Rules RulesFor(bool byItself)
        {
            TidyPlan.Rules rules = OverflowSettings.PlanRules();
            int askOver = OverflowSettings.AskOver.Value;
            if (byItself && askOver > 0)
            {
                rules.MostStacks = Math.Min(rules.MostStacks, askOver);
            }
            return rules;
        }

        private static TidyPlan.Rules RulesFor(Request request) => RulesFor(request.Silent);

        // What "Ask first" remembers a press by: the groups it covers.
        private static string ConfirmKey(Request request, List<Group> groups)
        {
            var keys = new List<string>();
            foreach (Group group in groups)
            {
                keys.Add(group.Key);
            }
            return (request.EveryGroup ? "every\n" : "group\n") + string.Join("\n", keys);
        }

        /// <summary>
        /// In the frame the chests the plan touches are his (inside the module's guard): each chest of the groups that is his
        /// now (claimed, or his already) is reloaded and read again (a friend may have changed one meanwhile, or set it up
        /// otherwise, or reserved it), the plan is made again over those alone, and carried out through the chest service's
        /// move, which needs no claim for chests already his, so in this same frame. Then each chest that changed is sorted
        /// (as the settings say), sent and held again, and he's told. <paramref name="first"/>: the plan made a moment ago in
        /// this same frame, every chest of the groups his already: it stands while every chest is still there, alike, and
        /// holding exactly what it held (a reload that brought in a newer record changes it), so a big group isn't planned
        /// twice in one frame.
        /// </summary>
        private static void Claimed(int number, Request request, List<Group> groups, ClaimResult held, Plans first = null)
        {
            try
            {
                if (!OverflowSettings.Active)
                {
                    return;   // switched off while waiting: move nothing
                }
                var claimed = new HashSet<Container>(held.Held);
                var again = new List<Group>();
                ChestRules finder = FindRules();
                long me = ChestClaims.PlayerId();
                int changed = 0;
                foreach (Group group in groups)
                {
                    var ready = new Group { Key = group.Key, InUse = group.InUse };
                    foreach (Container chest in group.Chests)
                    {
                        if (chest == null || !(claimed.Contains(chest) || Mine(chest)))
                        {
                            continue;
                        }
                        ChestHooks.Reload(chest);
                        // Read again now it's his: reserved, set up otherwise or marked meanwhile, or the chest service's own
                        // rules (a friend about to open it, a ship someone boarded: review round 1, minor 5).
                        if (ChestReserve.IsReserved(chest) || AlikeKey(chest) != group.Key || WhyLeftAlone(chest) != null ||
                            ChestFinder.Judge(chest, finder, me) != null)
                        {
                            changed++;
                            continue;
                        }
                        ready.Chests.Add(chest);
                    }
                    if (ready.Chests.Count >= 2)
                    {
                        again.Add(ready);
                    }
                }
                TidyPlan.Rules rules = RulesFor(request);
                Plans plans = first != null && changed == 0 && Unchanged(again, groups) && StillHeld(first) ? first : PlanAll(again, rules);
                var before = new Dictionary<Container, Dictionary<string, int>>();
                foreach (Container chest in plans.Chests)
                {
                    before[chest] = Contents(chest);
                }
                int inUse = held.InUse;
                foreach (Group group in groups)
                {
                    inUse += group.InUse;
                }
                if (plans.Orders.Count == 0)
                {
                    if (request.Silent)
                    {
                        return;
                    }
                    // The first plan found work: if chests were lost on the way, say so, not "already tidy" (review round 1,
                    // minor 3).
                    if (held.InUse + held.NoAnswer + held.Gone + changed > 0)
                    {
                        Say(Words.NoneMoved(inUse, held.NoAnswer, 0, held.Gone + changed));
                    }
                    else
                    {
                        SayAfter(Words.Nothing(again.Count > 0 ? again : groups, plans.StillSpread, rules.MakeRoom, inUse, held.NoAnswer, plans.Misplaced));
                    }
                    return;
                }
                // Every chest of the orders is his in this frame, so the move needs no claim and runs now.
                if (!ChestMove.Start(Module.Chests, plans.Orders, false, result => Moved(request, plans, before, result, inUse, held.NoAnswer, rules.MakeRoom)) &&
                    !request.Silent)
                {
                    Say(Words.Busy);
                }
            }
            finally
            {
                if (number == started)
                {
                    runningSince = -1f;
                }
            }
        }

        // What a chest holds: its items, in the inventory's own order, and their stacks.
        private static List<KeyValuePair<ItemDrop.ItemData, int>> Held(Container chest)
        {
            var held = new List<KeyValuePair<ItemDrop.ItemData, int>>();
            foreach (ItemDrop.ItemData item in chest.GetInventory().GetAllItems())
            {
                held.Add(new KeyValuePair<ItemDrop.ItemData, int>(item, item.m_stack));
            }
            return held;
        }

        // Whether every chest of a plan still holds exactly what it held as the plan was made (a reload that loads builds new
        // items, so any real reload fails this).
        private static bool StillHeld(Plans plans)
        {
            if (plans.Seen == null)
            {
                return false;
            }
            foreach (Container chest in plans.Chests)
            {
                if (chest == null || !plans.Seen.TryGetValue(chest, out List<KeyValuePair<ItemDrop.ItemData, int>> seen))
                {
                    return false;
                }
                List<KeyValuePair<ItemDrop.ItemData, int>> now = Held(chest);
                if (now.Count != seen.Count)
                {
                    return false;
                }
                for (int i = 0; i < now.Count; i++)
                {
                    if (!ReferenceEquals(now[i].Key, seen[i].Key) || now[i].Value != seen[i].Value)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // Whether the groups read again once the chests are his are the groups planned at first, chest for chest.
        private static bool Unchanged(List<Group> again, List<Group> groups)
        {
            if (again.Count != groups.Count)
            {
                return false;
            }
            for (int g = 0; g < groups.Count; g++)
            {
                if (again[g].Chests.Count != groups[g].Chests.Count)
                {
                    return false;
                }
                for (int c = 0; c < groups[g].Chests.Count; c++)
                {
                    if (again[g].Chests[c] != groups[g].Chests[c])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // What a chest holds, by name: its stacks' count first ("#cells"), to tell a merge apart from no change.
        private static Dictionary<string, int> Contents(Container chest)
        {
            var contents = new Dictionary<string, int>(StringComparer.Ordinal);
            List<ItemDrop.ItemData> items = chest.GetInventory().GetAllItems();
            contents["#cells"] = items.Count;
            foreach (ItemDrop.ItemData item in items)
            {
                contents.TryGetValue(item.m_shared.m_name, out int had);
                contents[item.m_shared.m_name] = had + item.m_stack;
            }
            return contents;
        }

        // After the chest service's moves, in the same frame: which chests changed and what went into each, then the sort,
        // the words, the sound and the light.
        private static void Moved(Request request, Plans plans, Dictionary<Container, Dictionary<string, int>> before, MoveResult result, int inUse,
                                  int noAnswer, bool makeRoom)
        {
            var changed = new List<Container>();
            var into = new Dictionary<Container, Dictionary<string, int>>();
            foreach (KeyValuePair<Container, Dictionary<string, int>> each in before)
            {
                Container chest = each.Key;
                if (chest == null)
                {
                    continue;
                }
                Dictionary<string, int> now = Contents(chest);
                var gained = new Dictionary<string, int>(StringComparer.Ordinal);
                bool differs = now.Count != each.Value.Count;
                foreach (KeyValuePair<string, int> thing in now)
                {
                    each.Value.TryGetValue(thing.Key, out int had);
                    differs |= had != thing.Value;
                    if (thing.Key != "#cells" && thing.Value > had)
                    {
                        gained[thing.Key] = thing.Value - had;
                    }
                }
                if (differs)
                {
                    changed.Add(chest);
                }
                if (gained.Count > 0)
                {
                    into[chest] = gained;
                }
            }
            SortChanged(changed);
            if (result.Moved > 0)
            {
                if (OverflowSettings.Sound.Value)
                {
                    Sorting.Sound();
                }
                if (OverflowSettings.LightUp.Value)
                {
                    foreach (Container chest in changed)
                    {
                        LightUp.Glow(chest);
                    }
                }
            }
            if (request.Silent && result.Moved <= 0)
            {
                return;
            }
            TidySay say = OverflowSettings.Say.Value;
            if (result.Moved > 0 && say == TidySay.Nothing)
            {
                return;
            }
            if (result.Moved <= 0)
            {
                Say(Words.NoneMoved(inUse + result.ChestsInUse, noAnswer + result.ChestsNoAnswer, result.ChestsReserved));
                return;
            }
            // The plan's stacks when everything moved as planned (the plan carried each order out as the chest service does);
            // else what the chest service counts, items.
            int stacks = result.Moved == plans.Items ? plans.Stacks : 0;
            string text = Words.Tidied(changed.Count, stacks, result, plans.OutOfMoves, plans.StillSpread, inUse, noAnswer, makeRoom, plans.Misplaced);
            if (say == TidySay.Detailed)
            {
                string lines = Words.Where(changed, into, chest => Messages.Label(chest, Setups.Of(chest)), Setups.ItemName);
                if (lines.Length > 0)
                {
                    text += "\n" + lines;
                }
            }
            Say(text);
        }

        // The chests a tidy changed, sorted as "Sort the chests a tidy changed" says, each one the sort changed sent and held
        // again (the move's own send went before the sort).
        private static void SortChanged(List<Container> changed)
        {
            TidySort when = OverflowSettings.Sort.Value;
            if (when == TidySort.Never || !SortSettings.On.Value)
            {
                return;
            }
            foreach (Container chest in changed)
            {
                if (when == TidySort.LikeOpening && !SortsAsItOpens(chest))
                {
                    continue;
                }
                if (Sorting.SortForTidy(chest))
                {
                    try   // never throws here: a throw would hide the error that brought it
                    {
                        ChestClaims.Hold(chest);
                        ZDOMan.instance.ForceSendZDO(ChestHooks.NetView(chest).GetZDO().m_uid);
                    }
                    catch (Exception error)
                    {
                        Module.Log.LogWarning($"A chest sorted after a tidy couldn't be sent or held: {error}");
                    }
                }
            }
        }

        // Whether "Sort a chest as it opens" would sort this chest as it opens.
        private static bool SortsAsItOpens(Container chest)
        {
            switch (SortSettings.OnOpen.Value)
            {
                case SortOnOpenChoice.Off:
                    return false;
                case SortOnOpenChoice.ChestsIBuilt:
                    if (!Sorting.BuiltByMe(chest))
                    {
                        return false;
                    }
                    break;
            }
            return SortSettings.OthersMarked.Value || !Sorting.MarkedByOther(chest);
        }

        // A message where "Message after a quick stack" puts it: the middle of the screen, or top left (also while it's off).
        private static void Say(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            MessageHud.MessageType type = Settings.Message.Value == MessagePlace.Center ? MessageHud.MessageType.Center : MessageHud.MessageType.TopLeft;
            if (type == MessageHud.MessageType.Center)
            {
                Messages.Center(text);
            }
            else
            {
                Messages.TopLeft(text);
            }
            MessageHold.Start(type, text);
        }

        // The words after a tidy that ran but moved nothing, as "Say what moved" allows: Nothing says only why it couldn't.
        private static void SayAfter(string text)
        {
            if (OverflowSettings.Say.Value == TidySay.Nothing && text != null && text.StartsWith(Words.AlreadyTidyStart, StringComparison.Ordinal))
            {
                return;
            }
            Say(text);
        }

        /// <summary>What he's told: plain functions of what happened, so the scratch harness reads every one.</summary>
        internal static class Words
        {
            internal const string AlreadyTidyStart = "Tidy: already tidy";

            internal const string NoChest = "Tidy: point at a set-up chest, or open one";

            internal const string CantBeSetUp = "Tidy: this chest can't have a setup, so it isn't tidied";

            internal const string NoGroups = "Tidy: no two chests nearby are set up alike";

            internal const string Unreadable = "Tidy: this chest's setup is from a newer version of the pack (or too long to follow), so it isn't tidied";

            internal const string Busy = "Tidy: the chests nearby are busy, press again in a moment";

            /// <summary>The most chests the detailed lines name, one line each; then "and 2 more chests".</summary>
            internal const int MostChests = 5;

            /// <summary>The most kinds of things a detailed line names; then "and 3 more".</summary>
            internal const int MostKinds = 4;

            internal static string NoSetup(string setupKey) =>
                "Tidy: this chest has no setup, so no chest is set up like it" + (setupKey != null ? $" ({setupKey} sets one up)" : string.Empty);

            internal static string LeftAlone(string why) => $"Tidy: this chest is {why}, so it isn't tidied";

            internal static string Alone(int inUse) =>
                "Tidy: no other chest nearby is set up like this one" +
                (inUse > 0 ? $" ({Count(inUse, "chest set up like it is", "chests set up like it are")} in use)" : string.Empty);

            internal static string AskFirst(int stacks, int chests, float seconds) =>
                $"Tidy: {Count(stacks, "stack", "stacks")} would move among {Count(chests, "chest", "chests")}. Press again within {seconds:0} seconds to tidy";

            /// <summary>Nothing moved: already tidy, or nothing could move (the chests too full, or nothing may be moved out of
            /// the way); with what was left out. <paramref name="misplaced"/>: kinds left in a chest whose setup doesn't take
            /// them (never "already tidy" then).</summary>
            internal static string Nothing(IList<Group> groups, int stillSpread, bool makeRoom, int inUse = 0, int noAnswer = 0, int misplaced = 0)
            {
                string text;
                if (stillSpread > 0 || misplaced > 0)
                {
                    var left = new List<string>();
                    if (stillSpread > 0)
                    {
                        left.Add($"{Count(stillSpread, "kind stays", "kinds stay")} in more than one chest");
                    }
                    if (misplaced > 0)
                    {
                        left.Add($"{Count(misplaced, "kind stays", "kinds stay")} in a chest whose setup doesn't take it");
                    }
                    string kinds = string.Join("; ", left);
                    text = makeRoom
                        ? $"Tidy: nothing can move, the chests are too full ({kinds})"
                        : $"Tidy: nothing can move without moving other things out of the way, which is off ({kinds})";
                }
                else
                {
                    text = AlreadyTidyStart + (groups != null ? $" ({Chests(groups)})" : string.Empty);
                }
                string skipped = Skipped(inUse, noAnswer);
                return skipped.Length > 0 ? $"{text}; {skipped}" : text;
            }

            /// <summary>Nothing moved though the first plan found work: the chests it named were in use, didn't answer, were
            /// reserved, or changed (gone, set up otherwise, marked, about to be opened by another player) meanwhile.</summary>
            internal static string NoneMoved(int inUse, int noAnswer, int reserved, int changed = 0)
            {
                string skipped = Skipped(inUse, noAnswer);
                if (reserved > 0)
                {
                    skipped += (skipped.Length > 0 ? "; " : string.Empty) + $"{Count(reserved, "chest", "chests")} reserved meanwhile";
                }
                if (changed > 0)
                {
                    skipped += (skipped.Length > 0 ? "; " : string.Empty) + $"{Count(changed, "chest", "chests")} changed meanwhile";
                }
                return "Tidy: nothing moved" + (skipped.Length > 0 ? $" ({skipped})" : string.Empty);
            }

            /// <summary>"Tidied 3 chests: 14 stacks moved" (<paramref name="stacks"/> 0: the items the chest service moved,
            /// "432 items moved"), and what's left: "press again to go on" when the most stacks a tidy moves was reached, else
            /// the kinds that couldn't come together (the chests too full; or, with "Move other things out of the way" off,
            /// not without moving other things), and the kinds left in a chest whose setup doesn't take them; then what was
            /// left out.</summary>
            internal static string Tidied(int chests, int stacks, MoveResult result, bool outOfMoves, int stillSpread, int inUse, int noAnswer,
                                          bool makeRoom = true, int misplaced = 0)
            {
                string moved = stacks > 0 ? Count(stacks, "stack", "stacks") : Count(result.Moved, "item", "items");
                string text = $"Tidied {Count(chests, "chest", "chests")}: {moved} moved";
                if (outOfMoves)
                {
                    text += ", press again to go on";
                }
                else if (stillSpread > 0 || misplaced > 0)
                {
                    var left = new List<string>();
                    if (stillSpread > 0)
                    {
                        left.Add(makeRoom
                            ? $"{Count(stillSpread, "kind", "kinds")} didn't fit together"
                            : $"{Count(stillSpread, "kind", "kinds")} couldn't come together without moving other things out of the way, which is off");
                    }
                    if (misplaced > 0)
                    {
                        left.Add($"{Count(misplaced, "kind stays", "kinds stay")} in a chest whose setup doesn't take it");
                    }
                    text += "; " + string.Join("; ", left) + (makeRoom ? " (the chests are full)" : string.Empty);
                }
                string skipped = Skipped(inUse + result.ChestsInUse, noAnswer + result.ChestsNoAnswer);
                if (result.ChestsReserved > 0)
                {
                    skipped += (skipped.Length > 0 ? "; " : string.Empty) + $"{Count(result.ChestsReserved, "chest", "chests")} reserved meanwhile";
                }
                return skipped.Length > 0 ? $"{text} ({skipped})" : text;
            }

            /// <summary>The detailed lines: what went into each chest a tidy changed, "3 Leather Scraps, 2 Deer Hide → Pelt
            /// chest", up to MostChests chests and MostKinds kinds each.</summary>
            internal static string Where(IList<Container> changed, Dictionary<Container, Dictionary<string, int>> into, Func<Container, string> labelOf,
                                         Func<string, string> itemName)
            {
                var lines = new List<string>();
                int more = 0;
                foreach (Container chest in changed)
                {
                    if (!into.TryGetValue(chest, out Dictionary<string, int> gained) || gained.Count == 0)
                    {
                        continue;
                    }
                    if (lines.Count >= MostChests)
                    {
                        more++;
                        continue;
                    }
                    var names = new List<string>(gained.Keys);
                    names.Sort((a, b) => gained[a] != gained[b] ? gained[b].CompareTo(gained[a]) : string.CompareOrdinal(a, b));
                    var things = new List<string>();
                    for (int i = 0; i < names.Count && i < MostKinds; i++)
                    {
                        things.Add($"{gained[names[i]]} {itemName(names[i])}");
                    }
                    string what = string.Join(", ", things) + (names.Count > MostKinds ? $" and {names.Count - MostKinds} more" : string.Empty);
                    lines.Add($"{what} → {labelOf(chest)}");
                }
                if (more > 0)
                {
                    lines.Add($"and {Count(more, "more chest", "more chests")}");
                }
                return string.Join("\n", lines);
            }

            // "3 chests set up like this one", or for several groups "2 groups, 7 chests".
            private static string Chests(IList<Group> groups)
            {
                int chests = 0;
                foreach (Group group in groups)
                {
                    chests += group.Chests.Count;
                }
                return groups.Count == 1 ? $"{Count(chests, "chest", "chests")} set up alike" : $"{Count(groups.Count, "group", "groups")}, {Count(chests, "chest", "chests")}";
            }

            private static string Skipped(int inUse, int noAnswer)
            {
                var skipped = new List<string>();
                if (inUse > 0)
                {
                    skipped.Add($"{Count(inUse, "chest", "chests")} in use");
                }
                if (noAnswer > 0)
                {
                    skipped.Add($"{Count(noAnswer, "chest's owner", "chests' owners")} didn't answer");
                }
                return string.Join("; ", skipped);
            }

            internal static string Count(int number, string one, string many) => $"{number} {(number == 1 ? one : many)}";
        }
    }
}
