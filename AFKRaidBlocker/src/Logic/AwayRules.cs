using System;

namespace AFKRaidBlocker
{
    /// <summary>What the player's own game saw in one frame. Plain values, so the rules can be tested without the game.</summary>
    internal struct Sample
    {
        /// <summary>Real seconds since the game started (they keep counting while the game is paused).</summary>
        internal double Now;

        /// <summary>His own movement controls moved his character this frame (walking, running, autorun, swimming).</summary>
        internal bool Moved;

        /// <summary>A key, a mouse button, the mouse itself, the wheel or a gamepad was used this frame, while the game
        /// had the focus.</summary>
        internal bool Input;

        /// <summary>What he is sitting or lying on: by an emote, in a chair, in a bed, or nothing.</summary>
        internal Seat Seat;
    }

    /// <summary>The settings the rules read, copied from the live settings each frame.</summary>
    internal struct Rules
    {
        /// <summary>Minutes without being at the keyboard before he counts as away; 0 for never.</summary>
        internal int Minutes;

        internal InputCounts Input;

        internal bool Emotes;

        internal bool Chairs;

        internal bool Beds;

        internal SeatTiming Timing;

        internal double Seconds => Minutes * 60.0;
    }

    /// <summary>
    /// The away rules (card A98). A player is away when, for every rule that's on: he hasn't been at the keyboard for
    /// the minutes set (movement, and with the setting keys and the mouse too); or he sits or lies down by a counted
    /// emote, in a chair or in a bed (at once, or after the same minutes there). He is back at the first movement or
    /// input that counts, and on standing up. Sitting or lying holds him away whatever he presses, until he stands up:
    /// the game itself ends an emote, a chair or a bed at his first movement.
    /// </summary>
    internal sealed class AwayRules
    {
        private double lastThere;
        private double seatedSince;
        private Seat seatedOn = Seat.None;
        private bool started;

        internal bool Away { get; private set; }

        internal Why Reason { get; private set; } = Why.Present;

        /// <summary>Starts again: present, the minutes counting from now. For a new character, a new world, or the mod
        /// switched on again.</summary>
        internal void Reset(double now)
        {
            lastThere = now;
            seatedOn = Seat.None;
            Away = false;
            Reason = Why.Present;
            started = true;
        }

        /// <summary>Takes one frame. True when Away changed in it.</summary>
        internal bool Step(Sample sample, Rules rules)
        {
            if (!started)
            {
                Reset(sample.Now);
            }

            bool there = sample.Moved || (rules.Input == InputCounts.MovementKeysAndMouse && sample.Input);
            if (there)
            {
                lastThere = sample.Now;
            }

            Seat counted = Counts(sample.Seat, rules) ? sample.Seat : Seat.None;
            if (counted != seatedOn)
            {
                seatedSince = sample.Now;   // a new seat (or standing up) starts its own minutes
                seatedOn = counted;
            }

            bool byTime = rules.Minutes > 0 && sample.Now - lastThere >= rules.Seconds;
            // With "never" (0) for the minutes, "after the same minutes" waits 0 seconds: a seat then counts at once.
            bool bySeat = counted != Seat.None
                && (rules.Timing == SeatTiming.AtOnce || sample.Now - seatedSince >= rules.Seconds);

            bool away = byTime || bySeat;
            Why reason = !away ? Why.Present
                : bySeat ? WhyOf(counted)
                : Why.NoMovement;

            bool changed = away != Away;
            Away = away;
            Reason = reason;
            return changed;
        }

        /// <summary>Whether a seat counts under the rules: each kind has its own switch.</summary>
        internal static bool Counts(Seat seat, Rules rules)
        {
            switch (seat)
            {
                case Seat.Emote:
                    return rules.Emotes;
                case Seat.Chair:
                    return rules.Chairs;
                case Seat.Bed:
                    return rules.Beds;
                default:
                    return false;
            }
        }

        private static Why WhyOf(Seat seat)
        {
            switch (seat)
            {
                case Seat.Emote:
                    return Why.Emote;
                case Seat.Chair:
                    return Why.Chair;
                case Seat.Bed:
                    return Why.Bed;
                default:
                    throw new ArgumentOutOfRangeException(nameof(seat));
            }
        }
    }
}
