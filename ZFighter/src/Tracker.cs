using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ZFighter
{
    /// <summary>
    /// Every building piece loaded near the player: the size of its drawing, the pieces whose surfaces lie on its own
    /// where they overlap, and its level. A piece is taken in a moment after the game's own WearNTear.Awake, only a real
    /// piece with a saved record (never the placement ghost). Four times a second, for at most a few milliseconds, it
    /// takes in the pieces loaded since, lets go of the ones gone, and gives new levels to every group of overlapping
    /// pieces that changed. A level changes only the position and scale of the piece's drawing objects (New, Worn,
    /// Broken, the snow), and only objects with no collider, skinned mesh, cloth or animation anywhere below them,
    /// worked out from what the game set, so going back is exact. The piece's root, its colliders, its snap points
    /// and its saved record are never touched. A piece whose drawing can't all be moved that way is never nudged
    /// itself; the pieces laid into it are drawn off it instead.
    /// </summary>
    internal static class Tracker
    {
        /// <summary>How deep two drawings must be laid into each other to count (on each side, in metres): their boxes
        /// must still overlap once each is shrunk by this much, so pieces that only touch don't count.</summary>
        internal const double Margin = 0.005;

        /// <summary>How close two faces' planes must be (metres) to count as one plane: surfaces this close fight
        /// in the distance too.</summary>
        internal const double PlaneTolerance = 0.003;

        /// <summary>A snap-point box thinner than this along an axis (its half size, metres) is flat there (a floor's snap
        /// points all lie on one plane): that axis says nothing about its faces.</summary>
        internal const double FlatSnapBox = 0.025;

        private const float CellSize = 4f;
        private const float WorkEvery = 0.25f;
        private const double MillisecondsPerRound = 3.0;

        /// <summary>One drawing object that may be nudged: where it is in the piece (child indices from the root), and
        /// its name, to check a piece of the same prefab is built the same.</summary>
        private sealed class UnitPath
        {
            internal int[] Path;
            internal string Name;
            internal bool HoldsNew;     // holds part of the piece's drawing as new (or the piece has no m_new)
        }

        /// <summary>What every piece of one prefab shares, worked out once from the first one taken in: which of its
        /// objects may be nudged, and whether they hold all of its drawing.</summary>
        private sealed class Shape
        {
            internal readonly List<UnitPath> Units = new List<UnitPath>();
            internal bool Movable;
        }

        private sealed class Unit
        {
            internal Transform Object;
            internal Vector3 Position;
            internal Vector3 Scale;
            internal Vec ParentOffset;
            internal Vec ParentScale;
            internal Turn ParentTurn;
            internal Turn ObjectTurn;
        }

        private sealed class Entry
        {
            internal int Id;
            internal Piece GamePiece;
            internal Transform Root;
            internal string Name;
            internal readonly List<Unit> Units = new List<Unit>();
            internal bool Movable;      // every drawn part lies in a unit, and nothing else forbids it
            internal bool Slanted;
            internal int Across = -1;   // a slanted piece's axis across its slope (Kinds.AcrossAxis), -1 if none
            internal Vec Center;        // the drawing's centre, in the root's frame
            internal Vec WorldSize;     // the drawing's sides, in metres, along the root's axes
            internal Vec RootScale;
            internal Obb Box;
            internal bool HasSnapBox;   // two or more snap points: the box they span, whose faces lie where a real face does
            internal Obb SnapBox;
            internal double Volume;
            internal int Kind;
            internal PlaceKey Key;
            internal readonly List<Entry> Neighbours = new List<Entry>();
            internal readonly List<long> Cells = new List<long>();
            internal int Level;
            internal double Shown;      // the nudge on screen now, in metres (0: as the game draws it)
        }

        private static readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private static readonly List<WearNTear> waiting = new List<WearNTear>();
        private static readonly HashSet<int> gone = new HashSet<int>();
        private static readonly HashSet<Entry> dirty = new HashSet<Entry>();
        private static readonly Dictionary<long, List<Entry>> grid = new Dictionary<long, List<Entry>>();
        private static readonly Dictionary<string, Shape> shapes = new Dictionary<string, Shape>();
        private static readonly Stopwatch clock = new Stopwatch();

        private static readonly NameList leftAlone = new NameList(() => Settings.LeftAlone.Value);
        private static readonly NameList listed = new NameList(() => Settings.Listed.Value);

        private static bool wasOn;
        private static bool depthLogged;
        private static bool cameraLogged;
        private static bool reportWasOn;
        private static float nextWork;
        private static string settingsSeen;
        private static string lastReport;

        /// <summary>A piece the game has just woken (WearNTear.Awake's postfix). Only noted here; taken in at the next
        /// round of work.</summary>
        internal static void Woke(WearNTear piece)
        {
            if (wasOn && piece != null)
            {
                waiting.Add(piece);
            }
        }

        /// <summary>A piece being destroyed (WearNTear.OnDestroy's postfix): let go of it at the next round.</summary>
        internal static void Destroyed(WearNTear piece)
        {
            if (wasOn && piece != null)
            {
                gone.Add(piece.GetInstanceID());
            }
        }

        /// <summary>Each frame, also while the mod is switched off (the kit calls it), so off takes everything back at
        /// once and on draws every loaded piece again.</summary>
        internal static void Update()
        {
            if (!Module.IsOn)
            {
                if (wasOn)
                {
                    RestoreAll();
                    Clear();
                }
                return;
            }
            if (!wasOn)
            {
                wasOn = true;
                LogDepthOnce();
                foreach (WearNTear piece in UnityEngine.Object.FindObjectsByType<WearNTear>(FindObjectsSortMode.None))
                {
                    waiting.Add(piece);
                }
                nextWork = 0f;
            }
            if (Time.unscaledTime < nextWork)
            {
                return;
            }
            nextWork = Time.unscaledTime + WorkEvery;

            LogCameraOnce();
            LetGo();
            TakeIn();
            NoticeSettings();
            if (dirty.Count > 0)
            {
                GiveLevels();
            }
            else if (Settings.LogDetails.Value && !reportWasOn)
            {
                lastReport = null;
                Report();   // switched on: say where things stand now, not only at the next change
            }
            reportWasOn = Settings.LogDetails.Value;
        }

        /// <summary>Every drawing back as the game set it (switched off, or an error).</summary>
        internal static void RestoreAll()
        {
            foreach (Entry entry in entries.Values)
            {
                Restore(entry);
            }
        }

        /// <summary>Forget every piece (switched off, or an error, after RestoreAll).</summary>
        internal static void Clear()
        {
            entries.Clear();
            waiting.Clear();
            gone.Clear();
            dirty.Clear();
            grid.Clear();
            shapes.Clear();
            settingsSeen = null;
            lastReport = null;
            wasOn = false;
        }

        /// <summary>One line in the log, once a session: the screen's depth buffer by default on this computer. It tells
        /// how small a nudge can be and still stop the flicker far away. An error here costs only the line.</summary>
        private static void LogDepthOnce()
        {
            if (depthLogged)
            {
                return;
            }
            depthLogged = true;
            try
            {
                Module.Log.LogInfo($"The screen's depth buffer: {SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil)} " +
                    $"by default, reversed: {SystemInfo.usesReversedZBuffer}, graphics: {SystemInfo.graphicsDeviceType}.");
            }
            catch (Exception e)
            {
                Module.Log.LogInfo($"The screen's depth buffer couldn't be read ({e.GetType().Name}).");
            }
        }

        /// <summary>And once the game's camera is there: the depth of the picture it draws into, when that is a picture
        /// of its own (the game draws into one when its render scale is below the screen's).</summary>
        private static void LogCameraOnce()
        {
            if (cameraLogged)
            {
                return;
            }
            try
            {
                Camera camera = Camera.main;
                if (camera == null || Player.m_localPlayer == null)
                {
                    return;   // the main menu's camera isn't the one the world is drawn with
                }
                cameraLogged = true;
                RenderTexture target = camera.targetTexture;
                Module.Log.LogInfo(target == null
                    ? "The game's camera draws straight to the screen."
                    : $"The game's camera draws into a picture with depth {target.depthStencilFormat}.");
            }
            catch (Exception e)
            {
                cameraLogged = true;
                Module.Log.LogInfo($"The game's camera couldn't be read ({e.GetType().Name}).");
            }
        }

        private static void LetGo()
        {
            if (gone.Count == 0)
            {
                return;
            }
            foreach (int id in gone)
            {
                if (!entries.TryGetValue(id, out Entry entry))
                {
                    continue;
                }
                entries.Remove(id);
                dirty.Remove(entry);
                RemoveFromGrid(entry);
                foreach (Entry neighbour in entry.Neighbours)
                {
                    neighbour.Neighbours.Remove(entry);
                    dirty.Add(neighbour);
                }
                entry.Neighbours.Clear();
            }
            gone.Clear();
            // A piece destroyed before it was taken in: drop it from the waiting list too.
            waiting.RemoveAll(piece => piece == null);
        }

        /// <summary>Takes in waiting pieces for a few milliseconds a round, so a base loading all at once spreads over
        /// a few rounds instead of holding up a frame.</summary>
        private static void TakeIn()
        {
            if (waiting.Count == 0)
            {
                return;
            }
            clock.Restart();
            var taken = new List<Entry>();
            int done = 0;
            while (done < waiting.Count && (done == 0 || clock.Elapsed.TotalMilliseconds < MillisecondsPerRound))
            {
                WearNTear piece = waiting[done++];
                if (piece == null || entries.ContainsKey(piece.GetInstanceID()))
                {
                    continue;
                }
                Entry entry = Make(piece);
                if (entry != null)
                {
                    entries[entry.Id] = entry;
                    AddToGrid(entry);
                    taken.Add(entry);
                }
            }
            waiting.RemoveRange(0, done);
            foreach (Entry entry in taken)
            {
                FindNeighbours(entry);
                dirty.Add(entry);
            }
        }

        /// <summary>A piece as Z-Fighter sees it, or null for one it never counts: the placement ghost or any copy
        /// without a saved record, a piece attached to something (a ship, a cart, another mod's moving base), or one
        /// with nothing drawn. A piece kept as the game draws it (a turret, whose aim is read from inside its drawing; a
        /// piece whose drawing isn't all in objects that may move) is still counted, so the pieces laid into it are drawn
        /// off it.</summary>
        private static Entry Make(WearNTear piece)
        {
            ZNetView view = piece.GetComponent<ZNetView>();
            if (view == null || view.GetZDO() == null)
            {
                return null;
            }
            Transform root = piece.transform;
            if (root.parent != null || piece.GetComponentInParent<Rigidbody>() != null)
            {
                return null;
            }

            var entry = new Entry
            {
                Id = piece.GetInstanceID(),
                GamePiece = piece.GetComponent<Piece>(),
                Root = root,
                Name = Utils.GetPrefabName(root.gameObject),
            };
            int usage = entry.GamePiece != null ? (int)entry.GamePiece.m_usage : 0;
            entry.Kind = entry.GamePiece != null ? Kinds.Rank(usage) : 6;
            entry.Slanted = Kinds.Slanted(entry.Name, usage);
            entry.Across = entry.Slanted ? Kinds.AcrossAxis(entry.Name) : -1;

            Shape shape = ShapeOf(entry.Name, piece, root);
            entry.Movable = shape.Movable && piece.GetComponentInChildren<Turret>(true) == null && TakeUnits(entry, shape, root);
            if (!entry.Movable)
            {
                entry.Units.Clear();
            }
            if (!DrawnBox(entry, piece))
            {
                return null;
            }
            SnapBox(entry);

            Vector3 position = root.position;
            entry.Key = new PlaceKey((long)Math.Round(position.x * 1000), (long)Math.Round(position.y * 1000),
                (long)Math.Round(position.z * 1000), entry.Name?.ToLowerInvariant(), (long)Math.Round(root.eulerAngles.y * 10));
            return entry;
        }

        /// <summary>The prefab's shape, worked out from this piece the first time one of it is taken in (the costly
        /// part: looking at every component below each object), the same for every later one.</summary>
        private static Shape ShapeOf(string name, WearNTear piece, Transform root)
        {
            if (name != null && shapes.TryGetValue(name, out Shape known))
            {
                return known;
            }
            var shape = new Shape();
            var units = new List<Transform>();
            FindUnits(root, root, new List<int>(), shape, units);
            Transform drawnNew = piece.m_new != null ? piece.m_new.transform : null;
            for (int i = 0; i < units.Count; i++)
            {
                Transform unit = units[i];
                shape.Units[i].HoldsNew = drawnNew == null || unit == drawnNew || unit.IsChildOf(drawnNew) || drawnNew.IsChildOf(unit);
            }
            shape.Movable = shape.Units.Count > 0 && Covered(piece, root, units);
            if (name != null)
            {
                shapes[name] = shape;
            }
            return shape;
        }

        /// <summary>The drawing objects that may be nudged: below the root, the highest objects that draw a mesh and have
        /// no collider, skinned mesh, cloth or animation anywhere below them (moving one would move those too). Where
        /// something below an object forbids it, its children are looked at instead.</summary>
        private static void FindUnits(Transform root, Transform parent, List<int> path, Shape shape, List<Transform> found)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.GetComponentsInChildren<MeshRenderer>(true).Length == 0)
                {
                    continue;   // draws nothing (snap points, colliders, particles)
                }
                path.Add(i);
                if (Forbids(child))
                {
                    FindUnits(root, child, path, shape, found);
                }
                else
                {
                    shape.Units.Add(new UnitPath { Path = path.ToArray(), Name = child.name });
                    found.Add(child);
                }
                path.RemoveAt(path.Count - 1);
            }
        }

        /// <summary>Whether anything at or below this object must not be moved or scaled with the drawing: a collider, a
        /// skinned mesh, cloth (Unity's or the game's Magica Cloth), or an animation that sets transforms itself. Cloth
        /// and animation are told by their type's name, so the mod needs no reference to those parts of Unity.</summary>
        private static bool Forbids(Transform t)
        {
            foreach (Component component in t.GetComponentsInChildren<Component>(true))
            {
                if (component == null)
                {
                    continue;   // a missing script
                }
                if (component is Collider || component is SkinnedMeshRenderer)
                {
                    return true;
                }
                string type = component.GetType().Name;
                if (type == "Cloth" || type == "Animator" || type == "Animation" || type.StartsWith("MagicaCloth", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether the units hold every part the piece draws as it is new (WearNTear.m_new, or the whole piece
        /// when it has none): otherwise nudging it would leave part of its surface where it was, still flickering, and
        /// the piece is kept as the game draws it so its neighbours move off it instead.</summary>
        private static bool Covered(WearNTear piece, Transform root, List<Transform> units)
        {
            Transform drawn = piece.m_new != null ? piece.m_new.transform : root;
            foreach (MeshRenderer renderer in drawn.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (!renderer.enabled || filter == null || filter.sharedMesh == null || Hidden(renderer.transform, drawn))
                {
                    continue;
                }
                bool inside = false;
                foreach (Transform unit in units)
                {
                    if (renderer.transform.IsChildOf(unit))
                    {
                        inside = true;
                        break;
                    }
                }
                if (!inside)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>This piece's own drawing objects, by its prefab's shape, with what the game set on each. False when
        /// the piece isn't built as its prefab's first was (another mod added or took objects), so it is kept as the
        /// game draws it.</summary>
        private static bool TakeUnits(Entry entry, Shape shape, Transform root)
        {
            foreach (UnitPath unitPath in shape.Units)
            {
                Transform t = root;
                Transform parent = root;
                foreach (int index in unitPath.Path)
                {
                    if (index >= t.childCount)
                    {
                        return false;
                    }
                    parent = t;
                    t = t.GetChild(index);
                }
                if (t.name != unitPath.Name)
                {
                    return false;
                }
                Turn parentTurn = TurnOf(parent, root);
                Turn objectTurn = TurnOf(t, root);
                if (unitPath.HoldsNew && (!parentTurn.QuarterTurns || !objectTurn.QuarterTurns))
                {
                    // Only one factor on every side is exact for an object turned otherwise. Judged by the drawing as new
                    // only: the game turns some pieces' worn, broken and snow objects a degree or so, and growing those a
                    // hair unevenly moves them by a fraction of the nudge, where turning the whole piece slanted would
                    // weaken the nudge on its upright faces.
                    entry.Slanted = true;
                    entry.Across = -1;
                }
                Vector3 parentScale = parent == root ? Vector3.one : ScaleAgainstRoot(parent, root, parentTurn);
                if (parentScale.x <= 0 || parentScale.y <= 0 || parentScale.z <= 0)
                {
                    return false;
                }
                entry.Units.Add(new Unit
                {
                    Object = t,
                    Position = t.localPosition,
                    Scale = t.localScale,
                    ParentOffset = parent == root ? new Vec(0, 0, 0) : ToVec(root.InverseTransformPoint(parent.position)),
                    ParentScale = ToVec(parentScale),
                    ParentTurn = parentTurn,
                    ObjectTurn = objectTurn,
                });
            }
            return entry.Units.Count > 0;
        }

        /// <summary>An object's turn against the root: where its own axes point, in the root's frame.</summary>
        private static Turn TurnOf(Transform t, Transform root)
        {
            if (t == root)
            {
                return Turn.None;
            }
            Quaternion between = Quaternion.Inverse(root.rotation) * t.rotation;
            return new Turn(ToVec(between * Vector3.right), ToVec(between * Vector3.up), ToVec(between * Vector3.forward));
        }

        /// <summary>An object's scale along its own axes, measured in the root's units: its scale in the world divided by
        /// the root's along the root axis each of its axes runs on.</summary>
        private static Vector3 ScaleAgainstRoot(Transform t, Transform root, Turn turn)
        {
            Vector3 own = t.lossyScale;
            Vector3 rootScale = Abs(root.lossyScale);
            float Along(Vec axis) =>
                (float)(rootScale.x * Math.Abs(axis.X) + rootScale.y * Math.Abs(axis.Y) + rootScale.z * Math.Abs(axis.Z));
            return new Vector3(own.x / Along(turn.X), own.y / Along(turn.Y), own.z / Along(turn.Z));
        }

        /// <summary>The box of the piece's drawing as it is new (WearNTear.m_new; else the units; else everything the
        /// piece draws), from its meshes' bounds, which every mesh has whether or not it can be read: its centre and
        /// sides in the root's frame, its box in the world, its volume.</summary>
        private static bool DrawnBox(Entry entry, WearNTear piece)
        {
            Transform root = entry.Root;
            bool any = false;
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            void Take(Transform under)
            {
                foreach (MeshFilter filter in under.GetComponentsInChildren<MeshFilter>(true))
                {
                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    Mesh mesh = filter.sharedMesh;
                    if (renderer == null || !renderer.enabled || mesh == null || Hidden(filter.transform, under))
                    {
                        // The game's own spare parts (the break fragments' planks) are drawn by nothing, and objects
                        // switched off inside the drawing (a wood wall's own snow, which the game never shows) aren't
                        // part of what's seen.
                        continue;
                    }
                    Bounds bounds = mesh.bounds;
                    for (int c = 0; c < 8; c++)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                            new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                        Vector3 local = root.InverseTransformPoint(filter.transform.TransformPoint(corner));
                        min = Vector3.Min(min, local);
                        max = Vector3.Max(max, local);
                        any = true;
                    }
                }
            }
            if (piece.m_new != null)
            {
                Take(piece.m_new.transform);
            }
            if (!any)
            {
                foreach (Unit unit in entry.Units)
                {
                    Take(unit.Object);
                }
            }
            if (!any)
            {
                Take(root);
            }
            if (!any)
            {
                return false;
            }

            Vector3 rootScale = root.lossyScale;
            Vector3 center = (min + max) * 0.5f;
            Vector3 worldSize = Vector3.Scale(max - min, Abs(rootScale));
            entry.Center = ToVec(center);
            entry.WorldSize = ToVec(worldSize);
            entry.RootScale = ToVec(rootScale);
            entry.Volume = Math.Max(1e-9, (double)worldSize.x * worldSize.y * worldSize.z);
            entry.Box = new Obb(ToVec(root.TransformPoint(center)), ToVec(root.right), ToVec(root.up), ToVec(root.forward),
                ToVec(worldSize * 0.5f));
            return true;
        }

        /// <summary>Whether an object, or any object between it and <paramref name="top"/> (not counting the top, which
        /// a worn or broken piece switches off as a whole), is switched off.</summary>
        private static bool Hidden(Transform t, Transform top)
        {
            for (Transform at = t; at != null && at != top; at = at.parent)
            {
                if (!at.gameObject.activeSelf)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The box the piece's snap points span, in the world, when it has two or more: the game puts snap points
        /// on a piece's real corners and edges, so this box's faces lie where real faces do even where bumps or a beam on
        /// its back make the drawing's box bigger (stone walls, wood walls).</summary>
        private static void SnapBox(Entry entry)
        {
            entry.HasSnapBox = false;
            if (entry.GamePiece == null)
            {
                return;
            }
            var points = new List<Transform>();
            entry.GamePiece.GetSnapPoints(points);
            if (points.Count < 2)
            {
                return;
            }
            Transform root = entry.Root;
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            foreach (Transform point in points)
            {
                Vector3 local = root.InverseTransformPoint(point.position);
                min = Vector3.Min(min, local);
                max = Vector3.Max(max, local);
            }
            Vector3 center = (min + max) * 0.5f;
            Vector3 size = Vector3.Scale(max - min, Abs(root.lossyScale));
            entry.SnapBox = new Obb(ToVec(root.TransformPoint(center)), ToVec(root.right), ToVec(root.up), ToVec(root.forward),
                ToVec(size * 0.5f));
            entry.HasSnapBox = true;
        }

        /// <summary>The pieces this one fights with: their drawings laid into each other, and a face of one on a face of
        /// the other's plane, by the drawings' boxes or by the snap points' boxes. Two slanted pieces (roofs) are linked
        /// by the overlap alone, since their boxes don't show their sloped faces; a slanted piece and a box-like one need
        /// a shared plane like any other two, so a roof isn't linked to every wall it rests on.</summary>
        private static void FindNeighbours(Entry entry)
        {
            entry.Box.WorldBox(out Vec min, out Vec max);
            foreach (long cell in Cells(min, max))
            {
                if (!grid.TryGetValue(cell, out List<Entry> inCell))
                {
                    continue;
                }
                foreach (Entry other in inCell)
                {
                    if (other == entry || entry.Neighbours.Contains(other))
                    {
                        continue;
                    }
                    if (Obb.Overlap(entry.Box, other.Box, Margin)
                        && ((entry.Slanted && other.Slanted)
                            || Obb.SharePlane(entry.Box, other.Box, PlaneTolerance)
                            || (entry.HasSnapBox && other.HasSnapBox
                                && Obb.SharePlane(entry.SnapBox, other.SnapBox, PlaneTolerance, FlatSnapBox))))
                    {
                        entry.Neighbours.Add(other);
                        other.Neighbours.Add(entry);
                        dirty.Add(other);
                    }
                }
            }
        }

        private static void AddToGrid(Entry entry)
        {
            entry.Box.WorldBox(out Vec min, out Vec max);
            foreach (long cell in Cells(min, max))
            {
                if (!grid.TryGetValue(cell, out List<Entry> inCell))
                {
                    grid[cell] = inCell = new List<Entry>();
                }
                inCell.Add(entry);
                entry.Cells.Add(cell);
            }
        }

        private static void RemoveFromGrid(Entry entry)
        {
            foreach (long cell in entry.Cells)
            {
                if (grid.TryGetValue(cell, out List<Entry> inCell))
                {
                    inCell.Remove(entry);
                    if (inCell.Count == 0)
                    {
                        grid.Remove(cell);
                    }
                }
            }
            entry.Cells.Clear();
        }

        private static IEnumerable<long> Cells(Vec min, Vec max)
        {
            int x0 = (int)Math.Floor(min.X / CellSize), x1 = (int)Math.Floor(max.X / CellSize);
            int z0 = (int)Math.Floor(min.Z / CellSize), z1 = (int)Math.Floor(max.Z / CellSize);
            for (int x = x0; x <= x1; x++)
            {
                for (int z = z0; z <= z1; z++)
                {
                    yield return ((long)x << 32) ^ (uint)z;
                }
            }
        }

        /// <summary>When a setting that changes the levels or the nudge changes (or the player arrives, which tells
        /// whose pieces are whose), every piece's level is worked out again.</summary>
        private static void NoticeSettings()
        {
            string now = string.Join("|", Settings.WhichOnTop.Value, Settings.NudgeMm.Value, Settings.Pieces.Value,
                listed.Text, leftAlone.Text, Settings.Others.Value, MyId());
            if (now == settingsSeen)
            {
                return;
            }
            settingsSeen = now;
            foreach (Entry entry in entries.Values)
            {
                dirty.Add(entry);
            }
        }

        /// <summary>New levels for every group of overlapping pieces with a piece that changed, each group as a whole,
        /// so the levels are the same for the same pieces however they loaded.</summary>
        private static void GiveLevels()
        {
            OnTop rule = Settings.WhichOnTop.Value;
            double nudge = Settings.NudgeMm.Value;
            long me = MyId();
            var seen = new HashSet<Entry>();
            var group = new List<Entry>();
            var queue = new Queue<Entry>();
            var nodes = new List<LevelNode>();
            var index = new Dictionary<Entry, int>();
            int cappedTotal = 0;

            foreach (Entry start in dirty)
            {
                if (seen.Contains(start) || !entries.ContainsKey(start.Id))
                {
                    continue;
                }
                group.Clear();
                queue.Enqueue(start);
                seen.Add(start);
                while (queue.Count > 0)
                {
                    Entry e = queue.Dequeue();
                    group.Add(e);
                    foreach (Entry n in e.Neighbours)
                    {
                        if (seen.Add(n))
                        {
                            queue.Enqueue(n);
                        }
                    }
                }

                nodes.Clear();
                index.Clear();
                for (int i = 0; i < group.Count; i++)
                {
                    index[group[i]] = i;
                }
                foreach (Entry e in group)
                {
                    var node = new LevelNode
                    {
                        Fixed = !e.Movable || LeftOut(e, me),
                        Volume = e.Volume,
                        Kind = e.Kind,
                        Key = e.Key,
                    };
                    foreach (Entry n in e.Neighbours)
                    {
                        if (index.TryGetValue(n, out int j))
                        {
                            node.Neighbours.Add(j);
                        }
                    }
                    nodes.Add(node);
                }

                int[] levels = Levels.Assign(nodes, rule, out int capped);
                cappedTotal += capped;
                for (int i = 0; i < group.Count; i++)
                {
                    Entry e = group[i];
                    e.Level = levels[i];
                    double distance = Nudge.Distance(levels[i], nudge);
                    if (distance != e.Shown)
                    {
                        Show(e, distance);
                    }
                }
            }
            dirty.Clear();

            if (cappedTotal > 0)
            {
                Module.Log.LogInfo($"{cappedTotal} piece(s) are laid into so many others that they share the highest " +
                    "level with one of them; where those two meet, they may still flicker.");
            }
            Report();
        }

        private static long MyId()
        {
            if (Game.instance == null)
            {
                return 0;
            }
            PlayerProfile profile = Game.instance.GetPlayerProfile();
            return profile != null ? profile.GetPlayerID() : 0;
        }

        /// <summary>Whether a piece stays as the game draws it, by the settings: on "Pieces left alone", not on "Listed
        /// pieces" while only those are fixed, or built by someone else while only mine are.</summary>
        private static bool LeftOut(Entry entry, long me)
        {
            if (leftAlone.Has(entry.Name))
            {
                return true;
            }
            if (Settings.Pieces.Value == WhichPieces.OnlyListedPieces && !listed.Has(entry.Name))
            {
                return true;
            }
            if (Settings.Others.Value == OthersPieces.OnlyMine)
            {
                long creator = entry.GamePiece != null ? entry.GamePiece.GetCreator() : 0;
                return me == 0 || creator != me;
            }
            return false;
        }

        /// <summary>Draws a piece nudged by a distance (metres; 0: as the game draws it).</summary>
        private static void Show(Entry entry, double distance)
        {
            if (distance <= 0)
            {
                Restore(entry);
                return;
            }
            entry.Shown = distance;   // before moving anything, so a restore after an error takes back a part-done move
            Vec factors = Nudge.Factors(entry.WorldSize, distance, entry.Slanted, entry.Across);
            Vec lift = new Vec(0, Nudge.LiftFor(distance, entry.Slanted) / Math.Abs(entry.RootScale.Y), 0);
            foreach (Unit unit in entry.Units)
            {
                if (unit.Object == null)
                {
                    continue;
                }
                Nudge.Place(entry.Center, factors, lift, unit.ParentOffset, unit.ParentScale, unit.ParentTurn,
                    unit.ObjectTurn, ToVec(unit.Position), ToVec(unit.Scale), out Vec position, out Vec scale);
                unit.Object.localPosition = ToVector3(position);
                unit.Object.localScale = ToVector3(scale);
            }
        }

        private static void Restore(Entry entry)
        {
            if (entry.Shown == 0)
            {
                return;
            }
            foreach (Unit unit in entry.Units)
            {
                if (unit.Object != null)
                {
                    unit.Object.localPosition = unit.Position;
                    unit.Object.localScale = unit.Scale;
                }
            }
            entry.Shown = 0;
        }

        private static void Report()
        {
            if (!Settings.LogDetails.Value)
            {
                return;
            }
            int overlapping = 0, nudged = 0, highest = 0, kept = 0;
            foreach (Entry entry in entries.Values)
            {
                if (entry.Neighbours.Count > 0)
                {
                    overlapping++;
                    if (!entry.Movable)
                    {
                        kept++;
                    }
                }
                if (entry.Shown > 0)
                {
                    nudged++;
                }
                highest = Math.Max(highest, entry.Level);
            }
            string report = $"{entries.Count} pieces loaded, {overlapping} laid into others, {nudged} drawn nudged " +
                $"(highest level {highest}); {kept} of those laid into others can't be nudged themselves, so the others " +
                "are drawn off them.";
            if (report != lastReport)
            {
                lastReport = report;
                Module.Log.LogInfo(report);
            }
        }

        private static Vec ToVec(Vector3 v) => new Vec(v.x, v.y, v.z);

        private static Vector3 ToVector3(Vec v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);

        private static Vector3 Abs(Vector3 v) => new Vector3(Math.Abs(v.x), Math.Abs(v.y), Math.Abs(v.z));
    }
}
