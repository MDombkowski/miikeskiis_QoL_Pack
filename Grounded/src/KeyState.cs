using System;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Grounded
{
    /// <summary>
    /// Whether the key works now: held (read live, at the moment the game aims), toggled on (switched by a press and
    /// release while building, from the module's Update), or always. The rules themselves are KeyRules'.
    ///
    /// A key the game's input can't read (Mouse 5 and 6, which it refuses; F13 to F15, Help, SysReq, the shifted
    /// characters, missing from its key map, for which it throws) is never held: the log says once which key, and it
    /// isn't tried again until the setting changes, so a friend's odd key never switches Grounded off.
    ///
    /// Nothing here allocates per frame: the key's parts are taken apart once per value of the setting (BepInEx's
    /// KeyboardShortcut.Modifiers makes a new sequence at each call), and the reads are delegates made once.
    /// </summary>
    internal static class KeyState
    {
        private static readonly SafeKey safeKey = new SafeKey(Readable, Warn);
        private static readonly Func<KeyboardShortcut, bool> HeldRead = _ => ZInput.GetKey(mainKey, false) && ModifiersHeld();
        private static readonly Func<KeyboardShortcut, bool> PressedRead = _ => ZInput.GetKeyDown(mainKey, false) && ModifiersHeld();
        private static readonly Func<KeyboardShortcut, bool> ReleasedRead = _ => ZInput.GetKeyUp(mainKey, false);

        private static bool toggledOn;
        private static bool armed;
        private static int keyVersion;
        private static int cachedVersion = -1;
        private static KeyCode mainKey;
        private static KeyCode[] modifiers = new KeyCode[0];

        internal static bool Active
        {
            get
            {
                KeyMode mode = Settings.HowKeyWorks.Value;
                if (mode == KeyMode.Always)
                {
                    return true;
                }
                KeyboardShortcut key = CurrentKey();
                bool keySet = mainKey != KeyCode.None;
                bool held = mode == KeyMode.Hold && keySet && safeKey.Read(key, keyVersion, HeldRead);
                return KeyRules.IsActive(mode, keySet, held, toggledOn);
            }
        }

        /// <summary>The key setting changed (Settings.Key.SettingChanged): take its parts apart again at the next read.</summary>
        internal static void KeyChanged() => keyVersion++;

        /// <summary>Each frame while the module is on: a press and release of the key while building switches the toggle,
        /// with a message in the middle of the screen. Any other key or mouse button going down in between, or the game
        /// losing focus, cancels it. Outside toggle mode the toggle is off, so it starts off when chosen.</summary>
        internal static void Update()
        {
            KeyMode mode = Settings.HowKeyWorks.Value;
            KeyboardShortcut key = CurrentKey();
            bool keySet = mainKey != KeyCode.None;
            bool pressed = false;
            bool released = false;
            bool building = false;
            bool typing = false;
            bool focused = false;
            bool interrupted = false;
            if (mode == KeyMode.Toggle && keySet)
            {
                pressed = safeKey.Read(key, keyVersion, PressedRead);
                released = safeKey.Read(key, keyVersion, ReleasedRead);
                Player player = Player.m_localPlayer;
                building = player != null && player.InPlaceMode();
                typing = Typing();
                focused = Application.isFocused;
                interrupted = armed && !pressed && OtherInputDown();
            }
            if (KeyRules.ToggleStep(ref armed, ref toggledOn, mode, keySet, pressed, released, building, typing, focused, interrupted) &&
                MessageHud.instance != null)
            {
                QoLMods.Kit.Say.Center(toggledOn ? "Grounded on: rugs lie on the floor" : "Grounded off");
            }
        }

        /// <summary>Switches the toggle off (an undo step, and while the module is off).</summary>
        internal static void Reset()
        {
            toggledOn = false;
            armed = false;
        }

        /// <summary>The key setting's value, its parts taken apart again only after it changed.</summary>
        private static KeyboardShortcut CurrentKey()
        {
            KeyboardShortcut key = Settings.Key.Value;
            if (cachedVersion != keyVersion)
            {
                cachedVersion = keyVersion;
                mainKey = key.MainKey;
                modifiers = key.Modifiers.ToArray();
            }
            return key;
        }

        private static bool ModifiersHeld()
        {
            foreach (KeyCode modifier in modifiers)
            {
                if (!ZInput.GetKey(modifier, false))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A keyboard key or a mouse button went down this frame (the set key's own press frame is left out by
        /// the caller).</summary>
        private static bool OtherInputDown()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
            {
                return true;
            }
            Mouse mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                                     mouse.middleButton.wasPressedThisFrame || mouse.forwardButton.wasPressedThisFrame ||
                                     mouse.backButton.wasPressedThisFrame);
        }

        private static bool Readable(KeyboardShortcut key)
        {
            bool readable = ZInput.IsKeyCodeValid(mainKey);
            foreach (KeyCode modifier in modifiers)
            {
                readable &= ZInput.IsKeyCodeValid(modifier);
            }
            return readable;
        }

        private static void Warn(KeyboardShortcut key, string why) =>
            Module.Log.LogWarning($"The key {key} of the setting \"Key\" can't be read ({why}); it counts as never held " +
                                  "until the setting is changed.");

        /// <summary>Text is being typed, or a menu or the inventory is open: a press is not for building.</summary>
        private static bool Typing() =>
            (Chat.instance != null && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible() ||
            Menu.IsVisible() || InventoryGui.IsVisible();
    }
}
