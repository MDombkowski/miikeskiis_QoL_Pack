using System;

namespace TopperUpper.Logic
{
    /// <summary>What the key does at a light or station (the research's 4.2): fed and left alone, or those two and "as its
    /// kind says".</summary>
    internal enum KeySteps
    {
        /// <summary>Switches between fed and left alone.</summary>
        FedAndLeftAlone,

        /// <summary>Goes round: as its kind says, fed, left alone.</summary>
        FedLeftAloneAndAsItsKind,
    }

    /// <summary>
    /// A light's or station's own mark (card A77, "leave one alone"), an int in its network record, written only by its
    /// owner: 0 as its kind says (its kind's switch, the world-fire and candle rules), 1 fed whatever those say, 2 left
    /// alone. Plain values, for the harness.
    /// </summary>
    internal static class FeedMark
    {
        internal const int AsKind = 0;
        internal const int Fed = 1;
        internal const int LeftAlone = 2;

        /// <summary>Whether it's fed, by its mark and what its kind says. A mark it doesn't know counts as AsKind.</summary>
        internal static bool IsFed(int mark, bool kindSaysFed) => mark == Fed || (mark != LeftAlone && kindSaysFed);

        /// <summary>The mark one press of the key gives. Fed and left alone: fed becomes left alone, and left alone becomes
        /// fed, as its kind if its kind is fed, else fed by its own mark. Three steps: as its kind, fed, left alone, round
        /// again.</summary>
        internal static int Next(int mark, bool kindSaysFed, KeySteps steps)
        {
            if (steps == KeySteps.FedLeftAloneAndAsItsKind)
            {
                return mark == AsKind ? Fed : mark == Fed ? LeftAlone : AsKind;
            }
            if (IsFed(mark, kindSaysFed))
            {
                return LeftAlone;
            }
            return kindSaysFed ? AsKind : Fed;
        }

        /// <summary>A mark read from a record, kept to the three it knows.</summary>
        internal static int Clean(int mark) => mark == Fed || mark == LeftAlone ? mark : AsKind;
    }

    /// <summary>What becomes of products with no linked output chest.</summary>
    internal enum NoOutputChoice
    {
        PutAwayLikeTheQuickStack,
        LeaveWhereTheyFall,
    }

    /// <summary>What becomes of products the linked output chests have no room for.</summary>
    internal enum FullOutputChoice
    {
        PutAwayLikeTheQuickStack,
        LeaveWhereTheyFall,
        StopFeedingTheStation,
    }

    /// <summary>Where a station's products go (the research's 4.3), from counts alone.</summary>
    internal struct OutputPlan
    {
        internal int IntoLinked;
        internal int PutAway;
        internal int Leave;

        /// <summary>The station's linked outputs are full and it stops being fed until they have room.</summary>
        internal bool Pause;
    }

    internal static class OutputChoice
    {
        /// <summary>
        /// Where <paramref name="amount"/> products go: into the linked output chests as far as they have room, the rest as
        /// the setting for a full output says; with none linked, as the setting for no linked output says.
        /// </summary>
        internal static OutputPlan Decide(int amount, bool hasLinkedOutput, int linkedRoom, NoOutputChoice none, FullOutputChoice full)
        {
            var plan = new OutputPlan();
            amount = Math.Max(0, amount);
            if (!hasLinkedOutput)
            {
                if (none == NoOutputChoice.PutAwayLikeTheQuickStack)
                {
                    plan.PutAway = amount;
                }
                else
                {
                    plan.Leave = amount;
                }
                return plan;
            }
            plan.IntoLinked = Math.Min(amount, Math.Max(0, linkedRoom));
            int rest = amount - plan.IntoLinked;
            if (rest <= 0)
            {
                return plan;
            }
            switch (full)
            {
                case FullOutputChoice.PutAwayLikeTheQuickStack:
                    plan.PutAway = rest;
                    break;
                case FullOutputChoice.StopFeedingTheStation:
                    plan.Leave = rest;
                    plan.Pause = true;
                    break;
                default:
                    plan.Leave = rest;
                    break;
            }
            return plan;
        }

        /// <summary>Whether a station whose linked outputs have <paramref name="linkedRoom"/> room for a product stops being
        /// fed that input: only with "stop feeding the station", only with outputs linked, and only while they're full.</summary>
        internal static bool Paused(bool hasLinkedOutput, int linkedRoom, FullOutputChoice full) =>
            hasLinkedOutput && full == FullOutputChoice.StopFeedingTheStation && linkedRoom <= 0;
    }

    /// <summary>
    /// The bookkeeping of a measured put from an item on the ground into a chest (GroundPut), from counts alone: what may
    /// be offered, and once the copy went in, how much really leaves the ground and how much must come back out of the
    /// chest. Plain values, for the harness.
    /// </summary>
    internal static class PutBook
    {
        /// <summary>How much to offer: the most asked, what lies on the ground, and the chest's room for it.</summary>
        internal static int Part(int asked, int onTheGround, int room) => Math.Max(0, Math.Min(asked, Math.Min(onTheGround, room)));

        /// <summary>
        /// After the copy went in: <paramref name="arrived"/> is what the chest gained (measured), and the item on the
        /// ground holds <paramref name="onTheGroundNow"/> and is still this game's (<paramref name="stillOurs"/>, false also
        /// when it's gone). Exactly what arrived leaves the ground, as far as the ground still holds it; whatever the ground
        /// no longer holds comes back out of the chest; the ground keeps the rest, and the item is removed when nothing is
        /// left.
        /// </summary>
        internal static Settled Settle(int arrived, int onTheGroundNow, bool stillOurs)
        {
            arrived = Math.Max(0, arrived);
            int leaves = stillOurs ? Math.Min(Math.Max(0, onTheGroundNow), arrived) : 0;
            return new Settled
            {
                Leaves = leaves,
                TakeBack = arrived - leaves,
                Left = stillOurs ? Math.Max(0, onTheGroundNow - leaves) : 0,
                Remove = stillOurs && onTheGroundNow - leaves <= 0 && leaves > 0,
            };
        }

        internal struct Settled
        {
            /// <summary>How many leave the ground: what the put really moved.</summary>
            internal int Leaves;

            /// <summary>How many come back out of the chest: they arrived, but the ground no longer held them.</summary>
            internal int TakeBack;

            /// <summary>What the item on the ground holds after.</summary>
            internal int Left;

            /// <summary>The item on the ground is removed: all of it went.</summary>
            internal bool Remove;
        }
    }
}
