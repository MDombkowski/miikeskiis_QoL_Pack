using System.Collections.Generic;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// The ghost of a terrain tool, after the game has placed it each frame (and again just before a click places the
    /// stroke, since the game's click runs the same method first):
    /// - **A square tool's ghost** snaps to the nearest of the ground's points and takes its turn.
    /// - **Flatten's ghost** takes the height it will level to (his keys; <see cref="FlattenTarget"/>), so the stroke
    ///   carries it.
    /// - **Wards and no-build places:** a stroke bigger than its tool's own size, Lower ground, Flatten, and every square
    ///   are checked against them over the whole of what they change; the ghost turns red when it can't be placed.
    /// - **The preview** (his first test, 2026-09-30): with "Preview grid" on, the grid shows the points the click will
    ///   change (Preview, Grid), with the stroke's edge on the ground, in place of the game's round marker; off, the round
    ///   marker is drawn at the size the stroke really has, and a square's outline shows as before.
    /// </summary>
    internal static class Ghost
    {
        // A square's answer, kept for a moment while it stays put: near a ward that bars him, each of its points is asked
        // about, and a busy base would otherwise cost that every frame (review round 1). Wards and no-build places are
        // measured along the ground only, so the height isn't part of what it's kept for (review round 2).
        private const float RecheckAfter = 0.25f;
        private static Footprint checkedSquare;
        private static float checkedAt = -1f;

        // How far past its radius a round stroke can change ground: its heights are measured from the ground's point
        // nearest its middle (up to 0.71 m off), and its paint from a point up to 1.41 m off (review round 2).
        internal const float RoundStrokeSlack = 1.42f;
        private static Player.PlacementStatus checkedStatus;
        private static Vector3 blockedPoint;

        private static readonly List<PointChange> Changes = new List<PointChange>();
        private static readonly List<float> Heights = new List<float>();

        internal static void After(Player player, bool flash)
        {
            GameObject ghost = GameHooks.PlacementGhost(player);
            Piece selected = player.GetBuildTool()?.GetSelectedPiece();
            TerrainOp op = selected != null ? selected.GetComponent<TerrainOp>() : null;
            if (ghost == null || !ghost.activeSelf || op == null)
            {
                Marks.Hide();
                Grid.Hide();
                return;
            }
            string prefab = selected.gameObject.name;
            Tool tool = Tool.Named(prefab);
            var facts = new Facts(op.m_settings, tool, Settings.DigDepth.Value);
            Numbers numbers = Strokes.NumbersFor(prefab, tool, facts);
            Player.PlacementStatus status = GameHooks.PlacementStatus(player);
            bool grid = Settings.PreviewGrid.Value;

            if (tool != null && tool.IsSlope)
            {
                AfterSlope(player, ghost, selected, tool, facts, numbers, status, flash, grid, prefab);
                return;
            }

            if (tool != null && tool.Square)
            {
                Vector3 at = ghost.transform.position;
                at.x = Mathf.Round(at.x);
                at.z = Mathf.Round(at.z);
                if (!AtFeet(player, selected) && ZoneSystem.instance != null)
                {
                    at.y = ZoneSystem.instance.GetGroundHeight(at);
                }
                float turn = Turn.Effective(GameHooks.PlaceRotation(player));
                var square = new Footprint(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.z), numbers.Points, numbers.Across, turn);
                if (tool.Flattens)
                {
                    at.y = FlattenTarget(player, at, square, null);
                }
                ghost.transform.position = at;
                // The square's own turn (a square's 0 to 90°, a rectangle's 0 to 180°), which its stroke's facing sends.
                ghost.transform.rotation = Quaternion.Euler(0f, (float)square.TurnDegrees, 0f);
                FollowTurnSetting(ghost, Settings.Turn.Value != TurnChoice.DontTurn);

                if (status == Player.PlacementStatus.Valid)
                {
                    status = SquareStatus(square, at, flash);
                }
                Set(player, status);
                bool valid = status == Player.PlacementStatus.Valid;
                float lift = tool.IsPrecisionRaise ? numbers.Lift : 0f;
                Marks.HideRing();
                if (grid)
                {
                    long key = Key(prefab, square.CentreX, square.CentreZ, square.Length * 64 + square.Width, (float)square.TurnDegrees, at.y, numbers, valid);
                    if (Grid.Wants(key))
                    {
                        Preview.Square(square, Strokes.KindOf(tool, numbers), SquareTarget(tool, op.m_settings, at.y), numbers.Lift, GroundLookup.Reader, Changes);
                    }
                    bool drawn = Grid.Show(key, Changes, LookOf(tool, op.m_settings), Grid.FinishOf(op.m_settings.m_paintType), valid);
                    Marks.ShowSquare(square, at.y, lift, valid);
                    ShowRoundMarker(ghost, !drawn);
                    return;
                }
                Grid.Hide();
                bool outlined = Marks.ShowSquare(square, at.y, lift, valid);
                // Without the outline, the game's own round marker shows where the stroke goes.
                ShowRoundMarker(ghost, !outlined);
                return;
            }

            Marks.HideSquare();
            bool resized = (numbers.Chosen & Chosen.Radius) != 0 && Mathf.Abs(numbers.Radius - facts.Radius) > 0.01f;
            Vector3 centre = ghost.transform.position;
            bool flatten = tool != null && tool.Flattens;
            RoundStroke stroke = RoundStroke.Of(op.m_settings, numbers, centre, flatten);
            if (flatten)
            {
                centre.y = FlattenTarget(player, centre, null, stroke.SmoothRadius);
                ghost.transform.position = centre;
                stroke = RoundStroke.Of(op.m_settings, numbers, centre, true);
            }
            if (status == Player.PlacementStatus.Valid)
            {
                bool lower = tool != null && tool.IsLowerGround;
                if (Settings.WholeStrokeWards.Value && (lower || flatten || (resized && numbers.Radius > facts.Radius)))
                {
                    float reach = numbers.Radius + RoundStrokeSlack;
                    if (!PrivateArea.CheckAccess(centre, reach, flash, true))
                    {
                        Set(player, Player.PlacementStatus.PrivateZone);
                    }
                    else if (NoBuildNear(centre, reach))
                    {
                        Set(player, Player.PlacementStatus.NoBuildZone);
                    }
                }
                else if (lower && !PrivateArea.CheckAccess(centre, numbers.Radius, flash, false))
                {
                    // As the game checks a pickaxe's dig: over its whole reach, any ward that lets him in enough.
                    Set(player, Player.PlacementStatus.PrivateZone);
                }
            }
            bool placeable = GameHooks.PlacementStatus(player) == Player.PlacementStatus.Valid;
            if (grid)
            {
                float edge = RingRadius(stroke);
                long key = Key(prefab, Preview.Nearest(centre.x), Preview.Nearest(centre.z), Preview.Nearest(centre.x - 0.5f) * 7919 + Preview.Nearest(centre.z - 0.5f),
                               edge, centre.y, numbers, placeable);
                if (Grid.Wants(key))
                {
                    Preview.Round(stroke, GroundLookup.Reader, Changes);
                }
                bool drawn = Grid.Show(key, Changes, LookOf(tool, op.m_settings), Grid.FinishOf(op.m_settings.m_paintType), placeable);
                if (drawn)
                {
                    Marks.ShowRing(RingCentre(stroke), edge, placeable);
                }
                else
                {
                    Marks.HideRing();
                }
                Marks.ScaleRing(ghost, selected, drawn ? 1f : MarkerFactor(selected, tool, facts, numbers, resized, stroke.PaintRadius));
                ShowRoundMarker(ghost, !drawn);
                return;
            }
            Grid.Hide();
            Marks.HideRing();
            Marks.ScaleRing(ghost, selected, MarkerFactor(selected, tool, facts, numbers, resized, stroke.PaintRadius));
            ShowRoundMarker(ghost, true);
        }

        // Where Slope was last aimed (the ground point under the aim, before its ghost moved to the area's middle) and the
        // turn it took: what his click works its stroke out from (Strokes.Begin, right after the ghost is placed again).
        private static Vector3 slopeAim;
        private static float slopeTurn;

        /// <summary>
        /// Slope's ghost (B02): it snaps to the ground point under the aim, and moves to its area's middle (with two clicks,
        /// the middle of the strip from the first click to here), turned with it; wards and no-build places are checked
        /// over the whole area; the grid shows every point's height after (points that rise green, those that drop
        /// orange), worked out from the ground by the setting's way, with the area's outline (a square's, a strip's, or the
        /// ring of a round area).
        /// </summary>
        private static void AfterSlope(Player player, GameObject ghost, Piece selected, Tool tool, Facts facts, Numbers numbers,
                                       Player.PlacementStatus status, bool flash, bool grid, string prefab)
        {
            Vector3 at = ghost.transform.position;
            at.x = Mathf.Round(at.x);
            at.z = Mathf.Round(at.z);
            ZoneSystem zones = ZoneSystem.instance;
            if (zones != null)
            {
                at.y = zones.GetGroundHeight(at);
            }
            bool square = facts.Square;
            float turn = Turn.Effective(GameHooks.PlaceRotation(player));
            TurnChoice turning = Settings.Turn.Value;
            slopeAim = at;
            slopeTurn = turn;
            SlopeStroke area = SlopeTool.AreaAt(at, numbers, turn, square, turning, out _, out _);
            var centre = new Vector3(area.CentreX, at.y, area.CentreZ);
            if (zones != null && (centre.x != at.x || centre.z != at.z))
            {
                centre.y = zones.GetGroundHeight(centre);
            }
            ghost.transform.position = centre;
            ghost.transform.rotation = Quaternion.Euler(0f, area.Round ? turn : (float)area.Area.TurnDegrees, 0f);
            bool twoClicks = SlopeTool.Way.Value == SlopeWay.TwoClicks;
            FollowTurnSetting(ghost, square && !twoClicks && turning != TurnChoice.DontTurn);

            if (status == Player.PlacementStatus.Valid)
            {
                if (!area.Round)
                {
                    status = SquareStatus(area.Area, centre, flash);
                }
                else if (Settings.WholeStrokeWards.Value)
                {
                    float reach = area.Radius + RoundStrokeSlack;
                    if (!PrivateArea.CheckAccess(centre, reach, flash, true))
                    {
                        status = Player.PlacementStatus.PrivateZone;
                    }
                    else if (NoBuildNear(centre, reach))
                    {
                        status = Player.PlacementStatus.NoBuildZone;
                    }
                }
            }
            Set(player, status);
            bool valid = status == Player.PlacementStatus.Valid;
            if (grid)
            {
                long key = Key(prefab, area.CentreX, area.CentreZ, SlopeKey(area, twoClicks), area.Round ? area.Radius : (float)area.Area.TurnDegrees,
                               at.y, numbers, valid);
                if (Grid.Wants(key))
                {
                    SlopeStroke work = SlopeTool.Work(player, at, numbers, turn, square, turning, GroundLookup.Reader);
                    if (work != null)
                    {
                        Preview.Slope(work, GroundLookup.Reader, Changes);
                    }
                    else
                    {
                        Changes.Clear();
                    }
                }
                bool drawn = Grid.Show(key, Changes, GridLook.Slope, Color.white, valid);
                ShowSlopeOutline(area, centre, valid);
                ShowRoundMarker(ghost, !drawn);
                return;
            }
            Grid.Hide();
            if (!area.Round)
            {
                Marks.HideRing();
                bool outlined = Marks.ShowSquare(area.Area, centre.y, 0f, valid);
                ShowRoundMarker(ghost, !outlined);
                return;
            }
            Marks.Hide();
            Marks.ScaleRing(ghost, selected, MarkerFactor(selected, tool, facts, numbers, true, numbers.Radius));
            ShowRoundMarker(ghost, true);
        }

        private static void ShowSlopeOutline(SlopeStroke area, Vector3 centre, bool valid)
        {
            if (area.Round)
            {
                Marks.HideSquare();
                Marks.ShowRing(centre, area.Radius, valid);
            }
            else
            {
                Marks.HideRing();
                Marks.ShowSquare(area.Area, centre.y, 0f, valid);
            }
        }

        // What Slope's grid shows beyond the area's middle and turn: its size, the first click, and the settings and keys
        // its plane follows.
        private static int SlopeKey(SlopeStroke area, bool twoClicks)
        {
            unchecked
            {
                int key = area.Round ? 1 : area.Area.Length * 64 + area.Area.Width;
                key = key * 31 + (int)SlopeTool.FlagsNow();
                key = key * 31 + (int)SlopeTool.Way.Value;
                key = key * 31 + (int)SlopeTool.Beyond.Value;
                if (twoClicks)
                {
                    key = key * 31 + (SlopeTool.HasStart ? 1 : 0);
                    key = key * 31 + SlopeTool.StartX;
                    key = key * 31 + SlopeTool.StartZ;
                    key = key * 31 + Mathf.RoundToInt(SlopeTool.StartHeight * 100f);
                    key = key * 31 + (int)Controls.FlattenChoice();
                }
                return key;
            }
        }

        /// <summary>The Slope stroke his click makes now: worked out from the ground, where the ghost was just aimed (his
        /// click places the ghost again first: Strokes.Begin), exactly as the grid shows it.</summary>
        internal static SlopeStroke SlopeNow(Player player, Numbers numbers, bool square) =>
            SlopeTool.Work(player, slopeAim, numbers, slopeTurn, square, Settings.Turn.Value, GroundLookup.Reader);

        /// <summary>Two clicks' first click, where the ghost was just aimed: true when it marked the start.</summary>
        internal static bool SlopeFirstClick(Player player, Numbers numbers, bool square) =>
            SlopeTool.FirstClick(player, slopeAim, numbers, slopeTurn, square, GroundLookup.Reader);

        /// <summary>How the grid shows this tool's stroke.</summary>
        internal static GridLook LookOf(Tool tool, TerrainOp.Settings settings)
        {
            if (tool == null)
            {
                return settings.m_raise && settings.m_raiseDelta > 0f ? GridLook.Raise :
                       settings.m_raise && settings.m_raiseDelta < 0f ? GridLook.Dig : GridLook.Finish;
            }
            if (tool.Digs)
            {
                return GridLook.Dig;
            }
            if (tool.IsPrecisionRaise)
            {
                return GridLook.Raise;
            }
            if (tool.Flattens)
            {
                return GridLook.Flatten;
            }
            if (tool.IsSlope)
            {
                return GridLook.Slope;
            }
            return tool.Square && tool.Kind == SquareKind.Remove ? GridLook.Remove : GridLook.Finish;
        }

        /// <summary>A square stroke's height as Squares applies it, in world metres: the smoothing tools' with the
        /// game's level offset, the rest the ghost's own (Strokes.ApplySquare).</summary>
        internal static float SquareTarget(Tool tool, TerrainOp.Settings settings, float y) =>
            tool.Kind == SquareKind.SmoothAndPaint ? y + settings.m_levelOffset : y;

        /// <summary>The edge the ring shows: the ground a round stroke moves, or, for one that only paints, its paint.</summary>
        internal static float RingRadius(RoundStroke stroke) =>
            stroke.Flatten ? stroke.SmoothRadius : stroke.HeightReach > 0f ? stroke.HeightReach : stroke.PaintRadius;

        /// <summary>The ground's point the ring is drawn round: the one the stroke measures from (its paint's, for a stroke
        /// that only paints, half a metre back as the game lays paint).</summary>
        internal static Vector3 RingCentre(RoundStroke stroke)
        {
            bool paintOnly = !stroke.Flatten && stroke.HeightReach <= 0f;
            float x = paintOnly && stroke.HalfOffset ? stroke.Position.x - 0.5f : stroke.Position.x;
            float z = paintOnly && stroke.HalfOffset ? stroke.Position.z - 0.5f : stroke.Position.z;
            return new Vector3(Preview.Nearest(x), stroke.Position.y, Preview.Nearest(z));
        }

        /// <summary>
        /// The height Flatten levels to now, in world metres: the choice of the first Flatten key held, or "Flatten to"
        /// (Controls.FlattenChoice). Your feet: the ground under him, as the game's Level ground takes it. Where you point:
        /// the ground under the tool's middle. Otherwise, from the heights of the points it covers now (a square's, or those
        /// within the round tool's radius); where none can be read, where it points.
        /// </summary>
        private static float FlattenTarget(Player player, Vector3 at, Footprint? square, float? radius)
        {
            FlattenHeight choice = Controls.FlattenChoice();
            ZoneSystem zones = ZoneSystem.instance;
            float pointed = zones != null ? zones.GetGroundHeight(at) : at.y;
            if (choice == FlattenHeight.Feet)
            {
                return zones != null ? zones.GetGroundHeight(player.transform.position) : at.y;
            }
            if (choice == FlattenHeight.Pointed)
            {
                return pointed;
            }
            Heights.Clear();
            if (square.HasValue)
            {
                foreach ((int x, int z) in square.Value.Points())
                {
                    if (GroundLookup.Reader(x, z, out PointState state))
                    {
                        Heights.Add(state.Height);
                    }
                }
            }
            else if (radius.HasValue)
            {
                int centreX = Preview.Nearest(at.x);
                int centreZ = Preview.Nearest(at.z);
                float r = radius.Value;
                int span = Mathf.CeilToInt(r);
                for (int z = centreZ - span; z <= centreZ + span; z++)
                {
                    for (int x = centreX - span; x <= centreX + span; x++)
                    {
                        float dx = x - centreX;
                        float dz = z - centreZ;
                        if (Mathf.Sqrt(dx * dx + dz * dz) <= r && GroundLookup.Reader(x, z, out PointState state))
                        {
                            Heights.Add(state.Height);
                        }
                    }
                }
            }
            return Preview.HeightOf(choice, Heights) ?? pointed;
        }

        /// <summary>How much to grow the game's round marker (with the grid off): to the ground the stroke really moves,
        /// for a resized tool and for Terraformer's own (whose markers come from the game's other tools), or to its paint
        /// while it only paints (A99, review round 1); the game's own tools at their own size, shaping, keep the game's
        /// marker as it is (MODULES.md rule 4).</summary>
        private static float MarkerFactor(Piece selected, Tool tool, Facts facts, Numbers numbers, bool resized, float paintRadius)
        {
            bool paintOnly = (numbers.Chosen & Chosen.PaintOnly) != 0;
            if (!resized && tool == null && !paintOnly)
            {
                return 1f;
            }
            float marker = Marks.MarkerRadius(selected);
            float shown = ShownRadius(facts, numbers, paintRadius);
            if (marker > 0.01f)
            {
                return shown / marker;
            }
            return facts.Radius > 0f ? (paintOnly ? paintRadius : numbers.Radius) / facts.Radius : 1f;
        }

        /// <summary>The radius a round stroke shows with the grid off: the ground it moves, or, for a stroke that only
        /// paints (A99), its paint (<paramref name="paintRadius"/>, the stroke's own: RoundStroke.Of).</summary>
        internal static float ShownRadius(Facts facts, Numbers numbers, float paintRadius) =>
            (numbers.Chosen & Chosen.PaintOnly) != 0 ? paintRadius : numbers.Radius - facts.Rim;

        // A fingerprint of what the grid shows, so it's remade only when that changes (or a few times a second).
        private static long Key(string prefab, int x, int z, int extra, float size, float height, Numbers numbers, bool valid)
        {
            unchecked
            {
                long key = prefab.GetHashCode();
                key = key * 31 + x;
                key = key * 31 + z;
                key = key * 31 + extra;
                key = key * 31 + Mathf.RoundToInt(size * 100f);
                key = key * 31 + Mathf.RoundToInt(height * 100f);
                key = key * 31 + Mathf.RoundToInt(numbers.Radius * 100f);
                key = key * 31 + Mathf.RoundToInt(numbers.Sharpness * 10f);
                key = key * 31 + Mathf.RoundToInt(numbers.Lift * 1000f);
                key = key * 31 + numbers.Points;
                key = key * 31 + (int)numbers.Chosen;
                key = key * 31 + (valid ? 1 : 0);
                return key;
            }
        }

        /// <summary>Switched off: the ghost's round marker goes back to the tool's own size at once, and the grid goes.</summary>
        internal static void Unscale(Player player)
        {
            Grid.Hide();
            GameObject ghost = GameHooks.PlacementGhost(player);
            Piece selected = player.GetBuildTool()?.GetSelectedPiece();
            if (ghost != null && selected != null && selected.GetComponent<TerrainOp>() != null)
            {
                Marks.ScaleRing(ghost, selected, 1f);
                ShowRoundMarker(ghost, true);
            }
        }

        private static void Set(Player player, Player.PlacementStatus status)
        {
            if (GameHooks.PlacementStatus(player) == status)
            {
                return;
            }
            GameHooks.PlacementStatus(player) = status;
            GameHooks.SetPlacementGhostValid(player, status == Player.PlacementStatus.Valid);
        }

        /// <summary>Whether the game puts this ghost at the height of the ground he stands on (Level ground, Paved road), by
        /// the game's own rule: unless AltPlace is held (a gamepad's too), or, with a gamepad on the newer controls, its
        /// toggle is on.</summary>
        private static bool AtFeet(Player player, Piece piece)
        {
            if (!piece.m_groundPiece || !piece.m_allowAltGroundPlacement)
            {
                return false;
            }
            bool alternative = ZInput.IsNonClassicFunctionality() && ZInput.IsGamepadActive()
                ? player.AlternativePlacementActive
                : ZInput.GetButton("AltPlace") || ZInput.GetButton("JoyAltPlace");
            return !alternative;
        }

        /// <summary>The wheel turns a ghost only when the ghost's own piece says it can turn, so "Square tools turn" reaches
        /// the ghost in hand at once, not only the next one (review round 1).</summary>
        private static void FollowTurnSetting(GameObject ghost, bool turns)
        {
            Piece piece = ghost.GetComponent<Piece>();
            if (piece != null && piece.m_canRotate != turns)
            {
                piece.m_canRotate = turns;
            }
        }

        private static void ShowRoundMarker(GameObject ghost, bool show)
        {
            Transform marker = ghost.transform.Find("_GhostOnly");
            if (marker != null && marker.gameObject.activeSelf != show)
            {
                marker.gameObject.SetActive(show);
            }
        }

        /// <summary>
        /// A square's status beyond the game's own checks at the ghost's centre:
        /// - **With "Check wards over the whole stroke"**, no point of it in a place where building isn't allowed, and none
        ///   inside a ward that bars him (strictly: one ward that lets him in doesn't excuse another that doesn't).
        /// - **Without it,** the game's own checks again at the snapped centre, which can be up to 0.7 m from where the game
        ///   checked.
        /// A refusal flashes the ward's shield, as the game's own does, when asked.
        /// </summary>
        private static Player.PlacementStatus SquareStatus(Footprint square, Vector3 at, bool flash)
        {
            if (!Settings.WholeStrokeWards.Value)
            {
                if (Location.IsInsideNoBuildLocation(at))
                {
                    return Player.PlacementStatus.NoBuildZone;
                }
                return PrivateArea.CheckAccess(at, 0f, flash, false) ? Player.PlacementStatus.Valid : Player.PlacementStatus.PrivateZone;
            }
            float now = Time.unscaledTime;
            bool known = checkedAt >= 0f && now - checkedAt < RecheckAfter && Same(checkedSquare, square);
            if (!known)
            {
                checkedSquare = square;
                checkedAt = now;
                checkedStatus = WholeSquare(square, at.y, out blockedPoint);
            }
            if (flash && checkedStatus == Player.PlacementStatus.PrivateZone)
            {
                PrivateArea.CheckAccess(blockedPoint, 0f, true, true);
            }
            return checkedStatus;
        }

        private static bool Same(Footprint a, Footprint b) =>
            a.CentreX == b.CentreX && a.CentreZ == b.CentreZ && a.Length == b.Length && a.Width == b.Width && a.TurnDegrees == b.TurnDegrees;

        /// <summary>One look over the circle round the whole square first: with no ward that bars him and no place without
        /// building anywhere near, every point is fine, for one call each. Only when one is near is each point asked about.</summary>
        private static Player.PlacementStatus WholeSquare(Footprint square, float height, out Vector3 blocked)
        {
            blocked = default;
            var centre = new Vector3(square.CentreX, height, square.CentreZ);
            float reach = (float)square.Reach + 0.5f;
            bool wardNear = !PrivateArea.CheckAccess(centre, reach, false, true);
            bool noBuildNear = NoBuildNear(centre, reach);
            if (!wardNear && !noBuildNear)
            {
                return Player.PlacementStatus.Valid;
            }
            foreach ((int x, int z) in square.Points())
            {
                var point = new Vector3(x, height, z);
                if (noBuildNear && Location.IsInsideNoBuildLocation(point))
                {
                    return Player.PlacementStatus.NoBuildZone;
                }
                if (wardNear && !PrivateArea.CheckAccess(point, 0f, false, true))
                {
                    blocked = point;
                    return Player.PlacementStatus.PrivateZone;
                }
            }
            return Player.PlacementStatus.Valid;
        }

        /// <summary>Whether a place where building isn't allowed reaches within this distance of the point. The game's own
        /// question (Location.IsInsideNoBuildLocation) is about one point; this asks each place the game has loaded the
        /// same way, with the distance added.</summary>
        private static bool NoBuildNear(Vector3 point, float radius)
        {
            List<Location> all = GameHooks.AllLocations;
            if (all == null)
            {
                return Location.IsInsideNoBuildLocation(point);
            }
            foreach (Location location in all)
            {
                if (location != null && location.m_noBuild && location.IsInside(point, radius, buildCheck: true))
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The turn a square tool takes from the game's own build turn, by "Square tools turn".</summary>
    internal static class Turn
    {
        internal const float GameStep = 22.5f;

        /// <summary>A square tool's turn in degrees, 0 up to 180° (a rectangle looks the same turned by 180°; a square, which
        /// looks the same turned by 90°, takes it modulo 90° itself: Footprint), from the game's turn counted in its 22.5°
        /// steps.</summary>
        internal static float Effective(int steps) => Effective(steps, Settings.Turn.Value);

        internal static float Effective(int steps, TurnChoice choice)
        {
            switch (choice)
            {
                case TurnChoice.DontTurn:
                    return 0f;
                case TurnChoice.TwentyTwoAndAHalfDegrees:
                    return (float)Footprint.Normalise(steps * GameStep, 180.0);
                default:
                    // Two game steps make 45°; an odd count (the game starts each life at a random turn) rounds down.
                    int pairs = steps >= 0 ? steps / 2 : -((-steps + 1) / 2);
                    return (float)Footprint.Normalise(pairs * 2 * GameStep, 180.0);
            }
        }

        /// <summary>The turn a square tool's area shows at the game's turn: a square's 0 up to 90°, a rectangle's to 180°.</summary>
        internal static float Shown(int steps, bool rectangle) => (float)Footprint.Steps(Effective(steps), rectangle);
    }
}
