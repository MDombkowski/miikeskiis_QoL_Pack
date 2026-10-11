using System;
using System.Collections.Generic;
using UnityEngine;

namespace Terraformer
{
    /// <summary>One of the ground's points as a stroke would change it.</summary>
    internal struct PointChange
    {
        /// <summary>Its world coordinates, whole metres.</summary>
        internal int X;

        internal int Z;

        /// <summary>Its height now, in world metres.</summary>
        internal float Height;

        /// <summary>Its height once the stroke is made (the same as <see cref="Height"/> where it doesn't move).</summary>
        internal float After;

        /// <summary>How strongly the stroke acts on it, 0 to 1: the tool's curve there, for the grid's brightness.</summary>
        internal float Strength;

        /// <summary>Whether its height changes.</summary>
        internal bool Moves;

        /// <summary>Whether the stroke paints it (or, for Remove terrain changes, gives it the world's own paint back).</summary>
        internal bool Painted;
    }

    /// <summary>A ground point as the preview reads it: its height as drawn now (world metres), the two changes the game
    /// keeps for it (its level change, within 8 m, and its smoothing change, within 1 m), and whether its paint was
    /// changed.</summary>
    internal struct PointState
    {
        internal float Height;
        internal float Level;
        internal float Smooth;
        internal bool Painted;
    }

    /// <summary>Reads the ground point at these world coordinates; false where its ground isn't loaded.</summary>
    internal delegate bool GroundReader(int x, int z, out PointState state);

    /// <summary>The steps of a round stroke, with the numbers it's applied with: the tool's own settings, with the
    /// stroke's radius, sharpness and lift lent to them exactly as they will be where it's applied (Strokes.Lend applies
    /// these same values), as plain values.</summary>
    internal struct RoundStroke
    {
        internal Vector3 Position;
        internal bool Level;
        internal float LevelRadius;
        internal float LevelOffset;
        internal bool Raise;
        internal float RaiseRadius;
        internal float RaisePower;
        internal float RaiseDelta;
        internal bool Smooth;
        internal float SmoothRadius;
        internal float SmoothPower;
        internal bool Paint;
        internal float PaintRadius;
        internal bool HalfOffset;

        /// <summary>Round Flatten (Terraformer's own work): every point within the smoothing radius goes toward
        /// <see cref="FlattenTarget"/> by the smoothing curve, with no 1 m limit; paint is laid round the same middle point.</summary>
        internal bool Flatten;

        internal float FlattenTarget;

        /// <summary>A round stroke of this tool made with these numbers at this position.</summary>
        internal static RoundStroke Of(TerrainOp.Settings settings, Numbers numbers, Vector3 position, bool flatten = false)
        {
            var stroke = new RoundStroke
            {
                Position = position,
                Level = settings.m_level,
                LevelRadius = settings.m_levelRadius,
                LevelOffset = settings.m_levelOffset,
                Raise = settings.m_raise,
                RaiseRadius = settings.m_raiseRadius,
                RaisePower = settings.m_raisePower,
                RaiseDelta = settings.m_raiseDelta,
                Smooth = settings.m_smooth,
                SmoothRadius = settings.m_smoothRadius,
                SmoothPower = settings.m_smoothPower,
                Paint = settings.m_paintCleared,
                PaintRadius = settings.m_paintRadius,
                HalfOffset = settings.m_halfOffset,
                Flatten = flatten,
                FlattenTarget = position.y,
            };
            if ((numbers.Chosen & Chosen.Radius) != 0)
            {
                // Every radius moves by the same metres, so the paint's rim stays as wide as the game's.
                float by = numbers.Radius - settings.GetRadius();
                stroke.LevelRadius = Grown(stroke.LevelRadius, by);
                stroke.RaiseRadius = Grown(stroke.RaiseRadius, by);
                stroke.SmoothRadius = Grown(stroke.SmoothRadius, by);
                stroke.PaintRadius = Grown(stroke.PaintRadius, by);
            }
            if ((numbers.Chosen & Chosen.Sharpness) != 0)
            {
                if (settings.m_smooth)
                {
                    stroke.SmoothPower = Facts.SmoothPowerOf(numbers.Sharpness);
                }
                if (settings.m_raise)
                {
                    stroke.RaisePower = Facts.RaisePowerOf(numbers.Sharpness);
                }
            }
            if ((numbers.Chosen & Chosen.Lift) != 0 && settings.m_raise)
            {
                stroke.RaiseDelta = numbers.Lift;
            }
            if ((numbers.Chosen & Chosen.PaintOnly) != 0 && !flatten)
            {
                // Paint only (A99): the height steps are left out, the paint is the tool's own, as it would be.
                stroke.Level = false;
                stroke.Raise = false;
                stroke.Smooth = false;
            }
            return stroke;
        }

