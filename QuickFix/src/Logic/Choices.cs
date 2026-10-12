using System;
using System.ComponentModel;

// The choices of Quick Fix's settings. Their names are written into the settings file, so they never change once
// released; the descriptions are what a configuration manager (F1) shows.
namespace QuickFix
{
    /// <summary>What the Grinding Wheel or the Smith's Anvil repairs (his three choices, card B22).</summary>
    internal enum WhatItRepairs
    {
        [Description("Repairs Non-Metallic Gear")]
        NonMetallicGear,

        [Description("Repairs Metallic Gear")]
        MetallicGear,

        [Description("Repairs All")]
        All,
    }

    /// <summary>How much of an item's full durability one Sharpening Stone repair gives back.</summary>
    internal enum StoneShare
    {
        [Description("25% of full durability")]
        Quarter = 25,

        [Description("50% of full durability")]
        Half = 50,

        [Description("75% of full durability")]
        ThreeQuarters = 75,

        [Description("100% of full durability")]
        Full = 100,
    }

    /// <summary>What a Sharpening Stone repair costs.</summary>
    internal enum StoneCost
    {
        [Description("One stone used")]
        OneStone,

        [Description("A share of a stone's durability per use")]
        PercentPerUse,

        [Description("A share of a stone's durability per item repaired")]
        PercentPerItem,
    }

    /// <summary>How long the bar of a Sharpening Stone repair runs.</summary>
    internal enum StoneTime
    {
        [Description("Seconds per item")]
        PerItem,

        [Description("A flat time")]
        Flat,
    }

    /// <summary>What a repair cancelled before its bar fills costs (his pick, 2026-10-11: the stone kept).</summary>
    internal enum OnCancel
    {
        [Description("Stone kept, nothing repaired")]
        StoneKept,

        [Description("Stone used, nothing repaired")]
        StoneUsed,
    }

    /// <summary>What the character does while a Sharpening Stone repair runs.</summary>
    internal enum RepairLook
    {
        [Description("The equip motion")]
        EquipMotion,

        [Description("The crafting pose")]
        CraftingPose,

        [Description("Nothing")]
        Nothing,
    }

    /// <summary>The kinds of gear the Sharpening Stone's settings name. An axe is both a tool and a weapon.</summary>
    [Flags]
    internal enum GearKinds
    {
        None = 0,
        Tools = 1,
        Weapons = 2,
        Armor = 4,
        Shields = 8,
        Other = 16,
    }
}
