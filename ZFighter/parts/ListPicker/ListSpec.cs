// The QoL Mods list picker, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ListPicker\. Every mod that uses
// it carries a copy in its own parts\ListPicker\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using QoLMods.Catalogue;
using UnityEngine;

namespace QoLMods.ListPicker
{
    /// <summary>What a list's entries name: each kind is a tab of the picker window.</summary>
    internal enum ThingKind
    {
        Pieces,
        Items,
        Creatures,

        /// <summary>The game's item types by their names (Trophy, Ammo, Fish …), as Topper-Upper's "Never pick up" takes
        /// them beside items.</summary>
        ItemTypes,
    }

    /// <summary>
    /// One of the game's things a list can hold, read from the game (GameThings): its name as a new entry writes it, its
    /// name as the game shows it, its picture, and where it sits for browsing (the catalogue's Listing).
    /// </summary>
    internal sealed class Thing
    {
        internal ThingKind Kind;

        /// <summary>What a new entry writes: its prefab's name (an item type: the type's name).</summary>
        internal string Name = string.Empty;

        /// <summary>Its name as the game shows it, in his language.</summary>
        internal string Shown = string.Empty;

        /// <summary>Its $ token (a piece's or creature's m_name, an item's shared name); null for an item type.</summary>
        internal string Token;

        internal Sprite Icon;

        internal GameObject Prefab;

        /// <summary>The piece, for a list's test of which pieces it holds.</summary>
        internal Piece Piece;

        /// <summary>The item's data (its prefab's), for a list's test of which items it holds.</summary>
        internal ItemDrop.ItemData Item;

        /// <summary>The creature, for a list's test.</summary>
        internal Character Creature;

        /// <summary>An item type's value.</summary>
        internal ItemDrop.ItemData.ItemType Type;

        /// <summary>Where it sits, the catalogue's way (a piece on the Build shelf, an item or creature on Take).</summary>
        internal Listing Listing;

        /// <summary>His character has found or learned it (true with no character loaded).</summary>
        internal bool Found = true;

        /// <summary>A creature that can be tamed.</summary>
        internal bool Tame;

        /// <summary>A boss.</summary>
        internal bool Boss;

        /// <summary>Its biome of origin, as a label (the catalogue's).</summary>
        internal string Biome = Taxonomy.OtherBiome;

        /// <summary>One line on where it sits, for the bottom of the window.</summary>
        internal string Where = string.Empty;

        /// <summary>Not among the things the window lists (a piece in no build menu, a copy of an item): found only
        /// because an entry names it, so the list shows its picture.</summary>
        internal bool Extra;
    }

    /// <summary>
    /// What one list setting holds and how its text is written, as its module describes it to the picker
    /// (Lists.Attach). Each module writes its own lists' specs from its own reading of them, so the picker offers what
    /// the list can use and writes what the module reads.
    /// </summary>
    internal sealed class ListSpec
    {
        // Each module sets only the fields its own lists need (in its own build the others are never assigned).
#pragma warning disable CS0649
        /// <summary>The module's name as players see it, for the window's title.</summary>
        internal string Module = string.Empty;

        /// <summary>The kinds of thing the list names; more than one makes tabs.</summary>
        internal ThingKind[] Kinds = { ThingKind.Pieces };

        /// <summary>How the text is written and read (ListText).</summary>
        internal ListFormat Format = new ListFormat();

        /// <summary>Which things the list can use (null: every one of its kinds). A thing it can't use but an entry names
        /// still shows on the list, with a word saying so.</summary>
        internal Func<Thing, bool> Holds;

        /// <summary>The module also takes a thing's name as the game shows it, or its $ token (House Trained, Use Case):
        /// entries written that way are recognised.</summary>
        internal bool ShownNames;

        /// <summary>One line at the top of the window: what the list does.</summary>
        internal string Says = string.Empty;

        /// <summary>What an empty list means, said under it when it is empty ("every wood the kiln burns").</summary>
        internal string EmptyMeans;

        /// <summary>Where the window first opens for this list (the catalogue's pick: Flooring for floors, Metals for
        /// bars); null: everything.</summary>
        internal Selection? OpensOn;

        /// <summary>The module's log, for a failure of the picker (once each).</summary>
        internal Action<string> Warn;
#pragma warning restore CS0649
    }
}
