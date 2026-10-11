using System.Collections.Generic;

namespace Handyman
{
    /// <summary>
    /// The piece each build tool last had picked before Repair, and its turn, for the way back. The game keeps one slot
    /// per build-menu tab and picking Repair overwrites one, and a piece that turns at random gets a new turn when picked
    /// again (research 2026-09-29, section 1), so the piece itself and its turn are kept here, per tool (per piece table:
    /// two hammers share one, as they share the game's own selection). Only for this session; nothing is saved.
    /// Tools and pieces are compared by reference, never by Unity's equality, so it can be checked outside the game.
    /// </summary>
    internal sealed class ToolMemory<TTool, TPiece> where TTool : class where TPiece : class
    {
        private sealed class Entry
        {
            internal TTool Tool;
            internal TPiece Piece;
            internal int Turn;
        }

        private readonly List<Entry> entries = new List<Entry>();

        /// <summary>The tool has this piece picked, turned this way (a piece other than Repair).</summary>
        internal void Note(TTool tool, TPiece piece, int turn)
        {
            if (tool == null || piece == null)
            {
                return;
            }
            Entry entry = Find(tool);
            if (entry == null)
            {
                entry = new Entry { Tool = tool };
                entries.Add(entry);
            }
            entry.Piece = piece;
            entry.Turn = turn;
        }

        internal bool TryGet(TTool tool, out TPiece piece, out int turn)
        {
            Entry entry = tool == null ? null : Find(tool);
            piece = entry?.Piece;
            turn = entry?.Turn ?? 0;
            return piece != null;
        }

        /// <summary>Forget a tool's piece (it can no longer be picked).</summary>
        internal void Forget(TTool tool)
        {
            entries.RemoveAll(entry => ReferenceEquals(entry.Tool, tool));
        }

        internal void Clear() => entries.Clear();

        private Entry Find(TTool tool)
        {
            foreach (Entry entry in entries)
            {
                if (ReferenceEquals(entry.Tool, tool))
                {
                    return entry;
                }
            }
            return null;
        }
    }
}
