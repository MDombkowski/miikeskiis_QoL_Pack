using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AFKRaidBlocker
{
    /// <summary>
    /// Whether a key, a mouse button, the mouse, the wheel or a gamepad was used this frame, read from Unity's input
    /// system as the game reads it (ZInput), without taking anything from the game. A key or button counts as it is
    /// pressed, not while it's held, so a stuck key or one left held after switching windows can't keep him there; the
    /// mouse counts once it has really moved (60 counts from where it was, within a second), not for a jittery sensor;
    /// a stick as it crosses into a direction. Nothing counts while the game's window hasn't the focus: what is typed or
    /// clicked in another window is not being at the game.
    /// </summary>
    internal static class InputSeen
    {
        /// <summary>How far the mouse must get within a second to count, in the mouse's own counts (about a millimetre
        /// on a common mouse): measured as one move from where it started, so a sensor's back-and-forth jitter cancels
        /// out instead of adding up.</summary>
        internal const float MouseTravel = 60f;

        private static bool broken;
        private static Vector2 travel;
        private static double travelSince;

        internal static bool ThisFrame(double now)
        {
            if (broken || !Application.isFocused)
            {
                travel = Vector2.zero;
                return false;
            }
            try
            {
                return KeyPressed() || MouseUsed(now) || AnyGamepad();
            }
            catch (Exception error)
            {
                broken = true;
                Module.Log.LogWarning("Can't read the keys and the mouse, so from now on only movement keeps you " +
                                      $"there this session. {error.GetType().Name}: {error.Message}");
                return false;
            }
        }

        /// <summary>Any key pressed this frame, each key on its own (the keyboard's "any key" fires only when no key was
        /// down before, so one key held would hide every other press).</summary>
        private static bool KeyPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }
            foreach (KeyControl key in keyboard.allKeys)
            {
                if (key != null && key.wasPressedThisFrame)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool MouseUsed(double now)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }
            if (Pressed(mouse.leftButton) || Pressed(mouse.rightButton) || Pressed(mouse.middleButton)
                || Pressed(mouse.forwardButton) || Pressed(mouse.backButton)
                || mouse.scroll.ReadValue() != Vector2.zero)
            {
                return true;
            }
            return Travelled(ref travel, ref travelSince, mouse.delta.ReadValue(), now);
        }

        /// <summary>Adds this frame's mouse movement to the move since the window began (a second at most), and says
        /// whether that move has got far enough; then starts a new window.</summary>
        internal static bool Travelled(ref Vector2 sum, ref double since, Vector2 delta, double now)
        {
            if (now - since > 1.0)
            {
                sum = Vector2.zero;
                since = now;
            }
            sum += delta;
            if (sum.magnitude < MouseTravel)
            {
                return false;
            }
            sum = Vector2.zero;
            since = now;
            return true;
        }

        private static bool AnyGamepad()
        {
            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad != null && GamepadUsed(pad))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool GamepadUsed(Gamepad pad)
        {
            return Pressed(pad.buttonSouth) || Pressed(pad.buttonEast) || Pressed(pad.buttonWest) || Pressed(pad.buttonNorth)
                || Pressed(pad.leftShoulder) || Pressed(pad.rightShoulder)
                || Pressed(pad.leftTrigger) || Pressed(pad.rightTrigger)
                || Pressed(pad.startButton) || Pressed(pad.selectButton)
                || Pressed(pad.leftStickButton) || Pressed(pad.rightStickButton)
                || Turned(pad.dpad) || Turned(pad.leftStick) || Turned(pad.rightStick);
        }

        /// <summary>A stick or the d-pad pushed into a direction this frame.</summary>
        private static bool Turned(Vector2Control stick)
        {
            return stick is StickControl s
                ? Pressed(s.up) || Pressed(s.down) || Pressed(s.left) || Pressed(s.right)
                : stick is DpadControl d && (Pressed(d.up) || Pressed(d.down) || Pressed(d.left) || Pressed(d.right));
        }

        private static bool Pressed(ButtonControl button) => button != null && button.wasPressedThisFrame;
    }
}
