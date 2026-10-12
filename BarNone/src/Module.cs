using System;
using System.Collections.Generic;
using BepInEx.Logging;
using QoLMods.Kit;
using QoLMods.ListPicker;

namespace BarNone
{
    /// <summary>
    /// Bar None as a module of miikeskii's QoL Pack: see-through pieces (cage walls and floors, crystal walls, dividers,
    /// roundpole fences …) that he can reach through, and that let smoke, projectiles and the sky through, and loose items
    /// fall through, each part with its own switch and its own list of pieces. Client-side only: every part runs on his own
    /// game. It replaces
    /// JereKuusela's Smoke_Collision and Projectile_Collision, written from scratch, and each of its parts stands aside while
    /// the matching one of those is installed. The same code runs in the bundle and in its single release
    /// (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.barnone";

        internal const string Name = "Bar None";

        internal const string Version = "0.3.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "BarNone";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off stops every part at once, and the game's own rules are back: nothing is reached " +
            "through a piece, no smoke, projectile or sky passes one, and no item falls through one (crops under a cage " +
            "floor then die when they ripen, as in the game). On brings back each part switched on below.";

        private static bool started;

        /// <summary>The end of every "switched off" warning, true for both of the kit's uses: after an error while playing
        /// (the patches stay, and the sky part keeps going), or when the module couldn't start at all (nothing runs).</summary>
        internal override string SwitchedOffNote => started
            ? "Reaching, smoke, projectiles and items follow the game's rules again; the sky part isn't affected, so crops under listed pieces still grow while it's on."
            : "Pieces block reach, smoke, projectiles, items and the sky as in the game: crops under listed pieces have \"no sun\" and die when they ripen.";

        /// <summary>The kit logs this once, the moment the module runs with its patches applied.</summary>
        internal override string ReadyMessage
        {
            get
            {
                started = true;
                return base.ReadyMessage;
            }
        }

        internal override string UpdateTask => "keeping its parts in step with their settings";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>A warning in the module's log, or nowhere while it has no host yet.</summary>
        internal static void Warn(string text) => current?.Host?.Log?.LogWarning(text);

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            GameHooks.Resolve(missing, missingOptional);
            Rivals.Check();
            Lists.Resolve(missingOptional);
        }

        /// <summary>Every frame while the module runs, also while switched off: the rivals looked for again once every plugin
        /// has started, the smoke's helpers in step with the smoke part's switch and list, the fires tested again after a
        /// change of their settings or list, the windmills measured again when their switch or the sky list changes, and
        /// the items' helpers in step with the items part's switch and lists.</summary>
        internal override void Update()
        {
            Rivals.CheckOnceAtFirstFrame();
            SmokePart.Follow();
            SkyPart.Follow();
            ItemPart.Follow();
        }

        /// <summary>After an error: the smoke's and the items' ignored pairs restored and their helpers removed, and every
        /// fire tested again by the game's rules. Nothing of the sky part: it keeps going (SkyPart.On).</summary>
        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            SmokePart.RestoreAll,
            ItemPart.RestoreAll,
        };

        /// <summary>The patches stay after an error, so the sky part keeps caged crops alive (a crop that can't grow is
        /// destroyed when it ripens). Every other patch does nothing then: each asks Module.IsOn, which an error turns
        /// false.</summary>
        internal override bool KeepPatchesAfterError => true;
    }
}
