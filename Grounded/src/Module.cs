using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;
using QoLMods.ListPicker;

namespace Grounded
{
    /// <summary>
    /// Grounded as a module of miikeskii's QoL Pack (card A74, his name): while a key is held, a rug being placed looks
    /// through the rugs already there and lies flush on the floor under the cursor, so many rugs can overlap on one floor
    /// instead of each sitting on the last. Floors and rugs are lists in the settings. Client-side only: a rug placed
    /// this way is an ordinary piece for everyone. The same code runs in the bundle and in its single release
    /// (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.grounded";

        internal const string Name = "Grounded";

        internal const string Version = "0.2.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "Grounded";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off gives the game's own placement back at once; on, rugs lie on the floor " +
            "again while the key works.";

        internal override string SwitchedOffNote => "Pieces are placed as the game places them.";

        internal override string UpdateTask => "following the key";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. The patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            GameHooks.Resolve(missing, missingOptional);
            Lists.Resolve(missingOptional);
        }

        internal override void Update()
        {
            if (IsOn)
            {
                KeyState.Update();
            }
            else
            {
                KeyState.Reset();
            }
        }

        /// <summary>Grounded changes nothing that lasts: only where the game aims while the key works. After an error it
        /// switches the toggle off and forgets what it found.</summary>
        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            KeyState.Reset,
            Drawn.Forget,
            Placement.Forget,
        };
    }
}