        private static float Grown(float radius, float by) =>
            Facts.Clamp(radius + by, Settings.SmallestRadius, Strokes.LargestTrustedRadius);

        /// <summary>How far from its middle it moves ground: the largest radius among its steps that change height.</summary>
        internal float HeightReach
        {
            get
            {
                float reach = 0f;
                if (Level)
                {
                    reach = Math.Max(reach, LevelRadius);
                }
                if (Raise)
                {
                    reach = Math.Max(reach, RaiseRadius);
                }
                if (Smooth)
                {
                    reach = Math.Max(reach, SmoothRadius);
                }
                return reach;
            }
        }
    }

    /// <summary>
    /// What a stroke would do to each of the ground's points, worked out before the click on his own game's copy of the
    /// ground (his first test, 2026-09-30: the grid, the paint's ghost, the highlight of what Lower ground will dig, and
    /// "Ground cannot be lowered further"). Round strokes follow the game's own steps point by point, in the game's order
    /// (level, raise, smooth, paint), each reading the height as drawn before the stroke, as the game's do; squares and
    /// Flatten use the very point steps Terraformer applies them with (Squares). Pure arithmetic on what the ground
    /// reader gives, so the harness checks it against the game's steps written out.
    /// </summary>
    internal static class Preview
    {
        /// <summary>A height change smaller than this doesn't count as moving (a millimetre).</summary>
        internal const float MovesBy = 0.001f;

        /// <summary>The ground's point nearest a world position, as the game picks a stroke's middle point
        /// (Heightmap.WorldToVertex: rounded half up).</summary>
        internal static int Nearest(float world) => (int)Math.Floor(world + 0.5f);

        /// <summary>The points a round stroke reaches, each with what it would do there.</summary>
        internal static void Round(RoundStroke stroke, GroundReader ground, List<PointChange> into)
        {
            into.Clear();
            int centreX = Nearest(stroke.Position.x);
            int centreZ = Nearest(stroke.Position.z);
            int paintX = centreX;
            int paintZ = centreZ;
            if (stroke.Paint && stroke.HalfOffset && !stroke.Flatten)
            {
                paintX = Nearest(stroke.Position.x - 0.5f);
                paintZ = Nearest(stroke.Position.z - 0.5f);
            }
            float heightReach = stroke.Flatten ? stroke.SmoothRadius : stroke.HeightReach;
            float reach = Math.Max(heightReach, stroke.Paint ? stroke.PaintRadius + 1f : 0f);
            int span = (int)Math.Ceiling(reach) + 1;
            for (int z = centreZ - span; z <= centreZ + span; z++)
            {
                for (int x = centreX - span; x <= centreX + span; x++)
                {
                    float d = Distance(x, z, centreX, centreZ);
                    float dPaint = Distance(x, z, paintX, paintZ);
                    bool painted = stroke.Paint && dPaint < stroke.PaintRadius;
                    bool inHeight = heightReach > 0f && d <= heightReach;
                    if (!painted && !inHeight)
                    {
                        continue;
                    }
                    if (!ground(x, z, out PointState state))
                    {
                        continue;
                    }
                    float level = state.Level;
                    float smooth = state.Smooth;
                    float strength = 0f;
                    if (stroke.Flatten)
                    {
                        if (d <= stroke.SmoothRadius)
                        {
                            float weight = Squares.SmoothWeight(d, stroke.SmoothRadius, stroke.SmoothPower);
                            Squares.LevelPoint(ref level, ref smooth, state.Height, stroke.FlattenTarget, weight);
                            strength = Math.Max(strength, weight);
                        }
                    }
                    else
                    {
                        strength = GameSteps(stroke, d, state.Height, ref level, ref smooth);
                    }
                    var change = new PointChange
                    {
                        X = x,
                        Z = z,
                        Height = state.Height,
                        After = After(state, level, smooth),
                        Painted = painted,
                    };
                    change.Moves = Math.Abs(change.After - change.Height) > MovesBy;
                    change.Strength = inHeight ? strength : PaintStrength(dPaint, stroke.PaintRadius);
                    into.Add(change);
                }
            }
        }

