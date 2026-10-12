// What the QoL Pack does where its own game serves the world, a part: the one copy to edit is
// Games\Valheim\QoLMods\parts\Serving\. Every mod that uses it carries a copy in its own parts\Serving\ folder, kept
// identical by QoLMods\tools\sync-kit.ps1, so each mod still builds and publishes on its own. The rules for a part are in
// QoLMods\MODULES.md. A mod that carries it carries the chest service too (parts\Chests\), whose ServerPack is the other
// side of the handshake.
using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx;
using BepInEx.Bootstrap;
using QoLMods.Chests;
using UnityEngine;

namespace QoLMods.Serving
{
    /// <summary>
    /// The server pack's side of the handshake (protocol 1), answered by the QoL Pack itself in a world its own game serves
    /// (alone, or the game's Start Server) while no plugin of the server pack's kind is in this game (card B29): friends
    /// who join with the pack say hello to the host's game as to any server, and hear what the host's Nap Time and World
    /// Settings serve, under the server modules' ids ("NapTimeServer", "WorldSettingsServer"), so their beds and skies
    /// follow the host's exactly as they would follow the server pack's. The wire format and its limits are the server
    /// kit's (ServerSideQoLPack\kit\Handshake.cs; MODULES.md there): a hello is int protocol, string reply name, string
    /// sender, int count, count × (key, value); the answer, by the hello's reply name, is int protocol (1), string the
    /// plugin's name, int modules, then per module string id, string version, bool on, int count, count × (key, value).
    /// What a hello says is read and kept only to answer it: nothing here serves a game by what it says.
    ///
    /// Each hello is remembered by game and reply name (at most MostNamesPerGame names a game), forgotten HelloFor seconds
    /// after it came or when its game leaves. When a module's answer changes (its switch, or Changed), every live hello is
    /// answered again, at most once every AnswerAgainEvery seconds. Registered on each new world's router, only where this
    /// game serves the world and no plugin of the server pack's kind is here (ServerHalves); two plugins carrying this part
    /// (two of the pack's mods installed on their own) each try, and only the first answers, for its own modules.
    /// </summary>
    internal static class HostPack
    {
        /// <summary>How long a hello counts, in seconds: a pack says one every ten.</summary>
        internal const float HelloFor = 90f;

        /// <summary>The most reply names remembered for one game: a game sending more loses its oldest.</summary>
        internal const int MostNamesPerGame = 8;

        /// <summary>The least time between two rounds of answering every live hello again, in seconds.</summary>
        internal const float AnswerAgainEvery = 1f;

        /// <summary>The most key and value pairs one hello may carry; a hello with more is not read.</summary>
        internal const int MostSaid = 64;

        /// <summary>The start every reply name must have, and its longest length.</summary>
        internal const string ReplyPrefix = "modprojects.";

        internal const int LongestReplyName = 100;

        /// <summary>How often the hellos too old, and the games that left, are forgotten, in seconds.</summary>
        internal const float PruneEvery = 1f;

        /// <summary>The log's two kinds of line. Seams for the scratch harness.</summary>
        internal static Action<string> Info = text => ZLog.Log(text);
        internal static Action<string> Warning = text => ZLog.LogWarning(text);

        private static readonly List<Served> Modules = new List<Served>();
        private static readonly Dictionary<long, Guest> Known = new Dictionary<long, Guest>();
        private static readonly List<long> forget = new List<long>();

        private static object registeredOn;
        private static bool answering;
        private static bool changePending;
        private static float answeredAllAt = float.NegativeInfinity;
        private static float prunedAt = float.NegativeInfinity;
        private static int tickedFrame = -1;

        // This frame's world, for StillHere (made once, so a frame's turn allocates nothing).
        private static long tickSelf;
        private static ZNet tickNet;
        private static readonly Func<long, bool> StillHere = game => game == tickSelf || (tickNet != null && tickNet.GetPeer(game) != null);
        private static bool toldTaken;
        private static bool toldStandsAside;
        private static bool toldUnreadable;
        private static bool toldError;
        private static string name;

