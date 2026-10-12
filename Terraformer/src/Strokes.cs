using System;
using System.Collections.Generic;
using UnityEngine;

namespace Terraformer
{
    /// <summary>A stroke of his own, while his click places it.</summary>
    internal sealed class Stroke
    {
        /// <summary>The placed piece's prefab name, as the game sends it.</summary>
        internal string Prefab;

        /// <summary>Terraformer's tool, or null for the game's own tools and other mods'.</summary>
        internal Tool Tool;

        internal Numbers Numbers;

        /// <summary>How far from the stroke's middle it can change anything, in metres.</summary>
        internal float Reach;

        /// <summary>The placed piece's own settings: the object the game hands back where the stroke is applied.</summary>
        internal TerrainOp.Settings Settings;

        /// <summary>Whether Terraformer's numbers go with it and its zones are widened: every stroke of Terraformer's tools,
        /// and the game's own tools resized or sharpened. A stroke of the game's own tools at their own size is only
        /// recorded for undo (and claimed, so it's worked out on his game): the game's own stroke otherwise.</summary>
        internal bool Carries;

        /// <summary>Whether its area is a square or rectangle (Slope's "Area" as it was at the click).</summary>
        internal bool Square;

        /// <summary>Whether it was worked out on his game during the click (Slope's two clicks then start again).</summary>
        internal bool Applied;
    }

    /// <summary>
    /// Every step of a stroke Terraformer takes part in (the build plan, "Everything a stroke does"; research C and D):
    /// - **His click** (Player.TryPlacePiece) marks "his own stroke" for exactly its length, with the tool's numbers: its
    ///   remembered radius, sharpness, lift or square size. A stroke of one of the game's tools at its own size isn't
    ///   marked at all, so it's the game's own, untouched.
    /// - **The stroke's own copy** (TerrainOp.Awake) is widened to reach every zone it changes, plus a margin, so every
    ///   zone sharing a changed point receives it: no seams.
    /// - **Each zone's ground record is claimed** for his game before the stroke is sent (TerrainComp.ApplyOperation), so
    ///   the stroke is worked out on his own game at once.
    /// - **The numbers travel with the stroke,** added after the game's own data and marked as Terraformer's
    ///   (TerrainOp.Settings.Serialize), and are read back on whichever game applies it (Deserialize); the game itself
    ///   ignores them. So a friend on the pack who owns the ground applies his stroke as he chose it, and his game applies
    ///   a friend's stroke as the friend chose it, never with his own numbers.
    /// - **A round stroke is applied by the game's own code,** with the stroke's numbers lent to the tool's settings for
    ///   exactly that one application and put back after it (TerrainComp.DoOperation), grass included.
    /// - **A square stroke is applied by Terraformer's own code** (TerrainComp.InternalDoOperation; Squares).
    /// The applying steps work whether or not Terraformer is switched on: they serve strokes that reached his game.
    /// </summary>
    internal static class Strokes
    {
        /// <summary>Marks Terraformer's numbers in a stroke's package: "TFRM".</summary>
        internal const int Magic = 0x4D524654;

        /// <summary>The block's version: 2 since B02 (a rectangle's width, Slope's plane and flags after version 1's fields).
        /// An older reader takes a version-2 block's first fields and ignores the rest; a version-1 block reads as a square
        /// with no plane.</summary>
        internal const byte BlockVersion = 2;

        /// <summary>Version 1's length: the mark, the version, the chosen flags, three floats and an int.</summary>
        internal const int BlockSize = 4 + 1 + 1 + 4 + 4 + 4 + 4;

        /// <summary>What version 2 adds: the width (an int), the plane (five floats) and Slope's flags (a byte).</summary>
        internal const int BlockTail = 4 + 5 * 4 + 1;

        /// <summary>Numbers a stroke can carry that a game will use, whatever a modified copy sends.</summary>
        internal const float LargestTrustedRadius = 64f;

        internal const float LargestTrustedLift = 8f;

        /// <summary>A plane's height and its clamps are kept within this many metres of the world's middle height, its fall
        /// within <see cref="LargestTrustedFall"/> metres a metre; anything else (a NaN, a modified copy's nonsense) and the
        /// stroke carries no plane, and changes nothing.</summary>
        internal const float LargestTrustedHeight = 10000f;

        internal const float LargestTrustedFall = 1000f;

        /// <summary>His own stroke, while his click places it; null otherwise.</summary>
        internal static Stroke Current { get; private set; }

        /// <summary>The numbers read with the stroke being applied now, and the tool settings they came with.</summary>
        internal static Numbers? Arriving { get; private set; }

        internal static TerrainOp.Settings ArrivingFor { get; private set; }

        private static Lent lent;

        private static bool warnedGrid;

        // Grass redrawn again a frame after a square stroke, in case the ground was redrawn after it (research D, 6).
        private static readonly List<(Vector3 Centre, float Radius)> GrassLater = new List<(Vector3, float)>();
        private static readonly List<(Vector3 Centre, float Radius)> GrassNow = new List<(Vector3, float)>();

        // ---- On his own game, while he places a stroke ----

        /// <summary>The numbers a stroke of this tool is made with now: its remembered ones, and always a square's size and
        /// the precision tools' lift, and Lower ground's depth (its own setting); and "paint only" while it's on for this
        /// tool (A99). The ghost's preview asks the same.</summary>
        internal static Numbers NumbersFor(string prefab, Tool tool, Facts facts) =>
            NumbersFor(prefab, tool, facts, PaintOnly.AppliesTo(prefab));

