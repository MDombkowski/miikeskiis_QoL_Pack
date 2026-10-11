using System.Collections.Generic;
using BepInEx.Bootstrap;

namespace TopperUpper
{
    /// <summary>
    /// Other mods that fuel lights or feed stations themselves (card A77, "Works with, and what to switch off instead"):
    /// while one is installed, Topper-Upper feeds no light or station and puts nothing away from them (the loose-item
    /// pickup leaves every station's output to the rival too), so two mods never
    /// feed the same thing; it says so once in the log and in each hover line. Their plugin IDs were read from their own
    /// DLLs (the research's prior-art downloads, 2026-10-09): only those two were at hand, so the others the card names
    /// (OttoFuel, Valheim Plus's autoFuel, AutoRefillFires) aren't recognised yet; switch those off by hand.
    /// </summary>
    internal static class Rivals
    {
        /// <summary>TastyChickenLegs' AutomaticFuel (1.5.5's plugin ID).</summary>
        internal const string AutomaticFuel = "TastyChickenLegs.AutomaticFuel";

        /// <summary>blacks7ar's LazyVikings (1.2.4's plugin ID).</summary>
        internal const string LazyVikings = "blacks7ar.LazyVikings";

        private static readonly (string Guid, string Name)[] Known =
        {
            (AutomaticFuel, "AutomaticFuel"),
            (LazyVikings, "LazyVikings"),
        };

        /// <summary>The rivals running, by name; empty when none is.</summary>
        internal static readonly List<string> Found = new List<string>();

        /// <summary>True while a rival runs: Topper-Upper then feeds nothing.</summary>
        internal static bool Any => Found.Count > 0;

        private static bool lookedAtFirstTurn;

        /// <summary>At the module's first frame, when every plugin has started (one that starts after this module isn't
        /// listed yet when it resolves): looks again, once.</summary>
        internal static void AtFirstTurn()
        {
            if (!lookedAtFirstTurn)
            {
                lookedAtFirstTurn = true;
                Resolve();
            }
        }

        private static bool told;

        internal static void Resolve()
        {
            Found.Clear();
            foreach ((string guid, string name) in Known)
            {
                if (Chainloader.PluginInfos.TryGetValue(guid, out BepInEx.PluginInfo rival) && rival.Instance != null)
                {
                    Found.Add(name);
                }
            }
            if (Any && !told)
            {
                told = true;
                Module.Warn($"{string.Join(" and ", Found)} {(Found.Count > 1 ? "are" : "is")} installed and feeds lights and stations " +
                            "itself, so Topper-Upper feeds none and puts nothing away from them (its loose-item pickup leaves what " +
                            "lies at their outputs too), so the two never feed the same thing. Its loose-item pickup works " +
                            $"otherwise as set. Uninstall {string.Join(" and ", Found)} (or switch it off in the mod manager) to use Topper-Upper.");
            }
        }
    }
}
