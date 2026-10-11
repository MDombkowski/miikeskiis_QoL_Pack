using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PowerWheel
{
    /// <summary>Whether the key that opened the wheel is still held.</summary>
    internal enum KeyState
    {
        Held,
        Released,

        /// <summary>It can't be read (its source is gone, or the key throws): the wheel closes without a change.</summary>
        Unknown,
    }

    /// <summary>
    /// The mod's work on the keys, once a frame just before the game's Player.Update reads the power key (Patches.cs):
    /// read the keys the settings use, step the tap-or-hold watch (KeyWatch), take the game's press when the watch says
    /// so (ZInput.ResetButtonStatus, so the game doesn't use the power at the press), and act on a tap (the game's own
    /// StartGuardianPower, so every other mod's patch on it still applies: PlantEasily's block while planting, for one)
    /// or a hold (the wheel). Once a press is taken, the game's own button reads nothing more of it, so whether the key
    /// is still held comes from its Input System action (archive/LESSONS_LEARNED-2026-09, "ResetButtonStatus").
    /// </summary>
    internal static class Keys
    {
        private static readonly KeyWatch Watch = new KeyWatch();
        private static readonly Action Work = DoFrame;

        private static Player player;
        private static Player watchedFor;
        private static bool saidNoButton;
        private static bool ownFailed;
        private static KeyboardShortcut failedKey;

        /// <summary>From the prefix on Player.Update, for the local player, while the module is on.</summary>
        internal static void BeforePlayerUpdate(Player localPlayer)
        {
            player = localPlayer;
            try
            {
                Module.Guard("reading the power key", Work);
            }
            finally
            {
                player = null;
            }
        }

        /// <summary>Forget a press being waited on (the module off, or failed): switching on later can't act on it.</summary>
        internal static void Reset() => Watch.Reset();

        /// <summary>For the wheel's "let go": the key that opened it, held or not.</summary>
        internal static KeyState WheelKey()
        {
            bool? held;
            switch (Watch.WheelSource)
            {
                case KeySource.GameKey:
                    held = Held(GameHooks.PowerButton);
                    break;
                case KeySource.Gamepad:
                    held = Held(GameHooks.PadPowerButton);
                    break;
                case KeySource.OwnKey:
                    held = OwnHeld(Settings.OwnKey.Value);
                    break;
                default:
                    held = null;
                    break;
            }
            return held == null ? KeyState.Unknown : held.Value ? KeyState.Held : KeyState.Released;
        }

        private static void DoFrame()
        {
            bool newCharacter = !ReferenceEquals(player, watchedFor);
            if (newCharacter)
            {
                // Another character (logged out and in): whatever was going on was the other one's.
                watchedFor = player;
                Watch.Reset();
            }
            if (KeyLog.On)
            {
                SayPatchRuns(newCharacter);
            }

            bool useGame = Settings.Key.Value == KeyChoice.GamePowerKey;
            KeyboardShortcut own = Settings.OwnKey.Value;
            bool useOwn = !useGame && own.MainKey != KeyCode.None;
            bool usePad = Settings.Gamepad.Value;

            bool gameDown = useGame && ZInput.GetButtonDown(GameHooks.PowerButton);
            bool padDown = usePad && ZInput.GetButtonDown(GameHooks.PadPowerButton);
            bool ownDown = useOwn && OwnDown(own);
            bool logging = KeyLog.On;
            if (logging)
            {
                SayRawPress(useGame, gameDown, useOwn, own, ownDown);
            }
            if (Watch.Idle && !gameDown && !padDown && !ownDown)
            {
                return;   // most frames
            }

            bool ready = CommonReady();
            var frame = new KeyFrame
            {
                Game = new SourceFrame
                {
                    Down = gameDown,
                    Held = useGame && Held(GameHooks.PowerButton) == true,
                    Ready = ready && useGame && Action(GameHooks.PowerButton) != null,
                    PressReady = true,
                    OpenReady = true,
                },
                Own = new SourceFrame
                {
                    Down = ownDown,
                    Held = useOwn && OwnHeld(own) == true,
                    Ready = ready && useOwn && !Failed(own),
                    PressReady = true,
                    OpenReady = true,
                },
                Pad = new SourceFrame
                {
                    Down = padDown,
                    Held = usePad && Held(GameHooks.PadPowerButton) == true,
                    Ready = ready && usePad && Action(GameHooks.PadPowerButton) != null,
                    PressReady = PadInUse(),
                    OpenReady = !ZInput.GetButton(GameHooks.PadAltKeys),
                },
                WheelOpen = Wheel.Shows,
                Now = Time.unscaledTime,
                HoldSeconds = Settings.Hold,
            };

            bool wasIdle = Watch.Idle;
            KeySource before = Watch.Source;
            float heldFor = frame.Now - Watch.PressedAt;
            KeyStep step = Watch.Step(frame);
            if (logging)
            {
                SayStep(frame, step, wasIdle, before, heldFor, useGame, useOwn, usePad);
            }
            if (step.Take == KeySource.GameKey)
            {
                ZInput.ResetButtonStatus(GameHooks.PowerButton);
            }
            else if (step.Take == KeySource.Gamepad)
            {
                ZInput.ResetButtonStatus(GameHooks.PadPowerButton);
            }
            switch (step.Event)
            {
                case KeyEvent.Tap:
                    if (Settings.Tap.Value == TapChoice.UsesPower)
                    {
                        player.StartGuardianPower();   // its own refusals and messages, as at the game's own press
                    }
                    break;
                case KeyEvent.Open:
                    bool opened = Wheel.Open(player, out string why);
                    KeyLog.Line(opened ? "The wheel opened: " + why : "The wheel didn't open: " + why + ". Nothing more until the key is let go.");
                    if (!opened)
                    {
                        Watch.OpenFailed();
                    }
                    break;
            }
        }

        // The key log's calls, each through KeyLog.Safe (an error there switches only the log off), kept out of DoFrame
        // so that its frames make nothing for the log to collect while the log is off.
        private static void SayPatchRuns(bool newCharacter) => KeyLog.Safe(() => KeyLog.PatchRuns(newCharacter));

        private static void SayRawPress(bool useGame, bool gameDown, bool useOwn, KeyboardShortcut own, bool ownDown) =>
            KeyLog.Safe(() => LogRawPress(useGame, gameDown, useOwn, own, ownDown));

        private static void SayStep(KeyFrame frame, KeyStep step, bool wasIdle, KeySource before, float heldFor, bool useGame, bool useOwn,
                                    bool usePad) =>
            KeyLog.Safe(() => Report(frame, step, wasIdle, before, heldFor, useGame, useOwn, usePad));

        /// <summary>The game's power key as the game has it bound now (F unless rebound), for the log.</summary>
        internal static string GamePowerKeyPath()
        {
            InputAction action = ZInput.instance?.GetButtonDef(GameHooks.PowerButton)?.ButtonAction;
            if (action == null)
            {
                return "the game has no such button";
            }
            string path = action.bindings.Count > 0 ? action.bindings[0].effectivePath : null;
            return string.IsNullOrEmpty(path) ? "not bound" : path;
        }

        // The log's view of the key itself, apart from what the mod reads: for the game's power key, its Input System
        // action (the game's own button is fed from it); for its own key, the keyboard's key of the same name. The game's
        // button counts a press at its own tick, which may come a frame after the key went down: then a press shows as
        // two lines on frames next to each other ("went down, reports no press", then "reports a press"), which is normal.
        private static void LogRawPress(bool useGame, bool gameDown, bool useOwn, KeyboardShortcut own, bool ownDown)
        {
            if (useGame)
            {
                InputAction action = Action(GameHooks.PowerButton);
                bool keyDown = action != null && action.WasPressedThisFrame();
                if (keyDown || gameDown)
                {
                    KeyLog.Line($"{(keyDown ? $"The key {GamePowerKeyPath()} went down" : "The key didn't go down in this frame")}; " +
                                $"the game's power button {(gameDown ? "reports a press" : "reports no press (yet)")}.");
                }
            }
            if (useOwn)
            {
                bool keyboardDown = KeyboardDown(own.MainKey, out string keyName);
                if (keyboardDown || ownDown)
                {
                    KeyLog.Line($"Its own key {own}: the keyboard's {keyName} {(keyboardDown ? "went down" : "didn't go down")}; " +
                                $"the mod reads {(ownDown ? "a press" : "no press")}" +
                                (own.Modifiers.Any() ? $" (with {string.Join(" + ", own.Modifiers)} needed held: {(own.Modifiers.All(m => ZInput.GetKey(m, false)) ? "held" : "not all held")})" : string.Empty) + ".");
                }
            }
        }

        // The keyboard's own key of the same name as a Unity key code (BackQuote is the Input System's Backquote), read
        // straight from the keyboard, for the log only.
        private static bool KeyboardDown(KeyCode code, out string keyName)
        {
            keyName = code.ToString();
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                keyName += " (no keyboard found)";
                return false;
            }
            if (!Enum.TryParse(code.ToString(), true, out Key key) || key == Key.None)
            {
                keyName += " (no keyboard key of that name)";
                return false;
            }
            keyName = key.ToString();
            return keyboard[key].wasPressedThisFrame;
        }

        // What the watch made of this frame, in words, with the reasons a press was left to the game or dropped.
        private static void Report(in KeyFrame frame, KeyStep step, bool wasIdle, KeySource before, float heldFor, bool useGame,
                                   bool useOwn, bool usePad)
        {
            if (wasIdle && Watch.Note != KeyNote.Waiting)
            {
                // Down in this frame, but not waited on: the game keeps it.
                foreach (KeySource source in new[] { KeySource.GameKey, KeySource.OwnKey, KeySource.Gamepad })
                {
                    if (frame.Of(source).Down)
                    {
                        KeyLog.Line($"{KeyLog.NameOf(source)} went down; left to the game, because: " +
                                    KeyLog.Because(WhyNot(source, frame.Of(source), useGame, useOwn, usePad)) + ".");
                    }
                }
                return;
            }
            KeySource about = before != KeySource.None ? before : step.Source;
            string name = KeyLog.NameOf(about);
            switch (Watch.Note)
            {
                case KeyNote.Waiting:
                    KeySource now = Watch.Source;
                    KeyLog.Line($"{KeyLog.NameOf(now)} went down: waiting {KeyLog.Seconds(frame.HoldSeconds)} to tell a tap from a hold" +
                                (now == KeySource.OwnKey ? " (the game doesn't read this key)." : " (taken from the game, so it doesn't use the power as you press)."));
                    break;
                case KeyNote.Tap:
                    KeyLog.Line($"{name} let go after {KeyLog.Seconds(heldFor)}: a tap, " +
                                (Settings.Tap.Value == TapChoice.UsesPower ? "so it uses the power (the game's own, with its own refusals)." : "which does nothing (\"A tap of the key\" is Nothing)."));
                    break;
                case KeyNote.NoTapAway:
                    KeyLog.Line($"{name} let go, found only after the game had no frame for longer than half a second or the hold (another window had the focus, or a long hitch): no tap.");
                    break;
                case KeyNote.NoTapAfterWaiting:
                    KeyLog.Line($"{name} let go after a hold that waited for LT: no tap.");
                    break;
                case KeyNote.Dropped:
                    KeyLog.Line($"{name}: the press was dropped after {KeyLog.Seconds(heldFor)}, with no tap, because: " +
                                KeyLog.Because(WhyNot(before, frame.Of(before), useGame, useOwn, usePad)) + ".");
                    break;
                case KeyNote.WaitsForLT:
                    KeyLog.Line($"{name} held {KeyLog.Seconds(heldFor)}: the wheel waits until LT is let go.");
                    break;
                case KeyNote.Open:
                    KeyLog.Line($"{name} held {KeyLog.Seconds(heldFor)}: opening the wheel.");
                    break;
                case KeyNote.ClosedKeyHeld:
                    KeyLog.Line($"The wheel closed with {KeyLog.NameInSentence(about)} still held: nothing more until it's let go.");
                    break;
            }
        }

        /// <summary>Why the mod may not act on a press of this source now (an empty list when it may).</summary>
        private static List<string> WhyNot(KeySource source, SourceFrame view, bool useGame, bool useOwn, bool usePad)
        {
            var reasons = new List<string>();
            switch (source)
            {
                case KeySource.GameKey:
                    if (!useGame)
                    {
                        reasons.Add("\"Key\" is OwnKey");
                    }
                    else if (Action(GameHooks.PowerButton) == null)
                    {
                        reasons.Add("the game has no power button");
                    }
                    break;
                case KeySource.OwnKey:
                    if (!useOwn)
                    {
                        reasons.Add(Settings.Key.Value == KeyChoice.GamePowerKey ? "\"Key\" is GamePowerKey" : "no key is set in \"Own key\"");
                    }
                    else if (Failed(Settings.OwnKey.Value))
                    {
                        reasons.Add("the key in \"Own key\" can't be read");
                    }
                    break;
                case KeySource.Gamepad:
                    if (!usePad)
                    {
                        reasons.Add("\"Gamepad\" is off");
                    }
                    else if (Action(GameHooks.PadPowerButton) == null)
                    {
                        reasons.Add("the game has no gamepad power button");
                    }
                    else if (view.Down && !view.PressReady)
                    {
                        reasons.Add("no gamepad in use, a layout other than the classic one, or LT held as it was pressed");
                    }
                    break;
            }
            if (!Application.isFocused)
            {
                reasons.Add("the game window doesn't have the focus");
            }
            if (!GameHooks.TakesInput(player))
            {
                reasons.Add("the game isn't taking your input (a window, the map, chat or the console is open, or you're dead, teleporting or in a cutscene)");
            }
            if (Hud.InRadial())
            {
                reasons.Add("a wheel is open");
            }
            if (Hud.IsPieceSelectionVisible())
            {
                reasons.Add("the build menu is open");
            }
            if (player.InPlaceMode())
            {
                reasons.Add("a hammer, hoe, cultivator or other build tool is in your hand (the key stays the game's there)");
            }
            string missing = Wheel.MissingPart();
            if (missing != null)
            {
                reasons.Add("the game's wheel isn't there yet (" + missing + " is missing)");
            }
            return reasons;
        }

        /// <summary>
        /// What every source needs before the mod takes a press: the game would read the power key now (it takes input,
        /// no wheel or build menu is open: Player.Update's own conditions), the game has the focus (a key held while it
        /// loses it reads as let go, which mustn't count as a tap), no build tool, hoe or cultivator is in hand (there the
        /// game's wheel can't open, so F stays exactly the game's), and the wheel exists.
        /// </summary>
        private static bool CommonReady() =>
            Application.isFocused && GameHooks.TakesInput(player) && !Hud.InRadial() && !Hud.IsPieceSelectionVisible() &&
            !player.InPlaceMode() && Wheel.CanOpen;

        /// <summary>The game reads the gamepad's power button only in the classic layout, with a gamepad in use and its Alt
        /// keys not held (Player.Update), at the press only; elsewhere the power is both sticks pressed, which stays the
        /// game's. Asked of a new press only (SourceFrame.PressReady): raising the shield (LT) or touching the mouse
        /// during a press doesn't undo it.</summary>
        private static bool PadInUse() =>
            ZInput.InputLayout == InputLayout.Default && ZInput.IsGamepadActive() && !ZInput.GetButton(GameHooks.PadAltKeys);

        private static InputAction Action(string button)
        {
            InputAction action = ZInput.instance?.GetButtonDef(button)?.ButtonAction;
            if (action == null && button == GameHooks.PowerButton && !saidNoButton)
            {
                saidNoButton = true;
                Module.Log.LogWarning($"The game has no \"{button}\" button, so F can't open the wheel; \"Own key\" still can " +
                                      "(the setting \"Key\").");
            }
            return action;
        }

        private static bool? Held(string button)
        {
            InputAction action = Action(button);
            return action == null ? (bool?)null : action.IsPressed();
        }

        // The mod's own key, read through the game's input. Some keys can't be read at all (the game's input throws for
        // F13 to F15, Help, SysReq, the shifted characters …: archive/LESSONS_LEARNED-2026-09, "ZInput.GetKey throws"):
        // such a key is left alone, with one log line, until the setting changes.
        private static bool OwnDown(KeyboardShortcut key)
        {
            if (Failed(key))
            {
                return false;
            }
            try
            {
                return ZInput.GetKeyDown(key.MainKey, false) && key.Modifiers.All(modifier => ZInput.GetKey(modifier, false));
            }
            catch (Exception error)
            {
                Fail(key, error);
                return false;
            }
        }

        private static bool? OwnHeld(KeyboardShortcut key)
        {
            if (key.MainKey == KeyCode.None || Failed(key))
            {
                return null;
            }
            try
            {
                return ZInput.GetKey(key.MainKey, false);
            }
            catch (Exception error)
            {
                Fail(key, error);
                return null;
            }
        }

        private static bool Failed(KeyboardShortcut key) => ownFailed && key.Equals(failedKey);

        private static void Fail(KeyboardShortcut key, Exception error)
        {
            ownFailed = true;
            failedKey = key;
            Module.Log.LogWarning($"The key {key} in \"Own key\" can't be read ({error.Message}); it does nothing until it's changed.");
        }
    }
}