        internal static Numbers NumbersFor(string prefab, Tool tool, Facts facts, bool paintOnly)
        {
            Numbers numbers = Memory.For(prefab, facts);
            if (paintOnly)
            {
                // A stroke that only paints always carries the block, the game's own tools at their own size too, so
                // whichever game applies it leaves its height steps out.
                numbers.Chosen |= Chosen.PaintOnly;
            }
            if (tool != null && tool.Square)
            {
                numbers.Chosen |= Chosen.Points;
                if (tool.IsPrecisionRaise || tool.IsPrecisionLower)
                {
                    numbers.Chosen |= Chosen.Lift;
                }
            }
            else if (tool != null && tool.IsLowerGround)
            {
                numbers.Chosen |= Chosen.Lift;
            }
            if (tool != null && tool.IsSlope)
            {
                // Its edge and its size always travel, so a friend's game applies the very stroke the grid showed.
                numbers.Chosen |= Chosen.Sharpness;
                if (!tool.Square)
                {
                    numbers.Chosen |= Chosen.Radius;
                }
            }
            return numbers;
        }

        private static readonly List<PointChange> Checked = new List<PointChange>();

        /// <summary>
        /// His click: marks the stroke, and starts its record for undo.
        /// - **The ghost is placed first,** as the game's own click is about to place it (Player.TryPlacePiece's first step),
        ///   so what's checked is where the stroke really goes, not where the ghost was a frame before (review round 1).
        /// - **The game's own refusals come first:** a ghost that can't be placed is left to the game, which says why.
        /// - **Refused here,** saying why: the ground there is still loading, for a stroke that carries Terraformer's
        ///   numbers; or a dig would lower nothing ("Ground cannot be lowered further.", his first test).
        /// - **A stroke of the game's own tools at their own size** is recorded for undo only where the ground is loaded;
        ///   elsewhere it's the game's own stroke, untouched, as before 0.2.0.
        /// </summary>
        internal static bool Begin(Player player, Piece piece)
        {
            Current = null;
            if (player == null || piece == null || player != Player.m_localPlayer)
            {
                return true;
            }
            TerrainOp op = piece.GetComponent<TerrainOp>();
            if (op == null)
            {
                return true;
            }
            try
            {
                GameHooks.PlaceGhost(player, false);
            }
            catch (Exception)
            {
                // Another mod's error (or the game's) placing the ghost: the game's own call is about to raise it again,
                // under its own name; Terraformer's own part of it is always guarded (the last look).
                return true;
            }
            if (!Module.IsOn)
            {
                // Switched off by an error while the ghost was placed: the game's own tools are the game's; one of
                // Terraformer's would be placed with nothing to apply it, so it's refused (the last look).
                return Tool.Named(piece.gameObject.name) == null;
            }
            string prefab = piece.gameObject.name;
            Tool tool = Tool.Named(prefab);
            var facts = new Facts(op.m_settings, tool, Settings.DigDepth.Value);
            Numbers numbers = NumbersFor(prefab, tool, facts);
            GameObject ghost = GameHooks.PlacementGhost(player);
            if (ghost == null || !ghost.activeSelf || GameHooks.PlacementStatus(player) != Player.PlacementStatus.Valid)
            {
                return true;   // the game refuses it itself, and says why
            }
            Vector3 at = ghost.transform.position;
            float reach;
            float turn = 0f;
            bool square = tool != null && tool.Square;
            if (square)
            {
                // The turn the ghost took (Ghost.After), exactly, rather than read back from its rotation.
                turn = Turn.Effective(GameHooks.PlaceRotation(player));
                reach = (float)new Footprint(0, 0, numbers.Points, numbers.Across, turn).Reach;
            }
            else
            {
                reach = numbers.Radius;
            }
            bool carries = tool != null || numbers.Chosen != Chosen.None;
            if (ZNetScene.instance != null && !ZNetScene.instance.IsAreaReady(at))
            {
                if (!carries)
                {
                    return true;   // the game's own stroke, untouched and not recorded
                }
                QoLMods.Kit.Say.Center("The ground here is still loading.");
                return false;
            }
            if (tool != null && tool.Digs && !CanDig(tool, op.m_settings, numbers, at, turn))
            {
                QoLMods.Kit.Say.Center("Ground cannot be lowered further.");
                return false;
            }
            if (tool != null && tool.IsSlope && !SlopeClick(player, ref numbers, ref square, ref reach))
            {
                return false;
            }
            Current = new Stroke
            {
                Prefab = prefab,
                Tool = tool,
                Numbers = numbers,
                Reach = reach,
                Settings = op.m_settings,
                Carries = carries,
                Square = square,
            };
            string name = Localization.instance != null ? Localization.instance.Localize(piece.m_name) : piece.m_name;
            Undo.Start(name, square ? reach : Mathf.Max(reach, op.m_settings.GetRadius()), piece);
            return true;
        }

