namespace Grounded
{
    /// <summary>
    /// When Grounded acts, from plain facts about the key, so the scratch harness can run every rule without the game.
    /// With no key set (the setting emptied), only "always" acts: emptying the key gives the game's own placement back.
    /// </summary>
    internal static class KeyRules
    {
        internal static bool IsActive(KeyMode mode, bool keySet, bool held, bool toggledOn)
        {
            switch (mode)
            {
                case KeyMode.Always:
                    return true;
                case KeyMode.Toggle:
                    return keySet && toggledOn;
                default:
                    return keySet && held;
            }
        }

        /// <summary>
        /// One frame of the toggle. A press arms it, while building (a build tool in hand), not typing or in a menu, and
        /// with the game in focus; the release then switches it, if it is still armed, still building, not typing and in
        /// focus. Losing focus between the two (Alt-Tab with Left Alt as the key), or another key going down
        /// (<paramref name="interrupted"/>), disarms it, so leaving the game never switches it. Outside toggle mode, or
        /// with no key, it is off. True when this frame switched it.
        /// </summary>
        internal static bool ToggleStep(ref bool armed, ref bool on, KeyMode mode, bool keySet, bool pressed, bool released,
                                        bool building, bool typing, bool focused, bool interrupted)
        {
            if (mode != KeyMode.Toggle || !keySet)
            {
                armed = false;
                on = false;
                return false;
            }
            if (!focused || interrupted)
            {
                armed = false;
            }
            if (pressed)
            {
                armed = building && !typing && focused && !interrupted;
            }
            if (!released)
            {
                return false;
            }
            bool flips = armed && building && !typing;   // out of focus it was disarmed above
            armed = false;
            if (flips)
            {
                on = !on;
            }
            return flips;
        }
    }
}
