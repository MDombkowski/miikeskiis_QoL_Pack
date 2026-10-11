using System;
using QoLMods.Catalogue;
using QoLMods.Chests;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// Copying a chest's setup and pasting it onto another (card A41, his "Control-C and Control-V ... so that I don't have
    /// to do that every single time"): from the setup window's buttons, or Ctrl+C and Ctrl+V while a chest's window is open
    /// (SetupKeys). The copy is kept in the game for the session; with "Copy to the clipboard too", Copy also puts it on the
    /// computer's clipboard as a line of text (to send to a friend), and Paste reads a setup from there when nothing was
    /// copied in the game. Pasting replaces the chest's setup, or with the setting adds to it, and says what it now takes.
    /// </summary>
    internal static class SetupClipboard
    {
        /// <summary>The setup copied in this game, as its text; null before the first copy.</summary>
        internal static string Copied { get; private set; }

        /// <summary>The computer's clipboard, through Unity. Only the scratch test harness sets another.</summary>
        internal static Func<string> ReadClipboard = () => GUIUtility.systemCopyBuffer;

        internal static Action<string> WriteClipboard = text => GUIUtility.systemCopyBuffer = text;

        /// <summary>Copies the setup of a chest (whose window is open).</summary>
        internal static void Copy(Container chest)
        {
            if (chest == null || !Setups.Settable(chest))
            {
                return;
            }
            ItemPicks picks = ItemPicks.Parse(ChestSetup.Read(chest));
            if (picks == null || picks.IsEmpty)
            {
                Messages.Center("This chest has no setup to copy.");
                return;
            }
            if (!picks.Readable)
            {
                Messages.Center(picks.TooLong
                    ? "This chest's setup is longer than a setup can be (another mod's, or another version's): it can't be copied."
                    : "This chest was set up by a newer version of the pack: update StoreStash to copy it.");
                return;
            }
            Copied = picks.Text();
            if (SetupSettings.SystemClipboard.Value)
            {
                try
                {
                    WriteClipboard(Copied);
                }
                catch (Exception error)
                {
                    Module.Log.LogWarning($"Couldn't put the setup on the clipboard: {error.Message}");
                }
            }
            Messages.Center($"Setup copied: {Setups.Describe(picks, 4)}");
        }

        /// <summary>Gives a chest (whose window is open, so it's his) the setup copied: in its place, or added to it.</summary>
        internal static void Paste(Container chest)
        {
            if (chest == null || !Setups.Settable(chest))
            {
                return;
            }
            if (!QoLMods.Chests.ChestAccess.MayTouch(chest))
            {
                Messages.Center($"{Mine.Whose(chest)} personal chest: look, don't touch.");   // card A80: its setup is theirs
                return;
            }
            ItemPicks from = Source(out string why);
            if (from == null)
            {
                Messages.Center(why);
                return;
            }
            ItemPicks now = ItemPicks.Parse(ChestSetup.Read(chest));
            if (now != null && !now.Readable && !now.TooLong)
            {
                Messages.Center("This chest was set up by a newer version of the pack: update StoreStash to change it.");
                return;
            }
            ItemPicks result = Combine(now, from, SetupSettings.Paste.Value, out bool whole);
            if (!Setups.Write(chest, result.IsEmpty ? null : result))
            {
                Messages.Center("Couldn't paste: the chest isn't yours right now. Try again in a moment.");
                return;
            }
            Messages.Center(whole
                ? $"Setup pasted: this chest takes {Setups.Describe(result, 4)}"
                : $"Setup pasted, but not all of it fitted (a setup holds only so much): this chest takes {Setups.Describe(result, 4)}");
        }

        /// <summary>What pasting makes of a chest's setup (null: none) and the one copied: the copy in its place, whole (the
        /// parts a newer version wrote too), or both together; <paramref name="whole"/> false when adding didn't fit. Adding
        /// puts the copy's picks, excepts and biomes beside the chest's, so it isn't always more: biomes then narrow every
        /// family, member and kind of both, and an except of the copy's leaves that thing out of both. A setup this version
        /// can't read (too long to follow) is replaced.</summary>
        internal static ItemPicks Combine(ItemPicks now, ItemPicks copied, PasteMode mode, out bool whole)
        {
            whole = true;
            if (mode != PasteMode.Add || now == null || !now.Readable)
            {
                return copied.Copy();
            }
            ItemPicks result = now.Copy();
            whole = result.AddAll(copied);
            return result;
        }

        // The setup to paste: the one copied in the game, else (with the setting) one on the clipboard; null and why not.
        private static ItemPicks Source(out string why)
        {
            why = null;
            string text = Copied;
            if (text == null && SetupSettings.SystemClipboard.Value)
            {
                try
                {
                    text = ReadClipboard()?.Trim();
                }
                catch (Exception)
                {
                    text = null;
                }
                if (string.IsNullOrEmpty(text))
                {
                    why = "Nothing copied yet: copy a chest's setup first (Ctrl+C with its window open).";
                    return null;
                }
                // Any version word this one reads ("picks2" leaves a quick pick out: card A81).
                if (text.IndexOf('\n') >= 0 || !ItemPicks.LooksLikeSetup(text))
                {
                    why = "Your clipboard doesn't hold a chest's setup.";
                    return null;
                }
                if (text.Length > ItemPicks.MaxLength)
                {
                    why = "The setup on your clipboard is too long to be a chest's.";
                    return null;
                }
            }
            if (text == null)
            {
                why = "Nothing copied yet: copy a chest's setup first (Ctrl+C with its window open).";
                return null;
            }
            ItemPicks picks = ItemPicks.Parse(text);
            if (picks == null || picks.IsEmpty || !picks.Readable)
            {
                why = "That setup can't be pasted here.";
                return null;
            }
            return picks;
        }
    }
}