        /// <summary>
        /// Slope at his click (B02): with two clicks, the first marks where the slope starts and refuses the stroke, at no
        /// cost (as "Ground cannot be lowered further."); otherwise its stroke is worked out from the ground, exactly as the
        /// grid shows it, and its plane, area and flags go into its numbers. False when there's no stroke to make.
        /// </summary>
        private static bool SlopeClick(Player player, ref Numbers numbers, ref bool square, ref float reach)
        {
            if (Ghost.SlopeFirstClick(player, numbers, square))
            {
                return false;
            }
            SlopeStroke slope = Ghost.SlopeNow(player, numbers, square);
            if (slope == null || !slope.HasPlane)
            {
                QoLMods.Kit.Say.Center("The ground here is still loading.");
                return false;
            }
            // A click that would change nothing (only raise on ground above the slope, or ground already on it with its
            // paint kept) is refused before the game charges stamina, wear or stone, as the grid shows it (review round 1).
            Preview.Slope(slope, GroundLookup.Reader, Checked);
            if (Checked.Count > 0 && !SlopeTool.ChangesAnything(Checked, slope.Flags))
            {
                QoLMods.Kit.Say.Center("Nothing here to slope.");
                return false;
            }
            numbers.Plane = slope.Plane;
            numbers.Slope = slope.Flags;
            numbers.Chosen |= Chosen.Plane;
            square = !slope.Round;
            if (square)
            {
                // A strip's own size (two clicks), or the tool's square or rectangle.
                numbers.Points = slope.Area.Length;
                numbers.Width = slope.Area.Width;
                numbers.Chosen |= Chosen.Points;
                numbers.Chosen = slope.Area.Width != slope.Area.Length ? numbers.Chosen | Chosen.Across : numbers.Chosen & ~Chosen.Across;
            }
            reach = slope.Reach;
            return true;
        }

        /// <summary>Whether a dig here would lower any ground, on his game's copy of it: false where everything it reaches
        /// is as deep as the game allows (8 m below the world's own height) or already below the dig. Ground that can't be
        /// read counts as diggable.</summary>
        private static bool CanDig(Tool tool, TerrainOp.Settings settings, Numbers numbers, Vector3 at, float turn)
        {
            if (tool.Square)
            {
                var square = new Footprint(Mathf.RoundToInt(at.x), Mathf.RoundToInt(at.z), numbers.Points, numbers.Across, turn);
                Preview.Square(square, KindOf(tool, numbers), at.y, numbers.Lift, GroundLookup.Reader, Checked);
            }
            else
            {
                Preview.Round(RoundStroke.Of(settings, numbers, at), GroundLookup.Reader, Checked);
            }
            return Checked.Count == 0 || Preview.AnyMoves(Checked);
        }

        /// <summary>His click is over: its record for undo is kept if it changed anything (inside the module's guard, since
        /// this runs from the click's finalizer: review round 1), and the stroke is no longer his own.</summary>
        internal static void End()
        {
            Stroke stroke = Current;
            if (stroke != null)
            {
                Module.Guard("keeping a stroke for undo", Undo.Finish);
                if (stroke.Applied && stroke.Tool != null && stroke.Tool.IsSlope)
                {
                    // Two clicks made their slope: the next starts again from a first click.
                    SlopeTool.Forget(null, false);
                }
            }
            Current = null;
        }

        /// <summary>Just before his own stroke is worked out in a zone (on his game): the zones round it noted for undo, and
        /// the stroke marked as applied.</summary>
        internal static void Working(TerrainComp compiler, Vector3 position, Vector3 facing)
        {
            Stroke stroke = Current;
            if (stroke == null)
            {
                return;
            }
            stroke.Applied = true;
            Undo.Before(compiler, position, ReachOf(facing));
        }

        /// <summary>The placed stroke's own copy, before it picks its zones: widened to reach every zone it changes. The
        /// copy's settings choose only the zones (the applying game reads the tool's own), so nothing else changes.</summary>
        internal static void Widen(TerrainOp op)
        {
            Stroke stroke = Current;
            if (stroke == null || !stroke.Carries || TerrainOp.m_forceDisableTerrainOps || op == null || !Mine(stroke, op.gameObject))
            {
                return;
            }
            bool square = stroke.Square;
            float reach = stroke.Reach;
            if (square)
            {
                // The square is applied at the turn this copy's facing sends, which another mod could have changed after
                // Terraformer placed the ghost: its reach at that turn too (review round 2).
                reach = Mathf.Max(reach, SquareReach(stroke.Numbers.Points, stroke.Numbers.Across, op.transform.forward));
            }
            float widened = WidenedReach(square, reach);
            TerrainOp.Settings copy = op.m_settings;
            copy.m_paintCleared = true;
            copy.m_paintRadius = Mathf.Max(copy.m_paintRadius, widened);
        }

        /// <summary>How far a stroke's zone search must reach so every zone it changes receives it. A round stroke's
        /// points lie within its radius of the nearest point to its middle (up to half a metre off on each axis), and its
        /// paint box reaches its whole ceiling in pixels: the ceiling plus a metre covers every case (research D, section
        /// 6). A square's points are exact: its corner, plus a metre.</summary>
        internal static float WidenedReach(bool square, float reach) => square ? reach + 1f : Mathf.Ceil(reach) + 1f;

        /// <summary>A square's turn as the game that applies it reads it, from the facing its stroke sends.</summary>
        internal static float TurnOf(Vector3 facing) => facing.sqrMagnitude > 1e-6f ? Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg : 0f;

        /// <summary>The reach of a square or rectangle of this size sent with this facing: its corner's distance from its
        /// middle.</summary>
        internal static float SquareReach(int points, int across, Vector3 facing) => (float)new Footprint(0, 0, points, across, TurnOf(facing)).Reach;

