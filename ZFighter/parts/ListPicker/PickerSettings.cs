// The QoL Mods list picker, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ListPicker\. Every mod that uses
// it carries a copy in its own parts\ListPicker\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using BepInEx.Configuration;
using QoLMods.Kit;

namespace QoLMods.ListPicker
{
    /// <summary>How big the picker window is.</summary>
    internal enum PickerSize
    {
        Small,
        Medium,
        Large,
    }

    /// <summary>How big the pictures in the picker are.</summary>
    internal enum PictureSize
    {
        Small,
        Large,
    }

    /// <summary>
    /// The list picker's settings, one section for every mod, "Lists (every mod)" (card B03, as "Chests (every mod)" is:
    /// ModuleSettings.BindShared): in the bundle one entry each, whichever module binds them first; in a single release,
    /// that mod's own. Each module with a list setting calls Bind from its BindSettings. Read each value when it's needed,
    /// never cached (his rule 3). Out of the box everything is on: the picker only adds a button in F1 and changes no
    /// setting's value (the card).
    /// </summary>
    internal static class PickerSettings
    {
        /// <summary>The section's name, the same in the bundle and in every single release.</summary>
        internal const string Section = "Lists (every mod)";

        internal static ConfigEntry<bool> PickButtons { get; private set; }

        internal static ConfigEntry<bool> TypedBox { get; private set; }

        internal static ConfigEntry<PickerSize> WindowSize { get; private set; }

        internal static ConfigEntry<PictureSize> Pictures { get; private set; }

        internal static ConfigEntry<bool> Names { get; private set; }

        internal static ConfigEntry<bool> Unfound { get; private set; }

        internal static ConfigEntry<bool> Remember { get; private set; }

        /// <summary>Binds the shared settings (each module with a list setting calls it from its BindSettings).</summary>
        internal static void Bind(ModuleSettings settings)
        {
            PickButtons = settings.BindShared(Section, "PickButtons", true,
                "Pick buttons on list settings", 30,
                "On (out of the box): every setting that holds a list of the game's things (pieces, items, creatures) has a " +
                "Pick button beside it here, which opens a window of everything the list can hold, with pictures: a click adds " +
                "a thing to the list or takes it out. The list's text stays as it is written now, so typing works as before. " +
                "Off: the lists are plain text boxes.");
            TypedBox = settings.BindShared(Section, "TypedBox", true,
                "Keep the typed box beside the button", 29,
                "On (out of the box): each list keeps its text box beside the Pick button, for typing. Off: the box gives way " +
                "to a short line saying how many things the list holds.");
            WindowSize = settings.BindShared(Section, "WindowSize", PickerSize.Large,
                "Window size", 28,
                "How much of the screen the picker window takes: Small, Medium or Large (out of the box, as the pack's other " +
                "windows), in the middle of the screen.");
            Pictures = settings.BindShared(Section, "PictureSize", PictureSize.Large,
                "Picture size", 27,
                "The size of the pictures in the picker window: Small fits more on the screen, Large (out of the box) shows " +
                "them as the game's own menus do.");
            Names = settings.BindShared(Section, "Names", false,
                "Names under the pictures", 26,
                "On: each picture has its name under it. Off (out of the box): the name shows at the bottom of the window as " +
                "you point at a picture, as in the game's build menu, so more pictures fit.");
            Unfound = settings.BindShared(Section, "Unfound", true,
                "Show things you haven't found yet", 25,
                "On (out of the box): the picker shows everything the list can hold, what your character hasn't found or " +
                "learned yet greyed. Off: only what your character knows (at the main menu, where no character is loaded, " +
                "everything). Whatever is already on a list always shows.");
            Remember = settings.BindShared(Section, "Remember", true,
                "Each list opens where you left it", 24,
                "On (out of the box): the picker opens each list where you left it this session (its choice on the left, its " +
                "search, its grouping). Off: each list opens on everything it can hold.");
        }

        /// <summary>The Pick buttons show (a module that never bound the settings counts as on).</summary>
        internal static bool ButtonsOn => PickButtons == null || PickButtons.Value;

        internal static bool TypedBoxOn => TypedBox == null || TypedBox.Value;

        internal static PickerSize Size => WindowSize?.Value ?? PickerSize.Large;

        internal static bool LargePictures => Pictures == null || Pictures.Value == PictureSize.Large;

        internal static bool NamesOn => Names != null && Names.Value;

        internal static bool UnfoundOn => Unfound == null || Unfound.Value;

        internal static bool RememberOn => Remember == null || Remember.Value;
    }
}
