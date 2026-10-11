using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// The Harmony patches: prefixes, postfixes and finalizers only, so a part switched off changes nothing (his thought of
// 2026-09-29; no transpilers, which a switched-off module would leave behind). Each target was looked up in GameHooks; a
// patch whose target or members weren't found is skipped (Prepare), and only its own feature is lost.
// Every class has a Cleanup (Broken.Patching): a patch that can't be applied (another mod's broken patch on the same
// method) loses only its own feature (decision 41).
// The game's own parameters are taken by position (__0, __1; ref __n for an out), never by name, and private fields are
// reached through GameHooks' references, so a renamed parameter or field can't stop a patch being applied (review N10).
// Every body asks its part's switch first (Parts, worked out once a frame); the work runs inside Module.Guard, whose first
// error switches the module off for the session. Each hands its work to a delegate made once, fed through fields, so the
// patches themselves allocate nothing per call; of the work, only the rare parts do (a path query's corners, a log line
// once one is due: code review 1, nit 13).
// A postfix that writes the game's state steps back when another mod's prefix skipped the game's own method
// (__runOriginal false): that mod has decided (review F12).
namespace HouseTrained
{
    // ------------------------------------------------------------------------------------------- tames spare buildings

    /// <summary>Where a hit on a piece is sent (the attacker's owner): a spared hit isn't sent.</summary>
    [HarmonyPatch]
    internal static class SpareSendPatch
    {
        private static readonly Action work = Work;
        private static WearNTear piece;
        private static HitData hit;
        private static bool send;

        private static bool Prepare() => GameHooks.WearNTearDamage != null && !Broken.Is(Feature.SpareSend);

        private static MethodBase TargetMethod() => GameHooks.WearNTearDamage;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SpareSend, original, ex, harmony);

        // __0: HitData hit.
        private static bool Prefix(WearNTear __instance, HitData __0)
        {
            if (!Parts.SpareOn || __0 == null)
            {
                return true;
            }
            piece = __instance;
            hit = __0;
            send = true;
            Module.Guard("sparing a building", work);
            piece = null;
            hit = null;
            return send;
        }

