using System.Collections.Generic;
using System.ComponentModel;
using BepInEx.Configuration;
using QoLMods.Kit;
using UnityEngine;

namespace Terraformer
{
    /// <summary>How the paint-only key works.</summary>
    internal enum PaintOnlyMode
    {
        /// <summary>Paint only while the key is held (his words, 2026-10-10).</summary>
        [Description("While held")]
        Hold,

        /// <summary>Pressed once: paint only, until it's pressed again.</summary>
        [Description("Pressed once on, again off")]
        Toggle,
    }

    /// <summary>
    /// Paint only (card A99, his words of 2026-10-10: paint "onto the existing topology without the terrain being deformed in
    /// any way whatsoever"): while its key is held, or after it's pressed once until it's pressed again (a setting), the
    /// painting tools that shape the ground as they paint (the game's Paved road and Cultivate, which smooth within 3 m,
    /// and Terraformer's Paved road (square) and Cultivate (square)) only paint. The stroke carries the choice (the numbers'
    /// <see cref="Chosen.PaintOnly"/>), so whichever game applies it leaves its height steps out: a round stroke has its
    /// tool's level, raise and smooth switched off for that one application (Strokes.Lend), a square only paints
    /// (Strokes.KindOf). Pathen, Grass and Terraformer's paths already only paint, so the key changes nothing there.
    /// </summary>
    internal static class PaintOnly
    {
        /// <summary>The part holding its settings, a section of its own ("Terraformer - Paint only" in the bundle).</summary>
        internal const string Part = "Paint only";

        /// <summary>The painting tools by prefab, each with the key of its paint-only switch, or null for one that already
        /// only paints (research B: Pathen and Grass have no height step; Terraformer's paths and Pathen (square) none).</summary>
        internal static readonly Dictionary<string, string> Painters = new Dictionary<string, string>
        {
            ["path_v2"] = null,
            ["paved_road_v2"] = "PavedRoad",
            ["cultivate_v2"] = "Cultivate",
            ["replant_v2"] = null,
            ["Terraformer_path_square"] = null,
            ["Terraformer_paved_square"] = "PavedRoadSquare",
            ["Terraformer_paved_path"] = null,
            ["Terraformer_paved_path_square"] = null,
            ["Terraformer_cultivate_square"] = "CultivateSquare",
            ["Terraformer_cultivate_path"] = null,
            ["Terraformer_cultivate_path_square"] = null,
            ["Terraformer_grass_square"] = null,
        };

        /// <summary>The switches' keys, names and order in the settings.</summary>
        internal static readonly (string Key, string EnglishName)[] Switches =
        {
            ("PavedRoad", "Paved road"),
            ("Cultivate", "Cultivate"),
            ("PavedRoadSquare", "Paved road (square)"),
            ("CultivateSquare", "Cultivate (square)"),
        };

        internal static ConfigEntry<KeyboardShortcut> Key { get; private set; }

        internal static ConfigEntry<PaintOnlyMode> Mode { get; private set; }

        /// <summary>Each switch, by its key in <see cref="Switches"/>.</summary>
        internal static readonly Dictionary<string, ConfigEntry<bool>> SwitchEntries = new Dictionary<string, ConfigEntry<bool>>();

        internal static ConfigEntry<bool> InBuildBar { get; private set; }

        internal static ConfigEntry<bool> ToggleOffWhenPutAway { get; private set; }

        internal static ConfigEntry<MessageChoice> Message { get; private set; }

        /// <summary>Its settings, in a section of their own (Settings.Bind calls this).</summary>
        internal static void Bind(ModuleSettings settings)
        {
            var fold = new FoldPart(settings, Part, "Paint only", Settings.PaintOnlyTop);
            int order = fold.First;
            Key = fold.Bind("PaintOnlyKey", new KeyboardShortcut(KeyCode.LeftAlt),
                "Paint-only key", order--,
                "With Paved road or Cultivate in hand (the game's, or Terraformer's squares of them), hold this key and the " +
                "tool only paints: its paving or tilled soil lands on the ground exactly as it lies, and not one point of the " +
                "ground is raised, lowered, levelled or smoothed. Let go and the tool works as before. The preview grid shows " +
                "only the paint while it's held. Pathen, Grass and Terraformer's paths already only paint, so the key " +
                "changes nothing there. Left Alt is none of the game's own keys; it's also Flatten key 3, which works only " +
                "with Flatten in hand, and the grown tree's preview key, which works only with a sapling in hand.");

            Mode = fold.Bind("PaintOnlyMode", PaintOnlyMode.Hold,
                "Hold or toggle", order--,
                "While held: paint only while the key is held down. Pressed once on, again off: one press turns paint only " +
                "on, the next turns it off.");

            SwitchEntries.Clear();
            foreach ((string key, string name) in Switches)
            {
                SwitchEntries[key] = fold.Bind("PaintOnly" + key, true,
                    $"Paint only: {name}", order--,
                    $"On: the paint-only key makes {name} only paint. Off: {name} shapes the ground as it paints, key or not.");
            }

            InBuildBar = fold.Bind("PaintOnlyInBuildBar", true,
                "\"Paint only\" by the tool's name", order--,
                "On: while paint only is on for the tool in your hand, the build bar at the bottom of the screen says " +
                "\"paint only\" after the tool's name.");

            ToggleOffWhenPutAway = fold.Bind("PaintOnlyToggleOff", true,
                "Toggle turns off when the tool is put away", order--,
                "With \"Pressed once on, again off\". On: paint only turns off when you put the hoe or cultivator away or " +
                "take up another building tool. Off: it stays on until you press the key again.");

            Message = fold.Bind("PaintOnlyMessage", MessageChoice.TopLeft,
                "Message as the toggle turns on and off", order--,
                "With \"Pressed once on, again off\": where \"Paint only: on\" and \"Paint only: off\" show: Centre (the middle " +
                "of the screen), TopLeft, or Off.");
            fold.End();
        }

