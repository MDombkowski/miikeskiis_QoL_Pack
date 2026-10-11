using System.Collections.Generic;

namespace HouseTrained
{
    /// <summary>When a creature may be fed from the hand (the setting "When").</summary>
    internal enum FeedWhen
    {
        OnlyWhenHungry,
        AnyTime,
    }

    /// <summary>What a tame that won't eat does with the food held out (the setting "When a tame won't eat").</summary>
    internal enum TameRefusal
    {
        /// <summary>He keeps it and is told why.</summary>
        KeepItAndSayWhy,
        /// <summary>The game goes on as without the mod: he eats it himself (or the game's "can't use").</summary>
        EatItYourself,
    }

    /// <summary>What using a food on a creature does.</summary>
    internal enum FeedVerdict
    {
        /// <summary>Not ours: the game goes on as without the mod (he eats the food himself, or "can't use").</summary>
        Game,
        /// <summary>The creature is fed one.</summary>
        Feed,
        /// <summary>Refused: a tame that isn't hungry while "When" says only when hungry; he keeps the food and is told.</summary>
        NotHungry,
    }

    /// <summary>What the game looking after a creature answers to a feed asked of it (sent as a number: never renumber).</summary>
    internal enum FeedAnswer
    {
        Fed = 0,
        NotHungry = 1,
        Frightened = 2,
        /// <summary>The rules said no on that game (it doesn't eat that, or it's wild and won't eat now). Number 2,
        /// "frightened", was sent by no released copy and stays unused.</summary>
        NotFed = 3,
        /// <summary>That game no longer looks after the creature.</summary>
        NotOwner = 4,
        /// <summary>House Trained is switched off on that game.</summary>
        Off = 5,
        /// <summary>The creature is gone or dead there.</summary>
        Gone = 6,
    }

    /// <summary>
    /// Feeding from the hand (card A84, his words of 2026-10-09: use a food a tame eats while looking at it to feed it,
    /// only when it's hungry by default, any time by a setting), as plain rules the harness checks.
    /// </summary>
    internal static class FeedRules
    {
        /// <summary>
        /// What using an item on a creature does. The game's own eating (MonsterAI.UpdateConsumeItem) takes only what is in
        /// the creature's m_consumeItems, only while it's hungry, and never while it's alerted with a target. A **wild**
        /// creature never takes his food when it won't eat it (frightened, or not hungry): the game goes on and he eats it
        /// himself, so eating mid-fight with a crosshair on a boar still works (code review M1). One of his tames that won't
        /// eat keeps his food and says why, or by the setting lets the game go on. A hand is trusted with a tame that's
        /// alerted (fighting beside him), not with a wild one.
        /// </summary>
        internal static FeedVerdict Decide(bool tamed, bool wildToo, bool eats, bool hungry, FeedWhen when, TameRefusal refusal, bool alerted, bool dead)
        {
            if (dead || !eats || (!tamed && !wildToo))
            {
                return FeedVerdict.Game;
            }
            bool wants = hungry || when == FeedWhen.AnyTime;
            if (!tamed)
            {
                return wants && !alerted ? FeedVerdict.Feed : FeedVerdict.Game;
            }
            if (!wants)
            {
                return refusal == TameRefusal.EatItYourself ? FeedVerdict.Game : FeedVerdict.NotHungry;
            }
            return FeedVerdict.Feed;
        }

        /// <summary>The answer the game looking after a creature sends for a verdict.</summary>
        internal static FeedAnswer AnswerFor(FeedVerdict verdict) => verdict switch
        {
            FeedVerdict.Feed => FeedAnswer.Fed,
            FeedVerdict.NotHungry => FeedAnswer.NotHungry,
            _ => FeedAnswer.NotFed,
        };

