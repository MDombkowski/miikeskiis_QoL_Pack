using System.Collections.Generic;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>The keyboard the picture draws (the setting "Keyboard").</summary>
    internal enum KeyboardKind
    {
        Full,
        WithoutNumberPad,
        Laptop,
    }

    /// <summary>One key's place on a picture, in key widths ("units") from the picture's top-left corner.</summary>
    internal readonly struct KeySpot
    {
        internal readonly int Id;
        internal readonly float X;
        internal readonly float Y;
        internal readonly float W;
        internal readonly float H;

        internal KeySpot(int id, float x, float y, float w = 1f, float h = 1f)
        {
            Id = id;
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        internal float MiddleX => X + W / 2f;

        internal float MiddleY => Y + H / 2f;
    }

    /// <summary>
    /// Where each key sits on the three keyboards, and the mouse beside them: a US layout, as settings name keys by their
    /// place on one (a key's cap may show another letter on another layout). Plain data, so the harness checks that every
    /// drawn key is one the game can read, and the suggestions measure distances on it.
    /// </summary>
    internal sealed class Layout
    {
        /// <summary>The gap between the keyboard and the mouse, in units.</summary>
        internal const float MouseGap = 0.75f;

        internal readonly List<KeySpot> Keys = new List<KeySpot>();

        /// <summary>The mouse's buttons, from the mouse's own top-left corner; its body is drawn under the buttons.</summary>
        internal readonly List<KeySpot> Mouse = new List<KeySpot>();

        internal float Width;
        internal float Height;

        internal const float MouseWidth = 3.6f;
        internal const float MouseHeight = 4.6f;

        /// <summary>The mouse's body, under its buttons, in the mouse's own units.</summary>
        internal static readonly Rect MouseBody = new Rect(0.6f, 1.6f, 3f, 3f);

        private static readonly Dictionary<KeyboardKind, Layout> made = new Dictionary<KeyboardKind, Layout>();

        internal static Layout Of(KeyboardKind kind)
        {
            if (!made.TryGetValue(kind, out Layout layout))
            {
                layout = kind == KeyboardKind.Laptop ? Laptop() : Desktop(kind == KeyboardKind.Full);
                layout.AddMouse();
                made[kind] = layout;
            }
            return layout;
        }

        /// <summary>A key's spot on this keyboard, if it is drawn on it.</summary>
        internal bool TryGet(int id, out KeySpot spot)
        {
            foreach (KeySpot each in Keys)
            {
                if (each.Id == id)
                {
                    spot = each;
                    return true;
                }
            }
            foreach (KeySpot each in Mouse)
            {
                if (each.Id == id)
                {
                    spot = new KeySpot(each.Id, Width + MouseGap + each.X, each.Y + 1.25f, each.W, each.H);
                    return true;
                }
            }
            spot = default;
            return false;
        }

        private void Add(KeyCode code, float x, float y, float w = 1f, float h = 1f) => Keys.Add(new KeySpot((int)code, x, y, w, h));

        private void Row(string letters, float x, float y)
        {
            foreach (char letter in letters)
            {
                Add((KeyCode)char.ToLowerInvariant(letter), x, y);
                x += 1f;
            }
        }

        private void Digits(float y)
        {
            Add(KeyCode.BackQuote, 0f, y);
            for (int d = 1; d <= 9; d++)
            {
                Add(KeyCode.Alpha0 + d, d, y);
            }
            Add(KeyCode.Alpha0, 10f, y);
            Add(KeyCode.Minus, 11f, y);
            Add(KeyCode.Equals, 12f, y);
            Add(KeyCode.Backspace, 13f, y, 2f);
        }

        private void MainBlock(float top)
        {
            Digits(top);
            Add(KeyCode.Tab, 0f, top + 1f, 1.5f);
            Row("QWERTYUIOP", 1.5f, top + 1f);
            Add(KeyCode.LeftBracket, 11.5f, top + 1f);
            Add(KeyCode.RightBracket, 12.5f, top + 1f);
            Add(KeyCode.Backslash, 13.5f, top + 1f, 1.5f);
            Add(KeyCode.CapsLock, 0f, top + 2f, 1.75f);
            Row("ASDFGHJKL", 1.75f, top + 2f);
            Add(KeyCode.Semicolon, 10.75f, top + 2f);
            Add(KeyCode.Quote, 11.75f, top + 2f);
            Add(KeyCode.Return, 12.75f, top + 2f, 2.25f);
            Add(KeyCode.LeftShift, 0f, top + 3f, 2.25f);
            Row("ZXCVBNM", 2.25f, top + 3f);
            Add(KeyCode.Comma, 9.25f, top + 3f);
            Add(KeyCode.Period, 10.25f, top + 3f);
            Add(KeyCode.Slash, 11.25f, top + 3f);
        }

        // A desktop keyboard: the function row, the main block, the navigation keys and arrows, and (Full) the number pad.
        private static Layout Desktop(bool numberPad)
        {
            var layout = new Layout();
            layout.Add(KeyCode.Escape, 0f, 0f);
            float[] fx = { 2f, 3f, 4f, 5f, 6.5f, 7.5f, 8.5f, 9.5f, 11f, 12f, 13f, 14f };
            for (int f = 0; f < 12; f++)
            {
                layout.Add(KeyCode.F1 + f, fx[f], 0f);
            }
            layout.Add(KeyCode.Print, 15.25f, 0f);
            layout.Add(KeyCode.ScrollLock, 16.25f, 0f);
            layout.Add(KeyCode.Pause, 17.25f, 0f);

            const float top = 1.25f;
            layout.MainBlock(top);
            layout.Add(KeyCode.RightShift, 12.25f, top + 3f, 2.75f);
            float bottom = top + 4f;
            layout.Add(KeyCode.LeftControl, 0f, bottom, 1.25f);
            layout.Add(KeyCode.LeftWindows, 1.25f, bottom, 1.25f);
            layout.Add(KeyCode.LeftAlt, 2.5f, bottom, 1.25f);
            layout.Add(KeyCode.Space, 3.75f, bottom, 6.25f);
            layout.Add(KeyCode.RightAlt, 10f, bottom, 1.25f);
            layout.Add(KeyCode.RightWindows, 11.25f, bottom, 1.25f);
            layout.Add(KeyCode.Menu, 12.5f, bottom, 1.25f);
            layout.Add(KeyCode.RightControl, 13.75f, bottom, 1.25f);

            layout.Add(KeyCode.Insert, 15.25f, top);
            layout.Add(KeyCode.Home, 16.25f, top);
            layout.Add(KeyCode.PageUp, 17.25f, top);
            layout.Add(KeyCode.Delete, 15.25f, top + 1f);
            layout.Add(KeyCode.End, 16.25f, top + 1f);
            layout.Add(KeyCode.PageDown, 17.25f, top + 1f);
            layout.Add(KeyCode.UpArrow, 16.25f, top + 3f);
            layout.Add(KeyCode.LeftArrow, 15.25f, bottom);
            layout.Add(KeyCode.DownArrow, 16.25f, bottom);
            layout.Add(KeyCode.RightArrow, 17.25f, bottom);
            layout.Width = 18.25f;

            if (numberPad)
            {
                const float x = 18.5f;
                layout.Add(KeyCode.Numlock, x, top);
                layout.Add(KeyCode.KeypadDivide, x + 1f, top);
                layout.Add(KeyCode.KeypadMultiply, x + 2f, top);
                layout.Add(KeyCode.KeypadMinus, x + 3f, top);
                layout.Add(KeyCode.Keypad7, x, top + 1f);
                layout.Add(KeyCode.Keypad8, x + 1f, top + 1f);
                layout.Add(KeyCode.Keypad9, x + 2f, top + 1f);
                layout.Add(KeyCode.KeypadPlus, x + 3f, top + 1f, 1f, 2f);
                layout.Add(KeyCode.Keypad4, x, top + 2f);
                layout.Add(KeyCode.Keypad5, x + 1f, top + 2f);
                layout.Add(KeyCode.Keypad6, x + 2f, top + 2f);
                layout.Add(KeyCode.Keypad1, x, top + 3f);
                layout.Add(KeyCode.Keypad2, x + 1f, top + 3f);
                layout.Add(KeyCode.Keypad3, x + 2f, top + 3f);
                layout.Add(KeyCode.KeypadEnter, x + 3f, top + 3f, 1f, 2f);
                layout.Add(KeyCode.Keypad0, x, bottom, 2f);
                layout.Add(KeyCode.KeypadPeriod, x + 2f, bottom);
                layout.Width = 22.5f;
            }
            layout.Height = bottom + 1f;
            return layout;
        }

        // A laptop: the function row as wide as the main block, Insert and Delete at its end; Home, Page Up, Page Down and
        // End in a column to the right; the arrows tucked under a short right Shift. No Print Screen, Scroll Lock or Pause
        // (most laptops reach them with Fn): they are still listed when something uses them.
        private static Layout Laptop()
        {
            var layout = new Layout();
            layout.Add(KeyCode.Escape, 0f, 0f);
            for (int f = 0; f < 12; f++)
            {
                layout.Add(KeyCode.F1 + f, 1f + f, 0f);
            }
            layout.Add(KeyCode.Insert, 13f, 0f);
            layout.Add(KeyCode.Delete, 14f, 0f);

            const float top = 1f;
            layout.MainBlock(top);
            layout.Add(KeyCode.RightShift, 12.25f, top + 3f, 0.75f);
            layout.Add(KeyCode.UpArrow, 13f, top + 3f);
            float bottom = top + 4f;
            layout.Add(KeyCode.LeftControl, 0f, bottom, 1.25f);
            layout.Add(KeyCode.LeftWindows, 1.25f, bottom);
            layout.Add(KeyCode.LeftAlt, 2.25f, bottom, 1.25f);
            layout.Add(KeyCode.Space, 3.5f, bottom, 5.5f);
            layout.Add(KeyCode.RightAlt, 9f, bottom);
            layout.Add(KeyCode.Menu, 10f, bottom);
            layout.Add(KeyCode.RightControl, 11f, bottom);
            layout.Add(KeyCode.LeftArrow, 12f, bottom);
            layout.Add(KeyCode.DownArrow, 13f, bottom);
            layout.Add(KeyCode.RightArrow, 14f, bottom);

            layout.Add(KeyCode.Home, 15.25f, top);
            layout.Add(KeyCode.PageUp, 15.25f, top + 1f);
            layout.Add(KeyCode.PageDown, 15.25f, top + 2f);
            layout.Add(KeyCode.End, 15.25f, top + 3f);
            layout.Width = 16.25f;
            layout.Height = bottom + 1f;
            return layout;
        }

        // The mouse: two big buttons, the wheel between them, the side buttons on its left (Mouse3 above Mouse4).
        private void AddMouse()
        {
            Mouse.Add(new KeySpot((int)KeyCode.Mouse0, 0.6f, 0f, 1.2f, 1.6f));
            Mouse.Add(new KeySpot((int)KeyCode.Mouse2, 1.8f, 0.3f, 0.6f, 1f));
            Mouse.Add(new KeySpot((int)KeyCode.Mouse1, 2.4f, 0f, 1.2f, 1.6f));
            Mouse.Add(new KeySpot((int)KeyCode.Mouse3, 0f, 1.9f, 0.5f, 0.9f));
            Mouse.Add(new KeySpot((int)KeyCode.Mouse4, 0f, 2.9f, 0.5f, 0.9f));
        }
    }
}