        private static void Work() => send = Spare.Send(piece, hit);
    }

    /// <summary>Where a hit on a piece lands (the piece's owner): a spared hit is dropped, or scaled to the share.</summary>
    [HarmonyPatch]
    internal static class SpareLandPatch
    {
        private static readonly Action work = Work;
        private static WearNTear piece;
        private static HitData hit;
        private static bool land;

        private static bool Prepare() => GameHooks.WearNTearRpcDamage != null && GameHooks.PieceView != null && !Broken.Is(Feature.SpareLand);

        private static MethodBase TargetMethod() => GameHooks.WearNTearRpcDamage;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.SpareLand, original, ex, harmony);

        // __1: HitData hit.
        private static bool Prefix(WearNTear __instance, HitData __1)
        {
            if (!Parts.SpareOn || __1 == null)
            {
                return true;
            }
            piece = __instance;
            hit = __1;
            land = true;
            Module.Guard("sparing a building", work);
            piece = null;
            hit = null;
            return land;
        }

        private static void Work() => land = Spare.Land(piece, GameHooks.PieceView(piece), hit);
    }

    /// <summary>Where a hit on a crop is sent: a spared hit isn't sent.</summary>
    [HarmonyPatch]
    internal static class CropsSendPatch
    {
        private static readonly Action work = Work;
        private static Destructible crop;
        private static HitData hit;
        private static bool send;

        private static bool Prepare() => GameHooks.DestructibleDamage != null && !Broken.Is(Feature.CropsSend);

        private static MethodBase TargetMethod() => GameHooks.DestructibleDamage;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.CropsSend, original, ex, harmony);

        private static bool Prefix(Destructible __instance, HitData __0)
        {
            if (!Parts.CropsOn || __0 == null)
            {
                return true;
            }
            crop = __instance;
            hit = __0;
            send = true;
            Module.Guard("sparing a crop", work);
            crop = null;
            hit = null;
            return send;
        }

        private static void Work() => send = Spare.Send(crop, hit);
    }

    /// <summary>Where a hit on a crop lands: a spared hit is dropped, or scaled to the share.</summary>
    [HarmonyPatch]
    internal static class CropsLandPatch
    {
        private static readonly Action work = Work;
        private static Destructible crop;
        private static HitData hit;
        private static bool land;

        private static bool Prepare() => GameHooks.DestructibleRpcDamage != null && GameHooks.DestructibleView != null && !Broken.Is(Feature.CropsLand);

        private static MethodBase TargetMethod() => GameHooks.DestructibleRpcDamage;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.CropsLand, original, ex, harmony);

        private static bool Prefix(Destructible __instance, HitData __1)
        {
            if (!Parts.CropsOn || __1 == null)
            {
                return true;
            }
            crop = __instance;
            hit = __1;
            land = true;
            Module.Guard("sparing a crop", work);
            crop = null;
            hit = null;
            return land;
        }

        private static void Work() => land = Spare.Land(crop, GameHooks.DestructibleView(crop), hit);
    }

    // --------------------------------------------------------------------------------------------------- friendly fire

    /// <summary>Where a hit on a character is sent: a friendly hit is judged (decision 19).</summary>
    [HarmonyPatch]
    internal static class JudgePatch
    {
        private static readonly Action work = Work;
        private static Character target;
        private static HitData hit;
        private static bool send;

        private static bool Prepare() => GameHooks.CharacterDamage != null && !Broken.Is(Feature.FriendlyJudge);

        private static MethodBase TargetMethod() => GameHooks.CharacterDamage;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.FriendlyJudge, original, ex, harmony);

        private static bool Prefix(Character __instance, HitData __0)
        {
            if (!Parts.FriendlyOn || __0 == null)
            {
                return true;
            }
            target = __instance;
            hit = __0;
            send = true;
            Module.Guard("judging a friendly hit", work);
            target = null;
            hit = null;
            return send;
        }

        private static void Work() => send = Friendly.Judge(target, hit);
    }

    /// <summary>An attack starting, on its own copy: an area attack or a wide swing hits friends too.</summary>
    [HarmonyPatch]
    internal static class AttackStartPatch
    {
        private static readonly Action work = Work;
        private static Attack attack;
        private static Humanoid attacker;

        private static bool Prepare() => GameHooks.AttackStart != null && !Broken.Is(Feature.FriendlyAttacks);

        private static MethodBase TargetMethod() => GameHooks.AttackStart;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.FriendlyAttacks, original, ex, harmony);

        // __0: Humanoid character (m_character isn't set yet in a prefix).
        private static void Prefix(Attack __instance, Humanoid __0)
        {
            if (!Parts.FriendlyOn || __0 == null)
            {
                return;
            }
            attack = __instance;
            attacker = __0;
            Module.Guard("letting an attack hit friends", work);
            attack = null;
            attacker = null;
        }

        private static void Work() => Friendly.AtAttackStart(attack, attacker, Time.time);
    }

    /// <summary>An area object set up by its spawner: one that damages hits friends too.</summary>
    [HarmonyPatch]
    internal static class AreaSetupPatch
    {
        private static readonly Action work = Work;
        private static Aoe area;
        private static Character owner;

        private static bool Prepare() => GameHooks.AoeSetup != null && !Broken.Is(Feature.FriendlyAreaObjects);

        private static MethodBase TargetMethod() => GameHooks.AoeSetup;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.FriendlyAreaObjects, original, ex, harmony);

        // __0: Character owner.
        private static void Postfix(Aoe __instance, Character __0, bool __runOriginal)
        {
            if (!__runOriginal || !Parts.FriendlyOn || __0 == null)
            {
                return;
            }
            area = __instance;
            owner = __0;
            Module.Guard("letting an area object hit friends", work);
            area = null;
            owner = null;
        }

        private static void Work() => Friendly.AfterAreaSetup(area, owner);
    }

    /// <summary>A projectile's blast: its flags set for the blast alone, and put back after it even if the game's method
    /// throws (review F2, F16).</summary>
    [HarmonyPatch]
    internal static class BlastPatch
    {
        private static readonly Action work = Work;
        private static Projectile shot;

        private static bool Prepare() => GameHooks.ProjectileDoAoe != null && GameHooks.ProjectileOwner != null && !Broken.Is(Feature.FriendlyBlasts);

        private static MethodBase TargetMethod() => GameHooks.ProjectileDoAoe;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.FriendlyBlasts, original, ex, harmony);

        private static void Prefix(Projectile __instance, out BlastState __state)
        {
            __state = new BlastState(__instance.m_hitFriendly, __instance.m_noDamageFriendly);
            if (!Parts.FriendlyOn)
            {
                return;
            }
            shot = __instance;
            Module.Guard("letting a blast hit friends", work);
            shot = null;
        }

        private static void Work() => Friendly.BeforeBlast(shot);

        private static Exception Finalizer(Projectile __instance, BlastState __state, Exception __exception)
        {
            Friendly.AfterBlast(__instance, __state);
            return __exception;
        }
    }

    /// <summary>A projectile set up by its shooter: with "projectiles stop on friends", it hits friends all its flight.</summary>
    [HarmonyPatch]
    internal static class ShotSetupPatch
    {
        private static readonly Action work = Work;
        private static Projectile shot;
        private static Character owner;

        private static bool Prepare() => GameHooks.ProjectileSetup != null && !Broken.Is(Feature.FriendlyProjectiles);

        private static MethodBase TargetMethod() => GameHooks.ProjectileSetup;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.FriendlyProjectiles, original, ex, harmony);

        private static void Postfix(Projectile __instance, Character __0, bool __runOriginal)
        {
            if (!__runOriginal || !Parts.FriendlyOn || __0 == null)
            {
                return;
            }
            shot = __instance;
            owner = __0;
            Module.Guard("letting a projectile stop on friends", work);
            shot = null;
            owner = null;
        }

        private static void Work() => Friendly.AfterProjectileSetup(shot, owner);
    }

    /// <summary>A creature hurt (on its owner's game): by a friend, it holds a grudge.</summary>
    [HarmonyPatch]
    internal static class HurtPatch
    {
        private static readonly Action work = Work;
        private static MonsterAI ai;
        private static Character attacker;

        private static bool Prepare() => GameHooks.MonsterOnDamaged != null && GameHooks.AiCharacter != null && !Broken.Is(Feature.FightBackHurt);

        private static MethodBase TargetMethod() => GameHooks.MonsterOnDamaged;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.FightBackHurt, original, ex, harmony);

        // __1: Character attacker.
        private static void Postfix(MonsterAI __instance, Character __1, bool __runOriginal)
        {
            if (!__runOriginal || !Parts.FightBackOn || __1 == null)
            {
                return;
            }
            ai = __instance;
            attacker = __1;
            Module.Guard("noting a grudge", work);
            ai = null;
            attacker = null;
        }

        private static void Work() => Friendly.AfterHurt(GameHooks.AiCharacter(ai), attacker);
    }

    /// <summary>Who is whose enemy, after the game: a live grudge makes the two enemies. Asked thousands of times a
    /// second, so with no grudge it costs one count test.</summary>
    [HarmonyPatch]
    internal static class EnemyPatch
    {
        private static readonly Action work = Work;
        private static Character a;
        private static Character b;
        private static bool enemies;

        private static bool Prepare() => GameHooks.IsEnemy != null && !Broken.Is(Feature.FightBackEnemy);

        private static MethodBase TargetMethod() => GameHooks.IsEnemy;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.FightBackEnemy, original, ex, harmony);

        private static void Postfix(Character __0, Character __1, ref bool __result, bool __runOriginal)
        {
            if (__result || Friendly.Grudges.Count == 0 || !Parts.FightBackOn || !__runOriginal)
            {
                return;
            }
            a = __0;
            b = __1;
            enemies = false;
            Module.Guard("telling a grudge", work);
            a = null;
            b = null;
            __result = enemies;
        }

        private static void Work() => enemies = Friendly.Grudge(a, b);
    }

    // ---------------------------------------------------------------------------------------- tames in pens: point 1

    /// <summary>A creature's ground contact, every physics step on its owner: a chosen tame doesn't count another creature,
    /// nor a wall piece's edge above its step, as ground.</summary>
    [HarmonyPatch]
    internal static class GroundPatch
    {
        private static readonly Action work = Work;
        private static Character tame;

        private static bool Prepare() => GameHooks.UpdateGroundContact != null && GameHooks.GroundContact != null && GameHooks.GroundCollider != null &&
                                         GameHooks.GroundPoint != null && GameHooks.Body != null && GameHooks.ResetGroundContact != null && !Broken.Is(Feature.PensGround);

        private static MethodBase TargetMethod() => GameHooks.UpdateGroundContact;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensGround, original, ex, harmony);

        private static void Prefix(Character __instance)
        {
            if (!Parts.GroundOn || !GameHooks.GroundContact(__instance) || !__instance.IsTamed())
            {
                return;
            }
            tame = __instance;
            Module.Guard("keeping a tame off creatures and fences", work);
            tame = null;
        }

        private static void Work() => Pens.BeforeGroundContact(tame);
    }

    /// <summary>A birth: its parent noted, and the note cleared after it, even if the game's method throws.</summary>
    [HarmonyPatch]
    internal static class BirthPatch
    {
        private static readonly Action work = Work;
        private static Procreation procreation;

        private static bool Prepare() => GameHooks.Procreate != null && GameHooks.SetLevel != null && !Broken.Is(Feature.PensBirths);

        private static MethodBase TargetMethod() => GameHooks.Procreate;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensBirths, original, ex, harmony);

        private static void Prefix(Procreation __instance)
        {
            Pens.AfterBirth();
            if (!Parts.BirthsOn)
            {
                return;
            }
            procreation = __instance;
            Module.Guard("noting a birth", work);
            procreation = null;
        }

        private static void Work() => Pens.BeforeBirth(procreation);

        private static Exception Finalizer(Exception __exception)
        {
            Pens.AfterBirth();
            return __exception;
        }
    }

    /// <summary>An item given its quality: a birth's egg is put on its parent's side of a wall.</summary>
    [HarmonyPatch]
    internal static class EggPatch
    {
        private static readonly Action work = Work;
        private static ItemDrop egg;

        private static bool Prepare() => GameHooks.SetQuality != null && GameHooks.Procreate != null && !Broken.Is(Feature.PensEggs);

        private static MethodBase TargetMethod() => GameHooks.SetQuality;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensEggs, original, ex, harmony);

        private static void Postfix(ItemDrop __instance, bool __runOriginal)
        {
            if (!__runOriginal || !Parts.BirthsOn)
            {
                return;
            }
            egg = __instance;
            Module.Guard("putting an egg on its side of the wall", work);
            egg = null;
        }

        private static void Work() => Pens.AfterSetQuality(egg);
    }

    /// <summary>A creature given its level: a newborn or a grown-up of the noted parent or young is put right.</summary>
    [HarmonyPatch]
    internal static class SetLevelPatch
    {
        private static readonly Action work = Work;
        private static Character created;

        private static bool Prepare() => GameHooks.SetLevel != null && !Broken.Is(Feature.PensSetLevel);

        private static MethodBase TargetMethod() => GameHooks.SetLevel;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensSetLevel, original, ex, harmony);

        private static void Postfix(Character __instance, bool __runOriginal)
        {
            if (!__runOriginal || (!Parts.BirthsOn && !Parts.GrowUpsOn))
            {
                return;
            }
            created = __instance;
            Module.Guard("putting a newborn or a grown-up inside", work);
            created = null;
        }

        private static void Work() => Pens.AfterSetLevel(created);
    }

    /// <summary>A young growing up: its spot noted, and the note cleared after it, even if the game's method throws.</summary>
    [HarmonyPatch]
    internal static class GrowUpPatch
    {
        private static readonly Action work = Work;
        private static Growup growing;

        private static bool Prepare() => GameHooks.GrowupGrowUpdate != null && GameHooks.SetLevel != null && !Broken.Is(Feature.PensGrowUps);

        private static MethodBase TargetMethod() => GameHooks.GrowupGrowUpdate;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensGrowUps, original, ex, harmony);

        private static void Prefix(Growup __instance)
        {
            Pens.AfterGrowUp();
            if (!Parts.GrowUpsOn)
            {
                return;
            }
            growing = __instance;
            Module.Guard("noting a young growing up", work);
            growing = null;
        }

        private static void Work()
        {
            Character young = growing.GetComponent<Character>();
            if (young != null)
            {
                Pens.BeforeGrowUp(young, young.transform.position, young.IsTamed());
            }
        }

        private static Exception Finalizer(Exception __exception)
        {
            Pens.AfterGrowUp();
            return __exception;
        }
    }

    /// <summary>An egg hatching: its spot noted, and the note cleared after it, even if the game's method throws (review N8).</summary>
    [HarmonyPatch]
    internal static class HatchPatch
    {
        private static readonly Action work = Work;
        private static EggGrow egg;

        private static bool Prepare() => GameHooks.EggGrowUpdate != null && GameHooks.SetLevel != null && !Broken.Is(Feature.PensHatching);

        private static MethodBase TargetMethod() => GameHooks.EggGrowUpdate;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensHatching, original, ex, harmony);

        private static void Prefix(EggGrow __instance)
        {
            Pens.AfterGrowUp();
            if (!Parts.GrowUpsOn)
            {
                return;
            }
            egg = __instance;
            Module.Guard("noting an egg hatching", work);
            egg = null;
        }

        private static void Work() => Pens.BeforeGrowUp(null, egg.transform.position, egg.m_tamed);

        private static Exception Finalizer(Exception __exception)
        {
            Pens.AfterGrowUp();
            return __exception;
        }
    }

    // ---------------------------------------------------------------------------------------- tames in pens: point 3

    /// <summary>What a creature sees, after the game: a chosen tame doesn't see an enemy behind a wall.</summary>
    [HarmonyPatch]
    internal static class SightPatch
    {
        private static readonly Action work = Work;
        private static BaseAI ai;
        private static Character target;
        private static bool seen;

        private static bool Prepare() => GameHooks.CanSeeTarget != null && GameHooks.AiCharacter != null && !Broken.Is(Feature.PensSight);

        private static MethodBase TargetMethod() => GameHooks.CanSeeTarget;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensSight, original, ex, harmony);

        private static void Postfix(BaseAI __instance, Character __0, ref bool __result, bool __runOriginal)
        {
            if (!__result || !Parts.SightOn || !__runOriginal || __0 == null)
            {
                return;
            }
            ai = __instance;
            target = __0;
            seen = true;
            Module.Guard("hiding an enemy behind a wall", work);
            ai = null;
            target = null;
            __result = seen;
        }

        private static void Work() => Pens.AfterCanSee(ai, target, ref seen);
    }

    /// <summary>An attack on a target: a chosen tame doesn't attack one behind a wall (a backstop). On Humanoid.StartAttack,
    /// the one call MonsterAI.DoAttack makes to attack, rather than on DoAttack itself (since House Trained 0.1.1, card
    /// A64): Skarif's ValheimPerformanceOverhaul switches its AI throttling off for the whole session when another mod puts
    /// a prefix on MonsterAI.DoAttack (its conflict guard watches UpdateAI, DoAttack and FindPath), and it doesn't watch
    /// this one. The same attacks are held as before: a creature's on an enemy, never a player's, one at nobody, or a
    /// healer's on a friend (DoAttack's isFriend: not an enemy).</summary>
    [HarmonyPatch]
    internal static class AttackPatch
    {
        private static readonly Action work = Work;
        private static Humanoid self;
        private static MonsterAI ai;
        private static Character target;
        private static bool attack;

        private static bool Prepare() => GameHooks.StartAttack != null && GameHooks.AiCharacter != null && !Broken.Is(Feature.PensAttack);

        private static MethodBase TargetMethod() => GameHooks.StartAttack;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensAttack, original, ex, harmony);

        // __0: Character target, __1: bool secondaryAttack. Players attack through here too (at nobody): left alone at once.
        private static bool Prefix(Humanoid __instance, Character __0, ref bool __result)
        {
            if (!Parts.SightOn || __0 == null || __instance == null || __instance.IsPlayer())
            {
                return true;
            }
            self = __instance;
            target = __0;
            attack = true;
            Module.Guard("holding an attack across a wall", work);
            self = null;
            ai = null;
            target = null;
            if (attack)
            {
                return true;
            }
            __result = false;
            return false;
        }

        // Only a creature's own AI attacking an enemy (MonsterAI.DoAttack with isFriend false) is asked about the wall.
        private static void Work()
        {
            ai = self.GetBaseAI() as MonsterAI;
            attack = ai == null || !BaseAI.IsEnemy(self, target) || Pens.MayAttack(ai, target);
        }
    }

    /// <summary>The closest enemy a creature senses, after the game: a chosen tame passes over one it can't reach behind
    /// a wall, for the closest that passes.</summary>
    [HarmonyPatch]
    internal static class FindEnemyPatch
    {
        private static readonly Action work = Work;
        private static BaseAI ai;
        private static Character found;

        private static bool Prepare() => GameHooks.FindEnemy != null && GameHooks.AiCharacter != null && !Broken.Is(Feature.PensFindEnemy);

        private static MethodBase TargetMethod() => GameHooks.FindEnemy;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensFindEnemy, original, ex, harmony);

        private static void Postfix(BaseAI __instance, ref Character __result, bool __runOriginal)
        {
            if (__result == null || !Parts.LetGoOn || !__runOriginal)
            {
                return;
            }
            ai = __instance;
            found = __result;
            Module.Guard("passing over an enemy behind a wall", work);
            __result = found;
            ai = null;
            found = null;
        }

        private static void Work() => Pens.AfterFindEnemy(ai, ref found);
    }

    /// <summary>A creature's target, after the game kept or changed it: a chosen tame lets go of one it can't reach
    /// behind a wall.</summary>
    [HarmonyPatch]
    internal static class LetGoPatch
    {
        private static readonly Action work = Work;
        private static MonsterAI ai;
        private static bool hear;
        private static bool see;

        private static bool Prepare() => GameHooks.UpdateTarget != null && GameHooks.TargetCreature != null && GameHooks.TimeSinceHurt != null &&
                                         GameHooks.SetTargetInfo != null && GameHooks.SetAlerted != null && GameHooks.AiCharacter != null &&
                                         !Broken.Is(Feature.PensLetGo);

        private static MethodBase TargetMethod() => GameHooks.UpdateTarget;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensLetGo, original, ex, harmony);

        // __2: out bool canHearTarget, __3: out bool canSeeTarget.
        private static void Postfix(MonsterAI __instance, ref bool __2, ref bool __3, bool __runOriginal)
        {
            if (!Parts.LetGoOn || !__runOriginal)
            {
                return;
            }
            ai = __instance;
            hear = __2;
            see = __3;
            Module.Guard("letting go of an enemy behind a wall", work);
            __2 = hear;
            __3 = see;
            ai = null;
        }

        private static void Work() => Pens.AfterUpdateTarget(ai, ref hear, ref see);
    }

    // ---------------------------------------------------------------------------------- tames in pens: points 2 and 4

    /// <summary>The idle wander, every AI update while a creature is idle: before it, a chosen tame's picks and its walk
    /// away; after it, its walk watched with the move the game has just set (code review 2, finding 1).</summary>
    [HarmonyPatch]
    internal static class IdlePatch
    {
        private static readonly Action before = Before;
        private static readonly Action after = After;
        private static BaseAI ai;

        private static bool Prepare() => GameHooks.IdleMovement != null && GameHooks.AiCharacter != null && GameHooks.WanderTarget != null &&
                                         GameHooks.WanderTimer != null && GameHooks.WanderReached != null && !Broken.Is(Feature.PensWander);

        private static MethodBase TargetMethod() => GameHooks.IdleMovement;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensWander, original, ex, harmony);

        private static void Prefix(BaseAI __instance)
        {
            if (!Parts.WanderOn && !Parts.HeadsOn)
            {
                return;
            }
            ai = __instance;
            Module.Guard("a tame's wandering in its pen", before);
            ai = null;
        }

        // It writes the walk's state and the tame's move, so it steps back when another mod skipped the game's method.
        private static void Postfix(BaseAI __instance, bool __runOriginal)
        {
            if (!__runOriginal || (!Parts.WanderOn && !Parts.HeadsOn))
            {
                return;
            }
            ai = __instance;
            Module.Guard("watching a tame's walk in its pen", after);
            ai = null;
        }

        private static void Before() => Pens.BeforeIdle(ai);

        private static void After() => Pens.AfterIdle(ai);
    }

    /// <summary>A creature's patrol point, after the game: a chosen tame ignores a far-away spawn point.</summary>
    [HarmonyPatch]
    internal static class FarHomePatch
    {
        private static readonly Action work = Work;
        private static BaseAI ai;
        private static Vector3 point;
        private static bool has;

        private static bool Prepare() => GameHooks.GetPatrolPoint != null && GameHooks.AiCharacter != null && !Broken.Is(Feature.PensFarHome);

        private static MethodBase TargetMethod() => GameHooks.GetPatrolPoint;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.PensFarHome, original, ex, harmony);

        // __0: out Vector3 point.
        private static void Postfix(BaseAI __instance, ref Vector3 __0, ref bool __result, bool __runOriginal)
        {
            if (!__result || !Parts.FarHomeOn || !__runOriginal)
            {
                return;
            }
            ai = __instance;
            point = __0;
            has = true;
            Module.Guard("ignoring a far-away spawn point", work);
            ai = null;
            __result = has;
        }

        private static void Work() => Pens.AfterPatrolPoint(ai, point, ref has);
    }

    // ------------------------------------------------------------------------------------------ feeding from the hand

    /// <summary>An item used on a creature, after the game (card A84): a food it eats, that the game didn't take (it takes
    /// only a saddle), feeds it (HandFeed). The game then goes no further: he doesn't eat the food himself.</summary>
    [HarmonyPatch]
    internal static class FeedPatch
    {
        private static readonly Action work = Work;
        private static Tameable tameable;
        private static Humanoid user;
        private static ItemDrop.ItemData item;
        private static bool used;

        private static bool Prepare() => GameHooks.TameableUseItem != null && !Broken.Is(Feature.FeedHand);

        private static MethodBase TargetMethod() => GameHooks.TameableUseItem;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.FeedHand, original, ex, harmony);

        // __0: Humanoid user, __1: ItemDrop.ItemData item.
        private static void Postfix(Tameable __instance, Humanoid __0, ItemDrop.ItemData __1, ref bool __result, bool __runOriginal)
        {
            if (__result || !__runOriginal || !Parts.FeedOn || __0 == null || __1 == null)
            {
                return;
            }
            tameable = __instance;
            user = __0;
            item = __1;
            used = false;
            HandFeed.Claimed = false;
            Module.Guard("feeding a creature from the hand", work);
            tameable = null;
            user = null;
            item = null;
            // A feed that failed part way (the item given back) still keeps the game from eating the food himself.
            __result = used || HandFeed.Claimed;
        }

        private static void Work() => used = HandFeed.Use(tameable, user, item);
    }

    /// <summary>An item used on a creature's saddle, after the game (code review m6): looking at a saddled Lox's back hovers
    /// its saddle, whose own UseItem takes nothing, so the use goes on to the creature's feed, as on the creature
    /// itself.</summary>
    [HarmonyPatch]
    internal static class SaddleFeedPatch
    {
        private static readonly Action work = Work;
        private static Sadle saddle;
        private static Humanoid user;
        private static ItemDrop.ItemData item;
        private static bool used;

        private static bool Prepare() => GameHooks.SadleUseItem != null && !Broken.Is(Feature.FeedSaddle);

        private static MethodBase TargetMethod() => GameHooks.SadleUseItem;

        private static Exception Cleanup(MethodBase original, Exception ex, Harmony harmony) => Broken.Patching(Feature.FeedSaddle, original, ex, harmony);

        // __0: Humanoid user, __1: ItemDrop.ItemData item.
        private static void Postfix(Sadle __instance, Humanoid __0, ItemDrop.ItemData __1, ref bool __result, bool __runOriginal)
        {
            if (__result || !__runOriginal || !Parts.FeedOn || __instance == null || __0 == null || __1 == null)
            {
                return;
            }
            saddle = __instance;
            user = __0;
            item = __1;
            used = false;
            HandFeed.Claimed = false;
            Module.Guard("feeding a saddled creature from the hand", work);
            saddle = null;
            user = null;
            item = null;
            __result = used || HandFeed.Claimed;
        }

        private static void Work()
        {
            Tameable tameable = saddle.GetTameable();
            used = tameable != null && HandFeed.Use(tameable, user, item);
        }
    }
}
