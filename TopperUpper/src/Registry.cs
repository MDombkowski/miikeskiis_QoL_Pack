using System.Collections.Generic;
using HarmonyLib;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>What kind of game component a light or station is.</summary>
    internal enum FedType
    {
        Fire,
        Smelter,
        Fermenter,
        Shield,
        Cooking,
        Turret,
        Beehive,
        Sap,
    }

    /// <summary>
    /// One light or station this computer has loaded, as the loop keeps it: the game's component, its network view, its
    /// prefab, and the loop's own notes (when it's next looked at, why it wasn't fed, when the server last wrote it).
    /// </summary>
    internal sealed class Fed
    {
        internal readonly MonoBehaviour Component;
        internal readonly FedType Type;
        internal readonly ZNetView View;
        internal readonly string Prefab;

        /// <summary>When it's next looked at for a feed (Time.time).</summary>
        internal float Due;

        /// <summary>When what it made is next looked for.</summary>
        internal float ProductsDue;

        /// <summary>Why it wasn't fed when last looked at, for its hover line; null when nothing stood in the way.</summary>
        internal string Why;

        /// <summary>Its owner when last looked at, for "stand aside while the server feeds".</summary>
        internal long LastOwner;

        /// <summary>When the server (or a mod on it) was last seen taking it over while in his area.</summary>
        internal float ServerTookAt = -1000f;

        /// <summary>When it was last fed the way a hand would, by message to a friend's game.</summary>
        internal float HandFedAt = -1000f;

        /// <summary>How many feeds in a row found nothing to give: it's looked at less often meanwhile (Loop).</summary>
        internal int Barren;

        /// <summary>When it loaded on this computer (Time.time at its Awake).</summary>
        internal readonly float LoadedAt;

        /// <summary>Its catch-up for this load is settled (CatchUps): done, or not wanted. Once per load.</summary>
        internal bool CatchUpDone;

        /// <summary>Another player's game looked after it here before this game did: that game ran it, so it has no time
        /// to catch up on for this game (CatchUps).</summary>
        internal bool OwnedElsewhere;

        /// <summary>When its catch-up began to wait for the server pack's answer and the chests (Time.time), or below 0.</summary>
        internal float CatchUpWaitSince = -1f;

        /// <summary>When a fermenter waiting for its catch-up was last looked at (CatchUps.Tick: once a second).</summary>
        internal float CatchUpLookedAt = -1000f;

        /// <summary>When the wind was last looked at for a windmill (Winds.Note).</summary>
        internal float WindNotedAt = -1000f;

        internal Fed(MonoBehaviour component, FedType type, ZNetView view)
        {
            Component = component;
            Type = type;
            View = view;
            Prefab = Utils.GetPrefabName(component.gameObject);
            LoadedAt = Time.time;
        }

        /// <summary>The prefab of the object its record belongs to: its own, or (the battering ram's smelter sits on a
        /// child) its root's (code review n2).</summary>
        internal string RootPrefab => rootPrefab ?? (rootPrefab = View != null && View.gameObject != null ? Utils.GetPrefabName(View.gameObject) : Prefab);

        private string rootPrefab;

        /// <summary>Still in the world, and its record still there.</summary>
        internal bool Alive => Component != null && View != null && View.IsValid();

        /// <summary>A light: a fire or torch, or the hot tub (a smelter with nothing to make, fed as a light).</summary>
        internal bool IsLight => Type == FedType.Fire || (Type == FedType.Smelter && Prefab == Kinds.HotTub);

        internal Vector3 Position => Component.transform.position;

        internal ZDO Zdo => View.GetZDO();

        /// <summary>This game looks after it (owns its record), so its feeding is this game's.</summary>
        internal bool Mine => View.IsOwner();

        /// <summary>Built by a player: its piece names a creator (Piece.IsPlacedByPlayer).</summary>
        internal bool PlayerBuilt
        {
            get
            {
                Piece piece = Component.GetComponentInParent<Piece>();
                return piece != null && piece.IsPlacedByPlayer();
            }
        }

        /// <summary>Its own mark: fed, left alone, or as its kind says (FeedMark), as this computer sees it (Marks).</summary>
        internal int Mark => Marks.Feed(this);

        /// <summary>Its links, as the text this computer sees for it (Marks; LinkText reads it).</summary>
        internal string LinkText => Marks.LinkText(this);
    }

    /// <summary>
    /// Every light and station that comes into being on this computer (postfixes on the components' Awake, or the shield
    /// generator's Start, where the game registers it), as the chest service keeps its chests: the game keeps no such list
    /// of its own for these. Destroyed and unloaded ones drop out as they are met. Placement previews have no record and
    /// never join. It notes them whether or not the mod is switched on (nothing in the game changes by it), so switching on
    /// later finds everything already loaded.
    /// </summary>
    internal static class Registry
    {
        /// <summary>The lights and stations loaded, in the order they came; the loop goes round it.</summary>
        internal static readonly List<Fed> All = new List<Fed>();

        private static readonly Dictionary<MonoBehaviour, Fed> ByComponent = new Dictionary<MonoBehaviour, Fed>();

        /// <summary>Notes one, from its Awake (or Start). Never throws into the game's own code: a failure is written to the
        /// log once, and that one isn't fed.</summary>
        internal static void Add(MonoBehaviour component, FedType type, ZNetView view)
        {
            try
            {
                if (component == null || view == null || view.GetZDO() == null || ByComponent.ContainsKey(component))
                {
                    return;
                }
                var fed = new Fed(component, type, view);
                ByComponent[component] = fed;
                All.Add(fed);
                if (type == FedType.Fermenter)
                {
                    CatchUps.Watch(fed);   // its catch-up is the loop's to try (a smelter's comes from its own update)
                }
            }
            catch (System.Exception error)
            {
                if (!toldFailed)
                {
                    toldFailed = true;
                    Module.Warn($"A light or station couldn't be noted, so it isn't fed: {error}");
                }
            }
        }

        private static bool toldFailed;

        /// <summary>The light or station a component is, if it was noted.</summary>
        internal static Fed Of(MonoBehaviour component) =>
            component != null && ByComponent.TryGetValue(component, out Fed fed) && fed.Alive ? fed : null;

        /// <summary>The light or station a hovered object belongs to (its switch, its body), or null.</summary>
        internal static Fed Pointed(GameObject hover)
        {
            if (hover == null)
            {
                return null;
            }
            // The nearest component above the hovered collider: a smelter's switches sit below the smelter.
            MonoBehaviour found = (MonoBehaviour)hover.GetComponentInParent<Smelter>() ?? hover.GetComponentInParent<Fermenter>();
            found = found ?? hover.GetComponentInParent<ShieldGenerator>();
            found = found ?? hover.GetComponentInParent<CookingStation>();
            found = found ?? hover.GetComponentInParent<Turret>();
            found = found ?? hover.GetComponentInParent<Beehive>();
            found = found ?? hover.GetComponentInParent<SapCollector>();
            found = found ?? hover.GetComponentInParent<Fireplace>();
            return Of(found);
        }

        /// <summary>Drops every entry whose object is gone, keeping the others' order (each of the loop's cursors stays on
        /// the entry it was about to pass over). Returns how many went.</summary>
        internal static int Prune(params Turns[] turns)
        {
            int removed = 0;
            for (int i = All.Count - 1; i >= 0; i--)
            {
                Fed fed = All[i];
                if (fed.Component == null || fed.View == null || fed.View.GetZDO() == null)
                {
                    All.RemoveAt(i);
                    foreach (Turns each in turns)
                    {
                        each.Removed(i);
                    }
                    removed++;
                }
            }
            if (removed > 0)
            {
                var gone = new List<MonoBehaviour>();
                foreach (KeyValuePair<MonoBehaviour, Fed> each in ByComponent)
                {
                    if (each.Key == null || each.Value.View == null || each.Value.View.GetZDO() == null)
                    {
                        gone.Add(each.Key);
                    }
                }
                foreach (MonoBehaviour key in gone)
                {
                    ByComponent.Remove(key);
                }
            }
            return removed;
        }
    }

    // The game's lights and stations as they come into being. Each only notes the component; nothing in the game changes.

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Awake))]
    internal static class NoteFireplace
    {
        private static void Postfix(Fireplace __instance)
        {
            if (GameHooks.Fireplaces)
            {
                Registry.Add(__instance, FedType.Fire, GameHooks.Read<ZNetView>(GameHooks.FireplaceView, __instance));
            }
        }
    }

    [HarmonyPatch(typeof(Smelter), "Awake")]
    internal static class NoteSmelter
    {
        private static void Postfix(Smelter __instance)
        {
            if (GameHooks.Smelters)
            {
                Registry.Add(__instance, FedType.Smelter, GameHooks.Read<ZNetView>(GameHooks.SmelterView, __instance));
            }
        }
    }

    [HarmonyPatch(typeof(Fermenter), "Awake")]
    internal static class NoteFermenter
    {
        private static void Postfix(Fermenter __instance)
        {
            if (GameHooks.Fermenters)
            {
                Registry.Add(__instance, FedType.Fermenter, GameHooks.Read<ZNetView>(GameHooks.FermenterView, __instance));
            }
        }
    }

    [HarmonyPatch(typeof(ShieldGenerator), "Start")]
    internal static class NoteShieldGenerator
    {
        private static void Postfix(ShieldGenerator __instance)
        {
            if (GameHooks.ShieldGenerators)
            {
                Registry.Add(__instance, FedType.Shield, GameHooks.Read<ZNetView>(GameHooks.ShieldView, __instance));
            }
        }
    }

    [HarmonyPatch(typeof(CookingStation), "Awake")]
    internal static class NoteCookingStation
    {
        private static void Postfix(CookingStation __instance)
        {
            // Only those that burn fuel (the Frost Foundry, the stone oven): food is never put on a cooking station.
            if (GameHooks.CookingStations && __instance.m_useFuel && __instance.m_fuelItem != null)
            {
                Registry.Add(__instance, FedType.Cooking, GameHooks.Read<ZNetView>(GameHooks.CookingView, __instance));
            }
        }
    }

    [HarmonyPatch(typeof(Turret), "Awake")]
    internal static class NoteTurret
    {
        private static void Postfix(Turret __instance)
        {
            if (GameHooks.Turrets)
            {
                Registry.Add(__instance, FedType.Turret, GameHooks.Read<ZNetView>(GameHooks.TurretView, __instance));
            }
        }
    }

    [HarmonyPatch(typeof(Beehive), "Awake")]
    internal static class NoteBeehive
    {
        private static void Postfix(Beehive __instance)
        {
            if (GameHooks.Beehives)
            {
                Registry.Add(__instance, FedType.Beehive, GameHooks.Read<ZNetView>(GameHooks.BeehiveView, __instance));
            }
        }
    }

    [HarmonyPatch(typeof(SapCollector), "Awake")]
    internal static class NoteSapCollector
    {
        private static void Postfix(SapCollector __instance)
        {
            if (GameHooks.SapCollectors)
            {
                Registry.Add(__instance, FedType.Sap, GameHooks.Read<ZNetView>(GameHooks.SapView, __instance));
            }
        }
    }
}
