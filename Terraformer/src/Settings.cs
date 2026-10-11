using System;
using System.Collections.Generic;
using System.ComponentModel;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;
using Hints = QoLMods.Kit.ConfigurationManagerAttributes;   // Jötunn's global class of the same name would hide the kit's (LESSONS_LEARNED)

namespace Terraformer
{
    /// <summary>How square tools turn with the game's build turn. A configuration manager shows each choice by its
    /// Description (shudnal's ConfigurationManager, SettingFieldDrawer.ObjectToGuiContent); the settings file keeps the
    /// names, so they never change.</summary>
    internal enum TurnChoice
    {
        /// <summary>In the game's own 22.5° steps (the default since 0.2.0, his word).</summary>
        [Description("22.5°")]
        TwentyTwoAndAHalfDegrees,

        /// <summary>In 45° steps: every notch of the wheel turns a square tool by 45°.</summary>
        [Description("45°")]
        FortyFiveDegrees,

        /// <summary>Never: square tools stay lined up with the world, as AdvancedTerrainModifiers' did.</summary>
        [Description("Off")]
        DontTurn,
    }

    /// <summary>Where Terraformer says a new value.</summary>
    internal enum MessageChoice
    {
        Centre,
        TopLeft,
        Off,
    }

    /// <summary>The height Flatten levels the ground to.</summary>
    internal enum FlattenHeight
    {
        /// <summary>The ground under his feet, as the game's own Level ground takes it.</summary>
        [Description("Your feet")]
        Feet,

        /// <summary>The ground under the middle of the tool.</summary>
        [Description("Where you point")]
        Pointed,

        [Description("The lowest point")]
        Lowest,

        [Description("The highest point")]
        Highest,

        /// <summary>The middle height of the tool's points: half are lower, half higher.</summary>
        [Description("The median")]
        Median,

        [Description("The average")]
        Average,
    }

    /// <summary>
    /// The player's settings. Alone, the mod keeps them in BepInEx\config\modprojects.terraformer.cfg, in the sections
    /// General, Paint only, Slope, Grown trees and Dust; inside miikeskii's QoL Pack, in the bundle's file, in
    /// "Terraformer" and "Terraformer - &lt;part&gt;". A configuration manager's window (F1) lists them all under the one
    /// heading, the parts as fold-outs (FoldPart; since 0.3.0). Every one takes effect at once, also when changed in game
    /// with a configuration manager: the mod reads them when it needs them.
    /// </summary>
    internal static class Settings
    {
        internal const float SmallestRadius = 0.5f;

        /// <summary>The part holding the dust switches, a section of its own (his ask, 2026-09-30).</summary>
        internal const string DustPart = "Dust";

        /// <summary>The title rows' orders of the parts folded under Terraformer's one heading in F1 (FoldPart), each
        /// part's settings counting down below its own, all below the general settings' (999 down to about 945) and above
        /// the hidden ones (1 to 3).</summary>
        internal const int PaintOnlyTop = 899;

        internal const int SlopeTop = 879;

        internal const int TreesTop = 859;

        internal const int DustTop = 799;

        /// <summary>What <see cref="Upgraded"/> holds once a settings file has 0.2.0's defaults.</summary>
        internal const int CurrentUpgrade = 2;

        /// <summary>0.1.x's defaults that 0.2.0 changed (his first test): each is moved to the new one once, where a file
        /// still holds it.</summary>
        internal static readonly KeyboardShortcut OldResetKey = new KeyboardShortcut(KeyCode.Z, KeyCode.RightControl);

        internal const TurnChoice OldTurn = TurnChoice.FortyFiveDegrees;

        internal const int OldLargestSquare = 15;

        internal static ConfigEntry<bool> Enabled { get; private set; }

        internal static ConfigEntry<bool> ToolsInMenus { get; private set; }

        /// <summary>Each tool's own switch, by the tool's Id.</summary>
        internal static readonly Dictionary<string, ConfigEntry<bool>> ToolSwitches = new Dictionary<string, ConfigEntry<bool>>();

        /// <summary>Each tool's dust switch, by its dust key (<see cref="Dust"/>).</summary>
        internal static readonly Dictionary<string, ConfigEntry<bool>> DustSwitches = new Dictionary<string, ConfigEntry<bool>>();

        internal static ConfigEntry<KeyboardShortcut> RadiusKey { get; private set; }

