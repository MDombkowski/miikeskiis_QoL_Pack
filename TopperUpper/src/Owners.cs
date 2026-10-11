using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// Whose game feeds a light or station (the research's 2.1 to 2.3, and section 9's last part). Only the game that looks
    /// after it (owns its record) feeds it; one a friend's game without the mod looks after is fed the way a hand would
    /// only with "Feed them as a hand would" (off out of the box), and only by one game of those near. And
    /// "stand aside while the server feeds a station": a station in his area whose owner turns to nobody was written by a
    /// mod on the server (ServersideQoL's AutoProcess sets the owner to nobody before writing a station); it's left alone
    /// for a minute, so the two don't write it at the same instant (a guess at the server's timing; on out of the box).
    /// </summary>
    internal static class Owners
    {
        /// <summary>How long a station the server took over is left alone, in seconds.</summary>
        internal const float StandAsideFor = 60f;

        /// <summary>A thing fed the way a hand would waits this long, in seconds, before the next, so the friend's game's
        /// record of it has come back before its room is worked out again.</summary>
        internal const float HandEvery = 10f;

        /// <summary>For a light or station this game doesn't look after: whether to feed it the way a hand would now, by
        /// message to the friend's game that does. Notes why not on it.</summary>
        internal static bool MayFeedByHand(Fed fed, float range, float now)
        {
            long owner = fed.Zdo.GetOwner();
            if (owner == 0L)
            {
                fed.Why = "No game looks after it yet";
                return false;
            }
            if (Hello.RunsTheMod(owner))
            {
                fed.Why = "A friend's game with Topper-Upper looks after it";
                return false;
            }
            if (Settings.Friends.Value != FriendsWithout.FeedThemAsAHandWould)
            {
                fed.Why = "A friend's game looks after it";
                return false;
            }
            if (now - fed.HandFedAt < HandEvery)
            {
                return false;
            }
            if (!Hello.ChosenToFeedByHand(fed.Position, range))
            {
                fed.Why = "Another player near feeds it";
                return false;
            }
            return true;
        }

        /// <summary>From the loop, for each station it passes: notes a station in his area whose owner just turned to
        /// nobody (on a server, with "Stand aside while the server feeds a station" on).</summary>
        internal static void Watch(Fed fed, float now)
        {
            long owner = fed.Zdo.GetOwner();
            if (owner == 0L && fed.LastOwner != 0L && Settings.StandAside.Value && ZNet.instance != null && !ZNet.instance.IsServer() &&
                ZNetScene.InActiveArea(fed.Position, ZNet.instance.GetReferencePosition()))
            {
                fed.ServerTookAt = now;
            }
            fed.LastOwner = owner;
        }

        /// <summary>Whether a station is being left alone because the server took it over lately.</summary>
        internal static bool StandingAside(Fed fed, float now) =>
            Settings.StandAside.Value && now - fed.ServerTookAt < StandAsideFor && now >= fed.ServerTookAt;
    }
}
