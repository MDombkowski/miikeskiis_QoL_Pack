// What the QoL Pack does where its own game serves the world, a part: the one copy to edit is
// Games\Valheim\QoLMods\parts\Serving\. Every mod that uses it carries a copy in its own parts\Serving\ folder, kept
// identical by QoLMods\tools\sync-kit.ps1, so each mod still builds and publishes on its own. The rules for a part are in
// QoLMods\MODULES.md. A mod that carries it carries the chest service too (parts\Chests\), whose ServerPack is the other
// side of the handshake.
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

namespace QoLMods.Serving
{
    /// <summary>
    /// The server pack's kind of plugin in this game (card B29): miikeskii's Server-Side QoL Pack, or one of its mods' own
    /// releases. Every one of them carries the server kit, whose handshake answers the QoL Pack's hello where the world is
    /// served, so one in this game decides what its modules serve here, and the QoL Pack's own serving stands aside (it
    /// would otherwise take the hello's registration from it). A plugin is of that kind when its assembly holds the server
    /// kit's handshake. Looked at again only when the number of plugins changes (they are all started long before a module
    /// first asks, from its Update or the settings window; never earlier, from its Resolve).
    /// </summary>
    internal static class ServerHalves
    {
        /// <summary>The server kit's handshake, by its full name: every plugin of the server pack's kind compiles it.</summary>
        internal const string KitHandshake = "ServerSideQoLPack.Kit.Handshake";

        private static int lookedAt = -1;
        private static readonly List<Assembly> Kits = new List<Assembly>();
        private static readonly List<string> NamesFound = new List<string>();

        /// <summary>
        /// Whether this game serves the world as a player's own (playing alone, or the game's Start Server): the only place
        /// the QoL Pack serves anything itself (his words: "a single player world (or their own locally hosted server"). Never
        /// a dedicated server, whose owner installs the server pack: the game's own IsDedicated (false in the player's
        /// build, by decompiling; the dedicated build's own), or Unity's batch mode, which a dedicated server is started in.
        /// </summary>
        internal static bool OwnWorld
        {
            get
            {
                ZNet net = ZNet.instance;
                return net != null && net.IsServer() && !net.IsDedicated() && !Application.isBatchMode;
            }
        }

        /// <summary>Whether a plugin of the server pack's kind is loaded in this game.</summary>
        internal static bool KitHere
        {
            get
            {
                Look();
                return Kits.Count > 0;
            }
        }

        /// <summary>Those plugins by name and version ("miikeskii's Server-Side QoL Pack 0.3.1"), joined with "and"; empty
        /// with none.</summary>
        internal static string Names
        {
            get
            {
                Look();
                if (NamesFound.Count <= 1)
                {
                    return NamesFound.Count == 1 ? NamesFound[0] : string.Empty;
                }
                return string.Join(", ", NamesFound.GetRange(0, NamesFound.Count - 1).ToArray()) + " and " + NamesFound[NamesFound.Count - 1];
            }
        }

        /// <summary>"is" for one plugin, "are" for several: "{Names} {Are} in your game".</summary>
        internal static string Are
        {
            get
            {
                Look();
                return NamesFound.Count > 1 ? "are" : "is";
            }
        }

        /// <summary>The type of this full name ("NapTimeServer.Module") in a plugin of the server pack's kind here, or null.</summary>
        internal static Type Find(string fullName)
        {
            Look();
            foreach (Assembly kit in Kits)
            {
                try
                {
                    Type type = kit.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch (Exception)
                {
                    // An assembly that can't be asked holds nothing this part looks for.
                }
            }
            return null;
        }

        private static void Look()
        {
            try
            {
                int plugins = Chainloader.PluginInfos.Count;
                if (plugins == lookedAt)
                {
                    return;
                }
                lookedAt = plugins;
                Kits.Clear();
                NamesFound.Clear();
                foreach (PluginInfo info in Chainloader.PluginInfos.Values)
                {
                    Assembly assembly = info?.Instance?.GetType().Assembly;
                    if (assembly == null || assembly == typeof(ServerHalves).Assembly || Kits.Contains(assembly))
                    {
                        continue;
                    }
                    bool kit;
                    try
                    {
                        kit = assembly.GetType(KitHandshake, false) != null;
                    }
                    catch (Exception)
                    {
                        kit = false;
                    }
                    if (kit)
                    {
                        Kits.Add(assembly);
                        NamesFound.Add(info.Metadata != null ? $"{info.Metadata.Name} {info.Metadata.Version}" : assembly.GetName().Name);
                    }
                }
            }
            catch (Exception)
            {
                // The plugins couldn't be listed: none of the server pack's kind is taken to be here.
            }
        }
    }
}
