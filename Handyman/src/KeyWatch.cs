namespace Handyman
{
    /// <summary>One frame's view of the key and of what the settings say, gathered from the game by Hammer (or made up by
    /// a test). Nothing in it is read from the game by KeyWatch itself, so its rules can be checked outside the game.</summary>
    internal struct KeyFrame
    {
        /// <summary>The mod may act on the key now: "R picks Repair" is on, a build tool it works with is in hand, and the
        /// game is taking input (no chat, inventory, map or menu in the way, and, for the Hide key, the game reads the
        /// keyboard's Hide in this input layout).</summary>
        internal bool Ready;

        /// <summary>The tool in hand. A press whose tool is no longer in hand is dropped.</summary>
        internal object Tool;

        /// <summary>The key is the game's Hide key (else the mod's own key, which the game doesn't read).</summary>
        internal bool UsesHideKey;

        /// <summary>The key went down in this frame.</summary>
        internal bool Down;

        /// <summary>The key is still held down (read for the game's Hide key only).</summary>
        internal bool Held;

        /// <summary>A Shift key is held.</summary>
        internal bool Shift;

        /// <summary>The build menu is open.</summary>
        internal bool MenuOpen;

        /// <summary>Seconds, on a clock that keeps running while the game is paused.</summary>
        internal float Now;

        internal PutAwayChoice PutAway;

        internal float HoldSeconds;

        internal MenuChoice Menu;
    }

    internal enum KeyEvent
    {
        None,

        /// <summary>A tap: the key was let go before the hold time, or the settings act at the press.</summary>
        Tap,

        /// <summary>The key has been held for the hold time: put the tool away.</summary>
        Hold,
    }

    /// <summary>What a frame's step decided.</summary>
    internal struct KeyStep
    {
        /// <summary>Cancel the game's own Hide press in this frame (ZInput.ResetButtonStatus), so the game doesn't put
        /// the tool away.</summary>
        internal bool TakeGamePress;

        internal KeyEvent Event;

        /// <summary>Whether the build menu was open when the key went down (a tap then picks Repair and closes it).</summary>
        internal bool MenuOpenAtPress;
    }

    /// <summary>
    /// Turns the key's presses into taps and holds, and says when the game's own press must be cancelled. Card A44's
    /// rules: with the game's Hide key and "Putting the hammer away" at "hold the key", the press is taken from the game
    /// at once and becomes a tap if the key is let go before the hold time, or a hold (the tool put away, as the game
    /// would have) once it is held that long. With "Shift and the key", Shift held leaves the press to the game, and
    /// the key alone is a tap at once. With "hotbar key only", every press is a tap at once. A key of the mod's own is
    /// never the game's, so nothing is cancelled and each press is a tap at once.
    /// </summary>
    internal sealed class KeyWatch
    {
        private bool waiting;
        private float pressedAt;
        private object toolAtPress;
        private bool menuAtPress;

        /// <summary>A press of the game's Hide key has been taken and the mod waits to see if it's a tap or a hold.</summary>
        internal bool Waiting => waiting;

        /// <summary>Forget a press being waited on (the mod switched off, or the tool gone).</summary>
        internal void Reset()
        {
            waiting = false;
            toolAtPress = null;
        }

        internal KeyStep Step(in KeyFrame frame)
        {
            KeyStep step = default;
            if (waiting)
            {
                if (!frame.Ready || !frame.UsesHideKey || !ReferenceEquals(frame.Tool, toolAtPress))
                {
                    // The tool left his hand, or a window took the keys (or the game lost the focus), or the mod was
                    // switched off, or "Key" was changed to a key of its own during the press: the press is dropped.
                    // A new press in this frame is then the game's, as it would be without the mod.
                    Reset();
                    return step;
                }
                if (frame.Down)
                {
                    // Let go and pressed again between two frames (a quick double tap in a slow frame): the first
                    // press was a tap, and the new one follows the usual rules. It is waited on as usual; were it a
                    // tap at once (only if a setting changed during the first press), that second tap is dropped,
                    // since one step carries one event.
                    bool firstMenu = menuAtPress;
                    Reset();
                    step = Fresh(frame);
                    step.Event = KeyEvent.Tap;
                    step.MenuOpenAtPress = firstMenu;
                    return step;
                }
                if (!frame.Held)
                {
                    step.Event = KeyEvent.Tap;
                    step.MenuOpenAtPress = menuAtPress;
                    Reset();
                    return step;
                }
                if (frame.Now - pressedAt >= frame.HoldSeconds)
                {
                    step.Event = KeyEvent.Hold;
                    step.MenuOpenAtPress = menuAtPress;
                    Reset();
                }
                return step;
            }
            return Fresh(frame);
        }

        /// <summary>A frame with no press being waited on: a new press, by the rules above.</summary>
        private KeyStep Fresh(in KeyFrame frame)
        {
            KeyStep step = default;
            if (!frame.Down || !frame.Ready)
            {
                return step;
            }
            if (frame.MenuOpen && frame.Menu == MenuChoice.AsInTheGame)
            {
                // The game's Hide key puts the tool away as in the game; the mod's own key means nothing to the game.
                return step;
            }
            if (frame.UsesHideKey)
            {
                if (frame.MenuOpen && frame.Menu == MenuChoice.Nothing)
                {
                    step.TakeGamePress = true;
                    return step;   // taken, and nothing done, even with Shift held
                }
                if (frame.PutAway == PutAwayChoice.ShiftAndTheKey && frame.Shift)
                {
                    return step;   // Shift and the key: the game puts the tool away
                }
                step.TakeGamePress = true;
                if (frame.PutAway == PutAwayChoice.HoldTheKey)
                {
                    Wait(frame);
                    return step;
                }
            }
            else if (frame.MenuOpen && frame.Menu == MenuChoice.Nothing)
            {
                return step;
            }
            step.Event = KeyEvent.Tap;
            step.MenuOpenAtPress = frame.MenuOpen;
            return step;
        }

        private void Wait(in KeyFrame frame)
        {
            waiting = true;
            pressedAt = frame.Now;
            toolAtPress = frame.Tool;
            menuAtPress = frame.MenuOpen;
        }
    }
}
