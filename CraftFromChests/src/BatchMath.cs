using System;

namespace CraftFromChests
{
    /// <summary>
    /// The arithmetic of crafting an amount (card B16), with no game or Unity in it, so a scratch harness can run it as it
    /// is: how big the next craft of a run is, what an amount comes to in the game's x5 crafts and single ones, how the
    /// amount steps, and how one x5 craft's skill raise is split into single steps.
    /// </summary>
    internal static class BatchMath
    {
        /// <summary>The size of a run's next craft: the game's batch (its x5 craft, <paramref name="batchSize"/> crafts at
        /// once) while at least a whole batch is left and one can be made now (the materials and the inventory room for it),
        /// else a single craft; 0 when nothing is left.</summary>
        internal static int NextSize(int remaining, int batchSize, bool batchesOn, bool batchPossible)
        {
            if (remaining <= 0)
            {
                return 0;
            }
            return batchesOn && batchSize > 1 && remaining >= batchSize && batchPossible ? batchSize : 1;
        }

        /// <summary>What an amount comes to when every batch can be made: 41 is eight x5 crafts and one single.</summary>
        internal static (int Batches, int Singles) Split(int amount, int batchSize)
        {
            if (amount <= 0)
            {
                return (0, 0);
            }
            return batchSize > 1 ? (amount / batchSize, amount % batchSize) : (0, amount);
        }

        /// <summary>The amount after one step of the + or - (or the wheel): by one, or by five with the step-by-five key
        /// held; with <paramref name="snap"/>, a step of five lands on a multiple of five (41, 45, 50; down 41, 40, 35), so
        /// a run is whole batches. Always 1 to <paramref name="most"/>.</summary>
        internal static int Step(int amount, int direction, bool five, bool snap, int most)
        {
            most = Math.Max(1, most);
            if (direction == 0)
            {
                return Clamp(amount, 1, most);
            }
            int next;
            if (five && snap)
            {
                next = direction > 0 ? (amount / 5 + 1) * 5 : (amount - 1) / 5 * 5;
            }
            else
            {
                next = amount + Math.Sign(direction) * (five ? 5 : 1);
            }
            return Clamp(next, 1, most);
        }

        /// <summary>The amount the "most you can make" click sets: what the materials allow, within 1 and the largest
        /// amount.</summary>
        internal static int Most(int makeable, int most) => Clamp(makeable, 1, Math.Max(1, most));

        /// <summary>
        /// How one raise of a crafting skill is made instead (the skill kept whole): the game raises the skill once by
        /// <paramref name="batchSize"/> after an x5 craft, and a level-up throws away whatever the raise had left over, so
        /// the same raise is made as <paramref name="batchSize"/> raises of a fifth each, as five single crafts raise it.
        /// Returns how many raises, each of <paramref name="each"/>; 1 (the raise as it was) when there's nothing to split.
        /// </summary>
        internal static int SplitRaise(float value, int batchSize, out float each)
        {
            if (batchSize <= 1 || value <= 0f)
            {
                each = value;
                return 1;
            }
            each = value / batchSize;
            return batchSize;
        }

        /// <summary>The most a batch's bonus can add to what it makes: the game adds a growing bonus at each lucky roll
        /// (+1, then +2 …, each times its bonus amount), so five lucky rolls add 1+2+3+4+5 = 15.</summary>
        internal static int MostBonus(int batchSize, int bonusAmount) =>
            batchSize <= 0 || bonusAmount <= 0 ? 0 : bonusAmount * batchSize * (batchSize + 1) / 2;

        private static int Clamp(int value, int low, int high) => value < low ? low : value > high ? high : value;
    }

    /// <summary>One run of crafts as it goes: how many are left and how many were made (card B16). The game's crafting
    /// and the decisions are Batches'; this only counts, so the harness can count with it too.</summary>
    internal sealed class RunTally
    {
        internal RunTally(int amount)
        {
            Start = amount;
            Remaining = amount;
        }

        /// <summary>The amount the run began with.</summary>
        internal int Start { get; }

        /// <summary>Crafts still to make.</summary>
        internal int Remaining { get; private set; }

        /// <summary>Crafts made so far (an x5 craft counts five).</summary>
        internal int Done { get; private set; }

        /// <summary>A craft of <paramref name="size"/> was made; true while more are left.</summary>
        internal bool Made(int size)
        {
            if (size > 0)
            {
                Done += size;
                Remaining = Math.Max(0, Remaining - size);
            }
            return Remaining > 0;
        }
    }
}
