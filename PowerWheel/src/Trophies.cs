using System.Collections.Generic;

namespace PowerWheel
{
    /// <summary>
    /// Which boss stones hold their trophies in this world, for "What counts as earned" at TrophyAtStone (card B06, his
    /// ask: a power stays a padlock until its boss's trophy is on its sacrificial stone). The game keeps no record of a
    /// placed trophy away from the stones (every stone's m_setsWorldKey is empty in its data), only the stone's own stand
    /// at the start temple, which a game has only while near it. So each stone this game sees holding its trophy (the
    /// patch on BossStone.SetActivated, at the stone's start and every second after; whoever placed it) is remembered for
    /// the session and on the character, in its own data (Player.m_customData, saved with it), one list per world.
    /// </summary>
    internal static class Trophies
    {
        /// <summary>The character data's key for a world's list, the world's id after it.</summary>
        internal const string DataKey = "PowerWheel_TrophiesOnStones_";

        // This session's sightings, by world id: kept even before the character is there, and merged into it.
        private static readonly Dictionary<long, HashSet<string>> Seen = new Dictionary<long, HashSet<string>>();

        /// <summary>From the patch on BossStone.SetActivated, with its trophy on.</summary>
        internal static void SeenOn(BossStone stone)
        {
            string power = stone != null && stone.m_itemStand != null && stone.m_itemStand.m_guardianPower != null
                ? stone.m_itemStand.m_guardianPower.name
                : null;
            SeenPower(power);
        }

        /// <summary>A stone holding the trophy of this power was seen, in this world (from SeenOn; a check outside the
        /// game, which can't read a Unity object's name, calls it straight).</summary>
        internal static void SeenPower(string power)
        {
            long? world = World();
            if (string.IsNullOrEmpty(power) || world == null)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            bool added = Of(world.Value).Add(power);
            if (added)
            {
                KeyLog.Line($"A boss stone holds its trophy: {power} counts as earned in this world (\"What counts as earned\" at TrophyAtStone).");
            }
            // A stone near where he comes into the world is first seen before his character is there (the game readies
            // the area, then spawns him): kept on the character as soon as it is, or on a new character.
            if (added || (player != null && !ReferenceEquals(player, keptWith)))
            {
                Keep(player, world.Value);
            }
        }

        // The character the session's sightings were last kept on.
        private static Player keptWith;

        /// <summary>Whether the power's trophy has been seen on its stone in this world, by this game or this character's
        /// before.</summary>
        internal static bool Placed(Player player, string power)
        {
            long? world = World();
            if (world == null)
            {
                return false;
            }
            Keep(player, world.Value);
            return Of(world.Value).Contains(power);
        }

        // The session's list and the character's merged both ways: the character's kept ones into the session's, and the
        // session's written onto the character if it lacks any.
        private static void Keep(Player player, long world)
        {
            if (player == null)
            {
                return;
            }
            keptWith = player;
            HashSet<string> seen = Of(world);
            string key = DataKey + world;
            player.m_customData.TryGetValue(key, out string kept);
            HashSet<string> stored = PowerRules.ReadTrophies(kept);
            seen.UnionWith(stored);
            if (!stored.SetEquals(seen))
            {
                player.m_customData[key] = PowerRules.WriteTrophies(seen);
            }
        }

        private static HashSet<string> Of(long world)
        {
            if (!Seen.TryGetValue(world, out HashSet<string> powers))
            {
                powers = new HashSet<string>();
                Seen.Add(world, powers);
            }
            return powers;
        }

        private static long? World() => ZNet.instance != null && ZNet.instance.GetWorld() != null ? ZNet.instance.GetWorld().m_uid : (long?)null;
    }
}
