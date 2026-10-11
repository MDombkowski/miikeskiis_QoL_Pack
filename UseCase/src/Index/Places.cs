using System;
using System.Collections.Generic;
using System.Linq;
using QoLMods.Catalogue;
using UnityEngine;

namespace UseCase
{
    /// <summary>
    /// The places inside the world's locations that take an item: boss altars and locked dungeon doors (the guardian
    /// stones are prefabs of the scene, read live by GameReader). The world loads a location only near a player (its prefab
    /// is a soft reference), so the game's own are a fixed list for 1.0.16, read from its location prefabs (below), and
    /// every altar or locked door is also learned as it loads near him: seen, for the spoiler rule, and, when another
    /// mod's, added to the index.
    /// </summary>
    internal static class Places
    {
        private readonly struct Fixed
        {
            internal readonly UseKind Kind;
            internal readonly string Location;
            internal readonly string ItemPrefab;
            internal readonly int Count;
            internal readonly string Boss;
            internal readonly string SeenKey;
            internal readonly string Token;
            internal readonly string Note;

            internal Fixed(UseKind kind, string location, string itemPrefab, int count, string boss, string seenKey, string token, string note = "")
            {
                Kind = kind;
                Location = location;
                ItemPrefab = itemPrefab;
                Count = count;
                Boss = boss;
                SeenKey = seenKey;
                Token = token;
                Note = note;
            }
        }

        // The game's own, 1.0.16: read from each location prefab's OfferingBowl and Door (and each altar's item stands) with
        // the data library's offline reader, and the doors' key rules from the decompiled Door (session 25-B, 2026-09-30;
        // the table and its sources: the vault's working\2026-09-30_use-case-plan.md). A place's seen key is how Loaded names it: an
        // altar by its bowl's name and object, a door by its prefab. Names and defeat keys are read from the game when the
        // index is made, so they follow his language and the game's own data; an item this game doesn't have leaves its
        // line out. Moder's, Yagluth's and Fader's altars take one item on each of their stands (3, 5, 3; Yagluth's bowl
        // says 3, but the stands decide). The Deep North's outer bowl takes 3 Malicious Blood to open the way into The Prison,
        // before the inner one summons its boss. The Intricate Key's other gate (Morkborg_gate) is placed nowhere in 1.0.16.
        private static readonly Fixed[] GameOwn =
        {
            new Fixed(UseKind.Summoning, "Eikthyrnir", "TrophyDeer", 2, "Eikthyr", "bowl:$piece_offerbowl_eikthyr@offeraltar_deer", "$enemy_eikthyr"),
            new Fixed(UseKind.Summoning, "GDKing", "AncientSeed", 3, "gd_king", "bowl:$prop_eldersummoningbowl_name@fire_button", "$enemy_gdking"),
            new Fixed(UseKind.Summoning, "Bonemass", "WitheredBone", 10, "Bonemass", "bowl:$piece_offerbowl_bonemass@offeraltar", "$enemy_bonemass"),
            new Fixed(UseKind.Summoning, "Dragonqueen", "DragonEgg", 3, "Dragon", "bowl:$prop_dragonsummoningbowl_name@offeraltar_dragon", "$enemy_dragon"),
            new Fixed(UseKind.Summoning, "GoblinKing", "GoblinTotem", 5, "GoblinKing", "bowl:$piece_offerbowl_yagluth@offeraltar_goblinking", "$enemy_goblinking"),
            new Fixed(UseKind.Summoning, "FaderLocation", "Bell", 3, "Fader", "bowl:$piece_offerbowl_fader@offeraltar_fader", "$enemy_fader"),
            new Fixed(UseKind.LockedDoors, "DN_Bossroom", "HatefulBlood", 3, "", "bowl:$piece_offerbowl_frozenking@offeraltar_FrozenKing", "$location_dnbossroomnew"),
            new Fixed(UseKind.Summoning, "DN_Bossroom", "HatefulBlood", 3, "FrozenKing", "bowl:$piece_offerbowl_frozenking@offeraltar_FrozenKing_bossroom", "$enemy_frozenking"),
            new Fixed(UseKind.LockedDoors, "SunkenCrypt4", "CryptKey", -1, "", "door:sunken_crypt_gate", "$piece_irongate"),
            new Fixed(UseKind.LockedDoors, "Mistlands_DvergrBossEntrance1", "DvergrKey", -1, "", "door:dungeon_queen_door", "$piece_queendoor"),
            new Fixed(UseKind.LockedDoors, "MorkBorg", "BloodGoldKey", -1, "", "door:Morkhalla_jotun_gate", "$piece_jotungate", "used up"),
        };

        private static readonly List<PlaceFact> learned = new List<PlaceFact>();

