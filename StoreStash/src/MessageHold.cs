using System;
using TMPro;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// Keeps a message after a quick stack fully shown a little longer for each line after its first (card A41's second
    /// round, decision 26: "Seconds per extra line", at most "Longest on screen" in all), then lets it fade as the game's
    /// own do. The game shows a message by queueing two fades on its text (MessageHud.ShowMessage: to full at once, then to
    /// nothing over 4 seconds), applied a frame apart; top-left messages wait their turn in a queue, at least a second
    /// apart. So once the text shows this message, the hold puts it back to full whenever it has begun to fade, until its
    /// time is up, and then starts the game's own 4-second fade once. It stops at once, touching nothing more, when the
    /// text shows another message (that one fades its own way), or when this one never showed within half a minute; when
    /// he hides the HUD it stops and hides the message at once (the game hides every message itself, but not while a
    /// message asked to show anyway, as a gamepad's do). A one-line message is never held, so out of the box
    /// every message looks as the game's do (MODULES.md rule 4). Only Messages.AfterQuickStack starts one.
    /// </summary>
    internal static class MessageHold
    {
        /// <summary>How long the game's own fade takes.</summary>
        internal const float GameFade = 4f;

        /// <summary>How long a message may wait for its turn top left before the hold gives up on it.</summary>
        internal const float WaitAtMost = 30f;

        // Seams: the game's message texts and clock. Only the scratch test harness sets others (the HUD needs the engine).
        internal static Func<MessageHud.MessageType, string> ShownText = type => TextOf(type)?.text;
        internal static Func<MessageHud.MessageType, float> ShownAlpha = type => TextOf(type)?.canvasRenderer.GetAlpha() ?? 0f;
        internal static Action<MessageHud.MessageType, float, float> Fade = (type, alpha, seconds) => TextOf(type)?.CrossFadeAlpha(alpha, seconds, true);
        internal static Func<bool> HudHidden = Hud.IsUserHidden;
        internal static Func<float> Clock = () => Time.unscaledTime;
        internal static Func<string, string> Localize = text => Localization.instance != null ? Localization.instance.Localize(text) : text;

        private enum Stage
        {
            None,
            Waiting,
            Holding,
        }

        private static Stage stage;
        private static MessageHud.MessageType where;
        private static string ours;
        private static float seconds;
        private static float since;

        /// <summary>Whether a hold is under way (waiting for its message to show, or holding it).</summary>
        internal static bool Busy => stage != Stage.None;

        /// <summary>
        /// How long a message of <paramref name="lines"/> lines stays fully shown before the game's fade: each line after
        /// the first adds <paramref name="perExtraLine"/> seconds, and with the fade it stays within
        /// <paramref name="longest"/> seconds on screen. Never less than nothing: one line, or a setting of 0, holds nothing.
        /// </summary>
        internal static float Seconds(int lines, float perExtraLine, float longest)
        {
            float hold = Math.Min(perExtraLine * (lines - 1), longest - GameFade);
            return hold > 0f ? hold : 0f;   // also for a NaN from a hand-edited file
        }

        /// <summary>The lines of a message: one more than its line breaks.</summary>
        internal static int Lines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            int lines = 1;
            foreach (char letter in text)
            {
                if (letter == '\n')
                {
                    lines++;
                }
            }
            return lines;
        }

        /// <summary>Called right after a message after a quick stack was sent (Messages.AfterQuickStack): holds it as the
        /// settings say, ending the hold on the one before.</summary>
        internal static void Start(MessageHud.MessageType type, string text)
        {
            Release();
            float hold = Seconds(Lines(text), Settings.SecondsPerExtraLine.Value, Settings.LongestOnScreen.Value);
            if (hold <= 0f)
            {
                return;
            }
            where = type;
            ours = Localize(text) ?? text;   // the game shows a message localised ($ words)
            seconds = hold;
            since = Clock();
            stage = Stage.Waiting;
        }

        /// <summary>Every frame (Module.Update): waits for the message to show, keeps it fully shown, and lets it fade.</summary>
        internal static void Tick()
        {
            if (stage == Stage.None)
            {
                return;
            }
            if (!Module.IsOn)
            {
                Release();
                return;
            }
            if (HudHidden())
            {
                // The HUD hidden: the game hides its messages itself, but not while a message asked to show anyway (a
                // gamepad's), so ours, held fully shown, is hidden here at once (review round 1).
                if (stage == Stage.Holding && ShownText(where) is string held && Ours(held))
                {
                    Fade(where, 0f, 0f);
                }
                stage = Stage.None;
                return;
            }
            string shown = ShownText(where);
            if (shown == null)
            {
                stage = Stage.None;   // no HUD at all
                return;
            }
            bool isOurs = Ours(shown);
            float now = Clock();
            if (stage == Stage.Waiting)
            {
                if (!isOurs)
                {
                    if (now - since > WaitAtMost)
                    {
                        stage = Stage.None;   // a long queue top left: never held
                    }
                    return;
                }
                stage = Stage.Holding;
                since = now;
            }
            else if (!isOurs)
            {
                stage = Stage.None;   // a newer message: it fades its own way
                return;
            }
            if (now - since >= seconds)
            {
                Fade(where, 0f, GameFade);
                stage = Stage.None;
                return;
            }
            if (ShownAlpha(where) < 1f)
            {
                Fade(where, 1f, 0f);   // its fade began: back to full (this also stops that fade)
            }
        }

        /// <summary>Ends a hold now (the module switched off, its undo step, or a newer message of ours): a message still held
        /// fully shown starts the game's own fade, since the hold stopped that one; one not shown yet is left to the game.</summary>
        internal static void Release()
        {
            if (stage == Stage.Holding)
            {
                string shown = ShownText(where);
                if (shown != null && Ours(shown))
                {
                    Fade(where, 0f, GameFade);
                }
            }
            stage = Stage.None;
        }

        // The text shows this message: as sent, or with the count the game adds to a repeat (" x2").
        private static bool Ours(string shown)
        {
            if (ours == null || !shown.StartsWith(ours, StringComparison.Ordinal))
            {
                return false;
            }
            if (shown.Length == ours.Length)
            {
                return true;
            }
            string rest = shown.Substring(ours.Length);
            if (rest.Length < 3 || !rest.StartsWith(" x", StringComparison.Ordinal))
            {
                return false;
            }
            for (int i = 2; i < rest.Length; i++)
            {
                if (!char.IsDigit(rest[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>The game's text for messages of that kind; null without a HUD (the kit's, which measures with it).</summary>
        internal static TMP_Text TextOf(MessageHud.MessageType type) => QoLMods.Kit.Say.TextOf(type);
    }
}
