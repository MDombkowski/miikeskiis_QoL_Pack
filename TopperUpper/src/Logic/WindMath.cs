using System;
using System.Collections.Generic;

namespace TopperUpper.Logic
{
    /// <summary>One weather of a biome, for its wind: how often it comes (its weight) and its wind's range.</summary>
    internal struct WindWeather
    {
        internal float Weight;
        internal float WindMin;
        internal float WindMax;
    }

    /// <summary>
    /// The windmill's power and the game's wind, on plain values (card A93, his addition 2: the windmill's wind three
    /// ways). The game's own (Valheim 1.0.17, the decompile of 2026-10-10): Windmill.GetPowerOutput is (1 - its cover) ×
    /// Utils.LerpStep(its m_minWindSpeed, 1, the wind's intensity); EnvMan.UpdateWind makes the intensity from a noise
    /// x = 0.5 + the sum over the octaves 1, 2, 4 and 8 of (u - 0.5) ÷ the octave (each u drawn evenly from 0 to 1), as
    /// Mathf.Lerp(the weather's m_windMin, its m_windMax, x), held between 0.05 and 1 (SetTargetWind); and a biome's
    /// weather is drawn by its weight (SelectWeightedEnvironment). The biome average is the power that gives on average:
    /// over each weather by its weight, and over the noise, worked out on an even grid of the four draws.
    /// </summary>
    internal static class WindMath
    {
        /// <summary>The noise's octaves, as EnvMan.AddWindOctave is called.</summary>
        internal static readonly int[] Octaves = { 1, 2, 4, 8 };

        /// <summary>How many points per draw the average takes (n to the fourth in all).</summary>
        internal const int Grid = 12;

        internal static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        /// <summary>Utils.LerpStep: where v lies between l and h, held between 0 and 1.</summary>
        internal static float LerpStep(float l, float h, float v) => Clamp01((v - l) / (h - l));

        /// <summary>Windmill.GetPowerOutput for a wind's intensity.</summary>
        internal static float Power(float cover, float minWind, float intensity) => (1f - cover) * LerpStep(minWind, 1f, intensity);

        /// <summary>
        /// Whether a windmill's noted wind (its "Departure Wind Speed") is written now: only once its catch-up for this load
        /// is settled (review F1: noted before, the wind now would overwrite the wind it was left in, and Departure would be
        /// Arrival), and only when the wind moved at least <paramref name="noticeably"/> from the note (or there's none).
        /// </summary>
        internal static bool ShouldNote(bool catchUpSettled, float noted, float wind, float noticeably) =>
            catchUpSettled && (noted < 0f || Math.Abs(noted - wind) >= noticeably);

        /// <summary>The intensity a weather gives for a noise value x (EnvMan.UpdateWind, SetTargetWind).</summary>
        internal static float Intensity(float windMin, float windMax, float x)
        {
            float value = windMin + (windMax - windMin) * Clamp01(x);   // Mathf.Lerp
            return value < 0.05f ? 0.05f : value > 1f ? 1f : value;
        }

        /// <summary>The average of <paramref name="of"/> over the game's wind noise, on an even grid of
        /// <paramref name="grid"/> points per draw (the middle of each of n equal parts).</summary>
        internal static double OverNoise(Func<float, double> of, int grid = Grid)
        {
            int n = Math.Max(1, grid);
            double sum = 0.0;
            long count = 0;
            for (int a = 0; a < n; a++)
            {
                for (int b = 0; b < n; b++)
                {
                    for (int c = 0; c < n; c++)
                    {
                        for (int d = 0; d < n; d++)
                        {
                            double x = 0.5 + (Mid(a, n) - 0.5) / Octaves[0] + (Mid(b, n) - 0.5) / Octaves[1] +
                                       (Mid(c, n) - 0.5) / Octaves[2] + (Mid(d, n) - 0.5) / Octaves[3];
                            sum += of((float)x);
                            count++;
                        }
                    }
                }
            }
            return sum / count;
        }

        private static double Mid(int i, int n) => (i + 0.5) / n;

        /// <summary>
        /// The power a windmill gets on average in a biome: each weather by its weight, each over the wind's noise, through
        /// the windmill's own curve (its cover and its least wind). NaN for a biome with no weather of any weight (the
        /// caller then uses the wind now).
        /// </summary>
        internal static float BiomeAverage(IList<WindWeather> weathers, float cover, float minWind, int grid = Grid)
        {
            double weights = 0.0;
            double sum = 0.0;
            if (weathers != null)
            {
                foreach (WindWeather weather in weathers)
                {
                    if (!(weather.Weight > 0f))
                    {
                        continue;
                    }
                    WindWeather each = weather;
                    double mean = OverNoise(x => Power(cover, minWind, Intensity(each.WindMin, each.WindMax, x)), grid);
                    sum += each.Weight * mean;
                    weights += each.Weight;
                }
            }
            return weights > 0.0 ? (float)(sum / weights) : float.NaN;
        }
    }
}
