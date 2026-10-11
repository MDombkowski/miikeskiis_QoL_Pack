namespace PowerWheel
{
    /// <summary>Where a press came from.</summary>
    internal enum KeySource
    {
        None,

        /// <summary>The game's "Forsaken power" button (keyboard "GP": F, or his rebind). The game reads its press too,
        /// so the mod takes that press away from it.</summary>
        GameKey,

        /// <summary>The mod's own key ("Own key"), which the game doesn't read.</summary>
        OwnKey,

        /// <summary>The gamepad's power button ("JoyGP": D-pad down, classic layout). The game reads its press too.</summary>
        Gamepad,
    }

    /// <summary>One frame's view of one source, gathered by Keys (or made up by a test).</summary>
    internal struct SourceFrame
    {
        /// <summary>It went down in this frame.</summary>
        internal bool Down;

        /// <summary>It is held down now (read from its own input, not the game's button: the game's reads nothing more of a
        /// press once it was taken).</summary>
        internal bool Held;

        /// <summary>The mod may act on it now, and keep waiting on its press: its setting uses it, the game takes input (no
        /// wheel or build menu open), the game has focus, no build tool, hoe or cultivator is in hand, and the wheel can
        /// open.</summary>
        internal bool Ready;

        /// <summary>What the game asks only at the press itself: for the gamepad, the classic layout, the gamepad in use
        /// and its Alt keys not held (Player.Update). Raising the shield (LT, also the Alt key) or touching the mouse
        /// later in the press doesn't undo a press the game would already have acted on. Always true for the keyboard.</summary>
        internal bool PressReady;

        /// <summary>The wheel may open now: for the gamepad, its Alt keys not held (LT is also the wheel's "back", whose
        /// release would close it at once), so the wheel waits until LT is let go. Always true for the keyboard.</summary>
        internal bool OpenReady;
    }

    /// <summary>One frame's view of the keys and the wheel.</summary>
    internal struct KeyFrame
    {
        internal SourceFrame Game;
        internal SourceFrame Own;
        internal SourceFrame Pad;

        /// <summary>This mod's wheel shows (open, or closing).</summary>
        internal bool WheelOpen;

        /// <summary>Seconds, on a clock that keeps running while the game is paused.</summary>
        internal float Now;

        internal float HoldSeconds;

        internal SourceFrame Of(KeySource source) =>
            source == KeySource.GameKey ? Game : source == KeySource.OwnKey ? Own : source == KeySource.Gamepad ? Pad : default;
    }

    internal enum KeyEvent
    {
        None,

        /// <summary>Let go before the hold time: use the power, as the game would have at the press (if "A tap of the key"
        /// says so).</summary>
        Tap,

        /// <summary>Held for the hold time: open the wheel.</summary>
        Open,
    }

    /// <summary>What a frame's step made of the key, for the log ("Write what it sees of the key to the log").</summary>
    internal enum KeyNote
    {
        /// <summary>Nothing new.</summary>
        None,

        /// <summary>A press is now waited on, to tell a tap from a hold.</summary>
        Waiting,

        /// <summary>Let go before the hold time: a tap.</summary>
        Tap,

        /// <summary>Let go, but found only after the game was away (no frame for longer than a hold): no tap.</summary>
        NoTapAway,

        /// <summary>Let go after a hold that waited for the gamepad's LT: no tap.</summary>
        NoTapAfterWaiting,

        /// <summary>The press waited on was dropped, with no tap: the mod may no longer act on it.</summary>
        Dropped,

        /// <summary>Held for the hold time, but the wheel waits until the gamepad's LT is let go.</summary>
        WaitsForLT,

        /// <summary>Held for the hold time: the wheel opens.</summary>
        Open,

        /// <summary>The wheel closed with the key still held: nothing more until it is let go.</summary>
        ClosedKeyHeld,
    }

    /// <summary>What a frame's step decided.</summary>
    internal struct KeyStep
    {
        /// <summary>The source whose game press must be taken in this frame (ZInput.ResetButtonStatus), so the game
        /// doesn't use the power at the press; None for none.</summary>
        internal KeySource Take;

        internal KeyEvent Event;

        /// <summary>The source of the press the event is about.</summary>
        internal KeySource Source;
    }

    /// <summary>
    /// Turns the key's presses into taps and holds (card A75; Handyman's KeyWatch is the model). A press the mod may act
    /// on is taken from the game at once and waited on: let go before the hold time, it is a tap; held that long, the
    /// wheel opens, and its own release (the wheel's "let go") is the wheel's business. While the wheel shows, and after
    /// it closed with the key still held, nothing more happens until the key is let go. A press it may not act on (a
    /// build tool in hand, a window open, the game without focus …) is left to the game, untouched. A press being waited
    /// on is dropped, with no tap, if the game stops taking input or loses the focus, a build tool comes into his hand, or
    /// the mod is switched off; and a release found only after a pause longer than a hold (the game not running while
    /// another window had the focus, which also resets the keyboard) is no tap either. What the game asks only at the
    /// press (the gamepad's layout, its being in use, its Alt keys) is asked only at the press; the wheel waits while the
    /// gamepad's Alt key (LT) is held, and a hold that waited so is no tap when let go.
    /// </summary>
    internal sealed class KeyWatch
    {
        private enum State
        {
            Idle,
            Waiting,
            Wheel,
            Spent,
        }

        /// <summary>The shortest pause between two frames that counts as the game having been away.</summary>
        internal const float AwaySeconds = 0.5f;

        private State state;
        private KeySource source;
        private float pressedAt;
        private float lastSeen;

        // The hold time passed while the wheel couldn't open (the gamepad's LT held): a hold, so its release is no tap.
        private bool overdue;

        /// <summary>Nothing is going on: no press waited on, no wheel, no press left to let go.</summary>
        internal bool Idle => state == State.Idle;

        /// <summary>The source of the press that opened the wheel now showing, or None.</summary>
        internal KeySource WheelSource => state == State.Wheel ? source : KeySource.None;

        /// <summary>A press is waited on, to tell a tap from a hold.</summary>
        internal bool IsWaiting => state == State.Waiting;

        /// <summary>The source of the press last waited on, or of the wheel; None when idle.</summary>
        internal KeySource Source => state == State.Idle ? KeySource.None : source;

        /// <summary>When the press waited on went down (Time.unscaledTime).</summary>
        internal float PressedAt => pressedAt;

        /// <summary>What the last step made of the key, for the log.</summary>
        internal KeyNote Note { get; private set; }

        /// <summary>Forget everything (the mod switched off, another character).</summary>
        internal void Reset()
        {
            state = State.Idle;
            source = KeySource.None;
        }

        /// <summary>The wheel couldn't open after an Open event: wait for the key to be let go, doing nothing.</summary>
        internal void OpenFailed()
        {
            if (state == State.Wheel)
            {
                state = State.Spent;
            }
        }

        internal KeyStep Step(in KeyFrame frame)
        {
            Note = KeyNote.None;
            switch (state)
            {
                case State.Waiting:
                    return Waiting(frame);
                case State.Wheel:
                    if (frame.WheelOpen)
                    {
                        return default;   // the wheel reads the key's release itself
                    }
                    if (frame.Of(source).Held && !frame.Of(source).Down)
                    {
                        state = State.Spent;   // closed by a click, Escape, death …, with the key still held
                        Note = KeyNote.ClosedKeyHeld;
                        return default;
                    }
                    Reset();
                    return Fresh(frame);
                case State.Spent:
                    if (frame.Of(source).Held && !frame.Of(source).Down)
                    {
                        return default;
                    }
                    Reset();
                    return Fresh(frame);
                default:
                    return Fresh(frame);
            }
        }

        private KeyStep Waiting(in KeyFrame frame)
        {
            SourceFrame now = frame.Of(source);
            KeyStep step = default;
            // Longer than a hold (and than AwaySeconds, so a slow frame rate keeps its taps) since the last frame seen: the
            // game was away (not running while another window had the focus, or a long hitch), so a key found let go now
            // may have been let go only as the focus went (which resets the keyboard): not a tap.
            bool away = frame.Now - lastSeen > (frame.HoldSeconds > AwaySeconds ? frame.HoldSeconds : AwaySeconds);
            lastSeen = frame.Now;
            if (!now.Ready)
            {
                // A window took the keys, the game lost the focus, a build tool came into his hand, or the setting changed
                // during the press: dropped, with no tap. The press was already taken from the game, so nothing happens.
                Reset();
                Note = KeyNote.Dropped;
                return step;
            }
            if (now.Down)
            {
                // Let go and pressed again between two frames: the first press was a tap, and this one is waited on (or,
                // if the game would read it at its press, the gamepad's Alt keys held, say, left to the game).
                step.Event = away || overdue ? KeyEvent.None : KeyEvent.Tap;
                step.Source = source;
                Note = NoteOfRelease(away);
                if (!now.PressReady)
                {
                    Reset();
                    return step;
                }
                step.Take = TakenFromGame(source);
                pressedAt = frame.Now;
                overdue = false;
                return step;
            }
            if (!now.Held)
            {
                step.Event = away || overdue ? KeyEvent.None : KeyEvent.Tap;
                step.Source = source;
                Note = NoteOfRelease(away);
                Reset();
                return step;
            }
            if (frame.Now - pressedAt >= frame.HoldSeconds)
            {
                if (!now.OpenReady)
                {
                    if (!overdue)
                    {
                        Note = KeyNote.WaitsForLT;
                    }
                    overdue = true;   // a hold, waiting for the gamepad's LT to be let go: its release is no tap
                    return step;
                }
                step.Event = KeyEvent.Open;
                step.Source = source;
                state = State.Wheel;
                Note = KeyNote.Open;
            }
            return step;
        }

        private KeyNote NoteOfRelease(bool away) => away ? KeyNote.NoTapAway : overdue ? KeyNote.NoTapAfterWaiting : KeyNote.Tap;

        /// <summary>A frame with nothing going on: a new press, from the first source that went down and may be acted on
        /// (the game's key, then the mod's own, then the gamepad's).</summary>
        private KeyStep Fresh(in KeyFrame frame)
        {
            KeySource pressed = Pick(frame, KeySource.GameKey) ?? Pick(frame, KeySource.OwnKey) ?? Pick(frame, KeySource.Gamepad) ?? KeySource.None;
            if (pressed == KeySource.None)
            {
                return default;   // no press, or one the game keeps
            }
            state = State.Waiting;
            source = pressed;
            pressedAt = frame.Now;
            lastSeen = frame.Now;
            overdue = false;
            Note = KeyNote.Waiting;
            return new KeyStep { Take = TakenFromGame(pressed), Source = pressed };
        }

        private static KeySource? Pick(in KeyFrame frame, KeySource candidate)
        {
            SourceFrame view = frame.Of(candidate);
            return view.Down && view.Ready && view.PressReady ? candidate : (KeySource?)null;
        }

        private static KeySource TakenFromGame(KeySource pressed) => pressed == KeySource.OwnKey ? KeySource.None : pressed;
    }
}
