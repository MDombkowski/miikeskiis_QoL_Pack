using System;
using BepInEx.Configuration;

namespace Grounded
{
    /// <summary>
    /// Reads a key so that one the game's input can't read never stops the mod: a key it refuses (<c>readable</c> says
    /// no) or whose read throws counts as not down, is reported once through <c>warn</c>, and isn't tried again until the
    /// key setting changes. The caller numbers the setting's values (<c>version</c> goes up each time it changes), so
    /// whether the key is readable is asked once per value, not every frame. Plain code: the scratch harness runs it
    /// with stand-in reads.
    /// </summary>
    internal sealed class SafeKey
    {
        private readonly Func<KeyboardShortcut, bool> readable;
        private readonly Action<KeyboardShortcut, string> warn;
        private int checkedVersion = int.MinValue;
        private bool failed;

        internal SafeKey(Func<KeyboardShortcut, bool> readable, Action<KeyboardShortcut, string> warn)
        {
            this.readable = readable;
            this.warn = warn;
        }

        internal bool Read(KeyboardShortcut key, int version, Func<KeyboardShortcut, bool> read)
        {
            if (version != checkedVersion)
            {
                checkedVersion = version;
                failed = false;
                if (!readable(key))
                {
                    Fail(key, "the game's input doesn't read it (of the mouse buttons, it reads Mouse 0 to Mouse 4)");
                }
            }
            if (failed)
            {
                return false;
            }
            try
            {
                return read(key);
            }
            catch (Exception error)
            {
                // Keys missing from the game's own key map (F13 to F15, Help, SysReq, the shifted characters) make its
                // input throw.
                Fail(key, error.Message);
                return false;
            }
        }

        private void Fail(KeyboardShortcut key, string why)
        {
            failed = true;
            warn(key, why);
        }
    }
}
