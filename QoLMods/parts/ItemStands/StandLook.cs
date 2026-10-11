// The QoL Mods item stands part: the one copy to edit is Games\Valheim\QoLMods\parts\ItemStands\. Every mod that uses
// it carries a copy in its own parts\ItemStands\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using UnityEngine;

namespace QoLMods.ItemStands
{
    /// <summary>How an item's fit is worked out.</summary>
    internal enum StandCheck
    {
        /// <summary>The game's own check on each counted stand, so another mod's change to it shows.</summary>
        GameCheck,

        /// <summary>The part's copy of the check, from the stands' lists and the item's model only.</summary>
        StandListsOnly,
    }

    /// <summary>How a mark is drawn.</summary>
    internal enum MarkLook
    {
        /// <summary>Small arrows drawn by the mod (the game's fonts have none): up for the wall stand, right for the flat
        /// one.</summary>
        DrawnArrows,

        /// <summary>The characters ↑ and → as text; the game's fonts lack them, so they come from a wider fallback
        /// font.</summary>
        ArrowCharacters,

        /// <summary>The letters V (vertical: the wall stand), H (horizontal: the flat one) and VH.</summary>
        Letters,
    }

    /// <summary>How the drawn mark for "both stands" puts its two arrows together.</summary>
    internal enum BothShape
    {
        /// <summary>Up, then right, side by side: "↑→".</summary>
        SideBySide,

        /// <summary>Up above right.</summary>
        Stacked,

        /// <summary>One corner, an arrow head at each end: up along the left, right along the bottom.</summary>
        Corner,
    }

    /// <summary>Where on an item's icon a mark sits.</summary>
    internal enum MarkPlace
    {
        LeftMiddle,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        RightMiddle,
    }

    /// <summary>
    /// How the marks look, from the calling module's settings; a new one is made whenever they change, never edited after,
    /// so a drawn mark knows it must be dressed again when it's handed another one (compared by reference: exact). The
    /// item stands' arrows and the armor stand's mark share the look, colour and size, each with its own place.
    /// </summary>
    internal sealed class StandLook
    {
        /// <summary>The look when no settings say otherwise (Add to Cart with no Item Stand Icons to follow).</summary>
        internal static readonly StandLook Default = new StandLook(true, true, true, true, MarkLook.DrawnArrows, BothShape.SideBySide,
                                                                   Color.white, true, DefaultSize, MarkPlace.LeftMiddle, 0f, 0f,
                                                                   MarkPlace.RightMiddle, 0f, 0f);

        /// <summary>The default size of a mark on a 64-pixel icon, in pixels (the pack's padlock badge is 13).</summary>
        internal const float DefaultSize = 14f;

        internal const float SmallestSize = 8f;

        internal const float LargestSize = 24f;

        /// <summary>How far in from the icon's edge a mark sits, on a 64-pixel icon: HexQuickStackStorage's favourite
        /// border and the pack's own take the outer 2 pixels.</summary>
        internal const float Inset = 3f;

        internal StandLook(bool wall, bool flat, bool both, bool armor, MarkLook look, BothShape bothShape, Color colour, bool outline,
                           float size, MarkPlace place, float nudgeRight, float nudgeUp, MarkPlace armorPlace, float armorNudgeRight,
                           float armorNudgeUp)
        {
            Wall = wall;
            Flat = flat;
            Both = both;
            Armor = armor;
            Look = look;
            BothShape = bothShape;
            Colour = colour;
            Outline = outline;
            Size = Mathf.Clamp(size, SmallestSize, LargestSize);
            Place = place;
            NudgeRight = nudgeRight;
            NudgeUp = nudgeUp;
            ArmorPlace = armorPlace;
            ArmorNudgeRight = armorNudgeRight;
            ArmorNudgeUp = armorNudgeUp;
        }

        /// <summary>Show the mark of items only the wall (vertical) stand takes.</summary>
        internal bool Wall { get; }

        /// <summary>Show the mark of items only the flat (horizontal) stand takes.</summary>
        internal bool Flat { get; }

        /// <summary>Show the mark of items both stands take.</summary>
        internal bool Both { get; }

        /// <summary>Show the mark of items the armor stand takes.</summary>
        internal bool Armor { get; }

        internal MarkLook Look { get; }

        internal BothShape BothShape { get; }

        /// <summary>The mark's colour: the drawn arrows' fill (their outline stays dark), or the text's.</summary>
        internal Color Colour { get; }

        /// <summary>A dark outline round the drawn arrows, so they read over any picture.</summary>
        internal bool Outline { get; }

        /// <summary>The mark's height on a 64-pixel icon, in pixels (one arrow's length); smaller icons in step.</summary>
        internal float Size { get; }

        internal MarkPlace Place { get; }

        /// <summary>A fine offset, in pixels on a 64-pixel icon: right and up.</summary>
        internal float NudgeRight { get; }

        internal float NudgeUp { get; }

        /// <summary>Where the armor stand's mark sits.</summary>
        internal MarkPlace ArmorPlace { get; }

        /// <summary>The armor stand mark's own fine offset.</summary>
        internal float ArmorNudgeRight { get; }

        internal float ArmorNudgeUp { get; }

        /// <summary>Whether a mark for this (one mark's part of an item's fit: Wall, Flat, Both or Armor) shows in this
        /// look.</summary>
        internal bool Shows(Fit fit) =>
            fit == Fit.Wall ? Wall : fit == Fit.Flat ? Flat : fit == Fit.Both ? Both : fit == Fit.Armor && Armor;

        /// <summary>Where the mark for this sits: the armor stand's at its own place, the arrows at theirs.</summary>
        internal MarkPlace PlaceOf(Fit fit) => fit == Fit.Armor ? ArmorPlace : Place;

        /// <summary>The fine offset of the mark for this, right and up.</summary>
        internal Vector2 NudgeOf(Fit fit) => fit == Fit.Armor ? new Vector2(ArmorNudgeRight, ArmorNudgeUp) : new Vector2(NudgeRight, NudgeUp);

        /// <summary>The mark as text, for the text looks: the armor stand's is A in both.</summary>
        internal string TextOf(Fit fit)
        {
            if (fit == Fit.Armor)
            {
                return "A";
            }
            if (Look == MarkLook.Letters)
            {
                return fit == Fit.Wall ? "V" : fit == Fit.Flat ? "H" : "VH";
            }
            return fit == Fit.Wall ? "↑" : fit == Fit.Flat ? "→" : "↑→";
        }
    }
}
