using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BarNone
{
    /// <summary>
    /// Sky: crops and saplings see the sky through listed pieces (and, each by its own switch, beehives and birds' nests,
    /// windmills, the Wisp Fountain and the Eternal Pyre). Only these callers change: the game's cover test itself (Cover)
    /// is never patched, so shelter, resting, beds, stations, fermenters, eggs, fires in rain and wind, the Ashlands heat and
    /// weathering keep the game's rules.
    /// A crop that can't grow is destroyed when it ripens by whichever game owns it then, which is why this part is on for
    /// everyone in the pack (his call, 2026-09-29).
    /// </summary>
    internal static class SkyPart
    {
        /// <summary>The game casts a plant's roof ray this long.</summary>
        internal const float RoofRayLength = 100f;

        /// <summary>The game's cover rays are this long, starting this far out.</summary>
        internal const float CoverRayLength = 30f;
        internal const float CoverRayStart = 0.5f;

        private static readonly RaycastHit[] roofHits = new RaycastHit[64];
        private static readonly RaycastHit[] coverHits = new RaycastHit[32];
        private static readonly Collider[] spaceHits = new Collider[64];
        private static readonly List<bool> listedFlags = new List<bool>(64);
        private static readonly List<bool> leakyFlags = new List<bool>(64);

        private static bool windmillsWereOn;
        private static string windmillsList;

        /// <summary>The part is switched on: the mod's Enabled switch and "Sky: crops and saplings see the sky through
        /// pieces". Not the module's running state: an error in another part switches those parts off for the session, but
        /// never this one, since caged crops die at ripening without it (the module keeps its patches after an error for
        /// this). Each feature below is also lost on its own error or failed patch (Broken), and only that one.</summary>
        internal static bool On => Settings.Enabled != null && Settings.Enabled.Value && Settings.SkyThrough.Value;

        internal static bool PlantsOn => On && !Broken.Is(Feature.SkyRoof) && GameHooks.PlantsReady;

        internal static bool SpaceOn => On && Settings.SkyGrowSpace.Value && !Broken.Is(Feature.SkySpace) && GameHooks.SpaceReady;

        internal static bool BeehivesOn => On && Settings.SkyBeehives.Value && !Broken.Is(Feature.SkyBeehives) && GameHooks.BeehivesReady;

        internal static bool WispsOn => On && Settings.SkyWisps.Value && !Broken.Is(Feature.SkyWisps) && GameHooks.WispsReady;

        internal static bool WindmillsOn => On && Settings.SkyWindmills.Value && !Broken.Is(Feature.SkyWindmills) && GameHooks.WindmillsReady;

        /// <summary>After Plant.HaveRoof said "roof": the same ray again, every hit collected; the roof stays only if some
        /// hit isn't a listed piece (nor, with the leaky switch on, anything the game lets rain through).</summary>
        internal static bool RoofRemains(Plant plant)
        {
            int count = Physics.RaycastNonAlloc(plant.transform.position, Vector3.up, roofHits, RoofRayLength, GameHooks.PlantRoofMask);
            bool leakyCounts = Settings.SkyThroughLeaky.Value;
            listedFlags.Clear();
            leakyFlags.Clear();
            for (int i = 0; i < count && i < roofHits.Length; i++)
            {
                Collider collider = roofHits[i].collider;
                listedFlags.Add(PieceLists.Sky.Has(collider));
                leakyFlags.Add(leakyCounts && collider.CompareTag("leaky"));
            }
            return SkyRules.RoofRemains(count, roofHits.Length, listedFlags, leakyFlags);
        }

        /// <summary>After Plant.HaveGrowSpace said "no space": the game's sphere again with listed pieces left out, then
        /// the vines' own test as the game runs it.</summary>
        internal static bool HasSpace(Plant plant)
        {
            Vector3 position = plant.transform.position;
            int count = Physics.OverlapSphereNonAlloc(position, plant.m_growRadius, spaceHits, GameHooks.PlantSpaceMask);
            if (count >= spaceHits.Length)
            {
                return false;   // can't tell: the game's answer
            }
            for (int i = 0; i < count; i++)
            {
                Collider collider = spaceHits[i];
                if (PieceLists.Sky.Has(collider))
                {
                    continue;
                }
                Plant other = collider.GetComponent<Plant>();
                if (!SkyRules.SpaceAllows(other != null, other == plant, other != null && other.GetStatus() == Plant.Status.Healthy))
                {
                    return false;
                }
            }
            if (plant.m_growRadiusVines > 0f)
            {
                count = Physics.OverlapSphereNonAlloc(position, plant.m_growRadiusVines, spaceHits, GameHooks.PlantSpaceMask);
                for (int i = 0; i < count; i++)
                {
                    if (spaceHits[i].GetComponentInParent<Vine>() != null)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>The share of the game's 17 cover rays from this point that hit something other than a listed piece, or
        /// -1 while the game hasn't made its rays yet (then its own answer stands).</summary>
        internal static float CoverWithoutListed(Vector3 point)
        {
            Vector3[] rays = GameHooks.CoverRays;
            int mask = GameHooks.CoverMask;
            if (rays == null || rays.Length == 0 || mask == 0)
            {
                return -1f;
            }
            int covered = 0;
            foreach (Vector3 ray in rays)
            {
                // As the game casts them: from half a metre out along the ray (its length as given, not normalised), to 30 m.
                int count = Physics.RaycastNonAlloc(point + ray * CoverRayStart, ray, coverHits, CoverRayLength - CoverRayStart, mask);
                listedFlags.Clear();
                for (int i = 0; i < count && i < coverHits.Length; i++)
                {
                    listedFlags.Add(PieceLists.Sky.Has(coverHits[i].collider));
                }
                if (SkyRules.RayCovered(count, coverHits.Length, listedFlags))
                {
                    covered++;
                }
            }
            return SkyRules.Share(covered, rays.Length);
        }

        /// <summary>Every frame (while the module runs): when the windmills' switch (or the part, or the mod) or, while it's
        /// on, the sky list changes, every windmill is measured again at once, instead of at its next check within 5 s.
        /// Under the sky part's own guard, as every sky path is.</summary>
        internal static void Follow()
        {
            try
            {
                bool on = WindmillsOn;
                string list = on ? Settings.SkyPieces.Value : null;
                if (on != windmillsWereOn || !string.Equals(list, windmillsList, StringComparison.Ordinal))
                {
                    windmillsWereOn = on;
                    windmillsList = list;
                    MeasureWindmillsAgain();
                }
            }
            catch (Exception error)
            {
                Broken.Running(Feature.SkyWindmills, "measuring the windmills again", error);
            }
        }

        /// <summary>Each windmill's own cover check run again (the postfix then applies, or not, by the switch as it is
        /// now).</summary>
        internal static void MeasureWindmillsAgain()
        {
            if (GameHooks.WindmillCheckCover == null)
            {
                return;
            }
            foreach (Windmill windmill in Object.FindObjectsByType<Windmill>(FindObjectsSortMode.None))
            {
                try
                {
                    GameHooks.WindmillCheckCover.Invoke(windmill, null);
                }
                catch (Exception)
                {
                    // The game's own check on a windmill not ready for it (one still starting): its next own check, within 5 s, will do.
                }
            }
        }
    }

    /// <summary>The sky part's rules, apart from Unity so the harness can check them.</summary>
    internal static class SkyRules
    {
        /// <summary>After the game found a roof above a plant: whether one remains once listed pieces (and, with the switch
        /// on, leaky ones) let the sky through. A full buffer can't tell, so the game's roof stands.</summary>
        internal static bool RoofRemains(int count, int bufferSize, IReadOnlyList<bool> listed, IReadOnlyList<bool> leaky)
        {
            if (count >= bufferSize)
            {
                return true;
            }
            for (int i = 0; i < count; i++)
            {
                if (!listed[i] && !leaky[i])
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>One collider in a plant's growing space (a listed piece already left out): the game's rule, which
        /// lets only the plant itself and other plants that aren't healthy share it.</summary>
        internal static bool SpaceAllows(bool isPlant, bool isItself, bool isHealthy) => isPlant && (isItself || !isHealthy);

        /// <summary>A cover ray counts as covered when it hits anything that isn't a listed piece. A full buffer can't
        /// tell, so it counts as covered, as the game counts it.</summary>
        internal static bool RayCovered(int count, int bufferSize, IReadOnlyList<bool> listed)
        {
            if (count >= bufferSize)
            {
                return true;
            }
            for (int i = 0; i < count; i++)
            {
                if (!listed[i])
                {
                    return true;
                }
            }
            return false;
        }

        internal static float Share(int covered, int rays) => rays <= 0 ? 0f : covered / (float)rays;

        /// <summary>A beehive's or wisp fountain's verdict from the cover measured without listed pieces, as the game
        /// decides it from its own (below its largest cover). A cover of -1 (the game's rays not made yet) keeps the game's
        /// "no".</summary>
        internal static bool FreeSpace(float cover, float maxCover) => cover >= 0f && cover < maxCover;
    }
}
