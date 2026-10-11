using System;
using UnityEngine;

namespace AFKRaidBlocker
{
    /// <summary>
    /// What a player's game tells other players' games, on his own character's record (the game's own way of sharing a
    /// character's state; a game without the mod ignores it, and an unmodded server only passes it on):
    /// - whether he is away (always, while the mod is on), so that another player's copy of the mod counts him as away
    ///   for "everyone near is away" (Nearby);
    /// - whether he lets others see it, for the away mark: a word in grey beside his name over his head, added by each
    ///   game with the mod after the game has written the name, every frame.
    ///
    /// The mark is a nicety: an error showing it switches only the showing off for the session, and an error writing
    /// switches only the writing off, each with one line in the log, never the mod (which holds off the raids).
    /// </summary>
    internal static class Mark
    {
        /// <summary>The flags on a character's record. Permanent: older and newer versions must read the same.</summary>
        internal const string AwayKey = "modprojects.afkraidblocker.away";

        internal const string ShowKey = "modprojects.afkraidblocker.showmark";

        private static readonly int AwayHash = AwayKey.GetStableHashCode();
        private static readonly int ShowHash = ShowKey.GetStableHashCode();

        private static bool showingBroken;
        private static bool writingBroken;

        /// <summary>Sets both flags on his own character's record to what's wanted, writing only what differs.</summary>
        internal static void WriteOwn(Player player, bool away, bool show)
        {
            if (writingBroken || player == null)
            {
                return;
            }
            try
            {
                ZDO record = RecordOf(player);
                if (record == null || !record.IsOwner())
                {
                    return;
                }
                if (record.GetBool(AwayHash) != away)
                {
                    record.Set(AwayHash, away);
                }
                if (record.GetBool(ShowHash) != show)
                {
                    record.Set(ShowHash, show);
                }
            }
            catch (Exception error)
            {
                writingBroken = true;
                Module.Log.LogWarning($"Switched the away flags off for this session after an error writing them; raids " +
                                      $"are still held off as before. {error.GetType().Name}: {error.Message}");
                TryClear(player);
            }
        }

        /// <summary>Whether another player's game says he's away (false for a game without the mod, or the mod off).</summary>
        internal static bool IsAway(Player other)
        {
            ZDO record = RecordOf(other);
            return record != null && record.GetBool(AwayHash);
        }

        /// <summary>After the game has written the names over heads: the mark beside each player who is away and shows it.</summary>
        internal static void OnHuds(EnemyHud hud)
        {
            if (showingBroken || hud == null)
            {
                return;
            }
            try
            {
                string text = (Settings.MarkText.Value ?? "").Trim();
                if (text.Length == 0)
                {
                    return;
                }
                Player self = Player.m_localPlayer;
                foreach (Player other in Player.GetAllPlayers())
                {
                    if (other == null || other == self)
                    {
                        continue;
                    }
                    ZDO record = RecordOf(other);
                    if (record == null || !record.GetBool(AwayHash) || !record.GetBool(ShowHash))
                    {
                        continue;
                    }
                    TMPro.TMP_Text label = GameHooks.NameLabel(hud, other);
                    if (label != null)
                    {
                        label.text = Marked(label.text, text);
                    }
                }
            }
            catch (Exception error)
            {
                showingBroken = true;
                Module.Log.LogWarning($"Switched showing other players' away marks off for this session after an error; " +
                                      $"raids are still held off as before. {error.GetType().Name}: {error.Message}");
            }
        }

        /// <summary>A name with the mark after it. The game writes the name again every frame, so this never piles up;
        /// a name already marked (another call in the same frame) is left as it is.</summary>
        internal static string Marked(string name, string text)
        {
            string mark = Suffix(text);
            name = name ?? "";
            return name.EndsWith(mark, StringComparison.Ordinal) ? name : name + mark;
        }

        internal static string Suffix(string text) => " <color=#A8A8A8>(" + text + ")</color>";

        private static ZDO RecordOf(Player player)
        {
            ZNetView view = player.GetComponent<ZNetView>();
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }

        /// <summary>After the writing broke: one last try to leave both flags off, so nobody sees him away after he's back.</summary>
        private static void TryClear(Player player)
        {
            try
            {
                ZDO record = RecordOf(player);
                if (record != null && record.IsOwner())
                {
                    record.Set(AwayHash, false);
                    record.Set(ShowHash, false);
                }
            }
            catch (Exception)
            {
                // Nothing more can be done; the flags go with the character when he leaves.
            }
        }
    }
}
