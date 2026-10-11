namespace SpareKeys
{
    /// <summary>
    /// The module's turn every frame (the kit calls it also while the mod is switched off, so it can follow the switch):
    /// at the first frame, when every plugin has started, it finds the configuration manager and the mods it stands aside
    /// for, and starts reading mods' code; off, it closes the window and the panels at once; on, it runs them.
    /// </summary>
    internal static class Frames
    {
        private static bool firstFrameDone;

        internal static void Update()
        {
            if (!firstFrameDone)
            {
                firstFrameDone = true;
                Reach.AtFirstFrame();
            }
            if (!Module.IsOn)
            {
                KeyMapWindow.Close();   // also gives back an input hold still waiting for its next frame
                PickPanels.HideAll();
                return;
            }
            if (Settings.Code.Value != CodeRule.Off)
            {
                CodeScan.StartOnce();
            }
            CodeScan.Tick();
            Collector.LogClashesOnce();
            KeyMapWindow.Update();
            PickPanels.Update();
        }
    }
}
