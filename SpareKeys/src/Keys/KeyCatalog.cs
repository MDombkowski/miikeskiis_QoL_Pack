using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>Where a key sits on a keyboard: the headings of the list of free keys, in keyboard order.</summary>
    internal enum Region
    {
        Function,
        Digits,
        Letters,
        Symbols,
        Editing,
        Modifiers,
        Navigation,
        NumberPad,
        System,
        Mouse,
        Gamepad,
    }

    /// <summary>One key the map knows: its id (a KeyCode's number, or a gamepad control's own number from 20000), what its
    /// cap says, its name in lists, the Input System's name for it (how the game's own binds and Jötunn's name it), where
    /// it sits, and why it isn't offered, if it isn't.</summary>
    internal sealed class KeyInfo
    {
        internal readonly int Id;
        internal readonly string Cap;
        internal readonly string Name;
        internal readonly string Control;
        internal readonly Region Region;

        /// <summary>Null for a key the map offers; else why it never does ("toggles a light" …).</summary>
        internal readonly string NotOffered;

        /// <summary>Its place in keyboard order: rows from the top left, then the mouse, then the gamepad.</summary>
        internal int Order;

        internal KeyInfo(int id, string cap, string name, string control, Region region, string notOffered = null)
        {
            Id = id;
            Cap = cap;
            Name = name;
            Control = control;
            Region = region;
            NotOffered = notOffered;
        }

        internal KeyCode Code => (KeyCode)Id;

        internal bool IsGamepad => Region == Region.Gamepad;
    }

    /// <summary>
    /// Every key the map can name: exactly the keyboard keys the game's input can read (ZInput's own key map,
    /// s_keyCodeToKeyMap in Valheim 1.0.17), the five mouse buttons it reads, and the gamepad's controls. A key outside it
    /// (F13 to F15, Help, SysReq, the shifted characters such as Hash or At) is never drawn, never offered and never asked
    /// about: ZInput.GetKey throws for it (LESSONS_LEARNED, archive 2026-09). Plain data, so the harness checks it.
    /// </summary>
    internal static class KeyCatalog
    {
        /// <summary>Gamepad controls that have no KeyCode of their own get ids from here.</summary>
        internal const int GamepadBase = 20000;

        private static readonly List<KeyInfo> all = new List<KeyInfo>();
        private static readonly Dictionary<int, KeyInfo> byId = new Dictionary<int, KeyInfo>();
        private static readonly Dictionary<string, KeyInfo> byKeyboardControl = new Dictionary<string, KeyInfo>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, KeyInfo> byGamepadControl = new Dictionary<string, KeyInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The mouse buttons, as the game's own map names them: Mouse3 is "forward", Mouse4 "back"
        /// (ZInput.s_keyCodeToMouseButtonMap).</summary>
        internal static readonly KeyCode[] MouseButtons = { KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.Mouse2, KeyCode.Mouse3, KeyCode.Mouse4 };

        /// <summary>Every key, in keyboard order.</summary>
        internal static IReadOnlyList<KeyInfo> All => all;

        static KeyCatalog()
        {
            const string toggles = "it toggles a light and changes what you type";
            const string system = "Windows uses it";
            Add(KeyCode.Escape, "Esc", "Escape", "escape", Region.Editing);
            for (int f = 1; f <= 12; f++)
            {
                Add(KeyCode.F1 + f - 1, "F" + f, "F" + f, "f" + f, Region.Function);
            }
            Add(KeyCode.Print, "PrtSc", "Print Screen", "printScreen", Region.System, system);
            Add(KeyCode.ScrollLock, "ScrLk", "Scroll Lock", "scrollLock", Region.Navigation);
            Add(KeyCode.Pause, "Pause", "Pause", "pause", Region.Navigation);

            Add(KeyCode.BackQuote, "`", "Backquote (`)", "backquote", Region.Symbols);
            for (int d = 1; d <= 9; d++)
            {
                Add(KeyCode.Alpha0 + d, d.ToString(), d.ToString(), d.ToString(), Region.Digits);
            }
            Add(KeyCode.Alpha0, "0", "0", "0", Region.Digits);
            Add(KeyCode.Minus, "-", "Minus (-)", "minus", Region.Symbols);
            Add(KeyCode.Equals, "=", "Equals (=)", "equals", Region.Symbols);
            Add(KeyCode.Backspace, "Back", "Backspace", "backspace", Region.Editing);
            Add(KeyCode.Insert, "Ins", "Insert", "insert", Region.Navigation);
            Add(KeyCode.Home, "Home", "Home", "home", Region.Navigation);
            Add(KeyCode.PageUp, "PgUp", "Page Up", "pageUp", Region.Navigation);
            Add(KeyCode.Numlock, "Num", "Num Lock", "numLock", Region.NumberPad, "it switches the number pad between digits and arrows");
            Add(KeyCode.KeypadDivide, "/", "Number Pad /", "numpadDivide", Region.NumberPad);
            Add(KeyCode.KeypadMultiply, "*", "Number Pad *", "numpadMultiply", Region.NumberPad);
            Add(KeyCode.KeypadMinus, "-", "Number Pad -", "numpadMinus", Region.NumberPad);

            Add(KeyCode.Tab, "Tab", "Tab", "tab", Region.Editing);
            foreach (char letter in "QWERTYUIOP")
            {
                AddLetter(letter);
            }
            Add(KeyCode.LeftBracket, "[", "Left Bracket ([)", "leftBracket", Region.Symbols);
            Add(KeyCode.RightBracket, "]", "Right Bracket (])", "rightBracket", Region.Symbols);
            Add(KeyCode.Backslash, "\\", "Backslash (\\)", "backslash", Region.Symbols);
            Add(KeyCode.Delete, "Del", "Delete", "delete", Region.Navigation);
            Add(KeyCode.End, "End", "End", "end", Region.Navigation);
            Add(KeyCode.PageDown, "PgDn", "Page Down", "pageDown", Region.Navigation);
            Add(KeyCode.Keypad7, "7", "Number Pad 7", "numpad7", Region.NumberPad);
            Add(KeyCode.Keypad8, "8", "Number Pad 8", "numpad8", Region.NumberPad);
            Add(KeyCode.Keypad9, "9", "Number Pad 9", "numpad9", Region.NumberPad);
            Add(KeyCode.KeypadPlus, "+", "Number Pad +", "numpadPlus", Region.NumberPad);

            Add(KeyCode.CapsLock, "Caps", "Caps Lock", "capsLock", Region.System, toggles);
            foreach (char letter in "ASDFGHJKL")
            {
                AddLetter(letter);
            }
            Add(KeyCode.Semicolon, ";", "Semicolon (;)", "semicolon", Region.Symbols);
            Add(KeyCode.Quote, "'", "Quote (')", "quote", Region.Symbols);
            Add(KeyCode.Return, "Enter", "Enter", "enter", Region.Editing);
            Add(KeyCode.Keypad4, "4", "Number Pad 4", "numpad4", Region.NumberPad);
            Add(KeyCode.Keypad5, "5", "Number Pad 5", "numpad5", Region.NumberPad);
            Add(KeyCode.Keypad6, "6", "Number Pad 6", "numpad6", Region.NumberPad);

            Add(KeyCode.LeftShift, "Shift", "Left Shift", "leftShift", Region.Modifiers);
            foreach (char letter in "ZXCVBNM")
            {
                AddLetter(letter);
            }
            Add(KeyCode.Comma, ",", "Comma (,)", "comma", Region.Symbols);
            Add(KeyCode.Period, ".", "Period (.)", "period", Region.Symbols);
            Add(KeyCode.Slash, "/", "Slash (/)", "slash", Region.Symbols);
            Add(KeyCode.RightShift, "Shift", "Right Shift", "rightShift", Region.Modifiers);
            Add(KeyCode.UpArrow, "Up", "Up Arrow", "upArrow", Region.Navigation);
            Add(KeyCode.Keypad1, "1", "Number Pad 1", "numpad1", Region.NumberPad);
            Add(KeyCode.Keypad2, "2", "Number Pad 2", "numpad2", Region.NumberPad);
            Add(KeyCode.Keypad3, "3", "Number Pad 3", "numpad3", Region.NumberPad);
            Add(KeyCode.KeypadEnter, "Enter", "Number Pad Enter", "numpadEnter", Region.NumberPad);

            Add(KeyCode.LeftControl, "Ctrl", "Left Ctrl", "leftCtrl", Region.Modifiers);
            Add(KeyCode.LeftWindows, "Win", "Left Windows", "leftWindows", Region.System, system);
            Add(KeyCode.LeftAlt, "Alt", "Left Alt", "leftAlt", Region.Modifiers);
            Add(KeyCode.Space, "Space", "Space", "space", Region.Editing);
            Add(KeyCode.RightAlt, "Alt", "Right Alt", "rightAlt", Region.Modifiers);
            Add(KeyCode.RightWindows, "Win", "Right Windows", "rightWindows", Region.System, system);
            Add(KeyCode.Menu, "Menu", "Menu", "contextMenu", Region.Navigation);
            Add(KeyCode.RightControl, "Ctrl", "Right Ctrl", "rightCtrl", Region.Modifiers);
            Add(KeyCode.LeftArrow, "Left", "Left Arrow", "leftArrow", Region.Navigation);
            Add(KeyCode.DownArrow, "Down", "Down Arrow", "downArrow", Region.Navigation);
            Add(KeyCode.RightArrow, "Right", "Right Arrow", "rightArrow", Region.Navigation);
            Add(KeyCode.Keypad0, "0", "Number Pad 0", "numpad0", Region.NumberPad);
            Add(KeyCode.KeypadPeriod, ".", "Number Pad .", "numpadPeriod", Region.NumberPad);

            // In the game's key map, but on no common keyboard's picture: listed when something uses it. (The game's map
            // also has the Meta keys and AltGr: the same keys as the Windows keys and Right Alt, so IdOf names them so.)
            Add(KeyCode.KeypadEquals, "=", "Number Pad =", "numpadEquals", Region.NumberPad);

            Add(KeyCode.Mouse0, "L", "Left Mouse Button", "leftButton", Region.Mouse);
            Add(KeyCode.Mouse1, "R", "Right Mouse Button", "rightButton", Region.Mouse);
            Add(KeyCode.Mouse2, "M", "Middle Mouse Button", "middleButton", Region.Mouse);
            Add(KeyCode.Mouse3, "4", "Mouse Button 4 (Mouse3)", "forwardButton", Region.Mouse);
            Add(KeyCode.Mouse4, "5", "Mouse Button 5 (Mouse4)", "backButton", Region.Mouse);

            // The gamepad: the game's own controls by their Input System names, and the KeyCodes a setting can hold
            // for them (ZInput.s_keyCodeToGamepadButtonMap: two KeyCodes for most buttons).
            int pad = GamepadBase;
            foreach (string[] each in new[]
            {
                new[] { "buttonSouth", "Gamepad A (South)" }, new[] { "buttonEast", "Gamepad B (East)" },
                new[] { "buttonWest", "Gamepad X (West)" }, new[] { "buttonNorth", "Gamepad Y (North)" },
                new[] { "leftShoulder", "Gamepad Left Bumper" }, new[] { "rightShoulder", "Gamepad Right Bumper" },
                new[] { "leftTrigger", "Gamepad Left Trigger" }, new[] { "rightTrigger", "Gamepad Right Trigger" },
                new[] { "select", "Gamepad Select" }, new[] { "start", "Gamepad Start" },
                new[] { "leftStickPress", "Gamepad Left Stick Press" }, new[] { "rightStickPress", "Gamepad Right Stick Press" },
                new[] { "dpad/up", "Gamepad D-Pad Up" }, new[] { "dpad/down", "Gamepad D-Pad Down" },
                new[] { "dpad/left", "Gamepad D-Pad Left" }, new[] { "dpad/right", "Gamepad D-Pad Right" },
                new[] { "leftStick/up", "Gamepad Left Stick Up" }, new[] { "leftStick/down", "Gamepad Left Stick Down" },
                new[] { "leftStick/left", "Gamepad Left Stick Left" }, new[] { "leftStick/right", "Gamepad Left Stick Right" },
                new[] { "rightStick/up", "Gamepad Right Stick Up" }, new[] { "rightStick/down", "Gamepad Right Stick Down" },
                new[] { "rightStick/left", "Gamepad Right Stick Left" }, new[] { "rightStick/right", "Gamepad Right Stick Right" },
                new[] { "touchpadButton", "Gamepad Touchpad" },
            })
            {
                var info = new KeyInfo(pad++, string.Empty, each[1], each[0], Region.Gamepad);
                Register(info);
                byGamepadControl[each[0]] = info;
            }
            foreach (KeyValuePair<KeyCode, string> joy in new Dictionary<KeyCode, string>
            {
                { KeyCode.JoystickButton0, "buttonSouth" }, { KeyCode.JoystickButton16, "buttonSouth" },
                { KeyCode.JoystickButton1, "buttonEast" }, { KeyCode.JoystickButton17, "buttonEast" },
                { KeyCode.JoystickButton2, "buttonWest" }, { KeyCode.JoystickButton18, "buttonWest" },
                { KeyCode.JoystickButton3, "buttonNorth" }, { KeyCode.JoystickButton19, "buttonNorth" },
                { KeyCode.JoystickButton4, "leftShoulder" }, { KeyCode.JoystickButton13, "leftShoulder" },
                { KeyCode.JoystickButton5, "rightShoulder" }, { KeyCode.JoystickButton14, "rightShoulder" },
                { KeyCode.JoystickButton6, "select" }, { KeyCode.JoystickButton10, "select" },
                { KeyCode.JoystickButton7, "start" },
                { KeyCode.JoystickButton8, "leftStickPress" }, { KeyCode.JoystickButton11, "leftStickPress" },
                { KeyCode.JoystickButton9, "rightStickPress" }, { KeyCode.JoystickButton12, "rightStickPress" },
            })
            {
                JoystickToPad[(int)joy.Key] = byGamepadControl[joy.Value].Id;
            }

            for (int i = 0; i < all.Count; i++)
            {
                all[i].Order = i;
            }
        }

        /// <summary>A setting's JoystickButtonN, as the gamepad control the game reads it as.</summary>
        private static readonly Dictionary<int, int> JoystickToPad = new Dictionary<int, int>();

        private static void AddLetter(char letter) =>
            Add((KeyCode)char.ToLowerInvariant(letter), letter.ToString(), letter.ToString(), char.ToLowerInvariant(letter).ToString(), Region.Letters);

        private static void Add(KeyCode code, string cap, string name, string control, Region region, string notOffered = null)
        {
            var info = new KeyInfo((int)code, cap, name, control, region, notOffered);
            Register(info);
            if (region != Region.Mouse)
            {
                byKeyboardControl[control] = info;
            }
        }

        private static void Register(KeyInfo info)
        {
            all.Add(info);
            byId[info.Id] = info;
        }

        /// <summary>The key with this id, or null for one the map doesn't know (and so never asks the game about).</summary>
        internal static KeyInfo Get(int id) => byId.TryGetValue(id, out KeyInfo info) ? info : null;

        internal static bool Knows(int id) => byId.ContainsKey(id);

        /// <summary>A KeyCode as the map's key id: itself for a keyboard key or mouse button it knows, the gamepad control a
        /// JoystickButtonN is read as, or -1 for a KeyCode the game's input can't read (which is never drawn).</summary>
        internal static int IdOf(KeyCode code)
        {
            if (JoystickToPad.TryGetValue((int)code, out int pad))
            {
                return pad;
            }
            // One key, two names: the game reads LeftMeta (LeftCommand, LeftApple) as the Windows key, and AltGr as Right
            // Alt (ZInput's map: Key.LeftMeta is Key.LeftWindows, Key.AltGr is Key.RightAlt).
            switch (code)
            {
                case KeyCode.LeftMeta:
                    return (int)KeyCode.LeftWindows;
                case KeyCode.RightMeta:
                    return (int)KeyCode.RightWindows;
                case KeyCode.AltGr:
                    return (int)KeyCode.RightAlt;
            }
            return byId.ContainsKey((int)code) && (int)code < GamepadBase ? (int)code : -1;
        }

        /// <summary>
        /// A binding path as the game and Jötunn keep them ("&lt;Keyboard&gt;/e", "&lt;Keyboard&gt;/LeftCtrl" from the game's
        /// defaults, "&lt;Mouse&gt;/leftButton", "&lt;Gamepad&gt;/buttonSouth") as a key id; -1 for none ("", an axis, or a
        /// control the map doesn't know).
        /// </summary>
        internal static int IdOfPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return -1;
            }
            int slash = path.IndexOf('/');
            if (slash <= 0 || slash == path.Length - 1)
            {
                return -1;
            }
            string device = path.Substring(0, slash).Trim();
            string control = path.Substring(slash + 1).Trim();
            if (device.Equals("<Keyboard>", StringComparison.OrdinalIgnoreCase))
            {
                // The game's own defaults name keys by the Input System's Key enum (KeyToPath): Digit1 as "1", and the
                // Windows and Command keys as Meta. The Input System's own control names are the enum's, lower-cased.
                if (control.StartsWith("digit", StringComparison.OrdinalIgnoreCase) && control.Length == 6)
                {
                    control = control.Substring(5);
                }
                else if (control.Equals("leftMeta", StringComparison.OrdinalIgnoreCase) || control.Equals("leftCommand", StringComparison.OrdinalIgnoreCase) ||
                         control.Equals("leftApple", StringComparison.OrdinalIgnoreCase))
                {
                    control = "leftWindows";   // the Input System's leftMeta is the Windows key
                }
                else if (control.Equals("rightMeta", StringComparison.OrdinalIgnoreCase) || control.Equals("rightCommand", StringComparison.OrdinalIgnoreCase) ||
                         control.Equals("rightApple", StringComparison.OrdinalIgnoreCase))
                {
                    control = "rightWindows";
                }
                else if (control.Equals("altGr", StringComparison.OrdinalIgnoreCase))
                {
                    control = "rightAlt";
                }
                else if (control.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || control.Equals("shift", StringComparison.OrdinalIgnoreCase) ||
                         control.Equals("alt", StringComparison.OrdinalIgnoreCase))
                {
                    // Either side ("<Keyboard>/shift"): the left one stands for it.
                    control = "left" + control;
                }
                return byKeyboardControl.TryGetValue(control, out KeyInfo key) ? key.Id : -1;
            }
            if (device.Equals("<Mouse>", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < MouseButtons.Length; i++)
                {
                    if (Get((int)MouseButtons[i]).Control.Equals(control, StringComparison.OrdinalIgnoreCase))
                    {
                        return (int)MouseButtons[i];
                    }
                }
                return -1;
            }
            if (device.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return byGamepadControl.TryGetValue(control, out KeyInfo pad) ? pad.Id : -1;
            }
            return -1;
        }

        /// <summary>Shift, Ctrl and Alt, either side (and AltGr): keys held with something else. They are never called a
        /// clash, and they are the keys of the modifier row.</summary>
        internal static bool IsModifier(int id)
        {
            switch ((KeyCode)id)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                case KeyCode.LeftControl:
                case KeyCode.RightControl:
                case KeyCode.LeftAlt:
                case KeyCode.RightAlt:
                case KeyCode.AltGr:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The six modifiers a layer can hold, in the order the modifier row and a combination's name use: Ctrl,
        /// Alt, Shift, left before right.</summary>
        internal static readonly KeyCode[] LayerModifiers =
        {
            KeyCode.LeftControl, KeyCode.RightControl, KeyCode.LeftAlt, KeyCode.RightAlt, KeyCode.LeftShift, KeyCode.RightShift,
        };

        /// <summary>A modifier's rank in a combination's name (Ctrl, Alt, Shift, then anything else held).</summary>
        internal static int ModifierRank(int id)
        {
            int index = Array.IndexOf(LayerModifiers, (KeyCode)id);
            return index >= 0 ? index : 10 + (byId.TryGetValue(id, out KeyInfo info) ? info.Order : id);
        }

        internal static bool IsAlt(int id) => id == (int)KeyCode.LeftAlt || id == (int)KeyCode.RightAlt || id == (int)KeyCode.AltGr;

        /// <summary>Why a key isn't offered with these modifiers held (Alt+Tab and Alt+F4 are Windows'), or null.</summary>
        internal static string NotOfferedWith(int id, int[] layer)
        {
            KeyInfo info = Get(id);
            if (info == null)
            {
                return "the game can't read it";
            }
            if (info.NotOffered != null)
            {
                return info.NotOffered;
            }
            if (layer != null && Array.Exists(layer, IsAlt) && (id == (int)KeyCode.Tab || id == (int)KeyCode.F4))
            {
                return "Windows uses it with Alt";
            }
            return null;
        }

        /// <summary>A key's name for lists ("Left Alt", "F8"), or its KeyCode's own name for one the map doesn't know.</summary>
        internal static string NameOf(int id)
        {
            KeyInfo info = Get(id);
            return info != null ? info.Name : ((KeyCode)id).ToString();
        }
    }
}
