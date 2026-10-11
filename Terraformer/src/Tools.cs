using System;
using System.Collections.Generic;

namespace Terraformer
{
    internal enum ToolTable
    {
        Hoe,
        Cultivator,
    }

    /// <summary>What a square tool does to the points in its square.</summary>
    internal enum SquareKind
    {
        /// <summary>Smooths each point to the stroke's height (within the game's 1 m smoothing limit), then paints.</summary>
        SmoothAndPaint,

        /// <summary>Raises each point toward the stroke's height plus the lift, by at most the lift, then paints.</summary>
        RaiseAndPaint,

        /// <summary>Paints only; no height change.</summary>
        Paint,

        /// <summary>Puts each point back to the world's own height and paint.</summary>
        Remove,

        /// <summary>Digs each point toward the stroke's height minus the depth, by at most the depth, then paints.</summary>
        LowerAndPaint,

        /// <summary>Sets each point to the stroke's height, within the game's 8 m limit, then paints (Flatten).</summary>
        FlattenAndPaint,

        /// <summary>Slope's: its points go onto the plane its stroke carries (SlopeStroke), never through Squares.Apply,
        /// which leaves them as they are.</summary>
        Slope,
    }

    /// <summary>The mark drawn on a tool's picture, over the game's own picture of the tool it copies.</summary>
    internal enum Mark
    {
        Frame,
        Band,
        FramedBand,
        DownArrow,
        Cross,

        /// <summary>A down arrow and a square frame (Lower ground (precision)).</summary>
        FramedDownArrow,

        /// <summary>A level sign, two flat lines (Flatten).</summary>
        Level,

        FramedLevel,

        /// <summary>No mark: the picture itself is the tool's own (the stone and cultivated paths).</summary>
        None,

        /// <summary>A ramp, a wedge rising to the right (Slope).</summary>
        Slope,
    }

    /// <summary>How a tool's picture is made from the game's picture it starts from.</summary>
    internal enum Picture
    {
        /// <summary>The game's picture as it is, with the mark.</summary>
        AsIs,

        /// <summary>The raise picture upside down (Lower ground).</summary>
        UpsideDown,

        /// <summary>Pathen's brush stroke, filled with the tool's own game picture: the paving's stones, the cultivator's
        /// tilled soil (the paved and cultivated paths; his word, 2026-09-30).</summary>
        InPathenStroke,
    }

    /// <summary>
    /// One of Terraformer's own tools: a copy of one of the game's terrain pieces with its own settings, made once per
    /// game session (Pieces) and put in the hoe's or cultivator's menu after <see cref="After"/>. The list below is the
    /// whole of what replaces AdvancedTerrainModifiers' twelve (research A), with its Shovel's Lower ground in the hoe,
    /// and since 0.2.0 (his first test) Flatten, Flatten (square) and Lower ground (precision).
    /// </summary>
    internal sealed class Tool
    {
        /// <summary>Its switch's key in the settings, permanent.</summary>
        internal string Id;

        /// <summary>Its prefab's name, which is what travels over the network: no spaces or brackets (research C).</summary>
        internal string Prefab;

        /// <summary>The game's own terrain piece it's a copy of.</summary>
        internal string Base;

        /// <summary>For <see cref="Picture.InPathenStroke"/>: the game's piece whose picture gives the shape (Pathen's).
        /// The tool's own <see cref="Base"/> always gives the colours.</summary>
        internal string PictureFrom;

        internal Picture Picture;

        internal ToolTable Table;

        /// <summary>The prefab it follows in its menu: one of the game's, or one of Terraformer's placed before it.</summary>
        internal string After;

        /// <summary>Whether its area is a square of points (or a rectangle): fixed for every tool but Slope, whose "Area"
        /// setting picks round or square (B02).</summary>
        internal bool Square
        {
            get => IsSlope ? SlopeTool.AreaIsSquare : square;
            set => square = value;
        }

        private bool square;

        internal SquareKind Kind;

        /// <summary>Its name in the build menu: the game's own name (a $ word the game translates) with a word added, or
        /// English where the game has no word for it.</summary>
        internal string Name;

        internal string EnglishName;

        internal string EnglishDescription;

        internal Mark Mark;

        /// <summary>Changes to the copied settings, made once when the copy is made.</summary>
        internal Action<TerrainOp.Settings> Adjust;

        internal bool IsLowerGround => Id == LowerGroundId;

        internal bool IsPrecisionRaise => Id == PrecisionRaiseId;

        internal bool IsPrecisionLower => Id == PrecisionLowerId;