        internal static ConfigEntry<float> RadiusStep { get; private set; }

        internal static ConfigEntry<float> LargestRadius { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> SharpnessKey { get; private set; }

        internal static ConfigEntry<int> SharpnessStep { get; private set; }

        internal static ConfigEntry<bool> Remember { get; private set; }

        internal static ConfigEntry<bool> RememberAfterQuitting { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> ResetKey { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> UndoKey { get; private set; }

        internal static ConfigEntry<int> UndoSteps { get; private set; }

        internal static ConfigEntry<bool> ReverseWheel { get; private set; }

        internal static ConfigEntry<TurnChoice> Turn { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> StraightenKey { get; private set; }

        internal static ConfigEntry<float> LargestLift { get; private set; }

        internal static ConfigEntry<bool> SquaresResize { get; private set; }

        internal static ConfigEntry<int> LargestSquare { get; private set; }

        internal static ConfigEntry<float> LiftStep { get; private set; }

        internal static ConfigEntry<bool> LiftCanDig { get; private set; }

        internal static ConfigEntry<float> DigDepth { get; private set; }

        internal static ConfigEntry<bool> LowerNeedsWorkbench { get; private set; }

        internal static ConfigEntry<bool> LowerDropsStone { get; private set; }

        internal static ConfigEntry<FlattenHeight> FlattenTo { get; private set; }

        /// <summary>The keys that pick another height for Flatten while held, each with the height it picks, in the order
        /// they're asked (the first held one wins).</summary>
        internal static readonly List<(ConfigEntry<KeyboardShortcut> Key, ConfigEntry<FlattenHeight> To)> FlattenKeys =
            new List<(ConfigEntry<KeyboardShortcut>, ConfigEntry<FlattenHeight>)>();

        internal static ConfigEntry<bool> WholeStrokeWards { get; private set; }

        internal static ConfigEntry<MessageChoice> Messages { get; private set; }

        internal static ConfigEntry<bool> PreviewGrid { get; private set; }

        internal static ConfigEntry<bool> Outline { get; private set; }

        /// <summary>Each tool's remembered values, kept between game sessions. Not listed in a configuration manager.</summary>
        internal static ConfigEntry<string> Remembered { get; private set; }

        /// <summary>Which version's defaults the file has reached (<see cref="CurrentUpgrade"/>). Not listed.</summary>
        internal static ConfigEntry<int> Upgraded { get; private set; }

        internal static void Bind(ModuleSettings settings)
        {
            // The host has bound Enabled, first in General. A configuration manager lists the settings of a section by
            // their Order, highest first.
            Enabled = settings.Enabled;
            const string part = ModuleSettings.General;
            int order = 999;

            // On out of the box at his call (decision 4, 2026-09-30), over MODULES.md rule 4.
            ToolsInMenus = settings.Bind(part, "ToolsInMenus", true,
                "Tools in the hoe and cultivator", order--,
                "On: Terraformer's own tools (the square ones, Lower ground, Flatten, Slope, the paths and Remove terrain changes) are " +
                "in the hoe's and the cultivator's build menus, after the game's own. Off: the menus are the game's, and the " +
                "keys below still resize and sharpen the game's own tools. Each tool also has its own switch below.");

            foreach (Tool tool in Tool.All)
            {
                ToolSwitches[tool.Id] = settings.Bind(part, tool.Id, true,
                    $"Tool: {tool.EnglishName}", order--,
                    $"{tool.EnglishName} ({(tool.Table == ToolTable.Hoe ? "hoe" : "cultivator")}): {tool.EnglishDescription} " +
                    "Off takes it out of the menu at once.");
            }

            RadiusKey = settings.Bind(part, "RadiusKey", new KeyboardShortcut(KeyCode.RightControl),
                "Radius key", order--,
                "Hold this key and turn the mouse wheel to make the terrain tool in your hand bigger or smaller: every round " +
                "tool of the hoe and the cultivator, the game's own included. With a square tool it adds or takes away a row " +
                "of points on each side (see \"Square tools resize\"). The size shows in the middle of the screen, with its " +
                "width in metres. While it's held, the wheel doesn't zoom the camera.");

            RadiusStep = settings.Bind(part, "RadiusStep", 0.5f,
                "Radius per notch", order--,
                "How many metres one notch of the wheel changes a round tool's radius. A radius is half the tool's width, so " +
                "at 0.5 each notch makes it 1 m wider or narrower. Radius 0.5 = 1 m across, 1 = 2 m, 2 = 4 m, 4 = 8 m, " +
                "8 = 16 m. Terraformer's own round tools start at radius 2 (4 m across). The radius is the ground the tool " +
                "moves up or down; a painted rim the game adds around it isn't counted. Square tools count points instead: " +
                "the points are 1 m apart, so 5 × 5 is 4 m, 9 × 9 is 8 m and 17 × 17 is 16 m.",
                new AcceptableValueRange<float>(0.1f, 2f));

            LargestRadius = settings.Bind(part, "LargestRadius", 20f,
                "Largest radius", order--,
                "The largest radius a round tool can be given, in metres (twice that across). The smallest is 0.5 m (1 m " +
                "across).",
                new AcceptableValueRange<float>(4f, 30f));

            SharpnessKey = settings.Bind(part, "SharpnessKey", new KeyboardShortcut(KeyCode.RightAlt),
                "Sharpness key", order--,
                "Hold this key and turn the mouse wheel to change how sharp a round tool that changes height is: at 0 % its " +
                "change fades out gently from the middle to the edge, at 100 % it's as strong at the edge as in the middle. " +
                "With Raise ground (precision) it sets the lift instead, with Lower ground (precision) the depth, and with Slope " +
                "its edge (soft at 0 %, sharp at 100 %); with a sapling, while its grown tree shows, the tree's size. While " +
                "it's held, the wheel doesn't zoom the camera.");

            SharpnessStep = settings.Bind(part, "SharpnessStep", 5,
                "Sharpness per notch", order--,
                "How many percent one notch of the wheel changes a tool's sharpness.",
                new AcceptableValueRange<int>(1, 25));

            Remember = settings.Bind(part, "Remember", true,
                "Remember each tool's settings", order--,
                "On: each terrain tool keeps its own radius, sharpness, lift, square size and width when you put it away, open the " +
                "build menu or pick another tool. Off: they go back to the tool's own whenever you leave building.");

            RememberAfterQuitting = settings.Bind(part, "RememberAfterQuitting", true,
                "Keep them after quitting", order--,
                "On: the remembered values are kept when you quit the game, in this settings file. Off: they last until you " +
                "quit.");

            ResetKey = settings.Bind(part, "ResetKey", new KeyboardShortcut(KeyCode.Backspace, KeyCode.RightControl),
                "Reset key", order--,
                "Puts the terrain tool in your hand back to its own radius, sharpness, lift and square size (a rectangle becomes " +
                "a square again); with Slope it also forgets the first of two clicks.");

            UndoKey = settings.Bind(part, "UndoKey", new KeyboardShortcut(KeyCode.Z, KeyCode.RightControl),
                "Undo key", order--,
                "With a hoe or cultivator tool in hand, puts back the ground your last stroke changed, its height and its " +
                "paint, exactly as they were, and gives back the stone the stroke cost. Press it again for the stroke " +
                "before, up to \"Strokes to undo\". It refuses, and says why, where anyone has changed that ground since " +
                "(so a friend's work is never undone) or where the ground is too far away to be loaded.");

            UndoSteps = settings.Bind(part, "UndoSteps", 5,
                "Strokes to undo", order--,
                "How many of your latest strokes the undo key can take back. They're kept until you quit or leave the world.",
                new AcceptableValueRange<int>(1, 5));

            ReverseWheel = settings.Bind(part, "ReverseWheel", false,
                "Reverse the wheel", order--,
                "On: turning the wheel toward you makes a tool bigger, sharper or higher, instead of away from you.");

            Turn = settings.Bind(part, "Turn", TurnChoice.TwentyTwoAndAHalfDegrees,
                "Square tools turn", order--,
                "How the square tools turn, with the mouse wheel (or a gamepad's turn), as building pieces do. They share the " +
                "game's own turn, so a square takes the angle of the piece you last turned. 22.5°: the game's own steps; at " +
                "22.5° all four sides are stepped, and a small square looks the same as an unturned or 45° one. 45°: a notch " +
                "turns it by 45°; turned, its north-east and south-west sides are straight, the other two stepped, as the " +
                "game draws the ground. Off: always lined up with the world.");

            StraightenKey = settings.Bind(part, "StraightenKey", new KeyboardShortcut(KeyCode.Home),
                "Straighten key", order--,
                "With a square tool in hand, turns it back to line up with the world (north up). The game's build turn is " +
                "one for every piece, so the next building piece starts straight too.");

            SquaresResize = settings.Bind(part, "SquaresResize", true,
                "Square tools resize", order--,
                "On: the radius key and the wheel make a square tool bigger or smaller, a row of points on each side at a " +
                "time. Off: every square tool is 5 x 5 (4 m), and none can be made a rectangle.");

            LargestSquare = settings.Bind(part, "LargestSquare", 17,
                "Largest square", order--,
                "The most points along a square tool's edge, a rectangle's length and width each (odd numbers only: 3, 5, 7 …). The points are 1 m apart, so 5 " +
                "points make 4 m, 9 make 8 m and 17 make 16 m (1.41 times that, turned 45°).",
                new AcceptableValueRange<int>(3, Footprint.LargestSize));

            // "Rectangles for" and "Width key" (B02): every square tool's, so beside "Largest square".
            SlopeTool.BindRectangles(settings, part, ref order);

            LiftStep = settings.Bind(part, "LiftStep", 0.05f,
                "Lift per notch", order--,
                "How many metres one notch of the wheel, with the sharpness key held, changes Raise ground (precision)'s " +
                "lift and Lower ground (precision)'s depth: how far one click raises or digs the square.",
                new AcceptableValueRange<float>(0.01f, 0.25f));

            LargestLift = settings.Bind(part, "LargestLift", 1f,
                "Largest lift", order--,
                "The highest Raise ground (precision)'s lift, and the deepest Lower ground (precision)'s depth, can be set, " +
                "in metres (1 m in AdvancedTerrainModifiers). Each click of the raise still costs the game's two stone.",
                new AcceptableValueRange<float>(0.25f, 8f));

            LiftCanDig = settings.Bind(part, "LiftCanDig", false,
                "Precision raise can dig", order--,
                "On: Raise ground (precision)'s lift can go below zero, down to minus the largest lift, and then it digs the " +
                "square down to that depth below the ghost instead. Off: from 0 up, as AdvancedTerrainModifiers.");

            DigDepth = settings.Bind(part, "DigDepth", 0.5f,
                "Lower ground depth", order--,
                "How deep one click of Lower ground digs at its middle, in metres. The pickaxe digs 0.5 m.",
                new AcceptableValueRange<float>(0.1f, 1f));

            LowerNeedsWorkbench = settings.Bind(part, "LowerNeedsWorkbench", false,
                "Lower ground needs a workbench", order--,
                "On: Lower ground and Lower ground (precision) work only near a workbench, as AdvancedTerrainModifiers' " +
                "Lower ground did. Off: anywhere, as a pickaxe digs.");

            LowerDropsStone = settings.Bind(part, "LowerDropsStone", false,
                "Lower ground drops stone", order--,
                "On: a click of Lower ground or Lower ground (precision) drops a stone a third of the time, as a pickaxe hit " +
                "does. Off: never, as AdvancedTerrainModifiers' Lower ground.");

            FlattenTo = settings.Bind(part, "FlattenTo", FlattenHeight.Feet,
                "Flatten to", order--,
                "The height Flatten and Flatten (square) level the ground to when none of the keys below is held: your feet " +
                "(the ground you stand on, as the game's Level ground), where you point (the ground under the middle of the " +
                "tool), the lowest or the highest point the tool covers, or the median or the average of all its points (a " +
                "pillar or a pit in the tool pulls an average toward it, not a median). The preview grid shows the height " +
                "before you click. No point is ever moved more than 8 m from the world's own height (the game's rule).");

            var flattenKeys = new[]
            {
                (Key: new KeyboardShortcut(KeyCode.LeftShift), To: FlattenHeight.Pointed, Note: " Left Shift is also the game's own key for placing where you point."),
                (Key: new KeyboardShortcut(KeyCode.LeftControl), To: FlattenHeight.Lowest, Note: " Left Ctrl is also the game's crouch key, so you crouch while you hold it."),
                (Key: new KeyboardShortcut(KeyCode.LeftAlt), To: FlattenHeight.Highest, Note: string.Empty),
            };
            FlattenKeys.Clear();
            for (int i = 0; i < flattenKeys.Length; i++)
            {
                int number = i + 1;
                ConfigEntry<KeyboardShortcut> key = settings.Bind(part, $"FlattenKey{number}", flattenKeys[i].Key,
                    $"Flatten key {number}", order--,
                    $"Held while you aim and click with Flatten or Flatten (square), it levels to the height chosen in " +
                    $"\"Flatten key {number} levels to\" instead. The first of the keys held wins.{flattenKeys[i].Note}");
                ConfigEntry<FlattenHeight> to = settings.Bind(part, $"FlattenKey{number}To", flattenKeys[i].To,
                    $"Flatten key {number} levels to", order--,
                    $"The height Flatten levels to while \"Flatten key {number}\" is held (the choices of \"Flatten to\").");
                FlattenKeys.Add((key, to));
            }

            WholeStrokeWards = settings.Bind(part, "WholeStrokeWards", true,
                "Check wards over the whole stroke", order--,
                "On: a stroke bigger than its tool's own size, Lower ground, Flatten and every square are refused if any part " +
                "of them would change ground inside a ward that doesn't let you in, or in a place where the game allows no " +
                "building. Off: the game's own checks, at the stroke's middle only (Lower ground's over its reach, as the " +
                "pickaxe's dig). A stroke of the game's own tools at their own size is always checked as the game checks it.");

            Messages = settings.Bind(part, "Messages", MessageChoice.Centre,
                "Messages", order--,
                "Where a new radius, sharpness, lift or square size is shown while you change it, and what undo did: Centre " +
                "(the middle of the screen), TopLeft, or Off.");

            PreviewGrid = settings.Bind(part, "PreviewGrid", true,
                "Preview grid", order--,
                "On: while you aim a terrain tool, a grid lies on the ground over exactly the points your click will change, " +
                "each as bright as its change (a soft tool fades toward its edge; at 100 % sharpness nothing fades), with a " +
                "line at the edge. Ground the tool won't move stays dim, so Lower ground lights only what it can still dig. " +
                "The paint tools tint it in their finish (stone grey for paved, dark soil for cultivated), and Flatten shows " +
                "the ground as it will be. It replaces the game's round marker. Off: the game's round marker, grown or shrunk " +
                "to a resized tool's true size, and the square outline (\"Square outline\").");

            Outline = settings.Bind(part, "Outline", true,
                "Square outline", order--,
                "With \"Preview grid\" off. On: a square tool shows a line on the ground around the square it will change, " +
                "turned with it, and a second one at the lift above it for Raise ground (precision). The line turns red where " +
                "it can't be placed. Off: the game's own round marker shows where it goes instead.");

            Remembered = settings.Bind(part, "Remembered", string.Empty,
                "Remembered tools", 1,
                "Each terrain tool's remembered radius, sharpness, lift and square size, written by the mod. Leave it alone; " +
                "the reset key clears a tool's.");
            Hide(Remembered);

            Upgraded = settings.Bind(part, "Upgraded", 0,
                "Settings upgraded", 2,
                "Which version's defaults this file has reached, written by the mod. Leave it alone.");
            Hide(Upgraded);

            // The dust switches, a section of their own, folded under the heading: the game's six tools, then Terraformer's.
            var dust = new FoldPart(settings, DustPart, "Dust", DustTop);
            int dustOrder = dust.First;
            foreach (Dust.Entry entry in Dust.Entries)
            {
                DustSwitches[entry.Key] = dust.Bind(entry.Key, true,
                    $"Dust: {entry.EnglishName}", dustOrder--,
                    $"On: {entry.EnglishName} kicks up dust where it lands, as the game's tools do. Off: no dust; its sound " +
                    "stays.");
            }
            dust.End();

            // Paint only (A99) and Slope with the rectangles (B02), each a section of its own.
            PaintOnly.Bind(settings);
            SlopeTool.Bind(settings);

            // The grown tree's preview (card B01), a part of its own.
            Trees.TreeSettings.Bind(settings);
        }

        private static void Hide(ConfigEntryBase entry)
        {
            Hints hints = ModuleSettings.HintsOf(entry);
            if (hints != null)
            {
                hints.Browsable = false;
            }
        }

        private static bool upgradeChecked;

        /// <summary>At the module's first frame: a settings file written by 0.1.x moves, once, each setting still at 0.1.x's
        /// default to 0.2.0's (his first test): the square tools turn in 22.5° steps, the reset key moves to Right Ctrl +
        /// Backspace for undo (R is the game's own key to put the tool away), and squares reach 17 points (16 m). A setting
        /// he changed is left alone; a new file already has 0.2.0's defaults, which this leaves as they are. At the first
        /// frame, not while binding: the bundle takes a separate release's settings over just after binding, and a 0.1.x
        /// file's values would otherwise arrive after the upgrade (review round 1).</summary>
        internal static void UpgradeOnce()
        {
            if (upgradeChecked || Upgraded == null)
            {
                return;
            }
            upgradeChecked = true;
            if (Upgraded.Value >= CurrentUpgrade)
            {
                return;
            }
            (TurnChoice turn, KeyboardShortcut reset, int largest) = UpgradedValues(Turn.Value, ResetKey.Value, LargestSquare.Value);
            if (turn != Turn.Value)
            {
                Turn.Value = turn;
            }
            if (!SameKey(reset, ResetKey.Value))
            {
                ResetKey.Value = reset;
            }
            if (largest != LargestSquare.Value)
            {
                LargestSquare.Value = largest;
            }
            Upgraded.Value = CurrentUpgrade;
        }

        /// <summary>0.2.0's values for three settings of a file written by 0.1.x: each still at 0.1.x's default takes the
        /// new one; any other is kept.</summary>
        internal static (TurnChoice Turn, KeyboardShortcut Reset, int LargestSquare) UpgradedValues(TurnChoice turn, KeyboardShortcut reset, int largestSquare) =>
            (turn == OldTurn ? TurnChoice.TwentyTwoAndAHalfDegrees : turn,
             SameKey(reset, OldResetKey) ? new KeyboardShortcut(KeyCode.Backspace, KeyCode.RightControl) : reset,
             largestSquare == OldLargestSquare ? 17 : largestSquare);

        internal static bool SameKey(KeyboardShortcut a, KeyboardShortcut b) => a.ToString() == b.ToString();

        /// <summary>Whether this tool is switched on in the settings.</summary>
        internal static bool IsSwitchedOn(Tool tool) =>
            ToolSwitches.TryGetValue(tool.Id, out ConfigEntry<bool> entry) && entry.Value;

        /// <summary>Whether this dust switch is on (a switch not bound yet counts as on: the game's own).</summary>
        internal static bool DustOn(string key) =>
            !DustSwitches.TryGetValue(key, out ConfigEntry<bool> entry) || entry.Value;

        // Keys the game's input can't read: ZInput throws for a key missing from its own map (LESSONS_LEARNED, 26-B), so a
        // key that failed is remembered with the value it had, told once in the log, and read again only once it changes.
        private static readonly Dictionary<ConfigEntryBase, string> Unreadable = new Dictionary<ConfigEntryBase, string>();

        /// <summary>The key is held down now (its main key and every modifier), read through the game's input.</summary>
        internal static bool Held(ConfigEntry<KeyboardShortcut> entry) => Read(entry, pressedNow: false);

        /// <summary>The key's main key went down this frame while every modifier is held.</summary>
        internal static bool Pressed(ConfigEntry<KeyboardShortcut> entry) => Read(entry, pressedNow: true);

        private static bool Read(ConfigEntry<KeyboardShortcut> entry, bool pressedNow)
        {
            if (entry == null)
            {
                return false;
            }
            KeyboardShortcut key = entry.Value;
            if (key.MainKey == KeyCode.None)
            {
                return false;
            }
            string text = key.ToString();
            if (Unreadable.TryGetValue(entry, out string failed))
            {
                if (failed == text)
                {
                    return false;
                }
                Unreadable.Remove(entry);
            }
            try
            {
                if (pressedNow ? !ZInput.GetKeyDown(key.MainKey, false) : !ZInput.GetKey(key.MainKey, false))
                {
                    return false;
                }
                foreach (KeyCode modifier in key.Modifiers)
                {
                    if (!ZInput.GetKey(modifier, false))
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                Unreadable[entry] = text;
                Module.Log.LogWarning($"The key {text} set for \"{entry.Definition.Key}\" can't be read by the game's input, so " +
                                      "it does nothing; pick another key in the settings.");
                return false;
            }
        }
    }
}
