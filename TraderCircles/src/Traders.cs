using System.Collections.Generic;
using UnityEngine;

namespace TraderCircles
{
    /// <summary>
    /// The mod's work each frame, outside drawing: notices a new world, notices when a trader is found, asks the server
    /// about a trader once it is switched on and not yet found, and works out its circles when places arrive. Positions
    /// are never logged; the log says only which trader, how many circles and whether the self-check passed.
    /// </summary>
    internal static class Traders
    {
        /// <summary>How often, in seconds, the traders' map icons are looked at and a question may go out.</summary>
        private const float CheckEvery = 1f;

        /// <summary>How long, in milliseconds, circles may be placed in one frame.</summary>
        private const double FrameBudget = 3.0;

        /// <summary>How long, in seconds, without an answer before the log says the server hasn't answered.</summary>
        private const float AnswerWait = 30f;

        private static readonly Dictionary<Vector3, string> icons = new Dictionary<Vector3, string>();

        // The world being played, known by its network object: a new one means a new world, or the same one rejoined.
        private static ZNet world;
        private static float nextCheck;
        private static float joinedAt;
        private static bool warnedNoIcons;

        /// <summary>Whether any trader has circles to draw now.</summary>
        internal static bool AnyCircles
        {
            get
            {
                foreach (Trader trader in Trader.All)
                {
                    if (trader.ShowsCircles)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        internal static void Update()
        {
            if (ZNet.instance != world)
            {
                world = ZNet.instance;
                foreach (Trader trader in Trader.All)
                {
                    trader.Forget();
                }
                ServerQuestion.NewWorld();
                joinedAt = Time.unscaledTime;
                warnedNoIcons = false;
                nextCheck = 0f;
            }
            if (world == null || !Module.IsOn)
            {
                return;
            }

            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + CheckEvery;
                CheckTraders();
            }
            FitCircles();
        }

        private static void CheckTraders()
        {
            if (Player.m_localPlayer == null || Game.instance == null || ZoneSystem.instance == null || ZRoutedRpc.instance == null ||
                WorldGenerator.instance == null)
            {
                return;
            }

            // A trader counts as found once the server has sent its camp's map icon, which it does to every player the
            // moment anyone comes near the camp. A player's game gets the icons shortly after joining; until then it
            // can't tell found from not found, so nothing is asked. On a server or in single player they are at hand.
            if (!world.IsServer())
            {
                icons.Clear();
                ZoneSystem.instance.GetLocationIcons(icons);
                if (icons.Count == 0)
                {
                    if (!warnedNoIcons && Time.unscaledTime - joinedAt > 60f && AnyShown())
                    {
                        warnedNoIcons = true;
                        Module.Log.LogInfo("The server hasn't sent the map's location icons yet, so no trader can be told found or " +
                                           "not, and the server isn't asked anything until it has.");
                    }
                    return;
                }
            }

            // Working out a trader's ground takes a moment, so at most one is worked out per check.
            bool groundWorkedOut = false;
            foreach (Trader trader in Trader.All)
            {
                if (!trader.Found && ZoneSystem.instance.GetLocationIcon(trader.LocationName, out _))
                {
                    trader.Found = true;
                    bool hadCircles = trader.Circles.Count > 0;
                    trader.ForgetPlaces();
                    if (hadCircles)
                    {
                        Module.Log.LogInfo($"{trader.DisplayName} has been found in this world, so {trader.DisplayName}'s circles are gone.");
                    }
                }
                if (trader.Found || !trader.Shown.Value)
                {
                    continue;
                }

                if (!trader.Asked)
                {
                    if (trader.Ground == null)
                    {
                        if (groundWorkedOut)
                        {
                            continue;   // the next check
                        }
                        groundWorkedOut = true;
                        trader.Ground = Ground.For(trader);
                        if (trader.Ground == null)
                        {
                            trader.Asked = true;   // nothing to ask about
                            Module.Log.LogWarning($"This game has no camp named {trader.LocationName}, so there are no circles for {trader.DisplayName}.");
                            continue;
                        }
                    }
                    ServerQuestion.Ask(trader);
                }
                else if (trader.Spots.Count == 0 && !trader.WarnedNoAnswer && trader.AskedAt > 0f && Time.unscaledTime - trader.AskedAt > AnswerWait)
                {
                    trader.WarnedNoAnswer = true;
                    Module.Log.LogInfo($"The server hasn't answered where {trader.DisplayName} could be. An unmodded server stays " +
                                       $"silent only when its world has no place left for {trader.DisplayName}, and a server mod may stop the " +
                                       "question; either way there are no circles for that trader.");
                }
            }
        }

        private static void FitCircles()
        {
            // Circles are placed for at most a few milliseconds per frame, so a whole answer or a new size never stalls
            // a frame; a new size's circles replace the old ones only once all are placed.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (Trader trader in Trader.All)
            {
                if (trader.Found || trader.Spots.Count == 0 || trader.Ground == null || WorldGenerator.instance == null)
                {
                    continue;
                }
                float radius = trader.Radius;
                if (trader.CirclesRadius == 0f)
                {
                    trader.CirclesRadius = radius;   // the first circles: shown as they come
                }
                bool newSize = trader.CirclesRadius != radius;
                List<Circle> filling = newSize ? trader.NextCircles : trader.Circles;
                if (newSize && trader.NextRadius != radius)
                {
                    trader.NextCircles.Clear();
                    trader.NextRadius = radius;
                }
                while (filling.Count < trader.Spots.Count && watch.Elapsed.TotalMilliseconds < FrameBudget)
                {
                    filling.Add(PlaceNext(trader, filling.Count, radius));
                }
                if (newSize && filling.Count == trader.Spots.Count)
                {
                    trader.Circles.Clear();
                    trader.Circles.AddRange(trader.NextCircles);
                    trader.CirclesRadius = radius;
                    trader.NextCircles.Clear();
                    trader.NextRadius = 0f;
                }
                LogSummary(trader);
            }
        }

        private static Circle PlaceNext(Trader trader, int index, float radius)
        {
            int seed = WorldGenerator.instance.m_world != null ? WorldGenerator.instance.m_world.m_seed : 0;
            return CircleFit.Place(trader.Ground, trader, trader.Spots[index], radius, seed, world.GetWorldUID());
        }

        // Once the answer is complete (no new place for a second) and every place's circle is placed, one line for the
        // size shown: how many, how big, and the self-check. Again whenever the size changes. No position.
        private static void LogSummary(Trader trader)
        {
            float radius = trader.CirclesRadius;
            if (Time.unscaledTime - trader.LastSpotAt < 1f || trader.Circles.Count != trader.Spots.Count ||
                (trader.LoggedCircles == trader.Circles.Count && trader.LoggedRadius == radius))
            {
                return;
            }
            trader.LoggedCircles = trader.Circles.Count;
            trader.LoggedRadius = radius;

            int missing = 0;
            int offGround = 0;
            int cut = 0;
            for (int i = 0; i < trader.Circles.Count; i++)
            {
                Circle circle = trader.Circles[i];
                if (!circle.Holds(trader.Spots[i]))
                {
                    missing++;
                }
                else if (!circle.PlaceOnGround)
                {
                    offGround++;
                }
                else if (circle.Cut)
                {
                    cut++;
                }
            }
            string drawn = $"{trader.DisplayName}: {trader.Circles.Count - missing} circles, {Mathf.RoundToInt(radius * 2f)} m across";
            drawn += cut > 0 ? $", {cut} of them cut where {trader.DisplayName} can't appear." : ".";
            if (missing == 0 && offGround == 0)
            {
                Module.Log.LogInfo($"{drawn} Self-check passed: each holds its place in its drawn part.");
            }
            else
            {
                Module.Log.LogWarning($"{drawn} Self-check failed: {missing} left out because the place wasn't inside, {offGround} drawn " +
                                      $"whole, uncut, because the place lies outside the ground where {trader.DisplayName} can appear as this " +
                                      "mod works it out. The game's rules may have changed; please report this.");
            }
        }

        private static bool AnyShown()
        {
            foreach (Trader trader in Trader.All)
            {
                if (trader.Shown.Value)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
