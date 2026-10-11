using System;
using System.Collections.Generic;

namespace Terraformer.Trees
{
    /// <summary>
    /// Each sapling's pick, remembered by its prefab's name: the tree he last showed and the size he set. Kept while the
    /// game runs and after quitting, in the hidden setting "Remembered trees" (TreeChoice's line), written a few seconds
    /// after a change, or at once on leaving building, rather than at every press (every write saves the whole settings
    /// file), as Terraformer's tool sizes are (Memory).
    /// </summary>
    internal static class TreeMemory
    {
        private static readonly Dictionary<string, TreeChoice> Choices = new Dictionary<string, TreeChoice>();

        private static bool loaded;
        private static bool dirty;
        private static bool unsaved;
        private static bool warnedUnsaved;
        private static float changedAt;

        /// <summary>What he picked for this sapling, or null.</summary>
        internal static TreeChoice Of(string sapling) =>
            sapling != null && Choices.TryGetValue(sapling, out TreeChoice choice) ? choice : null;

        internal static void SetTree(string sapling, string tree, float now) => Change(sapling, now, choice => choice.Tree = tree);

        internal static void SetSize(string sapling, float size, float now) => Change(sapling, now, choice => choice.Size = size);

        private static void Change(string sapling, float now, Action<TreeChoice> change)
        {
            if (sapling == null)
            {
                return;
            }
            if (!Choices.TryGetValue(sapling, out TreeChoice choice))
            {
                choice = new TreeChoice();
                Choices[sapling] = choice;
            }
            change(choice);
            dirty = true;
            changedAt = now;
        }

        /// <summary>Reads the remembered picks from the hidden setting, once.</summary>
        internal static void LoadOnce()
        {
            if (loaded || TreeSettings.Remembered == null)
            {
                return;
            }
            loaded = true;
            foreach (KeyValuePair<string, TreeChoice> pair in TreeChoice.Parse(TreeSettings.Remembered.Value))
            {
                Choices[pair.Key] = pair.Value;
            }
        }

        /// <summary>Writes the picks to the hidden setting when they changed a while ago, or at once when told to. A settings
        /// file that can't be written just now is tried again a few seconds later.</summary>
        internal static void WriteIfDue(float now, bool atOnce = false)
        {
            if (!dirty || (!atOnce && now - changedAt < Memory.WriteDelay) || TreeSettings.Remembered == null)
            {
                return;
            }
            dirty = false;
            string text = TreeChoice.Format(Choices);
            try
            {
                if (TreeSettings.Remembered.Value != text)
                {
                    TreeSettings.Remembered.Value = text;   // BepInEx saves the file as it's set
                }
                else if (unsaved)
                {
                    TreeSettings.Remembered.ConfigFile.Save();
                }
                unsaved = false;
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            {
                unsaved = true;
                dirty = true;
                changedAt = now;
                if (!warnedUnsaved)
                {
                    warnedUnsaved = true;
                    Module.Log.LogWarning($"The remembered trees couldn't be saved just now ({e.Message}); trying again.");
                }
            }
        }
    }
}
