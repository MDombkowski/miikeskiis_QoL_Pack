using System;
using Splatform;

namespace MapViewRules
{
    /// <summary>
    /// Whose a pin is. A friend's pin, read from a cartography table, keeps the friend's player ID as its owner until the
    /// player claims it with a click, which sets the owner to none; its author, the friend's platform ID, stays with it for
    /// good, saved and shared (the game's AddSharedMapData, GetMapData, SetMapData). A pin placed on this computer has no
    /// owner, and no author or the player's own.
    /// </summary>
    internal static class Players
    {
        /// <summary>A friend's pin: an owner, or an author who isn't this player.</summary>
        internal static bool ByAnother(Minimap.PinData pin)
        {
            if (pin.m_ownerID != 0L)
            {
                return true;
            }
            if (!pin.m_author.IsValid)
            {
                return false;
            }
            PlatformUserID local = LocalUser();
            return local.IsValid && pin.m_author != local;
        }

        private static PlatformUserID LocalUser()
        {
            try
            {
                return PlatformManager.DistributionPlatform.LocalUser.PlatformUserID;
            }
            catch (Exception)
            {
                return default;   // unknown: only the owner then tells a friend's pin
            }
        }
    }
}
