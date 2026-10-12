using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Chests;
using QoLMods.DayCycle;
using QoLMods.Kit;
using QoLMods.Serving;

namespace WorldSettings
{
    /// <summary>
    /// World Settings as a module of miikeskii's QoL Pack (card B23): the players' half of World Settings (Server), a module
    /// of miikeskii's Server-Side QoL Pack. On a server whose server pack answers for it, this game's day cycle follows the
    /// server's: the length of a day and of a night, how long each weather lasts and how long the wind holds (the day cycle
    /// part, QoLMods\parts\DayCycle). The game computes the time of day in every player's game from the server's clock and
    /// its own day length, which the game never sends, so without this a player's sky would keep the game's own cycle while
    /// the server sleeps by another. On a server without it: the game's own, untouched. Since 0.2.0 (card B29), in a world
    /// its own game runs (alone, or the game's Start Server), it sets the cycle itself by its "Your own world" settings (Own),
    /// and friends who join with the pack hear it as they would hear World Settings (Server) (the part
    /// QoLMods\parts\Serving, HostPack). The line under its Enabled switch in F1 says which applies (Own.Line).
    ///
    /// It hears the server through the QoL Pack's side of the handshake, ServerPack (in the chest service part): it says
    /// the hello itself every frame it's due, so it works with no chest module running, and never asks the chest service
    /// for anything else. The same code runs in the bundle and in its single release (standalone\Plugin.cs); see
    /// QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.worldsettings";

        internal const string Name = "World Settings";

        internal const string Version = "0.2.0";

        /// <summary>The server pack's module whose answer this follows.</summary>
        internal const string ServerModule = "WorldSettingsServer";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "WorldSettings";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "On a server running World Settings (Server), your game follows the server's length of day and night, how long " +
            "each weather lasts and how long the wind holds, so your sky and your bed match the server's. In a world your own " +
            "game runs (alone, or the game's Start Server), \"Your own world\" below sets them, and friends who join with the " +
            "pack follow it. The line under this switch says which applies where you are. Anywhere else nothing changes. " +
            "Off: the game's own, at once.";

        internal override string SwitchedOffNote => "The day, night, weather and wind are the game's own.";

        internal override string UpdateTask => "following the server's day";

        internal static ManualLogSource Log => current?.Host?.Log;

        /// <summary>True while the module runs and its Enabled switch is on.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>True while the module runs (started, and not switched off by an error), whatever its switch says.</summary>
        internal static bool Running => current != null && current.Host != null && current.Host.Active;

        private static void Say(string message) => Log?.LogInfo(message);

        private static void Warn(string message) => Log?.LogWarning(message);

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
            DayCycle.Resolve(missing);
            HostPack.Serve(ServerModule, Version, () => Own.Serving,
                           add => Own.AnswerCycle().Write((key, seconds) => add(key, HostPack.Number(seconds))), Name);
        }

        /// <summary>Every frame (also while switched off, so off takes the cycle back at once).</summary>
        internal override void Update()
        {
            ServerPack.Tick();   // the hello, due or not (the chest service may say it too: twice a frame changes nothing)
            HostPack.Tick();     // friends' hellos in a world this game runs (Nap Time may turn it too: once a frame)
            ZNet net = ZNet.instance;
            bool serving = net != null && net.IsServer();
            if (!Host.IsOn || !serving)
            {
                Own.Forget();   // switched on again, or a world served again later, takes the settings at once
            }
            DayCycle.Follow(!Host.IsOn ? null : serving ? Own.Wanted() : FromServer.Cycle(), Say);
            Cycle answer = Own.Serving ? Own.AnswerCycle() : Cycle.GameOwn;
            if (!answer.Equals(answered))
            {
                if (answered != null)
                {
                    HostPack.Changed();   // friends hear the new cycle at once
                }
                answered = answer;
            }
        }

        private Cycle answered;

        internal override IEnumerable<Action> UndoSteps => new Action[] { () => DayCycle.Follow(null, Say) };
    }

    /// <summary>What the server asks for, read from the server pack's answer.</summary>
    internal static class FromServer
    {
        private static object connection;
        private static long server;
        private static Cycle last;

        /// <summary>
        /// The cycle the server's World Settings (Server) answers with, or null for the game's own: alone, hosting, not yet
        /// answered on this connection, or a server pack that says it doesn't run it (or has it off). An answer that has
        /// only gone stale (no hello said for a minute, as through a long load or a respawn) keeps what the server last
        /// said on this connection, so the sky doesn't flip to the game's own and back.
        /// </summary>
        internal static Cycle Cycle()
        {
            ZNet net = ZNet.instance;
            if (net == null || net.IsServer())
            {
                connection = null;
                server = 0;
                last = null;
                return null;
            }
            object now = ServerPack.Connection();
            long id = ServerPack.ServerId();
            if (!ReferenceEquals(now, connection) || id != server)
            {
                connection = now;
                server = id;
                last = null;
            }
            if (ServerPack.Runs(Module.ServerModule))
            {
                last = QoLMods.DayCycle.Cycle.Read(key => ServerPack.Value(Module.ServerModule, key));
            }
            else if (ServerPack.Answered)
            {
                last = null;   // the server pack answers, without World Settings (Server) or with it off
            }
            return last;
        }
    }
}
