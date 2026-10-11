namespace TopperUpper.Loose
{
    /// <summary>Why a loose item is left where it lies (None: it may be put away).</summary>
    internal enum Left
    {
        None,

        /// <summary>Another game owns it, or its record is gone: never asked for (card A77, decision 7).</summary>
        NotOurs,

        /// <summary>Made into a piece (food set out on a table): it isn't loose.</summary>
        Piece,

        /// <summary>A quest item.</summary>
        QuestItem,

        /// <summary>A player dropped it on purpose (the game marks those not to be picked up by itself), and "Also items
        /// dropped on purpose" is off.</summary>
        DroppedOnPurpose,

        /// <summary>It found no chest a moment ago: looked at again a little later.</summary>
        Waiting,

        /// <summary>Younger than the wait: let it land, and the game's own pickup go first.</summary>
        TooNew,

        /// <summary>Beyond the chest range of him.</summary>
        FarFromYou,

        /// <summary>Near a player, who may pick it up by hand.</summary>
        NearPlayer,

        /// <summary>A kind on the never list.</summary>
        NeverList,

        /// <summary>A lone egg that could hatch where it lies.</summary>
        Egg,

        /// <summary>A fish still swimming.</summary>
        FishInWater,

        /// <summary>Falling, rolling or carried (or with no body of its own).</summary>
        Moving,

        /// <summary>Nothing under it and not afloat: hanging in mid-air.</summary>
        NotResting,

        /// <summary>Stuck in tar (the game won't let a hand take it either).</summary>
        InTar,

        /// <summary>At the output of a station whose products Topper-Upper puts away (or leaves, by his settings).</summary>
        AtStation,

        /// <summary>Food near a tame (or tameable) animal that eats it.</summary>
        Fodder,
    }

    /// <summary>What the game says about one item on the ground, read in the order the rules ask (cheap first).</summary>
    internal struct ItemFacts
    {
        internal bool Ours;
        internal bool IsPiece;
        internal bool QuestItem;
        internal bool AutoPickup;
        internal bool Waiting;

        /// <summary>Seconds since it was made (its record's spawn time, in the world's time).</summary>
        internal double Age;

        internal float FromYou;

        /// <summary>The nearest player's distance, in metres (a very large number for none).</summary>
        internal float NearestPlayer;

        internal bool OnNeverList;
        internal bool LoneEgg;
        internal bool FishInWater;

        /// <summary>It has a body of its own that the physics moves (not kinematic).</summary>
        internal bool Body;

        internal float Speed;

        /// <summary>Something under it besides itself.</summary>
        internal bool Resting;

        /// <summary>Floating in water (the game's Floating has a water level for it, and it isn't in tar): it lies still
        /// there as much as on the ground, though nothing is under it.</summary>
        internal bool Afloat;

        internal bool InTar;
        internal bool AtHandledOutput;
        internal bool NearEater;
    }

    /// <summary>The settings' word on which items may be put away, read each sweep.</summary>
    internal sealed class PickupRules
    {
        /// <summary>The fastest an item may move and still count as lying still, in metres a second.</summary>
        internal const float MostSpeed = 0.5f;

        internal double Wait = 3.0;
        internal float PlayerDistance = 4f;

        /// <summary>The chest range of him: items farther away are left.</summary>
        internal float Reach = 150f;

        internal bool TakeDropped;
        internal bool Fodder = true;
        internal bool Eggs = true;
    }

    /// <summary>
    /// Which loose items may go into chests (card A77's "Loose items"; the freeze research's section 8): only what this game
    /// owns, never an ownership request; a while after it was made, so it has landed and the game's own pickup went first;
    /// lying still on something or afloat; away from players; not dropped on purpose; not on the never list; not a lone egg that
    /// could hatch or a fish still swimming; not at a station's output Topper-Upper's products handle; and not food near a
    /// tame that eats it. Early asks only what's cheap to read; Why asks everything, Early's questions first and in the
    /// same order, so the game reads a body and searches around an item only once the cheap questions have passed.
    /// </summary>
    internal static class Qualify
    {
        /// <summary>The cheap questions alone: None if they all pass.</summary>
        internal static Left Early(in ItemFacts item, PickupRules rules)
        {
            if (!item.Ours)
            {
                return Left.NotOurs;
            }
            if (item.IsPiece)
            {
                return Left.Piece;
            }
            if (item.QuestItem)
            {
                return Left.QuestItem;
            }
            if (!item.AutoPickup && !rules.TakeDropped)
            {
                return Left.DroppedOnPurpose;
            }
            if (item.Waiting)
            {
                return Left.Waiting;
            }
            if (!(item.Age >= rules.Wait))
            {
                return Left.TooNew;
            }
            if (!(item.FromYou <= rules.Reach))
            {
                return Left.FarFromYou;
            }
            if (item.NearestPlayer < rules.PlayerDistance)
            {
                return Left.NearPlayer;
            }
            if (item.OnNeverList)
            {
                return Left.NeverList;
            }
            if (rules.Eggs && item.LoneEgg)
            {
                return Left.Egg;
            }
            if (item.FishInWater)
            {
                return Left.FishInWater;
            }
            return Left.None;
        }

        /// <summary>Every question: None if the item may be put away.</summary>
        internal static Left Why(in ItemFacts item, PickupRules rules)
        {
            Left early = Early(item, rules);
            if (early != Left.None)
            {
                return early;
            }
            if (!item.Body || !(item.Speed <= PickupRules.MostSpeed))
            {
                return Left.Moving;
            }
            if (!item.Resting && !item.Afloat)
            {
                return Left.NotResting;
            }
            if (item.InTar)
            {
                return Left.InTar;
            }
            if (item.AtHandledOutput)
            {
                return Left.AtStation;
            }
            if (rules.Fodder && item.NearEater)
            {
                return Left.Fodder;
            }
            return Left.None;
        }
    }
}