        /// <summary>Either of the two that dig (Lower ground and Lower ground (precision)): free, and they share its
        /// workbench and stone settings and its "can't be lowered further".</summary>
        internal bool Digs => IsLowerGround || IsPrecisionLower;

        /// <summary>Flatten or Flatten (square): levels to the height its keys pick.</summary>
        internal bool Flattens => Id == FlattenId || Id == FlattenSquareId;

        /// <summary>Slope (B02): sets its area onto one plane, worked out on his game and sent with the stroke.</summary>
        internal bool IsSlope => Id == SlopeId;

        /// <summary>Whether Terraformer works out its strokes itself, in place of the game's steps: every square, round
        /// Flatten (the game has no step that levels with a soft edge), and Slope, round or square.</summary>
        internal bool OwnWork => Square || Id == FlattenId || IsSlope;

        internal const string LowerGroundId = "LowerGround";

        internal const string PrecisionRaiseId = "RaiseGroundPrecision";

        internal const string PrecisionLowerId = "LowerGroundPrecision";

        internal const string FlattenId = "Flatten";

        internal const string FlattenSquareId = "FlattenSquare";

        internal const string SlopeId = "Slope";

        /// <summary>Terraformer's own round tools start at this radius (4 m across; his word, 2026-09-30).</summary>
        internal const float StartRadius = 2f;

        // Set when the copy is made.
        internal UnityEngine.GameObject Object;
        internal Piece Piece;
        internal TerrainOp Op;

        /// <summary>The copy's own settings object: what the game hands back when a stroke of this tool is applied, so it
        /// names the tool on the applying side (research C and D).</summary>
        internal TerrainOp.Settings OpSettings => Op != null ? Op.m_settings : null;

