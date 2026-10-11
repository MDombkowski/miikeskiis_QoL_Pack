using System.Collections.Generic;
using UnityEngine;

namespace ComfortBuild
{
    /// <summary>
    /// The comfort pieces standing near him, and what they give, as the game counts them: every loaded comfort piece
    /// whose origin is less than 10 m from his feet (Piece.GetAllComfortPiecesInRadius, the game's own search, which
    /// leaves out the placement ghost), whoever built it. Taken again every second while he builds, and whenever the
    /// build menu opens, so what it says follows him as he walks.
    /// </summary>
    internal static class Nearby
    {
        /// <summary>The game's comfort radius (SE_Rested passes 10 to the search itself).</summary>
        internal const float Radius = 10f;

        /// <summary>How often the look is taken again while he builds, in seconds.</summary>
        internal const float Every = 1f;

        private static readonly List<Piece> Found = new List<Piece>();
        private static readonly List<Piece> PiecesNow = new List<Piece>();
        private static readonly List<Thing> ThingsNow = new List<Thing>();
        private static float nextLook;
        private static int version;
        private static string lastKey = string.Empty;
        private static bool warnedMismatch;

        /// <summary>The pieces near him, as the count takes them (unlit fires give 0 unless "Count unlit fires as lit").</summary>
        internal static IList<Thing> Things => ThingsNow;

        /// <summary>The pieces themselves, in the same order as <see cref="Things"/>.</summary>
        internal static IList<Piece> Pieces => PiecesNow;

        /// <summary>Whether he stands under shelter now (the game's own test).</summary>
        internal static bool Sheltered { get; private set; }

        /// <summary>His comfort here as if he were sheltered: what the numbers and the grey are measured against.</summary>
        internal static int Level { get; private set; } = ComfortMath.ShelteredBase;

        /// <summary>Goes up each time what is near him changes, so the menu knows to draw again.</summary>
        internal static int Version => version;

        /// <summary>Takes the look again if it's due (or <paramref name="now"/>); true when what's near him changed.</summary>
        internal static bool Look(bool now)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }
            if (!now && Time.unscaledTime < nextLook)
            {
                return false;
            }
            nextLook = Time.unscaledTime + Every;
            bool unlitAsLit = Settings.UnlitAsLit.Value;
            Found.Clear();
            Piece.GetAllComfortPiecesInRadius(player.transform.position, Radius, Found);
            PiecesNow.Clear();
            ThingsNow.Clear();
            var key = new System.Text.StringBuilder();
            foreach (Piece piece in Found)
            {
                if (piece == null)
                {
                    continue;
                }
                int gives = unlitAsLit ? piece.m_comfort : piece.GetComfort();
                PiecesNow.Add(piece);
                ThingsNow.Add(new Thing(piece.m_name, (int)piece.m_comfortGroup, gives));
                key.Append(piece.GetInstanceID()).Append(':').Append(gives).Append(';');
            }
            Sheltered = player.InShelter();
            Level = ComfortMath.ShelteredBase + ComfortMath.Sum(ThingsNow);
            key.Append(Sheltered ? 'S' : 'U');
            string newKey = key.ToString();
            if (newKey == lastKey)
            {
                return false;
            }
            lastKey = newKey;
            version++;
            return true;
        }

        /// <summary>Forgets the look (a new world, or the module switched off).</summary>
        internal static void Forget()
        {
            Found.Clear();
            PiecesNow.Clear();
            ThingsNow.Clear();
            lastKey = string.Empty;
            nextLook = 0f;
            Level = ComfortMath.ShelteredBase;
            version++;
        }

        /// <summary>What a piece from the build menu would add here: its own comfort, as it will be once lit.</summary>
        internal static int Gain(Piece candidate) => ComfortMath.Gain(ThingsNow, AsCandidate(candidate));

        internal static Thing AsCandidate(Piece piece) => new Thing(piece.m_name, (int)piece.m_comfortGroup, piece.m_comfort);

        /// <summary>
        /// Checks the count against the game's own, once a session, while he is sheltered: a mod that changes comfort's
        /// rules (its radius, its groups) would make the numbers here wrong, and the log then says so (his acceptance: the
        /// total matches the game's own "Comfort: N" on the Resting icon). The game recounts only every 2 seconds of game
        /// time, and not at all while it is paused, so a difference must last 6 seconds of game time (Time.time, which
        /// stops with the game).
        /// </summary>
        internal static void CompareWithGame(ref float differentSince)
        {
            Player player = Player.m_localPlayer;
            if (warnedMismatch || player == null || !Sheltered || Settings.UnlitAsLit.Value)
            {
                differentSince = -1f;
                return;
            }
            int game = player.GetComfortLevel();
            if (game == Level)
            {
                differentSince = -1f;
                return;
            }
            if (differentSince < 0f)
            {
                differentSince = Time.time;
                return;
            }
            if (Time.time - differentSince > 6f)
            {
                warnedMismatch = true;
                Module.Log.LogWarning($"Comfort here counts {Level}, the game says {game}, for more than 6 seconds while sheltered. " +
                                      "Another mod may change how comfort is counted; the numbers and the grey follow the game's own rule.");
            }
        }
    }
}
