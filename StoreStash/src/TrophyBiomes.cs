using System;
using QoLMods.Catalogue;

namespace StoreStash
{
    /// <summary>
    /// Trophies by biome (card A82): the "Trophies" choices SameBiomeTrophyChest and SameBiomeTrophyChestOnly send a trophy
    /// to a chest holding trophies of its biome. A trophy's biome is the catalogue's biome of origin (Biomes, the one
    /// sorting and chest setups use): the earliest biome its creature lives in by the world's own spawn lists, a boss at its
    /// altar's. The chest service never reads the catalogue (MODULES.md: it never needs it), so StoreStash hands it this
    /// reading in StackRules.TrophyBiome, as it hands over its reading of chest setups.
    /// </summary>
    internal static class TrophyBiomes
    {
        /// <summary>An item's biome of origin, by its shared name (None when the catalogue can't place it). Only the scratch
        /// test harness sets another.</summary>
        internal static Func<string, Heightmap.Biome> Of = Biomes.OfItem;

        /// <summary>Works this world's biomes out, once per world (a world is a new scene), with a line in the log (nothing
        /// when another module of the pack has already done it): asked again until they're built, should the game's
        /// databases not be there yet (Biomes.Build then does nothing, and Biomes.Built stays false: review N8). Only the
        /// scratch test harness sets another.</summary>
        internal static Action Prepare = () =>
        {
            if (ZNetScene.instance != null && !Biomes.Built)
            {
                Biomes.Build(line => Module.Log.LogInfo(line));
            }
        };

        /// <summary>The reading StackRules.TrophyBiome gets: for the two biome choices only (null for the others, which
        /// never ask), the world's biomes worked out first.</summary>
        internal static Func<string, Heightmap.Biome> Reading(TrophyChoice choice)
        {
            if (choice != TrophyChoice.SameBiomeTrophyChest && choice != TrophyChoice.SameBiomeTrophyChestOnly)
            {
                return null;
            }
            Prepare();
            return Of;
        }
    }
}
