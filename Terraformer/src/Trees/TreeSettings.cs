using System.ComponentModel;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;
using Hints = QoLMods.Kit.ConfigurationManagerAttributes;   // Jötunn's global class of the same name would hide the kit's (LESSONS_LEARNED)

namespace Terraformer.Trees
{
    /// <summary>How the preview key works. A configuration manager shows each choice by its Description; the settings file
    /// keeps the names, so they never change.</summary>
    internal enum PreviewMode
    {
        /// <summary>The preview shows while the key is held (his words). Shown as "While held", as Paint only's own choice
        /// (review round 2, finding 9); the name stays "Held", so a settings file keeps its value.</summary>
        [Description("While held")]
        Held,

        /// <summary>A press turns it on, the next press off (his "config option").</summary>
        [Description("Pressed once on, again off")]
        Toggle,
    }

    /// <summary>The size a sapling planted with the preview grows to.</summary>
    internal enum TreeSizeChoice
    {
        /// <summary>The game's own: at random within the sapling's range, when it grows.</summary>
        [Description("As the game picks")]
        AsTheGamePicks,

        /// <summary>The size he sets with the size key and the wheel (it starts at the largest).</summary>
        [Description("Picked with the key")]
        PickedWithTheKey,

        [Description("The largest")]
        Largest,

        [Description("The smallest")]
        Smallest,
    }

    /// <summary>
    /// The grown tree's settings (card B01): a part of their own, "Grown trees" (alone, a section of the settings file; in
    /// the bundle, "Terraformer - Grown trees"). The size key is Terraformer's sharpness key (Right Alt by default), which means nothing
    /// with a sapling in hand. Every setting takes effect at once.
    /// </summary>
    internal static class TreeSettings
    {
        /// <summary>The part's name, which names its section. Permanent.</summary>
        internal const string Part = "Grown trees";

        internal static ConfigEntry<KeyboardShortcut> PreviewKey { get; private set; }

