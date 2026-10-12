// The QoL Mods list picker, a part: the one copy to edit is Games\Valheim\QoLMods\parts\ListPicker\. Every mod that uses
// it carries a copy in its own parts\ListPicker\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using QoLMods.Catalogue;
using UnityEngine;

namespace QoLMods.ListPicker
{
    /// <summary>
    /// The game's things a list can hold, read from the game the way the pack's windows read them, once per item list
    /// (ObjectDB) and scene (ZNetScene), and again once a world is loaded after the main menu (biomes and the scene's
    /// creatures come only then):
    /// pieces, every piece of every build tool the game has (the hammer's first, in each tool's own order), but its tool
    /// actions (repair, remove), terrain actions and items set down as they are, as Add to Cart's Build tab leaves them out;
    /// items, one per item (its canonical prefab, the game's copies left out), each with a picture; creatures, every
    /// character of the world's scene but the player, pictured by its trophy; and the game's item types. Whether his
    /// character has found each is asked again at each opening (Refresh). Pieces and items are there at the main menu (the
    /// game copies its item list there); creatures only in a world.
    /// </summary>
    internal static class GameThings
    {
        private static ObjectDB readFor;
        private static int readCount = -1;
        private static ZNetScene sceneFor;
        private static bool hadScene;

        private static readonly List<Thing> Pieces = new List<Thing>();
        private static readonly List<Thing> Items = new List<Thing>();
        private static readonly List<Thing> Creatures = new List<Thing>();
        private static readonly List<Thing> Types = new List<Thing>();
        private static readonly Dictionary<string, Thing> Extras = new Dictionary<string, Thing>(StringComparer.Ordinal);
        private static readonly Dictionary<string, HashSet<string>> InputsByStation = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        /// <summary>Goes up each time the game's things are read again, so a window drawn from them rebuilds.</summary>
        internal static int Version { get; private set; }

