using System.Collections.Generic;
using UnityEngine;

namespace Outfitter.Stands
{
    /// <summary>
    /// The game's own armour stand behind <see cref="IStand"/> (ArmorStand, read fresh from the game 1.0.17 on 2026-10-06).
    /// Only the hammer's stand counts (its prefab "ArmorStand"): the mannequins and other mods' stands are left alone. Every
    /// write is the game's own: the three keys of a place ("{i}_item", "{i}_variant", "{i}_itemData" through
    /// ItemDrop.SaveToZDO), the stand's own messages (RPC_SetVisualItem, RPC_SetPose, RPC_RequestOwn), and ZDOMan's send.
    /// </summary>
    internal sealed class GameStand : IStand
    {
        /// <summary>The prefab of the hammer's armour stand: the only stand that counts.</summary>
        internal const string Prefab = "ArmorStand";

        internal readonly ArmorStand Stand;
        private readonly ZNetView view;

        private GameStand(ArmorStand stand, ZNetView view)
        {
            Stand = stand;
            this.view = view;
        }

        /// <summary>The stand behind an armour stand, or null when it doesn't count (not the hammer's, or no record).</summary>
        internal static GameStand Of(ArmorStand stand)
        {
            if (stand == null || stand.m_slots == null || stand.m_slots.Count == 0)
            {
                return null;
            }
            ZNetView view = stand.m_netViewOverride != null ? stand.m_netViewOverride : stand.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || Utils.GetPrefabName(stand.gameObject) != Prefab)
            {
                return null;
            }
            return new GameStand(stand, view);
        }

        public object Key => Stand;

        public bool Valid => Stand != null && view != null && view.IsValid();

        public bool IsOwner => Valid && view.IsOwner();

        public bool HasOwner => Valid && view.HasOwner();

        public long Owner => Valid ? view.GetZDO().GetOwner() : 0L;

        public void RequestOwn()
        {
            if (Valid)
            {
                view.InvokeRPC("RPC_RequestOwn");
            }
        }

        public void TakeOwnership()
        {
            if (Valid)
            {
                view.ClaimOwnership();
            }
        }

        public Vector3 Position => Stand.transform.position;

        /// <summary>The record's data revision (every write to it moves it on), for what is worked out from the record
        /// (StandShow.Cached).</summary>
        internal long Revision => Valid ? view.GetZDO().DataRevision : 0L;

        public int Places => Stand.m_slots.Count;

        private static string Slot(int place, string what) => place + what;

        private ZDO Record => view.GetZDO();

        public bool Has(int place) => Valid && place >= 0 && place < Places && Record.GetInt(Slot(place, "_item")) != 0;

        public ItemDrop.ItemData Read(int place)
        {
            if (!Has(place) || ObjectDB.instance == null)
            {
                return null;
            }
            ZDO record = Record;
            GameObject prefab = ObjectDB.instance.GetItemPrefab(record.GetInt(Slot(place, "_item")));
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return null;   // a removed mod's item: it stays on the stand
            }
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            byte[] data = record.GetByteArray(Slot(place, "_itemData"));
            if (data != null && data.Length > 2)
            {
                ItemDrop.LoadFromZDO(item, record, place);   // as the game's take does (ArmorStand.DropItem)
            }
            else
            {
                item.m_variant = record.GetInt(Slot(place, "_variant"));   // a record from before items were kept whole: as shown
            }
            item.m_equipped = false;   // the game saves its copy before taking it off him; marked so, the game would put it on at his next load
            if (item.m_stack < 1)
            {
                item.m_stack = 1;
            }
            return item;
        }

        public void Write(int place, ItemDrop.ItemData item)
        {
            ItemDrop.ItemData copy = item.Clone();
            copy.m_stack = 1;
            copy.m_equipped = false;
            ZDO record = Record;
            record.Set(Slot(place, "_item"), item.m_dropPrefab.name.GetStableHashCode());
            record.Set(Slot(place, "_variant"), copy.m_variant);
            ItemDrop.SaveToZDO(copy, record, place);
        }

        public void Clear(int place) => Record.Set(Slot(place, "_item"), 0);

        public void Show(int place)
        {
            int hash = Record.GetInt(Slot(place, "_item"));
            view.InvokeRPC(ZNetView.Everybody, "RPC_SetVisualItem", place, hash, hash == 0 ? 0 : Record.GetInt(Slot(place, "_variant")));
        }

        public int FitFor(ItemDrop.ItemData item)
        {
            if (item == null || item.m_dropPrefab == null)
            {
                return -1;
            }
            return StandFit.First(Types(), item.m_shared.m_itemType, item.m_shared.m_attachOverride, HasAttachPoint(item.m_dropPrefab));
        }

        public int FitForPrefab(string prefab)
        {
            GameObject found = string.IsNullOrEmpty(prefab) || ObjectDB.instance == null ? null : ObjectDB.instance.GetItemPrefab(prefab);
            ItemDrop drop = found != null ? found.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return -1;
            }
            return StandFit.First(Types(), drop.m_itemData.m_shared.m_itemType, drop.m_itemData.m_shared.m_attachOverride, HasAttachPoint(found));
        }

        private List<IList<ItemDrop.ItemData.ItemType>> Types()
        {
            var types = new List<IList<ItemDrop.ItemData.ItemType>>(Stand.m_slots.Count);
            foreach (ArmorStand.ArmorStandSlot slot in Stand.m_slots)
            {
                types.Add(slot.m_supportedTypes);
            }
            return types;
        }

        // The game's model test (ArmorStand.UseItem): a direct child named "attach" or "attach_skin".
        internal static bool HasAttachPoint(GameObject prefab)
        {
            Transform root = prefab.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                string name = root.GetChild(i).gameObject.name;
                if (name == "attach" || name == "attach_skin")
                {
                    return true;
                }
            }
            return false;
        }

        public string GetString(string key) => Valid ? Record.GetString(key) : string.Empty;

        public long GetLong(string key) => Valid ? Record.GetLong(key) : 0L;

        public void SetString(string key, string value) => Record.Set(key, value ?? string.Empty);

        public void SetLong(string key, long value) => Record.Set(key, value);

        public void Send()
        {
            if (Valid && ZDOMan.instance != null)
            {
                ZDOMan.instance.ForceSendZDO(Record.m_uid);
            }
        }

        public bool MayUse(bool onlyBuiltByMe, bool flash)
        {
            if (!Valid || !PrivateArea.CheckAccess(Position, 0f, flash))
            {
                return false;
            }
            if (!onlyBuiltByMe)
            {
                return true;
            }
            Piece piece = Stand.GetComponent<Piece>();
            return piece != null && piece.IsCreator();
        }

        public int Pose => Valid ? Record.GetInt(ZDOVars.s_pose) : 0;

        public int PoseCount => Stand.m_poseCount;

        public void SetPose(int pose)
        {
            if (Valid)
            {
                view.InvokeRPC(ZNetView.Everybody, "RPC_SetPose", pose);
            }
        }
    }
}