        /// <summary>
        /// A module this copy answers for, from its Resolve: its id in the answer (the server module's whose place it takes),
        /// its version (the QoL module's own: no server module of that version exists, and the answer's plugin name says
        /// it comes from the QoL Pack in the host's game), whether it serves now (<paramref name="on"/>), and its pairs (<paramref name="pairs"/>, given an add:
        /// numbers through Number). A second call with the same id replaces the first.
        /// </summary>
        internal static void Serve(string id, string version, Func<bool> on, Action<Action<string, string>> pairs, string displayName = null)
        {
            if (string.IsNullOrEmpty(id) || on == null || pairs == null)
            {
                return;
            }
            var served = new Served { Id = id, Version = version ?? string.Empty, On = on, Pairs = pairs, DisplayName = displayName ?? id };
            for (int i = 0; i < Modules.Count; i++)
            {
                if (Modules[i].Id == id)
                {
                    Modules[i] = served;
                    return;
                }
            }
            Modules.Add(served);
        }

        /// <summary>A module's answer changed (a setting it answers with): every live hello is answered again soon. A
        /// switch turning needs no call: Tick follows each module's On.</summary>
        internal static void Changed() => changePending = true;

        /// <summary>A number as the server kit writes one in its answer: "R", in the invariant culture.</summary>
        internal static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Whether this copy answers the packs' hello on the world served now (registered on this connection).
        /// False on a game that only joins a server, with a plugin of the server pack's kind here, when another copy of
        /// this part registered first, and on a new world's first frame until Tick registers.</summary>
        internal static bool AnswersHere => answering && registeredOn != null && ReferenceEquals(registeredOn, ServerPack.Connection());

        /// <summary>
        /// Every frame, from each using module's Update (also while it is switched off; its work is done once a frame):
        /// registers on a new world's router, forgets old hellos and games that left, and answers every live hello again
        /// when a module's answer changed. Never throws.
        /// </summary>
        internal static void Tick()
        {
            if (tickedFrame == Time.frameCount)
            {
                return;
            }
            tickedFrame = Time.frameCount;
            try
            {
                ZRoutedRpc router = ZRoutedRpc.instance;
                ZNet net = ZNet.instance;
                if (router == null || net == null)
                {
                    return;
                }
                if (!ReferenceEquals(router, registeredOn))
                {
                    Register(router, net);
                }
                bool canPrune = ZDOMan.instance != null;
                tickSelf = canPrune ? ZDOMan.GetSessionID() : 0L;
                tickNet = net;
                if (Turn(Time.realtimeSinceStartup, canPrune, StillHere))
                {
                    AnswerAll(router);
                }
            }
            catch (Exception error)
            {
                TellOnce(ref toldError, $"[Your own world] Answering friends' games hit an error, and goes on: {error}");
            }
        }

        // ---- The decisions, on values given (the scratch harness drives these) ----

        /// <summary>One frame's turn at <paramref name="now"/>: once a second (when <paramref name="canPrune"/>) forgets
        /// the hellos too old and the games that left; then, only while answering, follows the modules' switches and says
        /// whether every live hello is answered again.</summary>
        internal static bool Turn(float now, bool canPrune, Func<long, bool> stillHere)
        {
            if (canPrune && now - prunedAt >= PruneEvery)
            {
                prunedAt = now;
                Prune(now, stillHere);
            }
            if (!answering)
            {
                return false;
            }
            FollowSwitches();
            if (!changePending || now - answeredAllAt < AnswerAgainEvery)
            {
                return false;
            }
            changePending = false;
            answeredAllAt = now;
            return true;
        }

        /// <summary>
        /// One hello from <paramref name="sender"/>, at <paramref name="now"/>: the answer to send by its reply name
        /// (<paramref name="replyName"/>), or null when it is ignored (<paramref name="problem"/> says why): from no game of
        /// this world (<paramref name="fromHere"/> false), too short, a protocol below 1, more than MostSaid pairs, or a
        /// reply name that isn't the project's own. A newer protocol is read as protocol 1 (its first fields).
        /// <paramref name="first"/> is true for a game not known before (its log line); <paramref name="from"/> is what the
        /// hello says it is.
        /// </summary>
        internal static ZPackage Received(long sender, ZPackage package, float now, bool fromHere, out string replyName, out string from,
                                          out string problem, out bool first)
        {
            replyName = null;
            from = null;
            first = false;
            if (!fromHere)
            {
                problem = "it came from no game in this world";
                return null;
            }
            if (!Read(package, out replyName, out from, out problem))
            {
                return null;
            }
            first = !Known.ContainsKey(sender);
            Remember(sender, replyName, from, now);
            return AnswerFor();
        }

