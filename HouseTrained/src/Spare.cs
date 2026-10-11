using System.Collections.Generic;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// Tames spare buildings (decisions 2 to 4, 9 to 12): the work of the four prefixes on where a hit on a piece or a crop
    /// is sent and where it lands. Where it is sent, a spared hit is only ever stopped whole (at 0%); where it lands, it is
    /// stopped at 0% or scaled to the share, once, there only. The rules themselves are SpareRules'.
    /// </summary>
    internal static class Spare
    {
        private static readonly HashSet<int> GrownCrops = new HashSet<int>();
        private static ZNetScene cropsOf;

        /// <summary>Where a hit on a piece is sent (the attacker's owner): false stops it, so it isn't sent at all.</summary>
        internal static bool Send(WearNTear piece, HitData hit) => Send(hit, PieceOf(piece), piece);

        /// <summary>Where a hit on a crop is sent: false stops it.</summary>
        internal static bool Send(Destructible crop, HitData hit) => !IsCrop(crop) || Send(hit, SpareTarget.Crop, crop);

        /// <summary>Where a hit on a piece lands (the piece's owner): false drops it; a share scales it.</summary>
        internal static bool Land(WearNTear piece, ZNetView view, HitData hit) => Land(hit, PieceOf(piece), piece, view);

        /// <summary>Where a hit on a crop lands: false drops it; a share scales it.</summary>
        internal static bool Land(Destructible crop, ZNetView view, HitData hit) => !IsCrop(crop) || Land(hit, SpareTarget.Crop, crop, view);

        private static bool Send(HitData hit, SpareTarget target, Component where)
        {
            if (Settings.SpareShare.Value > 0 || !Spares(hit, target, out Character attacker))
            {
                return true;
            }
            Tell("stopped", attacker, where, 0);
            return false;
        }

        private static bool Land(HitData hit, SpareTarget target, Component where, ZNetView view)
        {
            // Ownership first, as the game itself tests it: a hit can reach a computer that no longer owns the piece, which
            // ignores it anyway (review N7).
            if (view == null || !view.IsValid() || !view.IsOwner() || !Spares(hit, target, out Character attacker))
            {
                return true;
            }
            int share = Settings.SpareShare.Value;
            Tell("dropped", attacker, where, share);
            if (share <= 0)
            {
                return false;
            }
            hit.m_damage.Modify(SpareRules.Factor(share));
            return true;
        }

        private static bool Spares(HitData hit, SpareTarget target, out Character attacker)
        {
            attacker = hit != null ? hit.GetAttacker() : null;
            if (attacker == null || target == SpareTarget.NotCovered)
            {
                return false;
            }
            bool tamed = attacker.IsTamed();
            TameInfo info = Tames.Of(attacker);
            Character.Faction faction = attacker.GetFaction();
            SpareAttacker who = SpareRules.AttackerOf(attacker.IsPlayer(), tamed, info.Spared,
                faction == Character.Faction.Players || faction == Character.Faction.PlayerSpawned);
            // The trample is an area object's hit from a creature with a MovementDamage: the Lox and the moose have no other
            // area objects or projectiles, and HitData.m_ranged is set only by those and travels with the hit (review F5).
            bool trample = hit.m_ranged && info.HasTrample;
            var choices = new SpareChoices
            {
                PlayerBuilt = Settings.SparePlayerBuilt.Value,
                Ships = Settings.SpareShips.Value,
                World = Settings.SpareWorld.Value,
                Crops = Settings.SpareCrops.Value,
                Summons = Settings.SpareSummons.Value,
                Trample = Settings.SpareTrample.Value,
                SharePercent = Settings.SpareShare.Value,
            };
            return SpareRules.Spares(who, trample, target, choices);
        }

        private static SpareTarget PieceOf(WearNTear wear)
        {
            bool shipOrCart = wear.GetComponent<Ship>() != null || wear.GetComponent<Vagon>() != null;
            Piece piece = wear.GetComponent<Piece>();
            return SpareRules.PieceOf(shipOrCart, piece != null ? piece.GetCreator() : 0L);
        }

        /// <summary>A crop: a growing plant (a sapling), or what a plant grows into (the grown prefabs of every Plant in the
        /// game, read once from ZNetScene: Pickable_Carrot and the rest, not wild berries).</summary>
        private static bool IsCrop(Destructible destructible)
        {
            if (destructible.GetComponent<Plant>() != null)
            {
                return true;
            }
            ZNetView view = GameHooks.DestructibleView != null ? GameHooks.DestructibleView(destructible) : null;
            ZDO record = view != null && view.IsValid() ? view.GetZDO() : null;
            if (record == null)
            {
                return false;
            }
            Learn();
            return GrownCrops.Contains(record.GetPrefab());
        }

        private static void Learn()
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null || scene == cropsOf)
            {
                return;
            }
            cropsOf = scene;
            GrownCrops.Clear();
            foreach (GameObject prefab in scene.m_prefabs)
            {
                Plant plant = prefab != null ? prefab.GetComponent<Plant>() : null;
                if (plant == null || plant.m_grownPrefabs == null)
                {
                    continue;
                }
                foreach (GameObject grown in plant.m_grownPrefabs)
                {
                    if (grown != null)
                    {
                        GrownCrops.Add(grown.name.GetStableHashCode());
                    }
                }
            }
        }

        private static void Tell(string what, Character attacker, Component where, int share)
        {
            if (!Settings.SpareLog.Value)
            {
                return;
            }
            string piece = where != null ? Utils.GetPrefabName(where.gameObject) : "?";
            string tame = Utils.GetPrefabName(attacker.gameObject);
            Module.Log.LogInfo(share > 0
                ? $"A hit of {tame} on {piece} lands at {share}% (tames spare buildings)."
                : $"A hit of {tame} on {piece} was {what} (tames spare buildings).");
        }
    }
}
