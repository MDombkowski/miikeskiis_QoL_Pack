using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using QoLMods.Kit;

namespace Outfitter
{
    /// <summary>
    /// Binds settings in folded sections under Outfitter's one heading in F1 (Folds; his look at F1, 2026-09-28), each
    /// setting with the next order of its own range, so Outfitter's own sections and the stands' (job 30, src\Stands) each
    /// keep to theirs and never clash (MODULES.md rule 4: orders unique across the module, below 1000; a higher order is
    /// listed first). Every setting bound here bumps <see cref="Settings.Version"/> when it changes.
    /// </summary>
    internal sealed class SettingsBinder
    {
        private readonly ModuleSettings bound;
        private readonly int bottom;
        private int order;
        private ConfigEntry<bool> foldUnfolded;
        private List<ConfigEntryBase> foldSettings;

        /// <param name="top">The first order to use; each setting takes the next one down.</param>
        /// <param name="bottom">The last order this binder may use.</param>
        internal SettingsBinder(ModuleSettings settings, int top, int bottom)
        {
            bound = settings;
            order = top;
            this.bottom = bottom;
        }

        /// <summary>Opens a folded section: its title row (a setting of its own, "Unfolded", which remembers whether it is
        /// open), then every setting added until <see cref="EndFold"/>.</summary>
        internal void BeginFold(string part)
        {
            if (foldSettings != null)
            {
                throw new InvalidOperationException($"The section \"{part}\" was opened before the one before it was closed.");
            }
            var settings = new List<ConfigEntryBase>();
            foldUnfolded = bound.Bind(part, "Unfolded", false, part, NextOrder(),
                "Opens and closes this part of the settings in the window. It changes nothing else.", null, FoldDrawer.For(part, settings));
            foldSettings = settings;
        }

        /// <summary>Closes the section opened last, listing it under the module's heading.</summary>
        internal void EndFold()
        {
            if (foldSettings == null)
            {
                throw new InvalidOperationException("A section was closed that was never opened.");
            }
            Folds.Add(foldUnfolded, foldSettings, bound.SectionOf(ModuleSettings.General));
            foldUnfolded = null;
            foldSettings = null;
        }

        /// <summary>Binds one setting in <paramref name="part"/> (the section it is kept in), with the next order. Its key
        /// and its part never change once a version with it has been released.</summary>
        internal ConfigEntry<T> Add<T>(string part, string key, T value, string name, string description,
                                       AcceptableValueBase range = null, Action<ConfigEntryBase> drawer = null)
        {
            ConfigEntry<T> entry = bound.Bind(part, key, value, name, NextOrder(), description, range, drawer);
            entry.SettingChanged += (sender, args) => Settings.Changed();
            foldSettings?.Add(entry);
            return entry;
        }

        private int NextOrder()
        {
            if (order < bottom)
            {
                throw new InvalidOperationException($"Out of setting orders: this range ends at {bottom}.");
            }
            return order--;
        }
    }
}
