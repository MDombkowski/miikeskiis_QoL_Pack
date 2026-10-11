using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace UseCase
{
    /// <summary>
    /// Which pieces TimedTorchesStayLit (Tastychickenlegs, 1.4.0, in his profile) keeps lit with no fuel on this computer,
    /// for the setting "Pieces TimedTorchesStayLit keeps lit". Read from that mod's own settings when the index is made
    /// (its study page, and its code decompiled 2026-09-30): eight switches in [Stay Lit], one per vanilla piece, and a
    /// list of other prefab names in [Custom]; a piece it keeps lit can't be refueled at all (its prefix on
    /// Fireplace.Interact). Without that mod, or switched off in its settings, nothing is kept lit.
    /// </summary>
    internal static class TorchesStayLit
    {
        internal const string Guid = "Tastychickenlegs.TimedTorchesStayLit";

        private static readonly (string Key, string Prefab)[] Switches =
        {
            ("Wall Torch Stay Lit", "piece_walltorch"),
            ("Metal Torch Stay Lit", "piece_groundtorch"),
            ("Wood Torch Stay Lit", "piece_groundtorch_wood"),
            ("Green Torch Stay Lit", "piece_groundtorch_green"),
            ("Blue Torch Stay Lit", "piece_groundtorch_blue"),
            ("Floor Brazier Stay Lit", "piece_brazierfloor01"),
            ("Ceiling Brazier Stay Lit", "piece_brazierceiling01"),
            ("Jackoturnip Stay Lit", "piece_jackoturnip"),
        };

        private static HashSet<string> kept = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Reads its settings again (each time the index is made: that mod reloads its file when it changes).</summary>
        internal static void Read()
        {
            var now = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) && info?.Instance != null)
                {
                    ConfigFile config = info.Instance.Config;
                    bool on = !config.TryGetEntry("", "Mod Enabled", out ConfigEntry<bool> enabled) || enabled.Value;
                    if (on)
                    {
                        foreach ((string key, string prefab) in Switches)
                        {
                            if (config.TryGetEntry("Stay Lit", key, out ConfigEntry<bool> entry) && entry.Value)
                            {
                                now.Add(prefab);
                            }
                        }
                        // Its own rule: names separated by commas, compared exactly, with no spaces trimmed.
                        if (config.TryGetEntry("Custom", "Custom Items Stay Lit", out ConfigEntry<string> custom) && !string.IsNullOrEmpty(custom.Value))
                        {
                            foreach (string prefab in custom.Value.Split(','))
                            {
                                if (prefab.Length > 0)
                                {
                                    now.Add(prefab);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Its settings changed shape: nothing counts as kept lit, and the game's own fuel shows.
                now.Clear();
            }
            kept = now;
        }

        /// <summary>Whether that mod keeps this piece lit with no fuel on this computer.</summary>
        internal static bool Keeps(string prefab) => prefab != null && kept.Contains(prefab);
    }
}
