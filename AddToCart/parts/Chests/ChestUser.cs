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

        /// <param name="name">The module's name, for log lines.</param>
        /// <param name="log">Its log. Asked for each time, since a module's host exists only once it runs.</param>
        /// <param name="guard">Its Module.Guard.</param>
        /// <param name="isOn">Its Module.IsOn: running and switched on. The service stops a fetch that is under way
        /// before taking anything once this turns false.</param>
        internal ChestUser(string name, Func<ManualLogSource> log, Action<string, Action> guard, Func<bool> isOn)
        {
            Name = name;
            this.log = log;
            this.guard = guard;
            this.isOn = isOn;
        }

        internal string Name { get; }

        internal ManualLogSource Log => log();

        internal bool IsOn => isOn();

        internal void Guard(string task, Action work) => guard(task, work);
    }
}