        /// <summary>Reads a hello (protocol 1's fields): false, with <paramref name="problem"/>, when it can't be answered.</summary>
        internal static bool Read(ZPackage package, out string replyName, out string from, out string problem)
        {
            replyName = null;
            from = null;
            problem = null;
            try
            {
                int protocol = package.ReadInt();
                if (protocol < 1)
                {
                    problem = $"its protocol is {protocol}";
                    return false;
                }
                replyName = package.ReadString();
                from = package.ReadString() ?? string.Empty;
                int count = package.ReadInt();
                if (count < 0 || count > MostSaid)
                {
                    problem = $"it says it carries {count} pairs";
                    return false;
                }
                for (int i = 0; i < count; i++)
                {
                    package.ReadString();
                    package.ReadString();
                }
            }
            catch (Exception error)
            {
                problem = $"it couldn't be read ({error.GetType().Name})";
                return false;
            }
            if (!ReplyNameOk(replyName))
            {
                problem = "its reply name isn't one of the project's own";
                return false;
            }
            return true;
        }

        /// <summary>Whether an answer may be sent by this name: the project's own, not too long, and never the hello's own
        /// name.</summary>
        internal static bool ReplyNameOk(string replyName) =>
            replyName != null && replyName.Length <= LongestReplyName && replyName.StartsWith(ReplyPrefix, StringComparison.Ordinal) &&
            replyName != ServerPack.HelloName;

        /// <summary>The answer (see the class): protocol 1, this plugin's name, and every module this copy answers for, each
        /// with its id, version, whether it serves now, and its pairs while it does. A module whose On or pairs throw says
        /// off, with nothing more.</summary>
        internal static ZPackage AnswerFor()
        {
            var package = new ZPackage();
            package.Write(ServerPack.Protocol);
            package.Write(Name);
            package.Write(Modules.Count);
            foreach (Served module in Modules)
            {
                bool on;
                var pairs = new List<KeyValuePair<string, string>>();
                try
                {
                    on = module.On();
                    if (on)
                    {
                        module.Pairs((key, value) => Add(pairs, key, value));
                    }
                }
                catch (Exception error)
                {
                    on = false;
                    pairs.Clear();
                    Failed(module, error);
                }
                package.Write(module.Id);
                package.Write(module.Version);
                package.Write(on);
                package.Write(pairs.Count);
                foreach (KeyValuePair<string, string> pair in pairs)
                {
                    package.Write(pair.Key);
                    package.Write(pair.Value);
                }
            }
            return package;
        }

        /// <summary>Forgets the hellos older than HelloFor at <paramref name="now"/>, and every game for which
        /// <paramref name="stillHere"/> says no (it left the world).</summary>
        internal static void Prune(float now, Func<long, bool> stillHere)
        {
            forget.Clear();
            foreach (KeyValuePair<long, Guest> each in Known)
            {
                each.Value.Hellos.RemoveAll(hello => now - hello.At > HelloFor);
                if (each.Value.Hellos.Count == 0 || (stillHere != null && !stillHere(each.Key)))
                {
                    forget.Add(each.Key);
                }
            }
            foreach (long game in forget)
            {
                Known.Remove(game);
            }
        }

        /// <summary>Notes each module whose On turned since the last look as a change: the packs learn at once.</summary>
        internal static void FollowSwitches()
        {
            foreach (Served module in Modules)
            {
                bool on;
                try
                {
                    on = module.On();
                }
                catch (Exception error)
                {
                    on = false;
                    Failed(module, error);
                }
                if (on != module.LastOn)
                {
                    module.LastOn = on;
                    changePending = true;
                }
            }
        }

        /// <summary>Every game forgotten, and any change waiting (as on a new world's router; for the harness).</summary>
        internal static void Forget()
        {
            Known.Clear();
            changePending = false;
        }

