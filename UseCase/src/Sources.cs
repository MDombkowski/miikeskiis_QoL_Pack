using System.Collections.Generic;
using UnityEngine;

namespace UseCase
{
    /// <summary>Where a tooltip was filled in, each a setting (Where it shows).</summary>
    internal enum Where
    {
        Inventory,
        Trader,
        CraftingCost,
        BuildCost,
    }

    /// <summary>
    /// Which item each of the game's tooltips is for. The game's tooltip holds only text, so the mod notes the item where
    /// the game fills one in (Patches: the inventory grid, the trader's list, the cost icons), by the UITooltip that shows
    /// it. The inventory's is noted every frame for the slot under the pointer, the cost icons' each time the panel is
    /// set up, the trader's when its list is made. A note never outlives its UITooltip: gone ones are dropped as the
    /// notes grow.
    /// </summary>
    internal static class Sources
    {
        private const int Tidy = 400;

        // By the tooltip's instance id, which a destroyed object keeps (Unity's own == calls every destroyed object null).
        private static readonly Dictionary<int, (UITooltip Tooltip, string Resource, Where Where, int Frame)> notes = new Dictionary<int, (UITooltip, string, Where, int)>();

        internal static void Note(UITooltip tooltip, string resource, Where where)
        {
            if (tooltip == null || string.IsNullOrEmpty(resource))
            {
                return;
            }
            int id = tooltip.GetInstanceID();
            if (notes.Count >= Tidy && !notes.ContainsKey(id))
            {
                Prune();
            }
            notes[id] = (tooltip, resource, where, UnityEngine.Time.frameCount);
        }

        /// <summary>The item a tooltip was last filled in for, where, and whether that was this frame or the last.</summary>
        internal static bool TryGet(UITooltip tooltip, out string resource, out Where where, out bool fresh)
        {
            if (tooltip != null && notes.TryGetValue(tooltip.GetInstanceID(), out (UITooltip Tooltip, string Resource, Where Where, int Frame) note))
            {
                resource = note.Resource;
                where = note.Where;
                fresh = note.Frame >= UnityEngine.Time.frameCount - 1;
                return true;
            }
            resource = null;
            where = Where.Inventory;
            fresh = false;
            return false;
        }

        internal static void Clear() => notes.Clear();

        private static void Prune()
        {
            var gone = new List<int>();
            foreach (KeyValuePair<int, (UITooltip Tooltip, string Resource, Where Where, int Frame)> note in notes)
            {
                if (note.Value.Tooltip == null)   // Unity's check: destroyed with its window or list
                {
                    gone.Add(note.Key);
                }
            }
            foreach (int id in gone)
            {
                notes.Remove(id);
            }
            if (notes.Count >= Tidy)
            {
                notes.Clear();   // still full of live ones: start again, each is noted again when it is filled in
            }
        }
    }
}
