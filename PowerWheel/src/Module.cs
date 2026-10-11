using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace PowerWheel
{
    /// <summary>
    /// PowerWheel as a module of miikeskii's QoL Pack (card A75, his name): a tap of the game's power key (F) uses the
    /// forsaken power as before, and holding it opens the game's own radial wheel with every forsaken power's icon; letting
    /// go picks the one under the pointer. Client-side only. The same code runs in the bundle and in its single release
    /// (standalone\Plugin.cs); see QoLMods\MODULES.md. On out of the box (his ask, the card's decision 2).
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.powerwheel";

        internal const string Name = "PowerWheel";

        internal const string Version = "0.2.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "PowerWheel";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off gives F back to the game at once: it uses your forsaken power as you press it, " +
            "and a wheel of this mod's that is open closes.";

        internal override string SwitchedOffNote => "F uses the forsaken power as in the game.";

        internal override string UpdateTask => "following its switch";

        internal override string LateUpdateTask => "updating the wheel";

        internal override IEnumerable<Action> UndoSteps => new Action[] { Wheel.CloseIfOurs, Controls.Restore, Looks.Hide, Keys.Reset };

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional) => GameHooks.Resolve(missing, missingOptional);

        /// <summary>Every frame, also while switched off: off takes back at once whatever the mod has open.</summary>
        internal override void Update()
        {
            KeyLog.Follow();
            if (!IsOn)
            {
                Wheel.CloseIfOurs();
                Controls.Restore();
                Looks.Hide();
                Keys.Reset();
            }
        }

        /// <summary>After every frame's updates, the wheel's own and the game's Player.Update included: whether the patch on
        /// Player.Update ran (PatchCheck), the wheel's cooldown lines, the description beside it.</summary>
        internal override void LateUpdate()
        {
            if (IsOn)
            {
                PatchCheck.LateTick();
                Wheel.Tick();
            }
        }
    }
}
