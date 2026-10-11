using System;
using System.Collections.Generic;
using System.Linq;

namespace SpareKeys
{
    /// <summary>The kind of screen a key works in, for telling whether two users of a key can both fire at once.</summary>
    internal enum Where
    {
        /// <summary>Anywhere: meets every other screen.</summary>
        Anywhere,

        /// <summary>Walking about in the world, no window open (with whatever in hand, pointing at whatever): Thing empty,
        /// or one of the game's menus drawn over the world that the game's input still reaches (the build menu, the radial
        /// menu), which a key of the world meets too.</summary>
        World,

        /// <summary>The inventory open: Thing empty for the inventory with or without a chest, or one window over it (a
        /// chest open, the split window); every one meets every other (the split window can show over a chest's).</summary>
        Inventory,

        /// <summary>The big map open.</summary>
        Map,

        /// <summary>Inside one window of its own that holds the game's input (a mod's window): Thing says which.</summary>
        Window,

        /// <summary>A menu: Thing says which (the main menu, the pause menu, the server list, the loading screen), or
        /// empty for any of them.</summary>
        Menu,

        /// <summary>While typing (chat, the console).</summary>
        Typing,
    }

    /// <summary>One screen a key works in: its kind, and for a window or a menu which one.</summary>
    internal readonly struct KeyScreen
    {
        internal readonly Where Where;
        internal readonly string Thing;

        internal KeyScreen(Where where, string thing = null)
        {
            Where = where;
            Thing = thing ?? string.Empty;
        }

        /// <summary>Whether both can be showing at once: the same screen, or one over the other (the world and the build
        /// menu over it, the inventory and a chest open in it), or a menu or window not named (any of them); every window
        /// of the inventory meets every other; anywhere meets every one.</summary>
        internal static bool Meet(KeyScreen a, KeyScreen b)
        {
            if (a.Where == Where.Anywhere || b.Where == Where.Anywhere)
            {
                return true;
            }
            if (a.Where != b.Where)
            {
                return false;
            }
            return a.Where == Where.Inventory || a.Thing.Length == 0 || b.Thing.Length == 0 ||
                   string.Equals(a.Thing, b.Thing, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// A key's when: the screens it works in, what must be in hand, and what it must be pointing at (in the world) or be
    /// over (in a window). An empty hand or target means anything or nothing. Names of things in hand and pointed at are
    /// Whens' (one name may stand for several kinds of thing: "a piece").
    /// </summary>
    internal sealed class Place
    {
        private static readonly string[] None = new string[0];

        internal readonly KeyScreen[] Screens;
        internal readonly string[] Hand;
        internal readonly string[] Target;

        /// <summary>Another way the same key works (StoreStash's Left Alt + H: pointing at a chest, or with a chest open);
        /// null for none.</summary>
        internal readonly Place Else;

        internal Place(IEnumerable<KeyScreen> screens, IEnumerable<string> hand = null, IEnumerable<string> target = null, Place orElse = null)
        {
            KeyScreen[] list = screens?.ToArray() ?? new KeyScreen[0];
            Screens = list.Length > 0 ? list : new[] { new KeyScreen(Where.Anywhere) };
            Hand = Names(hand);
            Target = Names(target);
            Else = orElse;
        }

        /// <summary>This way and every other way.</summary>
        internal IEnumerable<Place> Ways
        {
            get
            {
                for (Place way = this; way != null; way = way.Else)
                {
                    yield return way;
                }
            }
        }

        /// <summary>This, or else that: a key that works either way.</summary>
        internal Place Or(Place other) => other == null ? this : new Place(Screens, Hand, Target, Else == null ? other : Else.Or(other));

        internal Place(Where where, string thing = null, IEnumerable<string> hand = null, IEnumerable<string> target = null)
            : this(new[] { new KeyScreen(where, thing) }, hand, target)
        {
        }

        private static string[] Names(IEnumerable<string> names)
        {
            string[] list = names?.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
                                  .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            return list == null || list.Length == 0 ? None : list;
        }

        /// <summary>The first screen's kind (the one a heading follows).</summary>
        internal Where Where => Screens[0].Where;

        /// <summary>The first screen's window or menu.</summary>
        internal string Thing => Screens[0].Thing;

        internal static readonly Place Anywhere = new Place(SpareKeys.Where.Anywhere);
        internal static readonly Place World = new Place(SpareKeys.Where.World);

        /// <summary>In the world with neither the build menu nor the radial menu open (a key that checks both).</summary>
        internal static readonly Place Walking = new Place(SpareKeys.Where.World, Whens.WalkingAbout);

        /// <summary>In the world but not with the build menu open (a key that checks the build menu, not the radial).</summary>
        internal static readonly Place NoBuildMenu = new Place(new[]
        {
            new KeyScreen(SpareKeys.Where.World, Whens.WalkingAbout), new KeyScreen(SpareKeys.Where.World, Whens.RadialMenu),
        });
        internal static readonly Place Inventory = new Place(SpareKeys.Where.Inventory);
        internal static readonly Place Map = new Place(SpareKeys.Where.Map);
        internal static readonly Place Menu = new Place(SpareKeys.Where.Menu);
        internal static readonly Place Typing = new Place(SpareKeys.Where.Typing);

        /// <summary>In the world with a build tool in hand (the hammer, the hoe, the cultivator).</summary>
        internal static readonly Place Build = new Place(SpareKeys.Where.World, hand: new[] { Whens.BuildTool });

        /// <summary>In the world, pointing at one of these kinds of thing. The game points at nothing while a build tool is in
        /// hand (Player.UpdateHover), so a key that needs something pointed at needs anything but a build tool in hand.</summary>
        internal static Place Aim(params string[] things) => new Place(SpareKeys.Where.World, hand: new[] { Whens.NoBuildTool }, target: things);

        /// <summary>In the world with a tool in hand.</summary>
        internal static Place Holding(params string[] tools) => new Place(SpareKeys.Where.World, hand: tools);

        /// <summary>The same screens and target, with these tools in hand: Place.NoBuildMenu.Holding(…).</summary>
        internal Place Holding2(params string[] tools) => new Place(Screens, tools, Target, Else);

        /// <summary>One window of its own (a mod's window).</summary>
        internal static Place Window(string thing) => new Place(SpareKeys.Where.Window, thing);

        /// <summary>One of the game's menus over the world (the build menu, the radial menu): a key of the world meets it.</summary>
        internal static Place OverWorld(string thing) => new Place(SpareKeys.Where.World, thing);

        /// <summary>A window over the inventory (a chest open, the split window): a key of the inventory meets it too.</summary>
        internal static Place OverInventory(string thing) => new Place(SpareKeys.Where.Inventory, thing);

        /// <summary>The same screens and hand, over or pointing at these things (this way only).</summary>
        internal Place At(params string[] things) => new Place(Screens, Hand, things, Else);

        /// <summary>The same, with these tools in hand (this way only).</summary>
        internal Place With(params string[] tools) => new Place(Screens, tools, Target, Else);

        /// <summary>The same, in these screens as well (this way only).</summary>
        internal Place Also(params KeyScreen[] more) => new Place(Screens.Concat(more), Hand, Target, Else);

        /// <summary>The same, in this screen as well: Also(new KeyScreen(where, thing)).</summary>
        internal Place Also(Where where, string thing = null) => Also(new KeyScreen(where, thing));

        /// <summary>Places counted only with "Keys used only in menus, while typing or with a window open" on: every screen
        /// a menu, typing, a window (the inventory and a chest, the map, the split window, a mod's own window). A key for
        /// walking about never meets them.</summary>
        internal bool OnlyInMenusTypingOrWindow =>
            Ways.All(way => way.Screens.All(screen => screen.Where != SpareKeys.Where.World && screen.Where != SpareKeys.Where.Anywhere));

        /// <summary>
        /// Whether a key used in both places can fire for both at once (the compare rule, B04): their screens meet, and
        /// what they need in hand can be held together (an empty hand: anything), and what they point at can be one thing
        /// (an empty target: anything, so a key that works anywhere in the world fires while pointing at a chest too). A
        /// tool in hand and a target hold together; the hammer and the hoe never do; a chest and an armour stand never do.
        /// </summary>
        internal static bool Overlap(Place a, Place b) => a.Ways.Any(x => b.Ways.Any(y => WaysMeet(x, y)));

        private static bool WaysMeet(Place a, Place b)
        {
            bool screens = a.Screens.Any(x => b.Screens.Any(y => KeyScreen.Meet(x, y)));
            return screens && Whens.HandsMeet(a.Hand, b.Hand) && Whens.TargetsMeet(a.Target, b.Target);
        }

        /// <summary>Works anywhere with anything in hand, pointing at anything: whatever another key's when, the two meet.</summary>
        internal bool Everywhere => Ways.Any(way => way.Screens.Any(screen => screen.Where == SpareKeys.Where.Anywhere) && way.Hand.Length == 0 && way.Target.Length == 0);

        /// <summary>The when in words: "in the world", "the hammer in hand, at a piece", "a chest open".</summary>
        internal string Words => Whens.Words(this);

        /// <summary>One line holding everything that tells this place from another (the map's signature).</summary>
        internal string Signature =>
            string.Join("/", Screens.Select(screen => (int)screen.Where + screen.Thing)) + "|" + string.Join("/", Hand) + "|" + string.Join("/", Target) +
            (Else == null ? string.Empty : " or " + Else.Signature);
    }

    /// <summary>What kind of thing uses a key.</summary>
    internal enum UseKind
    {
        /// <summary>One of the game's buttons (ZInput), with his rebinds.</summary>
        GameButton,

        /// <summary>A key the game reads in its code, outside its buttons.</summary>
        GameFixed,

        /// <summary>His own console bind ("bind F7 …").</summary>
        ConsoleBind,

        /// <summary>A mod's KeyboardShortcut setting.</summary>
        Shortcut,

        /// <summary>A mod's KeyCode setting.</summary>
        KeyCodeSetting,

        /// <summary>A mod's text setting whose value reads as a key.</summary>
        TextSetting,

        /// <summary>A mod's Jötunn button with no setting behind it.</summary>
        JotunnButton,

        /// <summary>A key read in a mod's code with no setting behind it (the code scan).</summary>
        Code,

        /// <summary>A key one of the pack's own windows reads (Add to Cart's F for its search box).</summary>
        PackFixed,
    }

    /// <summary>
    /// One user of one key or combination: who (the owner), what (a setting or an action), the combination, where it
    /// works, and how it reads the key. The map is built from these and nothing else, so the harness builds them from
    /// settings files and checks the map without the game.
    /// </summary>
    internal sealed class Use
    {
        /// <summary>The game's owner id; console binds are "console".</summary>
        internal const string GameOwner = "valheim";

        internal const string ConsoleOwner = "console";

        /// <summary>The list on the left's row for the pack's modules.</summary>
        internal const string PackGroup = "pack";

        /// <summary>Who: "valheim", "console", a plugin's id, or "pack:Reserve Stock" for a module of the pack. Two users
        /// with the same owner never clash (a mod's own keys, the game's own).</summary>
        internal string OwnerId;

        /// <summary>Its name: "Valheim", "Console binds", "PlantEasily", "Reserve Stock".</summary>
        internal string Owner;

        /// <summary>The row of the list on the left it shows under: OwnerId, or PackGroup for the pack's modules.</summary>
        internal string Group;

        /// <summary>The setting's name as the configuration manager shows it, or the game's action ("Use").</summary>
        internal string What;

        /// <summary>The settings section it's in, or the game's heading; may be empty.</summary>
        internal string Section = string.Empty;

        /// <summary>The setting's key in its file, a console bind's command, or where the code reads it.</summary>
        internal string Detail = string.Empty;

        internal Combo Combo;

        internal Place Place = Place.Anywhere;

        /// <summary>When it works, in words ("at a chest", "debug mode"); empty when not known.</summary>
        internal string When = string.Empty;

        /// <summary>Its when is known (the game's tables, the pack's table, Whens' list of other mods' keys, his own list
        /// in F1); otherwise it works anywhere as far as can be told, and "A key whose when isn't known counts as" says
        /// whether it can make a clash.</summary>
        internal bool Known;

        internal UseKind Kind;

        /// <summary>Found only by reading a mod's code: maybe used (amber), unless the settings count it as used.</summary>
        internal bool Maybe;

        /// <summary>Held while clicking, scrolling or pressing another key (Shift-click, Right Ctrl with the wheel): it
        /// counts as using the key (an action put on that key would fire each time it is held), and is named so on hover.
        /// Shift, Ctrl and Alt are shared this way on purpose, so they are never called a clash.</summary>
        internal bool Held;

        /// <summary>It fires whatever else is held (the game's buttons, a KeyCode setting, the pack's keys, console
        /// binds), where BepInEx's own check refuses any extra key.</summary>
        internal bool Loose;

        /// <summary>One of the pack's keys made to work over the game's Use at its own things (Reserve Stock's Left Alt + E
        /// at a chest skips the game's E there): the game's Use carried under its modifier isn't a clash (any other key of
        /// the game's still is).</summary>
        internal bool OverGame;

        /// <summary>One of the game's debug-mode keys (Z, B, K, L …).</summary>
        internal bool DebugMode;

        /// <summary>A key of a pack module that is switched off.</summary>
        internal bool SwitchedOff;

        internal int Main => Combo.Main;

        internal int[] Mods => Combo.Mods;

        internal bool IsGame => OwnerId == GameOwner;

        internal bool IsGamepad
        {
            get
            {
                if (KeyCatalog.Get(Combo.Main)?.IsGamepad == true)
                {
                    return true;
                }
                foreach (int mod in Combo.Mods)
                {
                    if (KeyCatalog.Get(mod)?.IsGamepad == true)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>The combination as it reads ("Left Alt + E").</summary>
        internal string Keys => Combo.Text;

        /// <summary>"Reserve Stock: Key", for hover lines and the log.</summary>
        internal string Who => string.IsNullOrEmpty(What) ? Owner : $"{Owner}: {What}";
    }
}
