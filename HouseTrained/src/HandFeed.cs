using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// Feeding from the hand (card A84): a food a creature eats, used on it from the hotbar while he looks at it (the game's
    /// Humanoid.UseItem asks the creature's Tameable.UseItem first, as a saddle is put on; FeedPatch), feeds it one. The
    /// creature eats it as the game's own eating does once a dropped item is taken (MonsterAI.UpdateConsumeItem): its
    /// eaten handlers run (the Tameable's own: the soothe effect when hungry, and the fed time, Tameable.ResetFeedingTimer),
    /// then its eating effects and its eating animation (ZSyncAnimation, seen by everyone), and, by the setting, a share of
    /// its health back. The rules are FeedRules'.
    /// Only the game that looks after the creature (the owner of its record) writes its fed time, as the game's own eating
    /// happens only there. So when this game owns it, the feed is done here at once: one item measured out of his
    /// inventory first (FeedRules.LeftHim), and given back if the feed then fails. When another player's game owns it,
    /// that game is asked (a routed message; one without House Trained ignores it), with this game's rules, and nothing
    /// leaves his inventory until its answer says the creature ate: an answer that never comes costs nothing, and he's told.
    /// Meanwhile the item is promised (FeedLedger), so one food can't be promised twice, and one feed per creature waits at a
    /// time. An answer coming after the wait was given up on still takes the item, if he has one, since the creature ate.
    /// Answers are always settled, also while the module is off (a feed under way must be paid for); serving another game's
    /// feed needs the module on. Nothing is written into the world but the game's own fed time and health.
    /// </summary>
    internal static class HandFeed
    {
        internal const string AskName = "modprojects.housetrained.feed";
        internal const string AnswerName = "modprojects.housetrained.fed";

        /// <summary>The protocol this copy speaks; a feed of a newer one is served as far as this one reads it.</summary>
        internal const int Protocol = 1;

        /// <summary>How long a feed given up on still takes its item when the creature ate after all, in seconds.</summary>
        internal const float LateFor = 60f;

        internal static readonly FeedLedger<ZDOID> Ledger = new FeedLedger<ZDOID>();

        /// <summary>Sends a routed message to one game; a seam only the harness changes.</summary>
        internal static Action<long, string, ZPackage> Send = (target, name, package) => ZRoutedRpc.instance.InvokeRoutedRPC(target, name, package);

        /// <summary>The game's time now; a seam only the harness changes.</summary>
        internal static Func<float> Clock = () => Time.time;

        /// <summary>Set once a use is found to be a feed (fed or refused), for the patch: even if an error stops the feed
        /// part way, the game mustn't go on to eat the food or say "can't use".</summary>
        internal static bool Claimed;

        private static readonly Dictionary<int, Late> GivenUp = new Dictionary<int, Late>();
        private static ZRoutedRpc registeredOn;
        private static bool listening;
        private static bool toldNotListening;

        /// <summary>The rules a feed follows: the feeder's settings, sent along when another game serves it (all but what a
        /// tame's refusal does, which only the feeder's game acts on: the serving game always answers why).</summary>
        internal readonly struct Rules
        {
            internal readonly FeedWhen When;
            internal readonly bool WildToo;
            internal readonly int MealHeal;
            internal readonly TameRefusal Refusal;

            internal Rules(FeedWhen when, bool wildToo, int mealHeal, TameRefusal refusal = TameRefusal.KeepItAndSayWhy)
            {
                When = when;
                WildToo = wildToo;
                MealHeal = mealHeal;
                Refusal = refusal;
            }

            internal static Rules Now() => new Rules(Settings.FeedTiming.Value, Settings.FeedWild.Value, Settings.FeedHeal.Value, Settings.FeedRefused.Value);
        }

        private readonly struct Late
        {
            internal readonly string Kind;
            internal readonly float Since;

            internal Late(string kind, float since)
            {
                Kind = kind;
                Since = since;
            }
        }

        // ------------------------------------------------------------------------------------------------ the use

        /// <summary>
        /// An item used on a creature that the game itself didn't take (not its saddle): true when it was feeding, so the
        /// game goes no further (he doesn't eat it himself, nor hear "can't use"); false leaves the game to it (a food it
        /// doesn't eat, a wild one with wild ones off, another player using it).
        /// </summary>
        internal static bool Use(Tameable tameable, Humanoid user, ItemDrop.ItemData item)
        {
            Player player = user as Player;
            if (player == null || player != Player.m_localPlayer || tameable == null || item?.m_shared == null)
            {
                return false;
            }
            Character creature = tameable.GetComponent<Character>();
            MonsterAI ai = tameable.GetComponent<MonsterAI>();
            ZNetView view = tameable.GetComponent<ZNetView>();
            if (creature == null || ai == null || view == null || !view.IsValid())
            {
                return false;
            }
            string kind = item.m_shared.m_name;
            Rules rules = Rules.Now();
            FeedVerdict verdict = Judge(tameable, creature, ai, kind, rules);
            string name = tameable.GetHoverName();
            if (verdict == FeedVerdict.Game)
            {
                return false;
            }
            Claimed = true;
            if (verdict == FeedVerdict.NotHungry)
            {
                Tell(player, $"{name} isn't hungry.");
                return true;
            }
            if (view.IsOwner())
            {
                FeedHere(player, item, tameable, creature, ai, kind, rules);
            }
            else
            {
                AskOwner(player, view, kind, name, rules);
            }
            return true;
        }

        /// <summary>What the rules say of this creature and this food now, from this game's copy of its record.</summary>
        private static FeedVerdict Judge(Tameable tameable, Character creature, MonsterAI ai, string kind, Rules rules) =>
            FeedRules.Decide(creature.IsTamed(), rules.WildToo, FeedRules.Eats(Eaten(ai), kind), tameable.IsHungry(), rules.When, rules.Refusal, ai.IsAlerted(), creature.IsDead());

        private static IEnumerable<string> Eaten(MonsterAI ai)
        {
            if (ai.m_consumeItems == null)
            {
                yield break;
            }
            foreach (ItemDrop food in ai.m_consumeItems)
            {
                if (food != null && food.m_itemData?.m_shared != null)
                {
                    yield return food.m_itemData.m_shared.m_name;
                }
            }
        }

        // The creature's own entry for that food (a prefab's ItemDrop, as the game's eating hands its handlers an ItemDrop).
        private static ItemDrop FoodOf(MonsterAI ai, string kind)
        {
            if (ai.m_consumeItems != null)
            {
                foreach (ItemDrop food in ai.m_consumeItems)
                {
                    if (food != null && food.m_itemData?.m_shared != null && food.m_itemData.m_shared.m_name == kind)
                    {
                        return food;
                    }
                }
            }
            return null;
        }

        // This game looks after the creature: one item out of his inventory, measured, then the feed; the item back if the
        // feed didn't happen.
        private static void FeedHere(Player player, ItemDrop.ItemData used, Tameable tameable, Character creature, MonsterAI ai, string kind, Rules rules)
        {
            Inventory inventory = player.GetInventory();
            ItemDrop.ItemData stack = inventory.ContainsItem(used) ? used : OneOf(inventory, kind);
            string itemShown = Shown(kind);
            if (stack == null)
            {
                Tell(player, $"You have no {itemShown} left.");
                return;
            }
            int before = inventory.CountItems(kind, -1, false);
            if (!Ledger.CanPromise(kind, before))
            {
                // Every one he holds is promised to a feed waiting on another game (code review m1).
                Tell(player, $"Your last {itemShown} is already held out to a creature.");
                return;
            }
            ItemDrop.ItemData back = stack.Clone();
            back.m_stack = 1;
            bool left = false;
            bool fed = false;
            try
            {
                // Measured even when another mod's handler of the change threw once the item was taken (code review m2):
                // then it left, the error goes on to the module's guard, and the finally below gives it back.
                left = TakeOut(inventory, stack, kind, out Exception error);
                if (error != null)
                {
                    ExceptionDispatchInfo.Capture(error).Throw();
                }
                if (!left)
                {
                    Tell(player, $"The {itemShown} couldn't be taken from your inventory.");
                    return;
                }
                Eat(tameable, creature, ai, kind, rules.MealHeal, ref fed);
            }
            finally
            {
                if (left && !fed)
                {
                    GiveBack(player, inventory, back);
                }
            }
            Tell(player, fed ? $"{tameable.GetHoverName()} ate {itemShown} from your hand." : $"{tameable.GetHoverName()} wasn't fed.");
        }

        /// <summary>
        /// The creature eats one, as the game's own eating does once the item is taken, on the game that owns it.
        /// <paramref name="fed"/> is set once its eaten handlers have run (the Tameable's own writes the fed time; it's
        /// written here if no handler did), so a failure after that never gives the food back to a fed creature (code
        /// review n3). Another mod's handler that fails is told in the log and doesn't stop the rest.
        /// </summary>
        private static void Eat(Tameable tameable, Character creature, MonsterAI ai, string kind, int mealHeal, ref bool fed)
        {
            ZDO record = creature.GetComponent<ZNetView>()?.GetZDO();
            if (record == null)
            {
                return;
            }
            long before = record.GetLong(ZDOVars.s_tameLastFeeding, 0L);
            ItemDrop food = FoodOf(ai, kind);
            Action<ItemDrop> handlers = ai.m_onConsumedItem;
            if (handlers != null)
            {
                foreach (Delegate handler in handlers.GetInvocationList())
                {
                    try
                    {
                        ((Action<ItemDrop>)handler)(food);
                    }
                    catch (Exception error)
                    {
                        Module.Warn($"House Trained fed a creature, and a handler of its eating ({handler.Method.DeclaringType?.FullName}.{handler.Method.Name}) failed: {error.Message}");
                    }
                }
            }
            fed = true;
            if (record.GetLong(ZDOVars.s_tameLastFeeding, 0L) == before && ZNet.instance != null)
            {
                record.Set(ZDOVars.s_tameLastFeeding, ZNet.instance.GetTime().Ticks);
            }
            // Fed from here on: what follows only shows it, or heals.
            try
            {
                if (creature is Humanoid humanoid)
                {
                    humanoid.m_consumeItemEffects.Create(creature.transform.position, Quaternion.identity);
                }
                creature.GetComponent<ZSyncAnimation>()?.SetTrigger("consume");
                float heal = FeedRules.MealHeal(creature.GetMaxHealth(), mealHeal);
                if (heal > 0f)
                {
                    creature.Heal(heal, true);
                }
            }
            catch (Exception error)
            {
                Module.Warn($"House Trained fed a creature, but couldn't show it eating or heal it: {error.Message}");
            }
        }

        // ---------------------------------------------------------------------------- another player's game feeds it

        private static void AskOwner(Player player, ZNetView view, string kind, string name, Rules rules)
        {
            ZDO record = view.GetZDO();
            long owner = record.GetOwner();
            if (!listening)
            {
                Tell(player, $"{name} wasn't fed: another player's game looks after it, and this copy of House Trained can't ask it.");
                return;
            }
            if (owner == 0L)
            {
                Tell(player, $"{name} wasn't fed: no game looks after it this moment. Try again.");
                return;
            }
            ZDOID creature = record.m_uid;
            if (Ledger.Busy(creature))
            {
                Tell(player, $"{name} is already being fed.");   // its feed is on its way: one at a time (code review n1)
                return;
            }
            int have = player.GetInventory().CountItems(kind, -1, false);
            string itemShown = Shown(kind);
            if (!Ledger.CanPromise(kind, have))
            {
                Tell(player, $"Your last {itemShown} is already held out to a creature.");
                return;
            }
            int ask = Ledger.Start(kind, creature, itemShown, name, Clock());
            SendAsk(owner, ask, creature, kind, rules);
        }

        private static void SendAsk(long owner, int ask, ZDOID creature, string kind, Rules rules)
        {
            var package = new ZPackage();
            package.Write(Protocol);
            package.Write(ask);
            package.Write(creature);
            package.Write(kind);
            package.Write((int)rules.When);
            package.Write(rules.WildToo);
            package.Write(rules.MealHeal);
            Send(owner, AskName, package);
        }

        // A feed asked of this game: served if this game looks after the creature, by the asker's rules; answered either way.
        private static void OnAsk(long sender, ZPackage package)
        {
            try
            {
                int protocol = package.ReadInt();
                int ask = package.ReadInt();
                ZDOID creature = package.ReadZDOID();
                string kind = package.ReadString();
                var rules = new Rules((FeedWhen)package.ReadInt(), package.ReadBool(), package.ReadInt());
                if (protocol < 1)
                {
                    return;
                }
                FeedAnswer answer = FeedAnswer.Off;
                if (Module.IsOn)
                {
                    serving = creature;
                    servingKind = kind;
                    servingRules = rules;
                    served = FeedAnswer.NotFed;
                    Module.Guard("feeding a creature another player holds food out to", serve);
                    answer = served;
                }
                Answer(sender, ask, answer);
            }
            catch (Exception error)
            {
                Module.Warn($"A friend's feed couldn't be read: {error.Message}");
            }
        }

        private static readonly Action serve = Serve;
        private static ZDOID serving;
        private static string servingKind;
        private static Rules servingRules;
        private static FeedAnswer served;

        private static void Serve()
        {
            GameObject found = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(serving) : null;
            Tameable tameable = found != null ? found.GetComponent<Tameable>() : null;
            Character creature = found != null ? found.GetComponent<Character>() : null;
            MonsterAI ai = found != null ? found.GetComponent<MonsterAI>() : null;
            ZNetView view = found != null ? found.GetComponent<ZNetView>() : null;
            if (tameable == null || creature == null || ai == null || view == null || !view.IsValid() || creature.IsDead())
            {
                served = FeedAnswer.Gone;
                return;
            }
            if (!view.IsOwner())
            {
                served = FeedAnswer.NotOwner;
                return;
            }
            FeedVerdict verdict = Judge(tameable, creature, ai, servingKind, servingRules);
            if (verdict != FeedVerdict.Feed)
            {
                served = FeedRules.AnswerFor(verdict);
                return;
            }
            bool fed = false;
            try
            {
                Eat(tameable, creature, ai, servingKind, servingRules.MealHeal, ref fed);
            }
            finally
            {
                if (fed)
                {
                    served = FeedAnswer.Fed;
                }
            }
        }

        private static void Answer(long target, int ask, FeedAnswer answer)
        {
            var package = new ZPackage();
            package.Write(Protocol);
            package.Write(ask);
            package.Write((int)answer);
            Send(target, AnswerName, package);
        }

        // The answer to a feed this game asked for: settled, also while the module is off.
        private static void OnAnswer(long sender, ZPackage package)
        {
            try
            {
                package.ReadInt();
                int ask = package.ReadInt();
                var answer = (FeedAnswer)package.ReadInt();
                Settle(ask, answer, Clock());
            }
            catch (Exception error)
            {
                Module.Warn($"A friend's answer to a feed couldn't be read: {error.Message}");
            }
        }

        private static void Settle(int ask, FeedAnswer answer, float now)
        {
            Player player = Player.m_localPlayer;
            FeedLedger<ZDOID>.Ask waiting = Ledger.Find(ask);
            if (waiting == null)
            {
                // Given up on already: the creature ate after all, so its item goes now, if he still has one.
                if (answer == FeedAnswer.Fed && GivenUp.TryGetValue(ask, out Late late))
                {
                    GivenUp.Remove(ask);
                    if (player == null || !TakeOne(player.GetInventory(), late.Kind))
                    {
                        Module.Warn($"A creature ate {late.Kind} from your hand after its answer was given up on, and it was no longer in your inventory.");
                    }
                }
                return;
            }
            // Asked again only while the module is on: switched off meanwhile, the feed just ends.
            if (answer == FeedAnswer.NotOwner && player != null && Module.IsOn && Retry(player, waiting, now))
            {
                return;
            }
            Ledger.Settle(ask);
            if (player == null)
            {
                return;
            }
            string name = waiting.CreatureShown;
            switch (answer)
            {
                case FeedAnswer.Fed:
                    if (TakeOne(player.GetInventory(), waiting.Kind))
                    {
                        Tell(player, $"{name} ate {waiting.ItemShown} from your hand.");
                    }
                    else
                    {
                        Module.Warn($"A creature ate {waiting.Kind} from your hand, and it had left your inventory meanwhile.");
                    }
                    break;
                case FeedAnswer.NotHungry:
                    Tell(player, $"{name} isn't hungry.");
                    break;
                case FeedAnswer.Off:
                    Tell(player, $"{name} wasn't fed: the game looking after it has House Trained switched off.");
                    break;
                case FeedAnswer.Gone:
                    Tell(player, $"{name} wasn't fed: it's gone.");
                    break;
                case FeedAnswer.NotOwner:
                    Tell(player, $"{name} wasn't fed: it was changing hands between games. Try again.");
                    break;
                default:
                    Tell(player, $"{name} wasn't fed.");
                    break;
            }
        }

        // The game asked no longer looks after it: fed here if this game does now, else asked of the game that does.
        private static bool Retry(Player player, FeedLedger<ZDOID>.Ask waiting, float now)
        {
            GameObject found = ZNetScene.instance != null ? ZNetScene.instance.FindInstance(waiting.Creature) : null;
            ZNetView view = found != null ? found.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid())
            {
                return false;
            }
            if (view.IsOwner())
            {
                Tameable tameable = found.GetComponent<Tameable>();
                Character creature = found.GetComponent<Character>();
                MonsterAI ai = found.GetComponent<MonsterAI>();
                ItemDrop.ItemData stack = OneOf(player.GetInventory(), waiting.Kind);
                Ledger.Settle(waiting.Id);
                if (tameable == null || creature == null || ai == null || stack == null)
                {
                    Tell(player, $"{waiting.CreatureShown} wasn't fed.");
                    return true;
                }
                Rules rules = Rules.Now();
                switch (Judge(tameable, creature, ai, waiting.Kind, rules))
                {
                    case FeedVerdict.Feed:
                        FeedHere(player, stack, tameable, creature, ai, waiting.Kind, rules);
                        break;
                    case FeedVerdict.NotHungry:
                        Tell(player, $"{waiting.CreatureShown} isn't hungry.");
                        break;
                    default:
                        Tell(player, $"{waiting.CreatureShown} wasn't fed.");
                        break;
                }
                return true;
            }
            long owner = view.GetZDO().GetOwner();
            if (owner == 0L || !Ledger.Again(waiting.Id, now))
            {
                return false;
            }
            SendAsk(owner, waiting.Id, waiting.Creature, waiting.Kind, Rules.Now());
            return true;
        }

        // ----------------------------------------------------------------------------------------------- each frame

        /// <summary>Every frame, from the module's Update (also while it is switched off): registers on each new
        /// connection, and gives up on answers that didn't come.</summary>
        internal static void Tick(float now)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc != null && rpc != registeredOn)
            {
                Register(rpc);
            }
            List<FeedLedger<ZDOID>.Ask> overdue = Ledger.Overdue(now);
            if (overdue != null)
            {
                foreach (FeedLedger<ZDOID>.Ask ask in overdue)
                {
                    GivenUp[ask.Id] = new Late(ask.Kind, now);
                    if (Player.m_localPlayer != null)
                    {
                        Tell(Player.m_localPlayer, $"{ask.CreatureShown} wasn't fed: the game looking after it didn't answer (that player may not run House Trained).");
                    }
                }
            }
            if (GivenUp.Count > 0)
            {
                Forgotten.Clear();
                foreach (KeyValuePair<int, Late> late in GivenUp)
                {
                    if (now - late.Value.Since > LateFor || now < late.Value.Since)
                    {
                        Forgotten.Add(late.Key);
                    }
                }
                foreach (int id in Forgotten)
                {
                    GivenUp.Remove(id);
                }
            }
        }

        private static readonly List<int> Forgotten = new List<int>();

        // A new connection: its message handlers, and the feeds of the last one forgotten (their answers can't come).
        private static void Register(ZRoutedRpc rpc)
        {
            registeredOn = rpc;
            Ledger.Clear();
            GivenUp.Clear();
            listening = false;
            try
            {
                rpc.Register<ZPackage>(AskName, OnAsk);
                rpc.Register<ZPackage>(AnswerName, OnAnswer);
                listening = true;
            }
            catch (ArgumentException)
            {
                // Another plugin carrying House Trained registered them first: it feeds for this game.
                if (!toldNotListening)
                {
                    toldNotListening = true;
                    Module.Warn("Another copy of House Trained serves feeding from the hand; this copy feeds only creatures this game looks after.");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ helpers

        /// <summary>One stack of a kind in his inventory, never one he wears or holds.</summary>
        private static ItemDrop.ItemData OneOf(Inventory inventory, string kind)
        {
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_shared != null && item.m_shared.m_name == kind && !item.m_equipped)
                {
                    return item;
                }
            }
            return null;
        }

        /// <summary>One of a kind out of his inventory, measured (FeedRules.LeftHim): whether it left.</summary>
        private static bool TakeOne(Inventory inventory, string kind)
        {
            ItemDrop.ItemData stack = inventory != null ? OneOf(inventory, kind) : null;
            if (stack == null)
            {
                return false;
            }
            bool left = TakeOut(inventory, stack, kind, out Exception error);
            if (error != null)
            {
                Module.Warn($"Another mod's handler failed as House Trained took {kind} from your inventory: {error.Message}");
            }
            return left;
        }

        /// <summary>
        /// One item out of a stack of his, measured (LESSONS_LEARNED, one item out of an inventory; code review m2): whether
        /// it really left him. A handler of the inventory's change (another mod's) that throws once the item was taken is
        /// caught and handed back in <paramref name="error"/>, so the caller can give the item back before passing it on.
        /// </summary>
        internal static bool TakeOut(Inventory inventory, ItemDrop.ItemData stack, string kind, out Exception error)
        {
            error = null;
            int before = inventory.CountItems(kind, -1, false);
            bool said = false;
            bool threw = false;
            try
            {
                said = inventory.RemoveOneItem(stack);
            }
            catch (Exception caught)
            {
                threw = true;
                error = caught;
            }
            return FeedRules.LeftHim(said, threw, before, inventory.CountItems(kind, -1, false));
        }

        // An item taken for a feed that didn't happen: back into his inventory, or at his feet if it no longer fits.
        private static void GiveBack(Player player, Inventory inventory, ItemDrop.ItemData item)
        {
            if (!inventory.AddItem(item))
            {
                Transform at = player.transform;
                ItemDrop.DropItem(item, 1, at.position + at.forward + Vector3.up, at.rotation);
            }
        }

        private static string Shown(string kind) => Localization.instance != null ? Localization.instance.Localize(kind) : kind;

        private static void Tell(Player player, string text) => QoLMods.Kit.Say.Center(text);   // fitted to the screen (card B14)
    }
}
