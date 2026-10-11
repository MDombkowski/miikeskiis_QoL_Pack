using System;
using System.Collections.Generic;
using Hoarstone.Shapes;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>What this game held whole at the moment a round of working out took its pieces (review round 3):
    /// everything about a round's shapes is judged by it, since the shapes know only the pieces of that moment.</summary>
    internal sealed class SeenSnapshot
    {
        internal readonly Dictionary<ZoneId, long> Held = new Dictionary<ZoneId, long>();
        internal readonly HashSet<ZoneId> Seen = new HashSet<ZoneId>();
        internal bool Everything;
        internal long At;
    }

    /// <summary>
    /// Which zones' records this game holds whole, and since when (the plan's rule, after review rounds 1 and 2: a zone
    /// loaded is not yet a zone whose records have arrived, since the server sends them a little at a time, the pieces
    /// before a zone's controller, nearest first).
    /// - **Settled**: within the game's near simulation distance (the zones it is sent records for; a zone lingering a
    ///   few seconds after it left that distance doesn't count), loaded for 4 seconds, its controller's record here, and
    ///   no piece record in it or next to it come, gone or changed for 4 seconds.
    /// - **Seen**: settled once this session. It stays seen after the zone unloads (its records stay), but not while the
    ///   zone is loaded again and not yet settled; and a zone that unloads before it settles again, after records in it
    ///   changed meanwhile, is seen no more (its records are part old, part new).
    /// - **Held at**: the server's time, in seconds, when the zone was last settled here (while it stays settled, now):
    ///   how recent this game's knowledge of it is, against a saved tile's sources (Tiles.Accounts), taken into a
    ///   SeenSnapshot for each round of working out.
    /// A game hosting the world (single player too) holds every record of the world: every zone is seen there, and held
    /// now, once the first pass over the records is done.
    /// Every spawn system loaded here is handed to Records on the way, so a zone's controller is known as soon as it
    /// loads, before its spawning starts.
    /// </summary>
    internal static class SeenZones
    {
        private const float LoadedFor = 4f;
        private const float QuietFor = 4f;
        private const float LookEvery = 0.5f;

        private static readonly Dictionary<ZoneId, float> loadedSince = new Dictionary<ZoneId, float>();
        private static readonly HashSet<ZoneId> seen = new HashSet<ZoneId>();
        private static readonly HashSet<ZoneId> unsettled = new HashSet<ZoneId>();
        private static readonly Dictionary<ZoneId, long> heldAt = new Dictionary<ZoneId, long>();
        private static readonly List<ZoneId> gone = new List<ZoneId>();
        private static float nextLook;

        /// <summary>Goes up whenever a zone becomes seen, or a seen zone becomes unsettled, settled again or unseen.</summary>
        internal static int Version { get; private set; }

        /// <summary>Whether this game holds every record of the world (it hosts it, and its first pass is done).</summary>
        internal static bool Everything => ZNet.instance != null && ZNet.instance.IsServer() && Records.Passes > 0;

        /// <summary>Whether this game hosts the world but hasn't read its records yet: nothing is whole then.</summary>
        internal static bool HostNotReady => ZNet.instance != null && ZNet.instance.IsServer() && Records.Passes == 0;

        /// <summary>The server's time now, in seconds.</summary>
        internal static long Now() => ZNet.instance != null ? ZNet.instance.GetTime().Ticks / TimeSpan.TicksPerSecond : 0L;

        /// <summary>What this game holds whole now, for a round of working out about to take its pieces.</summary>
        internal static SeenSnapshot Snapshot()
        {
            var snap = new SeenSnapshot { Everything = Everything, At = Now() };
            if (HostNotReady)
            {
                return snap;
            }
            foreach (ZoneId zone in seen)
            {
                if (!unsettled.Contains(zone) && heldAt.TryGetValue(zone, out long at))
                {
                    snap.Seen.Add(zone);
                    snap.Held[zone] = at;
                }
            }
            return snap;
        }

        internal static void Tick(float now)
        {
            if (now < nextLook)
            {
                return;
            }
            nextLook = now + LookEvery;
            Player player = Player.m_localPlayer;
            ZoneSystem zones = ZoneSystem.instance;
            if (player == null || zones == null || ZNet.instance == null)
            {
                return;
            }
            foreach (SpawnSystem spawner in GameHooks.SpawnSystems())
            {
                ZNetView view = spawner != null ? spawner.GetComponent<ZNetView>() : null;
                if (view != null && view.IsValid())
                {
                    Records.NoteControl(view.GetZDO());
                }
            }
            long serverNow = Now();
            Vector3 at = player.transform.position;
            var here = ZoneId.At(at.x, at.z);
            SimulationDistance distance = ZNet.instance.GetSyncedSimulationDistance();
            int near = Math.Max(0, distance.NearSimulationDistance);
            bool InNear(ZoneId zone) =>
                Math.Abs(zone.X - here.X) <= near && Math.Abs(zone.Z - here.Z) <= near &&
                (distance.IsClassic || zones.ZonesWithinRadius(new Vector2s(here.X, here.Z), new Vector2s(zone.X, zone.Z), near));

            foreach (KeyValuePair<ZoneId, float> entry in loadedSince)
            {
                if (!InNear(entry.Key) || !zones.IsZoneLoaded(new Vector2s(entry.Key.X, entry.Key.Z)))
                {
                    gone.Add(entry.Key);
                }
            }
            foreach (ZoneId zone in gone)
            {
                float since = loadedSince[zone];
                loadedSince.Remove(zone);
                if (unsettled.Remove(zone))
                {
                    // Left before it settled: if records in it changed meanwhile, what this game holds of it is part old,
                    // part new (review round 2), so it isn't seen until it settles again.
                    if (seen.Contains(zone) && Records.LastChangeAround(zone) >= since)
                    {
                        seen.Remove(zone);
                        heldAt.Remove(zone);
                    }
                    Version++;
                }
            }
            gone.Clear();
            for (int dz = -near; dz <= near; dz++)
            {
                for (int dx = -near; dx <= near; dx++)
                {
                    var zone = new ZoneId(here.X + dx, here.Z + dz);
                    if (!InNear(zone) || !zones.IsZoneLoaded(new Vector2s(zone.X, zone.Z)))
                    {
                        continue;
                    }
                    if (!loadedSince.TryGetValue(zone, out float since))
                    {
                        loadedSince[zone] = since = now;
                    }
                    bool settled = now - since >= LoadedFor && now - Records.LastChangeAround(zone) >= QuietFor && Records.ControlOf(zone) != null;
                    if (settled)
                    {
                        heldAt[zone] = serverNow;
                        bool changed = seen.Add(zone);
                        changed |= unsettled.Remove(zone);
                        if (changed)
                        {
                            Version++;
                        }
                    }
                    else if (unsettled.Add(zone) && seen.Contains(zone))
                    {
                        Version++;
                    }
                }
            }
        }

        internal static void Forget()
        {
            loadedSince.Clear();
            seen.Clear();
            unsettled.Clear();
            heldAt.Clear();
            Version++;
        }
    }
}
