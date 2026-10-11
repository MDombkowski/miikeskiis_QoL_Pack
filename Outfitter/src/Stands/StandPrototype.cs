using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ItemType = ItemDrop.ItemData.ItemType;

namespace Outfitter.Stands
{
    /// <summary>
    /// The game's armour stand as its prefab has it (ArmorStand on "ArmorStand", read from ZNetScene, never one in the
    /// world): its places and what each takes, for the window, which shows what a stand would show of a loadout with no
    /// stand near (his words, 2026-10-07: "a hidden section ... that shows the Armor Stand's slots and what each slot is
    /// currently set to be occupied by"). Read once a scene; nothing when the prefab isn't there (another mod removed it).
    /// </summary>
    internal static class StandPrototype
    {
        private static object readFor;
        private static bool read;
        private static readonly List<IList<ItemType>> Types = new List<IList<ItemType>>();
        private static readonly List<int> Distinct = new List<int>();

        // Reads the prefab's places, once a scene.
        private static void Read()
        {
            object scene = ZNetScene.instance;
            if (read && ReferenceEquals(scene, readFor))
            {
                return;
            }
            readFor = scene;
            read = scene != null;
            Types.Clear();
            Distinct.Clear();
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(GameStand.Prefab) : null;
            ArmorStand stand = prefab != null ? prefab.GetComponent<ArmorStand>() : null;
            if (stand == null || stand.m_slots == null)
            {
                return;
            }
            foreach (ArmorStand.ArmorStandSlot slot in stand.m_slots)
            {
                Types.Add(slot?.m_supportedTypes ?? new List<ItemType>());
            }
            // The game's stand lists its seven places twice: a place taking the same types as one before it never takes
            // anything (the first that fits wins), so only the first of each is listed (review 1's m1).
            for (int i = 0; i < Types.Count; i++)
            {
                bool again = false;
                for (int j = 0; j < i && !again; j++)
                {
                    again = Types[j].Count == Types[i].Count && Types[j].All(Types[i].Contains);
                }
                if (!again)
                {
                    Distinct.Add(i);
                }
            }
        }

        /// <summary>The places an item can ever go to, each once, in the stand's order.</summary>
        internal static IReadOnlyList<int> Listed
        {
            get
            {
                Read();
                return Distinct;
            }
        }

        /// <summary>How many places a stand has (0: none known).</summary>
        internal static int Places
        {
            get
            {
                Read();
                return Types.Count;
            }
        }

        /// <summary>The place a stand would hang an item of this prefab in, as GameStand.FitForPrefab finds it; -1 for none.</summary>
        internal static int FitForPrefab(string prefab)
        {
            Read();
            if (Types.Count == 0)
            {
                return -1;
            }
            GameObject found = string.IsNullOrEmpty(prefab) || ObjectDB.instance == null ? null : ObjectDB.instance.GetItemPrefab(prefab);
            ItemDrop drop = found != null ? found.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return -1;
            }
            return StandFit.First(Types, drop.m_itemData.m_shared.m_itemType, drop.m_itemData.m_shared.m_attachOverride, GameStand.HasAttachPoint(found));
        }

        /// <summary>A place's name, by what it takes: "Helmet", "Chest", "Legs", "Cape", "Belt", "Shield (on its back)",
        /// "Weapon or tool (on its back)".</summary>
        internal static string NameOf(int place)
        {
            Read();
            return place >= 0 && place < Types.Count ? NameOf(Types[place]) : "?";
        }

        /// <summary>The same, by the types a place takes.</summary>
        internal static string NameOf(IList<ItemType> takes)
        {
            if (takes == null || takes.Count == 0)
            {
                return "Anything";
            }
            if (takes.Count > 1)
            {
                return "Weapon or tool (on its back)";
            }
            switch (takes[0])
            {
                case ItemType.Helmet:
                    return "Helmet";
                case ItemType.Chest:
                    return "Chest";
                case ItemType.Legs:
                    return "Legs";
                case ItemType.Shoulder:
                    return "Cape";
                case ItemType.Utility:
                    return "Belt";
                case ItemType.Shield:
                    return "Shield (on its back)";
                default:
                    return takes[0].ToString();
            }
        }
    }
}
