using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CraftFromChests
{
    /// <summary>
    /// Filling stations from chests, as AzuCraftyBoxes did. Pressing E on a smelter (and the kiln, blast furnace,
    /// spinning wheel, windmill and eitr refinery, which are smelters too), a fire or torch, a cooking station or the
    /// oven, the fermenter, the shield generator or the ballista, with none of what it takes on him, takes one from the
    /// nearest chest that holds it; with the fill key held, it fills the station as far as it goes, first from what he
    /// carries, then from the chests. Whatever he carries of it otherwise goes in as the game does it. The chests are
    /// claimed first (a Request); what they give is taken out as soon as they're his, and sent to the station with the
    /// station's own network call, never more than it has room for: a fire, a cooking station and the fermenter throw
    /// away what doesn't fit. What a friend's open chest gives (through his game) arrives in his inventory first, and goes
    /// to the station from there, after what his own chests give.
    /// </summary>
    internal static class Stations
    {
        private static Request pending;
        private static Component pendingFor;

        internal static bool On => Module.IsOn && Settings.FillStations.Value;

        /// <summary>What one station takes, in its own order; how many more fit, read from its record when asked; and how
        /// one is sent to it.</summary>
        internal sealed class Feed
        {
            internal Component Station;
            internal ZNetView View;
            internal List<ItemDrop> Kinds;
            internal Func<int> Room;
            internal Action<ItemDrop, bool> AddOne;

            /// <summary>Only one kind at a time (the ballista takes one kind of bolt until it's empty).</summary>
            internal bool OneKind;

            internal bool Valid => Station != null && View != null && View.IsValid();
        }

        internal static bool Filling => Settings.Held(Settings.FillKey.Value);

        /// <summary>
        /// His press of E on a station, when the game would find nothing on him, or with the fill key held. Returns true
        /// if it was handled here (the game's own method is then skipped); false to let the game do it (it's full, or
        /// neither he nor the chests have anything for it, and the game says so).
        /// </summary>
        internal static bool Press(Feed feed, Humanoid user, bool fill)
        {
            if (pending != null && !pending.Finished)
            {
                // A fill from chests is under way: this station's repeated presses wait for it; another's goes to the game.
                return ReferenceEquals(pendingFor, feed.Station);
            }
            pending = null;
            pendingFor = null;
            int room = feed.Room();
            if (room <= 0 || feed.Kinds.Count == 0)
            {
                return false;
            }
            Inventory inventory = user.GetInventory();
            var added = new Dictionary<ItemDrop, int>();
            ItemDrop onlyKind = null;
            if (fill)
            {
                foreach (ItemDrop kind in feed.Kinds)
                {
                    if (room <= 0)
                    {
                        break;
                    }
                    if (feed.OneKind && onlyKind != null && kind != onlyKind)
                    {
                        continue;
                    }
                    string name = kind.m_itemData.m_shared.m_name;
                    foreach (ItemDrop.ItemData item in inventory.GetAllItems().Where(each => each.m_shared.m_name == name && each.m_worldLevel >= Game.m_worldLevel).ToList())
                    {
                        int count = Mathf.Min(room, item.m_stack);
                        for (int i = 0; i < count; i++)
                        {
                            bool cheated = item.m_cheated;
                            if (!inventory.RemoveOneItem(item))
                            {
                                break;
                            }
                            feed.AddOne(kind, cheated);
                            room--;
                            added.TryGetValue(kind, out int sum);
                            added[kind] = sum + 1;
                        }
                    }
                    if (feed.OneKind && added.ContainsKey(kind))
                    {
                        onlyKind = kind;
                    }
                }
            }

            // From the chests: what still fits, by the kinds in the station's order, from what this computer's copies of
            // the chests show (a press without the fill key: one, and only when he carried none).
            var wanted = new Dictionary<string, int>();
            int want = fill ? room : added.Count > 0 ? 0 : Mathf.Min(room, 1);
            foreach (ItemDrop kind in feed.Kinds)
            {
                if (want <= 0)
                {
                    break;
                }
                if (feed.OneKind && onlyKind != null && kind != onlyKind)
                {
                    continue;
                }
                string name = kind.m_itemData.m_shared.m_name;
                int take = Mathf.Min(want, Stock.Of(name));
                if (take > 0)
                {
                    wanted.TryGetValue(name, out int sum);
                    wanted[name] = sum + take;
                    want -= take;
                    if (feed.OneKind)
                    {
                        onlyKind = kind;
                    }
                }
            }
            if (added.Count > 0)
            {
                Say(added, fromChests: false);
            }
            if (wanted.Count == 0)
            {
                return added.Count > 0;
            }
            pendingFor = feed.Station;
            pending = Supply.Start(wanted, request => Arrived(feed, request));
            return true;
        }

        // In the frame the chests are his: take out what still fits, and send it to the station.
        private static void Arrived(Feed feed, Request request)
        {
            pending = null;
            pendingFor = null;
            if (!On || !feed.Valid)
            {
                return;   // switched off meanwhile, or the station is gone: nothing is taken
            }
            request.Refresh();   // chests from an earlier round may have been a friend's in between
            int room = feed.Room();
            var added = new Dictionary<ItemDrop, int>();
            try
            {
                foreach (ItemDrop kind in feed.Kinds)
                {
                    string name = kind.m_itemData.m_shared.m_name;
                    if (room <= 0 || !request.Wanted.TryGetValue(name, out int wanted) || added.ContainsKey(kind))
                    {
                        continue;
                    }
                    // One at a time, taken then given: a throw part-way loses at most the one in hand. His own chests give
                    // what friends' open chests didn't (that is in his inventory, and goes in next).
                    int fromFriendsInAll = Mathf.Min(request.ArrivedByName(name), wanted);
                    int given = 0;
                    while (given < wanted - fromFriendsInAll && room > 0 && request.Remove(name, 1) == 1)
                    {
                        feed.AddOne(kind, false);
                        given++;
                        room--;
                    }
                    // Then what friends' open chests gave, from his inventory, where it arrived: no more of it than came.
                    Inventory inventory = Player.m_localPlayer != null ? Player.m_localPlayer.GetInventory() : null;
                    int fromFriends = inventory != null ? Mathf.Min(fromFriendsInAll, wanted - given) : 0;
                    while (fromFriends > 0 && room > 0)
                    {
                        ItemDrop.ItemData item = inventory.GetAllItems().FirstOrDefault(each => each.m_shared.m_name == name && each.m_worldLevel >= Game.m_worldLevel);
                        bool cheated = item != null && item.m_cheated;
                        if (item == null || !inventory.RemoveOneItem(item))
                        {
                            break;
                        }
                        feed.AddOne(kind, cheated);
                        fromFriends--;
                        given++;
                        room--;
                    }
                    if (given > 0)
                    {
                        added[kind] = given;
                    }
                }
            }
            finally
            {
                request.Send();   // even if another mod's handler threw mid-take: a changed chest is sent and held
            }
            if (added.Count > 0)
            {
                Say(added, fromChests: true);
            }
            else
            {
                Messages.Short(request, "anything for it");
            }
        }

        // "Added 10 Copper ore", as the game says it, with "from chests" for what came from them.
        private static void Say(Dictionary<ItemDrop, int> added, bool fromChests)
        {
            string text = string.Join("\n", added.Select(each => $"$msg_added {each.Value} {each.Key.m_itemData.m_shared.m_name}"));
            Messages.Center(Localization.instance.Localize(text) + (fromChests ? " (from chests)" : string.Empty));
        }

        /// <summary>The hover line naming the fill key, added to a station's hover text.</summary>
        internal static string Hint(string text)
        {
            if (!On || !Settings.StationHint.Value || string.IsNullOrEmpty(text) || Settings.FillKey.Value.MainKey == KeyCode.None)
            {
                return text;
            }
            string key = Settings.FillKey.Value.ToString().Replace("Left", string.Empty).Replace("Right", string.Empty);
            return text + Localization.instance.Localize($"\n[<color=yellow><b>{key} + $KEY_Use</b></color>] Fill from what you carry and the chests");
        }

        // Each kind of station: what it takes, how much room it has, and how one goes in, as its own code does it.

        internal static Feed Smelter(Smelter smelter)
        {
            var view = GameHooks.Read<ZNetView>(GameHooks.SmelterView, smelter);
            string[] kiln = Utils.GetPrefabName(smelter.gameObject) == "charcoal_kiln" ? Settings.KilnWoods() : null;
            return new Feed
            {
                Station = smelter,
                View = view,
                Kinds = smelter.m_conversion.Where(conversion => conversion?.m_from != null)
                                            .Select(conversion => conversion.m_from)
                                            .Where(from => GameHooks.Call<bool>(GameHooks.SmelterAllows, smelter, from.gameObject.name))
                                            .Where(from => kiln == null || kiln.Contains(from.gameObject.name))
                                            .ToList(),
                Room = () => smelter.m_maxOre - GameHooks.Call<int>(GameHooks.SmelterQueueSize, smelter),
                AddOne = (kind, cheated) => view.InvokeRPC("RPC_AddOre", kind.gameObject.name, cheated),
            };
        }

        internal static Feed SmelterFuel(Smelter smelter)
        {
            var view = GameHooks.Read<ZNetView>(GameHooks.SmelterView, smelter);
            return new Feed
            {
                Station = smelter,
                View = view,
                Kinds = new List<ItemDrop> { smelter.m_fuelItem },
                Room = () => FuelRoom(GameHooks.Call<float>(GameHooks.SmelterFuel, smelter), smelter.m_maxFuel),
                AddOne = (kind, cheated) => view.InvokeRPC("RPC_AddFuel"),
            };
        }

        internal static Feed Fire(Fireplace fire)
        {
            var view = GameHooks.Read<ZNetView>(GameHooks.FireplaceView, fire);
            return new Feed
            {
                Station = fire,
                View = view,
                Kinds = new List<ItemDrop> { fire.m_fuelItem },
                Room = () => FireRoom(view.GetZDO().GetFloat(ZDOVars.s_fuel), fire.m_maxFuel),
                AddOne = (kind, cheated) => view.InvokeRPC("RPC_AddFuel"),
            };
        }

        internal static Feed CookingFood(CookingStation station)
        {
            var view = GameHooks.Read<ZNetView>(GameHooks.CookingView, station);
            var incompatible = new HashSet<string>(station.m_incompatibleItems.Where(each => each?.m_item != null).Select(each => each.m_item.m_itemData.m_shared.m_name));
            return new Feed
            {
                Station = station,
                View = view,
                Kinds = station.m_conversion.Where(conversion => conversion?.m_from != null)
                                            .Select(conversion => conversion.m_from)
                                            .Where(from => !incompatible.Contains(from.m_itemData.m_shared.m_name))
                                            .Where(from => GameHooks.Call<bool>(GameHooks.CookingAllows, station, from.gameObject.name))
                                            .ToList(),
                Room = () => FreeSlots(station, view),
                AddOne = (kind, cheated) => view.InvokeRPC("RPC_AddItem", kind.gameObject.name, cheated),
            };
        }

        internal static Feed CookingFuel(CookingStation station)
        {
            var view = GameHooks.Read<ZNetView>(GameHooks.CookingView, station);
            return new Feed
            {
                Station = station,
                View = view,
                Kinds = new List<ItemDrop> { station.m_fuelItem },
                Room = () => FuelRoom(GameHooks.Call<float>(GameHooks.CookingFuel, station), station.m_maxFuel),
                AddOne = (kind, cheated) => view.InvokeRPC("RPC_AddFuel"),
            };
        }

        internal static Feed Fermenter(Fermenter fermenter)
        {
            var view = GameHooks.Read<ZNetView>(GameHooks.FermenterView, fermenter);
            return new Feed
            {
                Station = fermenter,
                View = view,
                Kinds = fermenter.m_conversion.Where(conversion => conversion?.m_from != null).Select(conversion => conversion.m_from).ToList(),
                Room = () => FermenterEmpty(fermenter) ? 1 : 0,
                AddOne = (kind, cheated) => view.InvokeRPC("RPC_AddItem", kind.gameObject.name.GetStableHashCode(), cheated),
            };
        }

        internal static Feed Shield(ShieldGenerator shield)
        {
            var view = GameHooks.Read<ZNetView>(GameHooks.ShieldView, shield);
            return new Feed
            {
                Station = shield,
                View = view,
                Kinds = shield.m_fuelItems.Where(fuel => fuel != null).ToList(),
                Room = () => FuelRoom(GameHooks.Call<float>(GameHooks.ShieldFuel, shield), shield.m_maxFuel),
                AddOne = (kind, cheated) => view.InvokeRPC("RPC_AddFuel"),
            };
        }

        internal static Feed Ballista(Turret turret)
        {
            var view = GameHooks.Read<ZNetView>(GameHooks.TurretView, turret);
            // Loaded already: only more of the same kind of bolt.
            string loaded = turret.GetAmmo() > 0 ? turret.GetAmmoType() : null;
            return new Feed
            {
                Station = turret,
                View = view,
                Kinds = turret.m_allowedAmmo.Where(each => each.m_ammo != null)
                                            .Select(each => each.m_ammo)
                                            .Where(ammo => GameHooks.Call<bool>(GameHooks.TurretAllows, turret, ammo.gameObject.name))
                                            .Where(ammo => loaded == null || ammo.gameObject.name == loaded)
                                            .ToList(),
                Room = () => turret.m_maxAmmo - turret.GetAmmo(),
                AddOne = (kind, cheated) => view.InvokeRPC("RPC_AddAmmo", kind.gameObject.name),
                OneKind = true,
            };
        }

        /// <summary>True if he carries any of these (at this world's level, as the game's own lookups count).</summary>
        internal static bool Carries(Humanoid user, IEnumerable<ItemDrop> kinds) =>
            kinds.Any(kind => kind != null && user.GetInventory().HaveItem(kind.m_itemData.m_shared.m_name));

        // A smelter, cooking station or shield generator takes fuel while it has at most its capacity less one; each adds
        // one (Smelter.OnAddFuel and RPC_AddFuel).
        private static int FuelRoom(float fuel, int maxFuel) => fuel > maxFuel - 1 ? 0 : Mathf.FloorToInt(maxFuel - fuel);

        // A fire adds one while its fuel, rounded up, is below its capacity, and throws away the rest (Fireplace.RPC_AddFuel).
        private static int FireRoom(float fuel, float maxFuel)
        {
            int room = 0;
            float level = Mathf.Max(0f, fuel);
            while (Mathf.CeilToInt(level) < maxFuel && room < 1000)
            {
                level = Mathf.Min(level + 1f, maxFuel);
                room++;
            }
            return room;
        }

        // A cooking station's empty slots (its record's "slot0", "slot1" … are empty), each taking one item.
        private static int FreeSlots(CookingStation station, ZNetView view)
        {
            int free = 0;
            for (int i = 0; i < station.m_slots.Length; i++)
            {
                if (view.GetZDO().GetString("slot" + i) == string.Empty)
                {
                    free++;
                }
            }
            return free;
        }

        internal static bool FermenterEmpty(Fermenter fermenter) => GameHooks.FermenterStatus.Invoke(fermenter, null)?.ToString() == "Empty";

        internal static void Reset()
        {
            pending = null;
            pendingFor = null;
        }
    }

    // The game's stations, patched. Each patch asks whether the mod is on first and changes nothing while it's off; each
    // hands the press to the game unless it was handled here.

    [HarmonyPatch(typeof(Smelter), "OnAddOre")]
    internal static class FillSmelter
    {
        [HarmonyPriority(Priority.Low)]   // after other mods' prefixes, so it sees whether one has stopped the press
        private static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, bool __runOriginal, ref bool __result)
        {
            if (!__runOriginal)
            {
                return true;   // another mod stopped this press already (TimedTorchesStayLit on its torches): leave it be
            }
            // An item in hand (from the hotbar) goes in as the game does it.
            if (item != null || !Stations.On || !GameHooks.Smelters || user != Player.m_localPlayer)
            {
                return true;
            }
            bool handled = false;
            Module.Guard("filling a smelter from chests", () =>
            {
                bool fill = Stations.Filling;
                // What he carries goes in as the game does it, one at a time, unless he fills.
                if (!fill && Stations.Carries(user, __instance.m_conversion.Where(each => each?.m_from != null).Select(each => each.m_from)))
                {
                    return;
                }
                handled = Stations.Press(Stations.Smelter(__instance), user, fill);
            });
            if (handled)
            {
                __result = true;
            }
            return !handled;
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
    internal static class FuelSmelter
    {
        [HarmonyPriority(Priority.Low)]   // after other mods' prefixes, so it sees whether one has stopped the press
        private static bool Prefix(Smelter __instance, Humanoid user, ItemDrop.ItemData item, bool __runOriginal, ref bool __result)
        {
            if (!__runOriginal)
            {
                return true;   // another mod stopped this press already (TimedTorchesStayLit on its torches): leave it be
            }
            if (item != null || !Stations.On || !GameHooks.Smelters || user != Player.m_localPlayer || __instance.m_fuelItem == null)
            {
                return true;
            }
            bool handled = false;
            Module.Guard("fuelling a smelter from chests", () =>
            {
                bool fill = Stations.Filling;
                if (!fill && Stations.Carries(user, new[] { __instance.m_fuelItem }))
                {
                    return;
                }
                handled = Stations.Press(Stations.SmelterFuel(__instance), user, fill);
            });
            if (handled)
            {
                __result = true;
            }
            return !handled;
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
    internal static class FuelFire
    {
        [HarmonyPriority(Priority.Low)]   // after other mods' prefixes, so it sees whether one has stopped the press
        private static bool Prefix(Fireplace __instance, Humanoid user, bool hold, bool alt, bool __runOriginal, ref bool __result)
        {
            if (!__runOriginal)
            {
                return true;   // another mod stopped this press already (TimedTorchesStayLit on its torches): leave it be
            }
            // Holding E repeats the game's own adding; only a fresh press reaches the chests.
            if (hold || !Stations.On || !GameHooks.Fireplaces || user != Player.m_localPlayer || !__instance.m_canRefill ||
                __instance.m_infiniteFuel || __instance.m_fuelItem == null)
            {
                return true;
            }
            bool handled = false;
            Module.Guard("fuelling a fire from chests", () =>
            {
                var view = GameHooks.Read<ZNetView>(GameHooks.FireplaceView, __instance);
                if (view == null || !view.IsValid())
                {
                    return;
                }
                // A plain E on a fire that can be put out puts it out, as in the game.
                if (__instance.m_canTurnOff && !alt && view.GetZDO().GetFloat(ZDOVars.s_fuel) > 0f)
                {
                    return;
                }
                bool fill = Stations.Filling;
                if (!fill && Stations.Carries(user, new[] { __instance.m_fuelItem }))
                {
                    return;
                }
                if (!view.HasOwner())
                {
                    view.ClaimOwnership();   // as the game's own Interact does, so the fuel has someone to go to
                }
                handled = Stations.Press(Stations.Fire(__instance), user, fill);
            });
            if (handled)
            {
                __result = true;
            }
            return !handled;
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnInteract")]
    internal static class FillCookingStation
    {
        [HarmonyPriority(Priority.Low)]   // after other mods' prefixes, so it sees whether one has stopped the press
        private static bool Prefix(CookingStation __instance, Humanoid user, bool __runOriginal, ref bool __result)
        {
            if (!__runOriginal)
            {
                return true;   // another mod stopped this press already (TimedTorchesStayLit on its torches): leave it be
            }
            if (!Stations.On || !GameHooks.CookingStations || user != Player.m_localPlayer)
            {
                return true;
            }
            bool handled = false;
            Module.Guard("filling a cooking station from chests", () =>
            {
                // E takes a cooked item off first; without fire, the game says it needs one.
                if (GameHooks.Call<bool>(GameHooks.CookingHasDoneItem, __instance) ||
                    (__instance.m_requireFire && !GameHooks.Call<bool>(GameHooks.CookingFireLit, __instance)))
                {
                    return;
                }
                bool fill = Stations.Filling;
                if (!fill && Stations.Carries(user, __instance.m_conversion.Where(each => each?.m_from != null).Select(each => each.m_from)))
                {
                    return;
                }
                var view = GameHooks.Read<ZNetView>(GameHooks.CookingView, __instance);
                if (view == null || !view.IsValid())
                {
                    return;
                }
                if (!view.HasOwner())
                {
                    view.ClaimOwnership();   // as the game's own CookItem does
                }
                handled = Stations.Press(Stations.CookingFood(__instance), user, fill);
            });
            if (handled)
            {
                __result = true;
            }
            return !handled;
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")]
    internal static class FuelCookingStation
    {
        [HarmonyPriority(Priority.Low)]   // after other mods' prefixes, so it sees whether one has stopped the press
        private static bool Prefix(CookingStation __instance, Humanoid user, ItemDrop.ItemData item, bool __runOriginal, ref bool __result)
        {
            if (!__runOriginal)
            {
                return true;   // another mod stopped this press already (TimedTorchesStayLit on its torches): leave it be
            }
            if (item != null || !Stations.On || !GameHooks.CookingStations || user != Player.m_localPlayer || __instance.m_fuelItem == null)
            {
                return true;
            }
            bool handled = false;
            Module.Guard("fuelling an oven from chests", () =>
            {
                bool fill = Stations.Filling;
                if (!fill && Stations.Carries(user, new[] { __instance.m_fuelItem }))
                {
                    return;
                }
                handled = Stations.Press(Stations.CookingFuel(__instance), user, fill);
            });
            if (handled)
            {
                __result = true;
            }
            return !handled;
        }
    }

    [HarmonyPatch(typeof(Fermenter), nameof(Fermenter.Interact))]
    internal static class FillFermenter
    {
        [HarmonyPriority(Priority.Low)]   // after other mods' prefixes, so it sees whether one has stopped the press
        private static bool Prefix(Fermenter __instance, Humanoid user, bool hold, bool __runOriginal, ref bool __result)
        {
            if (!__runOriginal)
            {
                return true;   // another mod stopped this press already (TimedTorchesStayLit on its torches): leave it be
            }
            if (hold || !Stations.On || !GameHooks.Fermenters || user != Player.m_localPlayer)
            {
                return true;
            }
            bool handled = false;
            Module.Guard("filling a fermenter from chests", () =>
            {
                if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, flash: false))
                {
                    return;   // the game flashes the ward and says no
                }
                GameHooks.FermenterUpdateCover.Invoke(__instance, new object[] { 0f, true });
                // Not empty, or no roof, or out in the open: the game taps it or says why.
                if (!Stations.FermenterEmpty(__instance) || !GameHooks.Read<bool>(GameHooks.FermenterRoof, __instance) ||
                    GameHooks.Read<bool>(GameHooks.FermenterExposed, __instance))
                {
                    return;
                }
                if (Stations.Carries(user, __instance.m_conversion.Where(each => each?.m_from != null).Select(each => each.m_from)))
                {
                    return;
                }
                handled = Stations.Press(Stations.Fermenter(__instance), user, fill: false);
            });
            if (handled)
            {
                __result = true;
            }
            return !handled;
        }
    }

    [HarmonyPatch(typeof(ShieldGenerator), "OnAddFuel")]
    internal static class FuelShield
    {
        [HarmonyPriority(Priority.Low)]   // after other mods' prefixes, so it sees whether one has stopped the press
        private static bool Prefix(ShieldGenerator __instance, Humanoid user, ItemDrop.ItemData item, bool __runOriginal, ref bool __result)
        {
            if (!__runOriginal)
            {
                return true;   // another mod stopped this press already (TimedTorchesStayLit on its torches): leave it be
            }
            if (item != null || !Stations.On || !GameHooks.ShieldGenerators || user != Player.m_localPlayer)
            {
                return true;
            }
            bool handled = false;
            Module.Guard("fuelling a shield generator from chests", () =>
            {
                bool fill = Stations.Filling;
                if (!fill && Stations.Carries(user, __instance.m_fuelItems))
                {
                    return;
                }
                handled = Stations.Press(Stations.Shield(__instance), user, fill);
            });
            if (handled)
            {
                __result = true;
            }
            return !handled;
        }
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.UseItem))]
    internal static class LoadBallista
    {
        [HarmonyPriority(Priority.Low)]   // after other mods' prefixes, so it sees whether one has stopped the press
        private static bool Prefix(Turret __instance, Humanoid user, ItemDrop.ItemData item, bool __runOriginal, ref bool __result)
        {
            if (!__runOriginal)
            {
                return true;   // another mod stopped this press already (TimedTorchesStayLit on its torches): leave it be
            }
            // An item in hand (bolts, or a trophy to set its targets) goes in as the game does it.
            if (item != null || !Stations.On || !GameHooks.Turrets || user != Player.m_localPlayer)
            {
                return true;
            }
            bool handled = false;
            Module.Guard("loading a ballista from chests", () =>
            {
                Stations.Feed feed = Stations.Ballista(__instance);
                bool fill = Stations.Filling;
                if (!fill && Stations.Carries(user, feed.Kinds))
                {
                    return;
                }
                handled = Stations.Press(feed, user, fill);
            });
            if (handled)
            {
                __result = true;
            }
            return !handled;
        }
    }

    // The fill hint on each station's hover text.

    [HarmonyPatch(typeof(Smelter), "OnHoverAddOre")]
    internal static class HintSmelterOre
    {
        private static void Postfix(ref string __result)
        {
            if (Stations.On && GameHooks.Smelters)
            {
                string text = __result;
                Module.Guard("adding the fill hint", () => text = Stations.Hint(text));
                __result = text;
            }
        }
    }

    [HarmonyPatch(typeof(Smelter), "OnHoverAddFuel")]
    internal static class HintSmelterFuel
    {
        private static void Postfix(ref string __result)
        {
            if (Stations.On && GameHooks.Smelters)
            {
                string text = __result;
                Module.Guard("adding the fill hint", () => text = Stations.Hint(text));
                __result = text;
            }
        }
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.GetHoverText))]
    internal static class HintFire
    {
        private static void Postfix(Fireplace __instance, ref string __result)
        {
            if (Stations.On && GameHooks.Fireplaces && __instance.m_canRefill && !__instance.m_infiniteFuel)
            {
                string text = __result;
                Module.Guard("adding the fill hint", () => text = Stations.Hint(text));
                __result = text;
            }
        }
    }

    [HarmonyPatch(typeof(CookingStation), nameof(CookingStation.GetHoverText))]
    internal static class HintCookingStation
    {
        private static void Postfix(ref string __result)
        {
            if (Stations.On && GameHooks.CookingStations)
            {
                string text = __result;
                Module.Guard("adding the fill hint", () => text = Stations.Hint(text));
                __result = text;
            }
        }
    }

    [HarmonyPatch(typeof(CookingStation), "OnHoverFuelSwitch")]
    internal static class HintOvenFuel
    {
        private static void Postfix(ref string __result)
        {
            if (Stations.On && GameHooks.CookingStations)
            {
                string text = __result;
                Module.Guard("adding the fill hint", () => text = Stations.Hint(text));
                __result = text;
            }
        }
    }

    [HarmonyPatch(typeof(ShieldGenerator), "OnHoverAddFuel")]
    internal static class HintShieldFuel
    {
        private static void Postfix(ref string __result)
        {
            if (Stations.On && GameHooks.ShieldGenerators)
            {
                string text = __result;
                Module.Guard("adding the fill hint", () => text = Stations.Hint(text));
                __result = text;
            }
        }
    }

    [HarmonyPatch(typeof(Turret), nameof(Turret.GetHoverText))]
    internal static class HintBallista
    {
        private static void Postfix(ref string __result)
        {
            if (Stations.On && GameHooks.Turrets)
            {
                string text = __result;
                Module.Guard("adding the fill hint", () => text = Stations.Hint(text));
                __result = text;
            }
        }
    }
}
