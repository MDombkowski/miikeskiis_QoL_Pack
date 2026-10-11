using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Kit;

namespace Terraformer
{
    /// <summary>
    /// One part of Terraformer's settings listed under its one heading in a configuration manager's window (F1), behind a
    /// title row that opens and closes it (Folds; his rule, one collapsible heading per mod): Paint only, Slope, Grown
    /// trees and Dust (job 35). The settings file keeps each part's own section. A part binds its settings through
    /// <see cref="Bind"/> (as ModuleSettings.Bind, its orders counting down from just below the title row's), then calls
    /// <see cref="End"/>. A hidden setting is bound through ModuleSettings itself, never here: a part opening would list it.
    /// </summary>
    internal sealed class FoldPart
    {
        private readonly ModuleSettings settings;
        private readonly string part;
        private readonly ConfigEntry<bool> unfolded;
        private readonly List<ConfigEntryBase> entries = new List<ConfigEntryBase>();

        /// <param name="top">The title row's order; the part's settings follow, one order down each, unique across the
        /// whole module, since every part shows under the one heading.</param>
        internal FoldPart(ModuleSettings settings, string part, string title, int top)
        {
            this.settings = settings;
            this.part = part;
            unfolded = settings.Bind(part, "Unfolded", false, title, top,
                "Opens and closes this part of the settings in the window. It changes nothing else.", null, FoldDrawer.For(title, entries));
            First = top - 1;
        }

        /// <summary>The order for the part's first setting.</summary>
        internal int First { get; }

        internal ConfigEntry<T> Bind<T>(string key, T defaultValue, string displayName, int order, string description,
                                        AcceptableValueBase acceptableValues = null)
        {
            ConfigEntry<T> entry = settings.Bind(part, key, defaultValue, displayName, order, description, acceptableValues);
            entries.Add(entry);
            return entry;
        }

        /// <summary>Lists the part under the module's heading, behind its title row.</summary>
        internal void End() => Folds.Add(unfolded, entries, settings.SectionOf(ModuleSettings.General));
    }
}
