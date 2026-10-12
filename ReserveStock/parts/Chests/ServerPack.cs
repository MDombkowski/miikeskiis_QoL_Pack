// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>
    /// The pack's side of the handshake with miikeskii's Server-Side QoL Pack, the server's one plugin of his server mods
    /// (cards A92, A91, A93; protocol 1, the build plan's section 3). Once he is in the world on a server (never when this
    /// game is the server: alone or hosting there is no server pack to ask), this game says hello to the server: a routed
    /// message "modprojects.serverpack.hello" with the protocol, where to reply (one reply name per plugin, so the copy
    /// that asked gets the answer even with two plugins carrying the service), who asks (the plugin's name and version, for
    /// the server's log), and what the modules using the service say (Say: key and value pairs, plain text, numbers in the
    /// invariant culture; the chest service says "chests.reach", Topper-Upper its four). Said every 10 s, and at once (at
    /// most every 2 s) when what it says changes. The server pack answers each hello with every module it runs: its id,
    /// version, whether it's on, and that module's own key and value pairs (Far Fetched: "granted", "reach"; Topper-Upper
    /// (Server): its catch-up settings and its clock). An answer is kept until the next, and counts while it's under a
    /// minute old (six hellos), so a server pack that stops answering stops counting. Only the server's own answer counts;
    /// an answer of a newer protocol is read for what protocol 1 has; a package that can't be read is ignored (said once a
    /// connection), and the last good answer stands. Everything is forgotten on a new connection, and what the server runs
    /// is said once a connection in the log. A server without the server pack never answers, and nothing changes:
    /// ServerReach keeps its own hello to a lone Far Fetched until the server pack answers that it runs one.
    /// </summary>
    internal static class ServerPack
    {
        internal const string HelloName = "modprojects.serverpack.hello";

        /// <summary>The protocol this copy speaks.</summary>
        internal const int Protocol = 1;

        private const string ReplyPrefix = "modprojects.serverpack.reply.";

        /// <summary>This copy's own reply name: one per plugin.</summary>
        internal static readonly string ReplyName = ReplyPrefix + typeof(ServerPack).Assembly.GetName().Name.ToLowerInvariant();

        /// <summary>How often the hello is said again, in seconds; the server pack forgets a game after 90 s without one.</summary>
        internal const float HelloEvery = 10f;

        /// <summary>The least time between two hellos, in seconds, however often what it says changes.</summary>
        internal const float HelloAtMostEvery = 2f;

        /// <summary>How often what the modules say is read again for a change between hellos, in seconds.</summary>
        internal const float LookEvery = 0.25f;

        /// <summary>How long an answer counts, in seconds: six hellos' worth.</summary>
        internal const float AnswerLasts = 60f;

        /// <summary>The most modules, or key and value pairs in one module, an answer may hold: past it, the package is bad.</summary>
        internal const int MostEntries = 512;

        /// <summary>The server's id on this connection, 0 with none (alone, hosting, not connected yet). A seam for the
        /// scratch harness, as ChestClaims' clocks are.</summary>
        internal static Func<long> ServerId = () =>
        {
            ZNet net = ZNet.instance;
            ZNetPeer server = net != null && !net.IsServer() ? net.GetServerPeer() : null;
            return server != null ? server.m_uid : 0L;
        };

        /// <summary>The connection now (the game's routed messages, made anew for each). A seam for the harness.</summary>
        internal static Func<object> Connection = () => ZRoutedRpc.instance;

        /// <summary>Real time, in seconds. A seam for the harness.</summary>
        internal static Func<float> Clock = () => Time.realtimeSinceStartup;

        /// <summary>The log's two kinds of line. Seams for the harness.</summary>
        internal static Action<string> Info = text => ZLog.Log(text);
        internal static Action<string> Warning = text => ZLog.LogWarning(text);

        private static readonly List<KeyValuePair<string, Func<string>>> Sayings = new List<KeyValuePair<string, Func<string>>>();

        private static object connectedTo;
        private static bool replying;
        private static float saidAt = float.NegativeInfinity;
        private static float firstSaidAt = float.NegativeInfinity;
        private static float lookedAt = float.NegativeInfinity;
        private static long saidTo;
        private static List<KeyValuePair<string, string>> said;
        private static Answer answer;
        private static long answeredBy;
        private static float answeredAt = float.NegativeInfinity;
        private static bool toldRuns;
        private static bool toldBad;
        private static string sender;

        /// <summary>
        /// What a module says to the server pack, from its Resolve: <paramref name="value"/> is read at each hello (and a
        /// few times a second between them, to notice a change), so a setting's change reaches the server at once. A key
        /// said again replaces the earlier one (in the bundle, every module using the service registers "chests.reach"). A
        /// value that throws is left out of that hello.
        /// </summary>
        internal static void Say(string key, Func<string> value)
        {
            if (string.IsNullOrEmpty(key) || value == null)
            {
                return;
            }
            for (int i = 0; i < Sayings.Count; i++)
            {
                if (Sayings[i].Key == key)
                {
                    Sayings[i] = new KeyValuePair<string, Func<string>>(key, value);
                    return;
                }
            }
            Sayings.Add(new KeyValuePair<string, Func<string>>(key, value));
        }

        /// <summary>The server pack answered on this connection, in the last minute.</summary>
        internal static bool Answered => answer != null && FromThisServer() && Clock() - answeredAt < AnswerLasts;

        /// <summary>The server pack's name and version as it answered ("miikeskii's Server-Side QoL Pack 0.1.0"); null
        /// while it hasn't.</summary>
        internal static string Version => Answered ? answer.ServerPack : null;

        /// <summary>The server pack runs that module ("FarFetched", "TopperUpperServer"): it's in the answer, and on.</summary>
        internal static bool Runs(string moduleId) => Answered && answer.Find(moduleId) is ModuleAnswer module && module.On;

        /// <summary>The value a module of the server pack answered for a key; null when there is none (or no answer).</summary>
        internal static string Value(string moduleId, string key) => Answered ? answer.Find(moduleId)?.Value(key) : null;

        /// <summary>The real time (Time.realtimeSinceStartup) of the last answer.</summary>
        internal static float AnsweredAt => answeredAt;

        /// <summary>The real time of this connection's first hello; negative infinity until it's said. For a module that
        /// waits a moment for the first answer (Topper-Upper's catch-up).</summary>
        internal static float FirstSaidAt => firstSaidAt;

        /// <summary>What a hello's turn does this frame.</summary>
        internal enum Turn
        {
            /// <summary>Nothing: said too lately to look for a change.</summary>
            Wait,

            /// <summary>Read what the modules say, and say it only if it changed.</summary>
            Look,

            /// <summary>Say it: a new server, or ten seconds since the last.</summary>
            Say,
        }

        /// <summary>The hello's pace (see the class): said to a new server at once and every HelloEvery seconds; between
        /// those, what the modules say is read every LookEvery seconds once HelloAtMostEvery seconds have passed since the
        /// last hello, and said if it changed.</summary>
        internal static Turn Next(bool sameServer, float now, float lastSaid, float lastLooked)
        {
            if (!sameServer || now - lastSaid >= HelloEvery)
            {
                return Turn.Say;
            }
            if (now - lastSaid < HelloAtMostEvery || now - lastLooked < LookEvery)
            {
                return Turn.Wait;
            }
            return Turn.Look;
        }

        /// <summary>Every frame, from ChestClaims.Tick: registers for answers on each new connection, and says hello when due.</summary>
        internal static void Tick()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null)
            {
                return;
            }
            if (rpc != connectedTo)
            {
                NewConnection(rpc);
                try
                {
                    rpc.Register<ZPackage>(ReplyName, Receive);
                    replying = true;
                }
                catch (ArgumentException)
                {
                    // The same plugin twice: its first copy gets the answers (and says the hello).
                }
            }
            long server = ServerId();
            if (!replying || server == 0L || Player.m_localPlayer == null)
            {
                return;
            }
            float now = Clock();
            Turn turn = Next(server == saidTo, now, saidAt, lookedAt);
            if (turn == Turn.Wait)
            {
                return;
            }
            lookedAt = now;
            List<KeyValuePair<string, string>> says = Gather();
            if (turn == Turn.Look && Same(says, said))
            {
                return;
            }
            Sent(server, now, says);
            rpc.InvokeRoutedRPC(server, HelloName, Hello(ReplyName, Sender(), says));
        }

        /// <summary>A hello said to <paramref name="server"/> at <paramref name="now"/> (from Tick, or the harness): what it
        /// said is what this copy last said (Said).</summary>
        internal static void Sent(long server, float now, List<KeyValuePair<string, string>> says)
        {
            saidAt = now;
            lookedAt = now;
            if (server != saidTo || float.IsNegativeInfinity(firstSaidAt))
            {
                firstSaidAt = now;
            }
            saidTo = server;
            said = says;
        }

        /// <summary>What this copy said for a key in its last hello on this connection; null before any, or for a key it
        /// didn't say. The server pack's answer is to that (ServerReach clamps Far Fetched's grant to the "chests.reach" it
        /// said).</summary>
        internal static string Said(string key)
        {
            if (said == null || key == null)
            {
                return null;
            }
            foreach (KeyValuePair<string, string> each in said)
            {
                if (each.Key == key)
                {
                    return each.Value;
                }
            }
            return null;
        }

        /// <summary>The hello's package (the wire format, protocol 1): the protocol, where to reply, who asks, then the
        /// count of pairs and each key and value.</summary>
        internal static ZPackage Hello(string replyName, string from, IList<KeyValuePair<string, string>> says)
        {
            var package = new ZPackage();
            package.Write(Protocol);
            package.Write(replyName ?? string.Empty);
            package.Write(from ?? string.Empty);
            package.Write(says?.Count ?? 0);
            if (says != null)
            {
                foreach (KeyValuePair<string, string> each in says)
                {
                    package.Write(each.Key ?? string.Empty);
                    package.Write(each.Value ?? string.Empty);
                }
            }
            return package;
        }

        /// <summary>
        /// Reads an answer (the wire format, protocol 1): the protocol, the server pack's name and version, the count of
        /// modules, then for each its id, version, whether it's on, the count of its pairs and each key and value. Null for
        /// a protocol below 1 (nothing this copy can read); an answer of a newer protocol is read for what protocol 1 has,
        /// and whatever follows is left unread. Throws on a package that ends too soon or holds a count past MostEntries.
        /// </summary>
        internal static Answer Read(ZPackage package)
        {
            int protocol = package.ReadInt();
            if (protocol < 1)
            {
                return null;
            }
            var read = new Answer { Protocol = protocol, ServerPack = package.ReadString() };
            int modules = Count(package.ReadInt());
            for (int i = 0; i < modules; i++)
            {
                var module = new ModuleAnswer { Id = package.ReadString(), Version = package.ReadString(), On = package.ReadBool() };
                int pairs = Count(package.ReadInt());
                for (int k = 0; k < pairs; k++)
                {
                    string key = package.ReadString();
                    module.Values[key] = package.ReadString();   // a key twice keeps its last value
                }
                read.Modules.Add(module);
            }
            return read;
        }

        /// <summary>
        /// The server pack's answer, by the reply name (registered on each connection), or from the harness: only the
        /// server's counts; read whole before anything changes, so a bad package keeps the last good answer.
        /// </summary>
        internal static void Receive(long from, ZPackage package)
        {
            long server = ServerId();
            if (server == 0L || from != server || package == null)
            {
                return;   // only the server's word counts
            }
            Answer read;
            try
            {
                read = Read(package);
            }
            catch (Exception error)
            {
                if (!toldBad)
                {
                    toldBad = true;
                    Warning($"[QoL Mods chest service] The server pack's answer couldn't be read, so it's ignored: {error.Message}. " +
                            "Said once a connection.");
                }
                return;
            }
            if (read == null)
            {
                return;   // a protocol below 1: nothing this copy can read
            }
            answer = read;
            answeredBy = from;
            answeredAt = Clock();
            if (!toldRuns)
            {
                toldRuns = true;
                Info($"[QoL Mods chest service] The server runs {read.Describe()}. Said once a connection.");
            }
        }

        /// <summary>A new connection (or the harness starting afresh): everything known of the last one forgotten.</summary>
        internal static void NewConnection(object connection)
        {
            connectedTo = connection;
            replying = false;
            answer = null;
            answeredBy = 0L;
            answeredAt = float.NegativeInfinity;
            saidAt = float.NegativeInfinity;
            firstSaidAt = float.NegativeInfinity;
            lookedAt = float.NegativeInfinity;
            saidTo = 0L;
            said = null;
            toldRuns = false;
            toldBad = false;
        }

        // A count as the package says it, refused past MostEntries or below 0.
        private static int Count(int count)
        {
            if (count < 0 || count > MostEntries)
            {
                throw new FormatException($"a count of {count}");
            }
            return count;
        }

        /// <summary>What the modules say now, in the order they first said it; a value that throws is left out.</summary>
        internal static List<KeyValuePair<string, string>> Gather()
        {
            var says = new List<KeyValuePair<string, string>>(Sayings.Count);
            foreach (KeyValuePair<string, Func<string>> each in Sayings)
            {
                string value;
                try
                {
                    value = each.Value() ?? string.Empty;
                }
                catch (Exception)
                {
                    continue;   // a module that can't say it now (not started yet, or failed) says nothing this time
                }
                says.Add(new KeyValuePair<string, string>(each.Key, value));
            }
            return says;
        }

        /// <summary>Whether two hellos say the same, pair by pair in order.</summary>
        internal static bool Same(List<KeyValuePair<string, string>> a, List<KeyValuePair<string, string>> b)
        {
            if (a == null || b == null || a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Key != b[i].Key || a[i].Value != b[i].Value)
                {
                    return false;
                }
            }
            return true;
        }

        // The plugin carrying this copy, by its name and version, for the server's log: found once among the plugins
        // BepInEx started, else the assembly's own name and version.
        private static string Sender()
        {
            if (sender != null)
            {
                return sender;
            }
            try
            {
                foreach (PluginInfo info in Chainloader.PluginInfos.Values)
                {
                    if (info?.Instance != null && info.Instance.GetType().Assembly == typeof(ServerPack).Assembly && info.Metadata != null)
                    {
                        sender = $"{info.Metadata.Name} {info.Metadata.Version}";
                        return sender;
                    }
                }
            }
            catch (Exception)
            {
                // The assembly's own name below.
            }
            System.Reflection.AssemblyName own = typeof(ServerPack).Assembly.GetName();
            sender = $"{own.Name} {own.Version}";
            return sender;
        }

        private static bool FromThisServer() =>
            answeredBy != 0L && ServerId() == answeredBy && connectedTo != null && connectedTo == Connection();

        /// <summary>One answer of the server pack, as read.</summary>
        internal sealed class Answer
        {
            internal int Protocol;
            internal string ServerPack;
            internal readonly List<ModuleAnswer> Modules = new List<ModuleAnswer>();

            /// <summary>A module by its id (the first, if the answer names it twice), or null.</summary>
            internal ModuleAnswer Find(string id)
            {
                if (id == null)
                {
                    return null;
                }
                foreach (ModuleAnswer module in Modules)
                {
                    if (module.Id == id)
                    {
                        return module;
                    }
                }
                return null;
            }

            /// <summary>"miikeskii's Server-Side QoL Pack 0.1.0: FarFetched 0.2.0, TopperUpperServer 0.1.0 (off)".</summary>
            internal string Describe()
            {
                var parts = new List<string>();
                foreach (ModuleAnswer module in Modules)
                {
                    parts.Add($"{module.Id} {module.Version}" + (module.On ? string.Empty : " (off)"));
                }
                return $"{ServerPack}: " + (parts.Count > 0 ? string.Join(", ", parts) : "no modules");
            }
        }

        /// <summary>One module in an answer: its id, version, whether it's on, and its own key and value pairs.</summary>
        internal sealed class ModuleAnswer
        {
            internal string Id;
            internal string Version;
            internal bool On;
            internal readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.Ordinal);

            internal string Value(string key) => key != null && Values.TryGetValue(key, out string value) ? value : null;
        }
    }
}
