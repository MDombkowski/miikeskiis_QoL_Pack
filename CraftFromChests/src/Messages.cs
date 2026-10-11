using System.Collections.Generic;
using QoLMods.Chests;

namespace CraftFromChests
{
    /// <summary>What he's told when the chests can't give what something needs, in the middle of the screen, as the
    /// game's own messages are.</summary>
    internal static class Messages
    {
        /// <summary>Says why the chests couldn't give <paramref name="what"/> ("the materials"), from what the request
        /// met: a chest someone has open, a chest that wasn't his in time or that his game can't get (which item, how far,
        /// and why: ChestExplain, card A69), or simply too little; and writes the chests and their situations to the log.</summary>
        internal static void Short(Request request, string what)
        {
            string why;
            string explained = null;
            Player player = Player.m_localPlayer;
            if (request != null && request.Finished && !request.Cancelled && player != null)
            {
                List<ShortItem> items = ChestExplain.Shortfall(Module.Chests, request.Left, player.transform.position, request.Rules);
                ChestExplain.Log(Module.Chests, $"Getting {what} from the chests", items);
                explained = ChestExplain.ShortReason(items);
            }
            if (request == null)
            {
                why = "the chests nearby don't hold enough";
            }
            else if (!request.Finished)
            {
                why = "a chest's owner hasn't answered yet";
            }
            else if (request.InUse > 0)
            {
                why = request.InUse == 1 ? "a chest with some of it is open by someone else" : $"{request.InUse} chests with some of it are open by someone else";
            }
            else if (explained != null)
            {
                why = explained;
            }
            else if (request.NoAnswer > 0)
            {
                why = "a chest wasn't handed to you in time";
            }
            else if (request.NoRoom)
            {
                why = "your inventory has no room for it from a chest a friend has open";
            }
            else if (Supply.WaitedOnHolds(request.Left))
            {
                why = "someone else is about to open a chest with some of it; try again in a few seconds";
            }
            else
            {
                why = "the chests nearby don't hold enough";
            }
            Center($"Couldn't get {what} from the chests: {why}.");
        }

        /// <summary>Says why a craft the chests were asked for didn't happen as its bar ended: what the request met, or,
        /// when every chest had answered and they held it all, that one of them was opened or reserved by someone else
        /// meanwhile (either hands the chest to him), or reserved by this player, or else that the chests or what he
        /// carries changed.</summary>
        internal static void Refused(Request request, string what)
        {
            if (request == null || !request.Finished || request.Cancelled || !request.Covered)
            {
                Short(request, what);
            }
            else if (request.TakenOver > 0)
            {
                Center("Couldn't craft it: someone else opened or reserved a chest it was using during the craft. Press Craft again.");
            }
            else if (request.ReservedSince > 0)
            {
                Center("Couldn't craft it: a chest it was using was reserved during the craft. Press Craft again.");
            }
            else
            {
                Center("Couldn't craft it: what you carry or the chests changed during the craft. Press Craft again.");
            }
        }

        internal static void Center(string text)
        {
            Player player = Player.m_localPlayer;
            if (player != null)   // Unity's own null test: a destroyed player isn't null to ?.
            {
                player.Message(MessageHud.MessageType.Center, text);
            }
        }
    }
}