        /// <summary>The game's own level, raise and smooth steps on one point, in its order (TerrainComp.InternalDoOperation,
        /// LevelTerrain, RaiseTerrain, SmoothTerrain), each reading the height as drawn before the stroke. The strongest
        /// weight any step gave it.</summary>
        internal static float GameSteps(RoundStroke stroke, float d, float height, ref float level, ref float smooth)
        {
            float strength = 0f;
            float target = stroke.Position.y + stroke.LevelOffset;
            if (stroke.Level && d <= stroke.LevelRadius)
            {
                float change = target - height + smooth;
                smooth = 0f;
                level = Facts.Clamp(level + change, -Squares.LevelLimit, Squares.LevelLimit);
                strength = 1f;
            }
            if (stroke.Raise && d <= stroke.RaiseRadius)
            {
                float weight = Squares.RaiseWeight(d, stroke.RaiseRadius, stroke.RaisePower);
                float lift = stroke.RaiseDelta * weight;
                float goal = stroke.Position.y + lift;
                bool acts = true;
                if (stroke.RaiseDelta < 0f && goal > height)
                {
                    acts = false;
                }
                if (acts && stroke.RaiseDelta > 0f)
                {
                    if (goal < height)
                    {
                        acts = false;
                    }
                    else if (goal > height + lift)
                    {
                        goal = height + lift;
                    }
                }
                if (acts)
                {
                    float change = goal - height + smooth;
                    smooth = 0f;
                    level = Facts.Clamp(level + change, -Squares.LevelLimit, Squares.LevelLimit);
                }
                strength = Math.Max(strength, weight);
            }
            if (stroke.Smooth && d <= stroke.SmoothRadius)
            {
                float weight = Squares.SmoothWeight(d, stroke.SmoothRadius, stroke.SmoothPower);
                float t = weight < 0f ? 0f : weight > 1f ? 1f : weight;
                float change = height + (target - height) * t - height;
                smooth = Facts.Clamp(smooth + change, -Squares.SmoothLimit, Squares.SmoothLimit);
                strength = Math.Max(strength, weight);
            }
            return strength;
        }

        /// <summary>The points a square stroke changes, each with what it would do there (Squares' own point steps).</summary>
        internal static void Square(Footprint square, SquareKind kind, float target, float lift, GroundReader ground, List<PointChange> into)
        {
            into.Clear();
            foreach ((int x, int z) in square.Points())
            {
                if (!ground(x, z, out PointState state))
                {
                    continue;
                }
                float level = state.Level;
                float smooth = state.Smooth;
                bool painted = true;
                switch (kind)
                {
                    case SquareKind.SmoothAndPaint:
                        Squares.SmoothPoint(ref smooth, state.Height, target);
                        break;
                    case SquareKind.RaiseAndPaint:
                        Squares.RaisePoint(ref level, ref smooth, state.Height, target, lift);
                        break;
                    case SquareKind.LowerAndPaint:
                        Squares.LowerPoint(ref level, ref smooth, state.Height, target, Math.Abs(lift));
                        break;
                    case SquareKind.FlattenAndPaint:
                        Squares.LevelPoint(ref level, ref smooth, state.Height, target, 1f);
                        break;
                    case SquareKind.Remove:
                        level = 0f;
                        smooth = 0f;
                        painted = state.Painted;
                        break;
                    case SquareKind.Slope:
                        painted = false;   // Slope's preview is its own (Preview.Slope)
                        break;
                }
                var change = new PointChange
                {
                    X = x,
                    Z = z,
                    Height = state.Height,
                    After = After(state, level, smooth),
                    Painted = painted,
                    Strength = 1f,
                };
                change.Moves = Math.Abs(change.After - change.Height) > MovesBy;
                into.Add(change);
            }
        }

