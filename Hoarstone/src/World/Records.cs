using System;
using System.Collections.Generic;
using Hoarstone.Shapes;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>One piece copied for the worker: where it stands, whether it's a Hoarstone, and its record's number.</summary>
    internal struct PieceCopy
    {
        internal float X;
        internal float Z;
        internal bool Stone;
        internal uint Id;
    }

    /// <summary>One piece a player built, as this game's record of it says.</summary>
    internal struct PieceRecord : IEquatable<PieceRecord>
    {
        internal float X;
        internal float Z;
        internal bool Stone;

        public bool Equals(PieceRecord other) => X == other.X && Z == other.Z && Stone == other.Stone;
    }

    /// <summary>
    /// Every piece a player built that this game holds a record of (A88's "work from the records"): a game keeps each
    /// record it receives for the whole session, so after a walk round a settlement it knows all of it, however large.
    /// Kept three ways, so it is both quick and whole:
    /// - **A slow pass over the game's records** (ZDOMan's records by sector, then its separate list of portals, which
    ///   the game keeps apart from every sector), a few thousand a frame, every few seconds: the truth, and on a game
    ///   hosting the world (or single player) every piece of the world from the start.
    /// - **Every piece that loads** (a postfix on Piece.Awake), so a piece just built counts at once.
    /// - **Every record destroyed** (ZDOMan.m_onZDODestroyed, which every game hears of), so a piece taken down anywhere
    ///   leaves at once.
    /// A pass started before an event never undoes it: what was added or removed while a pass ran is applied over it. A
    /// change of the kinds that count starts a new pass. Ships and carts never count (they move), nor the kinds "Pieces
    /// that never count" names. The zone controllers' records are noted on the way, for the saved tiles, and each zone
    /// remembers when a piece record in it last came, went or changed (SeenZones waits for its records to settle).
    /// </summary>
    internal static class Records
    {
        /// <summary>The Hoarstone's mark in a Ward's saved record (a bool). Permanent: it's in players' worlds.</summary>
        internal static readonly int MarkHash = "modprojects.qolmods.hoarstone".GetStableHashCode();

        /// <summary>What a Hoarstone cost when it was placed, in its saved record (the cost text's form), so taking it down
        /// gives back what was paid, whatever the cost setting says by then. Permanent.</summary>
        internal static readonly int PaidHash = "modprojects.qolmods.hoarstone.cost".GetStableHashCode();

        internal const string WardPrefab = "guard_stone";

        internal static readonly int WardHash = WardPrefab.GetStableHashCode();

        internal static readonly int ZoneControlHash = "_ZoneCtrl".GetStableHashCode();

        private const float PassEvery = 8f;
        private const int SlotsPerFrame = 16384;
        private const int RecordsPerFrame = 3000;

        private static Dictionary<ZDOID, PieceRecord> known = new Dictionary<ZDOID, PieceRecord>();
        private static Dictionary<ZoneId, ZDO> controls = new Dictionary<ZoneId, ZDO>();
        private static readonly Dictionary<ZoneId, float> lastChange = new Dictionary<ZoneId, float>();

        private static ZDOMan boundTo;
        private static ZNetScene hashesFor;
        private static int hashesNeverVersion = -1;
        private static HashSet<int> pieceHashes;

        // The pass under way.
        private static List<ZDO>[] sectors;
        private static int nextSector;
        private static Dictionary<ZDOID, PieceRecord> fresh;
        private static Dictionary<ZoneId, ZDO> freshControls;
        private static readonly Dictionary<ZDOID, PieceRecord> addedDuringPass = new Dictionary<ZDOID, PieceRecord>();
        private static readonly HashSet<ZDOID> removedDuringPass = new HashSet<ZDOID>();
        private static float nextPassAt;

        /// <summary>Goes up whenever the pieces known change.</summary>
        internal static int Version { get; private set; }

        /// <summary>How many full passes have finished since the world was entered (the first one makes the records
        /// whole on a game hosting the world).</summary>
        internal static int Passes { get; private set; }

        internal static int Count => known.Count;

        /// <summary>The zone controller's record of a zone, if this game holds it: still a zone controller, and still at
        /// that zone (the game reuses a destroyed record's object for another).</summary>
        internal static ZDO ControlOf(ZoneId zone)
        {
            if (!controls.TryGetValue(zone, out ZDO zdo) || zdo == null || !zdo.IsValid() || zdo.GetPrefab() != ZoneControlHash)
            {
                return null;
            }
            Vector3 at = zdo.GetPosition();
            return ZoneId.At(at.x, at.z).Equals(zone) ? zdo : null;
        }

        internal static IEnumerable<ZoneId> ControlZones => controls.Keys;

        /// <summary>When a piece record in this zone or one next to it last came, went or changed (the session's start
        /// where none has).</summary>
        internal static float LastChangeAround(ZoneId zone)
        {
            float last = float.NegativeInfinity;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (lastChange.TryGetValue(new ZoneId(zone.X + dx, zone.Z + dz), out float at) && at > last)
                    {
                        last = at;
                    }
                }
            }
            return last;
        }

        /// <summary>A copy of the pieces known, taken on the main thread; the worker sorts it (<see cref="Sorted"/>).</summary>
        internal static PieceCopy[] Snapshot()
        {
            var copy = new PieceCopy[known.Count];
            int k = 0;
            foreach (KeyValuePair<ZDOID, PieceRecord> entry in known)
            {
                copy[k++] = new PieceCopy { X = entry.Value.X, Z = entry.Value.Z, Stone = entry.Value.Stone, Id = entry.Key.ID };
            }
            return copy;
        }

        /// <summary>The pieces in a fixed order (by x, then z, then Hoarstones last, then the record's number), so every
        /// game sorts the same pieces into the same settlements. Safe on the worker thread.</summary>
        internal static List<ShapePoint> Sorted(PieceCopy[] copy)
        {
            Array.Sort(copy, (a, b) =>
            {
                int c = a.X.CompareTo(b.X);
                if (c != 0) return c;
                c = a.Z.CompareTo(b.Z);
                if (c != 0) return c;
                c = a.Stone.CompareTo(b.Stone);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            var points = new List<ShapePoint>(copy.Length);
            foreach (PieceCopy piece in copy)
            {
                points.Add(new ShapePoint(piece.X, piece.Z, piece.Stone));
            }
            return points;
        }

        /// <summary>A zone controller this game holds loaded (SeenZones hands over every spawn system it finds): its
        /// record is known at once, before the next pass, so its saved tile is read before the zone's spawning starts
        /// (ten seconds after it wakes).</summary>
        internal static void NoteControl(ZDO zdo)
        {
            if (zdo == null || boundTo == null || !zdo.IsValid() || zdo.GetPrefab() != ZoneControlHash)
            {
                return;
            }
            Vector3 at = zdo.GetPosition();
            var zone = ZoneId.At(at.x, at.z);
            controls[zone] = zdo;
            if (freshControls != null)
            {
                freshControls[zone] = zdo;
            }
        }

        /// <summary>Every frame: follows the world (a new one starts afresh), the kinds that count, and the pass.</summary>
        internal static void Tick(float now)
        {
            ZDOMan man = ZDOMan.instance;
            if (man != boundTo)
            {
                Bind(man, now);
            }
            if (man == null || ZNetScene.instance == null)
            {
                return;
            }
            if (pieceHashes == null || hashesFor != ZNetScene.instance || hashesNeverVersion != Settings.NeverCountVersion)
            {
                Kinds(now);
                sectors = null;    // a pass read with the old kinds is dropped: a new one starts at once
                fresh = null;
                freshControls = null;
                nextPassAt = now;
            }
            if (sectors == null && now >= nextPassAt)
            {
                sectors = GameHooks.Sectors(man);
                nextSector = 0;
                fresh = new Dictionary<ZDOID, PieceRecord>();
                freshControls = new Dictionary<ZoneId, ZDO>();
                addedDuringPass.Clear();
                removedDuringPass.Clear();
            }
            if (sectors != null)
            {
                Step(man, now);
            }
        }

        private static void Step(ZDOMan man, float now)
        {
            int slots = 0, records = 0;
            while (nextSector < sectors.Length && slots < SlotsPerFrame && records < RecordsPerFrame)
            {
                List<ZDO> list = sectors[nextSector++];
                slots++;
                if (list != null)
                {
                    records += list.Count;
                    Take(list);
                }
            }
            if (nextSector < sectors.Length)
            {
                return;
            }
            // The portals, which the game keeps in a list of their own and in no sector.
            foreach (KeyValuePair<ZoneSystem.SectorIndex, List<ZDO>> portals in man.GetPortals())
            {
                if (portals.Value != null)
                {
                    Take(portals.Value);
                }
            }
            // The pass is done: what happened while it ran wins over what it read.
            foreach (KeyValuePair<ZDOID, PieceRecord> added in addedDuringPass)
            {
                fresh[added.Key] = added.Value;
            }
            foreach (ZDOID removed in removedDuringPass)
            {
                fresh.Remove(removed);
            }
            if (Differ(fresh, known, now))
            {
                known = fresh;
                Version++;
            }
            controls = freshControls;
            sectors = null;
            fresh = null;
            freshControls = null;
            Passes++;
            nextPassAt = now + PassEvery;
        }

        private static void Take(List<ZDO> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                ZDO zdo = list[i];
                if (zdo == null)
                {
                    continue;
                }
                if (Read(zdo, out PieceRecord record))
                {
                    fresh[zdo.m_uid] = record;
                }
                else if (zdo.GetPrefab() == ZoneControlHash)
                {
                    Vector3 at = zdo.GetPosition();
                    freshControls[ZoneId.At(at.x, at.z)] = zdo;
                }
            }
        }

        // Whether the pass's pieces differ from those known; each zone where one came, went or moved is stamped.
        private static bool Differ(Dictionary<ZDOID, PieceRecord> next, Dictionary<ZDOID, PieceRecord> before, float now)
        {
            bool differ = false;
            foreach (KeyValuePair<ZDOID, PieceRecord> entry in next)
            {
                if (!before.TryGetValue(entry.Key, out PieceRecord other) || !other.Equals(entry.Value))
                {
                    differ = true;
                    Stamp(entry.Value, now);
                }
            }
            foreach (KeyValuePair<ZDOID, PieceRecord> entry in before)
            {
                if (!next.ContainsKey(entry.Key))
                {
                    differ = true;
                    Stamp(entry.Value, now);
                }
            }
            return differ;
        }

        private static void Stamp(PieceRecord record, float now) => lastChange[ZoneId.At(record.X, record.Z)] = now;

        // Whether a record is a piece that counts, and what it says of it.
        private static bool Read(ZDO zdo, out PieceRecord record)
        {
            record = default;
            int prefab = zdo.GetPrefab();
            if (!pieceHashes.Contains(prefab) || zdo.GetLong(ZDOVars.s_creator) == 0L)
            {
                return false;
            }
            Vector3 at = zdo.GetPosition();
            record = new PieceRecord { X = at.x, Z = at.z, Stone = prefab == WardHash && zdo.GetBool(MarkHash) };
            return true;
        }

        /// <summary>A piece that just loaded, or a record just changed here (a Hoarstone just marked): noted at once.</summary>
        internal static void Note(ZDO zdo)
        {
            if (zdo == null || pieceHashes == null || boundTo == null || !zdo.IsValid())
            {
                return;
            }
            if (Read(zdo, out PieceRecord record))
            {
                if (!known.TryGetValue(zdo.m_uid, out PieceRecord before) || !before.Equals(record))
                {
                    known[zdo.m_uid] = record;
                    Version++;
                    Stamp(record, Time.unscaledTime);
                }
                if (sectors != null)
                {
                    addedDuringPass[zdo.m_uid] = record;
                    removedDuringPass.Remove(zdo.m_uid);
                }
            }
        }

        private static void OnDestroyed(ZDO zdo)
        {
            try
            {
                if (zdo == null)
                {
                    return;
                }
                if (known.TryGetValue(zdo.m_uid, out PieceRecord record))
                {
                    known.Remove(zdo.m_uid);
                    Version++;
                    Stamp(record, Time.unscaledTime);
                }
                if (sectors != null)
                {
                    removedDuringPass.Add(zdo.m_uid);
                    addedDuringPass.Remove(zdo.m_uid);
                }
                if (zdo.GetPrefab() == ZoneControlHash)
                {
                    DropControl(controls, zdo);
                    if (freshControls != null)
                    {
                        DropControl(freshControls, zdo);
                    }
                }
            }
            catch (Exception error)
            {
                Module.Fail("noting a record destroyed", error);
            }
        }

        private static void DropControl(Dictionary<ZoneId, ZDO> map, ZDO zdo)
        {
            ZoneId found = default;
            bool any = false;
            foreach (KeyValuePair<ZoneId, ZDO> entry in map)
            {
                if (ReferenceEquals(entry.Value, zdo))
                {
                    found = entry.Key;
                    any = true;
                    break;
                }
            }
            if (any)
            {
                map.Remove(found);
            }
        }

        // The kinds of piece that count: every prefab with a Piece, but ships, carts and the setting's list.
        private static void Kinds(float now)
        {
            hashesFor = ZNetScene.instance;
            hashesNeverVersion = Settings.NeverCountVersion;
            var never = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in (Settings.NeverCount.Value ?? "").Split(','))
            {
                string trimmed = name.Trim();
                if (trimmed.Length > 0)
                {
                    never.Add(trimmed);
                }
            }
            var hashes = new HashSet<int>();
            foreach (GameObject prefab in hashesFor.m_prefabs)
            {
                if (prefab == null || never.Contains(prefab.name) || prefab.GetComponent<Piece>() == null)
                {
                    continue;
                }
                if (prefab.GetComponent<Ship>() != null || prefab.GetComponent<Vagon>() != null)
                {
                    continue;
                }
                hashes.Add(prefab.name.GetStableHashCode());
            }
            pieceHashes = hashes;
            if (known.Count > 0)
            {
                // Pieces of kinds that no longer count leave at once; the new pass brings in kinds that count again.
                var kept = new Dictionary<ZDOID, PieceRecord>();
                foreach (KeyValuePair<ZDOID, PieceRecord> entry in known)
                {
                    ZDO zdo = boundTo?.GetZDO(entry.Key);
                    if (zdo != null && pieceHashes.Contains(zdo.GetPrefab()))
                    {
                        kept[entry.Key] = entry.Value;
                    }
                    else
                    {
                        Stamp(entry.Value, now);
                    }
                }
                if (kept.Count != known.Count)
                {
                    known = kept;
                    Version++;
                }
            }
        }

        private static void Bind(ZDOMan man, float now)
        {
            Unbind();
            boundTo = man;
            if (man != null)
            {
                man.m_onZDODestroyed += OnDestroyed;
            }
            nextPassAt = now;
        }

        private static void Unbind()
        {
            if (boundTo != null)
            {
                boundTo.m_onZDODestroyed -= OnDestroyed;
            }
            boundTo = null;
            known = new Dictionary<ZDOID, PieceRecord>();
            controls = new Dictionary<ZoneId, ZDO>();
            lastChange.Clear();
            sectors = null;
            fresh = null;
            freshControls = null;
            addedDuringPass.Clear();
            removedDuringPass.Clear();
            pieceHashes = null;
            hashesFor = null;
            Passes = 0;
            Version++;
        }

        /// <summary>Forgets everything and stops listening (switched off for good, or after an error).</summary>
        internal static void Forget() => Unbind();
    }
}