        /// <summary>Whether a kind can be read now; else why not, in a few words for the Pick button.</summary>
        internal static bool Ready(ThingKind kind, out string why)
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null || db.m_items == null || db.m_items.Count == 0)
            {
                why = "once the game has loaded";
                return false;
            }
            if (kind == ThingKind.Creatures && ZNetScene.instance == null)
            {
                why = "in a world";
                return false;
            }
            why = null;
            return true;
        }

        /// <summary>The things of a kind (empty while it can't be read: Ready).</summary>
        internal static List<Thing> Of(ThingKind kind)
        {
            Read();
            switch (kind)
            {
                case ThingKind.Pieces:
                    return Pieces;
                case ThingKind.Items:
                    return Items;
                case ThingKind.Creatures:
                    return Creatures;
                default:
                    return Types;
            }
        }

        // Reads everything again when the game's item list or the scene is a new one (a new world, or a world after the
        // main menu). Cheap: one pass over each list.
        private static void Read()
        {
            ObjectDB db = ObjectDB.instance;
            ZNetScene scene = ZNetScene.instance;
            if (db == null || db.m_items == null)
            {
                return;
            }
            bool hasScene = scene != null;
            if (db == readFor && db.m_items.Count == readCount && scene == sceneFor && hasScene == hadScene)
            {
                return;
            }
            readFor = db;
            readCount = db.m_items.Count;
            sceneFor = scene;
            hadScene = hasScene;
            Version++;
            Pieces.Clear();
            Items.Clear();
            Creatures.Clear();
            Types.Clear();
            Extras.Clear();
            InputsByStation.Clear();
            GameFacts.Prepare();   // the catalogue's facts, and the biomes once in a world
            ReadPieces(db);
            ReadItems(db);
            if (scene != null)
            {
                ReadCreatures(scene);
            }
            ReadTypes();
        }

        private static void ReadPieces(ObjectDB db)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int tool = 0;
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                PieceTable table = drop?.m_itemData?.m_shared?.m_buildPieces;
                if (table == null || table.m_pieces == null)
                {
                    continue;
                }
                // Progression's ties go by each tool's own order, the hammer's first (Add to Cart's Catalogue).
                int order = prefab.name == "Hammer" ? 0 : ++tool * 10000;
                for (int index = 0; index < table.m_pieces.Count; index++)
                {
                    GameObject piecePrefab = table.m_pieces[index];
                    Piece piece = piecePrefab != null ? piecePrefab.GetComponent<Piece>() : null;
                    if (piece == null || piece.m_repairPiece || piece.m_removePiece || piecePrefab.GetComponent<TerrainOp>() != null ||
                        piecePrefab.GetComponent<ItemDrop>() != null || !seen.Add(piecePrefab.name))
                    {
                        continue;
                    }
                    try
                    {
                        Pieces.Add(PieceThing(piecePrefab, piece, order + index, false));
                    }
                    catch (Exception)
                    {
                        // Another mod's odd piece leaves only itself out.
                    }
                }
            }
        }

        private static Thing PieceThing(GameObject prefab, Piece piece, int order, bool extra)
        {
            Listing listing = Listings.Piece("piece:" + prefab.name, prefab, piece, order);
            var thing = new Thing
            {
                Kind = ThingKind.Pieces,
                Name = prefab.name,
                Token = piece.m_name,
                Shown = Shown(piece.m_name, prefab.name),
                Icon = piece.m_icon,
                Prefab = prefab,
                Piece = piece,
                Listing = listing,
                Biome = listing.Biome,
                Extra = extra,
            };
            listing.Name = thing.Shown;
            string made = listing.Piece != null && listing.Piece.Material.Length > 0 ? " · made of " + listing.Piece.Material : string.Empty;
            string categories = listing.Piece != null ? string.Join(", ", listing.Piece.Categories) : string.Empty;
            thing.Where = Join(categories + made, listing.Biome);
            return thing;
        }

        private static void ReadItems(ObjectDB db)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameObject prefab in db.m_items)
            {
                ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                ItemDrop.ItemData.SharedData shared = drop?.m_itemData?.m_shared;
                if (shared == null || string.IsNullOrEmpty(shared.m_name) || shared.m_icons == null || shared.m_icons.Length == 0)
                {
                    continue;
                }
                // One per item: its canonical prefab (the catalogue's), the game's copies (FW_, SP_ …) left out.
                GameObject canonical = GameFacts.Canonical(shared.m_name) ?? prefab;
                if (canonical != prefab || !seen.Add(shared.m_name))
                {
                    continue;
                }
                try
                {
                    Items.Add(ItemThing(prefab, drop, false));
                }
                catch (Exception)
                {
                    // Another mod's odd item leaves only itself out.
                }
            }
        }

        private static Thing ItemThing(GameObject prefab, ItemDrop drop, bool extra)
        {
            ItemDrop.ItemData.SharedData shared = drop.m_itemData.m_shared;
            string shown = Shown(shared.m_name, prefab.name);
            Listing listing = Listings.Item(Shelf.Take, "item:" + prefab.name, shown, shared.m_name, null, string.Empty, 1);
            var thing = new Thing
            {
                Kind = ThingKind.Items,
                Name = prefab.name,
                Token = shared.m_name,
                Shown = shown,
                Icon = shared.m_icons != null && shared.m_icons.Length > 0 ? shared.m_icons[0] : null,
                Prefab = prefab,
                Item = drop.m_itemData,
                Type = shared.m_itemType,
                Listing = listing,
                Biome = listing.Biome,
                Extra = extra,
            };
            string section = listing.Section.Length > 0 && listing.Section != listing.Biome ? " › " + listing.Section : string.Empty;
            string sub = listing.Sub.Length > 0 ? " › " + listing.Sub : string.Empty;
            thing.Where = Join(listing.Family + section + sub, listing.Biome);
            return thing;
        }

        private static void ReadCreatures(ZNetScene scene)
        {
            if (scene.m_prefabs == null)
            {
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameObject prefab in scene.m_prefabs)
            {
                Character character = prefab != null ? prefab.GetComponent<Character>() : null;
                if (character == null || prefab.GetComponent<Player>() != null || !seen.Add(prefab.name))
                {
                    continue;
                }
                try
                {
                    Creatures.Add(CreatureThing(prefab, character));
                }
                catch (Exception)
                {
                    // Another mod's odd creature leaves only itself out.
                }
            }
        }

        private static Thing CreatureThing(GameObject prefab, Character character)
        {
            // Its picture and biome: its trophy's, else its first drop's biome (the catalogue knows every item's).
            Sprite icon = null;
            string from = null;
            CharacterDrop drops = prefab.GetComponent<CharacterDrop>();
            if (drops != null && drops.m_drops != null)
            {
                foreach (CharacterDrop.Drop each in drops.m_drops)
                {
                    ItemDrop item = each?.m_prefab != null ? each.m_prefab.GetComponent<ItemDrop>() : null;
                    ItemDrop.ItemData.SharedData shared = item?.m_itemData?.m_shared;
                    if (shared == null)
                    {
                        continue;
                    }
                    if (shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy && shared.m_icons != null && shared.m_icons.Length > 0)
                    {
                        icon = shared.m_icons[0];
                        from = shared.m_name;
                        break;
                    }
                    from = from ?? shared.m_name;
                }
            }
            string biome = from != null ? Biomes.LabelOf(from) : Taxonomy.OtherBiome;
            string shown = Shown(character.m_name, prefab.name);
            bool tame = prefab.GetComponent<Tameable>() != null;
            var thing = new Thing
            {
                Kind = ThingKind.Creatures,
                Name = prefab.name,
                Token = character.m_name,
                Shown = shown,
                Icon = icon,
                Prefab = prefab,
                Creature = character,
                Tame = tame,
                Boss = character.m_boss,
                Biome = biome,
                Listing = new Listing
                {
                    Shelf = Shelf.Take,
                    Key = "creature:" + prefab.name,
                    Name = shown,
                    Biome = biome,
                    Search = (shown + " " + prefab.name + " " + biome + (tame ? " tame" : string.Empty) + (character.m_boss ? " boss" : string.Empty)).ToLowerInvariant(),
                },
            };
            thing.Where = Join(character.m_boss ? "Boss" : tame ? "Can be tamed" : "Creature", biome);
            return thing;
        }

        // The game's item types, as Topper-Upper's "Never pick up" names them, each pictured by its first item in the
        // pack's order.
        private static void ReadTypes()
        {
            foreach (ItemDrop.ItemData.ItemType type in Enum.GetValues(typeof(ItemDrop.ItemData.ItemType)))
            {
                if (type == ItemDrop.ItemData.ItemType.None)
                {
                    continue;
                }
                Thing first = null;
                foreach (Thing item in Items)
                {
                    if (item.Type == type && (first == null || Browse.CompareProgression(item.Listing, first.Listing) < 0))
                    {
                        first = item;
                    }
                }
                string name = type.ToString();
                string shown = TypeLabel(type);
                Types.Add(new Thing
                {
                    Kind = ThingKind.ItemTypes,
                    Name = name,
                    Shown = shown,
                    Icon = first?.Icon,
                    Type = type,
                    Listing = new Listing
                    {
                        Shelf = Shelf.Take,
                        Key = "type:" + name,
                        Name = shown,
                        GameOrder = (int)type,
                        Search = (shown + " " + name).ToLowerInvariant(),
                    },
                    Where = "Every item of this type" + (first != null ? ", as " + first.Shown : string.Empty),
                });
            }
        }

        /// <summary>An item type in words, after the game's own name for it.</summary>
        internal static string TypeLabel(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                    return "One-handed weapon";
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                    return "Two-handed weapon";
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                    return "Two-handed weapon (left)";
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                    return "Ammo (not equipped)";
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                    return "Atgeir";
                default:
                    return type.ToString();
            }
        }

        /// <summary>
        /// A thing the window doesn't list but an entry names, found by its exact prefab name so the list shows its
        /// picture: a piece in no build menu (the scene's own, an old variant), a copy of an item, a creature. Null when
        /// the game has none of that name.
        /// </summary>
        internal static Thing Extra(ThingKind kind, string name)
        {
            if (string.IsNullOrEmpty(name) || kind == ThingKind.ItemTypes)
            {
                return null;
            }
            Read();
            string key = kind + "\n" + name;
            if (Extras.TryGetValue(key, out Thing known))
            {
                return known;
            }
            Thing thing = null;
            try
            {
                switch (kind)
                {
                    case ThingKind.Pieces:
                        GameObject piecePrefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
                        Piece piece = piecePrefab != null ? piecePrefab.GetComponent<Piece>() : null;
                        if (piece != null)
                        {
                            thing = PieceThing(piecePrefab, piece, 900000, true);
                        }
                        break;
                    case ThingKind.Items:
                        GameObject itemPrefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(name) : null;
                        ItemDrop drop = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
                        if (drop?.m_itemData?.m_shared != null)
                        {
                            thing = ItemThing(itemPrefab, drop, true);
                        }
                        break;
                    default:
                        GameObject creaturePrefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : null;
                        Character character = creaturePrefab != null ? creaturePrefab.GetComponent<Character>() : null;
                        if (character != null)
                        {
                            thing = CreatureThing(creaturePrefab, character);
                            thing.Extra = true;
                        }
                        break;
                }
            }
            catch (Exception)
            {
                thing = null;
            }
            Extras[key] = thing;
            return thing;
        }

        /// <summary>Asks again whether his character has found each thing (the setting "Show things you haven't found
        /// yet" greys or hides the rest): a piece he can build, an item he has had or can make. With no character loaded
        /// (the main menu), everything counts as found; creatures always do.</summary>
        internal static void Refresh(IEnumerable<Thing> things)
        {
            Player player = Player.m_localPlayer;
            foreach (Thing thing in things)
            {
                try
                {
                    switch (thing.Kind)
                    {
                        case ThingKind.Pieces:
                            thing.Found = player == null || thing.Piece == null || player.IsRecipeKnown(thing.Piece.m_name);
                            break;
                        case ThingKind.Items:
                            thing.Found = player == null || thing.Token == null || player.IsMaterialKnown(thing.Token) || player.IsRecipeKnown(thing.Token);
                            break;
                        default:
                            thing.Found = true;
                            break;
                    }
                }
                catch (Exception)
                {
                    thing.Found = true;
                }
            }
        }

        /// <summary>
        /// What a station takes, by its prefab's name: what its conversions start from (a smelter, kiln, windmill,
        /// spinning wheel, fermenter or cooking station), its fuels (the shield generator) or its ammo (the ballista); the
        /// prefab names of those items. Found in the build menus' pieces, else the world's scene. Empty for a station the
        /// game doesn't have.
        /// </summary>
        internal static HashSet<string> Inputs(string station)
        {
            Read();
            if (InputsByStation.TryGetValue(station ?? string.Empty, out HashSet<string> known))
            {
                return known;
            }
            var inputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                GameObject prefab = null;
                foreach (Thing piece in Pieces)
                {
                    if (piece.Name == station)
                    {
                        prefab = piece.Prefab;
                        break;
                    }
                }
                if (prefab == null && ZNetScene.instance != null)
                {
                    prefab = ZNetScene.instance.GetPrefab(station);
                }
                if (prefab != null)
                {
                    Smelter smelter = prefab.GetComponent<Smelter>();
                    if (smelter != null && smelter.m_conversion != null)
                    {
                        foreach (Smelter.ItemConversion each in smelter.m_conversion)
                        {
                            Add(inputs, each?.m_from);
                        }
                    }
                    Fermenter fermenter = prefab.GetComponent<Fermenter>();
                    if (fermenter != null && fermenter.m_conversion != null)
                    {
                        foreach (Fermenter.ItemConversion each in fermenter.m_conversion)
                        {
                            Add(inputs, each?.m_from);
                        }
                    }
                    CookingStation cooking = prefab.GetComponent<CookingStation>();
                    if (cooking != null && cooking.m_conversion != null)
                    {
                        foreach (CookingStation.ItemConversion each in cooking.m_conversion)
                        {
                            Add(inputs, each?.m_from);
                        }
                    }
                    ShieldGenerator shield = prefab.GetComponent<ShieldGenerator>();
                    if (shield != null && shield.m_fuelItems != null)
                    {
                        foreach (ItemDrop each in shield.m_fuelItems)
                        {
                            Add(inputs, each);
                        }
                    }
                    Turret turret = prefab.GetComponent<Turret>();
                    if (turret != null && turret.m_allowedAmmo != null)
                    {
                        foreach (Turret.AmmoType each in turret.m_allowedAmmo)
                        {
                            Add(inputs, each.m_ammo);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // What was read stands; a station another mod changed oddly gives what could be read.
            }
            if (Settled(inputs))
            {
                InputsByStation[station ?? string.Empty] = inputs;
            }
            return inputs;
        }

        // An empty answer is kept only once the build menus could be read (it may be asked before the game has loaded).
        private static bool Settled(HashSet<string> inputs) => inputs.Count > 0 || Pieces.Count > 0;

        private static void Add(HashSet<string> inputs, ItemDrop item)
        {
            if (item != null)
            {
                inputs.Add(item.gameObject.name);
            }
        }

        // A $ token in his language; the prefab's name where the game has no text for it.
        private static string Shown(string token, string prefab)
        {
            string shown = GameFacts.Localize(token);
            return string.IsNullOrEmpty(shown) || shown.StartsWith("$", StringComparison.Ordinal) || shown.StartsWith("[", StringComparison.Ordinal)
                ? prefab
                : shown;
        }

        private static string Join(string what, string biome) =>
            string.IsNullOrEmpty(biome) || biome == Taxonomy.OtherBiome ? what : what + " · " + biome;
    }
}
