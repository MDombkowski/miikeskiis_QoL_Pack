// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace QoLMods.Chests
{
    /// <summary>
    /// The game's private members the chest service uses, looked up once, and its three patches, applied once for the
    /// whole plugin however many modules use the service. Checked against the game's Container (m_nview, m_inUse,
    /// CheckAccess, Load, Awake, Interact, RPC_OpenResponse) of 2026-09-25.
    /// </summary>
    internal static class ChestHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly List<string> Missing = new List<string>();
        private static bool resolved;
        private static FieldInfo nview;
        private static FieldInfo inUse;
        private static FieldInfo currentContainer;
        private static FieldInfo shipPlayers;
        private static Func<Container, long, bool> checkAccess;
        private static Func<Container, bool> load;

        /// <summary>Container.RPC_OpenResponse(long uid, bool granted): where a chest's owner's answer to an open request
        /// arrives. The service's patch sits there.</summary>
        private static MethodInfo openResponse;

        /// <summary>Container.Awake(): every chest that comes into being, for the service's list of chests.</summary>
        private static MethodInfo awake;

        /// <summary>Container.Interact(Humanoid, bool, bool): his own press of E on a chest.</summary>
        private static MethodInfo interact;

        /// <summary>
        /// Called from each using module's Resolve, at the game's start. The first call looks the members up and applies
        /// the service's patches; every call adds to <paramref name="missing"/> whatever the service can't work without,
        /// so each module that needs it stays off. The patches stay for the session: they only note chests and catch the
        /// service's own answers, and must keep catching those even after the module that asked has been switched off.
        /// </summary>
        internal static void Resolve(List<string> missing)
        {
            if (!resolved)
            {
                resolved = true;
                Find();
            }
            missing.AddRange(Missing);
        }

        private static void Find()
        {
            nview = typeof(Container).GetField("m_nview", AnyInstance);
            if (nview == null || nview.FieldType != typeof(ZNetView))
            {
                Missing.Add("Container.m_nview");
            }

            MethodInfo access = typeof(Container).GetMethod("CheckAccess", AnyInstance, null, new[] { typeof(long) }, null);
            if (access == null || access.ReturnType != typeof(bool))
            {
                Missing.Add("Container.CheckAccess");
            }
            else
            {
                checkAccess = (Func<Container, long, bool>)Delegate.CreateDelegate(typeof(Func<Container, long, bool>), access);
            }

            MethodInfo reload = typeof(Container).GetMethod("Load", AnyInstance, null, Type.EmptyTypes, null);
            if (reload == null || reload.ReturnType != typeof(bool))
            {
                Missing.Add("Container.Load");
            }
            else
            {
                load = (Func<Container, bool>)Delegate.CreateDelegate(typeof(Func<Container, bool>), reload);
            }

            inUse = typeof(Container).GetField("m_inUse", AnyInstance);
            if (inUse == null || inUse.FieldType != typeof(bool))
            {
                Missing.Add("Container.m_inUse");
            }

            currentContainer = typeof(InventoryGui).GetField("m_currentContainer", AnyInstance);
            if (currentContainer == null || currentContainer.FieldType != typeof(Container))
            {
                Missing.Add("InventoryGui.m_currentContainer");
            }

            // Optional: without it, a ship's chest is skipped whenever anyone, him too, is aboard.
            shipPlayers = typeof(Ship).GetField("m_players", AnyInstance);
            if (shipPlayers != null && shipPlayers.FieldType != typeof(List<Player>))
            {
                shipPlayers = null;
            }

            openResponse = typeof(Container).GetMethod("RPC_OpenResponse", AnyInstance, null, new[] { typeof(long), typeof(bool) }, null);
            if (openResponse == null)
            {
                Missing.Add("Container.RPC_OpenResponse");
            }
            awake = typeof(Container).GetMethod("Awake", AnyInstance, null, Type.EmptyTypes, null);
            if (awake == null)
            {
                Missing.Add("Container.Awake");
            }
            interact = typeof(Container).GetMethod(nameof(Container.Interact), AnyInstance, null, new[] { typeof(Humanoid), typeof(bool), typeof(bool) }, null);
            if (interact == null)
            {
                Missing.Add("Container.Interact");
            }

            if (Missing.Count > 0)
            {
                return;
            }
            try
            {
                // Its own Harmony ID, one per plugin: modprojects.qolmods.chests in the bundle, modprojects.<mod>.chests in
                // a single release. A module's host never removes it (MODULES.md, "Parts").
                string plugin = typeof(ChestHooks).Assembly.GetName().Name.ToLowerInvariant();
                var harmony = new Harmony($"modprojects.{plugin}.chests");
                harmony.Patch(openResponse, prefix: new HarmonyMethod(AccessTools.Method(typeof(ChestPatches), nameof(ChestPatches.OpenResponsePrefix))));
                harmony.Patch(awake, postfix: new HarmonyMethod(AccessTools.Method(typeof(ChestPatches), nameof(ChestPatches.AwakePostfix))));
                // Last, so it sees whether an earlier prefix (MyLittleUI's rename on Shift+E) has stopped the game's own E press.
                harmony.Patch(interact, prefix: new HarmonyMethod(AccessTools.Method(typeof(ChestPatches), nameof(ChestPatches.InteractPrefix))) { priority = Priority.Last });
            }
            catch (Exception error)
            {
                Missing.Add($"the chest service's patches on Container ({error.Message})");
            }
        }

        internal static ZNetView NetView(Container chest) => (ZNetView)nview.GetValue(chest);

        /// <summary>Sets the chest's own "in use" mark directly: what its owner's game checks before handing it to
        /// anyone, without the lid, sound and record the game's SetInUse adds. Only on a chest this computer owns.</summary>
        internal static void SetInUse(Container chest, bool value) => inUse.SetValue(chest, value);

        /// <summary>True while this chest is the one open in his own inventory window.</summary>
        internal static bool OpenOnMyScreen(Container chest) =>
            InventoryGui.instance != null && InventoryGui.IsVisible() && (Container)currentContainer.GetValue(InventoryGui.instance) == chest;

        /// <summary>True if anyone other than him is aboard this ship. Without the game's list of who is aboard, true
        /// whenever anyone is.</summary>
        internal static bool OthersAboard(Ship ship)
        {
            if (shipPlayers == null)
            {
                return !ship.CanBeRemoved();
            }
            foreach (Player aboard in (List<Player>)shipPlayers.GetValue(ship))
            {
                if (aboard != null && aboard != Player.m_localPlayer)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The game's own "may this player open it" test: public chests yes, private ones only for the builder.</summary>
        internal static bool CheckAccess(Container chest, long playerId) => checkAccess(chest, playerId);

        /// <summary>Reloads the chest's contents from its network record now, rather than at the game's next check, up
        /// to a second later. Does nothing if nothing changed, or while the chest is open on this computer.</summary>
        internal static void Reload(Container chest) => load(chest);
    }
}
