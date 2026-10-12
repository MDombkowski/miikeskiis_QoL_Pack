using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>
    /// One setting of a loaded mod, as plain text: which plugin, its section and key, its name in a configuration manager,
    /// its type and its value as BepInEx writes it. The mod reads these from every plugin's settings at run time; the
    /// harness reads them from settings files. Either way ModKeys turns them into uses the same way.
    /// </summary>
    internal sealed class SettingRecord
    {
        internal string PluginId;
        internal string PluginName;
        internal string Section;
        internal string Key;
        internal string DisplayName;

        /// <summary>"KeyboardShortcut", "KeyCode", "String" (anything else is skipped).</summary>
        internal string Type;

        internal string Value;

        /// <summary>For a setting of the pack (the bundle, or one of its modules' single releases): the module's name
        /// ("Reserve Stock"); null for any other mod's.</summary>
        internal string PackModule;

        /// <summary>For a pack module's setting: whether its module is switched on.</summary>
        internal bool ModuleOn = true;

        /// <summary>The other settings of its section, by key (for a pack key that counts only while another setting says
        /// so: Handyman's own key).</summary>
        internal Func<string, string> Sibling = _ => null;
    }

    /// <summary>
    /// Mods' settings as uses of keys, and what the map knows of the pack's own keys: where each works and how it reads
    /// it. The pack's setting keys are permanent once released (QoLMods\MODULES.md, rule 4), so this table can name them; a
    /// key added later without a line here still counts, as working anywhere.
    /// </summary>
    internal static class ModKeys
    {
        /// <summary>The bundle's plugin id; its single releases' ids start "modprojects.".</summary>
        internal const string BundleId = "modprojects.qolmods";

        private sealed class Known
        {
            internal Place Place = Place.Anywhere;

            /// <summary>How it's pressed, when that says more than the when ("held with the wheel"), else empty.</summary>
            internal string Note = string.Empty;

            internal bool Held;
            internal bool OverGame;

            /// <summary>The other setting (in the same section) and its value with which this key counts at all.</summary>
            internal string OnlyWhenKey;

            internal string OnlyWhenValue;

            /// <summary>Another setting (in the same section) and its value with which this key works in AltPlace instead
            /// (and is pressed, not held).</summary>
            internal string AltKey;

            internal string AltValue;

            internal Place AltPlace;
        }

        private static readonly Dictionary<string, Known> pack = new Dictionary<string, Known>(StringComparer.Ordinal);

        private static void K(string module, string key, Place place, string note = null, bool held = false, bool overGame = false,
                              string onlyWhenKey = null, string onlyWhenValue = null, string altKey = null, string altValue = null,
                              Place altPlace = null) =>
            pack[module + "\n" + key] = new Known
            {
                Place = place,
                Note = note ?? string.Empty,
                Held = held,
                OverGame = overGame,
                OnlyWhenKey = onlyWhenKey,
                OnlyWhenValue = onlyWhenValue,
                AltKey = altKey,
                AltValue = altValue,
                AltPlace = altPlace,
            };

        /// <summary>Every module and setting key the table knows ("Reserve Stock\nKey"), for the harness.</summary>
        internal static IEnumerable<string> PackKeys => pack.Keys;

        /// <summary>A pack key's when, from the table (null when the table has no line for it).</summary>
        internal static Place PackPlace(string module, string key) => pack.TryGetValue(module + "\n" + key, out Known known) ? known.Place : null;

        // Every pack key's when, read from its module's own code by session 58-B (2026-10-10, card B04): where the key is
        // checked, with what in hand, pointing at what, in which window. A key works in the world when its module checks
        // the game's input (Player.TakeInput, or the same list of windows): not with the inventory, the map, a menu or a
        // text box open, but with the build menu and the radial menu (the game's input reaches the world under them); a
        // key that needs something pointed at needs anything but a build tool in hand (the game points at nothing then,
        // Player.UpdateHover). Over the game's own key: the module skips the game's use of E on that thing while its key
        // is held. A new pack key needs a line here (A76 decision 10).
        static ModKeys()
        {
            Place cartWindow = Place.Window("Add to Cart's window");
            Place outfitterWindow = Place.Window("Outfitter's window");
            Place stand = Place.Aim(Whens.ArmourStand);
            K("Add to Cart", "OpenKey", Place.NoBuildMenu.Or(cartWindow));
            K("Bar None", "ReachKey", Place.Holding(Whens.NoBuildTool), "held, to reach through pieces", held: true);
            // On purpose with the inventory open too; it works while the module is switched off.
            K("Craft from Chests", "ToggleKey", Place.World.Also(Where.Inventory));
            K("Craft from Chests", "FillKey",
              Place.Aim(Whens.Smelter, Whens.HotTub, Whens.Fire, Whens.CookingStation, Whens.ShieldGenerator, Whens.Ballista),
              "held while pressing the game's use", held: true);
            // Held: read while a ghost shows, the inventory open too; as a toggle, the map open too.
            K("Grounded", "Key", Place.Holding(Whens.BuildTool).Also(Where.Inventory).Also(Where.Map), "held while placing a rug", held: true);
            K("Handyman", "OwnKey", Place.Holding(Whens.Hammer, Whens.OtherBuildTool), "while \"Key\" is OwnKey",
              onlyWhenKey: "Key", onlyWhenValue: "OwnKey");
            // Job 34's new module (55-B, 2026-10-10): not while typing, in a menu or the trader; the inventory and the map open too.
            K("Hoarstone", "ShowKey", Place.World.Also(Where.Inventory).Also(Where.Map));
            K("Item Stand Icons", "HoldKey", Place.Anywhere, "held, to show the marks", held: true);
            K("Outfitter", "WindowKey", Place.NoBuildMenu.Also(Where.Inventory).Or(outfitterWindow));
            K("Outfitter", "PickKey", Place.Inventory.At(Whens.Item), "held with a left click on a cell", held: true);
            // At an armour stand the game's use of E is skipped while any of these is held (Outfitter's StandKeysStopTheGamesE).
            K("Outfitter", "EquipKey", stand, overGame: true);
            K("Outfitter", "SaveKey", stand, overGame: true);
            K("Outfitter", "PersonalKey", stand, overGame: true);
            K("Outfitter", "PoseKey", stand, "while \"Change a marked stand's pose\" is on", overGame: true,
              onlyWhenKey: "ChangePose", onlyWhenValue: "true");
            K("Portion Control", "AllButOneKey", Place.OverInventory(Whens.SplitWindow));
            K("Portion Control", "MaxWeightKey", Place.OverInventory(Whens.SplitWindow));
            // A container in the parents (a chest, a cart's, a ship's); the game's opening is skipped while it is held.
            K("Reserve Stock", "Key", Place.Aim(Whens.AnyContainer), overGame: true);
            K("Spare Keys", "OpenKey", Place.NoBuildMenu.Or(Place.Window("Spare Keys' window")));   // this mod (Module.Name)
            // StoreStash picks one of its keys per press, the one with the most modifiers (its Keys.Pressed).
            K("StoreStash", "QuickStackKey", Place.World.Also(Where.Inventory));
            K("StoreStash", "PersonalKey", Place.World.Also(Where.Inventory));
            K("StoreStash", "MineKey", Place.Aim(Whens.AnyContainer).Or(Place.OverInventory(Whens.ChestOpen)));
            K("StoreStash", "OneStackKey", Place.Inventory.At(Whens.Item));   // a stack in his own grid
            K("StoreStash", "FavoriteModifier", Place.Inventory.At(Whens.Item), "held with a right-click, to mark a favourite", held: true);
            K("StoreStash", "TrashModifier", Place.Inventory.At(Whens.Item), "held with a right-click, to mark trash", held: true,
              onlyWhenKey: "TrashOn", onlyWhenValue: "true");
            // Each of these works only while its part's switch is on (in the same section as the key).
            K("StoreStash", "SetupKey", Place.Aim(Whens.AnyContainer).Or(Place.OverInventory(Whens.ChestOpen)),
              onlyWhenKey: "SetupOn", onlyWhenValue: "true");
            K("StoreStash", "SetupCopyKey", Place.OverInventory(Whens.ChestOpen), onlyWhenKey: "SetupOn", onlyWhenValue: "true");
            K("StoreStash", "SetupPasteKey", Place.OverInventory(Whens.ChestOpen), onlyWhenKey: "SetupOn", onlyWhenValue: "true");
            K("StoreStash", "SortKey", Place.Inventory, onlyWhenKey: "SortOn", onlyWhenValue: "true");
            // At a chest, or a chest open; with "Covers" every group in range, wherever he points.
            K("StoreStash", "OverflowKey", Place.World.Also(Where.Inventory), onlyWhenKey: "OverflowOn", onlyWhenValue: "true");
            // Terraformer's keys need a terrain tool's piece picked (the hoe's and the cultivator's), the build menu closed;
            // the keys held with the wheel or while flattening don't check the build menu.
            Place terrain = Place.NoBuildMenu.With(Whens.TerrainTool);
            Place terrainHeld = Place.Holding(Whens.TerrainTool);
            K("Terraformer", "RadiusKey", terrainHeld, "held with the wheel", held: true);
            K("Terraformer", "SharpnessKey", terrainHeld, "held with the wheel", held: true);
            K("Terraformer", "ResetKey", terrain);
            K("Terraformer", "UndoKey", terrain);
            K("Terraformer", "StraightenKey", terrain, "a square tool picked");
            for (int i = 1; i <= 3; i++)
            {
                K("Terraformer", "FlattenKey" + i, terrainHeld, "held while flattening", held: true);
            }
            // Job 35's new keys (56-B, 2026-10-10, as it sent them; read again when its build lands).
            K("Terraformer", "PaintOnlyKey", terrain, "held while painting", held: true);
            K("Terraformer", "TreePreviewKey", Place.NoBuildMenu.With(Whens.Cultivator), "held, a sapling picked", held: true);
            K("Terraformer", "PreviousTreeKey", Place.NoBuildMenu.With(Whens.Cultivator), "while the grown tree shows");
            K("Terraformer", "NextTreeKey", Place.NoBuildMenu.With(Whens.Cultivator), "while the grown tree shows");
            K("Terraformer", "WidthKey", terrainHeld, "held with the wheel", held: true);
            // Used only while "Key" is OwnKey; the game's own F (its "Key" GamePowerKey) is the game's button.
            K("PowerWheel", "OwnKey", Place.Walking.With(Whens.NoBuildTool), onlyWhenKey: "Key", onlyWhenValue: "OwnKey");
            // The game's use of E is skipped at its own kinds of thing while its key is held (Topper-Upper's Interact.Skip).
            K("Topper-Upper", "LightKey", Place.Aim(Whens.Fire, Whens.HotTub), overGame: true);
            K("Topper-Upper", "StationKey",
              Place.Aim(Whens.Smelter, Whens.Fermenter, Whens.ShieldGenerator, Whens.Oven, Whens.Ballista, Whens.Beehive, Whens.SapCollector),
              overGame: true);
            // At a chest it stands aside (for StoreStash's chest setup); while linking, anywhere but a chest ends the link.
            K("Topper-Upper", "LinkKey",
              Place.Aim(Whens.Smelter, Whens.Fermenter, Whens.ShieldGenerator, Whens.Oven, Whens.Ballista, Whens.Beehive, Whens.SapCollector)
                   .Or(Place.World.At(Whens.NotContainer)), "to link a station, and while linking to end it");
            // Use Case's Key: held over an item, or, with "When" set to ToggledByKey, pressed anywhere but while typing.
            Place overItem = Place.Inventory.At(Whens.Item).Or(Place.OverWorld(Whens.BuildMenu).At(Whens.Item));   // the build costs too
            // Not while typing: its check reads TextInput, which Jötunn's input block sets in the pack's own windows too.
            Place notTyping = Place.World.Also(Where.Inventory).Also(Where.Map).Also(Where.Menu);
            K("Use Case", "Key", overItem, "held", held: true, altKey: "When", altValue: "ToggledByKey", altPlace: notTyping);
            K("Use Case", "PinKey", notTyping);
            K("Use Case", "IgnoreKey", overItem, "while its panel shows");
            // Job 46's keys (68-B, 2026-10-11, card B25 and B26): at a tame or its saddle; the game's E there (pet, follow,
            // ride) is skipped while each is held (House Trained's InteractPatch and SaddleInteractPatch). The saddle's key
            // is None out of the box; "Return to base" works only while its switch is on, on a tame off a Hoarstone's
            // ground.
            K("House Trained", "FollowSaddleKey", Place.Aim("a tame"), "at a saddle", overGame: true);
            K("House Trained", "GroundReturnKey", Place.Aim("a tame"), "on a tame off a Hoarstone's ground", overGame: true,
              onlyWhenKey: "GroundReturn", onlyWhenValue: "true");
            // Job 45's key (69-B, 2026-10-11, card B24): Nap Time's second key, read as the player lies down on their own
            // bed (Bed.Interact, the game's E there): it picks a nap or the game's sleep for that lie-down, and the game's E
            // still lies them down, so the two are one press on purpose.
            K("Nap Time", "NapKey", Place.Aim("a bed"), "at your own bed, by day, to pick a nap", overGame: true);
        }

        /// <summary>
        /// One setting as a use; null when it holds no key (None, empty, or a value that isn't a key), when it is a pack
        /// key that doesn't count now (Handyman's own key while it uses the game's), or a type that never holds keys.
        /// <paramref name="unreadable"/> names a key the game can't read, for the log. <paramref name="whens"/>: the whens
        /// of other mods' keys (OtherMods' list, then his own in F1, so his wins).
        /// </summary>
        internal static Use FromSetting(SettingRecord record, out string unreadable, IList<Whens.Line> whens = null)
        {
            unreadable = null;
            Combo combo;
            switch (record.Type)
            {
                case "KeyboardShortcut":
                    combo = KeyText.Shortcut(record.Value, out unreadable);
                    break;
                case "KeyCode":
                    combo = KeyText.Single(record.Value, out unreadable);
                    break;
                case "String":
                    // Some mods keep a key as text: only a setting named like a key whose value reads as one counts, and only
                    // as maybe used.
                    if (!LooksLikeKeyName(record.Key))
                    {
                        return null;
                    }
                    combo = KeyText.Loose(record.Value);
                    break;
                default:
                    return null;
            }
            if (combo.IsNone)
            {
                return null;
            }
            Use use = Template(record, whens);
            if (use == null)
            {
                return null;
            }
            use.Combo = combo;
            return use;
        }

        /// <summary>
        /// One setting as a use without its key: who, what, its when and how it reads its key. For the setting a key is
        /// being picked for (the panel beside F1), whose key may not be set yet. Null for a type that never holds keys, or a
        /// pack key that doesn't count now.
        /// </summary>
        internal static Use Template(SettingRecord record, IList<Whens.Line> whens = null)
        {
            UseKind kind;
            switch (record.Type)
            {
                case "KeyboardShortcut":
                    kind = UseKind.Shortcut;
                    break;
                case "KeyCode":
                    kind = UseKind.KeyCodeSetting;
                    break;
                case "String":
                    kind = UseKind.TextSetting;
                    break;
                default:
                    return null;
            }
            bool inPack = record.PackModule != null;
            Known known = null;
            Whens.Line line = null;
            if (inPack)
            {
                pack.TryGetValue(record.PackModule + "\n" + record.Key, out known);
                if (known?.OnlyWhenKey != null && record.Sibling(known.OnlyWhenKey) != known.OnlyWhenValue)
                {
                    return null;
                }
            }
            else
            {
                line = Whens.Find(whens, record.PluginId, record.PluginName, record.Key, record.DisplayName);
                if (line != null && line.Place == null)
                {
                    return null;   // a setting its mod never reads
                }
            }
            bool alt = known?.AltKey != null && record.Sibling(known.AltKey) == known.AltValue;
            Place place = alt ? known.AltPlace : known?.Place ?? line?.Place ?? Place.Anywhere;
            return new Use
            {
                OwnerId = inPack ? "pack:" + record.PackModule : record.PluginId,
                Owner = inPack ? record.PackModule : record.PluginName,
                Group = inPack ? Use.PackGroup : record.PluginId,
                What = string.IsNullOrEmpty(record.DisplayName) ? record.Key : record.DisplayName,
                Section = record.Section ?? string.Empty,
                Detail = record.Key,
                Combo = Combo.None,
                Place = place,
                When = known != null ? (alt ? place.Words : WithNote(place, known.Note)) : line != null ? place.Words : string.Empty,
                Known = known != null || line != null,
                Kind = kind,
                Maybe = kind == UseKind.TextSetting,
                Held = known?.Held == true && !alt,
                // How a mod reads its key decides whether it fires with more held: the pack's modules check only the keys
                // they name (MODULES.md's modules all read "main key down and the listed modifiers held"), a KeyCode setting
                // is read with GetKeyDown on its own; another mod's shortcut is taken to use BepInEx's own check.
                Loose = inPack || kind == UseKind.KeyCodeSetting,
                OverGame = known?.OverGame == true,
                SwitchedOff = inPack && !record.ModuleOn,
            };
        }

        // "held with the wheel, a terrain tool in hand": the table's note, then the when in words.
        private static string WithNote(Place place, string note) => string.IsNullOrEmpty(note) ? place.Words : note + ", " + place.Words;

        /// <summary>A text setting named like a key ("ToggleKey", "Hotkey", "Bind"): the only text settings read.</summary>
        internal static bool LooksLikeKeyName(string key)
        {
            string lower = (key ?? string.Empty).ToLowerInvariant();
            return lower.Contains("key") || lower.Contains("hotkey") || lower.Contains("shortcut") || lower.Contains("bind") || lower.Contains("button");
        }

        /// <summary>The pack module a setting belongs to: in the bundle its section is the module's name, or "&lt;Name&gt; -
        /// &lt;part&gt;" (the shared sections, "Chests (every mod)" and the like, belong to none); a single release's plugin
        /// is the module. Null for a setting of any other mod.</summary>
        internal static string PackModuleOf(string pluginId, string pluginName, string section)
        {
            if (string.Equals(pluginId, BundleId, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(section) || section.EndsWith("(every mod)", StringComparison.Ordinal))
                {
                    return null;
                }
                int dash = section.IndexOf(" - ", StringComparison.Ordinal);
                return dash > 0 ? section.Substring(0, dash) : section;
            }
            return pluginId != null && pluginId.StartsWith("modprojects.", StringComparison.OrdinalIgnoreCase) ? pluginName : null;
        }

        /// <summary>
        /// A Jötunn button (named "button!mod id" in the game's own table, its gamepad side "Joy!button!mod id") as a use
        /// of its mod, unless one of the mod's settings already holds that key (most Jötunn buttons are backed by a
        /// setting: counted once). One with no setting behind it is usually a key of the mod's own window (XPortal's Up and
        /// Down Arrow, read only while its portal panel shows), and when isn't known: it counts as maybe used, as a key
        /// found in a mod's code does, never a clash. Its modifiers are lost (Jötunn registers a shortcut by its main key),
        /// so it counts as the key alone.
        /// </summary>
        internal static Use FromJotunnButton(string name, string path, Func<string, string> pluginName, ICollection<Use> settingUses)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            string bare = name.StartsWith(JotunnGamepad, StringComparison.Ordinal) ? name.Substring(JotunnGamepad.Length) : name;
            int bang = bare.LastIndexOf('!');
            if (bang <= 0 || bang == bare.Length - 1)
            {
                return null;
            }
            int key = KeyCatalog.IdOfPath(path);
            if (key < 0)
            {
                return null;
            }
            string button = bare.Substring(0, bang);
            string modId = bare.Substring(bang + 1);
            if (settingUses.Any(use => string.Equals(use.OwnerId, modId, StringComparison.OrdinalIgnoreCase) && use.Main == key))
            {
                return null;
            }
            string owner = pluginName(modId) ?? modId;
            return new Use
            {
                OwnerId = modId,
                Owner = owner,
                Group = modId,
                What = WithoutPrefix(button, owner),
                Section = "Jötunn buttons",
                Detail = name,
                Combo = new Combo(key, null),
                Kind = UseKind.JotunnButton,
                Maybe = true,
                Loose = true,
            };
        }

        /// <summary>How Jötunn names a button's gamepad side in the game's table (InputManager.RegisterButton).</summary>
        internal const string JotunnGamepad = "Joy!";

        // "XPortal_DropdownScrollUp" of XPortal reads "DropdownScrollUp": the mod's own name is said beside it anyway.
        private static string WithoutPrefix(string button, string owner)
        {
            string prefix = (owner ?? string.Empty).Replace(" ", string.Empty) + "_";
            return prefix.Length > 1 && button.Length > prefix.Length && button.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? button.Substring(prefix.Length)
                : button;
        }

        /// <summary>His console binds ("bind F7 …", the game's Terminal.m_binds): each runs its commands at the key's press
        /// whatever else is held, while he isn't typing.</summary>
        internal static Use FromConsoleBind(KeyCode key, IEnumerable<string> commands)
        {
            int id = KeyCatalog.IdOf(key);
            if (id < 0)
            {
                return null;
            }
            string what = string.Join("; ", commands ?? Enumerable.Empty<string>());
            return new Use
            {
                OwnerId = Use.ConsoleOwner,
                Owner = "Console binds",
                Group = Use.ConsoleOwner,
                What = what,
                Section = "Console binds",
                Detail = "bind " + key + " " + what,
                Combo = new Combo(id, null),
                // The game runs a bind whenever chat isn't focused and the console is closed (Chat.Update): with the
                // inventory, the map, the pause menu or a mod's window open too.
                Place = Place.World.Also(Where.Inventory).Also(Where.Map).Also(Where.Menu, Whens.PauseMenu).Also(Where.Window),
                When = "except while typing in chat or the console",
                Known = true,
                Kind = UseKind.ConsoleBind,
                Loose = true,
            };
        }

        /// <summary>The pack's own windows' fixed keys (F for a search box), for modules that are there.</summary>
        internal static IEnumerable<Use> PackFixed(Func<string, bool> moduleThere)
        {
            foreach (string[] each in new[]
            {
                new[] { "Add to Cart", "Search the window", "Add to Cart's window" },
                new[] { "Outfitter", "Search the drawer", "Outfitter's window" },
            })
            {
                if (moduleThere(each[0]))
                {
                    yield return new Use
                    {
                        OwnerId = "pack:" + each[0],
                        Owner = each[0],
                        Group = Use.PackGroup,
                        What = each[1],
                        Section = "Its window",
                        Detail = "a fixed key",
                        Combo = new Combo((int)KeyCode.F, null),
                        Place = Place.Window(each[2]),
                        When = each[2],
                        Known = true,
                        Kind = UseKind.PackFixed,
                        Loose = true,
                    };
                }
            }
        }

        /// <summary>A key found in a mod's code (the code scan): maybe used, wherever. A Shift, Ctrl or Alt only held
        /// (GetKey) is taken as held with something else.</summary>
        internal static Use FromCode(string pluginId, string pluginName, KeyCode key, bool held, string where)
        {
            int id = KeyCatalog.IdOf(key);
            if (id < 0)
            {
                return null;
            }
            return new Use
            {
                OwnerId = pluginId,
                Owner = pluginName,
                Group = pluginId,
                What = held ? "Held, read in its code" : "Read in its code",
                Section = "In its code",
                Detail = where,
                Combo = new Combo(id, null),
                Kind = UseKind.Code,
                Maybe = true,
                Held = held && KeyCatalog.IsModifier(id),
                Loose = true,
            };
        }
    }
}
