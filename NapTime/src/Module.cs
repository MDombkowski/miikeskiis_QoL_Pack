using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Chests;
using QoLMods.Kit;
using QoLMods.Naps;
using QoLMods.Serving;

namespace NapTime
{
    /// <summary>
    /// Nap Time as a module of miikeskii's QoL Pack (card B24, his idea of 2026-10-11; his picks the same day): the players'
    /// half of Nap Time (Server), a module of miikeskii's Server-Side QoL Pack. The game's bed refuses the morning; on a
    /// server whose Nap Time (Server) is on, this lets the player lie down in the daytime inside the server's nap window,
    /// marks each lie-down a nap or not (in the afternoon by the key used and the player's settings), says so on the bed's
    /// hover text,
    /// and says "Good evening" when a nap ends at dusk. The server decides the skip itself, once everyone is in bed (the
    /// rules: QoLMods\parts\Naps). Since 0.2.0 (card B29) it does the same in a world its own game runs (alone, or the
    /// game's Start Server), by its "Your own world" settings: the skip there is the part's NapSkip, and friends who join
    /// with the pack hear it as they would hear Nap Time (Server) (the part QoLMods\parts\Serving, HostPack). Anywhere else
    /// nothing changes. The line under its Enabled switch in F1 says which applies (Own.Line).
    ///
    /// It hears the server through the QoL Pack's side of the handshake, ServerPack (in the chest service part), as World
    /// Settings does; in a game that serves the world itself with a plugin of the server pack's kind here, it asks Nap Time
    /// (Server) in this same game (Server). The same code runs in the bundle and in its single release
    /// (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.naptime";

        internal const string Name = "Nap Time";

        internal const string Version = "0.2.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "NapTime";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Lets you lie down in your bed in the daytime: once everyone is in bed, the clock runs on to sunset. On a server " +
            "running Nap Time (Server), by that server's hours; in a world your own game runs (alone, or the game's Start " +
            "Server), by \"Your own world\" below. In the afternoon your bed's two keys pick a nap or the game's sleep to the " +
            "next morning (below). The line under this switch says which applies where you are. Anywhere else nothing " +
            "changes. Off: your bed is the game's own at once.";

        internal override string SwitchedOffNote => "Your bed is the game's own.";

        internal override string UpdateTask => "following the server's naps";

        internal static ManualLogSource Log => current?.Host?.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>True while the module runs (started, and not switched off by an error), whatever its switch says.</summary>
        internal static bool Running => current != null && current.Host != null && current.Host.Active;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal static void Info(string message) => Log?.LogInfo(message);

        internal static void Warn(string message) => Log?.LogWarning(message);

        internal override void BindSettings(ModuleSettings settings)
        {
            Settings.Bind(settings);
            var hints = ModuleSettings.HintsOf(settings.Enabled);   // the kit's hints (Jötunn has a class of that name too)
            if (hints != null)
            {
                hints.CustomDrawer = entry => Where.DrawSwitch(entry, Own.Line, Warn);   // the switch, and what applies here under it
            }
        }

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            Beds.Resolve(missing);
            NapSkip.Rules = Own.Rules;
            NapSkip.Guard = Guard;
            NapSkip.Say = Info;
            var skip = new List<string>();
            NapSkip.Resolve(skip);
            if (skip.Count > 0)
            {
                missingOptional.Add($"naps in your own world ({string.Join(", ", skip)})");
            }
            HostPack.Serve(NapRules.ServerModuleId, Version, () => Own.Serving,
                           add => Settings.OwnServed(NapSkip.NapTo).Write((key, number) => add(key, HostPack.Number(number)), add), Name);
        }

        /// <summary>Every frame (also while switched off, so off takes the bed's mark back at once).</summary>
        internal override void Update()
        {
            ServerPack.Tick();   // the hello, due or not (the chest service may say it too: twice a frame changes nothing)
            HostPack.Tick();     // friends' hellos in a world this game runs (World Settings may turn it too: once a frame)
            NapSkip.Watch();
            Server.Tick(Host.IsOn);
            Served answer = Own.Serving ? Settings.OwnServed(NapSkip.NapTo) : null;
            if (!Equals(answer, answered))
            {
                if (answered != null || answer != null)
                {
                    HostPack.Changed();   // friends hear at once when what this game serves changes
                }
                answered = answer;
            }
            if (!Host.IsOn)
            {
                Beds.ClearMark();
            }
        }

        private Served answered;

        internal override IEnumerable<Action> UndoSteps => new Action[] { Beds.ClearMark };
    }
}
