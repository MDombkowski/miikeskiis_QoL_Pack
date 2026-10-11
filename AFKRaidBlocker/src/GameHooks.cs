using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AFKRaidBlocker
{
    /// <summary>
    /// The game methods this mod hooks, and the private members it uses, looked up once at startup. Read in the game's
    /// own code (1.0.17, decompiled 2026-10-10): ZNet.SendServerSyncPlayerData sends a client's report to the server
    /// every 2 seconds (ZNet.SendPeriodicData); RandEventSystem.RetrievePlayerEventData reads each report, the host's own
    /// straight from its dictionary, before every raid roll; EnemyHud.UpdateHuds writes every player's name over their
    /// head each frame.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo huds;
        private static FieldInfo hudCharacter;
        private static FieldInfo hudName;

        /// <summary>ZNet.SendServerSyncPlayerData(ZNetPeer): a client's report, written into a package for the server.</summary>
        internal static MethodInfo SendReport { get; private set; }

        /// <summary>RandEventSystem.RetrievePlayerEventData(Dictionary, Vector3, out PlayerEventData): the server's
        /// reading of one player's report; false leaves that player out of every random event.</summary>
        internal static MethodInfo ReadReport { get; private set; }

        /// <summary>EnemyHud.UpdateHuds(Player, Sadle, float): writes the names over heads. Null when the away mark
        /// can't be shown.</summary>
        internal static MethodInfo UpdateHuds { get; private set; }

        /// <summary>Whether other players' away marks can be shown (the over-head names and their private parts were found).</summary>
        internal static bool CanShowMarks => UpdateHuds != null;

        /// <summary>The names that were looked for and found, by member: what the harness checks against the game.</summary>
        internal static readonly List<string> Found = new List<string>();

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            Found.Clear();

            SendReport = typeof(ZNet).GetMethod("SendServerSyncPlayerData", AnyInstance, null, new[] { typeof(ZNetPeer) }, null);
            if (SendReport == null || SendReport.ReturnType != typeof(void))
            {
                SendReport = null;
                missing.Add("ZNet.SendServerSyncPlayerData");
            }
            else
            {
                Found.Add("ZNet.SendServerSyncPlayerData");
            }

            ReadReport = typeof(RandEventSystem).GetMethod("RetrievePlayerEventData", AnyStatic, null,
                new[] { typeof(Dictionary<string, string>), typeof(Vector3), typeof(RandEventSystem.PlayerEventData).MakeByRefType() }, null);
            if (ReadReport == null || ReadReport.ReturnType != typeof(bool))
            {
                ReadReport = null;
                missing.Add("RandEventSystem.RetrievePlayerEventData");
            }
            else
            {
                Found.Add("RandEventSystem.RetrievePlayerEventData");
            }

            const string withoutMarks = "other players' away marks then don't show";
            MethodInfo update = typeof(EnemyHud).GetMethod("UpdateHuds", AnyInstance, null, new[] { typeof(Player), typeof(Sadle), typeof(float) }, null);
            huds = typeof(EnemyHud).GetField("m_huds", AnyInstance);
            Type hudData = typeof(EnemyHud).GetNestedType("HudData", BindingFlags.NonPublic | BindingFlags.Public);
            hudCharacter = hudData?.GetField("m_character", AnyInstance);
            hudName = hudData?.GetField("m_name", AnyInstance);
            if (update == null || huds == null || !typeof(IDictionary).IsAssignableFrom(huds.FieldType)
                || hudCharacter == null || !typeof(Character).IsAssignableFrom(hudCharacter.FieldType)
                || hudName == null || !typeof(TMPro.TMP_Text).IsAssignableFrom(hudName.FieldType))
            {
                UpdateHuds = null;
                missingOptional.Add($"EnemyHud.UpdateHuds and its name labels ({withoutMarks})");
            }
            else
            {
                UpdateHuds = update;
                Found.Add("EnemyHud.UpdateHuds");
            }
        }

        /// <summary>The over-head name label the game shows for a character now, or null when it shows none.</summary>
        internal static TMPro.TMP_Text NameLabel(EnemyHud hud, Character character)
        {
            if (!(huds.GetValue(hud) is IDictionary all) || !all.Contains(character))
            {
                return null;
            }
            object data = all[character];
            if (data == null || !ReferenceEquals(hudCharacter.GetValue(data), character))
            {
                return null;
            }
            return hudName.GetValue(data) as TMPro.TMP_Text;
        }
    }
}
