using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using QoLMods.Chests;
using UnityEngine;

namespace CraftFromChests
{
    /// <summary>
    /// Building from chests. The build menu, the build panel and the game's own "have I enough" before placing count what
    /// he carries and every chest nearby (Player.HaveRequirements(Piece …), through the survey), as the crafting menu
    /// does. A click can't wait for a chest's owner, so the chests he'd be short of that aren't his yet are asked for as
    /// soon as he picks a piece, and every half second while one isn't (a friend opened it meanwhile), except a chest
    /// someone else is taking from, which only a click asks for: only asked, never held, so nobody is kept out of them. A
    /// chest a friend on the pack has open is taken from through his game only at a click (ChestShare): what it gives lands
    /// in his inventory, and the next click places the piece from there. When he places it (Player.TryPlacePiece), the chests he owns right then are
    /// reloaded and planned, for this piece and for every extra plant PlantEasily will place with it; if they give what
    /// he's short of, the game places it and every ConsumeResources in that frame takes what he carries and the rest from
    /// the chests he owns (Taking). If they don't, nothing is placed: he's told, the rest is asked for, and the next click
    /// places it.
    ///
    /// Two places ask "can I place it" and then pay without that check: PlantEasily's ghost grid (inside
    /// Player.UpdatePlacementGhost) and its replanting on harvest (inside Pickable.SetPicked). There the answer counts
    /// only the chests he owns right now, and a yes approves paying from them in that frame, so whatever was told yes pays
    /// in full.
    /// </summary>
    internal static class Building
    {
        private const float AskEvery = 0.5f;

        /// <summary>How long before the same chests are asked for again for the same piece.</summary>
        private const float AskAgainAfter = 3f;

        private static int placingFrame = -1;
        private static int strictDepth;
        private static float nextLook;
        private static Piece askedFor;
        private static float askedAt;


        // This computer's copy of each chest near him, as last seen: its items as its record holds them (a fingerprint, not
        // the record's revision, which a ship's rocking, snow or rain changes too), and when they changed while the chest
        // wasn't his (someone else taking from it, or putting into it). Asking ahead leaves such a chest alone a while, and
        // a chest not yet seen twice in a row: asked during its owner's hold after a take, the request is dropped and the
        // chest kept from its owner a while more (ChestClaims), so two players building from one chest would take it from
        // each other by turns.
        private sealed class Seen
        {
            internal int Items;
            internal float SeenAt;
            internal bool Compared;
            internal float ChangedAt = float.NegativeInfinity;
        }

        private static readonly Dictionary<Container, Seen> seen = new Dictionary<Container, Seen>();

        internal static bool On => Module.IsOn && Settings.BuildFromChests.Value;

        internal static void Reset()
        {
            placingFrame = -1;
            strictDepth = 0;
        }

        /// <summary>True in a frame in which this mod approved paying for pieces from the chests he owns: every
        /// ConsumeResources in it that isn't a craft's takes what he's short of from those chests.</summary>
        internal static bool PlacingThisFrame => placingFrame == Time.frameCount;

        /// <summary>True inside PlantEasily's two places (see the class).</summary>
        internal static bool Strict => strictDepth > 0;

        internal static void EnterStrict() => strictDepth++;

        internal static void LeaveStrict()
        {
            if (strictDepth > 0)
            {
                strictDepth--;
            }
        }

        /// <summary>From the "can build" check in a strict place, when it said yes thanks to the chests he owns.</summary>
        internal static void Approved() => placingFrame = Time.frameCount;

        /// <summary>The piece wasn't placed after all: nothing in this frame is paid from the chests.</summary>
        internal static void NotPlaced() => placingFrame = -1;

