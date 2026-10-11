using System;
using UnityEngine;
using Valheim.UI;

namespace PowerWheel
{
    /// <summary>
    /// The power wheel's controls, over the game's own. The game's wheel reads its input through public functions it sets
    /// at every root opening (RadialConfigHelper.SetItemInteractionControls); the mod keeps the ones just set and puts its
    /// own in front of four of them, each passing straight to the game's whenever the power page isn't the one open (or
    /// the mod is off), so the game's own wheels never see a difference, and the game's next root opening sets its own
    /// again anyway:
    /// - "release to use" is the power key let go (the game's is G, T or the gamepad's wheel button let go);
    /// - a right click, when "Right click" picks and uses, picks instead of going back (the game's "back", which closes a
    ///   top page, is skipped for that click, and its "confirm" takes it);
    /// - the mouse's direction is the power he has while the pointer is in the wheel's centre, so letting go there keeps
    ///   it and never uses it (PowerRules.Uses; the game's direction is the pointer's, anywhere).
    /// The game's own functions are always called outside the mod's guard: an error of the game's is never the mod's.
    /// </summary>
    internal static class Controls
    {
        private static readonly Func<bool> OurRelease = Release;
        private static readonly Func<bool> OurConfirm = Confirm;
        private static readonly Func<bool> OurBack = Back;
        private static readonly Func<Vector2> OurMouse = Mouse;

        private static RadialBase radial;
        private static Func<bool> gameRelease;
        private static Func<bool> gameConfirm;
        private static Func<bool> gameBack;
        private static Func<Vector2> gameMouse;

        /// <summary>How the power about to be chosen was chosen: set by the function that answered "yes", read and reset by
        /// Picks. The game's gamepad hover, flick and double tap call the element without asking any of these, and count
        /// as a click.</summary>
        internal static Via Via = Via.Click;

        /// <summary>At the root's opening, right after the game set its own controls.</summary>
        internal static void Install(RadialBase on)
        {
            radial = on;
            if (!ReferenceEquals(on.GetReleaseToUse, OurRelease))
            {
                gameRelease = on.GetReleaseToUse;
            }
            if (!ReferenceEquals(on.GetConfirm, OurConfirm))
            {
                gameConfirm = on.GetConfirm;
            }
            if (!ReferenceEquals(on.GetBack, OurBack))
            {
                gameBack = on.GetBack;
            }
            if (!ReferenceEquals(on.GetMouseDirection, OurMouse))
            {
                gameMouse = on.GetMouseDirection;
            }
            on.GetReleaseToUse = OurRelease;
            on.GetConfirm = OurConfirm;
            on.GetBack = OurBack;
            on.GetMouseDirection = OurMouse;
        }

        /// <summary>The game's own functions back where the mod's still are (the mod switched off or failed).</summary>
        internal static void Restore()
        {
            if (radial == null)
            {
                return;
            }
            if (ReferenceEquals(radial.GetReleaseToUse, OurRelease))
            {
                radial.GetReleaseToUse = gameRelease;
            }
            if (ReferenceEquals(radial.GetConfirm, OurConfirm))
            {
                radial.GetConfirm = gameConfirm;
            }
            if (ReferenceEquals(radial.GetBack, OurBack))
            {
                radial.GetBack = gameBack;
            }
            if (ReferenceEquals(radial.GetMouseDirection, OurMouse))
            {
                radial.GetMouseDirection = gameMouse;
            }
        }

        private static bool Ours() => Module.IsOn && Wheel.IsOurs(radial);

        private static bool Release()
        {
            if (Ours())
            {
                bool? released = null;
                Module.Guard("reading the key's release", () => released = Released());
                if (released != null)
                {
                    return released.Value;
                }
            }
            return gameRelease?.Invoke() ?? false;
        }

        // The key let go: the wheel then picks what the pointer points at, by "Letting go of the key". The game losing the
        // focus (a key held then reads as let go) and a key that can't be read close it without a change instead.
        private static bool Released()
        {
            if (!Application.isFocused)
            {
                KeyLog.Line("The game lost the focus with the wheel open: it closes without a change.");
                radial.QueuedClose();
                return false;
            }
            switch (Keys.WheelKey())
            {
                case KeyState.Held:
                    return false;
                case KeyState.Released:
                    KeyLog.Line("The key let go with the wheel open: it picks what the pointer points at, by \"Letting go of the key\" " +
                                "(nothing, if the pointer has no direction; a \"chosen by Release\" line follows when it picks).");
                    Via = Via.Release;
                    return true;
                default:
                    KeyLog.Line("The key that opened the wheel can't be read: the wheel closes without a change.");
                    radial.QueuedClose();
                    return false;
            }
        }

        private static bool Confirm()
        {
            if (Ours())
            {
                bool right = false;
                Module.Guard("reading a right click in the wheel", () => right = RightClickPicks());
                if (right)
                {
                    Via = Via.RightClick;
                    return true;
                }
            }
            bool confirmed = gameConfirm?.Invoke() ?? false;
            if (confirmed)
            {
                Via = Via.Click;
            }
            return confirmed;
        }

        private static bool Back()
        {
            if (Ours())
            {
                bool right = false;
                Module.Guard("reading a right click in the wheel", () => right = RightClickPicks());
                if (right)
                {
                    return false;   // the confirm takes it, in the same frame (the game asks "back" first)
                }
            }
            return gameBack?.Invoke() ?? false;
        }

        private static Vector2 Mouse()
        {
            if (Ours())
            {
                Vector2? centre = null;
                Module.Guard("reading the pointer in the wheel", () => centre = InCentre());
                if (centre != null)
                {
                    return centre.Value;
                }
            }
            return gameMouse?.Invoke() ?? Vector2.zero;
        }

        /// <summary>A right click let go, when "Right click" picks and uses (the game's own test for its "back" click).</summary>
        private static bool RightClickPicks() =>
            Settings.RightClick.Value == RightClickChoice.PicksAndUses && ZInput.GetMouseButtonUp(1) && !ZInput.IsGamepadMouseActive();

        /// <summary>With the pointer in the wheel's centre: the direction of the power he has (or none, with no power yet,
        /// which the game reads as no choice: letting go then picks nothing). Null elsewhere: the game's own direction.</summary>
        private static Vector2? InCentre()
        {
            Vector2 offset = (Vector2)ZInput.pointerPosition - radial.InfoPosition;
            if (!PowerRules.InCentre(offset.x, offset.y, Wheel.IconDistance))
            {
                return null;
            }
            RadialMenuElement current = Wheel.CurrentElement;
            if (current == null)
            {
                return Vector2.zero;
            }
            Vector2 toCurrent = (Vector2)current.ElementTransform.position - radial.InfoPosition;
            return toCurrent.sqrMagnitude > 0f ? toCurrent.normalized : Vector2.zero;
        }
    }
}