        private static bool toggled;

        // The build tool (the hoe's or the cultivator's menu) in his hand when the toggle went on.
        private static PieceTable toggledWith;

        /// <summary>Whether this prefab is a painting tool (the key works only with one in hand).</summary>
        internal static bool IsPainter(string prefab) => prefab != null && Painters.ContainsKey(prefab);

        /// <summary>Whether paint only, while it's wanted, changes this tool's strokes: a painting tool that shapes the ground,
        /// with its switch on.</summary>
        internal static bool SwitchedOn(string prefab)
        {
            if (prefab == null || !Painters.TryGetValue(prefab, out string key) || key == null)
            {
                return false;
            }
            return SwitchEntries.TryGetValue(key, out ConfigEntry<bool> entry) && entry.Value;
        }

        /// <summary>Whether he wants paint only now: the key held, or the toggle on.</summary>
        internal static bool Wanted
        {
            get
            {
                if (Mode == null)
                {
                    return false;
                }
                return Mode.Value == PaintOnlyMode.Toggle ? toggled : Settings.Held(Key);
            }
        }

        /// <summary>Whether a stroke of this prefab made now only paints.</summary>
        internal static bool AppliesTo(string prefab) => Module.IsOn && SwitchedOn(prefab) && Wanted;

        /// <summary>With a terrain tool in hand, after the game's own input this frame: the key turns the toggle on or off,
        /// with a painting tool in hand only.</summary>
        internal static void Frame(Player player, string prefab)
        {
            if (Mode.Value != PaintOnlyMode.Toggle || !IsPainter(prefab) || !Settings.Pressed(Key))
            {
                return;
            }
            toggled = !toggled;
            toggledWith = toggled ? player.GetBuildTool() : null;
            Say(player, toggled ? "Paint only: on" : "Paint only: off");
        }

        /// <summary>Every frame: the toggle goes off when the tool is put away (a setting: leaving building, or another
        /// build tool in hand), when the toggle stops being the key's way, and while Terraformer is off.</summary>
        internal static void Tick(Player player)
        {
            if (!toggled)
            {
                return;
            }
            if (!Module.IsOn || Mode == null || Mode.Value != PaintOnlyMode.Toggle)
            {
                Forget();
                return;
            }
            if (!ToggleOffWhenPutAway.Value)
            {
                return;
            }
            PieceTable now = player != null && player.InPlaceMode() ? player.GetBuildTool() : null;
            if (now != toggledWith)
            {
                Forget();
                if (player != null)
                {
                    Say(player, "Paint only: off");
                }
            }
        }

        /// <summary>The toggle off, without a word (switched off, or after an error).</summary>
        internal static void Forget()
        {
            toggled = false;
            toggledWith = null;
        }

        /// <summary>What the build bar's name line gets after the tool's name: " · paint only" while paint only is on for
        /// that tool and "\"Paint only\" by the tool's name" is on, else null. The bar itself is written by the one bar
        /// patch, Trees.TreeBarPatch (an optional hook: a game update that moves the bar loses only the bar's words, never
        /// Terraformer; review round 1 of A99/B02, F1).</summary>
        internal static string BarSuffix(Piece piece)
        {
            if (piece == null || InBuildBar == null)
            {
                return null;
            }
            // Asked every frame in build mode: the piece's name is read once per piece, not every frame (review round 2).
            if (!ReferenceEquals(piece, barPiece))
            {
                barPiece = piece;
                barPrefab = BarPrefabOf(piece.gameObject.name);
            }
            return barPrefab != null ? BarSuffix(InBuildBar.Value, AppliesTo(barPrefab)) : null;
        }

        // The last piece the bar asked about, and its prefab if paint only can change it (else null).
        private static Piece barPiece;
        private static string barPrefab;

        /// <summary>A piece's prefab name if paint only can change its strokes (a painting tool that shapes the ground),
        /// else null: only those ever get the bar's words.</summary>
        internal static string BarPrefabOf(string prefab) =>
            prefab != null && Painters.TryGetValue(prefab, out string key) && key != null ? prefab : null;

        /// <summary>The rule, on plain values (for a harness).</summary>
        internal static string BarSuffix(bool inBuildBar, bool applies) => inBuildBar && applies ? " · paint only" : null;

        private static void Say(Player player, string text)
        {
            switch (Message.Value)
            {
                case MessageChoice.Centre:
                    QoLMods.Kit.Say.Center(text);
                    break;
                case MessageChoice.TopLeft:
                    QoLMods.Kit.Say.TopLeft(text);
                    break;
            }
        }
    }
}