        internal static readonly Tool[] All =
        {
            new Tool
            {
                Id = "LevelGroundSquare", Prefab = "Terraformer_level_square", Base = "mud_road_v2", Table = ToolTable.Hoe,
                After = "mud_road_v2", Square = true, Kind = SquareKind.SmoothAndPaint, Mark = Mark.Frame,
                Name = "$piece_levelground (square)", EnglishName = "Level ground (square)",
                EnglishDescription = "smooths a square of the ground's points to the height of the ground you stand on (hold " +
                                     "the game's place-anywhere key, Left Shift, for where you point), by at most 1 m in " +
                                     "all, as the game's Level ground does, and makes it dirt.",
            },
            new Tool
            {
                Id = FlattenId, Prefab = "Terraformer_flatten", Base = "mud_road_v2", Table = ToolTable.Hoe,
                After = "Terraformer_level_square", Square = false, Mark = Mark.Level,
                Name = "Flatten", EnglishName = "Flatten",
                EnglishDescription = "levels the ground to one height, all the way (up to the game's 8 m from the world's own " +
                                     "ground): your feet's, or another the Flatten keys pick as you click (the lowest, " +
                                     "highest, median or average point, or where you point), and makes it dirt.",
                Adjust = settings =>
                {
                    // Worked out by Terraformer (Squares.Flatten). The game's smoothing stays on the copy so the game saves
                    // the stroke as a height change, and its power is the soft edge's (100 %: flat to the edge).
                    settings.m_level = false;
                    settings.m_raise = false;
                    settings.m_smooth = true;
                    settings.m_smoothRadius = StartRadius;
                    settings.m_smoothPower = Facts.SharpestSmooth;
                    settings.m_paintCleared = true;
                    settings.m_paintRadius = StartRadius;
                },
            },
            new Tool
            {
                Id = FlattenSquareId, Prefab = "Terraformer_flatten_square", Base = "mud_road_v2", Table = ToolTable.Hoe,
                After = "Terraformer_flatten", Square = true, Kind = SquareKind.FlattenAndPaint, Mark = Mark.FramedLevel,
                Name = "Flatten (square)", EnglishName = "Flatten (square)",
                EnglishDescription = "levels a square of the ground's points to one height, all the way (up to the game's 8 m " +
                                     "from the world's own ground), picked as Flatten's, and makes it dirt.",
            },
            new Tool
            {
                Id = SlopeId, Prefab = "Terraformer_slope", Base = "mud_road_v2", Table = ToolTable.Hoe,
                After = "Terraformer_flatten_square", Square = true, Kind = SquareKind.Slope, Mark = Mark.Slope,
                Name = "Slope", EnglishName = "Slope",
                EnglishDescription = "lays the ground of its area onto one even slope: from its highest point down to its " +
                                     "lowest, the slope closest to the ground there, or from one click to another (the " +
                                     "setting \"How the slope is found\"), its edge blended soft or sharp, up to the game's " +
                                     "8 m from the world's own ground. The grid shows the slope before you click.",
                Adjust = settings =>
                {
                    // Worked out by Terraformer (SlopeStroke). The game's smoothing stays on the copy so the game saves the
                    // stroke as a height change; its strength is the edge's starting 50 %.
                    settings.m_level = false;
                    settings.m_raise = false;
                    settings.m_smooth = true;
                    settings.m_smoothRadius = StartRadius;
                    settings.m_smoothPower = Facts.SmoothPowerOf(SlopeTool.DefaultEdge);
                    settings.m_paintCleared = true;
                    settings.m_paintRadius = StartRadius;
                },
            },
            new Tool
            {
                Id = PrecisionRaiseId, Prefab = "Terraformer_raise_precise", Base = "raise_v2", Table = ToolTable.Hoe,
                After = "raise_v2", Square = true, Kind = SquareKind.RaiseAndPaint, Mark = Mark.Frame,
                Name = "$piece_raise (precision)", EnglishName = "Raise ground (precision)",
                EnglishDescription = "raises a square of the ground's points by the lift a click (the sharpness key and the " +
                                     "wheel set it), up to the height where you point plus the lift, never lowering any.",
            },
            new Tool
            {
                Id = LowerGroundId, Prefab = "Terraformer_lower", Base = "raise_v2", Table = ToolTable.Hoe,
                After = "Terraformer_raise_precise", Square = false, Mark = Mark.DownArrow, Picture = Picture.UpsideDown,
                Name = "Lower ground", EnglishName = "Lower ground",
                EnglishDescription = "digs where you point, like a pickaxe hit (the Shovel's dig, in the hoe).",
                Adjust = settings =>
                {
                    // The pickaxe dig's numbers (research B, digg_v3), as AdvancedTerrainModifiers' Lower ground had, at
                    // Terraformer's starting size: 2 m, with the dig's 1 m painted rim around it.
                    settings.m_raise = true;
                    settings.m_raiseRadius = StartRadius;
                    settings.m_raisePower = 0.5f;
                    settings.m_raiseDelta = -0.5f;
                    settings.m_paintCleared = true;
                    settings.m_paintRadius = StartRadius + 1f;
                },
            },
            new Tool
            {
                Id = PrecisionLowerId, Prefab = "Terraformer_lower_precise", Base = "raise_v2", Table = ToolTable.Hoe,
                After = "Terraformer_lower", Square = true, Kind = SquareKind.LowerAndPaint, Mark = Mark.FramedDownArrow,
                Picture = Picture.UpsideDown,
                Name = "Lower ground (precision)", EnglishName = "Lower ground (precision)",
                EnglishDescription = "digs a square of the ground's points down by the depth a click (the sharpness key and " +
                                     "the wheel set it), down to the height where you point minus the depth, never raising " +
                                     "any.",
                Adjust = settings =>
                {
                    settings.m_raise = true;
                    settings.m_raiseDelta = -0.5f;
                },
            },
            new Tool
            {
                Id = "PathenSquare", Prefab = "Terraformer_path_square", Base = "path_v2", Table = ToolTable.Hoe,
                After = "path_v2", Square = true, Kind = SquareKind.Paint, Mark = Mark.FramedBand,
                Name = "$piece_path (square)", EnglishName = "Pathen (square)",
                EnglishDescription = "makes a square of the ground dirt, with no height change.",
            },
            new Tool
            {
                Id = "PavedRoadSquare", Prefab = "Terraformer_paved_square", Base = "paved_road_v2", Table = ToolTable.Hoe,
                After = "paved_road_v2", Square = true, Kind = SquareKind.SmoothAndPaint, Mark = Mark.Frame,
                Name = "$piece_pavedroad (square)", EnglishName = "Paved road (square)",
                EnglishDescription = "smooths a square like Level ground (square) and paves it.",
            },
            new Tool
            {
                Id = "PavedRoadPath", Prefab = "Terraformer_paved_path", Base = "paved_road_v2", Table = ToolTable.Hoe,
                After = "Terraformer_paved_square", Square = false, Mark = Mark.None,
                PictureFrom = "path_v2", Picture = Picture.InPathenStroke,
                Name = "$piece_pavedroad (path)", EnglishName = "Paved road (path)",
                EnglishDescription = "paves the ground with no height change.",
                Adjust = settings =>
                {
                    settings.m_smooth = false;
                    settings.m_paintRadius = StartRadius;
                },
            },
            new Tool
            {
                Id = "PavedRoadPathSquare", Prefab = "Terraformer_paved_path_square", Base = "paved_road_v2", Table = ToolTable.Hoe,
                After = "Terraformer_paved_path", Square = true, Kind = SquareKind.Paint, Mark = Mark.Frame,
                PictureFrom = "path_v2", Picture = Picture.InPathenStroke,
                Name = "$piece_pavedroad (path, square)", EnglishName = "Paved road (path, square)",
                EnglishDescription = "paves a square of the ground with no height change.",
                Adjust = settings => settings.m_smooth = false,
            },
            new Tool
            {
                Id = "RemoveTerrainChanges", Prefab = "Terraformer_remove", Base = "mud_road_v2", Table = ToolTable.Hoe,
                After = "Terraformer_paved_path_square", Square = true, Kind = SquareKind.Remove, Mark = Mark.Cross,
                Name = "Remove terrain changes", EnglishName = "Remove terrain changes",
                EnglishDescription = "puts a square of the ground back to the world's own height and paint.",
                Adjust = settings =>
                {
                    settings.m_level = false;
                    settings.m_raise = false;
                    settings.m_smooth = false;
                    settings.m_paintCleared = false;
                },
            },
            new Tool
            {
                Id = "CultivateSquare", Prefab = "Terraformer_cultivate_square", Base = "cultivate_v2", Table = ToolTable.Cultivator,
                After = "cultivate_v2", Square = true, Kind = SquareKind.SmoothAndPaint, Mark = Mark.Frame,
                Name = "$piece_cultivate (square)", EnglishName = "Cultivate (square)",
                EnglishDescription = "smooths a square of the ground's points to the height where you point, by at most 1 m " +
                                     "in all, and cultivates it, lined up for planting.",
            },
            new Tool
            {
                Id = "CultivatePath", Prefab = "Terraformer_cultivate_path", Base = "cultivate_v2", Table = ToolTable.Cultivator,
                After = "Terraformer_cultivate_square", Square = false, Mark = Mark.None,
                PictureFrom = "path_v2", Picture = Picture.InPathenStroke,
                Name = "$piece_cultivate (path)", EnglishName = "Cultivate (path)",
                EnglishDescription = "cultivates the ground with no height change.",
                Adjust = settings =>
                {
                    settings.m_smooth = false;
                    settings.m_paintRadius = StartRadius;
                },
            },
            new Tool
            {
                Id = "CultivatePathSquare", Prefab = "Terraformer_cultivate_path_square", Base = "cultivate_v2", Table = ToolTable.Cultivator,
                After = "Terraformer_cultivate_path", Square = true, Kind = SquareKind.Paint, Mark = Mark.Frame,
                PictureFrom = "path_v2", Picture = Picture.InPathenStroke,
                Name = "$piece_cultivate (path, square)", EnglishName = "Cultivate (path, square)",
                EnglishDescription = "cultivates a square of the ground with no height change.",
                Adjust = settings => settings.m_smooth = false,
            },
            new Tool
            {
                Id = "GrassSquare", Prefab = "Terraformer_grass_square", Base = "replant_v2", Table = ToolTable.Cultivator,
                After = "replant_v2", Square = true, Kind = SquareKind.Paint, Mark = Mark.Frame,
                Name = "$piece_replant (square)", EnglishName = "Grass (square)",
                EnglishDescription = "grows grass back on a square of the ground, as the game's Grass does, with no height change.",
            },
        };

        private static readonly Dictionary<string, Tool> ByPrefab = new Dictionary<string, Tool>();

        static Tool()
        {
            foreach (Tool tool in All)
            {
                ByPrefab[tool.Prefab] = tool;
            }
        }

        /// <summary>Terraformer's tool with this prefab name, or null.</summary>
        internal static Tool Named(string prefab) =>
            prefab != null && ByPrefab.TryGetValue(prefab, out Tool tool) ? tool : null;

        /// <summary>Terraformer's tool whose copy owns this settings object (the one the game hands back when a stroke of
        /// this tool is applied), or null for any other terrain piece's.</summary>
        internal static Tool WithSettings(TerrainOp.Settings settings)
        {
            if (settings == null)
            {
                return null;
            }
            foreach (Tool tool in All)
            {
                if (ReferenceEquals(tool.OpSettings, settings))
                {
                    return tool;
                }
            }
            return null;
        }
    }
}