        internal static ConfigEntry<PreviewMode> Mode { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> PreviousTreeKey { get; private set; }

        internal static ConfigEntry<KeyboardShortcut> NextTreeKey { get; private set; }

        internal static ConfigEntry<TreeSizeChoice> Size { get; private set; }

        internal static ConfigEntry<bool> LockTree { get; private set; }

        internal static ConfigEntry<bool> LockFacing { get; private set; }

        internal static ConfigEntry<bool> KeepTurn { get; private set; }

        internal static ConfigEntry<float> Opacity { get; private set; }

        internal static ConfigEntry<bool> NameInBar { get; private set; }

        internal static ConfigEntry<bool> GrowSpace { get; private set; }

        /// <summary>Each sapling's remembered pick (TreeChoice's line). Not listed in a configuration manager.</summary>
        internal static ConfigEntry<string> Remembered { get; private set; }

        /// <summary>Binds the part, from Settings.Bind: a fold-out under Terraformer's one heading (FoldPart), its orders
        /// counting down from Settings.TreesTop.</summary>
        internal static void Bind(ModuleSettings settings)
        {
            var fold = new FoldPart(settings, Part, "Grown trees", Settings.TreesTop);
            int order = fold.First;

            // His words (2026-10-10): "a modifier key that when held while previewing the placement of trees ...". Left Alt
            // isn't the game's (ZInput.ResetKBMButtons), and Terraformer's other Left Alt (Flatten key 3) is read only with
            // Flatten in hand, so the two never meet. The key's name was fixed with Spare Keys' table (56-B).
            PreviewKey = fold.Bind("TreePreviewKey", new KeyboardShortcut(KeyCode.LeftAlt),
                "Preview key", order--,
                "While you place a sapling with the cultivator, hold this key to see the tree it will grow into, full grown, " +
                "see-through, where the sapling goes, facing as it faces and at the size it will have. Nothing shows until " +
                "you press it. A sapling planted while the tree shows grows into exactly that tree (\"Lock the tree it " +
                "becomes\"). Seeds that grow crops show their crop; vines, which grow against a wall, show nothing.");

            Mode = fold.Bind("TreePreviewMode", PreviewMode.Held,
                "Hold or toggle", order--,
                "While held: the tree shows while the preview key is held down. Pressed once on, again off: one press shows " +
                "it, the next hides it.");

            // Q and E only step a piece's snap points while placing, and a sapling has none (Player.UpdatePlacementGhost),
            // so they're free with a sapling in hand (his pick).
            PreviousTreeKey = fold.Bind("PreviousTreeKey", new KeyboardShortcut(KeyCode.Q),
                "Previous tree", order--,
                "While the tree shows, steps back through the trees that sapling can grow into. Only the birch sapling can " +
                "grow into more than one (two birches); the others grow one tree each. Q is the game's key for autorun and " +
                "for stepping through a piece's snap points, but neither happens while you place a sapling.");

            NextTreeKey = fold.Bind("NextTreeKey", new KeyboardShortcut(KeyCode.E),
                "Next tree", order--,
                "While the tree shows, steps on to the next tree that sapling can grow into. E is the game's key for using " +
                "things, which does nothing while you place a sapling.");

            Size = fold.Bind("TreeSize", TreeSizeChoice.PickedWithTheKey,
                "The tree's size", order--,
                "The size a sapling planted while the tree shows grows to. As the game picks: at random within that sapling's " +
                "range, as in the game (the preview shows the largest). Picked with the key: hold Terraformer's \"Sharpness " +
                "key\" and turn the mouse wheel while the tree shows; it starts at the largest, and each sapling keeps its " +
                "own. The largest, or the smallest, that sapling can grow.");

            LockTree = fold.Bind("LockTree", true,
                "Lock the tree it becomes", order--,
                "On: a sapling planted while the tree shows grows into exactly the tree you saw, at that size and facing, " +
                "instead of the game's random one. Every sapling one click plants gets it, PlantEasily's rows included. Off: " +
                "the preview only shows, and the game picks when it grows. The limit: a sapling grows in the game of the " +
                "first player near it when its time comes. If that game doesn't run this version of Terraformer (a friend " +
                "without the pack, or with AdvancedTerrainModifiers, which keeps Terraformer off), the game picks the tree as " +
                "usual. Switching Terraformer off with its \"Enabled\" setting doesn't undo a lock: such a sapling still grows " +
                "as locked.");

            LockFacing = fold.Bind("LockFacing", true,
                "Lock its facing", order--,
                "On: the tree faces exactly as the sapling did, as the preview shows. Off: the game turns it up to 11.25° " +
                "either way at random when it grows.");

            KeepTurn = fold.Bind("KeepTurn", true,
                "Keep my turn for the next sapling", order--,
                "On: while the tree shows, the next sapling keeps the turn you gave the last one, so a row faces one way: " +
                "the game would give it a new random turn after each planting (and whenever what you carry changes). It " +
                "also holds while a planting's row is still being planted. Off: the game's random turn, as ever.");

            Opacity = fold.Bind("PreviewOpacity", 0.5f,
                "Preview opacity", order--,
                "How solid the tree looks: 0 can't be seen, 1 is solid. At 0.5 you see through it to what's behind.",
                new AcceptableValueRange<float>(0f, 1f));

            NameInBar = fold.Bind("NameInBuildBar", true,
                "Name the tree in the build bar", order--,
                "On: while the tree shows, the build bar names it after the sapling, with its size: \"Birch Sapling · Birch 2 " +
                "of 2 · 85 % (50 % to 100 %)\".");

            GrowSpace = fold.Bind("ShowGrowSpace", false,
                "Show the space it needs to grow", order--,
                "On: while the tree shows, a ring on the ground shows the room the sapling needs to grow (its grow radius: " +
                "2 m for most trees, 3 m for an oak). Anything solid inside it, or another healthy sapling, stops it " +
                "growing.");

            fold.End();

            Remembered = settings.Bind(Part, "RememberedTrees", string.Empty,
                "Remembered trees", 3,
                "Each sapling's last picked tree and size, written by the mod. Leave it alone.");
            Hints hints = ModuleSettings.HintsOf(Remembered);
            if (hints != null)
            {
                hints.Browsable = false;
            }
        }
    }
}
