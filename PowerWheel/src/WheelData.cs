using System;
using System.Diagnostics;
using UnityEngine;
using Valheim.UI;

namespace PowerWheel
{
    /// <summary>
    /// The game's wheel data, RadialData.SO: the wheel's element prefabs, its ring sizes and the player's Radial Menu
    /// settings. The game sets it only in RadialDataInitializer.Awake, which runs the first time the object holding that
    /// initializer wakes: the game's own wheel's first opening (G), and probably its Settings window's (whose Radial Menu
    /// tab writes the data without a check; read from the code, not seen). Until then it isn't there, and 0.1.0 left every press
    /// to the game, F using the power at once and its own key doing nothing (card A75, his tests; 0.1.1's key log said
    /// "the wheel's data (RadialData.SO) is missing" at every press). So when it's missing it is made here the way that
    /// Awake makes it: the game's own RadialData.Init, with the data the game's initializer holds. When the game's
    /// initializer wakes later, it does the same again with the same data.
    /// </summary>
    internal static class WheelData
    {
        /// <summary>How long between two searches for the game's initializer when none was found (seconds).</summary>
        private const double SearchEvery = 10;

        // Seams for the scratch harness, which has no Unity engine: where the initializer's data comes from, and how it is
        // made the game's. Null in the game: the game's own, found at start (GameHooks).
        internal static Func<RadialDataSO> FromInitializer = null;
        internal static Action<RadialDataSO> InitData = null;

        // When the last search found nothing (Stopwatch seconds); a search finds the game's objects, so it isn't run every
        // press while there's nothing to find.
        internal static double searchedAt = double.NegativeInfinity;

        private static bool said;

        /// <summary>The game's wheel data, made from the game's initializer's if the game hasn't made it yet; null when it
        /// isn't there and can't be made.</summary>
        internal static RadialDataSO Get()
        {
            RadialDataSO data = RadialData.SO;
            if (data != null)
            {
                return data;
            }
            Func<RadialDataSO> from = FromInitializer ?? (GameHooks.CanMakeRadialData ? GameHooks.FindInitializerData : (Func<RadialDataSO>)null);
            Action<RadialDataSO> init = InitData ?? (GameHooks.CanMakeRadialData ? GameHooks.InitRadialData : (Action<RadialDataSO>)null);
            double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            if (from == null || init == null || now - searchedAt < SearchEvery)
            {
                return null;
            }
            RadialDataSO found = from();
            if (found == null)
            {
                searchedAt = now;
                return null;
            }
            init(found);
            data = RadialData.SO;
            if (data == null)
            {
                searchedAt = now;   // the game's call didn't set it: not searched again every press
            }
            if (data != null && !said && Module.IsOn)
            {
                said = true;
                Module.Log.LogInfo("The game's wheel data wasn't set up yet (the game does that when its own wheel first opens, G), " +
                                   "so it was set up the way the game does it, and the power wheel can open.");
            }
            return data;
        }
    }
}