        /// <summary>Every frame: while he holds a building tool with a piece picked, asks for the chests he'd be short of
        /// that aren't his yet.</summary>
        internal static void Update()
        {
            if (!On || Time.time < nextLook)
            {
                return;
            }
            nextLook = Time.time + AskEvery;
            Player player = Player.m_localPlayer;
            if (player == null || !player.InPlaceMode())
            {
                return;
            }
            Watch(player.transform.position);
            if (ChestClaims.Busy)
            {
                return;
            }
            Piece piece = player.GetSelectedPiece();
            // For PlantEasily's grid, the plants it shows short of seeds too: once their chests are his, they can be planted.
            Dictionary<string, int> wanted = ShortOf(player, piece, 1 + PlantEasily.Extras(player, piece, ifPaid: true));
            if (wanted == null || wanted.Count == 0 || (piece == askedFor && Time.time - askedAt < AskAgainAfter))
            {
                return;
            }
            Request mine = Supply.FromOwned(wanted, player.transform.position);
            // With "My personal chests first" (card A80), his personal chests holding some of it are asked for too while
            // they aren't his, even when the chests he holds would give it all: a click takes only from chests his game holds.
            List<Container> personal = Settings.PersonalFirst.Value
                ? Supply.PersonalNotMine(wanted, player.transform.position, NotToAskAhead)
                : null;
            if (!mine.Covered || (personal != null && personal.Count > 0))
            {
                bool noRoom = false;
                AskFor(piece, player, mine.Covered ? new Dictionary<string, int>() : mine.Left, ahead: true, ref noRoom, personal);
            }
        }

        // Every half second in place mode: notes which chests near him someone else is taking from (see Seen).
        private static void Watch(Vector3 centre)
        {
            float now = Time.time;
            foreach (Container gone in seen.Keys.Where(chest => chest == null).ToList())
            {
                seen.Remove(gone);
            }
            foreach (Container chest in ChestFinder.Find(centre, Settings.Rules()))
            {
                ZNetView view = ChestHooks.NetView(chest);
                int items = Fingerprint(view.GetZDO().GetByteArray(ZDOVars.s_items));
                if (!seen.TryGetValue(chest, out Seen before))
                {
                    seen[chest] = new Seen { Items = items, SeenAt = now };
                    continue;
                }
                // Compared only with a look just before: a chest seen again after a while starts afresh.
                bool justBefore = now - before.SeenAt <= 2f * AskEvery;
                if (justBefore && items != before.Items && !view.IsOwner())
                {
                    before.ChangedAt = now;
                }
                before.Compared = justBefore;
                before.Items = items;
                before.SeenAt = now;
            }
        }

        // The chest's items as its owner last saved them (Container.Save), as a number that changes when they do.
        private static int Fingerprint(byte[] items)
        {
            if (items == null)
            {
                return 0;
            }
            unchecked
            {
                int hash = (int)2166136261;
                foreach (byte each in items)
                {
                    hash = (hash ^ each) * 16777619;
                }
                return hash ^ items.Length;
            }
        }

        // True for a chest to leave alone when asking ahead: one not yet seen twice in a row, one whose items changed in
        // the last few seconds while it wasn't his (see Seen), or one beyond the server's hand-out that only a claim here
        // could get (card A64): that is claimed at a click, never every few seconds ahead of one.
        private static bool NotToAskAhead(Container chest) =>
            !seen.TryGetValue(chest, out Seen look) || !look.Compared || Time.time - look.ChangedAt < ChestClaims.HoldFor + ChestClaims.LeaveAloneFor ||
            ChestClaims.NeedsFarClaim(chest);

