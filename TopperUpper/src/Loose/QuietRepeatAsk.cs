using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TopperUpper.Loose
{
    /// <summary>
    /// "Skip the log line for a repeat ownership request" (card A77, decision 8; the freeze research's section 5). The game's
    /// ItemDrop.RPC_RequestOwn (Valheim 1.0.17), which another game's ask for an item on the ground ends in, does this and
    /// nothing more:
    /// <code>
    ///   ZLog.Log("Player " + uid + " wants to pickup " + gameObject.name + "   im: " + ZDOMan.GetSessionID());
    ///   if (m_nview.IsOwner())                         m_nview.GetZDO().SetOwner(uid);       // the handover
    ///   else if (m_nview.GetZDO().GetOwner() == uid)   ZLog.Log("  but they are already the owner");
    ///   else                                           ZLog.Log("  but neither I nor the requesting player are the owners");
    /// </code>
    /// ZLog.Log only writes the line (Debug.Log), GetSessionID and GetOwner only read, and the name only reads. So whenever
    /// the item's view has its record and this game isn't the owner, the game would write its lines and change nothing;
    /// this prefix skips the method then, and only then (RepeatAsk.Skip). When this game owns the item, the game's own
    /// handover runs, with its line. With no record at all the game's own code runs as before. Its own switch, whatever
    /// "Pick up loose items into chests" says; nothing while the module is off.
    /// Also, from the same asks: how many were skipped, one Info line a minute while there were any (code review m2, so his
    /// freeze tests keep their scale; AskTally), and the server asking for an item this game owns, which means the server's
    /// own pickup is still on (n5; ServerPickup), said once.
    /// Its own guard (n1, n2): the method is looked up at the module's start (Resolve), a patch that can't be applied
    /// (another mod's broken patch on the same method) loses only this, through Cleanup, and an error in the prefix loses only
    /// this for the session, with the game's own code run for that ask; each said once in the log, never lights and stations.
    /// </summary>
    [HarmonyPatch]
    internal static class QuietRepeatAsk
    {
        private static MethodInfo target;
        private static bool lost;
        private static readonly AskTally Skipped = new AskTally();
        private static readonly ServerPickup Server = new ServerPickup();
        private static bool serverSeen;
        private static ZNet seenIn;

        /// <summary>At the module's start: finds the game's method, or says it's missing (the rest of the mod runs).</summary>
        internal static void Resolve(List<string> missingOptional)
        {
            target = typeof(ItemDrop).GetMethod("RPC_RequestOwn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                null, new[] { typeof(long) }, null);
            if (target == null || target.ReturnType != typeof(void))
            {
                target = null;
                missingOptional.Add("skipping the log lines for repeat ownership requests (the game has changed: ItemDrop.RPC_RequestOwn not found)");
            }
        }

        private static bool Prepare() => target != null;

        private static MethodBase TargetMethod() => target;

        /// <summary>If the patch can't be applied, only this is lost (the kit would otherwise take every patch of the module
        /// off): its record taken back off the method, said once. Always null, so the kit goes on.</summary>
        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony)
        {
            if (ex == null)
            {
                return null;
            }
            lost = true;
            try
            {
                if (original != null && harmony != null)
                {
                    harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
                }
            }
            catch (Exception)
            {
                // Taking the record back can fail the same way the patch did; this part is lost either way.
            }
            try
            {
                Module.Warn("Topper-Upper couldn't patch the game's answer to an ownership request (another mod's patch on " +
                            "ItemDrop.RPC_RequestOwn may have failed first), so its log lines for repeat requests are written as " +
                            $"before. The rest of Topper-Upper is not affected.\n{ex}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't stop the kit's patching.
            }
            return null;
        }

        [HarmonyPriority(Priority.Last)]
        private static bool Prefix(ItemDrop __instance, long __0)
        {
            if (lost || !Module.IsOn || __instance == null)
            {
                return true;
            }
            try
            {
                ZNetView view = __instance.GetComponent<ZNetView>();
                bool hasRecord = view != null && view.GetZDO() != null;
                bool owner = view != null && view.IsOwner();
                if (owner && !serverSeen && FromServer(__0))
                {
                    serverSeen = true;   // said from Tick, outside the game's message handling
                }
                bool on = Settings.SkipRepeatAskLog != null && Settings.SkipRepeatAskLog.Value;
                if (!RepeatAsk.Skip(on, hasRecord, owner))
                {
                    return true;
                }
                Skipped.Add(__0, Time.time);
                return false;
            }
            catch (Exception error)
            {
                lost = true;
                Module.Warn("Topper-Upper stopped skipping the log lines for repeat ownership requests after an error; the game " +
                            $"writes them as before until it's started again. The rest of Topper-Upper is not affected.\n{error}");
                return true;   // this ask: the game's own code
            }
        }

        /// <summary>Every frame, also while the module is off: the minute's line of asks skipped, and the server's pickup
        /// said once. A new world starts afresh.</summary>
        internal static void Tick()
        {
            if (seenIn != ZNet.instance)
            {
                seenIn = ZNet.instance;
                Skipped.Clear();
                Server.Reset();
                serverSeen = false;
            }
            if (!Module.IsOn)
            {
                return;
            }
            string line = Skipped.Due(Time.time);
            if (line != null)
            {
                Module.Log.LogInfo(line);
            }
            (bool log, bool screen) = Server.Next(serverSeen, Settings.PickUpLoose != null && Settings.PickUpLoose.Value);
            if (log)
            {
                Module.Log.LogInfo("The server asked your game for an item on the ground that your game looks after: a mod on the " +
                                   "server picks up loose items itself, asking for each one. Until it's switched off on the server, " +
                                   "it takes loose items before Topper-Upper does.");
            }
            if (screen)
            {
                QoLMods.Kit.Say.Center("Topper-Upper: a mod on the server picks up loose items itself.\nUntil it's switched off there, it takes them first.");
            }
        }

        /// <summary>The undo step: the minute's count forgotten (the server's notice stays said).</summary>
        internal static void Forget() => Skipped.Clear();

        // The asker is the dedicated server (its routed number is its session number, which its peer carries here).
        private static bool FromServer(long asker)
        {
            ZNet net = ZNet.instance;
            if (net == null || net.IsServer())
            {
                return false;
            }
            ZNetPeer server = net.GetServerPeer();
            return server != null && asker != 0L && asker == server.m_uid;
        }
    }
}
