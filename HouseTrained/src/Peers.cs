using System.Collections.Generic;

namespace HouseTrained
{
    /// <summary>
    /// Which games deal friendly fire too (decision 21), from their hellos: a game counts while it said hello in the last
    /// <see cref="Lasts"/> seconds and its hello said its friendly fire is on; this game always counts (it asks only while
    /// its own is on). Plain values, so the harness checks it; the hello itself, on the game's routed messages, is Hello's.
    /// </summary>
    internal sealed class Peers
    {
        /// <summary>A hello counts for this long, in seconds (three of the 60-second hellos).</summary>
        internal const float Lasts = 180f;

        private readonly Dictionary<long, Heard> heard = new Dictionary<long, Heard>();

        internal int Count => heard.Count;

        /// <summary>That game said hello at <paramref name="now"/>, with its friendly fire on or off.</summary>
        internal void Hear(long game, float now, bool friendlyFire) => heard[game] = new Heard(now, friendlyFire);

        /// <summary>When that game last said hello, or null if it hasn't (or not since the last Clear).</summary>
        internal float? LastHeard(long game) => heard.TryGetValue(game, out Heard at) ? at.At : (float?)null;

        /// <summary>Whether that game deals friendly fire now: this game always does; another while its last hello is
        /// fresh and said so.</summary>
        internal bool Deals(long game, long self, float now) =>
            game == self || (heard.TryGetValue(game, out Heard at) && at.FriendlyFire && now - at.At <= Lasts);

        /// <summary>Everything forgotten: a new connection.</summary>
        internal void Clear() => heard.Clear();

        private readonly struct Heard
        {
            internal readonly float At;
            internal readonly bool FriendlyFire;

            internal Heard(float at, bool friendlyFire)
            {
                At = at;
                FriendlyFire = friendlyFire;
            }
        }
    }
}
