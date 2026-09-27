// The QoL Mods catalogue, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Catalogue\. Every mod that uses
// it carries a copy in its own parts\Catalogue\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace QoLMods.Catalogue
{
    /// <summary>
    /// Each item's biome of origin, for progression order and the Biome headings. Worked out once per world from the
    /// game's own data (what the world puts where, what drops what, what is made from what), never typed in from memory.
    /// Written for Add to Cart 0.2.0 (session 9-B) and moved into the catalogue part with 0.3.0, so every mod places an
    /// item in the same biome.
    ///
    /// The rule. Whatever the world gives him (picked, chopped, mined, broken, killed, caught) belongs to the EARLIEST
    /// biome, in the order he meets them, where the world gives it: the vegetation ZoneSystem places, the creatures the
    /// spawn lists put out, and what each of those drops or leaves behind (a tree's logs, a rock's fragments, a nest's
    /// creatures). Whatever is made belongs to the LATEST biome among what it takes, worked out until nothing changes: a
    /// recipe's ingredients and its crafting station; what a smelter, kiln, fermenter or cooking station is fed, with its
    /// fuel and the station itself; the seed or egg it grows from; what a beehive, sap extractor or wisp lure cost. A
    /// trader's wares belong to his camp's biome, and a ware sold only once a world key is set comes no earlier than
    /// that key (the biome of the creature whose defeat sets it, or of the item brought to him to set it). An item he
    /// can get several ways takes the earliest of them. An ingredient nothing places is left out of the reckoning (the
    /// rest decide), never counted as early. Anything left over is None, shown as "Other".
    ///
    /// What counts: where the world normally puts things. The rarer variant areas (the game's alt biomes, such as the
    /// Dark Meadows where draugr roam, or the Black Forest's wild roots that drop ancient bark) count only for items
    /// found no other way. Spawns that wait for a boss to be beaten (greydwarfs in the Meadows after Eikthyr) count only
    /// for creatures that spawn no other way, and never earlier than the biome of the creature whose defeat opens them.
    /// Raids and other events never count.
    ///
    /// What the data can't show. Locations and dungeons are soft references the game loads only when he comes near, so
    /// what they hold (bosses, traders, dungeon loot and the creatures in them) can't be read here. The things found only
    /// there get a home biome from <see cref="Homes"/>, each with its source; what they give is still read from the game.
    /// </summary>
    internal static class Biomes
    {
        /// <summary>
        /// The biomes in the order he meets them. The Ocean comes right after the Black Forest: the sea's own things
        /// (serpents, leviathans, the deep-water fish) need a ship, and the first ship after the raft, the Karve ("A small
        /// and sleek ship, ready to set sail", Jötunn's piece list), is built from Black Forest materials (80 bronze nails);
        /// anything that also needs iron still lands in the Swamp or later. Where the Ocean sits moves its header more than
        /// its items: after the Swamp instead, six items would change (the offline check).
        /// </summary>
        private static readonly Heightmap.Biome[] Progression =
        {
            Heightmap.Biome.Meadows,
            Heightmap.Biome.BlackForest,
            Heightmap.Biome.Ocean,
            Heightmap.Biome.Swamp,
            Heightmap.Biome.Mountain,
            Heightmap.Biome.Plains,
            Heightmap.Biome.Mistlands,
            Heightmap.Biome.AshLands,
            Heightmap.Biome.DeepNorth,
        };

        /// <summary>
        /// Home biomes for things the world puts only inside locations and dungeons, by prefab name: only those that
        /// place something nothing else does (the offline check drops every entry that changes nothing). What each one
        /// gives (a boss's drops, a chest's loot, a trader's stock) is still read from the game. Sources: which location
        /// holds the thing (the comment on each line) was read from the game's own location and dungeon-room prefabs, in
        /// its asset bundles (build 1.0.16), by the offline check; each location's biome is the one ZoneSystem gives it,
        /// the same as Jötunn's location list shows (https://valheim-modding.github.io/Jotunn/data/zones/location-list.html).
        /// </summary>
        internal static readonly KeyValuePair<string, Heightmap.Biome>[] Homes =
        {
            // Bosses, at their altars.
            Home("Eikthyr", Heightmap.Biome.Meadows),               // Eikthyrnir
            Home("gd_king", Heightmap.Biome.BlackForest),           // GDKing
            Home("Bonemass", Heightmap.Biome.Swamp),                // Bonemass
            Home("Dragon", Heightmap.Biome.Mountain),               // Dragonqueen
            Home("GoblinKing", Heightmap.Biome.Plains),             // GoblinKing
            Home("SeekerQueen", Heightmap.Biome.Mistlands),         // Mistlands_DvergrBossEntrance1
            Home("Fader", Heightmap.Biome.AshLands),                // FaderLocation
            Home("FrozenKing", Heightmap.Biome.DeepNorth),          // DN_Bossroom (its later phases are its death effects)

            // Traders, at their camps.
            Home("Haldor", Heightmap.Biome.BlackForest),            // Vendor_BlackForest
            Home("Hildir", Heightmap.Biome.Meadows),                // Hildir_camp
            Home("BogWitch", Heightmap.Biome.Swamp),                // BogWitch_Camp

            // Creatures that live only in locations and dungeons (their spawners are placed there).
            Home("Skeleton", Heightmap.Biome.BlackForest),          // Crypt2-4 (burial chambers)
            Home("Ghost", Heightmap.Biome.BlackForest),             // Crypt2-4
            Home("Surtling", Heightmap.Biome.Swamp),                // FireHole
            Home("Fenring_Cultist", Heightmap.Biome.Mountain),      // MountainCave02
            Home("Ulv", Heightmap.Biome.Mountain),                  // MountainCave02
            Home("BlobTar", Heightmap.Biome.Plains),                // TarPit1-3
            Home("piece_Charred_Balista", Heightmap.Biome.AshLands), // CharredFortress
            Home("Charred_Melee_Dyrnwyn", Heightmap.Biome.AshLands), // PlaceofMystery3
            Home("ElakingMole", Heightmap.Biome.DeepNorth),         // TheHole01
            Home("BlobMorkBig", Heightmap.Biome.DeepNorth),         // MorkBorg
            Home("Fish4_cave", Heightmap.Biome.Mountain),           // MountainCave02
            Home("BogWitchKvastur", Heightmap.Biome.Swamp),         // BogWitch_Camp
            Home("GoblinShaman", Heightmap.Biome.Plains),           // GoblinCamp2
            Home("Charred_Mage", Heightmap.Biome.AshLands),         // CharredFortress
            Home("Skeleton_Hildir", Heightmap.Biome.BlackForest),   // Hildir_crypt (Brenna)
            Home("Fenring_Cultist_Hildir", Heightmap.Biome.Mountain), // Hildir_cave (Geirrhafa)
            Home("GoblinBruteBros", Heightmap.Biome.Plains),        // Hildir_plainsfortress (Zil and Thungr)

            // What is picked or looted there.
            Home("Beehive", Heightmap.Biome.Meadows),               // WoodHouse1-6
            Home("TreasureChest_meadows_buried", Heightmap.Biome.Meadows), // ShipSetting01
            Home("TreasureChest_meadows_01", Heightmap.Biome.Meadows), // WoodHouse6
            Home("TreasureChest_meadows_02", Heightmap.Biome.Meadows), // WoodHouse2
            Home("Pickable_SurtlingCoreStand", Heightmap.Biome.BlackForest), // Crypt2-4
            Home("Pickable_Mushroom_yellow", Heightmap.Biome.BlackForest), // Crypt2-4, TrollCave02
            Home("TreasureChest_blackforest", Heightmap.Biome.BlackForest), // Ruin1, Ruin2, StoneHouse3
            Home("TreasureChest_swamp", Heightmap.Biome.Swamp),     // Grave1, SwampRuin1-2
            Home("Pickable_DragonEgg", Heightmap.Biome.Mountain),   // DrakeNest01
            Home("Pickable_MountainCaveRandom", Heightmap.Biome.Mountain), // MountainCave02
            Home("TreasureChest_mountains", Heightmap.Biome.Mountain), // StoneTowerRuins04-05, AbandonedLogCabin02-04
            Home("Pickable_Flax_Wild", Heightmap.Biome.Plains),     // GoblinCamp2
            Home("TreasureChest_heath", Heightmap.Biome.Plains),    // StoneHouse1_heath, StoneHouse5_heath, Ruin3
            Home("Pickable_BlackCoreStand", Heightmap.Biome.Mistlands), // Mistlands_DvergrTownEntrance1-2
            Home("blackmarble_altar_crystal", Heightmap.Biome.Mistlands), // Mistlands_DvergrTownEntrance1-2
            Home("dvergrprops_crate_long", Heightmap.Biome.Mistlands), // Mistlands_GuardTower1_new to 3_new
            Home("dvergrprops_curtain", Heightmap.Biome.Mistlands), // Mistlands_GuardTower1_new to 3_new
            Home("TreasureChest_dvergrtown", Heightmap.Biome.Mistlands), // Mistlands_DvergrTownEntrance1-2
            Home("Charred_altar_bellfragment", Heightmap.Biome.AshLands), // CharredFortress
            Home("Ashlands_Fortress_Wall_Spikes", Heightmap.Biome.AshLands), // CharredFortress
            Home("TreasureChest_charredfortress", Heightmap.Biome.AshLands), // CharredFortress, FortressRuins
            Home("asksvin_carrion", Heightmap.Biome.AshLands),      // VoltureNest, MorgenHole1-3
            Home("VineAsh", Heightmap.Biome.AshLands),              // CharredRuins1-4
            Home("Pickable_Fiddlehead", Heightmap.Biome.AshLands),  // FortressRuins, CharredTowerRuins1
            Home("Pickable_Swordpiece2", Heightmap.Biome.AshLands), // PlaceofMystery2
            Home("Pickable_Swordpiece3", Heightmap.Biome.AshLands), // PlaceofMystery1
            Home("Pickable_FrostCoreHanger", Heightmap.Biome.DeepNorth), // TheHole01
            Home("Pickable_GlowWorm", Heightmap.Biome.DeepNorth),   // TheHole01
            Home("loot_deepNorth_Granary", Heightmap.Biome.DeepNorth), // NorthVillage
            Home("Morkhalla_ChestAncient", Heightmap.Biome.DeepNorth), // MorkBorg
            Home("elaking_trashpile", Heightmap.Biome.DeepNorth),   // TheHole01
        };

        private static KeyValuePair<string, Heightmap.Biome> Home(string prefab, Heightmap.Biome biome) => new KeyValuePair<string, Heightmap.Biome>(prefab, biome);

        private static readonly Dictionary<string, Heightmap.Biome> ByItem = new Dictionary<string, Heightmap.Biome>();

        /// <summary>How each placed item got its biome: the kind of source ("creatures", "homes", "vegetation" …).</summary>
        private static readonly Dictionary<string, string> HowByItem = new Dictionary<string, string>();

        /// <summary>A world's spawn systems, private in the game; mods (Jötunn) add their spawn lists to each one.</summary>
        private static readonly FieldInfo LiveSpawnSystems = typeof(SpawnSystem).GetField("m_instances", BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>The scene the last Build read: a new world is a new scene, and is read again.</summary>
        private static ZNetScene builtFor;

        /// <summary>True once this world's biomes are worked out.</summary>
        internal static bool Built => builtFor != null && builtFor == ZNetScene.instance;

        /// <summary>
        /// Works everything out, once per world, when the game's databases exist (in a world). Cheap enough for the first
        /// opening of a window: one pass over the vegetation, the spawn lists, the scene's prefabs and the recipes. Never
        /// throws: a part that fails is left out, and its items stay unknown. Whichever module asks first does the work;
        /// <paramref name="log"/> gets one line saying what was placed.
        /// </summary>
        internal static void Build(Action<string> log)
        {
            ObjectDB db = ObjectDB.instance;
            ZNetScene scene = ZNetScene.instance;
            ZoneSystem zones = ZoneSystem.instance;
            if (db == null || scene == null || zones == null || scene == builtFor)
            {
                return;
            }
            builtFor = scene;
            ByItem.Clear();
            HowByItem.Clear();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var solver = new Solver();
            var failed = new List<string>();
            var homes = new Dictionary<GameObject, Heightmap.Biome>();
            var keys = new Dictionary<string, int>();
            Step(failed, "homes", () => ReadHomes(scene, homes));
            Step(failed, "vegetation", () => ReadVegetation(zones, solver));
            Step(failed, "creatures", () => ReadCreatures(zones, homes, keys, solver));
            Step(failed, "homes' things", () => ReadHomeThings(homes, solver));
            Step(failed, "stations, plantings and traders", () => ReadScene(scene, homes, solver));
            Step(failed, "recipes", () => ReadRecipes(db, solver));
            Step(failed, "eggs", () => ReadEggs(db, solver));
            Step(failed, "working out", () => solver.Solve());
            foreach (KeyValuePair<string, int> known in solver.Ranks)
            {
                if (!known.Key.StartsWith(KeyPrefix))
                {
                    ByItem[known.Key] = Progression[known.Value];
                    if (solver.How.TryGetValue(known.Key, out (string Kind, string Detail) how))
                    {
                        HowByItem[known.Key] = how.Kind;
                    }
                }
            }
            try
            {
                log?.Invoke(Summary(db, solver, failed, clock.ElapsedMilliseconds));
            }
            catch (Exception)
            {
                // The log is a courtesy; the result stands without it.
            }
        }

        /// <summary>The item's biome of origin, by its shared name ("$item_wood"); None when unknown.</summary>
        internal static Heightmap.Biome OfItem(string sharedName)
        {
            return sharedName != null && ByItem.TryGetValue(sharedName, out Heightmap.Biome biome) ? biome : Heightmap.Biome.None;
        }

        /// <summary>The same as a label: the English biome names of Taxonomy.Biomes, or "Other".</summary>
        internal static string LabelOf(string sharedName) => English(OfItem(sharedName));

        /// <summary>How the item got its biome: the kind of source ("creatures", "homes", "vegetation", "recipes" …), or
        /// empty when unknown. Things a creature or a location gives are Creature Parts when nothing else claims them.</summary>
        internal static string HowOf(string sharedName) =>
            sharedName != null && HowByItem.TryGetValue(sharedName, out string how) ? how : string.Empty;

        /// <summary>
        /// The latest biome in progression among these items (a recipe's or a piece's costs). An item that can't be
        /// placed is left out, as the working-out leaves it out; None only if none of them can be placed.
        /// </summary>
        internal static Heightmap.Biome Latest(IEnumerable<string> sharedNames)
        {
            int latest = -1;
            if (sharedNames != null)
            {
                foreach (string name in sharedNames)
                {
                    latest = Math.Max(latest, Rank(OfItem(name)));
                }
            }
            return latest < 0 ? Heightmap.Biome.None : Progression[latest];
        }

        /// <summary>For sorting: the biome's place in progression; None ("Other") after all of them.</summary>
        internal static int Order(Heightmap.Biome biome)
        {
            int rank = Rank(biome);
            return rank < 0 ? Progression.Length : rank;
        }

        /// <summary>The header for a biome, in his language (the game's own names); "Other" for None.</summary>
        internal static string Label(Heightmap.Biome biome)
        {
            int rank = Rank(biome);
            if (rank < 0)
            {
                return "Other";
            }
            try
            {
                string token = "$biome_" + Progression[rank].ToString().ToLower();
                string name = Localization.instance != null ? Localization.instance.Localize(token) : null;
                if (!string.IsNullOrEmpty(name) && name != token && !name.StartsWith("["))
                {
                    return name;
                }
            }
            catch (Exception)
            {
                // Fall back to English below.
            }
            return English(Progression[rank]);
        }

        /// <summary>The game's English name for a biome (its $biome_ texts, as Jötunn's English list shows them).</summary>
        internal static string English(Heightmap.Biome biome)
        {
            switch (biome)
            {
                case Heightmap.Biome.Meadows: return "Meadows";
                case Heightmap.Biome.BlackForest: return "Black Forest";
                case Heightmap.Biome.Ocean: return "Ocean";
                case Heightmap.Biome.Swamp: return "Swamp";
                case Heightmap.Biome.Mountain: return "Mountain";
                case Heightmap.Biome.Plains: return "Plains";
                case Heightmap.Biome.Mistlands: return "Mistlands";
                case Heightmap.Biome.AshLands: return "Ashlands";
                case Heightmap.Biome.DeepNorth: return "Deep North";
                default: return "Other";
            }
        }

        /// <summary>A single biome's place in progression, or the earliest of several (a mask); -1 for none of them.</summary>
        internal static int Rank(Heightmap.Biome biomes)
        {
            for (int rank = 0; rank < Progression.Length; rank++)
            {
                if ((biomes & Progression[rank]) != 0)
                {
                    return rank;
                }
            }
            return -1;
        }

        private static void Step(List<string> failed, string what, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                failed.Add($"{what} ({e.GetType().Name}: {e.Message})");
            }
        }

        private static string Summary(ObjectDB db, Solver solver, List<string> failed, long ms)
        {
            var names = new HashSet<string>();
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop != null)
                {
                    names.Add(drop.m_itemData.m_shared.m_name);
                }
            }
            var byKind = new SortedDictionary<string, int>();
            int known = 0;
            foreach (string name in names)
            {
                if (solver.How.TryGetValue(name, out (string Kind, string Detail) how))
                {
                    known++;
                    byKind.TryGetValue(how.Kind, out int count);
                    byKind[how.Kind] = count + 1;
                }
            }
            var parts = new List<string>();
            foreach (KeyValuePair<string, int> kind in byKind)
            {
                parts.Add($"{kind.Key} {kind.Value}");
            }
            string text = $"Biomes: {known} of {names.Count} items placed ({string.Join(", ", parts)}); {names.Count - known} left as Other; {ms} ms.";
            if (failed.Count > 0)
            {
                text += " Left out after an error: " + string.Join("; ", failed) + ".";
            }
            return text;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Reading the game

        private static void ReadHomes(ZNetScene scene, Dictionary<GameObject, Heightmap.Biome> homes)
        {
            foreach (KeyValuePair<string, Heightmap.Biome> home in Homes)
            {
                GameObject prefab = scene.GetPrefab(home.Key);
                if (prefab != null)
                {
                    homes[prefab] = home.Value;
                }
            }
        }

        /// <summary>What ZoneSystem places (its own list, each location list's and each alt biome's, merged at its Start).</summary>
        private static void ReadVegetation(ZoneSystem zones, Solver solver)
        {
            var usual = new Dictionary<GameObject, Heightmap.Biome>();
            var variant = new Dictionary<GameObject, Heightmap.Biome>();
            foreach (ZoneSystem.ZoneVegetation veg in zones.m_vegetation)
            {
                if (veg == null || !veg.m_enable || veg.m_prefab == null || Rank(veg.m_biome) < 0)
                {
                    continue;
                }
                Dictionary<GameObject, Heightmap.Biome> into = string.IsNullOrEmpty(veg.AltBiomeParent) ? usual : variant;
                into.TryGetValue(veg.m_prefab, out Heightmap.Biome mask);
                into[veg.m_prefab] = mask | veg.m_biome;
            }
            foreach (KeyValuePair<GameObject, Heightmap.Biome> veg in usual)
            {
                Give(solver, veg.Key, Rank(veg.Value), "vegetation", false);
            }
            foreach (KeyValuePair<GameObject, Heightmap.Biome> veg in variant)
            {
                Give(solver, veg.Key, Rank(veg.Value), "vegetation", true);
            }
        }

        /// <summary>A spawn that waits for a world key (defeated_bonemass …): where, and which key.</summary>
        private readonly struct LateSpawn
        {
            internal readonly GameObject Creature;
            internal readonly Heightmap.Biome Biome;
            internal readonly string Key;

            internal LateSpawn(GameObject creature, Heightmap.Biome biome, string key)
            {
                Creature = creature;
                Biome = biome;
                Key = key;
            }
        }

        /// <summary>The creatures the spawn lists put out, each at its home (see the class summary), and what they drop.</summary>
        private static void ReadCreatures(ZoneSystem zones, Dictionary<GameObject, Heightmap.Biome> homes, Dictionary<string, int> keys, Solver solver)
        {
            var usual = new Dictionary<GameObject, Heightmap.Biome>();
            var variant = new Dictionary<GameObject, Heightmap.Biome>();
            var late = new List<LateSpawn>();
            foreach (SpawnSystemList list in SpawnLists(zones))
            {
                foreach (SpawnSystem.SpawnData spawn in list.m_spawners)
                {
                    Sort(spawn, Heightmap.Biome.None, usual, late);
                }
            }
            foreach (AltBiome alt in AltBiomeList.m_altBiomes)
            {
                if (alt != null && alt.m_enabled && alt.m_spawn != null)
                {
                    foreach (SpawnSystem.SpawnData spawn in alt.m_spawn)
                    {
                        Sort(spawn, alt.m_biome, variant, late);
                    }
                }
            }

            // Where each creature lives: its home (bosses and dungeon dwellers), else its usual spawns, else the variant
            // areas. It gives its world key that biome.
            var home = new Dictionary<GameObject, int>();
            foreach (KeyValuePair<GameObject, Heightmap.Biome> known in homes)
            {
                home[known.Key] = Rank(known.Value);
            }
            foreach (Dictionary<GameObject, Heightmap.Biome> tier in new[] { usual, variant })
            {
                foreach (KeyValuePair<GameObject, Heightmap.Biome> creature in tier)
                {
                    if (!home.ContainsKey(creature.Key) && Rank(creature.Value) >= 0)
                    {
                        home[creature.Key] = Rank(creature.Value);
                    }
                }
            }

            // A world key a creature's defeat sets (defeated_bonemass …) belongs to that creature's home.
            foreach (KeyValuePair<GameObject, int> creature in home)
            {
                if (creature.Value < 0)
                {
                    continue;
                }
                foreach (string key in DefeatKeys(creature.Key, 0))
                {
                    keys[key] = keys.TryGetValue(key, out int rank) ? Math.Min(rank, creature.Value) : creature.Value;
                    solver.World(Key(key), creature.Value, "keys", creature.Key.name);
                }
            }

            // Spawns that wait for a world key: only for creatures with no other home, never earlier than the key.
            var lateHome = new Dictionary<GameObject, int>();
            foreach (LateSpawn spawn in late)
            {
                if (home.ContainsKey(spawn.Creature))
                {
                    continue;
                }
                int rank = Rank(spawn.Biome);
                if (rank >= 0 && keys.TryGetValue(spawn.Key, out int keyRank))
                {
                    rank = Math.Max(rank, keyRank);
                }
                if (rank >= 0 && (!lateHome.TryGetValue(spawn.Creature, out int old) || rank < old))
                {
                    lateHome[spawn.Creature] = rank;
                }
            }
            // What they drop (the homes' own drops are given with the other homes).
            foreach (KeyValuePair<GameObject, Heightmap.Biome> creature in usual)
            {
                if (!homes.ContainsKey(creature.Key))
                {
                    Give(solver, creature.Key, Rank(creature.Value), "creatures", false);
                }
            }
            foreach (KeyValuePair<GameObject, Heightmap.Biome> creature in variant)
            {
                if (!homes.ContainsKey(creature.Key))
                {
                    Give(solver, creature.Key, Rank(creature.Value), "creatures", true);
                }
            }
            foreach (KeyValuePair<GameObject, int> creature in lateHome)
            {
                Give(solver, creature.Key, creature.Value, "creatures", false);
            }
        }

        /// <summary>The world keys a creature's defeat sets: its own, and its later phases' (a boss dies into the next).</summary>
        private static IEnumerable<string> DefeatKeys(GameObject creature, int depth)
        {
            Character character = creature != null && depth <= 3 ? creature.GetComponent<Character>() : null;
            if (character == null)
            {
                yield break;
            }
            if (!string.IsNullOrEmpty(character.m_defeatSetGlobalKey))
            {
                yield return character.m_defeatSetGlobalKey.ToLower();
            }
            if (character.m_deathEffects == null || character.m_deathEffects.m_effectPrefabs == null)
            {
                yield break;
            }
            foreach (EffectList.EffectData effect in character.m_deathEffects.m_effectPrefabs)
            {
                if (effect != null && effect.m_enabled && effect.m_prefab != null)
                {
                    foreach (string key in DefeatKeys(effect.m_prefab, depth + 1))
                    {
                        yield return key;
                    }
                }
            }
        }

        /// <summary>Files one spawn entry: where it spawns (inside an alt biome, only where both allow), and whether it
        /// waits for a world key. Event spawns (raids, Fimbulvinter) are left out.</summary>
        private static void Sort(SpawnSystem.SpawnData spawn, Heightmap.Biome area, Dictionary<GameObject, Heightmap.Biome> plain, List<LateSpawn> late)
        {
            if (spawn == null || !spawn.m_enabled || spawn.m_prefab == null || !string.IsNullOrEmpty(spawn.m_requiredPersistentEvent))
            {
                return;
            }
            Heightmap.Biome biome = (spawn.m_biome & area) != 0 ? spawn.m_biome & area : spawn.m_biome;
            if (!string.IsNullOrEmpty(spawn.m_requiredGlobalKey))
            {
                late.Add(new LateSpawn(spawn.m_prefab, biome, spawn.m_requiredGlobalKey.ToLower()));
                return;
            }
            plain.TryGetValue(spawn.m_prefab, out Heightmap.Biome mask);
            plain[spawn.m_prefab] = mask | biome;
        }

        /// <summary>The spawn lists: the zone control prefab's, and a live spawn system's (which also has the mods' lists).</summary>
        private static List<SpawnSystemList> SpawnLists(ZoneSystem zones)
        {
            var lists = new List<SpawnSystemList>();
            void Add(List<SpawnSystemList> more)
            {
                if (more == null)
                {
                    return;
                }
                foreach (SpawnSystemList list in more)
                {
                    if (list != null && list.m_spawners != null && !lists.Contains(list))
                    {
                        lists.Add(list);
                    }
                }
            }
            SpawnSystem prefab = zones.m_zoneCtrlPrefab != null ? zones.m_zoneCtrlPrefab.GetComponent<SpawnSystem>() : null;
            Add(prefab != null ? prefab.m_spawnLists : null);
            if (LiveSpawnSystems != null && LiveSpawnSystems.GetValue(null) is List<SpawnSystem> live)
            {
                foreach (SpawnSystem system in live)
                {
                    if (system != null)
                    {
                        Add(system.m_spawnLists);
                        break;
                    }
                }
            }
            return lists;
        }

        /// <summary>What the homes that aren't creatures give (chests, pickables), at their home biome.</summary>
        private static void ReadHomeThings(Dictionary<GameObject, Heightmap.Biome> homes, Solver solver)
        {
            foreach (KeyValuePair<GameObject, Heightmap.Biome> home in homes)
            {
                Give(solver, home.Key, Rank(home.Value), "homes", false);
            }
        }

        /// <summary>The scene's prefabs: stations and what they turn out, plantings, beehives and sap extractors, traders.</summary>
        private static void ReadScene(ZNetScene scene, Dictionary<GameObject, Heightmap.Biome> homes, Solver solver)
        {
            foreach (GameObject prefab in scene.m_prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }
                try
                {
                    ReadPrefab(prefab, homes, solver);
                }
                catch (Exception)
                {
                    // One odd prefab (another mod's) leaves only itself out.
                }
            }
        }

        private static void ReadPrefab(GameObject prefab, Dictionary<GameObject, Heightmap.Biome> homes, Solver solver)
        {
            Piece piece = prefab.GetComponent<Piece>();
            if (piece != null)
            {
                List<string> costs = Costs(piece.m_resources);
                Smelter smelter = prefab.GetComponent<Smelter>();
                if (smelter != null)
                {
                    string fuel = smelter.m_maxFuel > 0 ? Name(smelter.m_fuelItem) : null;
                    foreach (Smelter.ItemConversion conversion in smelter.m_conversion)
                    {
                        if (conversion != null)
                        {
                            Made(solver, Name(conversion.m_to), costs, Name(conversion.m_from), fuel, "stations", prefab.name);
                        }
                    }
                }
                Fermenter fermenter = prefab.GetComponent<Fermenter>();
                if (fermenter != null)
                {
                    foreach (Fermenter.ItemConversion conversion in fermenter.m_conversion)
                    {
                        if (conversion != null)
                        {
                            Made(solver, Name(conversion.m_to), costs, Name(conversion.m_from), null, "stations", prefab.name);
                        }
                    }
                }
                CookingStation cooking = prefab.GetComponent<CookingStation>();
                if (cooking != null)
                {
                    string fuel = cooking.m_useFuel ? Name(cooking.m_fuelItem) : null;
                    string burnt = Name(cooking.m_overCookedItem);
                    foreach (CookingStation.ItemConversion conversion in cooking.m_conversion)
                    {
                        if (conversion != null)
                        {
                            Made(solver, Name(conversion.m_to), costs, Name(conversion.m_from), fuel, "stations", prefab.name);
                            // Food left on too long burns into the station's overcooked item, coal on the cooking stations
                            // and the oven (CookingStation.m_overCookedItem): so coal belongs to the Meadows, where the
                            // first cooking station burns the first meat (his reasoning, 2026-09-26).
                            Made(solver, burnt, costs, Name(conversion.m_to), fuel, "stations", prefab.name);
                        }
                    }
                }
                Plant plant = prefab.GetComponent<Plant>();
                if (plant != null && plant.m_grownPrefabs != null)
                {
                    var grows = new List<string>();
                    var seen = new HashSet<GameObject>();
                    foreach (GameObject grown in plant.m_grownPrefabs)
                    {
                        Yields(grown, grows, seen, 0);
                    }
                    foreach (string item in grows)
                    {
                        Made(solver, item, costs, null, null, "grown", prefab.name);
                    }
                }
                Beehive hive = prefab.GetComponent<Beehive>();
                if (hive != null)
                {
                    Made(solver, Name(hive.m_honeyItem), costs, null, null, "producers", prefab.name);
                }
                SapCollector sap = prefab.GetComponent<SapCollector>();
                if (sap != null)
                {
                    Made(solver, Name(sap.m_spawnItem), costs, null, null, "producers", prefab.name);
                }
                WispSpawner lure = prefab.GetComponent<WispSpawner>();
                if (lure != null)
                {
                    // A lure draws wisps (or embers) at night; what he catches of them comes from the lure.
                    var lured = new List<string>();
                    Yields(lure.m_wispPrefab, lured, new HashSet<GameObject>(), 0);
                    foreach (string item in lured)
                    {
                        Made(solver, item, costs, null, null, "producers", prefab.name);
                    }
                }
            }
            Trader trader = prefab.GetComponent<Trader>();
            if (trader != null && homes.TryGetValue(prefab, out Heightmap.Biome traderHome))
            {
                int rank = Rank(traderHome);
                if (trader.m_items != null)
                {
                    foreach (Trader.TradeItem ware in trader.m_items)
                    {
                        if (ware == null || ware.m_prefab == null)
                        {
                            continue;
                        }
                        if (string.IsNullOrEmpty(ware.m_requiredGlobalKey))
                        {
                            solver.World(Name(ware.m_prefab), rank, "traders", prefab.name);
                        }
                        else
                        {
                            // Sold once a world key is set: no earlier than the key (see Key).
                            solver.Way(Name(ware.m_prefab), new List<string> { Key(ware.m_requiredGlobalKey) }, null, "traders", prefab.name, rank);
                        }
                    }
                }
                if (trader.m_useItems != null)
                {
                    // Bringing him an item (Hildir's chests) sets a world key.
                    foreach (Trader.TraderUseItem use in trader.m_useItems)
                    {
                        if (use != null && use.m_prefab != null && !string.IsNullOrEmpty(use.m_setsGlobalKey))
                        {
                            solver.Way(Key(use.m_setsGlobalKey), new List<string> { Name(use.m_prefab) }, null, "keys", prefab.name, rank);
                        }
                    }
                }
            }
        }

        /// <summary>Recipes: a new item at quality 1, from its ingredients (not the upgrade-only ones) and its station.</summary>
        private static void ReadRecipes(ObjectDB db, Solver solver)
        {
            foreach (Recipe recipe in db.m_recipes)
            {
                try
                {
                    if (recipe == null || !recipe.m_enabled || recipe.m_item == null || recipe.m_noCraftOnlyUpgrade)
                    {
                        continue;
                    }
                    var ingredients = new List<string>();
                    foreach (Piece.Requirement need in recipe.m_resources)
                    {
                        if (need != null && need.m_resItem != null && need.m_amount > 0 && !need.m_upgraderResource)
                        {
                            ingredients.Add(Name(need.m_resItem));
                        }
                    }
                    if (ingredients.Count == 0)
                    {
                        continue;
                    }
                    Piece station = recipe.m_craftingStation != null ? recipe.m_craftingStation.GetComponent<Piece>() : null;
                    List<string> stationCosts = station != null ? Costs(station.m_resources) : new List<string>();
                    if (recipe.m_requireOnlyOneIngredient)
                    {
                        solver.Way(Name(recipe.m_item), stationCosts, ingredients, "recipes", recipe.name);
                    }
                    else
                    {
                        stationCosts.AddRange(ingredients);
                        solver.Way(Name(recipe.m_item), stationCosts, null, "recipes", recipe.name);
                    }
                }
                catch (Exception)
                {
                    // One odd recipe (another mod's) leaves only itself out.
                }
            }
        }

        /// <summary>Eggs: what hatches from one (and what that grows up to) gives what it drops, from the egg.</summary>
        private static void ReadEggs(ObjectDB db, Solver solver)
        {
            foreach (GameObject prefab in db.m_items)
            {
                try
                {
                    EggGrow egg = prefab != null ? prefab.GetComponent<EggGrow>() : null;
                    ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                    if (egg == null || drop == null || egg.m_grownPrefab == null)
                    {
                        continue;
                    }
                    var hatches = new List<string>();
                    Yields(egg.m_grownPrefab, hatches, new HashSet<GameObject>(), 0);
                    var from = new List<string> { Name(drop) };
                    foreach (string item in hatches)
                    {
                        solver.Way(item, from, null, "grown", prefab.name);
                    }
                }
                catch (Exception)
                {
                    // One odd item (another mod's) leaves only itself out.
                }
            }
        }

        private static void Made(Solver solver, string output, List<string> stationCosts, string from, string fuel, string kind, string where)
        {
            if (string.IsNullOrEmpty(output))
            {
                return;
            }
            var takes = new List<string>(stationCosts);
            if (!string.IsNullOrEmpty(from))
            {
                takes.Add(from);
            }
            if (!string.IsNullOrEmpty(fuel))
            {
                takes.Add(fuel);
            }
            solver.Way(output, takes, null, kind, where);
        }

        private static List<string> Costs(Piece.Requirement[] resources)
        {
            var costs = new List<string>();
            if (resources == null)
            {
                return costs;
            }
            foreach (Piece.Requirement need in resources)
            {
                if (need != null && need.m_resItem != null && need.m_amount > 0)
                {
                    costs.Add(Name(need.m_resItem));
                }
            }
            return costs;
        }

        private static string Name(ItemDrop item) => item != null ? item.m_itemData.m_shared.m_name : null;

        /// <summary>
        /// A world key, worked out like an item: it belongs to the biome of the creature whose defeat sets it, or of the
        /// item brought to a trader to set it; a ware sold once it's set comes no earlier.
        /// </summary>
        internal static string Key(string key) => KeyPrefix + key.ToLower();

        private const string KeyPrefix = "key:";

        /// <summary>Everything a thing the world puts down gives him, at the rank of its biome; in a variant area (an
        /// alt biome), only for items found no other way.</summary>
        private static void Give(Solver solver, GameObject thing, int rank, string kind, bool variant)
        {
            if (rank < 0)
            {
                return;
            }
            var items = new List<string>();
            try
            {
                Yields(thing, items, new HashSet<GameObject>(), 0);
            }
            catch (Exception)
            {
                // One odd prefab (another mod's) leaves only itself out.
                return;
            }
            foreach (string item in items)
            {
                if (variant)
                {
                    solver.Variant(item, rank, kind, thing.name + " (alt biome)");
                }
                else
                {
                    solver.World(item, rank, kind, thing.name);
                }
            }
        }

        /// <summary>
        /// What a thing gives him: itself if it's an item, what he picks from it, what it drops when chopped, mined,
        /// broken or killed, and what it leaves behind or lets out (a tree's log and stump, a rock's fragments, a nest's
        /// creatures), a few steps deep.
        /// </summary>
        private static void Yields(GameObject thing, List<string> items, HashSet<GameObject> seen, int depth)
        {
            if (thing == null || depth > 6 || !seen.Add(thing))
            {
                return;
            }
            void Next(GameObject next) => Yields(next, items, seen, depth + 1);
            void Table(DropTable table)
            {
                if (table == null || table.m_drops == null || table.m_dropChance <= 0f || table.m_dropMax <= 0)
                {
                    return;
                }
                foreach (DropTable.DropData drop in table.m_drops)
                {
                    // An entry of weight 0 among others never drops (DropTable warns of it).
                    if (drop.m_weight > 0f || table.m_drops.Count == 1)
                    {
                        Next(drop.m_item);
                    }
                }
            }
            ItemDrop self = thing.GetComponent<ItemDrop>();
            if (self != null)
            {
                items.Add(self.m_itemData.m_shared.m_name);
            }
            var parts = new List<MonoBehaviour>();
            thing.GetComponentsInChildren(true, parts);
            foreach (MonoBehaviour part in parts)
            {
                switch (part)
                {
                    case Pickable pickable:
                        Next(pickable.m_itemPrefab);
                        Table(pickable.m_extraDrops);
                        break;
                    case PickableItem pickableItem:
                        Next(pickableItem.m_itemPrefab != null ? pickableItem.m_itemPrefab.gameObject : null);
                        if (pickableItem.m_randomItemPrefabs != null)
                        {
                            foreach (PickableItem.RandomItem random in pickableItem.m_randomItemPrefabs)
                            {
                                Next(random.m_itemPrefab != null ? random.m_itemPrefab.gameObject : null);
                            }
                        }
                        break;
                    case TreeBase tree:
                        Table(tree.m_dropWhenDestroyed);
                        Next(tree.m_logPrefab);
                        Next(tree.m_stubPrefab);
                        break;
                    case TreeLog log:
                        Table(log.m_dropWhenDestroyed);
                        Next(log.m_subLogPrefab);
                        break;
                    case MineRock rock:
                        Table(rock.m_dropItems);
                        break;
                    case MineRock5 rock5:
                        Table(rock5.m_dropItems);
                        break;
                    case Destructible destructible:
                        Next(destructible.m_spawnWhenDestroyed);
                        break;
                    case DropOnDestroyed drops:
                        Table(drops.m_dropWhenDestroyed);
                        break;
                    case Container container:
                        Table(container.m_defaultItems);
                        break;
                    case LootSpawner loot:
                        Table(loot.m_items);
                        break;
                    case SpawnArea area:
                        if (area.m_prefabs != null)
                        {
                            foreach (SpawnArea.SpawnData spawn in area.m_prefabs)
                            {
                                Next(spawn != null ? spawn.m_prefab : null);
                            }
                        }
                        break;
                    case CreatureSpawner spawner:
                        Next(spawner.m_creaturePrefab);
                        break;
                    case CharacterDrop characterDrop:
                        if (characterDrop.m_drops != null)
                        {
                            foreach (CharacterDrop.Drop drop in characterDrop.m_drops)
                            {
                                if (drop != null && drop.m_chance > 0f)
                                {
                                    Next(drop.m_prefab);
                                }
                            }
                        }
                        break;
                    case Character character:
                        // What it leaves when it dies, beyond its drops: the frost troll's remains, a boss's next phase.
                        if (character.m_deathEffects != null && character.m_deathEffects.m_effectPrefabs != null)
                        {
                            foreach (EffectList.EffectData effect in character.m_deathEffects.m_effectPrefabs)
                            {
                                if (effect != null && effect.m_enabled)
                                {
                                    Next(effect.m_prefab);
                                }
                            }
                        }
                        break;
                    case Growup growup:
                        Next(growup.m_grownPrefab);
                        if (growup.m_altGrownPrefabs != null)
                        {
                            foreach (Growup.GrownEntry grown in growup.m_altGrownPrefabs)
                            {
                                Next(grown != null ? grown.m_prefab : null);
                            }
                        }
                        break;
                    case Fish fish:
                        // The fish itself (an item, above). Its extra drops, a stray item on a fifth of catches, don't
                        // count: they would put onion seeds and sap in the Ocean.
                        Next(fish.m_pickupItem);
                        break;
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        // Working it out

        /// <summary>
        /// The working-out, kept apart from the game's objects so the same code can be checked outside the game. A rank
        /// is a place in the progression (0 = Meadows); lower is earlier.
        /// </summary>
        internal sealed class Solver
        {
            /// <summary>One way to make an item.</summary>
            private sealed class Route
            {
                internal string Output;
                internal string[] All;
                internal string[] AnyOf;
                internal string Kind;
                internal string Detail;
                internal int Floor;
            }

            /// <summary>Each placed item's rank.</summary>
            internal readonly Dictionary<string, int> Ranks = new Dictionary<string, int>();

            /// <summary>How each placed item got its rank: the kind of source (for the log) and which one.</summary>
            internal readonly Dictionary<string, (string Kind, string Detail)> How = new Dictionary<string, (string, string)>();

            private readonly List<Route> routes = new List<Route>();

            private readonly List<(string Item, int Rank, string Kind, string Detail)> variants = new List<(string, int, string, string)>();

            /// <summary>The world gives this item at this rank.</summary>
            internal void World(string item, int rank, string kind, string detail)
            {
                if (!string.IsNullOrEmpty(item) && rank >= 0)
                {
                    Offer(item, rank, kind, detail);
                }
            }

            /// <summary>A variant area (an alt biome) gives this item at this rank: counted only if nothing else places it.</summary>
            internal void Variant(string item, int rank, string kind, string detail)
            {
                if (!string.IsNullOrEmpty(item) && rank >= 0)
                {
                    variants.Add((item, rank, kind, detail));
                }
            }

            /// <summary>This item is made from all of <paramref name="all"/>, and one of <paramref name="anyOf"/> if given;
            /// never earlier than <paramref name="floor"/> (a trader's home).</summary>
            internal void Way(string output, List<string> all, List<string> anyOf, string kind, string detail, int floor = -1)
            {
                if (string.IsNullOrEmpty(output) || ((all == null || all.Count == 0) && (anyOf == null || anyOf.Count == 0)))
                {
                    return;
                }
                routes.Add(new Route
                {
                    Output = output,
                    All = all != null ? all.ToArray() : new string[0],
                    AnyOf = anyOf != null && anyOf.Count > 0 ? anyOf.ToArray() : null,
                    Kind = kind,
                    Detail = detail,
                    Floor = floor,
                });
            }

            /// <summary>
            /// Goes over the ways to make things until no item gets any earlier, every input placed. Then gives the items
            /// still unplaced what the variant areas give them, and goes over the ways again. Then, for the items still
            /// unplaced, once more leaving out the inputs nothing places (an essence no creature is known to drop
            /// shouldn't hide the Deep North weapons made with it); those inputs never decide anything else.
            /// </summary>
            internal void Solve()
            {
                Settle(null, null);
                var placed = new HashSet<string>(Ranks.Keys);
                bool more = false;
                foreach ((string item, int rank, string kind, string detail) in variants)
                {
                    if (!placed.Contains(item))
                    {
                        more |= Offer(item, rank, kind, detail);
                    }
                }
                if (more)
                {
                    Settle(null, null);
                }
                // A world key is a gate, not an ingredient: a ware behind a key nothing sets stays unplaced.
                var unplaced = new HashSet<string>();
                foreach (Route route in routes)
                {
                    foreach (string input in route.All)
                    {
                        if (input != null && !Ranks.ContainsKey(input) && !input.StartsWith(KeyPrefix))
                        {
                            unplaced.Add(input);
                        }
                    }
                }
                if (unplaced.Count > 0)
                {
                    Settle(unplaced, new HashSet<string>(Ranks.Keys));
                }
            }

            /// <summary>Offers every way until nothing changes. With <paramref name="leaveOut"/>, those inputs don't
            /// count, and only the items not in <paramref name="settled"/> are placed.</summary>
            private void Settle(HashSet<string> leaveOut, HashSet<string> settled)
            {
                bool changed = true;
                for (int pass = 0; changed && pass < 200; pass++)
                {
                    changed = false;
                    foreach (Route route in routes)
                    {
                        if (settled != null && settled.Contains(route.Output))
                        {
                            continue;
                        }
                        int rank = RankOf(route, leaveOut, out string without);
                        string detail = without == null ? route.Detail : route.Detail + ", leaving out " + without;
                        if (rank >= 0 && Offer(route.Output, rank, route.Kind, detail))
                        {
                            changed = true;
                        }
                    }
                }
            }

            private int RankOf(Route route, HashSet<string> leaveOut, out string without)
            {
                without = null;
                int rank = route.Floor;
                foreach (string input in route.All)
                {
                    if (input != null && leaveOut != null && leaveOut.Contains(input))
                    {
                        without = without == null ? input : without + ", " + input;
                        continue;
                    }
                    if (input == null || !Ranks.TryGetValue(input, out int inputRank))
                    {
                        return -1;
                    }
                    rank = Math.Max(rank, inputRank);
                }
                if (route.AnyOf != null)
                {
                    int best = -1;
                    foreach (string input in route.AnyOf)
                    {
                        if (input != null && Ranks.TryGetValue(input, out int inputRank) && (best < 0 || inputRank < best))
                        {
                            best = inputRank;
                        }
                    }
                    if (best < 0)
                    {
                        return -1;
                    }
                    rank = Math.Max(rank, best);
                }
                return rank;
            }

            private bool Offer(string item, int rank, string kind, string detail)
            {
                if (Ranks.TryGetValue(item, out int old) && old <= rank)
                {
                    return false;
                }
                Ranks[item] = rank;
                How[item] = (kind, detail);
                return true;
            }
        }
    }
}
