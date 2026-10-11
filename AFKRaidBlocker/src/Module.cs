using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace AFKRaidBlocker
{
    /// <summary>
    /// AFK Raid Blocker as a module of miikeskii's QoL Pack (card A98): a player who hasn't moved for some minutes, or
    /// who sits or lies down by an emote, counts as away from the keyboard, and the game's raids aren't started on him;
    /// a player nearby who is still playing gets raids as usual. Client-side: the server picks every raid from each
    /// player's own report of his base, and this mod changes only what his report says while he is away (Patches). The
    /// same code runs in the bundle and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.afkraidblocker";

        internal const string Name = "AFK Raid Blocker";

        internal const string Version = "0.1.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "AFKRaidBlocker";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off: raids come to you as usual at once, whether you're away or not, and your " +
            "away mark goes. On: the minutes start counting again from then.";

        internal override string SwitchedOffNote => "Raids come as they do without the mod.";

        internal override string UpdateTask => "watching whether you're away";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional) => GameHooks.Resolve(missing, missingOptional);

        internal override void Update() => Away.Update();

        internal override IEnumerable<Action> UndoSteps => new Action[] { Away.Stop };
    }
}
