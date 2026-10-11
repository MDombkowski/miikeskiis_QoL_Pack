using System;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// One zone's ground as a square stroke changes it: the game's own arrays in its terrain compiler (TerrainComp), one
    /// entry per point of the zone's 65 x 65 grid, row by row from the south-west, and the point's height as last drawn.
    /// Held as plain arrays so the harness can check the edits without the game (research D, section 1).
    /// </summary>
    internal sealed class Ground
    {
        /// <summary>The zone's width in metres between its first and last points (64): it has Width + 1 points a row.</summary>
        internal int Width;

        /// <summary>The world x and z (whole metres) of point (0, 0), the zone's south-west corner.</summary>
        internal int OriginX;

        internal int OriginZ;

        internal float[] LevelDelta;
        internal float[] SmoothDelta;
        internal bool[] ModifiedHeight;
        internal Color[] PaintMask;
        internal bool[] ModifiedPaint;

        /// <summary>A point's height as last drawn, above the zone's own height (x across, z up the rows).</summary>
        internal Func<int, int, float> HeightAt;
    }

    /// <summary>
    /// What a square tool does to the points in its square, done the game's own way point by point (research D, section 3):
    /// smoothing is kept within the game's 1 m limit and folded into the level change by a raise; a raise never lowers a
    /// point and moves it at most the lift a click; the level change is kept within 8 m; paint sets the tool's colour and
    /// keeps the grass and lava mask (the colour's alpha), and in the Deep North cultivating adds to the snow, as the game's
    /// own paint does. Every point is chosen from world coordinates, so each zone holding a copy of a border point changes
    /// it the same way: the two copies stay equal, and no seam opens (research D, section 4).
    /// </summary>
    internal static class Squares
    {
        internal const float SmoothLimit = 1f;
        internal const float LevelLimit = 8f;

        /// <summary>Changes the points of <paramref name="square"/> that lie in this zone; how many it changed.</summary>
        /// <param name="target">The stroke's height, above the zone's own height.</param>
        /// <param name="lift">For a raise: how far a click raises (above zero) or digs to (below zero).</param>
        /// <param name="paint">The tool's paint colour (alpha unused).</param>
        /// <param name="addsSnow">Cultivating in the Deep North: add to the snow channels instead of setting colours.</param>
        internal static int Apply(Ground ground, Footprint square, SquareKind kind, float target, float lift, Color paint, bool addsSnow)
        {
            int changed = 0;
            int pitch = ground.Width + 1;
            foreach ((int x, int z) in square.Points())
            {
                int column = x - ground.OriginX;
                int row = z - ground.OriginZ;
                if (column < 0 || row < 0 || column > ground.Width || row > ground.Width)
                {
                    continue;
                }
                int index = row * pitch + column;
                switch (kind)
                {
                    case SquareKind.Slope:
                        continue;   // Slope's own work (SlopeStroke.ApplyTo), never this
                    case SquareKind.SmoothAndPaint:
                        Smooth(ground, index, column, row, target);
                        Paint(ground, index, paint, addsSnow);
                        break;
                    case SquareKind.RaiseAndPaint:
                        Raise(ground, index, column, row, target, lift);
                        Paint(ground, index, paint, addsSnow);
                        break;
                    case SquareKind.Paint:
                        Paint(ground, index, paint, addsSnow);
                        break;
                    case SquareKind.Remove:
                        ground.LevelDelta[index] = 0f;
                        ground.SmoothDelta[index] = 0f;
                        ground.ModifiedHeight[index] = false;
                        ground.ModifiedPaint[index] = false;
                        break;
                    case SquareKind.LowerAndPaint:
                        Lower(ground, index, column, row, target, Math.Abs(lift));
                        Paint(ground, index, paint, addsSnow);
                        break;
                    case SquareKind.FlattenAndPaint:
                        Level(ground, index, column, row, target, 1f);
                        Paint(ground, index, paint, addsSnow);
                        break;
                }
                changed++;
            }
            return changed;
        }

        /// <summary>
        /// Round Flatten on this zone's points (Terraformer's own work, in place of the game's steps): every point within
        /// <paramref name="radius"/> of the stroke's middle point goes toward the target, all the way at the middle and,
        /// for a softer tool, less toward the edge (<see cref="SmoothWeight"/>, the game's smoothing curve without its 1 m
        /// limit); every point within <paramref name="paintRadius"/> is painted. The middle point is the ground's point
        /// nearest the stroke, as the game picks it, and distances are in world metres, so each zone holding a copy of a
        /// border point changes it the same way. How many points it changed.
        /// </summary>
        internal static int Flatten(Ground ground, int centreX, int centreZ, float radius, float power, float target,
                                    float paintRadius, Color paint, bool addsSnow)
        {
            int changed = 0;
            int pitch = ground.Width + 1;
            float reach = Math.Max(radius, paintRadius);
            int span = (int)Math.Ceiling(reach);
            for (int z = centreZ - span; z <= centreZ + span; z++)
            {
                for (int x = centreX - span; x <= centreX + span; x++)
                {
                    int column = x - ground.OriginX;
                    int row = z - ground.OriginZ;
                    if (column < 0 || row < 0 || column > ground.Width || row > ground.Width)
                    {
                        continue;
                    }
                    float distance = (float)Math.Sqrt((double)(x - centreX) * (x - centreX) + (double)(z - centreZ) * (z - centreZ));
                    int index = row * pitch + column;
                    bool touched = false;
                    if (distance <= radius)
                    {
                        Level(ground, index, column, row, target, SmoothWeight(distance, radius, power));
                        touched = true;
                    }
                    if (distance < paintRadius)
                    {
                        Paint(ground, index, paint, addsSnow);
                        touched = true;
                    }
                    if (touched)
                    {
                        changed++;
                    }
                }
            }
            return changed;
        }

        /// <summary>The game's smoothing curve: 1 at the middle, 0 at the edge, 1 - (d/r)^power (TerrainComp.SmoothTerrain;
        /// power 3 as a cube, as the game computes it).</summary>
        internal static float SmoothWeight(float distance, float radius, float power)
        {
            if (radius <= 0f)
            {
                return distance <= 0f ? 1f : 0f;
            }
            float t = distance / radius;
            t = power == 3f ? t * t * t : (float)Math.Pow(t, power);
            return 1f - t;
        }

        /// <summary>The game's raising curve: (1 - d/r)^power, or 1 everywhere for a power of 0 (TerrainComp.RaiseTerrain).</summary>
        internal static float RaiseWeight(float distance, float radius, float power)
        {
            if (power <= 0f)
            {
                return 1f;
            }
            float t = 1f - distance / radius;
            return power == 1f ? t : (float)Math.Pow(t, power);
        }

        /// <summary>The mirror of the precision raise (Lower ground (precision)): a point above target - depth goes down by
        /// at most the depth a click, never below it, and a point already there or lower is left alone; the level change
        /// stays within 8 m, as the game keeps it.</summary>
        internal static void Lower(Ground ground, int index, int column, int row, float target, float depth)
        {
            if (LowerPoint(ref ground.LevelDelta[index], ref ground.SmoothDelta[index], ground.HeightAt(column, row), target, depth))
            {
                ground.ModifiedHeight[index] = true;
            }
        }

        /// <summary>Flatten's step, the game's own level step with a weight: the point goes toward the target by the weight
        /// (1: all the way) from its height as drawn, its smoothing folded into its level change, which stays within 8 m of
        /// the world's own height (TerrainComp.LevelTerrain). No 1 m limit, as the game's level step has none.</summary>
        internal static void Level(Ground ground, int index, int column, int row, float target, float weight)
        {
            LevelPoint(ref ground.LevelDelta[index], ref ground.SmoothDelta[index], ground.HeightAt(column, row), target, weight);
            ground.ModifiedHeight[index] = true;
        }

        /// <summary>The game's smoothing at full strength: the point goes toward the target, but its smoothing stays within
        /// 1 m in all.</summary>
        internal static void Smooth(Ground ground, int index, int column, int row, float target)
        {
            SmoothPoint(ref ground.SmoothDelta[index], ground.HeightAt(column, row), target);
            ground.ModifiedHeight[index] = true;
        }

        /// <summary><see cref="Smooth"/> on one point's smoothing change (the preview asks this too).</summary>
        internal static void SmoothPoint(ref float smooth, float height, float target)
        {
            smooth = Clamp(smooth + (target - height), -SmoothLimit, SmoothLimit);
        }

        /// <summary>The game's raise at full strength. Above zero: a point below target + lift rises by at most the lift,
        /// never above it, and a point already there is left alone. Below zero (a dig): a point above target + lift goes
        /// straight down to it, and a point already below is left alone. At zero nothing moves.</summary>
        internal static void Raise(Ground ground, int index, int column, int row, float target, float lift)
        {
            if (RaisePoint(ref ground.LevelDelta[index], ref ground.SmoothDelta[index], ground.HeightAt(column, row), target, lift))
            {
                ground.ModifiedHeight[index] = true;
            }
        }

        /// <summary><see cref="Raise"/> on one point's two changes; whether it acted on the point.</summary>
        internal static bool RaisePoint(ref float level, ref float smooth, float height, float target, float lift)
        {
            float goal = target + lift;
            float next;
            if (lift > 0f)
            {
                if (goal < height)
                {
                    return false;
                }
                next = Math.Min(goal, height + lift);
            }
            else if (lift < 0f)
            {
                if (goal > height)
                {
                    return false;
                }
                next = goal;
            }
            else
            {
                return false;
            }
            Fold(ref level, ref smooth, next - height);
            return true;
        }

        /// <summary><see cref="Lower"/> on one point's two changes; whether it acted on the point.</summary>
        internal static bool LowerPoint(ref float level, ref float smooth, float height, float target, float depth)
        {
            if (depth <= 0f)
            {
                return false;
            }
            float goal = target - depth;
            if (goal > height)
            {
                return false;
            }
            Fold(ref level, ref smooth, Math.Max(goal, height - depth) - height);
            return true;
        }

        /// <summary><see cref="Level"/> on one point's two changes. Measured from the height as drawn: where a point's two
        /// changes add up past the 8 m the game draws (a dig to the floor, then smoothing), the game's own level step would
        /// stop short of the height, and Flatten would need a second click there (found by the harness).</summary>
        internal static void LevelPoint(ref float level, ref float smooth, float height, float target, float weight)
        {
            float world = height - Clamp(level + smooth, -LevelLimit, LevelLimit);   // the world's own height here
            float next = height + (target - height) * weight;
            smooth = 0f;
            level = Clamp(next - world, -LevelLimit, LevelLimit);
        }

        /// <summary>A height change as the game's level and raise steps make it: the point's smoothing is folded into its
        /// level change, which stays within 8 m of the world's own height.</summary>
        private static void Fold(ref float level, ref float smooth, float change)
        {
            float total = change + smooth;
            smooth = 0f;
            level = Clamp(level + total, -LevelLimit, LevelLimit);
        }

        /// <summary>The game's paint at full strength: the point's colour becomes the tool's, keeping its alpha (the grass
        /// and lava mask). In the Deep North, cultivating adds to the channels the colour uses (the snow depth) instead.</summary>
        internal static void Paint(Ground ground, int index, Color paint, bool addsSnow)
        {
            Color colour = ground.PaintMask[index];
            if (addsSnow)
            {
                if (paint.r > 0f)
                {
                    colour.r = Clamp(colour.r + 1f, 0f, 1f);
                }
                if (paint.g > 0f)
                {
                    colour.g = Clamp(colour.g + 1f, 0f, 1f);
                }
                if (paint.b > 0f)
                {
                    colour.b = Clamp(colour.b + 1f, 0f, 1f);
                }
            }
            else
            {
                colour.r = paint.r;
                colour.g = paint.g;
                colour.b = paint.b;
            }
            ground.PaintMask[index] = colour;
            ground.ModifiedPaint[index] = true;
        }

        private static float Clamp(float value, float low, float high) => value < low ? low : value > high ? high : value;

        /// <summary>The paint colour of the game's own paint types (Heightmap's mask colours), alpha unused.</summary>
        internal static Color ColourOf(TerrainModifier.PaintType type)
        {
            switch (type)
            {
                case TerrainModifier.PaintType.Cultivate: return new Color(0f, 1f, 0f, 1f);
                case TerrainModifier.PaintType.Paved: return new Color(0f, 0f, 1f, 1f);
                case TerrainModifier.PaintType.Reset: return new Color(0f, 0f, 0f, 1f);
                case TerrainModifier.PaintType.ClearVegetation: return new Color(0f, 0f, 0f, 1f);
                case TerrainModifier.PaintType.DeepSnow: return new Color(1f, 1f, 1f, 1f);
                default: return new Color(1f, 0f, 0f, 1f);
            }
        }
    }
}
