// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System.Collections.Generic;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>
    /// Which of his items a quick stack may put away (card A52): only from the game's own rows of his inventory, never what
    /// he wears or holds (a hand item the game has put away for a moment too) or has in his hand, never a quest item, the
    /// hotbar and his favourites as the rules say, trophies as the rules say.
    /// </summary>
    internal static class StackItems
    {
        /// <summary>The name of the character's row count among his unique keys, as the game keeps it
        /// (Player.InventoryRowsKey).</summary>
        internal const string RowsKey = "invrows";

        /// <summary>The rows of his inventory that are the game's own: the character's row count (4, more once bought from
        /// a trader: Player.SetInventorySize), never more than the inventory's height. Rows below it belong to other mods
        /// (EquipmentAndQuickSlots keeps its equipment and quick slots there) and are never touched.</summary>
        internal static int GameRows(Player player)
        {
            Inventory inventory = player.GetInventory();
            int rows = 4;
            if (player.TryGetUniqueKeyValue(RowsKey, out string value) && int.TryParse(value, out int kept))
            {
                rows = Mathf.Clamp(kept, 1, 9);
            }
            return Mathf.Min(rows, inventory.GetHeight());
        }

        /// <summary>His items the rules let go, row by row from the top, left to right (the order a quick stack offers
        /// them to the chests).</summary>
        internal static List<ItemDrop.ItemData> Choose(Player player, StackRules rules)
        {
            int rows = rules.AllRows ? player.GetInventory().GetHeight() : GameRows(player);
            var chosen = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (item != null && MayGo(item, rows, ChestHooks.Worn(player, item), ChestHooks.InHisHand(item), rules))
                {
                    chosen.Add(item);
                }
            }
            chosen.Sort((a, b) => a.m_gridPos.y != b.m_gridPos.y ? a.m_gridPos.y.CompareTo(b.m_gridPos.y) : a.m_gridPos.x.CompareTo(b.m_gridPos.x));
            return chosen;
        }

        /// <summary>
        /// Whether one of his items may go: in the game's own rows (<paramref name="gameRows"/>), not worn
        /// (<paramref name="equipped"/>, or its own mark), not in his hand, picked (StackRules.Pick), the hotbar row by
        /// StackRules.Hotbar, a favourite (StackRules.Keep) only from the hotbar with HotbarRule.EverythingButWorn, and a
        /// trophy unless TrophyRule.Never.
        /// </summary>
        internal static bool MayGo(ItemDrop.ItemData item, int gameRows, bool equipped, bool inHand, StackRules rules)
        {
            if (item.m_stack <= 0 || item.m_gridPos.y < 0 || item.m_gridPos.y >= gameRows || equipped || item.m_equipped || inHand)
            {
                return false;
            }
            // The game never lets a quest item into a chest (InventoryGui refuses the drag, Humanoid.DropItem the drop).
            if (item.m_shared.m_questItem)
            {
                return false;
            }
            if (rules.Pick != null && !rules.Pick(item))
            {
                return false;
            }
            bool hotbar = item.m_gridPos.y == 0;
            if (hotbar && rules.Hotbar == HotbarRule.Off)
            {
                return false;
            }
            bool kept = rules.Keep != null && rules.Keep(item);
            if (kept && !(hotbar && rules.Hotbar == HotbarRule.EverythingButWorn))
            {
                return false;
            }
            return !(IsTrophy(item) && rules.Trophies == TrophyRule.Never);
        }

        internal static bool IsTrophy(ItemDrop.ItemData item) => item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy;
    }
}
