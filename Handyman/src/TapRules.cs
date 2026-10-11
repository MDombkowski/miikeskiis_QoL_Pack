using System;
using System.Collections.Generic;

namespace Handyman
{
    /// <summary>What the mod does for a tap or a hold of the key.</summary>
    internal enum Act
    {
        Nothing,

        /// <summary>Pick the tool's Repair (or, where allowed, the serving tray's Remove).</summary>
        PickRepair,

        /// <summary>Pick Repair and close the build menu, as the menu's own hammer button does.</summary>
        PickRepairAndCloseMenu,

        /// <summary>Pick again the piece the tool had before Repair, turned as it was.</summary>
        BackToPiece,

        /// <summary>Put the tool away, as the game's Hide key does.</summary>
        PutAway,

        /// <summary>Open the build menu.</summary>
        OpenMenu,

        /// <summary>Nothing to go back to, and nothing to do but say so.</summary>
        NoPieceYet,
    }

    /// <summary>
    /// Card A44's rules for what a tap or a hold does, and which of a tool's pieces is its Repair, kept apart from the
    /// game so they can be checked outside it.
    /// </summary>
    internal static class TapRules
    {
        /// <summary>What a tap does.</summary>
        /// <param name="menuOpenAtPress">The build menu was open when the key went down (KeyWatch only makes a tap of
        /// such a press when "With the build menu open" says to pick Repair and close the menu).</param>
        /// <param name="repairPicked">Repair (or the tray's Remove) is the piece picked right now.</param>
        /// <param name="hasPieceToGoBackTo">The tool has had another piece picked this session.</param>
        internal static Act OnTap(bool menuOpenAtPress, bool repairPicked, AgainChoice again, bool hasPieceToGoBackTo, NoPieceChoice noPiece)
        {
            if (menuOpenAtPress)
            {
                return Act.PickRepairAndCloseMenu;
            }
            if (!repairPicked)
            {
                return Act.PickRepair;
            }
            switch (again)
            {
                case AgainChoice.BackToMyPiece:
                    if (hasPieceToGoBackTo)
                    {
                        return Act.BackToPiece;
                    }
                    switch (noPiece)
                    {
                        case NoPieceChoice.PutHammerAway:
                            return Act.PutAway;
                        case NoPieceChoice.OpenBuildMenu:
                            return Act.OpenMenu;
                        default:
                            return Act.NoPieceYet;
                    }
                case AgainChoice.PutHammerAway:
                    return Act.PutAway;
                default:
                    return Act.Nothing;
            }
        }

        /// <summary>What a hold does: put the tool away, as the game's Hide key would have at the press.</summary>
        internal static Act OnHold() => Act.PutAway;

        /// <summary>
        /// The piece the key picks among a tool's available pieces, or null when the key should leave this tool to the
        /// game: its Repair (the game's hammer has one, first in its table), if "Tools" allows this tool; otherwise, for a
        /// tool with no Repair, its Remove (the serving tray's), only if "Serving tray picks its Remove" is on.
        /// </summary>
        internal static T KeyPiece<T>(IEnumerable<T> available, Func<T, bool> isRepair, Func<T, bool> isRemove, bool isHammer,
            ToolsChoice tools, bool trayRemove) where T : class
        {
            T repair = null;
            T remove = null;
            foreach (T piece in available)
            {
                if (piece == null)
                {
                    continue;
                }
                if (repair == null && isRepair(piece))
                {
                    repair = piece;
                }
                else if (remove == null && isRemove(piece))
                {
                    remove = piece;
                }
            }
            if (repair != null)
            {
                return tools == ToolsChoice.EveryToolWithRepair || isHammer ? repair : null;
            }
            return trayRemove ? remove : null;
        }
    }
}
