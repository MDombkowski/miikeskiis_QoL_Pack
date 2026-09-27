// The QoL Mods kit. The one copy to edit is Games\Valheim\QoLMods\kit\; every mod folder carries a copy of it in its
// own kit\ folder, so each mod still builds and publishes on its own. After changing it here, run
// QoLMods\tools\sync-kit.ps1. The rules for a module are in QoLMods\MODULES.md.
using System.Collections.Generic;

namespace QoLMods.Kit
{
    /// <summary>
    /// A mod of miikeskii's QoL Pack, written once and run two ways: inside the bundle, where it gets a section of the
    /// bundle's settings, or as its own plugin, its single release. A module never makes its own plugin, settings
    /// file or Harmony instance: its host (ModuleHost) gives it those, and runs it.
    /// </summary>
    internal abstract class QoLModule
    {
        /// <summary>The module's short, permanent name: its folder's name, such as "XPortalMapPicker". Inside the
        /// bundle it names the module's Harmony ID.</summary>
        internal abstract string Id { get; }

        /// <summary>The name players see, such as "XPortal Map Picker": its log lines, and its section of the
        /// bundle's settings.</summary>
        internal abstract string DisplayName { get; }

        internal abstract string ModuleVersion { get; }

        /// <summary>The plugin ID of its single release, which also names that release's settings file. The bundle
        /// takes the module's settings from that file once, the first time it runs the module.</summary>
        internal abstract string SingleGuid { get; }

        /// <summary>What the Enabled switch does for this module, in plain words. The host binds the switch.</summary>
        internal abstract string EnabledDescription { get; }

        /// <summary>Ends every "switched off" warning, such as "XPortal itself works as usual."</summary>
        internal virtual string SwitchedOffNote => string.Empty;

        /// <summary>Inside the bundle, all the module's settings share one section named after it. A large module
        /// returns false, and each of its parts then gets a section "&lt;DisplayName&gt; - &lt;part&gt;", with General
        /// staying "&lt;DisplayName&gt;".</summary>
        internal virtual bool OneSectionInBundle => true;

        /// <summary>What the module's every-frame work is called in an error message.</summary>
        internal virtual string UpdateTask => "doing its work each frame";

        internal virtual string LateUpdateTask => "doing its work after each frame";

        /// <summary>For a mod released on its own before it became a module: the first version of that single release
        /// which stands aside by itself when the bundle is installed (XPortalMapPicker: 0.4.0). With an older copy also
        /// installed, the bundle's copy stays off instead, so the game is never changed twice. Null for a mod that was a
        /// module from its first release.</summary>
        internal virtual System.Version SingleStandsAsideFrom => null;

        /// <summary>After an error has switched the module off, its host normally removes its patches at the next frame.
        /// A module returns true to keep them in place instead, because one of them must keep working even then (Trader
        /// Circles' catch for the server's answers, which would otherwise become pins). Every one of its patches must
        /// then be safe to leave in place while the module is off: doing nothing, or only what it must still do.</summary>
        internal virtual bool KeepPatchesAfterError => false;

        /// <summary>Its host, set before BindSettings is called.</summary>
        internal ModuleHost Host { get; set; }

        /// <summary>Binds every setting through the ModuleSettings given, never through a ConfigFile. The Enabled
        /// switch is already bound.</summary>
        internal abstract void BindSettings(ModuleSettings settings);

        /// <summary>Finds everything the module reaches for: other mods, the game's private members. Adds a plain name to
        /// <paramref name="missing"/> for each part it can't work without (the host then leaves it off, unpatched), and to
        /// <paramref name="missingOptional"/> for each part it can do without.</summary>
        internal abstract void Resolve(List<string> missing, List<string> missingOptional);

        /// <summary>The rest of the warning "Switched off: …" when Resolve found parts missing, ending with the
        /// SwitchedOffNote where it applies. A section name in the bundle is the DisplayName, so it avoids the characters
        /// BepInEx refuses there: = ' " [ ] \ and tabs.</summary>
        internal virtual string DescribeMissing(IList<string> missing) =>
            $"the game has changed, and these parts were not found: {string.Join(", ", missing)}. {SwitchedOffNote}";

        /// <summary>The log line once the module runs.</summary>
        internal virtual string ReadyMessage => $"{DisplayName} {ModuleVersion} is ready.";

        /// <summary>Runs every frame while the module is active, also while its Enabled switch is off, so it can
        /// follow the switch: off must take back at once everything it shows or changes.</summary>
        internal virtual void Update()
        {
        }

        internal virtual void LateUpdate()
        {
        }

        /// <summary>After an error has switched the module off: the steps that take back everything it shows or has
        /// changed. Each step runs even if the one before it fails.</summary>
        internal virtual IEnumerable<System.Action> UndoSteps => new System.Action[0];
    }
}