        /// <summary>Before his stroke is sent to a zone: claims the zone's ground record for his game.</summary>
        internal static void Claim(TerrainComp compiler, TerrainOp op)
        {
            Stroke stroke = Current;
            if (stroke == null || compiler == null || op == null || !Mine(stroke, op.gameObject))
            {
                return;
            }
            ZNetView view = compiler.GetComponent<ZNetView>();
            if (view != null && view.IsValid() && !view.IsOwner())
            {
                view.ClaimOwnership();
            }
        }

        /// <summary>As his stroke is packed: Terraformer's numbers after the game's own data.</summary>
        internal static void Write(ZPackage package, GameObject prefab)
        {
            Stroke stroke = Current;
            if (stroke == null || !stroke.Carries || package == null || prefab == null || !Mine(stroke, prefab))
            {
                return;
            }
            WriteBlock(package, stroke.Numbers);
        }

        internal static void WriteBlock(ZPackage package, Numbers numbers)
        {
            package.Write(Magic);
            package.Write(BlockVersion);
            package.Write((byte)numbers.Chosen);
            package.Write(numbers.Radius);
            package.Write(numbers.Sharpness);
            package.Write(numbers.Lift);
            package.Write(numbers.Points);
            // Version 2 (B02).
            package.Write(numbers.Width);
            package.Write(numbers.Plane.H0);
            package.Write(numbers.Plane.Gx);
            package.Write(numbers.Plane.Gz);
            package.Write(numbers.Plane.Lo);
            package.Write(numbers.Plane.Hi);
            package.Write((byte)numbers.Slope);
        }

        private static bool Mine(Stroke stroke, GameObject gameObject) =>
            gameObject != null && Utils.GetPrefabName(gameObject.name) == stroke.Prefab;

        // ---- On the game that applies a stroke ----

        /// <summary>As a stroke is unpacked: Terraformer's numbers, if it carries any. Anything else is left unread.</summary>
        internal static void Read(ZPackage package, TerrainOp.Settings settings)
        {
            Arriving = null;
            ArrivingFor = null;
            if (package == null || settings == null)
            {
                return;
            }
            int start = package.GetPos();
            if (package.Size() - start < BlockSize)
            {
                return;
            }
            if (package.ReadInt() != Magic)
            {
                package.SetPos(start);
                return;
            }
            byte version = package.ReadByte();
            if (version < 1)
            {
                package.SetPos(start);
                return;
            }
            var chosen = (Chosen)package.ReadByte();
            float radius = package.ReadSingle();
            float sharpness = package.ReadSingle();
            float lift = package.ReadSingle();
            int points = package.ReadInt();
            if (version >= 2 && package.Size() - package.GetPos() >= BlockTail)
            {
                int width = package.ReadInt();
                var plane = new SlopePlane
                {
                    H0 = package.ReadSingle(),
                    Gx = package.ReadSingle(),
                    Gz = package.ReadSingle(),
                    Lo = package.ReadSingle(),
                    Hi = package.ReadSingle(),
                };
                var flags = (SlopeFlags)package.ReadByte();
                Arriving = Trusted(chosen, radius, sharpness, lift, points, width, plane, flags);
            }
            else
            {
                // Version 1 (or a block cut short): a square, no plane.
                Arriving = Trusted(chosen & ~(Chosen.Across | Chosen.Plane), radius, sharpness, lift, points);
            }
            ArrivingFor = settings;
        }

        /// <summary>The numbers a game will use from a version-1 stroke, kept within sane bounds whatever was sent.</summary>
        internal static Numbers Trusted(Chosen chosen, float radius, float sharpness, float lift, int points) =>
            Trusted(chosen & ~(Chosen.Across | Chosen.Plane), radius, sharpness, lift, points, 0, default, SlopeFlags.None);

        /// <summary>The numbers a game will use from a stroke, kept within sane bounds whatever was sent: a width within the
        /// square sizes, and a plane only with every number in it a sane number (its clamps may be ±infinity, for none; a
        /// low clamp above the high one is turned round), else no plane at all.</summary>
        internal static Numbers Trusted(Chosen chosen, float radius, float sharpness, float lift, int points, int width, SlopePlane plane, SlopeFlags flags)
        {
            var numbers = new Numbers();
            if ((chosen & Chosen.Radius) != 0 && IsNumber(radius))
            {
                numbers.Radius = Mathf.Clamp(radius, Settings.SmallestRadius, LargestTrustedRadius);
                numbers.Chosen |= Chosen.Radius;
            }
            if ((chosen & Chosen.Sharpness) != 0 && IsNumber(sharpness))
            {
                numbers.Sharpness = Mathf.Clamp(sharpness, 0f, 100f);
                numbers.Chosen |= Chosen.Sharpness;
            }
            if ((chosen & Chosen.Lift) != 0 && IsNumber(lift))
            {
                numbers.Lift = Mathf.Clamp(lift, -LargestTrustedLift, LargestTrustedLift);
                numbers.Chosen |= Chosen.Lift;
            }
            if ((chosen & Chosen.Points) != 0)
            {
                numbers.Points = Footprint.OddWithin(points);
                numbers.Chosen |= Chosen.Points;
            }
            if ((chosen & Chosen.PaintOnly) != 0)
            {
                numbers.Chosen |= Chosen.PaintOnly;
            }
            numbers.Width = numbers.Points;
            if ((chosen & Chosen.Across) != 0 && (chosen & Chosen.Points) != 0)
            {
                numbers.Width = Footprint.OddWithin(width);
                numbers.Chosen |= Chosen.Across;
            }
            if ((chosen & Chosen.Plane) != 0 && TrustedPlane(plane, out SlopePlane kept))
            {
                numbers.Plane = kept;
                // Only lower and only raise together would move nothing: taken as both ways.
                SlopeFlags known = flags & (SlopeFlags.Dirt | SlopeFlags.OnlyLower | SlopeFlags.OnlyRaise | SlopeFlags.Round);
                if ((known & (SlopeFlags.OnlyLower | SlopeFlags.OnlyRaise)) == (SlopeFlags.OnlyLower | SlopeFlags.OnlyRaise))
                {
                    known &= ~(SlopeFlags.OnlyLower | SlopeFlags.OnlyRaise);
                }
                numbers.Slope = known;
                numbers.Chosen |= Chosen.Plane;
            }
            return numbers;
        }

