// The QoL Mods item marks, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemMarks\. Every mod that uses it
// carries a copy in its own parts\ItemMarks\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using UnityEngine;

namespace QoLMods.ItemMarks
{
    /// <summary>
    /// A module that lets him mark a kind of item with a modifier held and a right-click, and shows the mark as a border on
    /// the item's cell: StoreStash for favourites; job 32 for trash marks. The part has no settings of its own: each of these
    /// is read from the module's own settings every time it's needed, so a changed setting counts at once, and the part's
    /// work for a module runs under that module's Guard, so an error switches off only that module (MODULES.md, "Parts").
    /// </summary>
    internal sealed class MarkUser
    {
        private readonly Func<bool> isOn;
        private readonly Action<string, Action> guard;

        /// <param name="name">The module's name, for log lines.</param>
        /// <param name="list">The kind of mark it sets and shows.</param>
        /// <param name="place">Where the kind's border sits on a cell: 0 along the edge (favourites), 1 just inside that
        /// (trash marks). Fixed per kind, so two kinds shown by two plugins never draw on top of each other.</param>
        /// <param name="isOn">Its Module.IsOn: running and switched on. Off, it marks nothing and shows no border.</param>
        /// <param name="guard">Its Module.Guard.</param>
        /// <param name="modifier">The key held with a right-click to mark or unmark (KeyCode.None: marking is off).</param>
        /// <param name="borders">Whether the mark shows as a border.</param>
        /// <param name="colour">The border's colour.</param>
        /// <param name="inChests">Whether marking and borders work in a chest's window too, not only in his inventory.</param>
        /// <param name="toggled">Told of each right-click that marked or unmarked (the item, whether it's marked now, and
        /// whether the change was saved: if not, nothing changed), to say so.</param>
        internal MarkUser(string name, MarkList list, int place, Func<bool> isOn, Action<string, Action> guard, Func<KeyCode> modifier,
                          Func<bool> borders, Func<Color> colour, Func<bool> inChests, Action<ItemDrop.ItemData, bool, bool> toggled)
        {
            Name = name;
            List = list;
            Place = place;
            this.isOn = isOn;
            this.guard = guard;
            Modifier = modifier;
            Borders = borders;
            Colour = colour;
            InChests = inChests;
            Toggled = toggled;
        }

        internal string Name { get; }

        internal MarkList List { get; }

        internal int Place { get; }

        internal Func<KeyCode> Modifier { get; }

        internal Func<bool> Borders { get; }

        internal Func<Color> Colour { get; }

        internal Func<bool> InChests { get; }

        internal Action<ItemDrop.ItemData, bool, bool> Toggled { get; }

        internal bool IsOn => isOn();

        internal void Guard(string task, Action work) => guard(task, work);
    }
}
