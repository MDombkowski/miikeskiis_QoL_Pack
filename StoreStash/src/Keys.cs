using System;
using System.Linq;
using BepInEx.Configuration;

namespace StoreStash
{
    /// <summary>
    /// StoreStash's keys, read once a frame. A key counts with extra modifiers held (as the game's and HexQuickStackStorage's
    /// did), so one press can match two of them: Left Alt + H is also H. The one with the most modifiers wins, so marking a
    /// chest never also runs the personal quick stack; between two alike, the first listed. The sort key (card A53) counts
    /// only while it would sort (Sorting.KeyLive), and no key works while the trash can's question is up. The setup keys
    /// (card A41) take part too, SetupKeys doing their work: the setup key, and the copy and paste keys only while a
    /// chest's window is open (so Ctrl+V never also runs a key bound to V there). Stack Overflow's tidy key (card A42) takes
    /// part while Stack Overflow is on. The one-stack key (card A82, OneStack: the middle mouse button out of the box) takes
    /// part only with his inventory open and pointing at a stack of his, first in the list.
    /// </summary>
    internal static class Keys
    {
        internal enum Which
        {
            None,
            QuickStack,
            Personal,
            Mine,
            Sort,
            Setup,
            Tidy,
            OneStack,
        }

        /// <summary>Whether a key went down this frame with its modifiers held. Only the scratch test harness sets another:
        /// it has no game input to read.</summary>
        internal static Func<KeyboardShortcut, bool> IsPressed = Settings.Pressed;

        /// <summary>Whether the game would take a key now (QuickStack.TakesKeys). Only the scratch test harness sets another:
        /// the game's windows and text boxes aren't there to ask.</summary>
        internal static Func<bool> TakingKeys = QuickStack.TakesKeys;

        /// <summary>Whether the trash can's question is up (TrashDialog.IsOpen): no key works under it. Only the scratch test
        /// harness sets another: it can't make the question's window.</summary>
        internal static Func<bool> Asking = () => TrashDialog.IsOpen;

        /// <summary>Every frame: the key pressed, if the game would take a key now, does its work.</summary>
        internal static void Update()
        {
            if (!Module.IsOn || Asking())
            {
                return;
            }
            Which pressed = Pressed();
            if (pressed == Which.None || !TakingKeys())
            {
                return;
            }
            switch (pressed)
            {
                case Which.QuickStack:
                    QuickStack.Ask();
                    break;
                case Which.Personal:
                    QuickStack.Ask(personal: true);
                    break;
                case Which.Mine:
                    Mine.Press();
                    break;
                case Which.Sort:
                    Sorting.KeyPressed();
                    break;
                case Which.Setup:
                    break;   // the setup keys' own work: SetupKeys, later in the frame
                case Which.Tidy:
                    Tidy.KeyPressed();
                    break;
                case Which.OneStack:
                    StoreStash.OneStack.Press();
                    break;
            }
        }

        /// <summary>Which of StoreStash's keys went down this frame: the one with the most modifiers held, if two match. The
        /// one-stack key (card A82) counts only with his inventory open and pointing at a stack of his, and first: so on a
        /// stack it wins a tie, and anywhere else a key it shares goes on as usual.</summary>
        internal static Which Pressed()
        {
            Which best = Which.None;
            int most = -1;
            if (StoreStash.OneStack.Ready())
            {
                Consider(Settings.OneStackKey.Value, Which.OneStack, ref best, ref most, StoreStash.OneStack.Pointing);
            }
            Consider(Settings.Key.Value, Which.QuickStack, ref best, ref most);
            Consider(Settings.PersonalKey.Value, Which.Personal, ref best, ref most);
            Consider(Settings.MineKey.Value, Which.Mine, ref best, ref most);
            if (Sorting.KeyLive())
            {
                Consider(SortSettings.Key.Value, Which.Sort, ref best, ref most);
            }
            if (SetupSettings.Active)
            {
                Consider(SetupSettings.Key.Value, Which.Setup, ref best, ref most);
                if (SetupKeys.ChestWindowOpen())
                {
                    Consider(SetupSettings.CopyKey.Value, Which.Setup, ref best, ref most);
                    Consider(SetupSettings.PasteKey.Value, Which.Setup, ref best, ref most);
                }
            }
            if (OverflowSettings.Active)
            {
                Consider(OverflowSettings.Key.Value, Which.Tidy, ref best, ref most);
            }
            return best;
        }

        // A key that went down counts when <only> (if given) says so too: asked only then, so it may cost a little.
        private static void Consider(KeyboardShortcut key, Which which, ref Which best, ref int most, Func<bool> only = null)
        {
            if (!IsPressed(key) || (only != null && !only()))
            {
                return;
            }
            int modifiers = key.Modifiers.Count();
            if (modifiers > most)
            {
                best = which;
                most = modifiers;
            }
        }
    }
}