        private static bool TrustedPlane(SlopePlane plane, out SlopePlane kept)
        {
            kept = plane;
            if (!IsNumber(plane.H0) || !IsNumber(plane.Gx) || !IsNumber(plane.Gz) || float.IsNaN(plane.Lo) || float.IsNaN(plane.Hi) ||
                Math.Abs(plane.H0) > LargestTrustedHeight || Math.Abs(plane.Gx) > LargestTrustedFall || Math.Abs(plane.Gz) > LargestTrustedFall)
            {
                return false;
            }
            // No clamp travels as infinity; any other is held to sane heights.
            kept.Lo = float.IsNegativeInfinity(plane.Lo) ? plane.Lo : Mathf.Clamp(plane.Lo, -LargestTrustedHeight, LargestTrustedHeight);
            kept.Hi = float.IsPositiveInfinity(plane.Hi) ? plane.Hi : Mathf.Clamp(plane.Hi, -LargestTrustedHeight, LargestTrustedHeight);
            if (kept.Lo > kept.Hi)
            {
                (kept.Lo, kept.Hi) = (kept.Hi, kept.Lo);
            }
            return true;
        }

        private static bool IsNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>After the game has applied (or dropped) a stroke: its numbers are forgotten, so they can never reach
        /// another one.</summary>
        internal static void Done()
        {
            Arriving = null;
            ArrivingFor = null;
        }

        private sealed class Lent
        {
            internal TerrainOp.Settings Settings;
            internal bool Level;
            internal bool Raise;
            internal bool Smooth;
            internal float LevelRadius;
            internal float RaiseRadius;
            internal float SmoothRadius;
            internal float PaintRadius;
            internal float RaisePower;
            internal float SmoothPower;
            internal float RaiseDelta;
        }

        /// <summary>Before a round stroke that carries Terraformer's numbers is applied: they're lent to its tool's
        /// settings, which the game hands every stroke of that tool. Put back by <see cref="Restore"/>.</summary>
        internal static void Lend(TerrainOp.Settings settings)
        {
            Restore();
            if (settings == null || Arriving == null || !ReferenceEquals(ArrivingFor, settings))
            {
                return;
            }
            Tool tool = Tool.WithSettings(settings);
            if (tool != null && tool.OwnWork)
            {
                return;   // squares and Flatten are Terraformer's own work (ApplyOwn)
            }
            lent = new Lent
            {
                Settings = settings,
                Level = settings.m_level,
                Raise = settings.m_raise,
                Smooth = settings.m_smooth,
                LevelRadius = settings.m_levelRadius,
                RaiseRadius = settings.m_raiseRadius,
                SmoothRadius = settings.m_smoothRadius,
                PaintRadius = settings.m_paintRadius,
                RaisePower = settings.m_raisePower,
                SmoothPower = settings.m_smoothPower,
                RaiseDelta = settings.m_raiseDelta,
            };
            // The same values the preview showed (RoundStroke.Of): every radius moved by the same metres, so the paint's rim
            // stays as wide as the game's; the sharpness as the game's strengths; the lift; and for a stroke that only
            // paints (A99), its height steps switched off, so the game's own save then treats it as paint (and Terraformer
            // watches that save: PaintBefore).
            RoundStroke stroke = RoundStroke.Of(settings, Arriving.Value, Vector3.zero);
            settings.m_level = stroke.Level;
            settings.m_raise = stroke.Raise;
            settings.m_smooth = stroke.Smooth;
            settings.m_levelRadius = stroke.LevelRadius;
            settings.m_raiseRadius = stroke.RaiseRadius;
            settings.m_smoothRadius = stroke.SmoothRadius;
            settings.m_paintRadius = stroke.PaintRadius;
            settings.m_raisePower = stroke.RaisePower;
            settings.m_smoothPower = stroke.SmoothPower;
            settings.m_raiseDelta = stroke.RaiseDelta;
        }

        /// <summary>After the application: the tool's own settings are put back, whatever happened in between.</summary>
        internal static void Restore()
        {
            Lent was = lent;
            lent = null;
            if (was == null)
            {
                return;
            }
            was.Settings.m_level = was.Level;
            was.Settings.m_raise = was.Raise;
            was.Settings.m_smooth = was.Smooth;
            was.Settings.m_levelRadius = was.LevelRadius;
            was.Settings.m_raiseRadius = was.RaiseRadius;
            was.Settings.m_smoothRadius = was.SmoothRadius;
            was.Settings.m_paintRadius = was.PaintRadius;
            was.Settings.m_raisePower = was.RaisePower;
            was.Settings.m_smoothPower = was.SmoothPower;
            was.Settings.m_raiseDelta = was.RaiseDelta;
        }

