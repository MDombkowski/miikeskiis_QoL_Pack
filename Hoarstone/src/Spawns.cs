using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// Keeping monsters from spawning inside a settlement's shape: the game's own tests, each refusing a point inside one
    /// of the shapes "Where" picks (research 2026-10-10_hoarstone-what-the-game-settles, part 2). They run in the game that
    /// owns the zone, where spawning runs. Each changes nothing while the mod or "Keep monsters from spawning in
    /// settlements" is off, and lets through, as the game does near a base, what the game lets into a base (with "Let in
    /// what the game lets into a base" on).
    /// </summary>
    internal static class Spawns
    {
        internal static bool IsRaidSpawn(SpawnSystem.SpawnData spawn)
        {
            List<SpawnSystem.SpawnData> raid = RandEventSystem.instance != null ? RandEventSystem.instance.GetCurrentSpawners() : null;
            return raid != null && raid.Contains(spawn);
        }
    }

    /// <summary>The zone spawner's test of a point (SpawnSystem.IsSpawnPointGood), used for every try and every member of
    /// a group, raids' included: a point it passed is refused inside a shape.</summary>
    [HarmonyPatch(typeof(SpawnSystem), "IsSpawnPointGood")]
    internal static class SpawnPointPatch
    {
        private static void Postfix(SpawnSystem.SpawnData spawn, ref Vector3 spawnPoint, ref bool __result)
        {
            if (!__result || !Module.IsOn)
            {
                return;
            }
            try
            {
                if (!Settings.KeepSpawnsOut.Value)
                {
                    return;
                }
                if (spawn != null && spawn.m_insidePlayerBase && Settings.LetInWhatTheGameLetsIn.Value)
                {
                    return;
                }
                if (!Settings.RaidMonsters.Value && Spawns.IsRaidSpawn(spawn))
                {
                    return;
                }
                if (Coverage.Inside(spawnPoint, Settings.KeptLayers))
                {
                    __result = false;
                }
            }
            catch (Exception error)
            {
                Module.Fail("checking a spawn point", error);
            }
        }
    }

    /// <summary>A fixed spawn point (CreatureSpawner.UpdateSpawner) inside a shape doesn't spawn, as the game already
    /// stops one inside a base. Its one-time check of its place runs first, as the game's does.</summary>
    [HarmonyPatch(typeof(CreatureSpawner), "UpdateSpawner")]
    internal static class FixedSpawnPointPatch
    {
        private static bool Prefix(CreatureSpawner __instance)
        {
            if (!Module.IsOn)
            {
                return true;
            }
            try
            {
                if (!Settings.KeepSpawnsOut.Value || !Settings.FixedSpawnPoints.Value)
                {
                    return true;
                }
                if (__instance.m_spawnInPlayerBase && Settings.LetInWhatTheGameLetsIn.Value)
                {
                    return true;
                }
                ZNetView view = GameHooks.SpawnerView(__instance);
                if (view == null || !view.IsValid() || !view.IsOwner())
                {
                    return true;   // not ours to spawn: the game's own code, which does nothing here
                }
                if (!Coverage.Inside(__instance.transform.position, Settings.KeptLayers))
                {
                    return true;
                }
                // Kept out. What the game's own run would still have done before its base check (review round 1: on its
                // first run, so it can't spawn once each time its area loads): its one look at the place it stands in,
                // which removes a spawner its place doesn't allow; then, while the creature it spawned lives, note it
                // alive, so its respawn timer starts only once that creature is gone.
                if (!GameHooks.SpawnerCheckedLocation(__instance))
                {
                    Location place = Location.GetLocation(__instance.transform.position);
                    GameHooks.SpawnerLocation(__instance) = place;
                    GameHooks.SpawnerCheckedLocation(__instance) = true;
                    if (place != null && place.m_blockSpawnGroups.Contains(__instance.m_spawnGroupID))
                    {
                        view.Destroy();
                        return false;
                    }
                }
                ZDO zdo = view.GetZDO();
                ZDOConnection connection = zdo.GetConnection();
                if (__instance.m_respawnTimeMinuts <= 0f && connection != null && connection.m_type == ZDOExtraData.ConnectionType.Spawned)
                {
                    return false;   // a spawner that spawns once and has: the game writes nothing more either
                }
                ZDOID spawned = zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Spawned);
                if (!spawned.IsNone() && ZDOMan.instance.GetZDO(spawned) != null)
                {
                    zdo.Set(ZDOVars.s_aliveTime, ZNet.instance.GetTime().Ticks);
                }
                return false;
            }
            catch (Exception error)
            {
                Module.Fail("checking a fixed spawn point", error);
                return true;
            }
        }
    }

    /// <summary>A nest's pick of a spot (SpawnArea.FindSpawnPoint): with "Nests inside a settlement too", a spot inside a
    /// shape is refused, so the nest tries again at its next turn. The game itself never stops nests near a base.</summary>
    [HarmonyPatch(typeof(SpawnArea), "FindSpawnPoint")]
    internal static class NestPatch
    {
        private static void Postfix(ref bool __result, ref Vector3 point)
        {
            if (!__result || !Module.IsOn)
            {
                return;
            }
            try
            {
                if (Settings.KeepSpawnsOut.Value && Settings.Nests.Value && Coverage.Inside(point, Settings.KeptLayers))
                {
                    __result = false;
                }
            }
            catch (Exception error)
            {
                Module.Fail("checking a nest's spot", error);
            }
        }
    }
}