        /// <summary>
        /// Before the game places a piece: true to let it, approving every payment in this frame from the chests he owns;
        /// false to place nothing this time (he's told why, and what's missing is asked for).
        /// </summary>
        internal static bool BeforePlacing(Player player, Piece piece)
        {
            int pieces = 1 + PlantEasily.Extras(player, piece);
            Dictionary<string, int> wanted = ShortOf(player, piece, pieces);
            if (wanted == null)
            {
                placingFrame = -1;
                return true;   // free: nothing is paid
            }
            Request mine = wanted.Count > 0 ? Supply.FromOwned(wanted, player.transform.position) : null;
            if (mine == null || mine.Covered)
            {
                placingFrame = Time.frameCount;   // he carries it all, or the chests he owns give the rest
                return true;
            }
            placingFrame = -1;
            bool noRoom = false;
            bool asked = AskFor(piece, player, mine.Left, ahead: false, ref noRoom);
            // Which item, how far its chest is and why it isn't his yet, said and logged (card A69): never "place it
            // again" for a chest that can't come.
            List<ShortItem> items = ChestExplain.Shortfall(Module.Chests, mine.Left, player.transform.position, Settings.Rules());
            ChestExplain.Log(Module.Chests, $"Placing {Localization.instance.Localize(piece.m_name)} (at a click)", items);
            string text = ChestExplain.PlaceMessage(items);
            if (asked && noRoom)
            {
                Messages.Center("No room in your inventory for the materials from a chest a friend has open.");
            }
            else if (!asked && pieces > 1 && text == null)
            {
                // PlantEasily's green ghosts can be old (it checks them again when the grid moves).
                Messages.Center($"Not enough in the chests you can use for all {pieces} plants: move the grid a little, or plant fewer.");
            }
            else if (text != null)
            {
                Messages.Center(text);
            }
            else if (asked)
            {
                Messages.Center("Getting the materials from a chest nearby: place it again in a moment.");
            }
            else if (Supply.WaitedOnHolds(mine.Left))
            {
                Messages.Center("Someone else is about to open a chest with what it needs: place it again in a few seconds.");
            }
            else
            {
                QoLMods.Kit.Say.Center("$msg_missingrequirement");
            }
            return false;
        }