        /// <summary>Whether a stroke with these settings is Terraformer's own work alone (every square tool, and Flatten),
        /// never the game's steps (which would apply the copy's round settings, or smooth instead of levelling).</summary>
        internal static bool IsOwnWork(TerrainOp.Settings settings)
        {
            Tool tool = Tool.WithSettings(settings);
            return tool != null && tool.OwnWork;
        }

        /// <summary>How far his stroke can change ground, as it's applied: a square's at the turn its facing sends, which
        /// another mod could have changed after Terraformer placed the ghost (as <see cref="Widen"/> takes it).</summary>
        internal static float ReachOf(Vector3 facing)
        {
            Stroke stroke = Current;
            if (stroke == null)
            {
                return 0f;
            }
            return stroke.Square ? Mathf.Max(stroke.Reach, SquareReach(stroke.Numbers.Points, stroke.Numbers.Across, facing)) : stroke.Reach;
        }

        /// <summary>His own stroke's settings, while his click places it: the one being recorded for undo.</summary>
        internal static bool IsCurrent(TerrainOp.Settings settings) =>
            Current != null && settings != null && ReferenceEquals(Current.Settings, settings);

        /// <summary>A stroke of Terraformer's own work, in place of the game's steps (which are then skipped, whatever
        /// happens here): a square, or Flatten.</summary>
        internal static void ApplyOwn(TerrainComp compiler, Vector3 position, Vector3 facing, TerrainOp.Settings settings)
        {
            Tool tool = Tool.WithSettings(settings);
            if (tool == null || compiler == null)
            {
                return;
            }
            if (tool.IsSlope)
            {
                ApplySlope(compiler, position, facing, settings);
            }
            else if (tool.Square)
            {
                ApplySquare(compiler, position, facing, settings);
            }
            else if (tool.Id == Tool.FlattenId)
            {
                ApplyFlatten(compiler, position, settings);
            }
        }

        /// <summary>Round Flatten in one zone: every point within its radius toward the stroke's height, by its curve
        /// (Squares.Flatten), with the same numbers the preview used (RoundStroke.Of).</summary>
        internal static void ApplyFlatten(TerrainComp compiler, Vector3 position, TerrainOp.Settings settings)
        {
            Numbers numbers = Arriving != null && ReferenceEquals(ArrivingFor, settings) ? Arriving.Value : default;
            RoundStroke stroke = RoundStroke.Of(settings, numbers, position, flatten: true);
            Ground ground = GroundOf(compiler);
            if (ground == null)
            {
                WarnGrid();
                return;
            }
            float zoneHeight = compiler.transform.position.y;
            bool addsSnow = settings.m_paintType == TerrainModifier.PaintType.Cultivate && WorldGenerator.IsDeepnorth(position.x, position.z);
            Squares.Flatten(ground, Preview.Nearest(position.x), Preview.Nearest(position.z), stroke.SmoothRadius, stroke.SmoothPower,
                            position.y - zoneHeight, stroke.PaintRadius, Squares.ColourOf(settings.m_paintType), addsSnow);
            Redrawn(compiler, position, Mathf.Max(stroke.SmoothRadius, stroke.PaintRadius) + 1f);
        }

        private static void WarnGrid()
        {
            if (!warnedGrid)
            {
                warnedGrid = true;
                Module.Log.LogWarning("A terrain stroke of Terraformer's own reached ground that isn't laid out on the game's usual " +
                                      "one-metre grid, so it changed nothing there.");
            }
        }

        // As the game ends its own steps (InternalDoOperation): the stroke counted where the zone's record is kept, and the
        // grass redrawn over it now and again a frame later.
        private static void Redrawn(TerrainComp compiler, Vector3 position, float grass)
        {
            GameHooks.Operations(compiler)++;
            GameHooks.LastOpPoint(compiler) = position;
            GameHooks.LastOpRadius(compiler) = grass;
            if (ClutterSystem.instance != null)
            {
                ClutterSystem.instance.ResetGrass(position, grass);
                GrassLater.Add((position, grass));
            }
        }

        /// <summary>Whether the game saves a stroke with these settings as paint only (TerrainComp.DoOperation's own rule).
        /// Such a save is skipped while the paint's checksum hasn't moved, and that checksum is a plain sum that can't see
        /// dirt turned to paved or cultivated (each adds up to 1).</summary>
        internal static bool SavesPaintOnly(TerrainOp.Settings settings) =>
            settings.m_paintCleared && !settings.m_level && !settings.m_raise && !settings.m_smooth;

        /// <summary>
        /// Whether Terraformer makes sure a stroke with these settings is saved when it repaints (reviews 1 and 2): every
        /// paint-only stroke of its own tools, always (a stroke on its way must apply, as it was made); and the game's own
        /// paint-only strokes (Pathen, a friend's included, where his game looks after the ground) while it's switched on,
        /// since the pure colours its squares leave make the game's checksum blind to them too.
        /// </summary>
        internal static bool WatchesSave(TerrainOp.Settings settings, bool ownTool, bool switchedOn, bool snowOnly) =>
            settings != null && SavesPaintOnly(settings) && (ownTool || (switchedOn && !snowOnly));

