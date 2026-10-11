using System;
using System.Collections.Generic;
using System.Linq;

namespace SpareKeys
{
    /// <summary>
    /// The words of a when (card B04): what can be in hand and what can be pointed at, each name standing for one or more
    /// kinds of thing, and which kinds one object can be at once; a when in words, and read back from words (his own list
    /// in F1, and the list of other mods' keys below, are written that way). Plain data and plain code: the harness checks
    /// every rule.
    /// </summary>
    internal static class Whens
    {
        // ---- In hand ----

        internal const string Hammer = "the hammer";
        internal const string Hoe = "the hoe";
        internal const string Cultivator = "the cultivator";
        internal const string TerrainTool = "a terrain tool";
        internal const string BuildTool = "a build tool";
        internal const string OtherBuildTool = "another mod's build tool";
        internal const string NoBuildTool = "anything but a build tool";
        internal const string ServingTray = "the serving tray";

        // A name in hand, as the kinds of thing it stands for. One thing is in hand at a time, so two names meet only
        // when they share a kind. A name not listed stands for itself.
        private static readonly Dictionary<string, string[]> hands = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Hammer] = new[] { "hammer" },
            [Hoe] = new[] { "hoe" },
            [Cultivator] = new[] { "cultivator" },
            [TerrainTool] = new[] { "hoe", "cultivator" },
            [ServingTray] = new[] { "serving tray" },
            [BuildTool] = new[] { "hammer", "hoe", "cultivator", "serving tray", "other build tool" },
            [OtherBuildTool] = new[] { "other build tool" },
            [NoBuildTool] = new[] { NotBuilding },
        };

        // ---- Pointed at, or under the pointer in a window ----

        internal const string Chest = "a chest";
        internal const string Cart = "a cart";
        internal const string ShipStorage = "a ship's storage";
        internal const string AnyContainer = "a container";
        internal const string ArmourStand = "an armour stand";
        internal const string ItemStand = "an item stand";
        internal const string CraftingStation = "a crafting station";
        internal const string Smelter = "a smelter";
        internal const string HotTub = "the hot tub";
        internal const string Fire = "a fire";
        internal const string CookingStation = "a cooking station";
        internal const string Oven = "an oven";
        internal const string Fermenter = "a fermenter";
        internal const string ShieldGenerator = "a shield generator";
        internal const string Ballista = "a ballista";
        internal const string Beehive = "a beehive";
        internal const string SapCollector = "a sap collector";
        internal const string Piece = "a piece";
        internal const string Usable = "anything you can use";
        internal const string NotContainer = "anything but a container";
        internal const string Item = "an item";

        // The kinds pointed at: one object of the world is one of these (a few are two at once: see together). A smelter
        // is the game's Smelter but the hot tub (the smelter, blast furnace, charcoal kiln, windmill, spinning wheel, eitr
        // refinery, frigid kiln); a fire the game's Fireplace (torches, sconces, braziers, campfire, hearth, bonfire); an
        // oven a cooking station with fuel of its own (the stone oven).
        private static readonly string[] pieceKinds =
        {
            "chest", "cart", "ship", "armour stand", "item stand", "crafting station", "smelter", "hot tub", "fire", "cooking station",
            "oven", "fermenter", "shield generator", "ballista", "beehive", "sap collector", "bed", "door", "portal", "sign", "ward",
            "other piece",
        };

        private static readonly string[] usableKinds = pieceKinds.Where(kind => kind != "other piece")
                                                                 .Concat(new[] { "tame", "pickable", "item on the ground" }).ToArray();

        private static readonly Dictionary<string, string[]> targets = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [Chest] = new[] { "chest" },
            [Cart] = new[] { "cart" },
            [ShipStorage] = new[] { "ship" },
            [AnyContainer] = new[] { "chest", "cart", "ship" },
            [ArmourStand] = new[] { "armour stand" },
            [ItemStand] = new[] { "item stand" },
            [CraftingStation] = new[] { "crafting station" },
            [Smelter] = new[] { "smelter" },
            [HotTub] = new[] { "hot tub" },
            [Fire] = new[] { "fire" },
            [CookingStation] = new[] { "cooking station", "oven" },
            [Oven] = new[] { "oven" },
            [Fermenter] = new[] { "fermenter" },
            [ShieldGenerator] = new[] { "shield generator" },
            [Ballista] = new[] { "ballista" },
            [Beehive] = new[] { "beehive" },
            [SapCollector] = new[] { "sap collector" },
            ["a bed"] = new[] { "bed" },
            ["a door"] = new[] { "door" },
            ["a portal"] = new[] { "portal" },
            ["a sign"] = new[] { "sign" },
            ["a ward"] = new[] { "ward" },
            ["a tame"] = new[] { "tame" },
            ["a plant"] = new[] { "plant" },
            ["something to pick"] = new[] { "pickable" },
            ["an item on the ground"] = new[] { "item on the ground" },
            [Piece] = pieceKinds,
            [Usable] = usableKinds,
            [NotContainer] = pieceKinds.Concat(usableKinds).Concat(new[] { "plant", "item" }).Where(kind => kind != "chest" && kind != "cart" && kind != "ship")
                                       .Distinct().ToArray(),
            [Item] = new[] { "item" },
        };

        // Kinds one object of the game can be at once (pointing at it is pointing at both), from the game's pieces.
        private static readonly string[][] together =
        {
            new[] { "tame", "item stand" },   // the Mysterious Rock (Placeable_HardRock), a hidden piece: a tame and an item stand
        };

        /// <summary>Whether two hands can be held at once: either is anything, or they share a kind (a name it doesn't know,
        /// a sword or another mod's tool, meets every other).</summary>
        internal static bool HandsMeet(string[] a, string[] b) => a.Length == 0 || b.Length == 0 || Share(Kinds(hands, a), Kinds(hands, b), false);

        private const string NotBuilding = "no build tool";

        /// <summary>Whether two targets can be one thing: either is anything, or they share a kind, or one object is a kind
        /// of each.</summary>
        internal static bool TargetsMeet(string[] a, string[] b) => a.Length == 0 || b.Length == 0 || Share(Kinds(targets, a), Kinds(targets, b), true);

        private static HashSet<string> Kinds(Dictionary<string, string[]> names, string[] list)
        {
            var kinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string name in list)
            {
                if (names.TryGetValue(name, out string[] each))
                {
                    kinds.UnionWith(each);
                }
                else
                {
                    kinds.Add(Unknown);   // a name it doesn't know: meets every other, as a when not known does
                }
            }
            return kinds;
        }

        private const string Unknown = "?";

        private static bool Share(HashSet<string> a, HashSet<string> b, bool objects)
        {
            if (a.Contains(Unknown) || b.Contains(Unknown) || a.Overlaps(b))
            {
                return true;
            }
            return objects && together.Any(pair => (a.Contains(pair[0]) && b.Contains(pair[1])) || (a.Contains(pair[1]) && b.Contains(pair[0])));
        }

        /// <summary>Whether a hand is only build tools (the Game's heading "Build Mode").</summary>
        internal static bool NeedsBuildTool(string[] hand) =>
            hand.Length > 0 && Kinds(hands, hand).All(kind => kind != Unknown && hands[BuildTool].Contains(kind, StringComparer.OrdinalIgnoreCase));

        /// <summary>Every name this list knows in hand, and pointed at (for the F1 list's description and the harness).</summary>
        internal static IEnumerable<string> HandNames => hands.Keys;

        internal static IEnumerable<string> TargetNames => targets.Keys;

        // ---- Screens ----

        internal const string ChestOpen = "a chest open";
        internal const string SplitWindow = "the split window";
        internal const string BuildMenu = "the build menu";

        /// <summary>The world with neither of the game's menus over it (the build menu, the radial menu).</summary>
        internal const string WalkingAbout = "walking about";
        internal const string RadialMenu = "the radial menu";
        internal const string MainMenu = "the main menu";
        internal const string PauseMenu = "the pause menu";
        internal const string ServerList = "the server list";
        internal const string LoadingScreen = "the loading screen";
        internal const string SettingsWindow = "the settings window";
        internal const string Popup = "a pop-up";

        // ---- In words ----

        /// <summary>A when in words: "in the world", "the hammer in hand", "at a chest", "the inventory open, over an item";
        /// several ways split by "; or ".</summary>
        internal static string Words(Place place) => string.Join("; or ", place.Ways.Select(WayWords));

        private static string WayWords(Place place)
        {
            var parts = new List<string>();
            // The world's own menus aren't named beside the world itself ("in the world", not "in the world or the radial
            // menu open"): a key of the world is used with them open or not.
            bool world = place.Screens.Any(screen => screen.Where == Where.World && (screen.Thing.Length == 0 || screen.Thing == WalkingAbout));
            List<string> screens = place.Screens.Where(screen => !(world && screen.Where == Where.World)).Select(ScreenWords).ToList();
            if (world)
            {
                screens.Insert(0, "in the world");
            }
            screens = screens.Distinct().ToList();
            bool worldOnly = screens.Count == 1 && world;
            if (!worldOnly || (place.Hand.Length == 0 && place.Target.Length == 0))
            {
                parts.Add(string.Join(" or ", screens));
            }
            if (place.Hand.Length > 0 && !(place.Target.Length > 0 && place.Hand.Length == 1 && place.Hand[0] == NoBuildTool))
            {
                parts.Add(string.Join(" or ", place.Hand) + " in hand");   // pointing at something already says no build tool
            }
            if (place.Target.Length > 0)
            {
                bool inWindow = place.Screens.All(screen => (screen.Where != Where.World && screen.Where != Where.Anywhere) ||
                                                            (screen.Where == Where.World && (screen.Thing == BuildMenu || screen.Thing == RadialMenu)));
                if (place.Hand.Length == 0 && !inWindow)
                {
                    parts.Add("anything in hand");   // read back as it is: pointing at something with any hand, a build tool too
                }
                parts.Add((inWindow ? "over " : "at ") + string.Join(" or ", place.Target));
            }
            return string.Join(", ", parts);
        }

        internal static string ScreenWords(KeyScreen screen)
        {
            switch (screen.Where)
            {
                case Where.World:
                    return screen.Thing.Length == 0 || screen.Thing == WalkingAbout ? "in the world" : screen.Thing + " open";
                case Where.Inventory:
                    return screen.Thing.Length == 0 ? "the inventory open" : screen.Thing;
                case Where.Map:
                    return "the map open";
                case Where.Window:
                    // A window by its name when the name says it's one ("Add to Cart's window", "a pop-up").
                    return screen.Thing.Length == 0 ? "a window open"
                        : screen.Thing.EndsWith("window", StringComparison.OrdinalIgnoreCase) || screen.Thing == Popup ? screen.Thing
                        : "window " + screen.Thing;
                case Where.Menu:
                    return screen.Thing.Length == 0 ? "a menu" : screen.Thing;
                case Where.Typing:
                    return "while typing";
                default:
                    return "anywhere";
            }
        }

        // ---- Read from words ----

        /// <summary>A when read from words, several ways split by "|" (or "; or ", as Words writes them, outside a list);
        /// see ReadWay; null when any way can't be read.</summary>
        internal static Place Read(string words, List<string> unknown = null)
        {
            if (string.IsNullOrWhiteSpace(words))
            {
                return null;
            }
            Place place = null;
            foreach (string way in words.Replace("; or ", "|").Split('|').Reverse())
            {
                Place read = ReadWay(way, unknown);
                if (read == null)
                {
                    return null;
                }
                place = new Place(read.Screens, read.Hand, read.Target, place);
            }
            return place;
        }

        /// <summary>
        /// One way of a when read from words, as Words writes them or shorter: parts split by commas; a part is screens ("world",
        /// "inventory", "chest open", "split window", "map", "menu", "main menu", "pause menu", "server list", "loading
        /// screen", "typing", "build menu", "radial menu", "window &lt;its name&gt;", "anywhere"), "hand &lt;names&gt;" or
        /// "&lt;names&gt; in hand", or "at &lt;names&gt;" or "over &lt;names&gt;"; several names split by "/" or " or ". A name in hand or
        /// pointed at that this list doesn't know is kept, taken to meet every other (the safe side), and named in
        /// <paramref name="unknown"/>. Null for empty words or a screen it can't read (named in <paramref name="unknown"/>).
        /// </summary>
        private static Place ReadWay(string words, List<string> unknown)
        {
            if (string.IsNullOrWhiteSpace(words))
            {
                return null;
            }
            var screens = new List<KeyScreen>();
            var hand = new List<string>();
            var target = new List<string>();
            bool anyHand = false;
            bool over = false;
            foreach (string raw in words.Split(','))
            {
                string part = raw.Trim();
                if (part.Length == 0)
                {
                    continue;
                }
                string lower = part.ToLowerInvariant();
                if (lower == "anything in hand" || lower == "hand anything")
                {
                    anyHand = true;
                }
                else if (lower.StartsWith("hand ", StringComparison.Ordinal))
                {
                    hand.AddRange(Alternatives(part.Substring(5)).Select(name => Known(hands, name, unknown)));
                }
                else if (lower.EndsWith(" in hand", StringComparison.Ordinal))
                {
                    hand.AddRange(Alternatives(part.Substring(0, part.Length - 8)).Select(name => Known(hands, name, unknown)));
                }
                else if (lower.StartsWith("at ", StringComparison.Ordinal) || lower.StartsWith("over ", StringComparison.Ordinal))
                {
                    over |= lower.StartsWith("over ", StringComparison.Ordinal);
                    target.AddRange(Alternatives(part.Substring(part.IndexOf(' ') + 1)).Select(name => Known(targets, name, unknown)));
                }
                else
                {
                    foreach (string each in Alternatives(part))
                    {
                        if (!TryScreen(each, out KeyScreen screen))
                        {
                            unknown?.Add(each);
                            return null;
                        }
                        screens.Add(screen);
                    }
                }
            }
            if (screens.Count == 0)
            {
                if (hand.Count == 0 && target.Count == 0)
                {
                    return null;
                }
                screens.Add(new KeyScreen(Where.World));   // a hand or a target alone: in the world
            }
            if (target.Count > 0 && hand.Count == 0 && !anyHand && !over &&
                screens.All(screen => screen.Where == Where.World && screen.Thing != BuildMenu && screen.Thing != RadialMenu))
            {
                hand.Add(NoBuildTool);   // the game points at nothing while a build tool is in hand (Player.UpdateHover)
            }
            return new Place(screens, hand, target);
        }

        private static IEnumerable<string> Alternatives(string text) =>
            text.Replace(" or ", "/").Split('/').Select(each => each.Trim()).Where(each => each.Length > 0);

        // A name as this list spells it ("chest" reads "a chest"), or his own as he typed it.
        private static string Known(Dictionary<string, string[]> names, string name, List<string> unknown)
        {
            string bare = Bare(name);
            foreach (string known in names.Keys)
            {
                if (string.Equals(Bare(known), bare, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }
            unknown?.Add(name);
            return name;
        }

        private static string Bare(string name)
        {
            string text = name.Trim();
            foreach (string article in new[] { "a ", "an ", "the " })
            {
                if (text.StartsWith(article, StringComparison.OrdinalIgnoreCase))
                {
                    return text.Substring(article.Length).Trim();
                }
            }
            return text;
        }

        private static bool TryScreen(string words, out KeyScreen screen)
        {
            string bare = Bare(words);
            string text = bare.ToLowerInvariant();
            if (text == "window" || text == "window open" || text == "any window")
            {
                screen = new KeyScreen(Where.Window);
                return true;
            }
            if (text.StartsWith("window ", StringComparison.Ordinal))
            {
                screen = new KeyScreen(Where.Window, bare.Substring(7).Trim());
                return screen.Thing.Length > 0;
            }
            switch (text)
            {
                case "anywhere":
                    screen = new KeyScreen(Where.Anywhere);
                    return true;
                case "world":
                case "in the world":
                    screen = new KeyScreen(Where.World);
                    return true;
                case "inventory":
                case "inventory open":
                    screen = new KeyScreen(Where.Inventory);
                    return true;
                case "chest open":
                case "chest":
                    screen = new KeyScreen(Where.Inventory, ChestOpen);
                    return true;
                case "split window":
                    screen = new KeyScreen(Where.Inventory, SplitWindow);
                    return true;
                case "map":
                case "map open":
                    screen = new KeyScreen(Where.Map);
                    return true;
                case "menu":
                    screen = new KeyScreen(Where.Menu);
                    return true;
                case "main menu":
                    screen = new KeyScreen(Where.Menu, MainMenu);
                    return true;
                case "pause menu":
                    screen = new KeyScreen(Where.Menu, PauseMenu);
                    return true;
                case "server list":
                    screen = new KeyScreen(Where.Menu, ServerList);
                    return true;
                case "loading screen":
                    screen = new KeyScreen(Where.Menu, LoadingScreen);
                    return true;
                case "settings window":
                case "settings":
                    screen = new KeyScreen(Where.Menu, SettingsWindow);
                    return true;
                case "typing":
                case "while typing":
                    screen = new KeyScreen(Where.Typing);
                    return true;
                case "build menu":
                case "build menu open":
                    screen = new KeyScreen(Where.World, BuildMenu);
                    return true;
                case "walking about":
                case "world, no menu":
                    screen = new KeyScreen(Where.World, WalkingAbout);
                    return true;
                case "radial menu":
                case "radial menu open":
                    screen = new KeyScreen(Where.World, RadialMenu);
                    return true;
                default:
                    // A window as Words names it ("Add to Cart's window", "a pop-up").
                    string named = words.Trim();
                    if (text.EndsWith("window", StringComparison.Ordinal) || text == "pop-up")
                    {
                        screen = new KeyScreen(Where.Window, named);
                        return true;
                    }
                    screen = default;
                    return false;
            }
        }

        // ---- His own list in F1, and the list of other mods' keys ----

        /// <summary>The when of a setting its mod never reads: it counts nowhere.</summary>
        internal const string Unused = "unused";

        /// <summary>
        /// The whens of other mods' keys Spare Keys knows, read from each mod's code (studied, never copied: PROJECT.md),
        /// written as his own list is ("mod id: setting key = when"); a line of his own in F1 wins over one here. Found by
        /// session 58-B, 2026-10-10, for the mods installed on his setup that day.
        /// </summary>
        internal const string OtherMods =
            // PlanBuild: its keys work with its Blueprint Rune in hand, the build menu closed (the game's Q steps a
            // blueprint's snap points too).
            "marcopogo.PlanBuild: Toggle = walking about/radial menu, hand another mod's build tool;" +
            "marcopogo.PlanBuild: Mirror = walking about/radial menu, hand another mod's build tool;" +
            "marcopogo.PlanBuild: ShiftModifier = walking about/radial menu, hand another mod's build tool;" +
            "marcopogo.PlanBuild: CtrlModifier = walking about/radial menu, hand another mod's build tool;" +
            "marcopogo.PlanBuild: AltModifier = walking about/radial menu, hand another mod's build tool;" +
            "marcopogo.PlanBuild: Blueprint Marketplace GUI toggle key = world | main menu | window PlanBuild's marketplace;" +
            // Plant Easily: with the cultivator in hand (the arrows while its Right Ctrl is held); its harvest key held with E.
            "advize.PlantEasily: EnableModKey = world, hand the cultivator;" +
            "advize.PlantEasily: EnableSnappingKey = world, hand the cultivator;" +
            "advize.PlantEasily: ToggleAutoReplantKey = world, hand the cultivator;" +
            "advize.PlantEasily: IncreaseXKey = world, hand the cultivator;" +
            "advize.PlantEasily: IncreaseYKey = world, hand the cultivator;" +
            "advize.PlantEasily: DecreaseXKey = world, hand the cultivator;" +
            "advize.PlantEasily: DecreaseYKey = world, hand the cultivator;" +
            "advize.PlantEasily: KeyboardModifierKey = world, hand the cultivator;" +
            "advize.PlantEasily: GamepadModifierKey = world, hand the cultivator;" +
            "advize.PlantEasily: KeyboardHarvestModifierKey = world, at something to pick/a beehive;" +
            // Cartur's Map Pins: anywhere but while typing.
            "com.jekkle.valheim.carturmappins: AutoPinningKey = world/inventory/map/menu;" +
            // Forsaken Powers Plus Remastered and Skarif's performance mod: no check at all.
            "Turbero.ForsakenPowersPlusRemastered: ForsakenPowerHotkey = anywhere;" +
            "Turbero.ForsakenPowersPlusRemastered: ResetPowerHotkey = anywhere;" +
            "com.Skarif.ValheimPerformanceOverhaul_WATER: Settings Menu Hotkey = anywhere;" +
            "com.Skarif.ValheimPerformanceOverhaul_WATER: Batch Hotkey = anywhere;" +
            // Equipment and Quick Slots: a quick slot's key uses its item (and takes the key from the game while it does).
            "randyknapp.mods.equipmentandquickslots: Quick slot hotkey 1 = world;" +
            "randyknapp.mods.equipmentandquickslots: Quick slot hotkey 2 = world;" +
            "randyknapp.mods.equipmentandquickslots: Quick slot hotkey 3 = world;" +
            "randyknapp.mods.equipmentandquickslots: Quick slot hotkey 4 = world;" +
            "randyknapp.mods.equipmentandquickslots: Quick slot hotkey 5 = world;" +
            "randyknapp.mods.equipmentandquickslots: Quick slot hotkey 6 = world;" +
            "randyknapp.mods.equipmentandquickslots: Equipment Panel Drag Key = inventory;" +
            // shudnal's configuration manager: anywhere, in the main menu too.
            "_shudnal.ConfigurationManager: Show config manager = anywhere;" +
            "_shudnal.ConfigurationManager: Reset position and size = anywhere;" +
            "_shudnal.ConfigurationManager: Reset scale = anywhere;" +
            // My Little UI: a link held with a click on an item; the chat held open.
            "shudnal.MyLittleUI: Link item modifier = inventory, over an item;" +
            "shudnal.MyLittleUI: Hold to inspect chat modifier = world";

        /// <summary>One line of a list of whens: a mod (its name or id), one of its settings (its key in the file or its
        /// name in F1), and the when.</summary>
        internal sealed class Line
        {
            internal string Mod;
            internal string Setting;

            /// <summary>The when; null for a setting the mod never reads ("unused"), which then counts nowhere.</summary>
            internal Place Place;

            internal string Text;
        }

        /// <summary>
        /// A list of whens: lines split by ";" (or new lines), each "&lt;mod&gt;: &lt;setting&gt; = &lt;when&gt;". A line it can't read
        /// is named in <paramref name="problems"/> and left out; a name in hand or pointed at it doesn't know is named
        /// there too, but kept.
        /// </summary>
        internal static List<Line> ReadList(string text, List<string> problems = null)
        {
            var lines = new List<Line>();
            foreach (string raw in (text ?? string.Empty).Replace("; or ", "|").Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = raw.Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                int colon = line.IndexOf(": ", StringComparison.Ordinal);
                int equals = line.LastIndexOf('=');
                if (colon <= 0 || equals <= colon + 1)
                {
                    problems?.Add($"\"{line}\": not \"mod: setting = when\"");
                    continue;
                }
                string mod = line.Substring(0, colon).Trim();
                string setting = line.Substring(colon + 1, equals - colon - 1).Trim();
                string when = line.Substring(equals + 1).Trim();
                if (mod.Length > 0 && setting.Length > 0 && string.Equals(when, Unused, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add(new Line { Mod = mod, Setting = setting, Place = null, Text = when });
                    continue;
                }
                var unknown = new List<string>();
                Place place = Read(when, unknown);
                if (place == null || mod.Length == 0 || setting.Length == 0)
                {
                    problems?.Add($"\"{line}\": " + (unknown.Count > 0 ? $"\"{unknown[0]}\" isn't a place it knows" : "no when"));
                    continue;
                }
                if (unknown.Count > 0)
                {
                    problems?.Add($"\"{line}\": {string.Join(", ", unknown.Select(name => "\"" + name + "\""))} not a name it knows: kept, and taken to meet every other, as a when not known is");
                }
                lines.Add(new Line { Mod = mod, Setting = setting, Place = place, Text = when });
            }
            return lines;
        }

        /// <summary>The line of a list that names this setting of this mod (the last one, so a later line wins), or null.</summary>
        internal static Line Find(IEnumerable<Line> lines, string pluginId, string pluginName, string key, string displayName)
        {
            Line found = null;
            foreach (Line line in lines ?? Enumerable.Empty<Line>())
            {
                bool mod = Same(line.Mod, pluginId) || Same(line.Mod, pluginName);
                if (mod && (Same(line.Setting, key) || Same(line.Setting, displayName)))
                {
                    found = line;
                }
            }
            return found;
        }

        private static bool Same(string a, string b) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
