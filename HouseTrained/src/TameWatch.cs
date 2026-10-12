using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// A line in the game's log whenever a tame dies on this game, or a tame's record is removed from the world, naming
    /// whose game removed it (card B12: his two Loxes vanished on 2026-10-10/11 with no drops and no saddle, and nothing
    /// written anywhere said how). It only writes: it changes nothing in the game. A death runs on the game that looks
    /// after the tame; a removal reaches every game in the world (the game sends it to everyone, with who sent it), so
    /// either player's log tells. With "Write tames' deaths and removals to the log" on (out of the box).
    /// </summary>
    internal static class TameWatch
    {
        private static AccessTools.FieldRef<Character, HitData> lastHit;
        private static bool lastHitLooked;
        private static bool failed;

        /// <summary>Runs one of its writes: an error costs only the watch, for the session, with a warning once; never the
        /// module (it runs on every removal in the world, and it only writes to the log).</summary>
        internal static void Safely(string what, Action write)
        {
            if (failed)
            {
                return;
            }
            try
            {
                write();
            }
            catch (Exception error)
            {
                failed = true;
                Module.Log.LogWarning($"House Trained: writing {what} to the log failed, so tames' deaths and removals aren't " +
                                      $"written for the rest of this session ({error.GetType().Name}: {error.Message}).");
            }
        }

        /// <summary>A tame dying on the game that looks after it (others play its death too, for a creature with a death
        /// animation, without its last hit: review m7): its kind, name, place and last hit. Never a summon.</summary>
        internal static void Died(Character tame)
        {
            if (tame == null || !tame.IsTamed() || Summoned(tame.gameObject))
            {
                return;
            }
            ZNetView view = tame.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return;
            }
            ZDO zdo = view.GetZDO();
            string name = zdo != null ? zdo.GetString(ZDOVars.s_tamedName, string.Empty) : string.Empty;
            HitData hit = LastHit(tame);
            Character attacker = hit?.GetAttacker();
            string by = attacker != null ? Localization.instance.Localize(attacker.m_name)
                : hit != null ? $"{hit.m_hitType} damage" : "nothing it was hit by";
            Module.Log.LogInfo($"House Trained: the tame {Kind(tame.m_name, null)}{Named(name)} died at {Place(tame.transform.position)}, " +
                               $"last hit by {by}. (Card B12's watch: a tame that dies drops its saddle and its loot where it fell.)");
        }

        /// <summary>The game's message that records were removed from the world, read before the game reads it (the
        /// package's read position put back): a line for each tame among them.</summary>
        internal static void Removed(long sender, ZPackage package)
        {
            ZDOMan records = ZDOMan.instance;
            if (package == null || records == null)
            {
                return;
            }
            int at = package.GetPos();
            try
            {
                int count = package.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    ZDO zdo = records.GetZDO(package.ReadZDOID());
                    if (zdo == null || !zdo.GetBool(ZDOVars.s_tamed))
                    {
                        continue;
                    }
                    GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
                    Character kind = prefab != null ? prefab.GetComponent<Character>() : null;
                    if (kind == null || Summoned(prefab))
                    {
                        continue;   // a tamed record that isn't a creature, or a staff's summon (they come and go: review m7)
                    }
                    string name = zdo.GetString(ZDOVars.s_tamedName, string.Empty);
                    string what = $"{Kind(kind.m_name, prefab.name)}{Named(name)} at {Place(zdo.GetPosition())}";
                    if (prefab.GetComponent<Growup>() != null)
                    {
                        // The game replaces a young that grows up with its grown form (review m7).
                        Module.Log.LogInfo($"House Trained: the tamed young {what} left the world, removed by {Who(sender)}: most " +
                                           "likely it grew up (the game puts the grown one in its place).");
                        continue;
                    }
                    Module.Log.LogInfo($"House Trained: the tame {what} was removed from the world by {Who(sender)} (its record " +
                                       "deleted). (Card B12's watch: a death removes it too, after the line saying it died, on the " +
                                       "game that looked after it.)");
                }
            }
            finally
            {
                package.SetPos(at);
            }
        }

        // A staff's summon (a skeleton, a troll, roots): tamed, but unsummoned by distance or its owner leaving.
        private static bool Summoned(GameObject creature)
        {
            Tameable tameable = creature != null ? creature.GetComponent<Tameable>() : null;
            return tameable != null && (tameable.m_unsummonDistance > 0f || tameable.m_unsummonOnOwnerLogoutSeconds > 0f);
        }

        private static HitData LastHit(Character tame)
        {
            if (!lastHitLooked)
            {
                lastHitLooked = true;
                try
                {
                    if (AccessTools.Field(typeof(Character), "m_lastHit") != null)
                    {
                        lastHit = AccessTools.FieldRefAccess<Character, HitData>("m_lastHit");
                    }
                }
                catch (Exception)
                {
                    lastHit = null;   // the line goes without the last hit
                }
            }
            return lastHit != null ? lastHit(tame) : null;
        }

        // "Lox", from its "$enemy_lox" name; the prefab's when that's missing.
        private static string Kind(string token, string prefab)
        {
            string shown = !string.IsNullOrEmpty(token) && Localization.instance != null ? Localization.instance.Localize(token) : token;
            return string.IsNullOrEmpty(shown) ? prefab ?? "creature" : shown;
        }

        private static string Named(string name) => string.IsNullOrEmpty(name) ? string.Empty : $" \"{name}\"";

        private static string Place(Vector3 at) => string.Format(CultureInfo.InvariantCulture, "x {0:0}, y {1:0}, z {2:0}", at.x, at.y, at.z);

        // Whose game sent the removal: this one, the server, or a player by name.
        private static string Who(long sender)
        {
            if (sender == ZDOMan.GetSessionID())
            {
                return "your own game";
            }
            ZNet net = ZNet.instance;
            if (net != null)
            {
                ZNetPeer server = net.GetServerPeer();
                if (server != null && server.m_uid == sender)
                {
                    return "the server";
                }
                foreach (ZNet.PlayerInfo player in net.GetPlayerList())
                {
                    if (player.m_characterID.UserID == sender)
                    {
                        return $"{player.m_name}'s game";
                    }
                }
            }
            return $"another game (its id {sender})";
        }
    }

    /// <summary>A tame dying on this game: its line in the log, before the game's own death runs.</summary>
    [HarmonyPatch]
    internal static class TameDeathLogPatch
    {
        private static Character dying;
        private static readonly Action work = () => TameWatch.Died(dying);

        private static bool Prepare() => !Broken.Is(Feature.TameDeathLog);

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(Character), nameof(Character.OnDeath));

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.TameDeathLog, original, ex, harmony);

        private static void Prefix(Character __instance)
        {
            if (!Module.IsOn || Settings.LogTames == null || !Settings.LogTames.Value || __instance == null || !__instance.IsTamed())
            {
                return;
            }
            dying = __instance;
            TameWatch.Safely("a tame's death", work);
            dying = null;
        }
    }

    /// <summary>Records removed from the world (the game's DestroyZDO message, from any game): a line for each tame.</summary>
    [HarmonyPatch]
    internal static class TameRemovedLogPatch
    {
        private static long from;
        private static ZPackage removed;
        private static readonly Action work = () => TameWatch.Removed(from, removed);

        private static bool Prepare() => TargetMethod() != null && !Broken.Is(Feature.TameRemovedLog);

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(ZDOMan), "RPC_DestroyZDO", new[] { typeof(long), typeof(ZPackage) });

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.TameRemovedLog, original, ex, harmony);

        // __0: the sender; __1: the package of removed records.
        private static void Prefix(long __0, ZPackage __1)
        {
            if (!Module.IsOn || Settings.LogTames == null || !Settings.LogTames.Value || __1 == null)
            {
                return;
            }
            from = __0;
            removed = __1;
            TameWatch.Safely("removed tames", work);
            removed = null;
        }
    }
}