        /// <summary>The game's own "changing snow" stroke, as its paint step decides it: Cultivate paint in the Deep North
        /// (the snow trails of everyone walking there, fires melting it). It changes only the snow channel, which the
        /// game's checksum does see, and it comes many times a second while anyone walks in deep snow, so the game's own
        /// isn't watched (the last look).</summary>
        internal static bool ChangesSnowOnly(TerrainOp.Settings settings, Vector3 position)
        {
            if (settings == null || settings.m_paintType != TerrainModifier.PaintType.Cultivate)
            {
                return false;
            }
            float x = position.x;
            float z = position.z;
            if (settings.m_halfOffset)
            {
                x -= 0.5f;
                z -= 0.5f;
            }
            return WorldGenerator.IsDeepnorth(x, z);
        }

        /// <summary>Before a stroke is worked out in a zone: the zone's paint as a fingerprint when Terraformer watches this
        /// stroke's save (<see cref="WatchesSave"/>), or 0 when it doesn't.</summary>
        internal static ulong PaintBefore(TerrainComp compiler, TerrainOp.Settings settings, Vector3 position)
        {
            if (compiler == null || settings == null)
            {
                return 0;
            }
            // A stroke of Terraformer's own tools, or one made paint-only (the game's own Paved road and Cultivate, lent
            // their flags off for it: A99), is watched whether Terraformer is on or off, as a stroke on its way must apply.
            bool own = Tool.WithSettings(settings) != null || ArrivesPaintOnly(settings);
            bool on = Module.IsOn;
            bool snowOnly = !own && on && SavesPaintOnly(settings) && ChangesSnowOnly(settings, position);
            if (!WatchesSave(settings, own, on, snowOnly))
            {
                return 0;
            }
            return PaintFingerprint(GameHooks.PaintMask(compiler), GameHooks.ModifiedPaint(compiler));
        }

        /// <summary>Whether the stroke being applied now with these settings only paints (A99).</summary>
        internal static bool ArrivesPaintOnly(TerrainOp.Settings settings) =>
            Arriving != null && ReferenceEquals(ArrivingFor, settings) && (Arriving.Value.Chosen & Chosen.PaintOnly) != 0;

        /// <summary>After the stroke was worked out, before the game saves the zone: if the paint changed, the save goes
        /// ahead even when the game's own checksum can't tell (a plain sum, the same for dirt, paved and cultivated): a
        /// last checksum that can't match makes the game's own save write. A zone the stroke didn't change isn't saved
        /// again.</summary>
        internal static void SaveIfRepainted(TerrainComp compiler, ulong before)
        {
            if (compiler == null || before == 0)
            {
                return;
            }
            if (PaintFingerprint(GameHooks.PaintMask(compiler), GameHooks.ModifiedPaint(compiler)) != before)
            {
                GameHooks.LastHash(compiler) = ~GameHooks.PaintMaskHash(compiler);
            }
        }

        /// <summary>A fingerprint of a zone's saved paint: which points are painted, and each painted point's colour to the
        /// bit (FNV-1a). Never 0, which means "not watched".</summary>
        internal static ulong PaintFingerprint(Color[] paint, bool[] painted)
        {
            ulong hash = 14695981039346656037UL;
            if (paint == null || painted == null)
            {
                return 1;
            }
            int count = Math.Min(paint.Length, painted.Length);
            for (int i = 0; i < count; i++)
            {
                if (!painted[i])
                {
                    continue;
                }
                Color colour = paint[i];
                hash = Mix(hash, (uint)i);
                hash = Mix(hash, Bits(colour.r));
                hash = Mix(hash, Bits(colour.g));
                hash = Mix(hash, Bits(colour.b));
                hash = Mix(hash, Bits(colour.a));
            }
            return hash == 0 ? 1 : hash;
        }

        private static ulong Mix(ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (value >> shift) & 0xFF;
                hash *= 1099511628211UL;
            }
            return hash;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        private struct FloatBits
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            internal float Float;

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal uint Bits;
        }

        private static uint Bits(float value) => new FloatBits { Float = value }.Bits;

        /// <summary>A square stroke, applied by Terraformer in place of the game's steps (which are then skipped, whatever
        /// happens here).</summary>
        internal static void ApplySquare(TerrainComp compiler, Vector3 position, Vector3 facing, TerrainOp.Settings settings)
        {
            Tool tool = Tool.WithSettings(settings);
            if (tool == null || !tool.Square || compiler == null)
            {
                return;
            }
            Numbers numbers = Arriving != null && ReferenceEquals(ArrivingFor, settings) ? Arriving.Value : default;
            int points = (numbers.Chosen & Chosen.Points) != 0 ? numbers.Points : 3;
            int across = (numbers.Chosen & Chosen.Points) != 0 ? numbers.Across : points;
            float lift = (numbers.Chosen & Chosen.Lift) != 0 ? numbers.Lift : settings.m_raiseDelta;
            // Footprint takes the nearest of the game's 22.5° steps: the facing brings the turn back a hair off.
            var square = new Footprint(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z), points, across, TurnOf(facing));