        /// <summary>The games known now and their reply names, for the harness.</summary>
        internal static int GamesKnown => Known.Count;

        internal static IList<string> ReplyNamesOf(long game) =>
            Known.TryGetValue(game, out Guest known) ? known.Hellos.ConvertAll(hello => hello.ReplyName) : new List<string>();

        /// <summary>Whether this copy answers (as Register leaves it), set by the harness without a router.</summary>
        internal static void AnsweringForHarness(bool on) => answering = on;

        // ---- The game's side ----

        private static void Register(ZRoutedRpc router, ZNet net)
        {
            Known.Clear();
            changePending = false;
            registeredOn = router;
            answering = false;
            if (!net.IsServer() || !ServerHalves.OwnWorld)
            {
                return;   // a game that only joins a server (its server answers, if anything does), or a dedicated server
            }
            if (ServerHalves.KitHere)
            {
                TellOnce(ref toldStandsAside, $"[Your own world] {ServerHalves.Names} {ServerHalves.Are} in this game and answer" +
                                              $"{(ServerHalves.Are == "is" ? "s" : string.Empty)} friends' games in the worlds it runs; Nap Time " +
                                              "and World Settings serve nothing there themselves (the line under each one's Enabled switch in " +
                                              "the settings window says what applies). Said once.",
                         warning: false);
                return;
            }
            try
            {
                router.Register<ZPackage>(ServerPack.HelloName, OnHello);
                answering = true;
                // Every module's switch as it stands now, so the first turn doesn't answer again for nothing.
                FollowSwitches();
                changePending = false;
            }
            catch (ArgumentException)
            {
                TellOnce(ref toldTaken, $"[Your own world] {Name}: another plugin already answers friends' games in the worlds this " +
                                        "game runs (another of miikeskii's QoL Pack's mods installed on its own), so this one's naps or day " +
                                        "don't reach them. Install miikeskii's QoL Pack in place of the separate mods. Said once.");
            }
        }

        private static void OnHello(long sender, ZPackage package)
        {
            try
            {
                // The game passes on whatever session ID a routed message names as its sender: only this world's own game
                // and the games connected to it count.
                long self = ZDOMan.instance != null ? ZDOMan.GetSessionID() : 0L;
                bool fromHere = sender == self || (ZNet.instance != null && ZNet.instance.GetPeer(sender) != null);
                ZPackage answer = Received(sender, package, Time.realtimeSinceStartup, fromHere, out string replyName, out string from,
                                           out string problem, out bool first);
                if (answer == null)
                {
                    TellOnce(ref toldUnreadable, $"[Your own world] A hello from {Who(sender)} was ignored: {problem}. Said once.");
                    return;
                }
                Send(sender, replyName, answer);
                if (first)
                {
                    Info($"[Your own world] {Who(sender)} runs {Cut(from)}: answered with {Listed()}.");
                }
            }
            catch (Exception error)
            {
                TellOnce(ref toldError, $"[Your own world] Answering friends' games hit an error, and goes on: {error}");
            }
        }

        // Every live hello answered again, each from one answer. From a list made first: a send to this game's own session
        // is handled inside the send.
        private static void AnswerAll(ZRoutedRpc router)
        {
            var due = new List<KeyValuePair<long, string>>();
            foreach (KeyValuePair<long, Guest> game in Known)
            {
                foreach (Hello hello in game.Value.Hellos)
                {
                    due.Add(new KeyValuePair<long, string>(game.Key, hello.ReplyName));
                }
            }
            if (due.Count == 0)
            {
                return;
            }
            ZPackage answer = AnswerFor();
            foreach (KeyValuePair<long, string> each in due)
            {
                Send(each.Key, each.Value, answer);
            }
        }

        private static void Send(long game, string replyName, ZPackage answer)
        {
            ZRoutedRpc router = ZRoutedRpc.instance;
            if (ReplyNameOk(replyName) && router != null)
            {
                router.InvokeRoutedRPC(game, replyName, answer);
            }
        }

