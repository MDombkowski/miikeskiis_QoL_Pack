using System;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// The keys and the wheel, read right after the game's own placement input each frame (a postfix on
    /// Player.UpdatePlacement), so they follow exactly what the game did with the wheel in that frame:
    /// - **The radius key and the wheel** resize a round tool, a notch to the next whole step, or a square tool by a row of
    ///   points on each side; the new size shows with its width in metres (his first test).
    /// - **The sharpness key and the wheel** sharpen a round tool that changes height, or set Raise ground (precision)'s
    ///   lift and Lower ground (precision)'s depth.
    /// - While either is held, the game's own turn of the ghost is undone for that frame, and the camera doesn't zoom
    ///   (<see cref="Adjusting"/>, read by postfixes on Player.CanRotatePiece and GameCamera.UpdateCamera).
    /// - **The wheel alone** turns a square tool by the game's own build turn; with 45° steps, a notch that turned it by
    ///   the game's 22.5° turns it once more.
    /// - **The reset key** puts the tool back to its own values; **the straighten key** lines a square tool up with the
    ///   world; **the undo key** takes back his last stroke (Undo).
    /// - **Flatten's keys** pick the height it levels to while held; a change is said.
    /// Every change is remembered per tool (Memory) and said on screen ("Messages").
    /// </summary>
    internal static class Controls
    {
        // The game steps its build turn when the wheel adds up past this; a notch here is the same.
        private const float NotchThreshold = 0.1f;

        private static float wheelSum;
        private static bool wasBuilding;
        private static FlattenHeight? saidFlatten;

        /// <summary>After the game's own placement input, this frame.</summary>
        internal static void Frame(Player player, bool takeInput, int turnBefore)
        {
            if (!Module.IsOn || player != Player.m_localPlayer)
            {
                return;
            }
            Piece piece = player.GetBuildTool()?.GetSelectedPiece();
            TerrainOp op = piece != null ? piece.GetComponent<TerrainOp>() : null;
            if (op == null || !takeInput || Hud.IsPieceSelectionVisible())
            {
                wheelSum = 0f;
                saidFlatten = null;
                return;
            }
            string prefab = piece.gameObject.name;
            Tool tool = Tool.Named(prefab);
            var facts = new Facts(op.m_settings, tool, Settings.DigDepth.Value);
            float now = Time.unscaledTime;

            if (Settings.Pressed(Settings.ResetKey))
            {
                Memory.Reset(prefab, now);
                Say(player, $"{Localization.instance.Localize(piece.m_name)}: back to its own");
                if (tool != null && tool.IsSlope)
                {
                    SlopeTool.Forget(player, true);
                }
            }
            if (Settings.Pressed(Settings.UndoKey))
            {
                Say(player, Undo.UndoLast(player), always: true);
            }
            SayFlattenChoice(player, tool);
            PaintOnly.Frame(player, prefab);

            bool straighten = facts.Square && Settings.Pressed(Settings.StraightenKey);
            bool radiusHeld = Settings.Held(Settings.RadiusKey);
            bool sharpnessHeld = Settings.Held(Settings.SharpnessKey);
            bool widthHeld = WidthHeld(facts, Limits.Now, Settings.Held(SlopeTool.WidthKey));
            int gameTurn = GameHooks.PlaceRotation(player);
            int turn = TurnAfter(turnBefore, gameTurn, straighten, radiusHeld || sharpnessHeld || widthHeld, facts.Square, Settings.Turn.Value,
                                 out bool turnedByWheel);
            if (turn != gameTurn)
            {
                GameHooks.PlaceRotation(player) = turn;
            }
            if (straighten)
            {
                wheelSum = 0f;
                Say(player, "Lined up with the world");
                return;
            }
            if (radiusHeld || sharpnessHeld || widthHeld)
            {
                int notch = Notch(ZInput.GetMouseScrollWheel());
                if (notch == 0)
                {
                    return;
                }
                if (Settings.ReverseWheel.Value)
                {
                    notch = -notch;
                }
                Numbers numbers = Memory.For(prefab, facts);
                if (radiusHeld && !sharpnessHeld && !widthHeld)
                {
                    Resize(player, prefab, op.m_settings, facts, numbers, notch, now);
                }
                else if (sharpnessHeld && !radiusHeld && !widthHeld)
                {
                    Sharpen(player, prefab, tool, facts, numbers, notch, now);
                }
                else if (widthHeld && !radiusHeld && !sharpnessHeld)
                {
                    Rewiden(player, prefab, facts, numbers, notch, now);
                }
                return;
            }
            wheelSum = 0f;
            if (turnedByWheel)
            {
                bool rectangle = (Memory.For(prefab, facts).Chosen & Chosen.Across) != 0;
                Say(player, $"Turned {Turn.Shown(turn, rectangle):0.#}°");
            }
        }

        /// <summary>
        /// The game's build turn after this frame, from the turn before the game read its input and the turn it left:
        /// - **The straighten key** (a square tool in hand) lines it up with the world, whatever else happened this frame.
        /// - **While the radius or sharpness key is held,** the wheel is Terraformer's: the game's turn is undone.
        /// - **Otherwise,** with 45° steps, a square tool's notch that turned it by the game's 22.5° turns it once more.
        /// <paramref name="turnedByWheel"/> says whether a square tool's turn changed by the wheel (to say it).
        /// </summary>
        internal static int TurnAfter(int before, int after, bool straighten, bool keyHeld, bool square, TurnChoice choice, out bool turnedByWheel)
        {
            turnedByWheel = false;
            if (straighten)
            {
                return 0;
            }
            if (keyHeld)
            {
                return before;
            }
            int turned = after - before;
            if (square && choice == TurnChoice.FortyFiveDegrees && (turned == 1 || turned == -1))
            {
                after += turned;
            }
            turnedByWheel = square && turned != 0 && choice != TurnChoice.DontTurn;
            return after;
        }

        /// <summary>Whether a Terraformer key is held with a terrain tool in hand: then the camera doesn't zoom.</summary>
        internal static bool Adjusting(Player player)
        {
            if (!Module.IsOn || player != Player.m_localPlayer)
            {
                return false;
            }
            if (Trees.TreeControls.Adjusting(player))
            {
                return true;   // the grown tree's size, with a sapling in hand (card B01)
            }
            Piece piece = player.GetBuildTool()?.GetSelectedPiece();
            if (piece == null || piece.GetComponent<TerrainOp>() == null)
            {
                return false;
            }
            if (Settings.Held(Settings.RadiusKey) || Settings.Held(Settings.SharpnessKey))
            {
                return true;
            }
            Tool tool = Tool.Named(piece.gameObject.name);
            var facts = new Facts(piece.GetComponent<TerrainOp>().m_settings, tool, Settings.DigDepth.Value);
            return WidthHeld(facts, Limits.Now, Settings.Held(SlopeTool.WidthKey));
        }

        /// <summary>Whether the width key counts as held: only where it can make a rectangle (a square tool, "Square tools
        /// resize" on, and a tool "Rectangles for" names), so elsewhere it leaves the wheel to turn the tool and zoom the
        /// camera as without it (review round 1).</summary>
        internal static bool WidthHeld(Facts facts, Limits limits, bool keyHeld) => keyHeld && limits.AllowsRectangle(facts);

        /// <summary>The height Flatten levels to now: the choice of the first of its keys held, else "Flatten to".</summary>
        internal static FlattenHeight FlattenChoice()
        {
            foreach ((ConfigEntry<KeyboardShortcut> key, ConfigEntry<FlattenHeight> to) in Settings.FlattenKeys)
            {
                if (Settings.Held(key))
                {
                    return to.Value;
                }
            }
            return Settings.FlattenTo.Value;
        }

        /// <summary>What a Flatten height is called on screen.</summary>
        internal static string Describe(FlattenHeight choice)
        {
            switch (choice)
            {
                case FlattenHeight.Pointed: return "where you point";
                case FlattenHeight.Lowest: return "the lowest point";
                case FlattenHeight.Highest: return "the highest point";
                case FlattenHeight.Median: return "the median";
                case FlattenHeight.Average: return "the average";
                default: return "your feet";
            }
        }

        // With Flatten in hand, a key that changes its height says so (not on picking the tool up).
        private static void SayFlattenChoice(Player player, Tool tool)
        {
            if (tool == null || !tool.Flattens)
            {
                saidFlatten = null;
                return;
            }
            FlattenHeight choice = FlattenChoice();
            if (saidFlatten.HasValue && saidFlatten.Value != choice)
            {
                Say(player, $"Flatten to {Describe(choice)}");
            }
            saidFlatten = choice;
        }

        /// <summary>A notch of the wheel, as the game counts one: +1 away from him, -1 toward him, 0 not yet.</summary>
        internal static int Notch(float wheel)
        {
            wheelSum += wheel;
            if (wheelSum > NotchThreshold)
            {
                wheelSum = 0f;
                return 1;
            }
            if (wheelSum < -NotchThreshold)
            {
                wheelSum = 0f;
                return -1;
            }
            return 0;
        }

        /// <summary>A round tool's radius (the ground it moves) after a notch: the next whole step up or down, so with 0.5 m
        /// steps every size is a whole number of metres across; held between the smallest and the largest.</summary>
        internal static float NextRadius(float radius, int notch, float step, float largest)
        {
            float next;
            if (step <= 0f)
            {
                next = radius;
            }
            else
            {
                double steps = radius / (double)step;
                double whole = notch > 0 ? Math.Floor(steps + 1e-4) + notch : Math.Ceiling(steps - 1e-4) + notch;
                next = (float)(whole * step);
            }
            next = Mathf.Clamp(next, Settings.SmallestRadius, Math.Max(Settings.SmallestRadius, largest));
            return Mathf.Round(next * 100f) / 100f;
        }

        /// <summary>What the screen says for a round tool's size: its radius and its width.</summary>
        internal static string RadiusText(float radius) =>
            $"Radius {radius.ToString("0.0#", CultureInfo.CurrentCulture)} m · {(2f * radius).ToString("0.#", CultureInfo.CurrentCulture)} m across";

        /// <summary>What the screen says for a round tool's size while it only paints (A99): its paint's radius and width.</summary>
        internal static string PaintRadiusText(float radius) =>
            $"Paint radius {radius.ToString("0.0#", CultureInfo.CurrentCulture)} m · {(2f * radius).ToString("0.#", CultureInfo.CurrentCulture)} m across";

        /// <summary>A round tool's paint radius at this reach, as its stroke is lent it (RoundStroke.Of: every radius moved
        /// by the same metres).</summary>
        internal static float PaintRadiusAt(TerrainOp.Settings settings, float reach) =>
            RoundStroke.Of(settings, new Numbers { Radius = reach, Chosen = Chosen.Radius }, Vector3.zero).PaintRadius;

        /// <summary>What the screen says for a square tool's size: its points and its width along a side (1 m a point
        /// unturned or at 22.5°, 1.41 m turned 45°); for a rectangle, its length and width ("Rectangle 9 × 5 · 8 × 4 m").</summary>
        internal static string SquareText(int points, float turn) => SquareText(points, points, turn);

        internal static string SquareText(int length, int width, float turn)
        {
            double spacing = Footprint.Spacing(turn);
            string along = ((length - 1) * spacing).ToString("0.#", CultureInfo.CurrentCulture);
            if (length == width)
            {
                return $"Square {length} × {length} · {along} m";
            }
            string across = ((width - 1) * spacing).ToString("0.#", CultureInfo.CurrentCulture);
            return $"Rectangle {length} × {width} · {along} × {across} m";
        }

        /// <summary>A square tool's side after a notch: a row of points more or fewer at each end, within the smallest and
        /// "Largest square".</summary>
        internal static int NextSide(int points, int notch, int largest) =>
            Footprint.OddWithin(Math.Max(Footprint.SmallestSize, Math.Min(largest, points + 2 * notch)));

        private static void Resize(Player player, string prefab, TerrainOp.Settings settings, Facts facts, Numbers numbers, int notch, float now)
        {
            if (facts.Square)
            {
                if (!Settings.SquaresResize.Value)
                {
                    return;
                }
                // Both sides a row each (a square stays a square: its width follows its length until the width key sets one).
                int points = NextSide(numbers.Points, notch, Settings.LargestSquare.Value);
                int width = points;
                Memory.SetPoints(prefab, points, now);
                if ((numbers.Chosen & Chosen.Across) != 0)
                {
                    width = NextSide(numbers.Width, notch, Settings.LargestSquare.Value);
                    Memory.SetWidth(prefab, width != points ? width : (int?)null, now);
                }
                Say(player, SquareText(points, width, Turn.Effective(GameHooks.PlaceRotation(player))));
                return;
            }
            // The radius he sets is the ground the tool moves; it's kept as the stroke's reach, its painted rim on top.
            float radius = NextRadius(numbers.Radius - facts.Rim, notch, Settings.RadiusStep.Value, Settings.LargestRadius.Value);
            Memory.SetRadius(prefab, radius + facts.Rim, now);
            // While it only paints (A99), the size that lands is its paint's (review round 1).
            Say(player, PaintOnly.AppliesTo(prefab) ? PaintRadiusText(PaintRadiusAt(settings, radius + facts.Rim)) : RadiusText(radius));
        }

        /// <summary>The width key and a notch (B02): a square tool's width a row more or fewer at each side, its length kept:
        /// a rectangle, for the tools "Rectangles for" names.</summary>
        private static void Rewiden(Player player, string prefab, Facts facts, Numbers numbers, int notch, float now)
        {
            if (!Limits.Now.AllowsRectangle(facts))
            {
                return;
            }
            int width = NextSide(numbers.Width, notch, Settings.LargestSquare.Value);
            Memory.SetWidth(prefab, width != numbers.Points ? width : (int?)null, now);
            Say(player, SquareText(numbers.Points, width, Turn.Effective(GameHooks.PlaceRotation(player))));
        }

        private static void Sharpen(Player player, string prefab, Tool tool, Facts facts, Numbers numbers, int notch, float now)
        {
            if (tool != null && tool.IsPrecisionLower)
            {
                float depth = Mathf.Clamp(-numbers.Lift + notch * Settings.LiftStep.Value, Facts.ShallowestDepth, Settings.LargestLift.Value);
                depth = Mathf.Round(depth * 100f) / 100f;
                Memory.SetLift(prefab, -depth, now);
                Say(player, $"Depth {depth.ToString("0.00", CultureInfo.CurrentCulture)} m");
                return;
            }
            if (tool != null && tool.IsPrecisionRaise)
            {
                float largest = Settings.LargestLift.Value;
                float lowest = Settings.LiftCanDig.Value ? -largest : 0f;
                float lift = Mathf.Clamp(numbers.Lift + notch * Settings.LiftStep.Value, lowest, largest);
                lift = Mathf.Round(lift * 100f) / 100f;
                Memory.SetLift(prefab, lift, now);
                Say(player, lift < 0f ? $"Digs {(-lift).ToString("0.00", CultureInfo.CurrentCulture)} m" : $"Lift {lift.ToString("0.00", CultureInfo.CurrentCulture)} m");
                return;
            }
            if (!facts.CanSharpen)
            {
                return;
            }
            float sharpness = Mathf.Clamp(numbers.Sharpness + notch * Settings.SharpnessStep.Value, 0f, 100f);
            sharpness = Mathf.Round(sharpness);
            Memory.SetSharpness(prefab, sharpness, now);
            Say(player, facts.Slopes ? $"Edge {sharpness:0} % sharp" : $"Sharpness {sharpness:0} %");
        }

        /// <summary>Says a value where "Messages" says; <paramref name="always"/> in the middle even with it off (what undo
        /// did, which he can't see otherwise).</summary>
        private static void Say(Player player, string text, bool always = false)
        {
            switch (Settings.Messages.Value)
            {
                case MessageChoice.Centre:
                    player.Message(MessageHud.MessageType.Center, text);
                    break;
                case MessageChoice.TopLeft:
                    player.Message(MessageHud.MessageType.TopLeft, text);
                    break;
                default:
                    if (always)
                    {
                        player.Message(MessageHud.MessageType.Center, text);
                    }
                    break;
            }
        }

        /// <summary>Every frame: forgets the tools' values on leaving building when they aren't remembered, and writes the
        /// remembered ones to the settings file a few seconds after a change (or at once on leaving building).</summary>
        internal static void Tick()
        {
            float now = Time.unscaledTime;
            Player player = Player.m_localPlayer;
            bool building = player != null && player.InPlaceMode();
            if (wasBuilding && !building)
            {
                if (!Settings.Remember.Value)
                {
                    Memory.ForgetAll(now);
                }
                Memory.WriteIfDue(now, atOnce: true);
            }
            wasBuilding = building;
            Memory.WriteIfDue(now);
            Undo.Trim();
            PaintOnly.Tick(player);
            SlopeTool.Tick(player);
        }
    }
}
