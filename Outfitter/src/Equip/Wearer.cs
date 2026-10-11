using Outfitter.Loadouts;
using QoLMods.Chests;
using UnityEngine;

namespace Outfitter.Equip
{
    /// <summary>
    /// The player as the equip step sees him: his inventory, what he wears, and putting things on and off. The game's own
    /// Player behind it in play (<see cref="PlayerWearer"/>); a fake in the scratch harness, which can't run the engine.
    /// </summary>
    internal interface IWearer
    {
        Inventory Inventory { get; }

        /// <summary>The shape of his inventory now (GameHooks.Shape in play).</summary>
        GridShape Grid { get; }

        long PlayerId { get; }

        /// <summary>Where he stands (the chest service's own seam, ChestFinder.Where).</summary>
        Vector3 Position { get; }

        /// <summary>What he wears an item as now; None when it isn't worn (a hand item the game put away counts as not
        /// worn here: see <see cref="HiddenRight"/>).</summary>
        WornAs WornAs(ItemDrop.ItemData item);

        ItemDrop.ItemData RightItem { get; }

        ItemDrop.ItemData LeftItem { get; }

        ItemDrop.ItemData HiddenRight { get; }

        ItemDrop.ItemData HiddenLeft { get; }

        /// <summary>The player this equip began for is gone: died and respawned as a new object, or logged out (R1, finding
        /// 1). Everything else about him is then unreadable: an equip ends.</summary>
        bool Gone { get; }

        /// <summary>Whether the game would put gear on now (not attacking, dodging, swimming off the ground, or in the middle
        /// of eating or the game's own equipping); false once he's gone.</summary>
        bool CanEquipNow { get; }

        /// <summary>Whether he is in no state for an equip to begin at all (gone, dead, teleporting, in a cutscene, dragging
        /// an item in the inventory window).</summary>
        bool Unavailable { get; }

        float CarryWeight { get; }

        float MaxCarryWeight { get; }

        /// <summary>Puts an item on at once (Humanoid.EquipItem); true when it's worn afterwards.</summary>
        bool Equip(ItemDrop.ItemData item, bool effects);

        /// <summary>Puts an item on the game's own way (its queue, with its animation); true when queued or worn.</summary>
        bool QueueEquip(ItemDrop.ItemData item);

        /// <summary>Takes an item off (Humanoid.UnequipItem); a hand item the game put away is let go of too.</summary>
        void Unequip(ItemDrop.ItemData item, bool effects);

        /// <summary>Drops any equip or unequip of this item waiting in the game's queue.</summary>
        void ClearQueued(ItemDrop.ItemData item);

        bool Queued(ItemDrop.ItemData item);
    }

    /// <summary>The game's own player, as the equip step needs him.</summary>
    internal sealed class PlayerWearer : IWearer
    {
        private readonly Player player;

        internal PlayerWearer(Player player)
        {
            this.player = player;
        }

        public Inventory Inventory => player.GetInventory();

        public GridShape Grid => GameHooks.Shape(player);

        public long PlayerId => player.GetPlayerID();

        public Vector3 Position => ChestFinder.Where(player);

        public WornAs WornAs(ItemDrop.ItemData item) => Capture.WornOf(player, item);

        public ItemDrop.ItemData RightItem => player.RightItem;

        public ItemDrop.ItemData LeftItem => player.LeftItem;

        public ItemDrop.ItemData HiddenRight => GameHooks.HiddenRight(player);

        public ItemDrop.ItemData HiddenLeft => GameHooks.HiddenLeft(player);

        // Unity's own null check first: a destroyed player's Animator throws.
        public bool Gone => player == null || Player.m_localPlayer != player;

        public bool CanEquipNow => !Gone && !player.InAttack() && !player.InDodge() && !(player.IsSwimming() && !player.IsOnGround()) &&
                                   !player.InMinorAction();

        public bool Unavailable => Gone || player.IsDead() || player.IsTeleporting() || player.InCutscene() || GameHooks.Dragging();

        public float CarryWeight => player.GetInventory().GetTotalWeight();

        public float MaxCarryWeight => player.GetMaxCarryWeight();

        public bool Equip(ItemDrop.ItemData item, bool effects)
        {
            if (item == null || Gone || (!item.IsEquipable() && item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.AmmoNonEquipable))
            {
                return false;
            }
            if (!player.IsItemEquiped(item))
            {
                player.EquipItem(item, effects);
            }
            return player.IsItemEquiped(item);
        }

        public bool QueueEquip(ItemDrop.ItemData item)
        {
            // The game's own way uses an item: a food or mead would be eaten (R1, finding 4). Only what can be worn.
            if (item == null || Gone || (!item.IsEquipable() && item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.AmmoNonEquipable))
            {
                return false;
            }
            if (player.IsItemEquiped(item))
            {
                return true;
            }
            if (player.IsEquipActionQueued(item))
            {
                return true;
            }
            if (item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable)
            {
                // The game's own way never puts bait on (Player.ToggleEquipped refuses what IsEquipable doesn't take), and
                // has no animation for it: on at once (R2, finding 12).
                return Equip(item, true);
            }
            // The game's own way: what a right-click in the inventory does (Player.ToggleEquipped through UseItem).
            player.UseItem(player.GetInventory(), item, fromInventoryGui: true);
            return player.IsItemEquiped(item) || player.IsEquipActionQueued(item);
        }

        public void Unequip(ItemDrop.ItemData item, bool effects)
        {
            if (item != null)
            {
                player.UnequipItem(item, effects);   // also lets go of it as a hand item the game put away
            }
        }

        public void ClearQueued(ItemDrop.ItemData item)
        {
            // RemoveEquipAction removes one action of any kind for the item (a reload too); at most a few are ever queued.
            for (int i = 0; i < 4 && item != null && player.IsEquipActionQueued(item); i++)
            {
                player.RemoveEquipAction(item);
            }
        }

        public bool Queued(ItemDrop.ItemData item) => item != null && player.IsEquipActionQueued(item);
    }
}
