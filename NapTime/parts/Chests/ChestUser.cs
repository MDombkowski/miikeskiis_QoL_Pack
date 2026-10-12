// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using BepInEx.Logging;

namespace QoLMods.Chests
{
    /// <summary>
    /// A module that uses the chest service. The service does its work for a module under that module's own rules: its
    /// log, and its Guard, so an error while serving it switches off only that module (MODULES.md, rule 7).
    /// </summary>
    internal sealed class ChestUser
    {
        private readonly Func<ManualLogSource> log;
        private readonly Action<string, Action> guard;
        private readonly Func<bool> isOn;
        private readonly Func<bool> claimsFar;
        private readonly Func<float> reach;

        /// <param name="name">The module's name, for log lines.</param>
        /// <param name="log">Its log. Asked for each time, since a module's host exists only once it runs.</param>
        /// <param name="guard">Its Module.Guard.</param>
        /// <param name="isOn">Its Module.IsOn: running and switched on. The service stops a fetch that is under way
        /// before taking anything once this turns false.</param>
        /// <param name="claimsFar">Whether the service may claim, for this module, a chest beyond the server's hand-out
        /// (ChestClaims, card A64); null (since card A69) for the shared setting "Chests beyond the server's reach"
        /// (ChestSettings), which every module of the pack follows.</param>
        /// <param name="reach">Its chest range in metres, read each time (ChestSettings.RangeOf): the largest among the
        /// modules that are on is the range asked of Far Fetched on the server (ServerReach); null for none.</param>
        internal ChestUser(string name, Func<ManualLogSource> log, Action<string, Action> guard, Func<bool> isOn, Func<bool> claimsFar = null,
                           Func<float> reach = null)
        {
            Name = name;
            this.log = log;
            this.guard = guard;
            this.isOn = isOn;
            this.claimsFar = claimsFar;
            this.reach = reach;
        }

        internal string Name { get; }

        internal ManualLogSource Log => log();

        internal bool IsOn => isOn();

        /// <summary>Whether the service may claim, for this module, a chest beyond the server's hand-out that no game can
        /// answer for (ChestClaims), read each time it's asked.</summary>
        internal bool ClaimsFar => claimsFar != null ? claimsFar() : ChestSettings.ClaimsFar;

        /// <summary>Its chest range in metres, read each time; 0 for none.</summary>
        internal float Reach => reach != null ? reach() : 0f;

        internal void Guard(string task, Action work) => guard(task, work);
    }
}