        /// <summary>The points a Slope stroke changes, each with its height after (the very step the applying game takes,
        /// SlopeStroke.Step) and the edge's weight as its strength; without a plane (two clicks, before the first), every
        /// point as it is, showing where the first click measures.</summary>
        internal static void Slope(SlopeStroke stroke, GroundReader ground, List<PointChange> into)
        {
            into.Clear();
            bool dirt = (stroke.Flags & SlopeFlags.Dirt) != 0;
            foreach ((int x, int z) in stroke.Points())
            {
                if (!ground(x, z, out PointState state))
                {
                    continue;
                }
                float level = state.Level;
                float smooth = state.Smooth;
                float weight = stroke.WeightAt(x, z);
                bool acted = stroke.Step(ref level, ref smooth, state.Height, stroke.TargetAt(x, z), weight);
                var change = new PointChange
                {
                    X = x,
                    Z = z,
                    Height = state.Height,
                    After = After(state, level, smooth),
                    Painted = acted && dirt,
                    Strength = stroke.HasPlane ? weight : 0f,
                };
                change.Moves = Math.Abs(change.After - change.Height) > MovesBy;
                into.Add(change);
            }
        }

        /// <summary>Whether any of the points moves: false for a dig where everything it reaches is already as deep as the
        /// game allows, or below the dig ("Ground cannot be lowered further.").</summary>
        internal static bool AnyMoves(List<PointChange> changes)
        {
            foreach (PointChange change in changes)
            {
                if (change.Moves)
                {
                    return true;
                }
            }
            return false;
        }

        private static readonly List<float> Sorted = new List<float>();

        /// <summary>The height Flatten levels to, from the points it covers: the lowest, highest, median or average of
        /// their heights now. Null with no point to go by.</summary>
        internal static float? HeightOf(FlattenHeight choice, List<float> heights)
        {
            if (heights == null || heights.Count == 0)
            {
                return null;
            }
            switch (choice)
            {
                case FlattenHeight.Lowest:
                {
                    float lowest = heights[0];
                    foreach (float height in heights)
                    {
                        lowest = Math.Min(lowest, height);
                    }
                    return lowest;
                }
                case FlattenHeight.Highest:
                {
                    float highest = heights[0];
                    foreach (float height in heights)
                    {
                        highest = Math.Max(highest, height);
                    }
                    return highest;
                }
                case FlattenHeight.Average:
                {
                    double sum = 0;
                    foreach (float height in heights)
                    {
                        sum += height;
                    }
                    return (float)(sum / heights.Count);
                }
                case FlattenHeight.Median:
                {
                    // Sorted in a list of its own, kept from one call to the next: asked every frame Flatten is aimed (review round 1).
                    List<float> sorted = Sorted;
                    sorted.Clear();
                    sorted.AddRange(heights);
                    sorted.Sort();
                    int middle = sorted.Count / 2;
                    return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2f;
                }
                default:
                    return null;
            }
        }

        /// <summary>A point's height after its changes: the height as drawn now, less its old change, plus its new one,
        /// each as the game draws it (within 8 m of the world's own height: Heightmap's ApplyToHeightmap clamp).</summary>
        internal static float After(PointState state, float level, float smooth) =>
            state.Height - Facts.Clamp(state.Level + state.Smooth, -Squares.LevelLimit, Squares.LevelLimit)
                         + Facts.Clamp(level + smooth, -Squares.LevelLimit, Squares.LevelLimit);

        /// <summary>The game's paint strength at a distance ((1 - d/r)^0.1, its curve with the tools' exponent).</summary>
        internal static float PaintStrength(float distance, float radius) =>
            radius <= 0f ? 0f : (float)Math.Pow(Math.Max(0.0, 1.0 - distance / radius), 0.1);

        private static float Distance(int x, int z, int centreX, int centreZ) =>
            (float)Math.Sqrt((double)(x - centreX) * (x - centreX) + (double)(z - centreZ) * (z - centreZ));
    }
}