        /// <summary>
        /// Player.HaveRequirements(Piece …) said no: yes if the chests make up what he's short of, and the game's other
        /// conditions hold, for the two modes that count materials (placing, and the build menu's "have some"). In a
        /// strict place, "placing" counts only the chests he owns right now.
        /// </summary>
        internal static bool HaveWithChests(Player player, Piece piece, Player.RequirementMode mode)
        {
            if (piece.m_craftingStation != null)
            {
                if (mode == Player.RequirementMode.CanAlmostBuild)
                {
                    var known = GameHooks.KnownStations != null ? GameHooks.Read<Dictionary<string, int>>(GameHooks.KnownStations, player) : null;
                    if (known == null || !known.ContainsKey(piece.m_craftingStation.m_name))
                    {
                        return false;
                    }
                }
                else if (!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, player.transform.position) &&
                         !ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoWorkbench))
                {
                    return false;
                }
            }
            if (piece.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(piece.m_dlc))
            {
                return false;
            }
            bool owned = mode == Player.RequirementMode.CanBuild && Strict;
            Inventory inventory = player.GetInventory();
            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (!requirement.m_resItem || requirement.m_amount <= 0)
                {
                    continue;
                }
                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                if (mode == Player.RequirementMode.CanAlmostBuild)
                {
                    if (!inventory.HaveItem(name) && Stock.Of(name) <= 0)
                    {
                        return false;
                    }
                }
                else if (inventory.CountItems(name) + (owned ? Stock.OwnedOf(name) : Stock.Of(name)) < requirement.m_amount)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>How many of this piece he could place, counting the chests; int.MaxValue for a free piece.</summary>
        internal static int HowMany(Player player, Piece piece)
        {
            if (ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()) || player.NoCostCheat())
            {
                return int.MaxValue;
            }
            int most = int.MaxValue;
            Inventory inventory = player.GetInventory();
            foreach (Piece.Requirement requirement in piece.m_resources)
            {
                if (!requirement.m_resItem || requirement.m_amount <= 0)
                {
                    continue;
                }
                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                most = Mathf.Min(most, (inventory.CountItems(name) + Stock.Of(name)) / requirement.m_amount);
            }
            return most;
        }

        // What placing this piece (and pieces-1 more like it) takes beyond what he carries, by item name (ConsumeResources
        // takes m_amount of each, at any quality); null for nothing to place or a free piece.
        private static Dictionary<string, int> ShortOf(Player player, Piece piece, int pieces)
        {
            if (piece == null || piece.m_repairPiece || piece.m_removePiece || player.NoCostCheat() ||
                ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))
            {
                return null;
            }
            var wanted = new Dictionary<string, int>();
            Taking.Shortfall(player, piece.m_resources, 0, -1, pieces, wanted);
            return wanted;
        }

        // Asks for the chests that would give what's still wanted and aren't his yet; true if there were any to ask for.
        // Asking ahead, before any click, leaves alone a chest someone else is taking from; a click asks for it all the same.
        // Only a click takes through a friend who has a chest open (it moves the items into his inventory); asking ahead
        // leaves such a chest alone. noRoom: nothing at all was asked, because his inventory had no room for what the
        // friends' open chests would give (known at once: nothing was sent). also: more chests to ask for with them (his
        // personal chests not yet his, card A80), first.
        private static bool AskFor(Piece piece, Player player, Dictionary<string, int> left, bool ahead, ref bool noRoom, List<Container> also = null)
        {
            List<ShareTake> shares = ahead ? null : new List<ShareTake>();
            List<Container> notMine = Supply.ToAskFor(left, player.transform.position, ahead ? NotToAskAhead : (Func<Container, bool>)null, shares);
            if (also != null && also.Count > 0)
            {
                notMine = also.Concat(notMine.Where(chest => !also.Contains(chest))).ToList();
            }
            if (notMine.Count == 0 && (shares == null || shares.Count == 0))
            {
                return false;
            }
            if (!ChestClaims.Busy)
            {
                askedFor = piece;
                askedAt = Time.time;
                ChestClaims.Start(Module.Chests, notMine, shares, Settings.Rules(), result => Stock.Invalidate(), quiet: ahead);
                noRoom = notMine.Count == 0 && shares != null &&
                         shares.All(share => share.Result != null && share.Result.Status == ShareStatus.NotSent && share.Result.NoRoom);
            }
            return true;
        }
    }

    /// <summary>
    /// PlantEasily (advize.PlantEasily), which he runs: with the cultivator it plants a grid from one click, one plant per
    /// "healthy" ghost, and pays for each. How many extra plants it will place, read from its own grid by reflection (its
    /// GhostGrid.GhostPlacementStatus and MaxActiveGhosts, its settings' ModActive and PreventInvalidPlanting), counted as
    /// its PlacePiece postfix counts them, so a click is approved only when the chests he owns pay for them all. Nothing of
    /// PlantEasily is changed. If it can't be read (it changed), none are counted and the log says so once.
    /// </summary>
    internal static class PlantEasily
    {
        internal const string Guid = "advize.PlantEasily";

        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo RightItem = typeof(Humanoid).GetField("m_rightItem", AnyInstance);

        private static bool looked;
        private static FieldInfo statuses;
        private static PropertyInfo maxActive;
        private static FieldInfo config;
        private static PropertyInfo modActive;
        private static PropertyInfo preventInvalid;

        /// <summary>How many plants PlantEasily will place besides the one clicked: 0 unless it's installed, he holds the
        /// cultivator and the piece is a plant. With <paramref name="ifPaid"/>, also those it shows short of seeds, which
        /// it would place once the chests are his: for asking for the chests ahead.</summary>
        internal static int Extras(Player player, Piece piece, bool ifPaid = false)
        {
            if (piece == null || !Chainloader.PluginInfos.ContainsKey(Guid))
            {
                return 0;
            }
            // As PlantEasily tells: the item in his right hand is the cultivator (Humanoid.m_rightItem, protected).
            var tool = RightItem?.GetValue(player) as ItemDrop.ItemData;
            if (tool == null || tool.m_shared.m_name != "$item_cultivator" || (piece.GetComponent<Plant>() == null && piece.GetComponent<Pickable>() == null))
            {
                return 0;
            }
            Look();
            if (statuses == null || maxActive == null)
            {
                return 0;
            }
            object settings = config?.GetValue(null);
            if (settings != null && modActive != null && !(bool)modActive.GetValue(settings))
            {
                return 0;
            }
            // Unknown: count the invalid ones too, which can only mean more to pay, never fewer.
            bool skipsInvalid = settings != null && preventInvalid != null && (bool)preventInvalid.GetValue(settings);
            var list = statuses.GetValue(null) as IList;
            int most = (int)maxActive.GetValue(null);
            int extras = 0;
            for (int i = 0; i < most; i++)
            {
                // Healthy 0, LackResources 1, the rest can't grow there. An entry not there yet (its grid was just rebuilt,
                // by its resize keys) counts as healthy: rebuilt, it starts that way, and may be planted in this very click.
                int status = list != null && i + 1 < list.Count ? Convert.ToInt32(list[i + 1]) : 0;
                if (status == 0 || (ifPaid && status == 1) || (!skipsInvalid && status > 1))
                {
                    extras++;
                }
            }
            return extras;
        }

        private static void Look()
        {
            if (looked)
            {
                return;
            }
            looked = true;
            Assembly assembly = Chainloader.PluginInfos[Guid].Instance?.GetType().Assembly;
            Type grid = assembly?.GetType("Advize_PlantEasily.GhostGrid");
            statuses = grid?.GetField("GhostPlacementStatus", AnyStatic);
            maxActive = grid?.GetProperty("MaxActiveGhosts", AnyStatic);
            config = assembly?.GetType("Advize_PlantEasily.ModContext")?.GetField("config", AnyStatic);
            modActive = config?.FieldType.GetProperty("ModActive", AnyInstance);
            preventInvalid = config?.FieldType.GetProperty("PreventInvalidPlanting", AnyInstance);
            if (statuses == null || maxActive == null || !typeof(IList).IsAssignableFrom(statuses.FieldType) || maxActive.PropertyType != typeof(int))
            {
                statuses = null;
                Module.Log.LogWarning("PlantEasily has changed: its planting grid couldn't be read, so its extra plants aren't counted " +
                                      "before a click. They are still paid from the chests you own, as far as those go.");
            }
        }
    }

    // The game's building, patched. Each patch asks whether the mod is on first and changes nothing while it's off.

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Piece), typeof(Player.RequirementMode) })]
    internal static class CountPieceWithChests
    {
        private static void Postfix(Player __instance, Piece piece, Player.RequirementMode mode, ref bool __result)
        {
            // Under the no-cost cheat, whatever asks pays nothing: the chests stay out of it.
            if (__result || mode == Player.RequirementMode.IsKnown || !Building.On || __instance != Player.m_localPlayer || piece == null ||
                __instance.NoCostCheat())
            {
                return;
            }
            bool result = false;
            Module.Guard("counting a piece's materials in the chests", () =>
            {
                result = Building.HaveWithChests(__instance, piece, mode);
                if (result && mode == Player.RequirementMode.CanBuild && Building.Strict)
                {
                    Building.Approved();   // told "yes" thanks to the chests he owns: a payment in this frame comes from them
                }
            });
            __result = result;
        }
    }

    /// <summary>PlantEasily's ghost grid asks inside Player.UpdatePlacementGhost (from its postfix): strict there. Left
    /// only if entered (a finalizer runs even when an earlier prefix threw and this one never ran).</summary>
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    internal static class StrictWhileGhostsAreChecked
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(out bool __state)
        {
            Building.EnterStrict();
            __state = true;
        }

        private static void Finalizer(bool __state)
        {
            if (__state)
            {
                Building.LeaveStrict();
            }
        }
    }

    /// <summary>PlantEasily's replanting asks inside Pickable.SetPicked (from its prefix) and pays at once: strict there,
    /// on a fresh count of the chests he owns.</summary>
    [HarmonyPatch(typeof(Pickable), nameof(Pickable.SetPicked))]
    internal static class StrictWhileReplanting
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(out bool __state)
        {
            Building.EnterStrict();
            Stock.InvalidateOwned();
            __state = true;
        }

        private static void Finalizer(bool __state)
        {
            if (__state)
            {
                Building.LeaveStrict();
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    internal static class PlaceFromChests
    {
        private static bool Prefix(Player __instance, Piece piece, bool __runOriginal, ref bool __result)
        {
            // Another mod stopped it (and may place it itself): the approval stands as it was.
            if (!__runOriginal || !Building.On || __instance != Player.m_localPlayer || piece == null)
            {
                return true;
            }
            // The game's "have I enough" already counted the chests, so after an error here (the mod then switches off)
            // this one piece isn't placed: placed, it would cost only what he carries.
            bool place = false;
            Module.Guard("placing a piece from chests", () => place = Building.BeforePlacing(__instance, piece));
            if (!place)
            {
                __result = false;
            }
            return place;
        }

        private static void Postfix(bool __result)
        {
            if (!__result)
            {
                Building.NotPlaced();   // not placed: nothing is paid
            }
        }
    }
}