        /// <summary>The game's own places, as facts for this world (names and keys read from the prefabs it has).</summary>
        internal static List<PlaceFact> Facts()
        {
            var facts = new List<PlaceFact>();
            ObjectDB db = ObjectDB.instance;
            ZNetScene scene = ZNetScene.instance;
            int order = 0;
            foreach (Fixed place in GameOwn)
            {
                GameObject itemPrefab = db != null ? db.GetItemPrefab(place.ItemPrefab) : null;
                ItemDrop item = itemPrefab != null ? itemPrefab.GetComponent<ItemDrop>() : null;
                if (item == null)
                {
                    continue;   // not in this game (a later update renamed it): its line is left out
                }
                Character boss = place.Boss.Length > 0 && scene != null && scene.GetPrefab(place.Boss) is GameObject bossPrefab ? bossPrefab.GetComponent<Character>() : null;
                string itemName = GameReader.NameOf(item);
                facts.Add(new PlaceFact
                {
                    Kind = place.Kind,
                    Token = boss != null && place.Kind == UseKind.Summoning ? boss.m_name : place.Token,
                    Prefab = place.Location,
                    Item = itemName,
                    Count = place.Count,
                    Note = place.Note,
                    SeenKey = place.SeenKey,
                    BeatenKey = boss != null ? boss.m_defeatSetGlobalKey ?? string.Empty : string.Empty,
                    AlwaysKnown = place.Kind == UseKind.GuardianStones,
                    Sort = new Listing
                    {
                        Shelf = Shelf.Take,
                        Key = "place:" + place.Location + ":" + place.ItemPrefab,
                        Name = GameFacts.Localize(place.Token),
                        Biome = Biomes.LabelOf(itemName),
                        GameOrder = order++,
                    },
                });
            }
            return facts;
        }

        /// <summary>An altar has loaded near him.</summary>
        internal static void Loaded(OfferingBowl bowl)
        {
            if (bowl == null)
            {
                return;
            }
            string key = "bowl:" + bowl.m_name + "@" + Utils.GetPrefabName(bowl.gameObject);
            Learn(key, () =>
            {
                Character boss = bowl.m_bossPrefab != null ? bowl.m_bossPrefab.GetComponent<Character>() : null;
                return new PlaceFact
                {
                    Kind = UseKind.Summoning,
                    Token = boss != null ? boss.m_name : bowl.m_name,
                    Prefab = Utils.GetPrefabName(bowl.gameObject),
                    Item = bowl.m_bossItem != null ? GameReader.NameOf(bowl.m_bossItem) : string.Empty,   // stands' altars: seen only
                    Count = bowl.m_useItemStands ? -1 : bowl.m_bossItems,
                    SeenKey = key,
                    BeatenKey = boss != null ? boss.m_defeatSetGlobalKey ?? string.Empty : string.Empty,
                };
            });
        }

        /// <summary>A locked door has loaded near him.</summary>
        internal static void Loaded(Door door)
        {
            if (door == null || door.m_keyItem == null)
            {
                return;
            }
            string key = "door:" + Utils.GetPrefabName(door.gameObject);
            Learn(key, () => new PlaceFact
            {
                Kind = UseKind.LockedDoors,
                Token = door.m_name,
                Prefab = Utils.GetPrefabName(door.gameObject),
                Item = GameReader.NameOf(door.m_keyItem),
                Note = door.m_consumeKey ? "used up" : string.Empty,
                SeenKey = key,
            });
        }

        /// <summary>A new index (a new world, or the recipes changed): the places learned this session go back in.</summary>
        internal static void Relearn(UseIndex index)
        {
            foreach (PlaceFact place in learned)
            {
                index.Learn(place);
            }
        }

        private static void Learn(string key, Func<PlaceFact> read)
        {
            string character = CharacterName();
            if (character.Length > 0)
            {
                Seen.Learn(character, key, message => Module.Log.LogWarning(message));
            }
            if (GameOwn.Any(place => place.SeenKey == key) || learned.Any(place => place.SeenKey == key))
            {
                return;
            }
            PlaceFact fact = read();
            if (fact.Item.Length == 0)
            {
                return;
            }
            fact.Sort = new Listing { Shelf = Shelf.Take, Key = "place:" + key, Name = GameFacts.Localize(fact.Token), Biome = Biomes.LabelOf(fact.Item), GameOrder = 1000 + learned.Count };
            learned.Add(fact);
            Index.Current?.Learn(fact);
        }

        private static string CharacterName()
        {
            if (Player.m_localPlayer != null)
            {
                return Player.m_localPlayer.GetPlayerName() ?? string.Empty;
            }
            PlayerProfile profile = global::Game.instance != null ? global::Game.instance.GetPlayerProfile() : null;
            return profile?.GetName() ?? string.Empty;
        }
    }
}
