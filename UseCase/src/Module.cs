using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using QoLMods.Catalogue;
using QoLMods.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UseCase
{
    /// <summary>
    /// Use Case as a module of miikeskii's QoL Pack (card A45, his idea of 2026-09-29): beside the game's item tooltip, a
    /// panel lists everything the item is used in (building, crafting, upgrading, refueling, and the game's other uses),
    /// each with its "· X", read live from the game so other mods' recipes and pieces count, with spoilers hidden and two
    /// ignore lists. Client-side only: it reads what is already on his computer, draws a panel, and writes nothing into
    /// the world. The same code runs in the bundle and in its single release (standalone\Plugin.cs); see
    /// QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.usecase";

        internal const string Name = "Use Case";

        internal const string Version = "0.2.0";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "UseCase";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        /// <summary>Its parts are sections of their own in the settings file ("Use Case - Spoilers" …), listed under the
        /// one heading in the window (Folds).</summary>
        internal override bool OneSectionInBundle => false;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off takes the panel away at once and the game's tooltip is all that shows; on brings " +
            "it back. Out of the box the panel itself waits for \"Show the panel\" below.";

        internal override string SwitchedOffNote => "The game's tooltips show as they do without the mod.";

        internal override string UpdateTask => "keeping the panel's keys and its list of uses";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            GameHooks.Resolve(missing, missingOptional);
            Knowledge.Resolve(missingOptional);
            Seasons.Resolve(missingOptional);
            Folds.RebuildList = ManagerLink.Rebuild();   // null with a Jötunn too old to ask
            Seen.Folder = Paths.ConfigPath;
            QoLMods.ListPicker.Lists.Resolve(missingOptional);
        }

        internal override void Update()
        {
            Folds.Tick();
            if (!IsOn || !Settings.ShowPanel.Value)
            {
                Panel.HideAll();
                return;
            }
            Index.Tick();
            Keys();
            Panel.Tidy();
        }

        internal override IEnumerable<Action> UndoSteps => new Action[]
        {
            Panel.HideAll,
            Index.Forget,
        };

        private static void Keys()
        {
            if (Typing())
            {
                return;
            }
            if (Settings.When.Value == ShowWhen.ToggledByKey && Settings.Pressed(Settings.Key.Value))
            {
                Panel.ToggledOn = !Panel.ToggledOn;
            }
            if (Settings.Pressed(Settings.PinKey.Value))
            {
                Panel.Pin();
            }
            string shown = Panel.ShownFor;
            if (shown != null && Settings.Pressed(Settings.IgnoreKey.Value))
            {
                string name = GameFacts.Localize(shown);
                Settings.NeverShowFor.Value = Loose.WithName(Settings.NeverShowFor.Value, name);
                Log.LogInfo($"{name} added to \"Never show a list for these resources\".");
            }
        }

        // The keys mean nothing while he types (chat, the console, a sign or a name, another mod's search box).
        internal static bool Typing()
        {
            if ((Chat.instance != null && Chat.instance.HasFocus()) || global::Console.IsVisible() || TextInput.IsVisible())
            {
                return true;
            }
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null)
            {
                return false;
            }
            TMP_InputField box = selected.GetComponent<TMP_InputField>();
            InputField oldBox = selected.GetComponent<InputField>();
            return (box != null && box.isFocused) || (oldBox != null && oldBox.isFocused);
        }
    }
}
