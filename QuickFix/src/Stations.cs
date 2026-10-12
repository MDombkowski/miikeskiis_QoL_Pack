using System.Collections.Generic;
using QoLMods.Kit;
using UnityEngine;

namespace QuickFix
{
    /// <summary>
    /// Repair near crafting stations (both off out of the box, his defaults): on opening a station, everything the game's
    /// own rule repairs there, at once (decision 5); and once each time he comes near a station, a Grinding Wheel or a
    /// Smith's Anvil (decision 6). Durability is the player's own, saved with the character; nothing here is sent to
    /// anyone but the repair sound (research part A, section 8).
    /// </summary>
    internal static class Stations
    {
        /// <summary>How often the stations near him are looked for.</summary>
        private const float LookEvery = 1f;

        private static float nextLook;
        private static readonly HashSet<int> nearBefore = new HashSet<int>();
        private static readonly HashSet<int> nearNow = new HashSet<int>();
        private static readonly List<Component> newlyNear = new List<Component>();

        /// <summary>After the game opened a station's window (CraftingStation.Interact made it the current station):
        /// everything its own rule repairs there, to full, in one go.</summary>
        internal static void Opened(CraftingStation station, Humanoid user, bool repeat)
        {
            Player player = user as Player;
            if (repeat || !Settings.OnUse.Value || player == null || player != Player.m_localPlayer || station == null ||
                !station.m_canRepair || player.GetCurrentCraftingStation() != station)
            {
                return;
            }
            int mended = Gear.MendAll(player, (item, recipe) => GameHooks.GameCanRepair(item));
            if (mended > 0)
            {
                Sound(station);
                Say.Center($"Repaired {Gear.Items(mended)} at the {Localized(station.m_name)}");
            }
        }

        /// <summary>Once a second: the stations and the two pieces within "Distance"; each that came near since the last
        /// look repairs once. Forgets what was near while both settings are off, so switching one on counts everything
        /// near as newly near.</summary>
        internal static void Tick(bool on)
        {
            if (!on || (!Settings.NearStations.Value && !Settings.NearPieces.Value))
            {
                nearBefore.Clear();
                return;
            }
            if (Time.time < nextLook)
            {
                return;
            }
            nextLook = Time.time + LookEvery;
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || player.IsTeleporting())
            {
                nearBefore.Clear();
                return;
            }

            Vector3 here = player.transform.position;
            float reach = Settings.NearDistance.Value;
            nearNow.Clear();
            newlyNear.Clear();
            if (Settings.NearStations.Value)
            {
                List<CraftingStation> stations = GameHooks.AllStations();
                if (stations != null)
                {
                    foreach (CraftingStation station in stations)
                    {
                        Near(station, here, reach);
                    }
                }
            }
            if (Settings.NearPieces.Value)
            {
                List<StationExtension> extensions = GameHooks.AllExtensions();
                if (extensions != null)
                {
                    foreach (StationExtension extension in extensions)
                    {
                        if (extension != null && Pieces.Which(extension.GetComponent<Piece>()) != RepairPiece.None)
                        {
                            Near(extension, here, reach);
                        }
                    }
                }
            }

            // One message for everything that came near at once (a centre message replaces the one before it).
            int mended = 0;
            var where = new List<string>();
            foreach (Component thing in newlyNear)
            {
                int count = 0;
                string name = null;
                if (thing is CraftingStation station)
                {
                    count = RepairAt(player, station);
                    name = Localized(station.m_name);
                }
                else if (thing is StationExtension extension)
                {
                    RepairPiece piece = Pieces.Which(extension.GetComponent<Piece>());
                    count = Pieces.Repair(player, piece, extension, speak: false);
                    name = Pieces.NameOf(piece);
                }
                if (count > 0)
                {
                    mended += count;
                    if (!where.Contains(name))
                    {
                        where.Add(name);
                    }
                }
            }
            if (mended > 0)
            {
                Say.Center($"Repaired {Gear.Items(mended)} at the {string.Join(" and the ", where)}");
            }
            nearBefore.Clear();
            nearBefore.UnionWith(nearNow);
        }

        internal static void Forget()
        {
            nearBefore.Clear();
            nearNow.Clear();
            newlyNear.Clear();
        }

        private static void Near(Component thing, Vector3 here, float reach)
        {
            if (thing == null || Vector3.Distance(here, thing.transform.position) > reach)
            {
                return;
            }
            int id = thing.GetInstanceID();
            nearNow.Add(id);
            if (!nearBefore.Contains(id))
            {
                newlyNear.Add(thing);
            }
        }

        /// <summary>What the game's own rule would repair at <paramref name="station"/> if its window were open (our copy of
        /// InventoryGui.CanRepair with this station, Rules.StationRepairs), and only where the station could be used: it
        /// repairs at all, and has its roof and fire (CheckUsable, silent).</summary>
        private static int RepairAt(Player player, CraftingStation station)
        {
            if (!station.m_canRepair || !station.CheckUsable(player, false))
            {
                return 0;
            }
            int level = station.GetLevel();
            bool noCost = player.NoCostCheat();
            int mended = Gear.MendAll(player, (item, recipe) => Rules.StationRepairs(
                recipe.m_repairStation != null ? recipe.m_repairStation.m_name : null,
                recipe.m_craftingStation != null ? recipe.m_craftingStation.m_name : null,
                station.m_name, item.m_worldLevel, Game.m_worldLevel, level, recipe.m_minStationLevel, noCost));
            if (mended > 0)
            {
                Sound(station);
            }
            return mended;
        }

        private static void Sound(CraftingStation station)
        {
            if (station.m_repairItemDoneEffects != null)
            {
                station.m_repairItemDoneEffects.Create(station.transform.position, Quaternion.identity);
            }
        }

        private static string Localized(string token) =>
            Localization.instance != null ? Localization.instance.Localize(token) : token;
    }
}
