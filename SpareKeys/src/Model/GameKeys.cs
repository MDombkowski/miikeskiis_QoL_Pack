using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>
    /// The game's own keys (Valheim 1.0.17, decompiled): what each of its buttons does and where, their default binds, and
    /// the keys it reads in its code outside its buttons. Plain data: the mod reads the buttons' binds live (with his
    /// rebinds) and names them from here; the harness uses the defaults.
    /// </summary>
    internal static class GameKeys
    {
        /// <summary>The game's name as an owner.</summary>
        internal const string Name = "Valheim";

        /// <summary>The game's Use button (E), the only key of the game's a pack key takes over on purpose (OverGame).</summary>
        internal const string UseButton = "Use";

        private sealed class Button
        {
            internal string What;
            internal Place Place;
            internal string When;
            internal string DefaultPath;
        }

        private static readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>(StringComparer.Ordinal);

        // A button's when in words: its note (how it's pressed), then the when.
        private static void B(string name, string what, Place place, string defaultPath, string note = null) =>
            buttons[name] = new Button
            {
                What = what,
                Place = place,
                When = string.IsNullOrEmpty(note) ? place.Words : note + ", " + place.Words,
                DefaultPath = defaultPath,
            };

        // Every button's when, read from the game's code (Valheim 1.0.17) by session 58-B, 2026-10-10 (card B04). The guards:
        // Player.TakeInput fails with any window open (the inventory and a chest, the map, the trader, a menu, a text box,
        // chat or the console) but not with the build menu or the radial menu; PlayerController.TakeInput (moving, jumping,
        // running, auto-run) fails only while typing, in a menu or a text box, so on the keyboard those work with the
        // inventory, the map and the build menu open too; the game points at nothing with a build tool in hand
        // (Player.UpdateHover). A button fires whatever modifiers are held.
        static GameKeys()
        {
            string[] buildTool = { Whens.BuildTool };
            string[] noBuildTool = { Whens.NoBuildTool };
            Place anyWorld = Place.World;
            Place moving = Place.World.Also(Where.Inventory).Also(Where.Map);   // PlayerController: windows don't stop it
            Place usable = new Place(SpareKeys.Where.World, Whens.WalkingAbout, noBuildTool, new[] { Whens.Usable });
            Place inWorldNotRadial = new Place(new[] { new KeyScreen(SpareKeys.Where.World, Whens.WalkingAbout), new KeyScreen(SpareKeys.Where.World, Whens.BuildMenu) });
            // ZInput.ResetKBMButtons, in its order.
            B("Attack", "Attack", Place.Walking, "<Mouse>/leftButton", "places a piece with a build tool");
            B("SecondaryAttack", "Secondary attack", Place.Walking.With(Whens.NoBuildTool), "<Mouse>/middleButton");
            B("Block", "Block", Place.Walking.With(Whens.NoBuildTool), "<Mouse>/rightButton");
            B("Use", "Use; with the inventory open, close it", usable.Or(Place.Inventory), "<Keyboard>/E");
            B("Hide", "Hide or show your weapon", anyWorld, "<Keyboard>/R");
            B("Jump", "Jump", moving, "<Keyboard>/Space");
            B("Crouch", "Crouch", Place.World.Also(Where.Map).With(Whens.NoBuildTool), "<Keyboard>/LeftCtrl");
            B("AltDodge", "Dodge", Place.Walking, "");
            B("Run", "Run", moving, "<Keyboard>/LeftShift");
            B("ToggleWalk", "Walk", inWorldNotRadial, "<Keyboard>/C");
            B("AutoRun", "Auto-run", moving.With(Whens.NoBuildTool), "<Keyboard>/Q");
            B("Sit", "Sit", inWorldNotRadial, "<Keyboard>/X");
            B("GP", "Guardian power", Place.Walking, "<Keyboard>/F");
            // With a build tool: place without snapping; in the world, E's other use (an item stand turned, a tame
            // renamed, a fire fed); in the inventory, craft several.
            B("AltPlace", "Place without snapping, and Shift + E, and craft several", Place.World.Also(Where.Inventory), "<Keyboard>/LeftShift");
            B("Forward", "Move forward", moving, "<Keyboard>/W");
            B("Left", "Move left", moving, "<Keyboard>/A");
            B("Backward", "Move backward", moving, "<Keyboard>/S");
            B("Right", "Move right", moving, "<Keyboard>/D");
            B("Inventory", "Inventory", Place.Walking.Or(Place.Inventory), "<Keyboard>/Tab");
            B("Map", "Map", Place.World.Or(Place.Map), "<Keyboard>/M");
            B("MapZoomOut", "Map zoom out", Place.World.Or(Place.Map), "<Keyboard>/Comma", "the map and the minimap");
            B("MapZoomIn", "Map zoom in", Place.World.Or(Place.Map), "<Keyboard>/Period", "the map and the minimap");
            B("Escape", "Menu, and close a window", Place.Anywhere, "<Keyboard>/Escape");
            // The build menu's tabs, or with it closed the ghost's next snap point (Player.UpdatePlacementGhost, any tool:
            // the hammer's pieces have them, and so do another mod's ghosts, PlanBuild's blueprints; the hoe's and the
            // cultivator's have none); and the Settings window's tabs.
            Place tabs = Place.OverWorld(Whens.BuildMenu).With(buildTool)
                              .Or(Place.NoBuildMenu.With(Whens.Hammer, Whens.OtherBuildTool))
                              .Or(new Place(SpareKeys.Where.Menu, Whens.SettingsWindow));
            B("TabLeft", "Build menu: previous tab; previous snap point", tabs, "<Keyboard>/Q");
            B("TabRight", "Build menu: next tab; next snap point", tabs, "<Keyboard>/E");
            B("BuildMenu", "Build menu", inWorldNotRadial.With(buildTool), "<Mouse>/rightButton");
            B("Remove", "Remove a piece", Place.NoBuildMenu.With(buildTool), "<Mouse>/middleButton");
            B("AutoPickup", "Auto pickup on or off", anyWorld, "<Keyboard>/V");
            B("ScrollChatUp", "Scroll the chat up", Place.Typing, "<Keyboard>/PageUp");
            B("ScrollChatDown", "Scroll the chat down", Place.Typing, "<Keyboard>/PageDown");
            B("ChatUp", "Chat history, back", Place.Typing, "<Keyboard>/UpArrow");
            B("ChatDown", "Chat history, forward", Place.Typing, "<Keyboard>/DownArrow");
            B("Console", "Console (when it's enabled)", Place.Anywhere, "<Keyboard>/F5");
            B("OpenRadial", "Radial menu", Place.NoBuildMenu, "<Keyboard>/G");
            B("OpenEmote", "Emotes", Place.NoBuildMenu, "<Keyboard>/T");
            B("RadialSecondaryInteract", "Radial menu: throw or back", Place.OverWorld(Whens.RadialMenu), "<Mouse>/rightButton");
            for (int i = 1; i <= 8; i++)
            {
                B($"Hotbar{i}Alt", $"Hotbar {i} (second key)", anyWorld, "");
            }
            B("Chat", "Chat", Place.NoBuildMenu.Or(Place.Map), "<Keyboard>/Enter");
            for (int i = 1; i <= 8; i++)
            {
                B($"Hotbar{i}", $"Hotbar {i}", anyWorld, $"<Keyboard>/{i}");
            }
        }

        /// <summary>The game's raw mouse buttons (MouseLeft …) are what its windows read for clicks, not actions of their
        /// own; HotbarUse is the gamepad's; nothing reads the Tab and LShift buttons or the keyboard's camera zoom (the
        /// decompile, 58-B). They are left out.</summary>
        internal static bool Skipped(string button) =>
            button.StartsWith("Mouse", StringComparison.Ordinal) || button == "HotbarUse" || button == "Tab" || button == "LShift" ||
            button == "CamZoomIn" || button == "CamZoomOut";

        /// <summary>A game button's action in words ("Use"), or its own name for one this table doesn't know; null for none.</summary>
        internal static string ActionName(string button) =>
            string.IsNullOrEmpty(button) ? null : buttons.TryGetValue(button, out Button known) ? known.What : button;

        /// <summary>The game's buttons with their default binds (ZInput.ResetKBMButtons): for the harness, and for a game
        /// whose input isn't up yet.</summary>
        internal static IEnumerable<KeyValuePair<string, string>> Defaults()
        {
            foreach (KeyValuePair<string, Button> each in buttons)
            {
                yield return new KeyValuePair<string, string>(each.Key, each.Value.DefaultPath);
            }
        }

        /// <summary>
        /// One of the game's buttons as a use, from its name and its bind as the game reads it now; null for one with no
        /// key, a raw mouse button, or a Jötunn button (named "button!mod id": Jotunn reads those for its mods, ModKeys).
        /// A button carries no modifier and fires whatever else is held (LESSONS_LEARNED, "a key that looks free may be the
        /// game's").
        /// </summary>
        internal static Use FromButton(string name, string path)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('!') >= 0 || Skipped(name))
            {
                return null;
            }
            int key = KeyCatalog.IdOfPath(path);
            if (key < 0)
            {
                return null;
            }
            buttons.TryGetValue(name, out Button known);
            return new Use
            {
                OwnerId = Use.GameOwner,
                Owner = Name,
                Group = Use.GameOwner,
                What = known?.What ?? name,
                Section = "Controls",
                Detail = name,
                Combo = new Combo(key, null),
                Place = known?.Place ?? Place.Anywhere,
                When = known?.When ?? string.Empty,
                Known = known != null,
                Kind = UseKind.GameButton,
                Loose = true,
            };
        }

        /// <summary>One of the game's buttons as a use without its key: its when, for the panel beside the Controls menu
        /// while it is being rebound. Null for a raw mouse button or a Jötunn button.</summary>
        internal static Use Template(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('!') >= 0 || Skipped(name))
            {
                return null;
            }
            buttons.TryGetValue(name, out Button known);
            return new Use
            {
                OwnerId = Use.GameOwner,
                Owner = Name,
                Group = Use.GameOwner,
                What = known?.What ?? name,
                Section = "Controls",
                Detail = name,
                Combo = Combo.None,
                Place = known?.Place ?? Place.Anywhere,
                When = known?.When ?? string.Empty,
                Known = known != null,
                Kind = UseKind.GameButton,
                Loose = true,
            };
        }

        private static Use Fixed(KeyCode key, string what, Place place, string source, KeyCode[] mods = null, bool held = false, bool debug = false) =>
            new Use
            {
                OwnerId = Use.GameOwner,
                Owner = Name,
                Group = Use.GameOwner,
                What = what,
                Section = "Fixed keys",
                Detail = source,
                Combo = new Combo((int)key, mods == null ? null : Array.ConvertAll(mods, mod => (int)mod)),
                Place = place,
                When = (debug ? "debug mode with cheats, " : held ? "held, " : string.Empty) + place.Words,
                Known = true,
                Kind = UseKind.GameFixed,
                Loose = true,
                Held = held,
                DebugMode = debug,
            };

        /// <summary>
        /// The keys the game reads in its code with a fixed KeyCode, outside its buttons (a grep of assembly_valheim for
        /// ZInput.GetKey*; the research's part 1). <paramref name="shortcutsMod"/>: ComfyMods' Shortcuts is installed and
        /// has moved the keys it makes configurable into its own settings, which are read as its keys instead.
        /// </summary>
        internal static List<Use> FixedKeys(bool shortcutsMod)
        {
            var ctrl = new[] { KeyCode.LeftControl };
            var shift = new[] { KeyCode.LeftShift };
            var list = new List<Use>();
            Place split = Place.OverInventory(Whens.SplitWindow);
            Place radial = Place.OverWorld(Whens.RadialMenu);
            Place debugWorld = Place.World;
            if (!shortcutsMod)
            {
                list.Add(Fixed(KeyCode.F1, "Free or capture the mouse", Place.Anywhere, "GameCamera.UpdateMouseCapture", ctrl));
                list.Add(Fixed(KeyCode.F2, "Network panel", Place.Anywhere, "ConnectPanel.Update"));
                list.Add(Fixed(KeyCode.F3, "Hide the HUD", Place.Anywhere, "Hud.Update", ctrl));
                list.Add(Fixed(KeyCode.F11, "Screenshot", Place.Anywhere, "GameCamera.LateUpdate"));
                list.Add(Fixed(KeyCode.Z, "Fly", debugWorld, "Player.Update", debug: true));
                list.Add(Fixed(KeyCode.B, "No building cost", debugWorld, "Player.Update", debug: true));
                list.Add(Fixed(KeyCode.K, "Kill nearby enemies", debugWorld, "Player.Update", debug: true));
                list.Add(Fixed(KeyCode.L, "Remove nearby drops", debugWorld, "Player.Update", debug: true));
            }
            list.Add(Fixed(KeyCode.F9, "Change the gamepad layout", Place.Anywhere, "KeyHints.Update"));
            list.Add(Fixed(KeyCode.F, "Search the build menu", Place.OverWorld(Whens.BuildMenu).With(Whens.BuildTool), "BuildUi", held: true));
            foreach (KeyCode key in new[] { KeyCode.LeftShift, KeyCode.RightShift, KeyCode.LeftControl, KeyCode.RightControl })
            {
                list.Add(Fixed(key, "With a click: split or move a stack", Place.Inventory.At(Whens.Item), "InventoryGrid.OnLeftDown", held: true));
            }
            for (int digit = 0; digit <= 9; digit++)
            {
                list.Add(Fixed(KeyCode.Alpha0 + digit, "Type an amount", split, "InventoryGui.UpdateSplitDialog"));
                list.Add(Fixed(KeyCode.Keypad0 + digit, "Type an amount", split, "InventoryGui.UpdateSplitDialog"));
            }
            foreach (KeyCode key in new[] { KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.Return, KeyCode.KeypadEnter })
            {
                list.Add(Fixed(key, key == KeyCode.Return || key == KeyCode.KeypadEnter ? "OK" : "Change the amount", split, "InventoryGui.UpdateSplitDialog"));
            }
            list.Add(Fixed(KeyCode.LeftShift, "With a right-click: throw from the radial menu", radial, "RadialConfigHelper", held: true));
            list.Add(Fixed(KeyCode.BackQuote, "Close the radial menu", radial, "RadialConfigHelper"));
            list.Add(Fixed(KeyCode.Tab, "Complete a command", Place.Typing, "Terminal.UpdateInput"));
            // In-game pop-ups (UnifiedPopup) take Enter and Escape too; the menus' own keys.
            list.Add(Fixed(KeyCode.Return, "OK in a pop-up", Place.Window(Whens.Popup), "UIGamePad"));
            list.Add(Fixed(KeyCode.J, "Copy the join code", new Place(SpareKeys.Where.Menu, Whens.PauseMenu), "JoinCode.Update"));
            Place serverList = new Place(SpareKeys.Where.Menu, Whens.ServerList);
            Place mainMenu = new Place(SpareKeys.Where.Menu, Whens.MainMenu);
            list.Add(Fixed(KeyCode.R, "Refresh the server list", serverList, "ServerListGui"));
            list.Add(Fixed(KeyCode.W, "Move a favourite server up", serverList, "ServerListGui"));
            list.Add(Fixed(KeyCode.S, "Move a favourite server down", serverList, "ServerListGui"));
            list.Add(Fixed(KeyCode.UpArrow, "Select in a list", Place.Menu, "FejdStartup, ServerListGui, ManageSavesMenu"));
            list.Add(Fixed(KeyCode.DownArrow, "Select in a list", Place.Menu, "FejdStartup, ServerListGui, ManageSavesMenu"));
            list.Add(Fixed(KeyCode.LeftArrow, "Select in a list", mainMenu, "ManageSavesMenu, UIGamePad"));
            list.Add(Fixed(KeyCode.RightArrow, "Select in a list", mainMenu, "ManageSavesMenu, UIGamePad"));
            list.Add(Fixed(KeyCode.Return, "Select", mainMenu, "FejdStartup"));
            list.Add(Fixed(KeyCode.N, "New character", mainMenu, "UIGamePad"));
            list.Add(Fixed(KeyCode.Insert, "New world", mainMenu, "UIGamePad"));
            list.Add(Fixed(KeyCode.Home, "Add a server", mainMenu, "UIGamePad"));
            list.Add(Fixed(KeyCode.Tab, "Next tab", Place.Menu, "TabHandler"));
            Place loading = new Place(SpareKeys.Where.Menu, Whens.LoadingScreen);
            list.Add(Fixed(KeyCode.Space, "Next tip", loading, "Hud.UpdateShownTip"));
            list.Add(Fixed(KeyCode.LeftArrow, "Previous tip", loading, "Hud.UpdateShownTip"));
            list.Add(Fixed(KeyCode.RightArrow, "Next tip", loading, "Hud.UpdateShownTip"));
            list.Add(Fixed(KeyCode.LeftControl, "Fly down; with a middle-click on the map, teleport", debugWorld.Also(Where.Map),
                           "Character.UpdateDebugFly, Minimap", held: true, debug: true));
            list.Add(Fixed(KeyCode.C, "Time of day and field of view with the mouse", Place.Walking, "GameCamera", shift, held: true, debug: true));
            list.Add(Fixed(KeyCode.N, "Skip the music", Place.World, "MusicMan, MusicLocation", shift, debug: true));
            return list;
        }
    }
}
