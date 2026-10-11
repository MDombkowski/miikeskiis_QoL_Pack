using System;
using System.Collections.Generic;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// Friendly fire (decisions 5 to 8, 14 to 21, 42, 44 to 46): which wild creatures' attacks, area objects and blasts
    /// also hit their friends, how a friendly hit is judged where it is sent (FriendlyRules), and fighting back (the
    /// grudges). Everything it changes on a live object is remembered and put back the moment the part or the module is
    /// switched off.
    /// </summary>
    internal static class Friendly
    {
        internal static readonly Grudges Grudges = new Grudges();

        /// <summary>While true, the enemy test answers as the game does (the grudges set aside): fighting back asks the
        /// game's own relation through it (review N2).</summary>
        internal static bool AsTheGame;

        /// <summary>A flagged attack is remembered until it is done (Attack.IsDone), or at the most this long, for one that
        /// never started (code review 1, finding 8; code review 2, nit 12).</summary>
        internal const float AttackLasts = 60f;

        private static readonly List<AreaChange> AreaObjects = new List<AreaChange>();
        private static readonly List<ShotChange> Shots = new List<ShotChange>();
        private static readonly List<AttackChange> Attacks = new List<AttackChange>();
        private static readonly Predicate<AttackChange> AttackOver = change => change.Attack.IsDone() || tidyNow - change.At > AttackLasts;
        private static readonly Predicate<AttackChange> AreaAttack = change => change.Area;
        private static readonly Predicate<AttackChange> Swing = change => !change.Area;
        private static float tidyNow;

        // The flag switches as last seen while the part was on, so one switched off alone puts back what it flagged.
        private static bool hadAreaAttacks;
        private static bool hadMelee;
        private static bool hadAreaObjects;
        private static bool hadShots;

        /// <summary>Whether this creature deals friendly fire now: a wild creature (never a player, a tame or a staff's
        /// summon: decision 42), a boss only by its setting, not on the list, and with "only when every player nearby runs
        /// the pack", only then.</summary>
        internal static bool Deals(Character attacker)
        {
            if (attacker == null || !Wild(attacker))
            {
                return false;
            }
            bool onlyAll = Settings.FfOnlyAll.Value;
            return FriendlyRules.Deals(true, attacker.IsBoss(), Settings.FfBosses.Value, Tames.Of(attacker).FriendlyNever, onlyAll,
                                       onlyAll && Hello.EveryoneNearbyDeals(attacker.transform.position, Settings.FfNearby.Value, Time.time));
        }

        /// <summary>A wild creature: not a player, not tamed, not of the players' factions.</summary>
        internal static bool Wild(Character creature)
        {
            if (creature.IsPlayer() || creature.IsTamed())
            {
                return false;
            }
            Character.Faction faction = creature.GetFaction();
            return faction != Character.Faction.Players && faction != Character.Faction.PlayerSpawned;
        }

        /// <summary>An attack starting (its own copy): its area attack, or a wide swing, hits friends too, and is remembered
        /// so that switching off lowers its flag at once, mid-swing (code review 1, finding 8).</summary>
        internal static void AtAttackStart(Attack attack, Humanoid attacker, float now)
        {
            // The attack's kind first (no lookups for a bite), then the attacker.
            if (FriendlyRules.FlagsAttack(attack.m_attackType, attack.m_multiHit, attack.m_hitFriendly, Settings.FfAreaAttacks.Value, Settings.FfMelee.Value) &&
                Deals(attacker))
            {
                Flag(attack, now, attack.m_attackType == Attack.AttackType.Area);
            }
        }

        /// <summary>An attack's flag raised, and the attack remembered until it is done; <paramref name="area"/>: flagged as
        /// an area attack, else as a wide swing.</summary>
        internal static void Flag(Attack attack, float now, bool area)
        {
            attack.m_hitFriendly = true;
            Attacks.Add(new AttackChange(attack, now, area));
        }

        /// <summary>Every frame while the part is on, with its flag switches: one just switched off puts back at once what it
        /// flagged (MODULES.md rule 4, every setting at once; code review 2, nit 8). The judge reads its own settings at
        /// each hit, so only the flags need this.</summary>
        internal static void Follow(bool areaAttacks, bool melee, bool areaObjects, bool shots)
        {
            if (hadAreaAttacks && !areaAttacks)
            {
                LowerAttacks(AreaAttack);
            }
            if (hadMelee && !melee)
            {
                LowerAttacks(Swing);
            }
            if (hadAreaObjects && !areaObjects)
            {
                PutBackAreas();
            }
            if (hadShots && !shots)
            {
                PutBackShots();
            }
            hadAreaAttacks = areaAttacks;
            hadMelee = melee;
            hadAreaObjects = areaObjects;
            hadShots = shots;
        }

        /// <summary>An area object set up by its spawner: one that damages (never a heal or buff cloud, never a trample) hits
        /// friends too, and its own kind with "also hurts its own kind".</summary>
        internal static void AfterAreaSetup(Aoe area, Character owner)
        {
            if (owner == null || !FriendlyRules.FlagsArea(area.m_hitEnemy, IsTrample(area, owner), Settings.FfAreaObjects.Value) || !Deals(owner))
            {
                return;
            }
            bool same = Settings.FfOwnKind.Value;
            if (area.m_hitFriendly && (area.m_hitSame || !same))
            {
                return;
            }
            AreaObjects.Add(new AreaChange(area, area.m_hitFriendly, area.m_hitSame));
            area.m_hitFriendly = true;
            if (same)
            {
                area.m_hitSame = true;
            }
        }

        /// <summary>A projectile set up by its shooter: with "projectiles stop on friends", it hits friends all its flight.</summary>
        internal static void AfterProjectileSetup(Projectile shot, Character owner)
        {
            if (!Settings.FfProjectiles.Value || shot.m_hitFriendly || !Deals(owner))
            {
                return;
            }
            Shots.Add(new ShotChange(shot));
            shot.m_hitFriendly = true;
        }

        /// <summary>Before a projectile's blast: its flag set to exactly what "exploding projectiles' blasts" says, and,
        /// when friends are to be hurt, its no-damage-to-friends lifted for the blast (review F16). The patch has kept what
        /// to put back after it.</summary>
        internal static void BeforeBlast(Projectile shot)
        {
            bool blasts = Settings.FfBlasts.Value;
            Character owner = blasts && GameHooks.ProjectileOwner != null ? GameHooks.ProjectileOwner(shot) : null;
            bool friendly = shot.m_hitFriendly;
            bool noDamage = shot.m_noDamageFriendly;
            FriendlyRules.ForBlast(blasts, owner != null && Deals(owner), !blasts && friendly && IsOurs(shot), ref friendly, ref noDamage);
            shot.m_hitFriendly = friendly;
            shot.m_noDamageFriendly = noDamage;
        }

        internal static void AfterBlast(Projectile shot, BlastState state)
        {
            if (shot != null)
            {
                shot.m_hitFriendly = state.HitFriendly;
                shot.m_noDamageFriendly = state.NoDamageFriendly;
            }
        }

        /// <summary>Where a hit on a character is sent: false drops a friendly hit; a share scales it (decision 19).</summary>
        internal static bool Judge(Character target, HitData hit)
        {
            Character attacker = hit != null ? hit.GetAttacker() : null;
            if (attacker == null || target == null)
            {
                return true;
            }
            bool wild = Wild(attacker);
            if (!wild)
            {
                return true;
            }
            var facts = new FriendlyFacts
            {
                WildAttacker = true,
                AttackerIsBoss = attacker.IsBoss(),
                AttackerInNever = Tames.Of(attacker).FriendlyNever,
                AttackerMayDeal = !Settings.FfOnlyAll.Value || Hello.EveryoneNearbyDeals(attacker.transform.position, Settings.FfNearby.Value, Time.time),
                TargetIsSelf = target == attacker,
                TargetIsPlayer = target.IsPlayer(),
                TargetIsTamed = target.IsTamed(),
                TargetIsEnemy = BaseAI.IsEnemy(attacker, target),
                SameKind = target.m_name == attacker.m_name,
            };
            var choices = new FriendlyChoices
            {
                OwnKind = Settings.FfOwnKind.Value,
                Bosses = Settings.FfBosses.Value,
                HurtTames = Settings.FfHurtTames.Value,
                SharePercent = Settings.FfShare.Value,
            };
            FriendlyOutcome outcome = FriendlyRules.Judge(facts, choices, out float factor);
            if (outcome == FriendlyOutcome.AsTheGame)
            {
                return true;
            }
            if (Settings.FfLog.Value)
            {
                Module.Log.LogInfo(outcome == FriendlyOutcome.Drop
                    ? $"A friendly hit of {Utils.GetPrefabName(attacker.gameObject)} on {Utils.GetPrefabName(target.gameObject)} was dropped (friendly fire)."
                    : $"A friendly hit of {Utils.GetPrefabName(attacker.gameObject)} on {Utils.GetPrefabName(target.gameObject)}: {hit.GetTotalDamage() * factor:0.#} damage (friendly fire).");
            }
            if (outcome == FriendlyOutcome.Drop)
            {
                return false;
            }
            if (factor != 1f)
            {
                hit.m_damage.Modify(factor);
            }
            return true;
        }

        /// <summary>A creature hurt (on its owner's game): by a wild creature that isn't its enemy by the game's own
        /// relation, it holds a grudge against it. Tames never take part.</summary>
        internal static void AfterHurt(Character hurt, Character attacker)
        {
            if (hurt == null || attacker == null || hurt == attacker || !Wild(hurt) || !Wild(attacker))
            {
                return;
            }
            bool enemies;
            AsTheGame = true;
            try
            {
                enemies = BaseAI.IsEnemy(hurt, attacker);
            }
            finally
            {
                AsTheGame = false;
            }
            if (!enemies)
            {
                Grudges.Hold(hurt, attacker, Time.time + Settings.FfGrudge.Value);
            }
        }

        /// <summary>The enemy test, after the game's: a live grudge makes the two enemies, either way round.</summary>
        internal static bool Grudge(Character a, Character b)
        {
            if (AsTheGame || Grudges.Count == 0 || a == null || b == null || a.IsTamed() || b.IsTamed())
            {
                return false;
            }
            return Grudges.Between(a, b, Time.time);
        }

        /// <summary>Once a second: lasted-out grudges dropped, area objects and projectiles gone from the world forgotten,
        /// and attacks that are done (review N2, N4; code review 1, finding 8; code review 2, nit 12).</summary>
        internal static void Tidy(float now)
        {
            Grudges.Prune(now);
            AreaObjects.RemoveAll(change => change.Area == null);
            Shots.RemoveAll(change => change.Shot == null);
            tidyNow = now;
            Attacks.RemoveAll(AttackOver);
        }

        /// <summary>The part or the module switched off: every flag put back as it was, every grudge forgotten (review
        /// F13; code review 1, finding 8).</summary>
        internal static void PutBack()
        {
            LowerAttacks(null);
            PutBackAreas();
            PutBackShots();
            Grudges.Clear();
            hadAreaAttacks = hadMelee = hadAreaObjects = hadShots = false;
        }

        // The flagged attacks lowered and forgotten: those the test picks, or with none, all of them.
        private static void LowerAttacks(Predicate<AttackChange> which)
        {
            foreach (AttackChange change in Attacks)
            {
                if (which == null || which(change))
                {
                    change.Attack.m_hitFriendly = false;
                }
            }
            if (which == null)
            {
                Attacks.Clear();
            }
            else
            {
                Attacks.RemoveAll(which);
            }
        }

        private static void PutBackAreas()
        {
            foreach (AreaChange change in AreaObjects)
            {
                if (change.Area != null)
                {
                    change.Area.m_hitFriendly = change.HitFriendly;
                    change.Area.m_hitSame = change.HitSame;
                }
            }
            AreaObjects.Clear();
        }

        private static void PutBackShots()
        {
            foreach (ShotChange change in Shots)
            {
                if (change.Shot != null)
                {
                    change.Shot.m_hitFriendly = false;
                }
            }
            Shots.Clear();
        }

        /// <summary>Only the grudges forgotten: fighting back switched off.</summary>
        internal static void ForgetGrudges() => Grudges.Clear();

        private static bool IsOurs(Projectile shot)
        {
            foreach (ShotChange change in Shots)
            {
                if (change.Shot == shot)
                {
                    return true;
                }
            }
            return false;
        }

        // A Lox's or a moose's trample: the area object its MovementDamage runs.
        private static bool IsTrample(Aoe area, Character owner)
        {
            MovementDamage trample = owner.GetComponent<MovementDamage>();
            return trample != null && trample.m_runDamageObject == area.gameObject;
        }

        private readonly struct AreaChange
        {
            internal readonly Aoe Area;
            internal readonly bool HitFriendly;
            internal readonly bool HitSame;

            internal AreaChange(Aoe area, bool hitFriendly, bool hitSame)
            {
                Area = area;
                HitFriendly = hitFriendly;
                HitSame = hitSame;
            }
        }

        private readonly struct ShotChange
        {
            internal readonly Projectile Shot;

            internal ShotChange(Projectile shot)
            {
                Shot = shot;
            }
        }

        private readonly struct AttackChange
        {
            internal readonly Attack Attack;
            internal readonly float At;
            internal readonly bool Area;

            internal AttackChange(Attack attack, float at, bool area)
            {
                Attack = attack;
                At = at;
                Area = area;
            }
        }
    }

    /// <summary>A projectile's two flags as they were before its blast.</summary>
    internal readonly struct BlastState
    {
        internal readonly bool HitFriendly;
        internal readonly bool NoDamageFriendly;

        internal BlastState(bool hitFriendly, bool noDamageFriendly)
        {
            HitFriendly = hitFriendly;
            NoDamageFriendly = noDamageFriendly;
        }
    }
}