        // Keeps a hello under its game and reply name; a game with MostNamesPerGame names already loses its oldest.
        private static void Remember(long sender, string replyName, string from, float now)
        {
            if (!Known.TryGetValue(sender, out Guest known))
            {
                Known[sender] = known = new Guest();
            }
            int same = known.Hellos.FindIndex(hello => hello.ReplyName == replyName);
            if (same >= 0)
            {
                known.Hellos.RemoveAt(same);
            }
            else if (known.Hellos.Count >= MostNamesPerGame)
            {
                known.Hellos.RemoveAt(0);   // the oldest: the list is kept oldest first
            }
            known.Hellos.Add(new Hello { ReplyName = replyName, At = now });
            known.Sender = from ?? string.Empty;
        }

        // A module's On or pairs threw: it answers off; said once per module.
        private static void Failed(Served module, Exception error)
        {
            if (module.ToldFailed)
            {
                return;
            }
            module.ToldFailed = true;
            Warning($"[Your own world] {module.DisplayName} (answering friends' games as {module.Id}) couldn't say what it serves, so they " +
                    $"hear it as off: {error.GetType().Name}: {error.Message}. Said once.");
        }

        private static void Add(List<KeyValuePair<string, string>> pairs, string key, string value)
        {
            if (key == null)
            {
                return;
            }
            var pair = new KeyValuePair<string, string>(key, value ?? string.Empty);
            int at = pairs.FindIndex(each => each.Key == key);
            if (at >= 0)
            {
                pairs[at] = pair;
            }
            else
            {
                pairs.Add(pair);
            }
        }

        /// <summary>This plugin's name and version, as the answer gives it ("miikeskii's QoL Pack 0.56.0"), found once
        /// among the plugins BepInEx started, else the assembly's own; then "in the host's game".</summary>
        internal static string Name
        {
            get
            {
                if (name != null)
                {
                    return name;
                }
                string plugin = null;
                try
                {
                    foreach (PluginInfo info in Chainloader.PluginInfos.Values)
                    {
                        if (info?.Instance != null && info.Instance.GetType().Assembly == typeof(HostPack).Assembly && info.Metadata != null)
                        {
                            plugin = $"{info.Metadata.Name} {info.Metadata.Version}";
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    // The assembly's own name below.
                }
                if (plugin == null)
                {
                    System.Reflection.AssemblyName own = typeof(HostPack).Assembly.GetName();
                    plugin = $"{own.Name} {own.Version}";
                }
                name = plugin + " in the host's game";
                return name;
            }
        }

        // The modules for the log: "NapTimeServer 0.2.0 (on), …".
        private static string Listed()
        {
            var names = new List<string>();
            foreach (Served module in Modules)
            {
                bool on;
                try
                {
                    on = module.On();
                }
                catch (Exception)
                {
                    on = false;
                }
                names.Add($"{module.Id} {module.Version} ({(on ? "on" : "off")})");
            }
            return names.Count > 0 ? string.Join(", ", names.ToArray()) : "no modules";
        }

        private static string Who(long game)
        {
            ZNetPeer peer = ZNet.instance != null ? ZNet.instance.GetPeer(game) : null;
            return peer != null && !string.IsNullOrEmpty(peer.m_playerName) ? $"{peer.m_playerName} ({game})" : $"game {game}";
        }

        /// <summary>A text a game sent, cut for the log to at most <paramref name="most"/> characters.</summary>
        internal static string Cut(string text, int most = 80)
        {
            if (text == null)
            {
                return string.Empty;
            }
            return text.Length <= most ? text : text.Substring(0, most) + "...";
        }

        private static void TellOnce(ref bool told, string message, bool warning = true)
        {
            if (told)
            {
                return;
            }
            told = true;
            (warning ? Warning : Info)(message);
        }

        private sealed class Served
        {
            internal string Id;
            internal string Version;
            internal Func<bool> On;
            internal Action<Action<string, string>> Pairs;
            internal bool LastOn;
            internal bool ToldFailed;
            internal string DisplayName;
        }

        private sealed class Guest
        {
            internal readonly List<Hello> Hellos = new List<Hello>();
            internal string Sender = string.Empty;
        }

        private sealed class Hello
        {
            internal string ReplyName;
            internal float At;
        }
    }
}
