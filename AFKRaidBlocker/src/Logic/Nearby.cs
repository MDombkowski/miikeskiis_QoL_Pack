using System.Collections.Generic;

namespace AFKRaidBlocker
{
    /// <summary>Another player as the presence rule sees him: where he stands from the away player, on the ground plane,
    /// and whether his own game says he's away (false for a player without the mod, or one too far to be loaded).</summary>
    internal struct Other
    {
        internal float Dx;
        internal float Dz;
        internal bool Away;
    }

    /// <summary>
    /// His rule, "the blocker should only block raids when all players in an area are AFK" (card A98). A raid that
    /// starts on a player reaches 96 m around him (every raid's m_eventRange), so while a player who isn't away stands
    /// within the setting's distance of the away player, the away player's raids aren't held off: a raid can start on
    /// him as without the mod, and reach the player who is still playing. A player without the mod counts as there.
    /// </summary>
    internal static class Nearby
    {
        /// <summary>Every raid's reach in the game, from its centre (RandomEvent.m_eventRange, 96 for all 31).</summary>
        internal const int RaidReach = 96;

        /// <summary>The farthest the setting goes: at the game's usual simulation distance, other players are loaded at
        /// least this far along each axis (2 zones of 64 m beyond his own).</summary>
        internal const int MostMetres = 128;

        /// <summary>Once someone counts as there, he still counts until this much farther, so a player working right at
        /// the edge doesn't switch the hold on and off.</summary>
        internal const float Margin = 4f;

        /// <summary>True when someone within the distance, measured along the ground, isn't away. 0 metres: nobody counts.
        /// With wasThere (someone counted the frame before), the distance stretches by the margin.</summary>
        internal static bool AnyoneThere(List<Other> others, float metres, bool wasThere = false)
        {
            if (metres <= 0f || others == null)
            {
                return false;
            }
            float reach = wasThere ? metres + Margin : metres;
            float most = reach * reach;
            foreach (Other other in others)
            {
                if (!other.Away && other.Dx * other.Dx + other.Dz * other.Dz <= most)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
