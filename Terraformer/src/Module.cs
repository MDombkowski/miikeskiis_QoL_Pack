using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using QoLMods.Kit;

namespace Terraformer
{
    /// <summary>
    /// Terraformer as a module of miikeskii's QoL Pack: the hoe's and cultivator's terrain tools, written from scratch to
    /// replace AdvancedTerrainModifiers whole. The game's own tools grow and sharpen with a key and the wheel, and each
    /// tool remembers its size and sharpness; square tools snap to the ground's grid and turn in 45° steps; Lower ground
    /// digs from the hoe's menu; a stroke reaches every zone it touches, so the ground never splits at a zone border; and
    /// a big stroke can't reshape a friend's warded ground, or a place where the game allows no building. Client-side
    /// only: every stroke of his is worked out on his own
    /// game and saved as the game's own ground data. The same code runs in the bundle and in its single release
    /// (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.terraformer";

        internal const string Name = "Terraformer";

        internal const string Version = "0.3.2";

        /// <summary>AdvancedTerrainModifiers' plugin ID (Searica's, kept by its forks too). With it running, Terraformer
        /// stays off: two mods changing the same terrain tools would each undo the other. The bundle and the single release
        /// list it as a soft dependency, so it has started, and can be seen, by the time this module looks.</summary>
        internal const string AdvancedTerrainModifiers = "Searica.Valheim.TerrainTools";

        internal static readonly (string Guid, string Name)[] Rivals =
        {
            (AdvancedTerrainModifiers, "AdvancedTerrainModifiers"),
        };

        // Marks a rival in the missing list, so DescribeMissing can say what to do about it.
        private const string RivalMark = "rival:";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "Terraformer";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off takes its tools out of the hoe's and cultivator's menus and gives the game's own " +
            "tools back their own size and sharpness at once; on brings everything back, with each tool's remembered size.";

        internal override string SwitchedOffNote => "The hoe and the cultivator work as in the game.";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override string UpdateTask => "keeping the terrain tools in step with their settings";

        /// <summary>The dust switches are a section of their own, "Terraformer - Dust" in the bundle (his ask, 2026-09-30),
        /// as are Paint only, Slope and Grown trees since job 35; the general settings stay in "Terraformer" (ModuleHost:
        /// General keeps the module's own name). In F1 the parts fold under the one heading (FoldPart, Folds).</summary>
        internal override bool OneSectionInBundle => false;

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            foreach ((string guid, string name) in Rivals)
            {
                if (Chainloader.PluginInfos.TryGetValue(guid, out BepInEx.PluginInfo rival) && rival.Instance != null)
                {
                    missing.Add(RivalMark + name);
                }
            }
            GameHooks.Resolve(missing, missingOptional);
            Trees.TreeHooks.Resolve(missingOptional);
            Folds.RebuildList = ManagerLink.Rebuild();   // null with a Jötunn too old to ask
        }

        internal override string DescribeMissing(IList<string> missing)
        {
            List<string> rivals = missing.Where(each => each.StartsWith(RivalMark, StringComparison.Ordinal))
                                         .Select(each => each.Substring(RivalMark.Length)).ToList();
            if (rivals.Count == 0)
            {
                return base.DescribeMissing(missing);
            }
            string names = string.Join(" and ", rivals);
            return $"{names} {(rivals.Count > 1 ? "are" : "is")} installed, and two mods changing the same terrain tools would " +
                   $"undo each other. Uninstall {names} (or switch it off in the mod manager) and start the game again to use " +
                   $"{Name}. {SwitchedOffNote}";
        }

        /// <summary>Every frame, also while switched off: the remembered values read once, the menus in step with the
        /// switches (off takes the tools out at once, on puts them back), each tool's dust in step with its switch (off gives
        /// the game's own back), the remembered values written a few seconds after a change, the second grass redraw after a
        /// square stroke, the settings' fold-outs in F1, and no marks or grid left behind without a player.</summary>
        internal override void Update()
        {
            Folds.Tick();
            Settings.UpgradeOnce();
            Memory.LoadOnce();
            Pieces.Sync();
            Dust.Sync();
            Controls.Tick();
            Strokes.Tick();
            Trees.TreeControls.Tick();
            if (Player.m_localPlayer == null || !IsOn)
            {
                Marks.Hide();
                Grid.Hide();
            }
        }

        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            Pieces.TakeOut,
            Marks.Clear,
            Grid.Clear,
            Dust.Restore,
            UnscaleGhost,
            Strokes.Forget,
            Trees.TreeGhost.Clear,
        };

        // After an error, a resized tool's ring goes back to the tool's own size at once, not when the ghost is next made.
        private static void UnscaleGhost()
        {
            if (Player.m_localPlayer != null)
            {
                Ghost.Unscale(Player.m_localPlayer);
            }
        }
    }
}
