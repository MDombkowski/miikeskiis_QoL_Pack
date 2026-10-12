using System;
using System.Collections.Generic;
using System.ComponentModel;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace Terraformer
{
    /// <summary>How Slope finds its plane.</summary>
    internal enum SlopeWay
    {
        /// <summary>From the area's highest point down to its lowest (his description).</summary>
        [Description("High to low")]
        HighToLow,

        /// <summary>The even slope nearest the ground already there (a best fit), which moves the least earth.</summary>
        [Description("Closest to the ground")]
        ClosestToGround,

        /// <summary>The first click marks where it starts, the second where it ends.</summary>
        [Description("Two clicks")]
        TwoClicks,
    }

    /// <summary>What Slope does past its high and low points.</summary>
    internal enum SlopeBeyond
    {
        [Description("Level")]
        Level,

        [Description("Keep sloping")]
        KeepSloping,
    }

    /// <summary>Slope's area.</summary>
    internal enum SlopeShape
    {
        [Description("Round")]
        Round,

        /// <summary>A square, or a rectangle with the width key.</summary>
        [Description("Square")]
        Square,
    }

    /// <summary>Which square tools can be rectangles.</summary>
    internal enum RectanglesFor
    {
        [Description("Slope only")]
        SlopeOnly,

        [Description("Every square tool")]
        EverySquareTool,
    }

    /// <summary>Which points Slope may move.</summary>
    internal enum SlopeMoves
    {
        [Description("Both")]
        Both,

        [Description("Only lower")]
        OnlyLower,

        [Description("Only raise")]
        OnlyRaise,
    }

    /// <summary>What Slope leaves the ground it moves looking like.</summary>
    internal enum SlopeGround
    {
        [Description("Dirt")]
        Dirt,

        [Description("As it was")]
        AsItWas,
    }

    /// <summary>
    /// Slope (card B02, his words of 2026-10-10: "find the highest point and lowest point, and attempt to smoothly
    /// transition the terrain from each point in a smooth slope … I want what is best"): a hoe tool that sets every point
    /// in its area onto one plane, by Terraformer's own level step with a weight that blends its edge into the ground
    /// around it (Right Alt and the wheel), within the game's 8 m. On his game, as he aims, the plane is worked out from
    /// the ground's heights there (high to low, closest to the ground, or two clicks: the setting), shown on the grid, and
    /// sent with the stroke, so whichever game applies it sets the points onto that very plane (SlopeStroke). This class:
    /// its settings, the two clicks' first point, and the working out from the ground.
    /// </summary>
    internal static class SlopeTool
    {
        /// <summary>The part holding its settings, a section of its own ("Terraformer - Slope" in the bundle).</summary>
        internal const string Part = "Slope";

        /// <summary>Slope's edge out of the box: 50 %, soft (the plan's table).</summary>
        internal const float DefaultEdge = 50f;

        internal static ConfigEntry<SlopeWay> Way { get; private set; }

        internal static ConfigEntry<SlopeBeyond> Beyond { get; private set; }

        internal static ConfigEntry<SlopeShape> Shape { get; private set; }

        internal static ConfigEntry<RectanglesFor> Rectangles { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> WidthKey { get; private set; }

        internal static ConfigEntry<int> Edge { get; private set; }

        internal static ConfigEntry<SlopeMoves> Moves { get; private set; }

        internal static ConfigEntry<SlopeGround> Ground { get; private set; }

        internal static ConfigEntry<bool> CostsAsRaise { get; private set; }

        /// <summary>Its settings, in a section of their own (Settings.Bind calls this).</summary>
        internal static void Bind(ModuleSettings settings)
        {
            var fold = new FoldPart(settings, Part, "Slope", Settings.SlopeTop);
            int order = fold.First;
            Way = fold.Bind("SlopeWay", SlopeWay.HighToLow,
                "How the slope is found", order--,
                "High to low: one even slope from the highest point in the tool's area down to its lowest, falling the way " +
                "the ground there falls, level across it (an even slope already there is left as it is). Closest to the " +
                "ground: the even slope nearest the ground already there, which moves the least earth and irons out bumps. " +
                "Two clicks: the first click marks where the slope starts and its height, the second where it ends; between " +
                "them the tool lays a ramp as wide as the tool, turned in the square tools' steps. The heights are where you " +
                "point, or what a Flatten key picks while it's held (the lowest, highest, median or average point of the " +
                "tool's area there, or your feet). The reset key, another tool or leaving building forgets the first click. " +
                "The preview grid shows the result before you click: points that rise green, those that drop orange.");

            Beyond = fold.Bind("SlopeBeyond", SlopeBeyond.Level,
                "Past the high and low points", order--,
                "Level: the slope never goes above its high point or below its low point; past them the ground is levelled " +
                "at their heights (a soft edge still blends it into the ground around). Keep sloping: the slope carries on " +
                "to the area's edge.");

            Shape = fold.Bind("SlopeArea", SlopeShape.Square,
                "Area", order--,
                "Square: Slope works on a square of the ground's points, turned and resized as the other square tools (and a " +
                "rectangle with the width key). Round: on the points within its radius, resized with the radius key.");

            Edge = fold.Bind("SlopeEdge", (int)DefaultEdge,
                "Edge", order--,
                "How sharp Slope's edge is until you change it with Terraformer's \"Sharpness key\" and the wheel, which Slope " +
                "remembers: at 100 % the whole area lies on the slope, with a step where it meets the ground around; softer, " +
                "a band inside the edge blends from the slope back to the ground, as wide as (100 % − this) of half the " +
                "area's smaller side. The reset key goes back to this.",
                new AcceptableValueRange<int>(0, 100));

            Moves = fold.Bind("SlopeMoves", SlopeMoves.Both,
                "Only lower, only raise, or both", order--,
                "Both: Slope raises and lowers the ground onto the slope. Only lower: it only cuts, leaving ground below the " +
                "slope as it is. Only raise: it only fills, leaving ground above the slope as it is.");

            Ground = fold.Bind("SlopeGround", SlopeGround.Dirt,
                "Its ground", order--,
                "Dirt: the ground Slope moves becomes dirt, as Level ground and Flatten leave it. As it was: its paint (grass, " +
                "paving, cultivated soil) stays.");

            CostsAsRaise = fold.Bind("SlopeCostsAsRaise", false,
                "Costs as Raise ground", order--,
                "Off: Slope is free and needs no workbench, as Flatten. On: each click costs what Raise ground costs (two " +
                "stone) and needs a workbench near, as Raise ground does.");
            fold.End();
        }

        /// <summary>"Rectangles for" and "Width key", with the general settings beside "Largest square" (Settings.Bind
        /// calls this): they reach every square tool, not only Slope.</summary>
        internal static void BindRectangles(ModuleSettings settings, string part, ref int order)
        {
            Rectangles = settings.Bind(part, "RectanglesFor", RectanglesFor.EverySquareTool,
                "Rectangles for", order--,
                "Which square tools the width key makes rectangles of: Slope only, or every square tool (Level ground " +
                "(square), Flatten (square), Raise ground (precision), Lower ground (precision) and the rest). A square stays " +
                "a square until the width key changes it.");

            WidthKey = settings.Bind(part, "WidthKey", new KeyboardShortcut(KeyCode.RightShift),
                "Width key", order--,
                "Hold this key and turn the mouse wheel to make a square tool's area wider or narrower, a row of points on " +
                "each side at a time, while its length stays: a rectangle, its length along the way it faces (the radius " +
                "key and the wheel still grow or shrink both sides). Each side goes up to \"Largest square\"; the reset key " +
                "makes it a square again. Right Shift is none of the game's own keys. While it's held, the wheel doesn't " +
                "turn the tool or zoom the camera; with a tool \"Rectangles for\" leaves out (or \"Square tools resize\" off) " +
                "it does nothing, and the wheel works as usual.");
        }

        /// <summary>Its edge out of the box, from "Edge" (the harness has no settings: 50 %).</summary>
        internal static float StartingEdge => Edge != null ? Mathf.Clamp(Edge.Value, 0, 100) : DefaultEdge;

        /// <summary>Whether Slope's area is a square (or rectangle) now; round otherwise.</summary>
        internal static bool AreaIsSquare => Shape == null || Shape.Value == SlopeShape.Square;

        /// <summary>"Rectangles for" now (every square tool where it isn't bound).</summary>
        internal static RectanglesFor RectanglesNow => Rectangles != null ? Rectangles.Value : RectanglesFor.EverySquareTool;

        /// <summary>The flags its strokes carry now.</summary>
        internal static SlopeFlags FlagsNow()
        {
            SlopeFlags flags = Ground == null || Ground.Value == SlopeGround.Dirt ? SlopeFlags.Dirt : SlopeFlags.None;
            if (Moves != null && Moves.Value == SlopeMoves.OnlyLower)
            {
                flags |= SlopeFlags.OnlyLower;
            }
            else if (Moves != null && Moves.Value == SlopeMoves.OnlyRaise)
            {
                flags |= SlopeFlags.OnlyRaise;
            }
            return flags;
        }

        /// <summary>The steps a two-click strip turns in: "Square tools turn" (22.5° or 45°; 90° when Off).</summary>
        internal static double StripStep(TurnChoice choice) =>
            choice == TurnChoice.TwentyTwoAndAHalfDegrees ? 22.5 : choice == TurnChoice.FortyFiveDegrees ? 45.0 : 90.0;

        /// <summary>A round area's width in points, for a two-click strip with "Area" round: its diameter's.</summary>
        internal static int RoundWidth(float radius) => Footprint.OddWithin(2 * (int)Math.Floor(radius + 1e-4) + 1);

        // ---- The two clicks' first point ----

        internal static bool HasStart { get; private set; }

        internal static int StartX { get; private set; }

        internal static int StartZ { get; private set; }

        internal static float StartHeight { get; private set; }

        /// <summary>Marks where the slope starts (the first of two clicks).</summary>
        internal static void MarkStart(int x, int z, float height)
        {
            HasStart = true;
            StartX = x;
            StartZ = z;
            StartHeight = height;
        }

        /// <summary>Forgets the first click; says so when asked and there was one.</summary>
        internal static void Forget(Player player, bool say)
        {
            if (!HasStart)
            {
                return;
            }
            HasStart = false;
            if (say && player != null)
            {
                Say(player, "Slope start forgotten");
            }
        }

        /// <summary>Every frame: the first click is forgotten, and said, on leaving building, with another tool in hand,
        /// or when "How the slope is found" is no longer two clicks.</summary>
        internal static void Tick(Player player)
        {
            if (!HasStart)
            {
                return;
            }
            if (!Module.IsOn || Way == null || Way.Value != SlopeWay.TwoClicks)
            {
                Forget(player, false);
                return;
            }
            Piece piece = player != null && player.InPlaceMode() ? player.GetBuildTool()?.GetSelectedPiece() : null;
            Tool tool = piece != null ? Tool.Named(piece.gameObject.name) : null;
            if (tool == null || !tool.IsSlope)
            {
                Forget(player, true);
            }
        }

        internal static void Say(Player player, string text)
        {
            switch (Settings.Messages.Value)
            {
                case MessageChoice.Centre:
                    QoLMods.Kit.Say.Center(text);
                    break;
                case MessageChoice.TopLeft:
                    QoLMods.Kit.Say.TopLeft(text);
                    break;
                default:
                    // Two clicks' steps are said even with Messages off: nothing else shows the first click was taken.
                    QoLMods.Kit.Say.Center(text);
                    break;
            }
        }

        // ---- Working a stroke out on his game ----

        private static readonly List<(double X, double Z, double H)> Heights = new List<(double, double, double)>();
        private static readonly List<float> Measured = new List<float>();

        /// <summary>
        /// The area a Slope stroke aimed here would take, without its plane: the tool's own (its square or rectangle at the
        /// turn, or its circle), or, with two clicks after the first, the strip from the first click's point to here.
        /// <paramref name="at"/> is the ground point he aims at (whole metres).
        /// </summary>
        internal static SlopeStroke AreaAt(Vector3 at, Numbers numbers, float turn, bool square, TurnChoice turning, out double endX, out double endZ)
        {
            int x = Preview.Nearest(at.x);
            int z = Preview.Nearest(at.z);
            endX = x;
            endZ = z;
            float sharpness = numbers.Sharpness;
            SlopeFlags flags = FlagsNow();
            if (Way != null && Way.Value == SlopeWay.TwoClicks && HasStart)
            {
                int width = square ? numbers.Across : RoundWidth(numbers.Radius);
                Footprint strip = SlopeMath.Strip(StartX, StartZ, x, z, width, StripStep(turning), out endX, out endZ);
                return SlopeStroke.Square(strip, sharpness, flags);
            }
            if (square)
            {
                return SlopeStroke.Square(new Footprint(x, z, numbers.Points, numbers.Across, turn), sharpness, flags);
            }
            return SlopeStroke.Circle(x, z, numbers.Radius, sharpness, flags);
        }

        /// <summary>
        /// A Slope stroke aimed here, worked out from his game's copy of the ground: its area and its plane. With two clicks
        /// before the first, the tool's own area with no plane (it shows where the first click measures). Null where none
        /// of its ground can be read.
        /// </summary>
        internal static SlopeStroke Work(Player player, Vector3 at, Numbers numbers, float turn, bool square, TurnChoice turning, GroundReader ground)
        {
            SlopeStroke stroke = AreaAt(at, numbers, turn, square, turning, out double endX, out double endZ);
            bool levelBeyond = Beyond == null || Beyond.Value == SlopeBeyond.Level;
            SlopeWay way = Way != null ? Way.Value : SlopeWay.HighToLow;
            Plane plane;
            if (way == SlopeWay.TwoClicks)
            {
                if (!HasStart)
                {
                    stroke.HasPlane = false;
                    return stroke;
                }
                SlopeStroke end = OwnArea(new Vector3((float)endX, at.y, (float)endZ), numbers, turn, square);
                float endHeight = Measure(player, new Vector3((float)endX, at.y, (float)endZ), end, ground);
                plane = SlopeMath.TwoPoints((StartX, StartZ, StartHeight), (endX, endZ, endHeight), levelBeyond);
            }
            else
            {
                Heights.Clear();
                foreach ((int x, int z) in stroke.Points())
                {
                    if (ground(x, z, out PointState state))
                    {
                        Heights.Add((x, z, state.Height));
                    }
                }
                if (Heights.Count == 0)
                {
                    return null;
                }
                plane = way == SlopeWay.ClosestToGround ? SlopeMath.BestFit(Heights, levelBeyond) : SlopeMath.HighToLow(Heights, levelBeyond);
            }
            stroke.Plane = SlopePlane.Of(plane, stroke.CentreX, stroke.CentreZ);
            stroke.HasPlane = true;
            return stroke;
        }

        /// <summary>Whether a Slope stroke would change anything, measured as the grid measures it (Preview.Slope): with
        /// "Its ground: dirt", a point it acts on (it moves, or is made dirt); with "As it was", a point that moves. False
        /// for "only raise" on ground wholly above the slope, "only lower" below it, or ground already on it with its paint
        /// kept: a click there is refused at no cost (review round 1).</summary>
        internal static bool ChangesAnything(List<PointChange> changes, SlopeFlags flags)
        {
            bool dirt = (flags & SlopeFlags.Dirt) != 0;
            foreach (PointChange change in changes)
            {
                if (change.Moves || (dirt && change.Painted))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The tool's own area at a point (never the strip), for measuring a click's height there.</summary>
        internal static SlopeStroke OwnArea(Vector3 at, Numbers numbers, float turn, bool square)
        {
            int x = Preview.Nearest(at.x);
            int z = Preview.Nearest(at.z);
            return square
                ? SlopeStroke.Square(new Footprint(x, z, numbers.Points, numbers.Across, turn), numbers.Sharpness, SlopeFlags.None)
                : SlopeStroke.Circle(x, z, numbers.Radius, numbers.Sharpness, SlopeFlags.None);
        }

        /// <summary>
        /// A click's height for two clicks: what the first Flatten key held picks (Controls.FlattenChoice's keys), measured
        /// as Flatten measures (your feet; where you point; the lowest, highest, median or average point of the tool's own
        /// area there); with no key held, where you point.
        /// </summary>
        internal static float Measure(Player player, Vector3 at, SlopeStroke area, GroundReader ground)
        {
            FlattenHeight choice = FlattenHeight.Pointed;
            foreach ((ConfigEntry<KeyboardShortcut> key, ConfigEntry<FlattenHeight> to) in Settings.FlattenKeys)
            {
                if (Settings.Held(key))
                {
                    choice = to.Value;
                    break;
                }
            }
            ZoneSystem zones = ZoneSystem.instance;
            float pointed = zones != null ? zones.GetGroundHeight(at) : at.y;
            if (choice == FlattenHeight.Feet)
            {
                return zones != null && player != null ? zones.GetGroundHeight(player.transform.position) : pointed;
            }
            if (choice == FlattenHeight.Pointed)
            {
                return pointed;
            }
            Measured.Clear();
            foreach ((int x, int z) in area.Points())
            {
                if (ground(x, z, out PointState state))
                {
                    Measured.Add(state.Height);
                }
            }
            return Preview.HeightOf(choice, Measured) ?? pointed;
        }

        /// <summary>Two clicks' first click: it marks where the slope starts and its height, and says so; true when it did
        /// (the click then makes no stroke, at no cost: Strokes.Begin refuses it).</summary>
        internal static bool FirstClick(Player player, Vector3 at, Numbers numbers, float turn, bool square, GroundReader ground)
        {
            if (Way == null || Way.Value != SlopeWay.TwoClicks || HasStart)
            {
                return false;
            }
            SlopeStroke own = OwnArea(at, numbers, turn, square);
            float height = Measure(player, at, own, ground);
            MarkStart(Preview.Nearest(at.x), Preview.Nearest(at.z), height);
            Say(player, "Slope starts here — click where it ends");
            return true;
        }
    }
}
