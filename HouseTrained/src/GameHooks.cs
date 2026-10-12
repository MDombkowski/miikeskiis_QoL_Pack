using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// The game methods this mod hooks, and the private members it uses, looked up once at the start, as the design
    /// review's hook table has them (the decompiled game 1.0.16, 2026-10-05). Each feature needs only its own: one missing
    /// after a game update loses only that feature (its patch class's Prepare then skips it), and the kit's start line
    /// names it. Private fields are reached through references made here, never by Harmony's name injection, so a missing
    /// one can't stop a patch being applied (review N10).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal delegate bool PatrolPoint(BaseAI ai, out Vector3 point);

        // The patched methods.
        internal static MethodInfo WearNTearDamage { get; private set; }
        internal static MethodInfo WearNTearRpcDamage { get; private set; }
        internal static MethodInfo DestructibleDamage { get; private set; }
        internal static MethodInfo DestructibleRpcDamage { get; private set; }
        internal static MethodInfo CharacterDamage { get; private set; }
        internal static MethodInfo AttackStart { get; private set; }
        internal static MethodInfo AoeSetup { get; private set; }
        internal static MethodInfo ProjectileDoAoe { get; private set; }
        internal static MethodInfo ProjectileSetup { get; private set; }
        internal static MethodInfo MonsterOnDamaged { get; private set; }
        internal static MethodInfo IsEnemy { get; private set; }
        internal static MethodInfo UpdateGroundContact { get; private set; }
        internal static MethodInfo Procreate { get; private set; }
        internal static MethodInfo SetQuality { get; private set; }
        internal static MethodInfo SetLevel { get; private set; }
        internal static MethodInfo GrowupGrowUpdate { get; private set; }
        internal static MethodInfo EggGrowUpdate { get; private set; }
        internal static MethodInfo CanSeeTarget { get; private set; }
        internal static MethodInfo StartAttack { get; private set; }
        internal static MethodInfo FindEnemy { get; private set; }
        internal static MethodInfo UpdateTarget { get; private set; }
        internal static MethodInfo IdleMovement { get; private set; }
        internal static MethodInfo GetPatrolPoint { get; private set; }
        internal static MethodInfo TameableUseItem { get; private set; }
        internal static MethodInfo SadleUseItem { get; private set; }
        internal static MethodInfo TameableInteract { get; private set; }
        internal static MethodInfo TameableHover { get; private set; }
        internal static MethodInfo SadleInteract { get; private set; }
        internal static MethodInfo SadleHover { get; private set; }
        internal static MethodInfo TameableRpcCommand { get; private set; }

        // Private members.
        internal static AccessTools.FieldRef<WearNTear, ZNetView> PieceView;
        internal static AccessTools.FieldRef<Destructible, ZNetView> DestructibleView;
        internal static AccessTools.FieldRef<Projectile, Character> ProjectileOwner;
        internal static AccessTools.FieldRef<Character, bool> GroundContact;
        internal static AccessTools.FieldRef<Character, Collider> GroundCollider;
        internal static AccessTools.FieldRef<Character, Vector3> GroundPoint;
        internal static AccessTools.FieldRef<Character, Rigidbody> Body;
        internal static Action<Character> ResetGroundContact;
        internal static AccessTools.FieldRef<BaseAI, Character> AiCharacter;
        internal static AccessTools.FieldRef<BaseAI, Vector3> WanderTarget;
        internal static AccessTools.FieldRef<BaseAI, float> WanderTimer;
        internal static AccessTools.FieldRef<BaseAI, bool> WanderReached;
        /// <summary>When the game last planned a path (its one-second cache), set back so a new wander point is planned at
        /// once (code review 2, finding 2); without it, the game's old path is followed for up to a second.</summary>
        internal static AccessTools.FieldRef<BaseAI, float> LastPathTime;
        internal static AccessTools.FieldRef<BaseAI, float> TimeSinceHurt;
        internal static AccessTools.FieldRef<MonsterAI, Character> TargetCreature;
        internal static Func<BaseAI, Vector3, bool> ValidWanderPoint;
        internal static PatrolPoint PatrolPointOf;
        internal static Action<BaseAI, ZDOID> SetTargetInfo;
        internal static Action<BaseAI, bool> SetAlerted;
        internal static Func<Pathfinding, Pathfinding.AgentType, int> AgentId;

        // With the Hoarstone (card B26): a tame's walk home, its edge while following, and its hunt on the ground.
        internal static MethodInfo Follow { get; private set; }
        internal static Func<BaseAI, float, Vector3, float, bool, bool> MoveTo;
        internal static AccessTools.FieldRef<MonsterAI, Vector3> LastKnownTargetPos;
        internal static AccessTools.FieldRef<MonsterAI, bool> BeenAtLastPos;
        internal static AccessTools.FieldRef<MonsterAI, float> TimeSinceSensed;
        internal static AccessTools.FieldRef<MonsterAI, float> TimeSinceAttacking;
        internal static AccessTools.FieldRef<MonsterAI, StaticTarget> TargetStatic;
        internal static Action<BaseAI, ZDOID> GroundTargetInfo;
        internal static Action<BaseAI, bool> GroundAlerted;
        internal static AccessTools.FieldRef<MonsterAI, Character> GroundTargetCreature;
        internal static AccessTools.FieldRef<BaseAI, float> GroundTimeSinceHurt;

        internal static void Resolve(List<string> missingOptional)
        {
            FieldsFound.Clear();
            Type hit = typeof(HitData);
            Type item = typeof(ItemDrop.ItemData);

            // Tames spare buildings.
            WearNTearDamage = Method(typeof(WearNTear), "Damage", AnyInstance, new[] { hit }, Feature.SpareSend, missingOptional);
            WearNTearRpcDamage = Method(typeof(WearNTear), "RPC_Damage", AnyInstance, new[] { typeof(long), hit }, Feature.SpareLand, missingOptional);
            PieceView = Field<WearNTear, ZNetView>("m_nview", Feature.SpareLand, missingOptional);
            DestructibleDamage = Method(typeof(Destructible), "Damage", AnyInstance, new[] { hit }, Feature.CropsSend, missingOptional);
            DestructibleRpcDamage = Method(typeof(Destructible), "RPC_Damage", AnyInstance, new[] { typeof(long), hit }, Feature.CropsLand, missingOptional);
            DestructibleView = Field<Destructible, ZNetView>("m_nview", Feature.CropsLand, missingOptional);

            // Friendly fire.
            CharacterDamage = Method(typeof(Character), "Damage", AnyInstance, new[] { hit }, Feature.FriendlyJudge, missingOptional);
            AttackStart = Method(typeof(Attack), "Start", AnyInstance,
                new[] { typeof(Humanoid), typeof(Rigidbody), typeof(ZSyncAnimation), typeof(CharacterAnimEvent), typeof(VisEquipment), item, typeof(Attack), typeof(float), typeof(float) },
                Feature.FriendlyAttacks, missingOptional);
            Type[] setup = { typeof(Character), typeof(Vector3), typeof(float), hit, item, item };
            AoeSetup = Method(typeof(Aoe), "Setup", AnyInstance, setup, Feature.FriendlyAreaObjects, missingOptional);
            ProjectileDoAoe = Method(typeof(Projectile), "DoAOE", AnyInstance,
                new[] { typeof(Vector3), typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType() }, Feature.FriendlyBlasts, missingOptional);
            ProjectileOwner = Field<Projectile, Character>("m_owner", Feature.FriendlyBlasts, missingOptional);
            ProjectileSetup = Method(typeof(Projectile), "Setup", AnyInstance, setup, Feature.FriendlyProjectiles, missingOptional);
            MonsterOnDamaged = Method(typeof(MonsterAI), "OnDamaged", AnyInstance | BindingFlags.DeclaredOnly,
                new[] { typeof(float), typeof(Character) }, Feature.FightBackHurt, missingOptional);
            IsEnemy = Method(typeof(BaseAI), "IsEnemy", AnyStatic, new[] { typeof(Character), typeof(Character) }, Feature.FightBackEnemy, missingOptional);

            // Tames in pens: point 1.
            UpdateGroundContact = Method(typeof(Character), "UpdateGroundContact", AnyInstance, new[] { typeof(float) }, Feature.PensGround, missingOptional);
            GroundContact = Field<Character, bool>("m_groundContact", Feature.PensGround, missingOptional);
            GroundCollider = Field<Character, Collider>("m_lowestContactCollider", Feature.PensGround, missingOptional);
            GroundPoint = Field<Character, Vector3>("m_groundContactPoint", Feature.PensGround, missingOptional);
            Body = Field<Character, Rigidbody>("m_body", Feature.PensGround, missingOptional);
            ResetGroundContact = Delegate<Action<Character>>(typeof(Character), "ResetGroundContact", AnyInstance, Type.EmptyTypes, Feature.PensGround, missingOptional);
            Procreate = Method(typeof(Procreation), "Procreate", AnyInstance, Type.EmptyTypes, Feature.PensBirths, missingOptional);
            SetQuality = Method(typeof(ItemDrop), "SetQuality", AnyInstance, new[] { typeof(int) }, Feature.PensEggs, missingOptional);
            SetLevel = Method(typeof(Character), "SetLevel", AnyInstance, new[] { typeof(int) }, Feature.PensSetLevel, missingOptional);
            GrowupGrowUpdate = Method(typeof(Growup), "GrowUpdate", AnyInstance, Type.EmptyTypes, Feature.PensGrowUps, missingOptional);
            EggGrowUpdate = Method(typeof(EggGrow), "GrowUpdate", AnyInstance, Type.EmptyTypes, Feature.PensHatching, missingOptional);

            // Point 3.
            CanSeeTarget = Method(typeof(BaseAI), "CanSeeTarget", AnyInstance, new[] { typeof(Character) }, Feature.PensSight, missingOptional);
            AiCharacter = Field<BaseAI, Character>("m_character", Feature.PensSight, missingOptional);
            StartAttack = Method(typeof(Humanoid), "StartAttack", AnyInstance, new[] { typeof(Character), typeof(bool) }, Feature.PensAttack, missingOptional);
            FindEnemy = Method(typeof(BaseAI), "FindEnemy", AnyInstance, Type.EmptyTypes, Feature.PensFindEnemy, missingOptional);
            UpdateTarget = Method(typeof(MonsterAI), "UpdateTarget", AnyInstance,
                new[] { typeof(Humanoid), typeof(float), typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType() }, Feature.PensLetGo, missingOptional);
            TargetCreature = Field<MonsterAI, Character>("m_targetCreature", Feature.PensLetGo, missingOptional);
            TimeSinceHurt = Field<BaseAI, float>("m_timeSinceHurt", Feature.PensLetGo, missingOptional);
            SetTargetInfo = Delegate<Action<BaseAI, ZDOID>>(typeof(BaseAI), "SetTargetInfo", AnyInstance, new[] { typeof(ZDOID) }, Feature.PensLetGo, missingOptional);
            SetAlerted = Delegate<Action<BaseAI, bool>>(typeof(BaseAI), "SetAlerted", AnyInstance, new[] { typeof(bool) }, Feature.PensLetGo, missingOptional);

            // Points 2 and 4.
            IdleMovement = Method(typeof(BaseAI), "IdleMovement", AnyInstance, new[] { typeof(float) }, Feature.PensWander, missingOptional);
            WanderTarget = Field<BaseAI, Vector3>("m_randomMoveTarget", Feature.PensWander, missingOptional);
            WanderTimer = Field<BaseAI, float>("m_randomMoveUpdateTimer", Feature.PensWander, missingOptional);
            WanderReached = Field<BaseAI, bool>("m_reachedRandomMoveTarget", Feature.PensWander, missingOptional);
            LastPathTime = Field<BaseAI, float>("m_lastFindPathTime", Feature.PensWander, missingOptional, needed: false);
            ValidWanderPoint = Delegate<Func<BaseAI, Vector3, bool>>(typeof(BaseAI), "IsValidRandomMovePoint", AnyInstance, new[] { typeof(Vector3) }, Feature.PensWander, missingOptional);
            AgentId = Delegate<Func<Pathfinding, Pathfinding.AgentType, int>>(typeof(Pathfinding), "GetAgentID", AnyInstance,
                new[] { typeof(Pathfinding.AgentType) }, Feature.PensWander, missingOptional);
            GetPatrolPoint = Method(typeof(BaseAI), "GetPatrolPoint", AnyInstance, new[] { typeof(Vector3).MakeByRefType() }, Feature.PensFarHome, missingOptional);
            PatrolPointOf = Delegate<PatrolPoint>(typeof(BaseAI), "GetPatrolPoint", AnyInstance, new[] { typeof(Vector3).MakeByRefType() }, Feature.PensWander, missingOptional);
            // Feeding from the hand (card A84): the game's use of an item on a creature it looks at.
            TameableUseItem = Method(typeof(Tameable), "UseItem", AnyInstance, new[] { typeof(Humanoid), item }, Feature.FeedHand, missingOptional);
            // Aiming at a saddled creature's saddle hovers the saddle, whose own UseItem takes nothing (code review m6).
            SadleUseItem = Method(typeof(Sadle), "UseItem", AnyInstance, new[] { typeof(Humanoid), item }, Feature.FeedSaddle, missingOptional);

            // Following (card B25) and "Return to base" (card B26): E on a tame and on its saddle, and their hover texts.
            TameableInteract = Method(typeof(Tameable), "Interact", AnyInstance, new[] { typeof(Humanoid), typeof(bool), typeof(bool) }, Feature.TameInteract, missingOptional);
            TameableHover = Method(typeof(Tameable), "GetHoverText", AnyInstance, Type.EmptyTypes, Feature.TameHover, missingOptional);
            SadleInteract = Method(typeof(Sadle), "Interact", AnyInstance, new[] { typeof(Humanoid), typeof(bool), typeof(bool) }, Feature.SaddleInteract, missingOptional);
            SadleHover = Method(typeof(Sadle), "GetHoverText", AnyInstance, Type.EmptyTypes, Feature.SaddleHover, missingOptional);
            TameableRpcCommand = Method(typeof(Tameable), "RPC_Command", AnyInstance, new[] { typeof(long), typeof(ZDOID), typeof(bool) }, Feature.TameStayMark, missingOptional);

            // With the Hoarstone (card B26), each of its three patches with its own members, so one lost leaves the others.
            MoveTo = Delegate<Func<BaseAI, float, Vector3, float, bool, bool>>(typeof(BaseAI), "MoveTo", AnyInstance,
                new[] { typeof(float), typeof(Vector3), typeof(float), typeof(bool) }, Feature.GroundIdle, missingOptional);
            if (MoveTo == null)
            {
                Broken.Missing(Feature.GroundFollow);
            }
            Follow = Method(typeof(BaseAI), "Follow", AnyInstance, new[] { typeof(GameObject), typeof(float) }, Feature.GroundFollow, missingOptional);
            GroundTargetCreature = Field<MonsterAI, Character>("m_targetCreature", Feature.GroundTarget, missingOptional);
            LastKnownTargetPos = Field<MonsterAI, Vector3>("m_lastKnownTargetPos", Feature.GroundTarget, missingOptional);
            BeenAtLastPos = Field<MonsterAI, bool>("m_beenAtLastPos", Feature.GroundTarget, missingOptional);
            TimeSinceSensed = Field<MonsterAI, float>("m_timeSinceSensedTargetCreature", Feature.GroundTarget, missingOptional);
            TimeSinceAttacking = Field<MonsterAI, float>("m_timeSinceAttacking", Feature.GroundTarget, missingOptional);
            TargetStatic = Field<MonsterAI, StaticTarget>("m_targetStatic", Feature.GroundTarget, missingOptional);
            GroundTimeSinceHurt = Field<BaseAI, float>("m_timeSinceHurt", Feature.GroundTarget, missingOptional);
            GroundTargetInfo = Delegate<Action<BaseAI, ZDOID>>(typeof(BaseAI), "SetTargetInfo", AnyInstance, new[] { typeof(ZDOID) }, Feature.GroundTarget, missingOptional);
            GroundAlerted = Delegate<Action<BaseAI, bool>>(typeof(BaseAI), "SetAlerted", AnyInstance, new[] { typeof(bool) }, Feature.GroundTarget, missingOptional);
            if (UpdateTarget == null)
            {
                Broken.Missing(Feature.GroundTarget);
            }
            if (IdleMovement == null || WanderTarget == null || WanderTimer == null || WanderReached == null || PatrolPointOf == null)
            {
                Broken.Missing(Feature.GroundIdle);
            }

            if (AiCharacter == null)
            {
                foreach (Feature needs in new[] { Feature.PensWander, Feature.PensFindEnemy, Feature.PensLetGo, Feature.PensAttack, Feature.PensFarHome, Feature.FightBackHurt,
                                                  Feature.GroundIdle, Feature.GroundTarget, Feature.GroundFollow })
                {
                    Broken.Missing(needs);
                }
            }
        }

        private static MethodInfo Method(Type type, string name, BindingFlags flags, Type[] parameters, Feature feature, List<string> missing)
        {
            MethodInfo method = null;
            try
            {
                method = type.GetMethod(name, flags, null, parameters, null);
            }
            catch (AmbiguousMatchException)
            {
            }
            if (method == null)
            {
                Lose(feature, $"{type.Name}.{name}", missing);
            }
            return method;
        }

        /// <summary>Every private field looked up, and whether the game has it as expected (its name and type), for the
        /// harness: there the references themselves can't be made (MonoMod emits them, which the harness's .NET doesn't
        /// run), but whether each field is there can be checked.</summary>
        internal static readonly List<KeyValuePair<string, bool>> FieldsFound = new List<KeyValuePair<string, bool>>();

        // A field that isn't needed only refines its feature: without it the feature runs a little cruder, and nothing is
        // lost or said.
        private static AccessTools.FieldRef<T, F> Field<T, F>(string name, Feature feature, List<string> missing, bool needed = true)
        {
            FieldInfo field = typeof(T).GetField(name, AnyInstance);
            bool found = field != null && field.FieldType == typeof(F);
            FieldsFound.Add(new KeyValuePair<string, bool>($"{typeof(T).Name}.{name}", found));
            if (found)
            {
                try
                {
                    return AccessTools.FieldRefAccess<T, F>(field);
                }
                catch (Exception)
                {
                    // The reference couldn't be made: its feature is lost below.
                }
            }
            if (needed)
            {
                Lose(feature, $"{typeof(T).Name}.{name}", missing);
            }
            return null;
        }

        private static D Delegate<D>(Type type, string name, BindingFlags flags, Type[] parameters, Feature feature, List<string> missing) where D : Delegate
        {
            try
            {
                MethodInfo method = type.GetMethod(name, flags, null, parameters, null);
                if (method != null)
                {
                    return AccessTools.MethodDelegate<D>(method, null, virtualCall: true);
                }
            }
            catch (Exception)
            {
                // A method the game no longer has the same way: its feature is lost below.
            }
            Lose(feature, $"{type.Name}.{name}", missing);
            return null;
        }

        private static void Lose(Feature feature, string member, List<string> missing)
        {
            Broken.Missing(feature);
            missing.Add($"{member} ({Broken.Consequence(feature).TrimEnd(',')})");
        }
    }
}
