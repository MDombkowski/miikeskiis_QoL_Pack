using System.Collections.Generic;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// The Hoarstone's entry in the hammer's menu, right after the game's Ward, put in and taken out by reference whenever
    /// the switches say so (checked every frame, so a change in F1 shows at once), as Terraformer puts its tools in the
    /// hoe's menu. The menu of a hammer in his hand is refreshed as the game refreshes its own: newly known pieces learnt,
    /// the tab kept, the piece he had picked kept (the Ward, if it was the Hoarstone), an open build menu redrawn.
    /// </summary>
    internal static class Menu
    {
        internal static void Sync()
        {
            bool wanted = Module.IsOn && Settings.InHammerMenu.Value;
            if (!wanted && Stone.Entry == null)
            {
                return;
            }
            if (wanted && !Stone.Make())
            {
                return;
            }
            Stone.FollowSettings();
            PieceTable table = Hammer();
            if (table == null || table.m_pieces == null)
            {
                return;
            }
            bool there = table.m_pieces.Contains(Stone.Entry);
            if (wanted == there)
            {
                return;
            }
            if (wanted)
            {
                table.m_pieces.Insert(PlaceFor(table), Stone.Entry);
            }
            else
            {
                table.m_pieces.Remove(Stone.Entry);
            }
            Refresh(table);
        }

        internal static void TakeOut()
        {
            PieceTable table = Stone.Entry != null ? Hammer() : null;
            if (table != null && table.m_pieces != null && table.m_pieces.Remove(Stone.Entry))
            {
                Refresh(table);
            }
        }

        private static PieceTable Hammer()
        {
            GameObject hammer = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab("Hammer") : null;
            return hammer != null ? hammer.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
        }

        // Right after the game's Ward; at the end if it isn't there.
        private static int PlaceFor(PieceTable table)
        {
            for (int i = 0; i < table.m_pieces.Count; i++)
            {
                if (table.m_pieces[i] != null && table.m_pieces[i].name == Records.WardPrefab)
                {
                    return i + 1;
                }
            }
            return table.m_pieces.Count;
        }

        private static void Refresh(PieceTable changed)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            PieceTable held = player.GetBuildTool();
            Piece picked = held == changed ? held.GetSelectedPiece() : null;
            GameHooks.UpdateKnownRecipesList(player);
            if (held != changed)
            {
                return;
            }
            player.SetBuildCategory(held.GetSelectedCategory());
            if (picked != null)
            {
                if (picked.gameObject == Stone.Entry && !held.m_pieces.Contains(Stone.Entry))
                {
                    picked = Stone.WardPiece;
                }
                if (picked != null)
                {
                    player.SetSelectedPiece(picked);
                }
            }
            if (Hud.IsPieceSelectionVisible() && Hud.instance != null && Hud.instance.m_buildUi != null)
            {
                Hud.instance.m_buildUi.OpenBuildMenu();
            }
        }
    }
}
