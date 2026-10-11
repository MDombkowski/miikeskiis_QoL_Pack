using System;
using TopperUpper.Logic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// How what a feed took reaches the light or station (code review M1). As the game that looks after it (its owner, the
    /// only case fed out of the box), written straight into its record exactly as the game's own add writes it, without
    /// the game's message: so a top-up of 25 Barley writes no 25 "Added ore" log lines and plays the added effect once
    /// per feed, not once per item. Each write follows the body of the game's own handler (Valheim 1.0.17):
    /// <list type="bullet">
    /// <item>A smelter's queue (Smelter.RPC_AddOre): its private QueueOre per item (the "itemN" entry, "queued", the
    /// cheated flag), the item checked against its conversions first; its ore effect once (WriteBook, Effects). Without
    /// QueueOre (a game update renamed it), the game's own message per item, log line and all.</item>
    /// <item>A smelter's (and the hot tub's) fuel (RPC_AddFuel: SetFuel of the fuel plus one): one write of the fuel plus
    /// the units; its fuel effect once.</item>
    /// <item>A fire's fuel: its own RPC_AddFuelAmount(units) sent to itself (runs at once): one write, clamped at its
    /// maximum exactly as single adds would be when the units are its room (Rooms.Fire), its effect and its look refreshed
    /// (UpdateState), as the game's own.</item>
    /// <item>An oven's or the Frost Foundry's fuel (CookingStation.RPC_AddFuel: SetFuel of the fuel plus one, and a log
    /// line): one write of the sum; its fuel effect once.</item>
    /// <item>The shield generator's fuel (RPC_AddFuel: its own RPC_SetFuel of the fuel plus one): its own RPC_SetFuel of
    /// the fuel plus the units, sent to itself; its fuel effect once.</item>
    /// <item>The ballista's missiles (Turret.RPC_AddAmmo: "ammo" plus one, "ammoType", the effect at its body, the bolt's
    /// look refreshed, a log line): one write of the count and the kind; its effect and the bolt's look once.</item>
    /// </list>
    /// A light or station a friend's game looks after (fed as a hand would, a setting) gets the game's own message per
    /// item, as a hand's presses do: only its owner can write it.
    /// </summary>
    internal static class Adds
    {
        /// <summary>One lane's units into a smelter's queue (book: what to play the effect for).</summary>
        internal static void Ore(Fed fed, string prefab, int units, WriteBook book)
        {
            if (units <= 0)
            {
                return;
            }
            var smelter = (Smelter)fed.Component;
            if (fed.Mine && GameHooks.QueueOre != null && Stations.ConversionOf(smelter, prefab) != null)
            {
                for (int i = 0; i < units; i++)
                {
                    GameHooks.QueueOre(smelter, prefab, false);
                }
                book.Wrote(AddKind.Ore, units);
                return;
            }
            for (int i = 0; i < units; i++)
            {
                fed.View.InvokeRPC("RPC_AddOre", prefab, false);
            }
        }

        /// <summary>Units of fuel into a smelter or the hot tub.</summary>
        internal static void SmelterFuel(Fed fed, int units, WriteBook book)
        {
            if (units <= 0)
            {
                return;
            }
            if (fed.Mine)
            {
                ZDO zdo = fed.Zdo;
                zdo.Set(ZDOVars.s_fuel, zdo.GetFloat(ZDOVars.s_fuel) + units);
                book.Wrote(AddKind.Fuel, units);
                return;
            }
            for (int i = 0; i < units; i++)
            {
                fed.View.InvokeRPC("RPC_AddFuel");
            }
        }

        /// <summary>Units of fuel into a fire: one message of the amount to itself (its own write, effect and look).</summary>
        internal static void FireFuel(Fed fed, int units)
        {
            if (units <= 0)
            {
                return;
            }
            if (fed.Mine)
            {
                fed.View.InvokeRPC("RPC_AddFuelAmount", (float)units);
                return;
            }
            for (int i = 0; i < units; i++)
            {
                fed.View.InvokeRPC("RPC_AddFuel");
            }
        }

        /// <summary>Units of fuel into an oven or the Frost Foundry.</summary>
        internal static void CookingFuel(Fed fed, int units, WriteBook book)
        {
            if (units <= 0)
            {
                return;
            }
            if (fed.Mine)
            {
                ZDO zdo = fed.Zdo;
                zdo.Set(ZDOVars.s_fuel, zdo.GetFloat(ZDOVars.s_fuel) + units);
                book.Wrote(AddKind.Fuel, units);
                return;
            }
            for (int i = 0; i < units; i++)
            {
                fed.View.InvokeRPC("RPC_AddFuel");
            }
        }

        /// <summary>Units of fuel into a shield generator: its own RPC_SetFuel of the sum, to itself.</summary>
        internal static void ShieldFuel(Fed fed, int units, WriteBook book)
        {
            if (units <= 0)
            {
                return;
            }
            if (fed.Mine)
            {
                var shield = (ShieldGenerator)fed.Component;
                float fuel = fed.Zdo.GetFloat(ZDOVars.s_fuel, shield.m_defaultFuel);
                fed.View.InvokeRPC("RPC_SetFuel", fuel + units);
                book.Wrote(AddKind.Fuel, units);
                return;
            }
            for (int i = 0; i < units; i++)
            {
                fed.View.InvokeRPC("RPC_AddFuel");
            }
        }

        /// <summary>Units of one kind of missile into a ballista.</summary>
        internal static void Ammo(Fed fed, string prefab, int units, WriteBook book)
        {
            if (units <= 0)
            {
                return;
            }
            if (fed.Mine)
            {
                ZDO zdo = fed.Zdo;
                zdo.Set(ZDOVars.s_ammo, zdo.GetInt(ZDOVars.s_ammo) + units);
                zdo.Set(ZDOVars.s_ammoType, prefab);
                book.Wrote(AddKind.Ammo, units);
                return;
            }
            for (int i = 0; i < units; i++)
            {
                fed.View.InvokeRPC("RPC_AddAmmo", prefab);
            }
        }

        /// <summary>After a feed's writes: the game's added effect once for each kind written, and what the game's own add
        /// refreshes (the ballista's bolt). An effect that fails costs only the effect.</summary>
        internal static void Effects(Fed fed, WriteBook book)
        {
            foreach (AddKind kind in book.EffectsOwed())
            {
                try
                {
                    Transform at = fed.Component.transform;
                    switch (fed.Component)
                    {
                        case Smelter smelter:
                            if (kind == AddKind.Ore)
                            {
                                smelter.m_oreAddedEffects.Create(at.position, at.rotation);
                            }
                            else
                            {
                                smelter.m_fuelAddedEffects.Create(at.position, at.rotation, at);
                            }
                            break;
                        case CookingStation cooking:
                            cooking.m_fuelAddedEffects.Create(at.position, at.rotation, at);
                            break;
                        case ShieldGenerator shield:
                            shield.m_fuelAddedEffects.Create(at.position, at.rotation, at);
                            break;
                        case Turret turret:
                            Transform body = turret.m_turretBody != null ? turret.m_turretBody.transform : at;
                            turret.m_addAmmoEffect?.Create(body.position, body.rotation);
                            GameHooks.TurretUpdateVisualBolt?.Invoke(turret, null);
                            break;
                    }
                }
                catch (Exception error)
                {
                    if (!toldEffect)
                    {
                        toldEffect = true;
                        Module.Warn($"A station's added effect couldn't play (what was added is in it): {error.Message}");
                    }
                }
            }
        }

        private static bool toldEffect;
    }
}