        /// <summary>Whether a creature eats an item, by its shared name, as the game's MonsterAI.CanConsume compares.</summary>
        internal static bool Eats(IEnumerable<string> eaten, string item)
        {
            if (eaten == null || string.IsNullOrEmpty(item))
            {
                return false;
            }
            foreach (string name in eaten)
            {
                if (name == item)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether one item really left his inventory (LESSONS_LEARNED, one item out of an inventory): the game's
        /// RemoveOneItem said so, or threw once it was reached (a handler of the inventory's change, another mod's, throws
        /// after the stack was changed: code review m2), and he holds one fewer of its kind now (another mod may have
        /// moved, returned or merged it meanwhile).</summary>
        internal static bool LeftHim(bool removedSaid, bool threw, int before, int after) => (removedSaid || threw) && after == before - 1;

        /// <summary>How much a meal from the hand heals: its share of full health.</summary>
        internal static float MealHeal(float maxHealth, int percent) => maxHealth <= 0f || percent <= 0 ? 0f : maxHealth * percent / 100f;
    }

    /// <summary>
    /// Feeds asked of another game, waiting for its answer (a creature another player's game looks after: only that game
    /// writes its fed time). Nothing leaves his inventory until the answer says it was fed, so a game that never answers
    /// (one without House Trained) costs nothing; meanwhile the item is promised, so the same food can't be promised twice.
    /// One feed per creature at a time. Plain values, so the harness checks it.
    /// </summary>
    internal sealed class FeedLedger<TCreature>
    {
        /// <summary>How long an answer is waited for, in seconds.</summary>
        internal const float Wait = 3f;

        /// <summary>How many games are asked in turn when the one asked no longer looks after the creature.</summary>
        internal const int MostTries = 2;

        private readonly Dictionary<int, Ask> asks = new Dictionary<int, Ask>();
        private readonly Dictionary<string, int> promised = new Dictionary<string, int>();
        private int nextId = 1;

        internal int Count => asks.Count;

        /// <summary>A feed asked, waiting for its answer.</summary>
        internal sealed class Ask
        {
            internal int Id;
            internal string Kind;
            internal string ItemShown;
            internal string CreatureShown;
            internal TCreature Creature;
            internal float Since;
            internal int Tries;
        }

        /// <summary>How many of a kind are promised to feeds still waiting.</summary>
        internal int Promised(string kind) => kind != null && promised.TryGetValue(kind, out int count) ? count : 0;

        /// <summary>Whether he holds one of a kind that isn't promised yet.</summary>
        internal bool CanPromise(string kind, int have) => have - Promised(kind) >= 1;

        /// <summary>Whether a feed of that creature is waiting already.</summary>
        internal bool Busy(TCreature creature)
        {
            foreach (Ask ask in asks.Values)
            {
                if (EqualityComparer<TCreature>.Default.Equals(ask.Creature, creature))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A feed asked: one of the kind promised. Its number, which the answer carries back.</summary>
        internal int Start(string kind, TCreature creature, string itemShown, string creatureShown, float now)
        {
            int id = nextId++;
            if (nextId == int.MaxValue)
            {
                nextId = 1;
            }
            asks[id] = new Ask { Id = id, Kind = kind, Creature = creature, ItemShown = itemShown, CreatureShown = creatureShown, Since = now, Tries = 1 };
            promised[kind] = Promised(kind) + 1;
            return id;
        }

        /// <summary>The feed waiting under that number, or null (unknown, or settled already).</summary>
        internal Ask Find(int id) => asks.TryGetValue(id, out Ask ask) ? ask : null;

        /// <summary>An answer came: the feed no longer waits, and its promise is taken back. Null for a number not waiting
        /// (an answer to a feed already settled or given up on).</summary>
        internal Ask Settle(int id)
        {
            if (!asks.TryGetValue(id, out Ask ask))
            {
                return null;
            }
            asks.Remove(id);
            int left = Promised(ask.Kind) - 1;
            if (left > 0)
            {
                promised[ask.Kind] = left;
            }
            else
            {
                promised.Remove(ask.Kind);
            }
            return ask;
        }

        /// <summary>The game asked no longer looked after the creature: asked again (of the game that does now), unless it
        /// has been tried enough. True when it may be asked again, with its clock restarted.</summary>
        internal bool Again(int id, float now)
        {
            Ask ask = Find(id);
            if (ask == null || ask.Tries >= MostTries)
            {
                return false;
            }
            ask.Tries++;
            ask.Since = now;
            return true;
        }

        /// <summary>The feeds whose answer is overdue, settled (given up on) and handed back to be told.</summary>
        internal List<Ask> Overdue(float now)
        {
            List<Ask> late = null;
            foreach (Ask ask in asks.Values)
            {
                if (now - ask.Since > Wait || now < ask.Since)
                {
                    (late ??= new List<Ask>()).Add(ask);
                }
            }
            if (late != null)
            {
                foreach (Ask ask in late)
                {
                    Settle(ask.Id);
                }
            }
            return late;
        }

        /// <summary>Everything forgotten: a new connection (answers from the last one can't come).</summary>
        internal void Clear()
        {
            asks.Clear();
            promised.Clear();
        }
    }
}
