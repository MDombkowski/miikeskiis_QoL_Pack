using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using QoLMods.Chests;
using QoLMods.Naps;
using QoLMods.Serving;


namespace NapTime
{
    /// <summary>
    /// What the server serves, worked out once a frame (Tick, from the module's Update): the nap window and the afternoon
    /// rule of a Nap Time (Server) that is on, or null for none (then nothing changes).
    /// - **Joined to a server:** from the server pack's answer in the handshake (ServerPack, in the chest service part), while
    ///   it lists Nap Time (Server) as on. An answer that has gone stale (none for a minute) counts as none.
    /// - **Alone or hosting** (this game serves the world), with a plugin of the server pack's kind in this game
    ///   (ServerHalves): it decides. The QoL Pack never says hello to itself, so this asks Nap Time (Server) in this same
    ///   game, if the server pack (or Nap Time (Server)'s own release) is installed here, through its permanent static
    ///   member NapTimeServer.Module.HostAnswer(string), which gives what it would answer, or null while it isn't on and
    ///   serving (or isn't there: then no naps). Looked up once per world, by name, from the plugin's own assembly.
    /// - **Alone or hosting, without one** (card B29): Nap Time serves the naps itself (Own), by its "Your own world"
    ///   settings.
    /// </summary>
    internal static class Server
    {
        /// <summary>The server pack's plugin ID, and Nap Time (Server)'s own release's: asked in that order.</summary>
        private static readonly string[] ServerPlugins = { "modprojects.serversideqolpack", "modprojects.naptimeserver" };

        private static Served now;
        private static object lookedOn;
        private static readonly List<Func<string, string>> hostAnswers = new List<Func<string, string>>();

        /// <summary>What the server serves this frame, or null.</summary>
        internal static Served Now => now;

        internal static void Tick(bool on)
        {
            now = on ? Find() : null;
        }

        private static Served Find()
        {
            ZNet net = ZNet.instance;
            if (net == null)
            {
                return null;
            }
            if (!net.IsServer())
            {
                if (!ServerPack.Runs(NapRules.ServerModuleId))
                {
                    return null;
                }
                return Served.Read(key => ServerPack.Value(NapRules.ServerModuleId, key));
            }
            if (!ServerHalves.KitHere)
            {
                return Own.Serving ? Settings.OwnServed(NapSkip.NapTo) : null;
            }
            object world = ZRoutedRpc.instance;
            if (!ReferenceEquals(world, lookedOn))
            {
                lookedOn = world;
                LookForHost();
            }
            foreach (Func<string, string> answer in hostAnswers)
            {
                if (answer(NapRules.FromKey) != null)
                {
                    return Served.Read(answer);
                }
            }
            return null;
        }

        // Nap Time (Server) in this same game: its HostAnswer, from each plugin that may hold it.
        private static void LookForHost()
        {
            hostAnswers.Clear();
            foreach (string id in ServerPlugins)
            {
                if (!Chainloader.PluginInfos.TryGetValue(id, out PluginInfo info) || info?.Instance is null)
                {
                    continue;
                }
                Type module = info.Instance.GetType().Assembly.GetType("NapTimeServer.Module", false);
                MethodInfo method = module?.GetMethod("HostAnswer", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                                                      null, new[] { typeof(string) }, null);
                if (method == null || method.ReturnType != typeof(string))
                {
                    continue;
                }
                hostAnswers.Add(key =>
                {
                    try
                    {
                        return method.Invoke(null, new object[] { key }) as string;
                    }
                    catch (Exception)
                    {
                        return null;   // its own error switches it off; until then, no answer
                    }
                });
            }
            if (hostAnswers.Count > 0)
            {
                Module.Info("This game serves the world: Nap Time follows Nap Time (Server) in this same game.");
            }
        }
    }
}
