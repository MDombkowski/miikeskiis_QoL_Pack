// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace QoLMods.Chests
{
    /// <summary>How a chest holding something wanted stands for this game right now (ChestExplain).</summary>
    internal enum ChestStanding
    {
        /// <summary>It comes at the next claim: another game is asked for it, or the server hands it over within two seconds.</summary>
        Coming,

        /// <summary>Beyond the server's reach: this game claims it itself, and may change it only now and then (ChestClaims).</summary>
        ComingFar,

        /// <summary>Someone has it open, or another player is about to open it.</summary>
        Busy,

        /// <summary>This game can't get it at all now (ChestClaims.CantClaim).</summary>
        Unreachable,
    }

    /// <summary>One item something was short of, and the nearest chest holding it, as it stands (ChestExplain).</summary>
    internal sealed class ShortItem
    {
        internal string Key;
        internal string Label;
        internal int Amount;

        /// <summary>The nearest chest of the best standing that holds some of it; null when no chest within the rules does.</summary>
        internal Container Chest;
        internal float Distance;
        internal ChestStanding Standing;

        /// <summary>Each chest within the rules holding some of it that this game can't change now, nearest first, with its
        /// situation in words (ChestClaims.Situation): for the log.</summary>
        internal readonly List<string> Chests = new List<string>();
    }

    /// <summary>
    /// Why something couldn't be had from the chests, in words for him and for the log (since 2026-10-08, card A69). He
    /// asked: "if the system thinks it's out of range, it should tell me that instead of saying "getting the materials
    /// from a chest nearby."" So every module says which item, how far its chest is, and why it isn't his yet, from one
    /// place: a chest another game is asked for, one the server hands over in a moment, one beyond the server's reach
    /// that this game claims itself (only now and then), one someone has open, or one this game can't get at all
    /// (ChestClaims.CantClaim: "Chests beyond the server's reach" off, a cart's or a ship's, or a slow connection). And,
    /// with "Write chest problems to the log" on (ChestSettings), a line in the log naming each chest and its situation.
    /// It reads this computer's copies only, and changes nothing.
    /// </summary>
    internal static class ChestExplain
    {
        /// <summary>The same line for the same module is written at most once in this many seconds.</summary>
        private const float LogAgainAfter = 5f;

        /// <summary>At most this many chests are named per item in a log line.</summary>
        private const int ChestsPerItem = 3;

        private static readonly Dictionary<string, float> Logged = new Dictionary<string, float>();

        /// <summary>
        /// For each item still wanted (by the service's keys), the chests within <paramref name="rules"/> around
        /// <paramref name="centre"/> that hold some of it and that this game can't change right now, and how it stands:
        /// the chests are taken best standing first (then nearest) until they hold the amount wanted, and the item stands
        /// as the worst of those it needs (one Tin in a near chest and nine in one that can't come is "can't come": card
        /// A69, review 1, m4), named by the first chest of that standing. Items no such chest holds come back with no chest.
        /// The chests' situations, for the log, are worked out only while the log is on.
        /// </summary>
        internal static List<ShortItem> Shortfall(ChestUser user, IDictionary<string, int> left, Vector3 centre, ChestRules rules)
        {
            var items = new List<ShortItem>();
            List<Container> chests = ChestFinder.Find(centre, rules);
            bool logs = ChestSettings.Logs;
            foreach (KeyValuePair<string, int> want in left)
            {
                if (want.Value <= 0)
                {
                    continue;
                }
                var item = new ShortItem { Key = want.Key, Label = Label(want.Key), Amount = want.Value };
                var holding = new List<(Container Chest, float Distance, ChestStanding Standing, int Count)>();
                foreach (Container chest in chests)   // nearest first
                {
                    int count = ChestClaims.MayChange(chest) ? 0 : ChestStock.Count(chest, want.Key, rules.LeaveOne, rules.LeaveOneOfGear);
                    if (count <= 0)
                    {
                        continue;
                    }
                    float distance = Vector3.Distance(ChestFinder.Where(chest), centre);
                    holding.Add((chest, distance, StandingOf(chest, user), count));
                    if (logs && item.Chests.Count < ChestsPerItem)
                    {
                        item.Chests.Add($"a chest {distance:0} m away ({chest.gameObject.name.Replace("(Clone)", "")}): {ChestClaims.Situation(chest, user)}");
                    }
                }
                int covered = 0;
                foreach (var each in holding.OrderBy(each => each.Standing).ThenBy(each => each.Distance))
                {
                    if (item.Chest == null || each.Standing > item.Standing)
                    {
                        item.Chest = each.Chest;
                        item.Distance = each.Distance;
                        item.Standing = each.Standing;
                    }
                    covered += each.Count;
                    if (covered >= want.Value)
                    {
                        break;
                    }
                }
                items.Add(item);
            }
            return items;
        }

        /// <summary>How a chest this game can't change right now stands (see ChestStanding).</summary>
        internal static ChestStanding StandingOf(Container chest, ChestUser user)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid())
            {
                return ChestStanding.Unreachable;
            }
            if (!view.IsOwner() && view.GetZDO().GetInt(ZDOVars.s_inUse) == 1 && ChestShare.CanShare(chest))
            {
                return ChestStanding.Coming;   // a friend on the pack has it open: taken through his game (ChestShare)
            }
            if (!view.IsOwner() && (view.GetZDO().GetInt(ZDOVars.s_inUse) == 1 || ChestClaims.IsWaitedOn(chest)))
            {
                return ChestStanding.Busy;
            }
            if (ChestClaims.CantClaim(chest, user))
            {
                return ChestStanding.Unreachable;
            }
            // A chest claimed here waits for the server's rounds like a far one wherever the server won't keep it with him
            // (ChestClaims.Usable); inside the hand-out, though, it was claimed only because the server passed it over, and
            // the words say so rather than "beyond the server's reach" (Claimed; card A69, his report of 2026-10-08: a
            // chest 55 m away was called beyond it).
            bool far = view.IsOwner()
                ? ChestClaims.ClaimedHere(chest) && (ChestClaims.BeyondHandOut(chest) || !ChestClaims.KeptWithHim(chest))
                : ZNet.instance != null && !ZNet.instance.IsServer() && ChestClaims.NeedsFarClaim(chest);
            return far ? ChestStanding.ComingFar : ChestStanding.Coming;
        }

        /// <summary>
        /// One short line for the middle of the screen when a piece couldn't be placed at a click: what's on its way, or
        /// why it can't be had, each item named with how far its chest is (his format, 2026-10-08: "Getting Wood (54 m)
        /// and Stone (56 m) from chests: place it again in a moment."). Null when no chest within the rules holds any of
        /// what's missing (the game's own message then says so).
        /// </summary>
        internal static string PlaceMessage(List<ShortItem> items)
        {
            List<ShortItem> held = items.Where(item => item.Chest != null).ToList();
            if (held.Count == 0)
            {
                return null;
            }
            ShortItem worst = held.OrderByDescending(item => item.Standing).ThenByDescending(item => item.Distance).First();
            List<ShortItem> these = held.Where(item => item.Standing == worst.Standing).ToList();
            string named = Named(these);
            string chests = Chests(these);
            switch (worst.Standing)
            {
                case ChestStanding.Unreachable:
                    return $"{named} {Is(these)} in {chests} that your game can't get: {Cannot(these)}.";
                case ChestStanding.Busy:
                    return $"{named} {Is(these)} in {chests} that {OpenBy(these)}: place it again once {ItIs(these)} free.";
                case ChestStanding.ComingFar:
                    return $"Getting {named} from {chests} {Claimed(these)}: place it again (it can take a few tries).";
                default:
                    return $"Getting {named} from {chests}: place it again in a moment.";
            }
        }

        /// <summary>
        /// The reason a take came up short, as a clause after "Couldn't get the materials from the chests: " (crafting, a
        /// station, a fetch); null when no chest within the rules holds any of what's missing.
        /// </summary>
        internal static string ShortReason(List<ShortItem> items)
        {
            List<ShortItem> held = items.Where(item => item.Chest != null).ToList();
            if (held.Count == 0)
            {
                return null;
            }
            ShortItem worst = held.OrderByDescending(item => item.Standing).ThenByDescending(item => item.Distance).First();
            List<ShortItem> these = held.Where(item => item.Standing == worst.Standing).ToList();
            string named = Named(these);
            string chests = Chests(these);
            string was = SeveralChests(these) ? "weren't" : "wasn't";
            switch (worst.Standing)
            {
                case ChestStanding.Unreachable:
                    return $"{named} {Is(these)} in {chests} that your game can't get: {Cannot(these)}";
                case ChestStanding.Busy:
                    return $"{named} {Is(these)} in {chests} that {OpenBy(these)}";
                case ChestStanding.ComingFar:
                    return $"{named} {Is(these)} in {chests} {Claimed(these)}, which {was} ready in time; try again";
                default:
                    return $"{named} {Is(these)} in {chests} that {was} handed to you in time; try again";
            }
        }

        // Why this game can't get these chests at all (ChestClaims.CantClaim), short, for the middle of the screen. The
        // chests on land give the reason, as it's the one he can act on; a cart's or a ship's is then only mentioned
        // (0.36.1's review: one chest's reason was given for all).
        private static string Cannot(List<ShortItem> items)
        {
            List<Container> all = items.Select(item => item.Chest).Where(chest => chest != null).Distinct().ToList();
            List<Container> land = all.Where(chest => !ChestClaims.OnVehicle(chest)).ToList();
            if (land.Count == 0)
            {
                return $"{(all.Count > 1 ? "they're" : "it's")} a cart's or a ship's, beyond the server's reach";
            }
            string itIs = land.Count > 1 ? "they're" : "it's";
            string vehicles = land.Count < all.Count ? " (a cart's or a ship's chest is never claimed)" : "";
            if (!ChestClaims.FarPossible())
            {
                return $"{itIs} beyond the server's reach, and your connection is too slow to claim {(land.Count > 1 ? "them" : "it")}{vehicles}";
            }
            return $"{itIs} beyond the server's reach, and the setting \"Chests beyond the server's reach\" is off: switch it on in F1, under \"" +
                   ChestSettings.Section + "\"" + vehicles;
        }

        // Where chests this game claims itself lie: "beyond the server's reach" when every one of them is, else that the
        // server didn't hand them over (it passed them over), which takes as long.
        private static string Claimed(List<ShortItem> items) =>
            items.Where(item => item.Chest != null).All(item => ChestClaims.BeyondHandOut(item.Chest))
                ? "beyond the server's reach"
                : $"your game claimed itself (the server didn't hand {(SeveralChests(items) ? "them" : "it")} over)";

        // Each item with how far its chest is: "Tin (54 m)", "Tin (54 m) and Copper (56 m)", "Tin (54 m), Copper (56 m)
        // and Wood (49 m)", "Tin (54 m), Copper (56 m) and 2 more" (his format, 2026-10-08).
        private static string Named(List<ShortItem> items)
        {
            List<string> named = items.GroupBy(item => item.Label).Select(group => group.First())
                                      .Select(item => WithDistance(item)).ToList();
            if (named.Count == 1)
            {
                return named[0];
            }
            if (named.Count <= 3)
            {
                return string.Join(", ", named.Take(named.Count - 1)) + " and " + named[named.Count - 1];
            }
            return $"{named[0]}, {named[1]} and {named.Count - 2} more";
        }

        // "Tin (54 m)"; "Bronze armour (quality 2, 54 m)" for an item of a quality.
        private static string WithDistance(ShortItem item) =>
            item.Label.EndsWith(")", StringComparison.Ordinal) && item.Label.Contains(" (quality ")
                ? $"{item.Label.Substring(0, item.Label.Length - 1)}, {item.Distance:0} m)"
                : $"{item.Label} ({item.Distance:0} m)";

        // "is" for one item, "are" for several ("Tin (54 m) and Copper (56 m) are").
        private static string Is(List<ShortItem> items) => items.Select(item => item.Label).Distinct().Count() > 1 ? "are" : "is";

        // Whether the items lie in more than one chest.
        private static bool SeveralChests(List<ShortItem> items) => items.Select(item => item.Chest).Distinct().Count() > 1;

        // "a chest" or "chests".
        private static string Chests(List<ShortItem> items) => SeveralChests(items) ? "chests" : "a chest";

        // "it's" or "they're", for the chests.
        private static string ItIs(List<ShortItem> items) => SeveralChests(items) ? "they're" : "it's";

        // Who has busy chests: someone has them open, or another player is about to open them (ChestClaims.IsWaitedOn).
        private static string OpenBy(List<ShortItem> items)
        {
            List<Container> chests = items.Select(item => item.Chest).Where(chest => chest != null).Distinct().ToList();
            int waited = chests.Count(ChestClaims.IsWaitedOn);
            return waited == 0 ? "someone has open" : waited == chests.Count ? "another player is about to open" : "someone has open or is about to open";
        }

        // An item's name for him to read, from the service's key ("$item_tin", or "name@quality").
        private static string Label(string key)
        {
            ChestStock.Parse(key, out string name, out int quality);
            string label = Localization.instance != null ? Localization.instance.Localize(name) : name;
            return quality > 0 ? $"{label} (quality {quality})" : label;
        }

        /// <summary>
        /// Writes a line to <paramref name="user"/>'s log when "Write chest problems to the log" is on: what couldn't be
        /// done (<paramref name="what"/>, "Building Cauldron"), and for each item, how many were missing and the chests
        /// holding it with their situations. The same line at most once in a few seconds.
        /// </summary>
        internal static void Log(ChestUser user, string what, List<ShortItem> items)
        {
            if (!ChestSettings.Logs || user == null || items == null || items.Count == 0)
            {
                return;
            }
            var parts = new List<string>();
            foreach (ShortItem item in items)
            {
                parts.Add(item.Chests.Count == 0
                    ? $"{item.Amount} {item.Label}: no chest within range holds it"
                    : $"{item.Amount} {item.Label}: " + string.Join("; ", item.Chests));
            }
            Write(user, $"{what}: short of " + string.Join(". ", parts) + ".");
        }

        /// <summary>From ChestClaims when a claim settles with chests it couldn't have: one line naming each, with its
        /// situation then (a claim made ahead of need passes nothing, so asking ahead never fills the log).</summary>
        internal static void LogClaim(ChestUser user, string task, IEnumerable<Container> missed)
        {
            if (!ChestSettings.Logs || user == null)
            {
                return;
            }
            Player player = Player.m_localPlayer;
            Vector3 centre = player != null ? player.transform.position : Vector3.zero;
            List<string> lines = missed.Where(chest => chest != null).Take(6)
                .Select(chest => $"a chest {Vector3.Distance(ChestFinder.Where(chest), centre):0} m away " +
                                 $"({chest.gameObject.name.Replace("(Clone)", "")}): {ChestClaims.Situation(chest, user)}")
                .ToList();
            if (lines.Count > 0)
            {
                Write(user, $"{char.ToUpperInvariant(task[0])}{task.Substring(1)}: {lines.Count} chest{(lines.Count == 1 ? "" : "s")} " +
                            "couldn't be had in time: " + string.Join("; ", lines) + ".");
            }
        }

        // The line, unless the same one was written for this module a moment ago.
        private static void Write(ChestUser user, string line)
        {
            float now = Time.realtimeSinceStartup;
            string key = user.Name + "\n" + line;
            if (Logged.TryGetValue(key, out float at) && now - at < LogAgainAfter)
            {
                return;
            }
            if (Logged.Count > 200)
            {
                foreach (string old in Logged.Where(each => now - each.Value >= LogAgainAfter).Select(each => each.Key).ToList())
                {
                    Logged.Remove(old);
                }
            }
            Logged[key] = now;
            try
            {
                user.Log.LogInfo(line);
            }
            catch (Exception)
            {
                // No log to write to (the module's host is gone): nothing to tell.
            }
        }
    }
}
