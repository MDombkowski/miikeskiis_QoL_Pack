using System;
using System.Collections.Generic;
using System.Reflection;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// The windmill's wind for the catch-up (card A93, his addition 2), three ways. Departure Wind Speed (his default):
    /// the wind noted on the windmill while a game feeding it looked after it, in its own record
    /// ("modprojects.topperupper.wind", the wind's intensity), at most every 30 s and only when it changed noticeably, so
    /// the last note is the wind when everyone left; a windmill never noted uses the wind now. Arrival Wind Speed: the
    /// wind now. Biome Average: the average the game's weathers for the windmill's biome give, each by how often it comes,
    /// through the windmill's own curve (WindMath). Each goes through the windmill's own cover (its private m_cover, which
    /// it refreshes every 5 s; without it, worked out from its power now), as Windmill.GetPowerOutput does. The game's own
    /// catch-up of what's in it uses the power now (Smelter.UpdateSmelter reads GetPowerOutput once for all its time).
    /// </summary>
    internal static class Winds
    {
        /// <summary>The noted wind's name in a windmill's record. Permanent, once written anywhere.</summary>
        internal const string Key = "modprojects.topperupper.wind";

        internal static readonly int KeyHash = Key.GetStableHashCode();

        /// <summary>A windmill's wind is looked at this often, in seconds.</summary>
        internal const float NoteEvery = 30f;

        /// <summary>A note is written only when the wind moved at least this much (intensity runs from 0.05 to 1).</summary>
        internal const float Noticeably = 0.05f;

        private static FieldInfo cover;
        private static readonly Dictionary<string, float> Averages = new Dictionary<string, float>(StringComparer.Ordinal);

        /// <summary>At the module's start: the windmill's private cover, or a word in the start log that it's worked out
        /// from its power instead.</summary>
        internal static void Resolve(List<string> missingOptional)
        {
            cover = typeof(Windmill).GetField("m_cover", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (cover != null && cover.FieldType != typeof(float))
            {
                cover = null;
            }
            if (cover == null)
            {
                missingOptional.Add("the windmill's cover for its departure and biome-average wind (the game has changed: Windmill.m_cover " +
                                    "not found; it's worked out from the windmill's power now)");
            }
        }

        /// <summary>From the loop: notes the wind on a windmill this game looks after and feeds (see the class), only once its
        /// catch-up for this load is settled, so the wind it was left in is read before the wind now replaces it (review F1).</summary>
        internal static void Note(Fed fed, float now)
        {
            if (!(fed.Component is Smelter smelter) || smelter.m_windmill == null || EnvMan.instance == null || now - fed.WindNotedAt < NoteEvery)
            {
                return;
            }
            bool settled = fed.CatchUpDone || CatchUps.Lost;
            if (!settled)
            {
                return;   // looked at again next pass, without waiting the 30 s
            }
            fed.WindNotedAt = now;
            if (!fed.Mine || !Settings.FeedStations.Value || !Stations.Feedable(fed) || !Stations.IsFed(fed))
            {
                return;
            }
            float wind = EnvMan.instance.GetWindIntensity();
            if (WindMath.ShouldNote(settled, fed.Zdo.GetFloat(KeyHash, -1f), wind, Noticeably))
            {
                fed.Zdo.Set(KeyHash, wind);
            }
        }

        /// <summary>The power the catch-up runs a windmill at, by the choice, and in <paramref name="said"/> which wind that
        /// was, in words for the log.</summary>
        internal static float Power(Fed fed, Smelter smelter, WindChoice choice, out string said)
        {
            Windmill windmill = smelter.m_windmill;
            float now = windmill.GetPowerOutput();
            float intensity = EnvMan.instance != null ? EnvMan.instance.GetWindIntensity() : 0f;
            float covered = CoverOf(windmill, now, intensity);
            switch (choice)
            {
                case WindChoice.DepartureWindSpeed:
                {
                    float noted = fed.Zdo.GetFloat(KeyHash, -1f);
                    if (noted < 0f)
                    {
                        said = $"the wind now ({now:0.00} power; no wind noted when it was left)";
                        return now;
                    }
                    float power = WindMath.Power(covered, windmill.m_minWindSpeed, noted);
                    said = $"the wind when it was left ({noted:0.00} wind, {power:0.00} power)";
                    return power;
                }
                case WindChoice.BiomeAverage:
                {
                    float power = BiomeAverage(windmill, covered, out string biome);
                    if (float.IsNaN(power))
                    {
                        said = $"the wind now ({now:0.00} power; the biome's weathers couldn't be read)";
                        return now;
                    }
                    said = $"the average wind of {biome} ({power:0.00} power)";
                    return power;
                }
                default:
                    said = $"the wind now ({now:0.00} power)";
                    return now;
            }
        }

        // The windmill's cover: its own, else worked out from its power now and the wind (none when it can't be).
        private static float CoverOf(Windmill windmill, float powerNow, float intensity)
        {
            if (cover != null)
            {
                try
                {
                    return Mathf.Clamp01((float)cover.GetValue(windmill));
                }
                catch (Exception)
                {
                    // Worked out below.
                }
            }
            float wind = WindMath.LerpStep(windmill.m_minWindSpeed, 1f, intensity);
            return wind > 0.01f ? Mathf.Clamp01(1f - powerNow / wind) : 0f;
        }

        // The game's weathers where the windmill stands, by weight, as EnvMan draws them (its own list for that place, with
        // the weathers a region adds or blocks; those only the Ashlands' and the Deep North's edges force left out).
        private static float BiomeAverage(Windmill windmill, float covered, out string biome)
        {
            biome = "its biome";
            if (EnvMan.instance == null || WorldGenerator.instance == null)
            {
                return float.NaN;
            }
            BiomeSector sector = WorldGenerator.instance.GetBiomeSector(windmill.transform.position);
            List<EnvEntry> entries = sector != null ? EnvMan.instance.GetAvailableEnvironments(sector) : null;
            if (entries == null)
            {
                return float.NaN;
            }
            biome = $"the {sector.Biome}";
            var weathers = new List<WindWeather>();
            var name = new System.Text.StringBuilder();
            foreach (EnvEntry entry in entries)
            {
                if (entry == null || entry.m_env == null || entry.m_ashlandsOverride || entry.m_deepnorthOverride)
                {
                    continue;
                }
                weathers.Add(new WindWeather { Weight = entry.m_weight, WindMin = entry.m_env.m_windMin, WindMax = entry.m_env.m_windMax });
                name.Append(entry.m_weight).Append('/').Append(entry.m_env.m_windMin).Append('/').Append(entry.m_env.m_windMax).Append(';');
            }
            float rounded = Mathf.Round(covered * 100f) / 100f;   // a hundredth of cover makes no difference worth working out again
            name.Append(rounded).Append('/').Append(windmill.m_minWindSpeed);
            string cached = name.ToString();
            if (!Averages.TryGetValue(cached, out float power))
            {
                power = WindMath.BiomeAverage(weathers, rounded, windmill.m_minWindSpeed);
                if (Averages.Count > 64)
                {
                    Averages.Clear();
                }
                Averages[cached] = power;
            }
            return power;
        }
    }
}
