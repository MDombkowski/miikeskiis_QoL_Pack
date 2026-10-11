using System.Collections.Generic;
using UnityEngine;

namespace Outfitter.Stands
{
    /// <summary>
    /// An armour stand as the stands' code sees it: its record (the places, the mark), its owner, and the game's messages to
    /// it. The game's own stand behind it in play (<see cref="GameStand"/>); a fake in the scratch harness, which can't run
    /// the engine. Nothing here checks who owns the stand: the callers change it only in a claim's frame
    /// (<see cref="StandClaims"/>), as the game's own code changes it only as its owner.
    /// </summary>
    internal interface IStand
    {
        /// <summary>The same stand from one frame to the next (the game's ArmorStand), for holds and caches.</summary>
        object Key { get; }

        /// <summary>Its record is there: not destroyed, not unloaded.</summary>
        bool Valid { get; }

        bool IsOwner { get; }

        bool HasOwner { get; }

        /// <summary>The owner's session ID (0: nobody).</summary>
        long Owner { get; }

        /// <summary>Asks its owner to hand it over (the game's RPC_RequestOwn, which hands it to whoever asks).</summary>
        void RequestOwn();

        /// <summary>Takes it, as the server (single player, or the host) may: only the server hands ownership out.</summary>
        void TakeOwnership();

        Vector3 Position { get; }

        /// <summary>How many places its record has (the hammer's stand lists its seven twice: only the first seven are
        /// ever filled, by the game's rule, <see cref="FitFor"/>).</summary>
        int Places { get; }

        /// <summary>Whether a place holds an item ("{i}_item" isn't 0).</summary>
        bool Has(int place);

        /// <summary>A new item read from a place's record (unequipped, at least one), or null when the place is empty or
        /// this game has no item by its prefab (a removed mod's): that one stays on the stand.</summary>
        ItemDrop.ItemData Read(int place);

        /// <summary>Writes an item into a place as the game's attach does: a copy, one of it, unequipped (its prefab, its
        /// style, the whole item). Shows nothing yet (<see cref="Show"/>).</summary>
        void Write(int place, ItemDrop.ItemData item);

        /// <summary>Empties a place as the game's take does ("{i}_item" 0).</summary>
        void Clear(int place);

        /// <summary>Tells everyone to show a place as its record says now (the game's RPC_SetVisualItem).</summary>
        void Show(int place);

        /// <summary>The place the game would hang an item in (the first whose types take it; anything but chest and leg
        /// armour needs an attach point on its model), whether or not it's free; -1 when the stand takes no such item.</summary>
        int FitFor(ItemDrop.ItemData item);

        /// <summary>The same for a loadout's item, by its prefab name; -1 when this game has no such item.</summary>
        int FitForPrefab(string prefab);

        string GetString(string key);

        long GetLong(string key);

        void SetString(string key, string value);

        void SetLong(string key, long value);

        /// <summary>Sends its record to everyone at once (ZDOMan.ForceSendZDO).</summary>
        void Send();

        /// <summary>Whether he may use it: the game's ward check (flashing the ward that refuses, when asked, as the game's
        /// own does), and (when asked) only a stand he built.</summary>
        bool MayUse(bool onlyBuiltByMe, bool flash);

        int Pose { get; }

        int PoseCount { get; }

        /// <summary>Turns it to a pose, for everyone (the game's RPC_SetPose; its owner keeps it).</summary>
        void SetPose(int pose);
    }

    /// <summary>
    /// The game's rule for which place of an armour stand takes an item (ArmorStand.UseItem and CanAttach, 1.0.17): the
    /// first place whose supported types take the item's attach type (its m_attachOverride when set, else its type; a place
    /// with no types takes anything), and, for anything but chest and leg armour, a model with an "attach" or "attach_skin"
    /// child. The game refuses when that place is full; it never looks further. Pure, so the scratch harness reaches it.
    /// </summary>
    internal static class StandFit
    {
        /// <summary>The place, or -1.</summary>
        internal static int First(IList<IList<ItemDrop.ItemData.ItemType>> places, ItemDrop.ItemData.ItemType type,
                                  ItemDrop.ItemData.ItemType attachOverride, bool hasAttachPoint)
        {
            if (type != ItemDrop.ItemData.ItemType.Legs && type != ItemDrop.ItemData.ItemType.Chest && !hasAttachPoint)
            {
                return -1;
            }
            ItemDrop.ItemData.ItemType attach = attachOverride != ItemDrop.ItemData.ItemType.None ? attachOverride : type;
            for (int i = 0; i < places.Count; i++)
            {
                IList<ItemDrop.ItemData.ItemType> takes = places[i];
                if (takes == null || takes.Count == 0 || takes.Contains(attach))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
