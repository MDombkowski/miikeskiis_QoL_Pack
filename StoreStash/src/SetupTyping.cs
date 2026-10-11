using System;

namespace StoreStash
{
    /// <summary>
    /// The setup window's typing guard (card A41's second round, decision 24): while one of its text boxes (the chest's
    /// name, the search) has the keyboard, the game's own keys wait. Jötunn's GUIManager.BlockInput does it: E, Tab and Esc
    /// don't close the inventory (it skips InventoryGui.Update's close keys), the character takes no keys
    /// (Player.TakeInput), and TextInput.IsVisible says yes, so StoreStash's own keys stand aside too (QuickStack.TakesKeys,
    /// SetupKeys). BlockInput is one count every mod shares, so the guard asks once while typing and gives back once: the
    /// frame after the box lets go (so the Enter or Esc that ended the typing can't reach the inventory's close keys in the
    /// same frame), and at once whenever the window closes (Release, from SetupWindow.Close, also the module's undo step).
    /// Its own state only, with BlockInput handed in, so the scratch harness runs it on a counter.
    /// </summary>
    internal sealed class TypingGuard
    {
        private readonly Action<bool> block;

        // The frame the boxes were first seen without the keyboard while the guard still held the keys; -1 while typing.
        private int letGo = -1;

        internal TypingGuard(Action<bool> block)
        {
            this.block = block;
        }

        /// <summary>True while it holds the game's keys (one BlockInput(true) not yet given back).</summary>
        internal bool Blocking { get; private set; }

        /// <summary>A box has the keyboard, or is about to (selected by a click): the keys wait from now.</summary>
        internal void Hold()
        {
            letGo = -1;
            if (!Blocking)
            {
                Blocking = true;
                block(true);
            }
        }

        /// <summary>Every frame: whether a box has the keyboard now. The keys come back the frame after it lets go.</summary>
        internal void Tick(bool typing, int frame)
        {
            if (typing)
            {
                Hold();
                return;
            }
            if (!Blocking)
            {
                return;
            }
            if (letGo < 0)
            {
                letGo = frame;
            }
            else if (frame > letGo)
            {
                Release();
            }
        }

        /// <summary>Gives the keys back now, if it holds them: once, however often it's asked.</summary>
        internal void Release()
        {
            letGo = -1;
            if (Blocking)
            {
                Blocking = false;
                block(false);
            }
        }
    }

    /// <summary>
    /// The setup window's search box (decision 24), as Add to Cart's filter box works: what he types is searched once he
    /// pauses (Pause after the last letter), and a search that starts from an empty box shows everything first (the list
    /// goes to All), so typing finds things across all of them; a family or "This chest's setup" picked after that searches
    /// within it, and typing more keeps that pick. Plain values only (the time is handed in), for the scratch harness.
    /// </summary>
    internal sealed class SetupSearch
    {
        /// <summary>How long after the last letter the search runs, in seconds.</summary>
        internal const float Pause = 0.25f;

        private string typed = string.Empty;
        private float due = -1f;

        /// <summary>The words searched for now (trimmed; empty: no search).</summary>
        internal string Query { get; private set; } = string.Empty;

        /// <summary>True while what he typed waits for him to pause.</summary>
        internal bool Waiting => due >= 0f;

        /// <summary>The box's text changed (each letter).</summary>
        internal void Typed(string text, float now)
        {
            typed = (text ?? string.Empty).Trim();
            due = now + Pause;
        }

        /// <summary>Whether what he typed is due to be searched now.</summary>
        internal bool Due(float now) => due >= 0f && now >= due;

        /// <summary>Searches what he typed, at once (he paused, or clicked the list: a search still waiting goes first, so
        /// it can't undo his click). False when nothing was waiting; <paramref name="starts"/> when a search starts from
        /// an empty one, and the list should show everything.</summary>
        internal bool Apply(out bool starts)
        {
            starts = false;
            if (due < 0f)
            {
                return false;
            }
            due = -1f;
            starts = Query.Length == 0 && typed.Length > 0;
            Query = typed;
            return true;
        }

        /// <summary>The box emptied at once (its x, or the window opening for another chest).</summary>
        internal void Clear()
        {
            typed = string.Empty;
            Query = string.Empty;
            due = -1f;
        }
    }
}
