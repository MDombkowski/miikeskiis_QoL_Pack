using System;
using QoLMods.Chests;

namespace StoreStash
{
    /// <summary>
    /// Which chests are his personal ones (card A43, decision 2): the game's Personal Chests he built, chests marked as his
    /// (the "mine" mark, Mine.cs), and, with its setting, every chest he built; each kind a setting of its own, and never a
    /// chest someone else marked as theirs. The personal quick stack fills only these (Settings.PersonalRules), and the
    /// other key leaves the first two kinds alone while "Use my personal chests" is off. His means this character: the
    /// game's Personal Chest and the mark both name the character, by player ID, so his other characters' chests aren't his
    /// here.
    /// </summary>
    internal static class Personal
    {
        /// <summary>The player ID of the character who built a chest (its piece's creator: a cart's or ship's chest sits
        /// below the piece), 0 if none. Only the scratch test harness sets another: it has no engine to ask about pieces.</summary>
        internal static Func<Container, long> CreatorOf = chest =>
        {
            Piece piece = chest.GetComponentInParent<Piece>();
            return piece != null ? piece.GetCreator() : 0L;
        };

        /// <summary>Whether the personal quick stack counts this chest as one of his (character <paramref name="me"/>),
        /// as the settings say now. A chest someone else marked as theirs never is, whoever built it. The rule is the chest
        /// service's (ChestMine.IsPersonal, since card A80), so Craft from Chests' "My personal chests first" counts the
        /// same chests.</summary>
        internal static bool Counts(Container chest, long me) => ChestMine.IsPersonal(chest, me, Kinds(), CreatorOf);

        /// <summary>The kinds of chest that count as his, as "Which chests are yours" says now.</summary>
        internal static PersonalKinds Kinds() =>
            new PersonalKinds(Settings.PersonalGameChests.Value, Settings.PersonalMarked.Value, Settings.PersonalBuilt.Value);

        /// <summary>The chests the quick stack key and button leave alone while "Use my personal chests" is off, so that only
        /// the personal key fills them: his own Personal Chests (another player's are never used at all) and chests marked as
        /// his, each while the personal key counts that kind. Never "every chest I built": that would leave the quick stack
        /// nothing at his own base.</summary>
        internal static bool LeftAloneByP(Container chest, long me) =>
            (Settings.PersonalGameChests.Value && chest.m_privacy == Container.PrivacySetting.Private) ||
            (Settings.PersonalMarked.Value && ChestMine.IsMine(chest, me));

        /// <summary>
        /// Why the personal quick stack has nowhere to put anything, before it starts: no chest of his nearby, or only
        /// reserved ones (Reserve Stock's mark: never put into by a quick stack). Null when one could take something. Looks
        /// where the quick stack would look (the rules' radius, carts and ships), so the words match what it would do.
        /// </summary>
        internal static string WhyNoneNearby(Player player, StackRules rules)
        {
            long me = ChestClaims.PlayerId();
            bool reservedOnly = false;
            foreach (Container chest in ChestFinder.Find(ChestFinder.Where(player), rules.Chests, withReserved: true))
            {
                if (!Counts(chest, me))
                {
                    continue;
                }
                if (!ChestReserve.IsReserved(chest))
                {
                    return null;
                }
                reservedOnly = true;
            }
            if (reservedOnly)
            {
                return "Personal quick stack: your chests nearby are all reserved (Reserve Stock)";
            }
            return Settings.PersonalMarked.Value && Settings.MineKey.Value.MainKey != UnityEngine.KeyCode.None
                ? $"Personal quick stack: no chest of yours nearby (point at one and press {Settings.KeyText(Settings.MineKey.Value)} to mark it as yours)"
                : "Personal quick stack: no chest of yours nearby";
        }
    }
}
