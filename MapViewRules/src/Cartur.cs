using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace MapViewRules
{
    /// <summary>
    /// What this mod needs to know of Cartur's Map Pins, read from its plugin and settings and never changed: whether it
    /// is installed, and whether it still shrinks pins and hides names by zoom itself, which this mod can do per section.
    /// </summary>
    internal static class Cartur
    {
        /// <summary>Its plugin ID, which is also the Harmony ID of its patches (its Plugin.Awake).</summary>
        internal const string Guid = "com.jekkle.valheim.carturmappins";

        private static ConfigEntry<float> zoomedOutScale;
        private static ConfigEntry<float> hideLabelsFrom;

        internal static bool Installed { get; private set; }

        /// <summary>Finds Cartur's Map Pins and the two settings read here. The mod runs without it: only its
        /// categories go unrecognised.</summary>
        internal static void Resolve(List<string> missingOptional)
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) || info.Instance == null)
            {
                missingOptional.Add("Cartur's Map Pins (its categories aren't recognised; All pins and the game's own pins keep their rules)");
                return;
            }
            Installed = true;
            ConfigFile config = info.Instance.Config;
            config.TryGetEntry("General", "ZoomedOutPinScale", out zoomedOutScale);
            config.TryGetEntry("General", "HideLabelsFromZoom", out hideLabelsFrom);
        }

        /// <summary>A sentence for the log when Cartur's still shrinks pins or hides names by zoom itself, or null.</summary>
        internal static string StillShrinks()
        {
            if (!Installed)
            {
                return null;
            }
            var doing = new List<string>();
            if (zoomedOutScale != null && zoomedOutScale.Value < 1f)
            {
                doing.Add($"shrinks every pin as the map zooms out (ZoomedOutPinScale {zoomedOutScale.Value.ToString(CultureInfo.InvariantCulture)}; 1 turns it off)");
            }
            if (hideLabelsFrom != null && hideLabelsFrom.Value < 100f)
            {
                doing.Add($"hides every name past a zoom (HideLabelsFromZoom {hideLabelsFrom.Value.ToString(CultureInfo.InvariantCulture)}; 100 turns it off)");
            }
            if (doing.Count == 0)
            {
                return null;
            }
            return $"Cartur's Map Pins also {string.Join(" and ", doing)}, in its General settings, so the two mods do it on top of " +
                   "each other. Change those to leave it to Map View Rules' sections.";
        }
    }
}
