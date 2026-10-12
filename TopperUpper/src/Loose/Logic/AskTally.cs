using System.Collections.Generic;

namespace TopperUpper.Loose
{
    /// <summary>
    /// The repeat ownership requests the log fix skipped, written as one line a minute while there were any (code review
    /// m2): the freeze research's tests count the game's "wants to pickup" lines, and with the fix on those repeats leave no
    /// line of their own, so the scale stays in the log ("Skipped 812 repeat ownership requests … from 1 asker"). A minute
    /// is counted from the first skip after the last line.
    /// </summary>
    internal sealed class AskTally
    {
        /// <summary>How often a line is written while asks are skipped, in seconds.</summary>
        internal const double Every = 60.0;

        private readonly HashSet<long> askers = new HashSet<long>();
        private int count;
        private double since;

        internal int Count => count;

        internal void Add(long asker, double now)
        {
            if (count == 0)
            {
                since = now;
            }
            count++;
            askers.Add(asker);
        }

        /// <summary>The line to write now, once a minute has passed since the first ask it counts; null otherwise. Starts
        /// counting afresh after it.</summary>
        internal string Due(double now)
        {
            if (count == 0 || now - since < Every)
            {
                return null;
            }
            string line = $"Skipped {count} repeat ownership request{(count == 1 ? string.Empty : "s")} for items on the ground in the " +
                          $"last minute, from {askers.Count} asker{(askers.Count == 1 ? string.Empty : "s")}, without the game's log lines for " +
                          "them (\"Skip the log line for a repeat ownership request\"; switch it off to see each one).";
            Clear();
            return line;
        }

        internal void Clear()
        {
            count = 0;
            askers.Clear();
        }
    }

    /// <summary>
    /// The server's own pickup, still on (code review n5; the freeze research, section 8.9): a vanilla server never asks a
    /// player's game for an item on the ground, so an ask from the server for one this game owns means a server mod picks
    /// items up (on his friend's server that was ServersideQoL's AutoStore, uninstalled on 2026-10-10; miikeskii's
    /// Server-Side QoL Pack never asks). Said once a session in the log, and once on screen while "Pick up loose items into
    /// chests" is on, so the server's owner can be asked to switch it off. The words name no mod (card B05, his rule 8).
    /// </summary>
    internal sealed class ServerPickup
    {
        private bool logged;
        private bool shown;

        /// <summary>What to say this frame: the log line the first time it's seen, the screen line the first time it's
        /// seen while the pickup is on.</summary>
        internal (bool Log, bool Screen) Next(bool seen, bool pickupOn)
        {
            if (!seen)
            {
                return (false, false);
            }
            bool log = !logged;
            bool screen = pickupOn && !shown;
            logged = true;
            shown |= screen;
            return (log, screen);
        }

        /// <summary>A new world's session: it may be said again.</summary>
        internal void Reset()
        {
            logged = false;
            shown = false;
        }
    }
}
