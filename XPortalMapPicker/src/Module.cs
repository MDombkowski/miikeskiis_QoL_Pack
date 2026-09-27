using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;

namespace XPortalMapPicker
{
    /// <summary>
    /// XPortal Map Picker as a module of miikeskii's QoL Pack: a Map button beside XPortal's destination list that
    /// lets the player choose a portal's destination on the big map. Client-side only: the choice goes to the server
    /// through XPortal's own OK button, so neither the server nor other players need this mod. The same code runs in
    /// the bundle and in its single release (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.xportalmappicker";

        internal const string Name = "XPortal Map Picker";

        internal const string Version = "0.4.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "XPortalMapPicker";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off takes the Map button away, at once if a portal's panel is open, and ends " +
            "any picking; XPortal then works exactly as it does without this mod. On brings the button back.";

        internal override string SwitchedOffNote => "XPortal itself works as usual.";

        internal override string UpdateTask => "running the map picker";

        internal override string LateUpdateTask => "placing the portal markers";

        internal override string ReadyMessage =>
            $"{Name} {Version} is ready for XPortal {XPortalHooks.Version}: look for the Map button beside a portal's destination list.";

        internal override IEnumerable<Action> UndoSteps => new Action[] { PickMode.Abandon, MapButton.Remove };

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach XPortal or the game:
        /// the first error switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            XPortalHooks.Resolve(missing, missingOptional);
            GameHooks.Resolve(missing, missingOptional);
        }

        // 0.3.0 and older, released before the bundle, don't know how to stand aside for it.
        internal override System.Version SingleStandsAsideFrom => new System.Version(0, 4, 0);

        internal override string DescribeMissing(IList<string> missing) =>
            missing.Contains(XPortalHooks.NotInstalled)
                ? "XPortal isn't installed, and this mod works only with it."
                : $"XPortal {XPortalHooks.Version} or the game has changed, and these parts were not found: {string.Join(", ", missing)}. {SwitchedOffNote}";

        internal override void Update()
        {
            PickMode.Tick();
            MapButton.Tick();
        }

        // After every Update, so the map has already moved this frame when the markers are placed on it.
        internal override void LateUpdate()
        {
            if (PickMode.IsPicking)
            {
                PickMode.PlaceMarkers();
            }
        }
    }
}
