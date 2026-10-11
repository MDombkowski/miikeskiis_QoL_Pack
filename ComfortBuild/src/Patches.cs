using HarmonyLib;

namespace ComfortBuild
{
    // The mod's patches on the game's build menu. The kit applies them once, at start, under the module's own Harmony ID;
    // every one does nothing while the mod is off, except the first, which only adds the tab (hidden until the mod is on).

    /// <summary>The build menu wakes: add the Comfort list and its tab (hidden until the first frame decides).</summary>
    [HarmonyPatch(typeof(BuildUi), "Awake")]
    internal static class BuildUiAwake
    {
        private static void Postfix(BuildUi __instance) => Module.Guard("adding the Comfort tab to the build menu", () => Menu.Attach(__instance));
    }

    /// <summary>The build menu opens: look around him first, and show or hide the tab for the tool in hand.</summary>
    [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.OpenBuildMenu))]
    internal static class BuildUiOpen
    {
        private static void Prefix(BuildUi __instance)
        {
            if (Module.IsOn)
            {
                Module.Guard("opening the build menu", () => Menu.BeforeOpen(__instance));
            }
        }
    }

    /// <summary>The list on the left is filled again: our rows leave it first, and come back on the Comfort tab.</summary>
    [HarmonyPatch(typeof(BuildUi), "UpdateTagButtons")]
    internal static class BuildUiTags
    {
        // Runs even while the mod is off: its rows must never sit in another tab's list.
        private static void Prefix(BuildUi __instance) => Module.Guard("filling the build menu's list", () => Menu.BeforeTags(__instance));

        private static void Postfix(BuildUi __instance)
        {
            if (Module.IsOn)
            {
                Module.Guard("filling the build menu's list", () => Menu.AfterTags(__instance));
            }
        }
    }

    /// <summary>A piece's button is set up for a piece: its comfort, and the grey.</summary>
    [HarmonyPatch(typeof(BuildUiPieceButton), nameof(BuildUiPieceButton.Setup))]
    internal static class PieceButtonSetup
    {
        // Runs even while the mod is off, to take back what an earlier look put on the reused button.
        private static void Postfix(BuildUiPieceButton __instance) => Module.Guard("showing a piece's comfort", () => Badges.Dress(__instance));
    }

    /// <summary>The panel at the bottom describes a piece (every frame while he builds): the comfort line shows in its
    /// own box beside it, set only when it changes.</summary>
    [HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
    internal static class PieceInfo
    {
        private static void Postfix(Hud __instance, Piece piece)
        {
            if (Module.IsOn)
            {
                Module.Guard("describing a piece's comfort", () => Line.Show(__instance, Reading.LineFor(piece)));
            }
        }
    }
}
