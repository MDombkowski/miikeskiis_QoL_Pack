// The QoL Mods chest service, a part: the one copy to edit is Games\Valheim\QoLMods\parts\Chests\. Every mod that uses
// it carries a copy in its own parts\Chests\ folder, kept identical by QoLMods\tools\sync-kit.ps1, so each mod still
// builds and publishes on its own. The rules for a part are in QoLMods\MODULES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace QoLMods.Chests
{
    /// <summary>
    /// The game's private members the chest service uses, looked up once, and its patches, applied once for the whole
    /// plugin however many modules use the service. Checked against the game's Container (m_nview, CheckAccess, Load,
    /// Awake, Interact, RPC_OpenResponse, RPC_RequestOpen, RPC_RequestStack, RPC_RequestTakeAll) of 2026-09-25.
    /// </summary>
    internal static class ChestHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>The game's three requests for a chest, answered by its owner: another player opening it, stacking into
        /// it, or taking all from it. While the service holds a chest, the ones from other players are dropped (ChestClaims).</summary>
        private static readonly string[] Requests = { "RPC_RequestOpen", "RPC_RequestStack", "RPC_RequestTakeAll" };

        /// <summary>MyLittleUI's Harmony ID (shudnal.MyLittleUI, 1.2.26): its prefix on Container.Interact renames a chest
        /// on Shift+E and stops the game's own E press, so the service's prefix there must run after it.</summary>
        private const string MyLittleUiHarmonyId = "shudnal.MyLittleUI";

        /// <summary>MultiUserChest's plugin ID (MSchmoecker, 0.6.2): it lets several players open one chest, and its
        /// prefixes on the open and stack requests don't check __runOriginal (the MultiUserChest study, point 4).</summary>
        internal const string MultiUserChestGuid = "com.maxsch.valheim.MultiUserChest";

        /// <summary>Its class holding those two prefixes, and their names (each (Container, long uid, long playerID) -> bool).</summary>
        private const string MultiUserChestPatches = "MultiUserChest.Patches.ContainerPatch";

        private static readonly string[] MultiUserChestPrefixes = { "ContainerRPC_RequestOpenPatch", "ContainerRPC_RequestStackPatch" };

        private static readonly List<string> Missing = new List<string>();
        private static bool resolved;
        private static FieldInfo nview;
        private static FieldInfo currentContainer;
        private static FieldInfo shipPlayers;
        private static FieldInfo dragItem;
        private static FieldInfo splitItem;
        private static FieldInfo hiddenRightItem;
        private static FieldInfo hiddenLeftItem;
        private static Harmony harmony;
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
        /// Called from each using module's Resolve, at the game's start, with the module as a user of the service. The
        /// first call looks the members up and applies the service's patches; every call adds to <paramref name="missing"/>
        /// whatever the service can't work without, so each module that needs it stays off. The patches stay for the
        /// session: they only note chests and catch the service's own answers, and must keep catching those even after the
        /// module that asked has been switched off. His open chests are shared with friends while any user is on
        /// (ChestShare.Sharing).
        /// </summary>
        internal static void Resolve(List<string> missing, ChestUser user = null)
        {
            ChestShare.Uses(user);
            ServerReach.Uses(user);
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

            currentContainer = typeof(InventoryGui).GetField("m_currentContainer", AnyInstance);
            if (currentContainer == null || currentContainer.FieldType != typeof(Container))
            {
                Missing.Add("InventoryGui.m_currentContainer");
            }

            // Optional: without them, a friend taking from the chest he has open (ChestShare) may take the stack in his hand.
            dragItem = typeof(InventoryGui).GetField("m_dragItem", AnyInstance);
            splitItem = typeof(InventoryGui).GetField("m_splitItem", AnyInstance);
            if (dragItem != null && dragItem.FieldType != typeof(ItemDrop.ItemData))
            {
                dragItem = null;
            }
            if (splitItem != null && splitItem.FieldType != typeof(ItemDrop.ItemData))
            {
                splitItem = null;
            }

            // Optional: without them, a hand item the game has put away (at a station, in bed, while swimming or eating,
            // with R) is known as worn only by its own mark, which the game clears when it puts it away.
            hiddenRightItem = typeof(Humanoid).GetField("m_hiddenRightItem", AnyInstance);
            hiddenLeftItem = typeof(Humanoid).GetField("m_hiddenLeftItem", AnyInstance);
            if (hiddenRightItem != null && hiddenRightItem.FieldType != typeof(ItemDrop.ItemData))
            {
                hiddenRightItem = null;
            }
            if (hiddenLeftItem != null && hiddenLeftItem.FieldType != typeof(ItemDrop.ItemData))
            {
                hiddenLeftItem = null;
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
            // Without all three, a hold couldn't keep a friend from opening a chest before his copy of it has reloaded.
            var requests = new List<MethodInfo>();
            foreach (string name in Requests)
            {
                MethodInfo request = typeof(Container).GetMethod(name, AnyInstance, null, new[] { typeof(long), typeof(long) }, null);
                if (request == null)
                {
                    Missing.Add("Container." + name);
                }
                else
                {
                    requests.Add(request);
                }
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
                harmony = new Harmony($"modprojects.{plugin}.chests");
                harmony.Patch(openResponse, prefix: new HarmonyMethod(AccessTools.Method(typeof(ChestPatches), nameof(ChestPatches.OpenResponsePrefix))));
                harmony.Patch(awake, postfix: new HarmonyMethod(AccessTools.Method(typeof(ChestPatches), nameof(ChestPatches.AwakePostfix))));
                // Last, so it sees whether an earlier prefix (MyLittleUI's rename on Shift+E) has stopped the game's own E press.
                // MyLittleUI's prefix is Priority.Last too, and equal priorities run in the order they were patched: the
                // pack loads first, so without "after" ours ran before it. "after" names its Harmony ID and is ignored when
                // it isn't installed. (Not Priority.Last - 1: a priority of -1 means "not set" to Harmony, so Normal.)
                harmony.Patch(interact, prefix: new HarmonyMethod(AccessTools.Method(typeof(ChestPatches), nameof(ChestPatches.InteractPrefix)))
                {
                    priority = Priority.Last,
                    after = new[] { MyLittleUiHarmonyId },
                });
                // First, so a request for a held chest is dropped before any other mod acts on it.
                foreach (MethodInfo request in requests)
                {
                    harmony.Patch(request, prefix: new HarmonyMethod(AccessTools.Method(typeof(ChestPatches), nameof(ChestPatches.RequestPrefix))) { priority = Priority.First });
                }
                // Other players' personal chests kept closed or look only (card A80): each patch of its own, none needed by
                // the rest of the service, so a failure there never switches the service off (look only then stays closed).
                try
                {
                    ChestAccess.Patch(harmony);
                }
                catch (Exception error)
                {
                    ZLog.LogWarning($"[QoL Mods chest service] Other players' personal chests couldn't be watched over (a chest its marker lets you only look at stays closed): {error}");
                }
            }
            catch (Exception error)
            {
                Missing.Add($"the chest service's patches on Container ({error.Message})");
            }
        }

        /// <summary>
        /// At the service's first turn, once every plugin has started: if MultiUserChest is loaded, the claims learn it
        /// (ChestShare.MultiUserChest: its owner's "yes" without the chest counts as in use after 1.5 s), and its two prefixes on the open and stack requests are skipped whenever the
        /// service's own prefix dropped that request during a hold, which they would otherwise answer anyway. If they
        /// can't be found (it changed), the log says so once, and the hold doesn't keep its answers out.
        /// </summary>
        internal static void AtFirstTurn()
        {
            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(MultiUserChestGuid, out BepInEx.PluginInfo info) || info.Instance == null)
                {
                    return;
                }
                ChestShare.MultiUserChest = true;
                Type patches = info.Instance.GetType().Assembly.GetType(MultiUserChestPatches);
                var prefix = new HarmonyMethod(AccessTools.Method(typeof(ChestPatches), nameof(ChestPatches.MultiUserChestPrefix)));
                int patched = 0;
                foreach (string name in MultiUserChestPrefixes)
                {
                    MethodInfo method = patches?.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                                                           new[] { typeof(Container), typeof(long), typeof(long) }, null);
                    if (method != null && method.ReturnType == typeof(bool) && harmony != null)
                    {
                        harmony.Patch(method, prefix: prefix);
                        patched++;
                    }
                }
                ZLog.Log(patched == MultiUserChestPrefixes.Length
                    ? "[QoL Mods chest service] MultiUserChest is loaded: a chest someone has open counts as in use after 1.5 s, and its " +
                      "answers to requests dropped during a hold are skipped."
                    : "[QoL Mods chest service] MultiUserChest is loaded, but it has changed: its answers to requests dropped during a " +
                      "hold can't be skipped, so a friend may get a chest just taken from before his copy has reloaded.");
            }
            catch (Exception error)
            {
                ZLog.LogWarning($"[QoL Mods chest service] MultiUserChest couldn't be looked at: {error}");
            }
        }

        /// <summary>True for the stack his own chest window holds in his hand right now (dragged, or in the split dialog):
        /// a friend's take through him spares it (ChestShare).</summary>
        internal static bool InHisHand(ItemDrop.ItemData item)
        {
            InventoryGui gui = InventoryGui.instance;
            if (gui == null || item == null)
            {
                return false;
            }
            return (dragItem != null && ReferenceEquals(dragItem.GetValue(gui), item)) ||
                   (splitItem != null && ReferenceEquals(splitItem.GetValue(gui), item));
        }

        /// <summary>True if he wears or holds this item: equipped (Humanoid.IsItemEquiped, which EquipmentAndQuickSlots
        /// widens to its own slots), marked as equipped, a hand item the game has put away for a moment (at a crafting
        /// station, in bed, while swimming or eating, or with R: Humanoid.HideHandItems keeps it in m_hiddenRightItem and
        /// m_hiddenLeftItem, and gives it back after), or waiting to be put on or taken off (Player.IsEquipActionQueued).
        /// Never put away (the card's "never what he wears or holds").</summary>
        internal static bool Worn(Player player, ItemDrop.ItemData item)
        {
            if (item == null)
            {
                return false;
            }
            if (item.m_equipped)
            {
                return true;
            }
            if (player == null)
            {
                return false;
            }
            return player.IsItemEquiped(item) ||
                   (hiddenRightItem != null && ReferenceEquals(hiddenRightItem.GetValue(player), item)) ||
                   (hiddenLeftItem != null && ReferenceEquals(hiddenLeftItem.GetValue(player), item)) ||
                   player.IsEquipActionQueued(item);
        }

        internal static ZNetView NetView(Container chest) => (ZNetView)nview.GetValue(chest);

        /// <summary>True while this chest is the one open in his own inventory window.</summary>
        internal static bool OpenOnMyScreen(Container chest) =>
            InventoryGui.instance != null && InventoryGui.IsVisible() && (Container)currentContainer.GetValue(InventoryGui.instance) == chest;

        /// <summary>The chest his inventory window holds open (or is waiting for), or null.</summary>
        internal static Container OpenChest(InventoryGui gui) =>
            gui != null && currentContainer != null ? currentContainer.GetValue(gui) as Container : null;

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
