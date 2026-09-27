// The QoL Mods kit. The one copy to edit is Games\Valheim\QoLMods\kit\; every mod folder carries a copy of it in its
// own kit\ folder, so each mod still builds and publishes on its own. After changing it here, run
// QoLMods\tools\sync-kit.ps1. The rules for a module are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace QoLMods.Kit
{
    /// <summary>
    /// Runs one module, inside the bundle or as its own plugin: binds its Enabled switch and settings, starts it, patches
    /// the game with the module's own Harmony patch classes only, calls it every frame, and keeps its errors to itself.
    /// The first error switches that module off for the rest of the session, takes back what it shows and removes its
    /// patches (unless the module keeps them, KeepPatchesAfterError); the game, the other modules and other mods carry on.
    /// </summary>
    internal sealed class ModuleHost
    {
        private readonly Harmony harmony;
        private readonly ModuleSettings settings;
        private readonly bool inBundle;

        // Done at the first Update, when every plugin has started.
        private bool checkedForOlderSingle;

        // After an error, the patches come off at the next Update rather than from inside a patched method.
        private bool unpatchPending;

        private ModuleHost(QoLModule module, ConfigFile config, Func<string, string> sectionOf, string harmonyId, ManualLogSource log, bool inBundle)
        {
            Module = module;
            Log = log;
            harmony = new Harmony(harmonyId);
            settings = new ModuleSettings(config, sectionOf);
            this.inBundle = inBundle;
            module.Host = this;
        }

        internal QoLModule Module { get; }

        internal ManualLogSource Log { get; }

        /// <summary>The module's own on/off switch, first in its settings.</summary>
        internal ConfigEntry<bool> Enabled => settings.Enabled;

        /// <summary>Started and patched. False if it could not start, and for the rest of the session after an error.</summary>
        internal bool Active { get; private set; }

        /// <summary>Active and switched on: what a patch checks before changing anything.</summary>
        internal bool IsOn => Active && Enabled != null && Enabled.Value;

        /// <summary>
        /// Runs a module as its own plugin, its single release: its sections are its parts ("General", "Map" …) in the
        /// plugin's own settings file, and its Harmony ID is the plugin's ID, as before the bundle existed. When the
        /// bundle is installed and holds this module, it stands aside instead, says so in the log and returns null.
        /// Call it from the plugin's Awake; the plugin's own dependencies make sure the bundle has started first.
        /// </summary>
        internal static ModuleHost RunAlone(BaseUnityPlugin plugin, QoLModule module, ManualLogSource log)
        {
            if (BundleHolds(module, out string bundleVersion, out string moduleVersion))
            {
                log.LogWarning($"{Bundle.Name} {bundleVersion} is installed and runs {module.DisplayName} {moduleVersion} itself, so this separate " +
                               $"copy ({module.ModuleVersion}) stands aside and does nothing. Its settings are in the bundle's " +
                               $"\"{module.DisplayName}\" section. This copy can be removed.");
                return null;
            }
            var host = new ModuleHost(module, plugin.Config, part => part, plugin.Info.Metadata.GUID, log, inBundle: false);
            host.Start(importFrom: null);
            return host;
        }

        /// <summary>
        /// Runs a module inside the bundle: its settings go in the bundle's file, in a section named after it, and its
        /// patches are made under the Harmony ID "&lt;bundle ID&gt;.&lt;module id&gt;". The first time the bundle runs this
        /// module, it takes the module's settings over from its single release's file, which it only reads.
        /// </summary>
        internal static ModuleHost RunInBundle(BaseUnityPlugin bundle, QoLModule module)
        {
            string main = module.DisplayName;
            Func<string, string> sectionOf = module.OneSectionInBundle
                ? (Func<string, string>)(part => main)
                : part => part == ModuleSettings.General ? main : $"{main} - {part}";
            var host = new ModuleHost(module, bundle.Config, sectionOf, $"{Bundle.Guid}.{module.Id.ToLowerInvariant()}",
                                      Logger.CreateLogSource(module.DisplayName), inBundle: true);
            host.Start(importFrom: Path.Combine(Paths.ConfigPath, module.SingleGuid + ".cfg"));
            return host;
        }

        /// <summary>Runs one piece of the module's work so that an error in it never reaches the game or other mods:
        /// the first error switches the module off for the rest of the session instead. Does nothing once it is off.</summary>
        internal void Guard(string task, Action work)
        {
            if (!Active)
            {
                return;
            }
            try
            {
                work();
            }
            catch (Exception error)
            {
                SwitchOff(task, error);
            }
        }

        internal void Update()
        {
            if (unpatchPending)
            {
                unpatchPending = false;
                TryToUndo(harmony.UnpatchSelf);
            }
            if (!checkedForOlderSingle)
            {
                checkedForOlderSingle = true;
                if (inBundle && Active)
                {
                    StandAsideForOlderSingle();
                }
            }
            Guard(Module.UpdateTask, Module.Update);
        }

        internal void LateUpdate() => Guard(Module.LateUpdateTask, Module.LateUpdate);

        private void Start(string importFrom)
        {
            var missing = new List<string>();
            var missingOptional = new List<string>();
            try
            {
                string enabledText = Module.EnabledDescription;
                if (importFrom != null)
                {
                    enabledText = $"{Module.DisplayName} {Module.ModuleVersion}. {enabledText}";
                }
                settings.BindEnabled(enabledText);
                Module.BindSettings(settings);
                TakeSettingsOver(importFrom);
                Module.Resolve(missing, missingOptional);
            }
            catch (Exception error)
            {
                // The module's section is already in the file, so the next start won't take the settings over again:
                // take over at least those that were bound.
                TryToUndo(() => TakeSettingsOver(importFrom));
                Log.LogWarning($"Switched off: it could not start. {Module.SwitchedOffNote}\n{error}");
                return;
            }
            if (missing.Count > 0)
            {
                Log.LogWarning($"Switched off: {Module.DescribeMissing(missing)}");
                return;
            }

            try
            {
                PatchOwnClasses();
            }
            catch (Exception error)
            {
                TryToUndo(harmony.UnpatchSelf);
                Log.LogWarning($"Switched off: its patches could not be applied. {Module.SwitchedOffNote}\n{error}");
                return;
            }

            Active = true;
            Log.LogInfo(Module.ReadyMessage + (Enabled.Value ? string.Empty : " It is switched off in its settings (Enabled = false) until switched on."));
            if (missingOptional.Count > 0)
            {
                Log.LogWarning($"Running without {string.Join("; ", missingOptional)}.");
            }
        }

        // In the bundle, the first time it runs this module: its single release's settings, if that file exists.
        private void TakeSettingsOver(string importFrom)
        {
            if (importFrom == null || !settings.IsNewToFile || settingsTakenOver || !File.Exists(importFrom))
            {
                return;
            }
            settingsTakenOver = true;
            int copied = settings.CopyFrom(importFrom);
            Log.LogInfo($"Took {copied} settings over from {Path.GetFileName(importFrom)}, the settings file of {Module.DisplayName}'s " +
                        "separate release. That happens once; that file is left as it was.");
        }

        private bool settingsTakenOver;

        // In the bundle: a copy of this mod's single release that is older than SingleStandsAsideFrom doesn't know how to
        // stand aside, and it is running too. So the bundle's copy steps back instead, rather than change the game twice.
        // Checked at the first Update, when every plugin has started, whatever order they started in.
        private void StandAsideForOlderSingle()
        {
            System.Version from = Module.SingleStandsAsideFrom;
            if (from == null)
            {
                return;
            }
            if (!Chainloader.PluginInfos.TryGetValue(Module.SingleGuid, out PluginInfo single) || single.Instance == null ||
                single.Metadata.Version >= from)
            {
                return;
            }
            Active = false;
            Log.LogWarning($"{Module.DisplayName} {single.Metadata.Version} is also installed on its own. That copy is older than {from} and " +
                           "can't stand aside for the bundle, so the bundle's copy stays off and that one runs instead. Update that copy " +
                           $"to {from} or later, or remove it, and the bundle runs {Module.DisplayName} itself.");
            foreach (Action undo in Module.UndoSteps)
            {
                TryToUndo(undo);
            }
            TryToUndo(harmony.UnpatchSelf);
        }

        // Every Harmony patch class in the module's own namespace (and the namespaces under it), and nothing else: in the
        // bundle, other modules' patch classes sit in the same DLL.
        private void PatchOwnClasses()
        {
            string own = Module.GetType().Namespace;
            foreach (Type type in AccessTools.GetTypesFromAssembly(Module.GetType().Assembly))
            {
                string space = type.Namespace ?? string.Empty;
                if (space == own || space.StartsWith(own + ".", StringComparison.Ordinal))
                {
                    harmony.CreateClassProcessor(type).Patch();
                }
            }
        }

        private void SwitchOff(string task, Exception error)
        {
            Active = false;
            Log.LogWarning($"Switched off after an error while {task}. {Module.SwitchedOffNote}\n{error}");
            foreach (Action undo in Module.UndoSteps)
            {
                TryToUndo(undo);
            }
            // Every patch already does nothing now that the module is off, or only what it must still do (KeepPatchesAfterError).
            unpatchPending = !Module.KeepPatchesAfterError;
        }

        private void TryToUndo(Action undo)
        {
            try
            {
                undo();
            }
            catch (Exception error)
            {
                Log.LogDebug($"Undoing after that error failed as well: {error}");
            }
        }

        // Is the bundle installed, and does it hold this module? The bundle has already started if it is there: every
        // single release names it as a soft dependency. The bundle's own copy of the kit is a different class, so it is
        // asked through its plugin's public ModuleVersion(id) method.
        private static bool BundleHolds(QoLModule module, out string bundleVersion, out string moduleVersion)
        {
            bundleVersion = moduleVersion = null;
            if (!Chainloader.PluginInfos.TryGetValue(Bundle.Guid, out PluginInfo bundle) || bundle.Instance == null)
            {
                return false;
            }
            bundleVersion = bundle.Metadata.Version.ToString();
            MethodInfo ask = bundle.Instance.GetType().GetMethod("ModuleVersion", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(string) }, null);
            if (ask == null)
            {
                moduleVersion = "(its own version)";
                return true;   // can't ask, so don't risk patching the game twice
            }
            moduleVersion = ask.Invoke(bundle.Instance, new object[] { module.Id }) as string;
            return moduleVersion != null;
        }
    }
}
