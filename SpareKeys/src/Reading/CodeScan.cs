using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx;
using BepInEx.Bootstrap;

namespace SpareKeys
{
    /// <summary>
    /// "Look inside mods' code": reads every loaded mod's assembly once per game session, on a thread of its own so the
    /// game never waits (about half a second on his 23 mods), and keeps what it found in memory (nothing is written). The
    /// pack's own assemblies are skipped: their keys are settings the map reads anyway. Started at the first frame (when
    /// every plugin has started) unless the setting is Off, or later when it is turned on.
    /// </summary>
    internal static class CodeScan
    {
        private sealed class Plugin
        {
            internal string Path;
            internal string Id;
            internal string Name;
        }

        private sealed class Found
        {
            internal Plugin Plugin;
            internal CodeHit Hit;
        }

        /// <summary>The largest assembly read (a mod's code is a few MB at most; one carrying its assets can be far more).</summary>
        private const long MostBytes = 20000000;

        private static Thread thread;
        private static volatile bool done;
        private static List<Found> results = new List<Found>();
        private static string summary;
        private static bool summarised;

        internal static bool Started => thread != null;

        /// <summary>Starts the scan once; the list of plugins is taken here, on the game's thread.</summary>
        internal static void StartOnce()
        {
            if (thread != null)
            {
                return;
            }
            var plugins = new List<Plugin>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PluginInfo info in Chainloader.PluginInfos.Values)
            {
                string id = info?.Metadata?.GUID;
                if (id == null || id.StartsWith("modprojects.", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(info.Location) ||
                    !paths.Add(info.Location))
                {
                    continue;
                }
                plugins.Add(new Plugin { Path = info.Location, Id = id, Name = info.Metadata.Name });
            }
            thread = new Thread(() => Work(plugins)) { IsBackground = true, Name = "Spare Keys: reading mods' code", Priority = ThreadPriority.BelowNormal };
            thread.Start();
        }

        // On the scan's own thread: never touches Unity or the game, only the files. One assembly that can't be read is
        // skipped and named; nothing it finds reaches the game's thread until all is done.
        private static void Work(List<Plugin> plugins)
        {
            var found = new List<Found>();
            var failed = new List<string>();
            var clock = Stopwatch.StartNew();
            foreach (Plugin plugin in plugins)
            {
                try
                {
                    var file = new FileInfo(plugin.Path);
                    if (!file.Exists)
                    {
                        continue;
                    }
                    if (file.Length > MostBytes)
                    {
                        failed.Add($"{plugin.Name} (over {MostBytes / 1000000} MB, left unread)");
                        continue;   // read whole into memory: a DLL full of assets would make a spike, then a pause
                    }
                    foreach (CodeHit hit in CodeReader.Scan(plugin.Path))
                    {
                        found.Add(new Found { Plugin = plugin, Hit = hit });
                    }
                }
                catch (Exception error)
                {
                    failed.Add($"{plugin.Name} ({error.GetType().Name})");
                }
            }
            int keys = found.Select(each => each.Plugin.Id + (int)each.Hit.Key).Distinct().Count();
            summary = $"Read {plugins.Count} mods' code in {clock.ElapsedMilliseconds} ms: {keys} fixed key reads found" +
                      (failed.Count > 0 ? $"; couldn't read {string.Join(", ", failed)}." : ".");
            results = found;
            done = true;
        }

        /// <summary>What the scan found, as maybe-used keys; none until it is done.</summary>
        internal static IEnumerable<Use> Uses()
        {
            if (!done)
            {
                yield break;
            }
            foreach (Found each in results)
            {
                Use use = ModKeys.FromCode(each.Plugin.Id, each.Plugin.Name, each.Hit.Key, each.Hit.Held, each.Hit.Where);
                if (use != null)
                {
                    yield return use;
                }
            }
        }

        /// <summary>On the game's thread, once the scan is done: one log line, and the map is worked out again.</summary>
        internal static void Tick()
        {
            if (!done || summarised)
            {
                return;
            }
            summarised = true;
            Module.Log?.LogInfo(summary);
            Collector.MarkDirty();
        }
    }
}
