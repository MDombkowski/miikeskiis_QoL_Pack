using System.Collections.Generic;
using UnityEngine;

namespace BarNone
{
    /// <summary>Who fired a projectile, as the projectile itself knows it.</summary>
    internal enum Shooter
    {
        /// <summary>A player: his own shots on his game, a friend's on theirs.</summary>
        Player,

        /// <summary>A ballista or a catapult: no shooter of its own, its shots marked Turret or Catapult by the game.</summary>
        Ballista,

        /// <summary>A tamed creature.</summary>
        Tame,

        /// <summary>An untamed creature, or the world's own ownerless shots (the Ashlands radiators' sparks, a tree's
        /// spawn).</summary>
        Enemy,
    }

    /// <summary>
    /// Projectiles: a hit on a listed piece is skipped, so the game's own loop tries the next hit behind it.
    /// A projectile is flown only by the game that owns it (its shooter's, or the creature's owner's for a creature's; a
    /// ballista's by the ballista's owner's). Each physics step that game collects every hit along the step, sorts them by
    /// distance and calls Projectile.OnHit on each until one hits or bounces; a prefix that skips a hit leaves the
    /// projectile flying.
    /// </summary>
    internal static class ProjectilePart
    {
        /// <summary>The part runs: the module on, "Projectiles: pass through pieces" on, Projectile_Collision not installed,
        /// and its hooks found.</summary>
        internal static bool On =>
            Module.IsOn && Settings.ProjectilesThrough.Value && !Rivals.ProjectilesStandAside && !Broken.Is(Feature.Projectiles) && GameHooks.ProjectilesReady;

        /// <summary>Whether the prefix has anything to do: the part runs, or the hits are to be written to the log (which
        /// changes nothing, so it works also while the part is off).</summary>
        internal static bool Watching =>
            On || (Module.IsOn && Settings.LogHits.Value && !Rivals.ProjectilesStandAside && !Broken.Is(Feature.Projectiles) && GameHooks.ProjectilesReady);

        /// <summary>Before Projectile.OnHit: false skips this hit (the projectile flies on), true lets the game's hit run.</summary>
        internal static bool BeforeOnHit(Projectile projectile, Collider collider)
        {
            if (collider == null)
            {
                return true;   // water, or a shield's own call
            }
            bool on = On;
            bool log = Settings.LogHits.Value;
            bool listed = PieceLists.Projectiles.Has(collider);
            if (!(on && listed) && !log)
            {
                return true;
            }
            Character owner = GameHooks.OwnerOf(projectile);
            bool hasOwner = owner != null;
            HitData.HitType hitType = projectile.m_hitType;
            bool catapultShot = !hasOwner && hitType != HitData.HitType.Turret && hitType != HitData.HitType.Catapult && IsCatapultShot(projectile);
            Shooter shooter = ShotRules.ShooterOf(hasOwner, hasOwner && owner.IsPlayer(), hasOwner && owner.IsTamed(), hitType, catapultShot);
            bool passes = on && listed &&
                          ShotRules.Passes(shooter, projectile.m_rayRadius, Settings.PlayersShots.Value, Settings.BallistasShots.Value,
                                           Settings.TamesShots.Value, Settings.EnemiesShots.Value, Settings.LargestProjectile.Value);
            if (log)
            {
                string verdict = passes ? "passes through" : listed ? (on ? "hits (not this shooter or size)" : "hits (the part is off)") : "hits";
                Module.Log.LogInfo($"Projectile {Utils.GetPrefabName(projectile.gameObject)} (size {projectile.m_rayRadius:0.###} m, " +
                                   $"{ShotRules.Describe(shooter)}) hit {NameOf(collider)}{(listed ? ", a listed piece" : string.Empty)}: {verdict}.");
            }
            return !passes;
        }

        private static HashSet<string> catapultShots;
        private static ZNetScene catapultShotsOf;

        /// <summary>A catapult's own shot: the game marks a catapult's shot "Catapult" only when the item loaded brings a
        /// projectile of its own; its usual boulder has no shooter and no mark, so it's known by its prefab, any catapult's
        /// own projectile (read once per world from the game's prefabs).</summary>
        private static bool IsCatapultShot(Projectile projectile)
        {
            ZNetScene scene = ZNetScene.instance;
            if (scene == null)
            {
                return false;
            }
            if (catapultShots == null || !ReferenceEquals(catapultShotsOf, scene))
            {
                catapultShots = new HashSet<string>(System.StringComparer.Ordinal);
                catapultShotsOf = scene;
                foreach (GameObject prefab in scene.m_prefabs)
                {
                    Catapult catapult = prefab != null ? prefab.GetComponent<Catapult>() : null;
                    if (catapult != null && catapult.m_projectile != null)
                    {
                        catapultShots.Add(Utils.GetPrefabName(catapult.m_projectile.gameObject));
                    }
                }
            }
            return catapultShots.Contains(Utils.GetPrefabName(projectile.gameObject));
        }

        // What a hit collider is, for the log: its piece's prefab name, else its root object's name.
        private static string NameOf(Collider collider)
        {
            string piece = PieceNames.Of(collider);
            return piece != null ? $"the piece {piece}" : Utils.GetPrefabName(collider.transform.root.gameObject);
        }
    }

    /// <summary>The projectile part's rules, apart from Unity so the harness can check them.</summary>
    internal static class ShotRules
    {
        /// <summary>Who fired it: a player, a tame and an untamed creature by the character the projectile keeps; without
        /// one, a ballista or catapult by the hit type the game gave its shot (or a catapult's own boulder, which it gives
        /// none), else the world's own (counted as an enemy's).</summary>
        internal static Shooter ShooterOf(bool hasOwner, bool ownerIsPlayer, bool ownerIsTamed, HitData.HitType hitType, bool catapultShot)
        {
            if (hasOwner)
            {
                return ownerIsPlayer ? Shooter.Player : ownerIsTamed ? Shooter.Tame : Shooter.Enemy;
            }
            return hitType == HitData.HitType.Turret || hitType == HitData.HitType.Catapult || catapultShot ? Shooter.Ballista : Shooter.Enemy;
        }

        /// <summary>The shooter in the log's words.</summary>
        internal static string Describe(Shooter shooter) => shooter switch
        {
            Shooter.Player => "a player's",
            Shooter.Ballista => "a ballista's or catapult's",
            Shooter.Tame => "a tame's",
            _ => "an enemy's or the world's",
        };

        /// <summary>Whether a projectile from this shooter, of this radius, passes a listed piece.</summary>
        internal static bool Passes(Shooter shooter, float radius, bool players, bool ballistas, bool tames, bool enemies, float largest)
        {
            bool allowed = shooter switch
            {
                Shooter.Player => players,
                Shooter.Ballista => ballistas,
                Shooter.Tame => tames,
                _ => enemies,
            };
            return allowed && (largest <= 0f || radius <= largest);
        }
    }
}
