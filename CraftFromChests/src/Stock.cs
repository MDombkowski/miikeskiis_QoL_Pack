using QoLMods.Chests;
using UnityEngine;

namespace CraftFromChests
{
    /// <summary>
    /// What the chests near him hold, for the menus and for deciding whether a craft or a piece is affordable: one count
    /// of every usable chest (the chest service's survey), from this computer's copies, kept a quarter of a second or until
    /// he moves a metre, since the crafting and build menus ask many times a frame. Never used for taking: a take counts
    /// again from the chests it holds (Request).
    /// </summary>
    internal static class Stock
    {
        private const float MaxAge = 0.25f;

        private static readonly ChestSurvey Empty = new ChestSurvey();
        private static ChestSurvey survey;
        private static float surveyedAt;
        private static Vector3 surveyedFrom;
        private static int surveyedWith;

        // The same, over only the chests this computer owns: what could be taken in this very frame (building's "can I
        // place it", which other mods also ask before they place and pay piece by piece).
        private static ChestSurvey owned;
        private static float ownedAt;
        private static Vector3 ownedFrom;
        private static int ownedWith;

        /// <summary>For the scratch test harness only, which can't run Unity's clock or find chests: when set, the survey
        /// every question gets. Never set in the game.</summary>
        internal static ChestSurvey Forced = null;

        /// <summary>Counts again at the next question: after a take, a chest handed over, or a change of settings.</summary>
        internal static void Invalidate()
        {
            survey = null;
            owned = null;
        }

        /// <summary>Counts the chests he owns again at the next question: before a payment that follows at once.</summary>
        internal static void InvalidateOwned() => owned = null;

        /// <summary>The survey over the chests this computer owns right now (at most a quarter of a second old): no claim
        /// needed to take from them.</summary>
        internal static ChestSurvey Owned()
        {
            if (Forced != null)
            {
                return ForcedOwned ?? Forced;
            }
            Player player = Player.m_localPlayer;
            if (player == null || Game.instance == null)
            {
                return Empty;
            }
            ChestRules rules = Settings.Rules();
            System.Func<Container, bool> skip = rules.Skip;
            rules.Skip = chest => !Request.Mine(chest) || (skip != null && skip(chest));
            int with = RulesKey(rules) * 7 + (skip != null ? 1 : 0);
            float now = Time.unscaledTime;
            Vector3 position = player.transform.position;
            if (owned == null || now - ownedAt > MaxAge || with != ownedWith || (position - ownedFrom).sqrMagnitude > 1f)
            {
                owned = ChestStock.Survey(position, rules);
                ownedAt = now;
                ownedFrom = position;
                ownedWith = with;
            }
            return owned;
        }

        /// <summary>For the scratch test harness only: the owned survey when Forced is set.</summary>
        internal static ChestSurvey ForcedOwned = null;

        /// <summary>How many of an item (any quality) the chests he owns right now may give.</summary>
        internal static int OwnedOf(string name) => Owned().StockOf(name);

        /// <summary>The survey as it stands (at most a quarter of a second old).</summary>
        internal static ChestSurvey Current()
        {
            if (Forced != null)
            {
                return Forced;
            }
            Player player = Player.m_localPlayer;
            if (player == null || Game.instance == null)
            {
                return Empty;
            }
            ChestRules rules = Settings.Rules();
            int with = RulesKey(rules);
            float now = Time.unscaledTime;
            Vector3 position = player.transform.position;
            if (survey == null || now - surveyedAt > MaxAge || with != surveyedWith || (position - surveyedFrom).sqrMagnitude > 1f)
            {
                survey = ChestStock.Survey(position, rules);
                surveyedAt = now;
                surveyedFrom = position;
                surveyedWith = with;
            }
            return survey;
        }

        /// <summary>How many of an item (by its shared name, any quality) the chests near him may give.</summary>
        internal static int Of(string name) => Current().StockOf(name);

        /// <summary>How many of an item (by its shared name, any quality) the reserved chests near him hold, counted apart
        /// (0 when the rules use reserved chests like any other: then they're in Of).</summary>
        internal static int ReservedOf(string name) => Current().ReservedOf(name);

        /// <summary>How many of one quality of an item they may give, as the game counts a recipe's materials (one quality
        /// at a time). An item without levels has quality 1 only.</summary>
        internal static int At(string name, int quality, int maxQuality) => Current().StockOf(KeyAt(name, quality, maxQuality));

        /// <summary>The chest service's key for one quality of an item: its name for an item without levels (quality 1),
        /// name@quality for one with levels. Quality 0 or below names nothing.</summary>
        internal static string KeyAt(string name, int quality, int maxQuality)
        {
            if (quality < 1)
            {
                return "\n";   // no item has this key
            }
            return maxQuality > 1 ? $"{name}@{quality}" : quality == 1 ? name : "\n";
        }

        /// <summary>One item of that key found in the chests (its name, quality and icon), or null.</summary>
        internal static ItemDrop.ItemData Sample(string key) => Current().Samples.TryGetValue(key, out ItemDrop.ItemData item) ? item : null;

        private static int RulesKey(ChestRules rules)
        {
            unchecked
            {
                int key = rules.Range.GetHashCode();
                key = key * 31 + (rules.OnlyBuiltByMe ? 1 : 0);
                key = key * 31 + (rules.LeaveOne ? 1 : 0);
                key = key * 31 + (rules.UseReserved ? 1 : 0);
                return key * 31 + (rules.Skip != null ? 1 : 0);
            }
        }
    }
}