            Ground ground = GroundOf(compiler);
            if (ground == null)
            {
                WarnGrid();
                return;
            }
            float zoneHeight = compiler.transform.position.y;
            // The same height the preview used (Ghost.SquareTarget), in the zone's own terms.
            float target = Ghost.SquareTarget(tool, settings, position.y) - zoneHeight;
            bool addsSnow = settings.m_paintType == TerrainModifier.PaintType.Cultivate && WorldGenerator.IsDeepnorth(position.x, position.z);
            Squares.Apply(ground, square, KindOf(tool, numbers), target, lift, Squares.ColourOf(settings.m_paintType), addsSnow);
            Redrawn(compiler, position, (float)square.Reach + 1f);
        }

        /// <summary>
        /// A Slope stroke in one zone (B02), in place of the game's steps: its area rebuilt from the stroke alone (its
        /// position, its facing, the size and flags it carries) and every point of it in this zone set onto the plane the
        /// stroke carries, never one worked out here, so the result is the preview his game showed. A Slope stroke without
        /// a plane it can trust changes nothing.
        /// </summary>
        internal static void ApplySlope(TerrainComp compiler, Vector3 position, Vector3 facing, TerrainOp.Settings settings)
        {
            Numbers numbers = Arriving != null && ReferenceEquals(ArrivingFor, settings) ? Arriving.Value : default;
            SlopeStroke stroke = SlopeOf(position, facing, numbers, settings);
            if (stroke == null)
            {
                WarnPlane();
                return;
            }
            Ground ground = GroundOf(compiler);
            if (ground == null)
            {
                WarnGrid();
                return;
            }
            stroke.ApplyTo(ground, compiler.transform.position.y, Squares.ColourOf(settings.m_paintType));
            Redrawn(compiler, position, stroke.Reach + 1f);
        }

        /// <summary>A Slope stroke as the game that applies it rebuilds it from what the stroke carries; null without a
        /// plane.</summary>
        internal static SlopeStroke SlopeOf(Vector3 position, Vector3 facing, Numbers numbers, TerrainOp.Settings settings)
        {
            if ((numbers.Chosen & Chosen.Plane) == 0)
            {
                return null;
            }
            float sharpness = (numbers.Chosen & Chosen.Sharpness) != 0 ? numbers.Sharpness : Facts.SharpnessOfSmooth(settings.m_smoothPower);
            SlopeStroke stroke;
            if ((numbers.Slope & SlopeFlags.Round) != 0)
            {
                float radius = (numbers.Chosen & Chosen.Radius) != 0 ? numbers.Radius : settings.m_smoothRadius;
                stroke = SlopeStroke.Circle(Preview.Nearest(position.x), Preview.Nearest(position.z), radius, sharpness, numbers.Slope);
            }
            else
            {
                int points = (numbers.Chosen & Chosen.Points) != 0 ? numbers.Points : Facts.StartPoints;
                int across = (numbers.Chosen & Chosen.Points) != 0 ? numbers.Across : points;
                var area = new Footprint(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z), points, across, TurnOf(facing));
                stroke = SlopeStroke.Square(area, sharpness, numbers.Slope);
            }
            stroke.Plane = numbers.Plane;
            stroke.HasPlane = true;
            return stroke;
        }

        private static bool warnedPlane;

        private static void WarnPlane()
        {
            if (!warnedPlane)
            {
                warnedPlane = true;
                Module.Log.LogWarning("A Slope stroke arrived without a slope it could use, so it changed nothing.");
            }
        }

        /// <summary>What a square tool's stroke does to its points: the tool's own kind, or only its paint for a stroke that
        /// only paints (A99; Remove terrain changes, which has no paint of its own, stays as it is).</summary>
        internal static SquareKind KindOf(Tool tool, Numbers numbers) =>
            (numbers.Chosen & Chosen.PaintOnly) != 0 && tool.Kind != SquareKind.Remove ? SquareKind.Paint : tool.Kind;

        /// <summary>A zone's ground as Squares edits it: the compiler's own arrays, and its point heights as last drawn.</summary>
        private static Ground GroundOf(TerrainComp compiler)
        {
            Heightmap heightmap = GameHooks.HeightmapOf(compiler);
            int width = GameHooks.Width(compiler);
            if (heightmap == null || width <= 0 || Mathf.Abs(heightmap.m_scale - 1f) > 1e-4f)
            {
                return null;   // squares work on the game's 1 m grid only
            }
            Vector3 centre = compiler.transform.position;
            return new Ground
            {
                Width = width,
                OriginX = Mathf.RoundToInt(centre.x) - width / 2,
                OriginZ = Mathf.RoundToInt(centre.z) - width / 2,
                LevelDelta = GameHooks.LevelDelta(compiler),
                SmoothDelta = GameHooks.SmoothDelta(compiler),
                ModifiedHeight = GameHooks.ModifiedHeight(compiler),
                PaintMask = GameHooks.PaintMask(compiler),
                ModifiedPaint = GameHooks.ModifiedPaint(compiler),
                HeightAt = (x, z) => heightmap.GetHeight(x, z),
            };
        }

        /// <summary>Every frame: the second grass redraw after a square stroke.</summary>
        internal static void Tick()
        {
            if (GrassNow.Count > 0 && ClutterSystem.instance != null)
            {
                foreach ((Vector3 centre, float radius) in GrassNow)
                {
                    ClutterSystem.instance.ResetGrass(centre, radius);
                }
            }
            GrassNow.Clear();
            GrassNow.AddRange(GrassLater);
            GrassLater.Clear();
        }

        /// <summary>After an error: nothing of a stroke is left behind, and any lent settings are put back.</summary>
        internal static void Forget()
        {
            Restore();
            Current = null;
            Undo.Clear();
            Done();
            GrassLater.Clear();
            GrassNow.Clear();
        }
    }
}
