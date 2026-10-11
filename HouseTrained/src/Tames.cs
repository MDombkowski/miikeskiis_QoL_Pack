using System.Runtime.CompilerServices;
using UnityEngine;

namespace HouseTrained
{
    /// <summary>What the module keeps for one creature: its names, which parts look after it (worked out again whenever
    /// a setting changes), its head's reach, its own step, and the pens part's timers.</summary>
    internal sealed class TameInfo
    {
        internal string Prefab;
        internal string Token;
        internal string Shown;
        internal int Version = int.MinValue;
        internal bool Spared;
        internal bool Penned;
        internal bool FriendlyNever;
        internal bool HasTrample;
        internal Reach Reach;
        internal Transform Model;
        internal float OwnStep;
        internal float BodyRadius;
        /// <summary>How far its body reaches up from its lowest point (its root capsule's height as it stands: a lying
        /// capsule's is its width; code review 1, finding 4), at scale 1.</summary>
        internal float BodyHeight;
        /// <summary>Its body's lowest point above its root (0.37 m for a moose calf: code review 1, finding 2), at scale 1.</summary>
        internal float FootOffset;
        internal bool Commandable;
        internal int PensEpoch;

        internal readonly StuckWatch Stuck = new StuckWatch();
        internal readonly NudgeWatch Nudge = new NudgeWatch();
        internal readonly LetGoWatch LetGo = new LetGoWatch();
        internal readonly LogGate Log = new LogGate();
        internal readonly FarMemory Far = new FarMemory();
        internal float NextHeadCast;
        /// <summary>The last look ahead of its head found a wall (kept until the next look).</summary>
        internal bool WallWasAhead;
        /// <summary>A walk the module set this update, not yet moved along (the last look, nit 3).</summary>
        internal bool FreshWalk;
        internal float NextEaseOut;
        internal float NextLetGoCheck;
        internal Vector3 Picked = new Vector3(float.NaN, float.NaN, float.NaN);
        internal Character SightTarget;
        internal float SightUntil;
        internal bool SightBlocked;
        /// <summary>Whether it stands on its own navmesh, asked at most once a second (decision 37).</summary>
        internal bool OnOwnMesh;
        internal float OwnMeshUntil;

        // Healing in pens (card A84; PenHealing): its next beat, the last beat this game healed it at (NaN: none since this
        // game took it over), and whether it's in a pen, looked at again after a while (-1 in PenSaid: not yet told).
        internal float NextHeal;
        internal bool HealSeen;
        internal float HealLast = float.NaN;
        internal float PenUntil;
        internal int PenVersion = int.MinValue;
        internal bool InPen;
        internal int PenSaid = -1;

        /// <summary>Its head's reach now: the table's, times the creature's scale (world modifiers scale its root) and
        /// its model's (stars grow some models, not their colliders; review N9).</summary>
        internal Reach ReachNow(Transform root)
        {
            float scale = root.localScale.y;
            float model = Model != null ? Model.localScale.z : 1f;
            float ahead = scale * model;
            return new Reach(Reach.Ahead * ahead, Reach.Top * ahead, Reach.Radius * ahead);
        }

        /// <summary>Forget the timers and notes kept for it: the pens part switched off.</summary>
        internal void ForgetPens()
        {
            Stuck.Restart(Vector3.zero, float.NaN);
            LetGo.Reset();
            NextHeadCast = NextEaseOut = NextLetGoCheck = OwnMeshUntil = 0f;
            WallWasAhead = FreshWalk = false;
            Log.Clear();
            Far.Clear();
            Picked = new Vector3(float.NaN, float.NaN, float.NaN);
            SightTarget = null;
            SightUntil = 0f;
        }
    }

    /// <summary>
    /// The module's knowledge of each creature, kept beside it for as long as it lives (a weak table: nothing is added to
    /// the creature, and a creature gone takes its entry with it). Some of the module's checks run every physics step for
    /// every creature this game owns, so the names and lists are worked out once per creature and again only when a
    /// setting changes (Settings.Version), with no string work per call (review F15).
    /// </summary>
    internal static class Tames
    {
        private static readonly ConditionalWeakTable<Character, TameInfo> Infos = new ConditionalWeakTable<Character, TameInfo>();
        private static readonly ConditionalWeakTable<Character, TameInfo>.CreateValueCallback Make = Create;

        private static int listsVersion = int.MinValue;
        private static Names spareList;
        private static Names pensList;
        private static Names neverList;

        // Every creature's entry gets its pens timers forgotten at its next look once this moves on (the pens part switched
        // off): a weak table can't be walked through in this .NET.
        private static int pensEpoch;

        /// <summary>The module's entry for a creature, up to date with the settings.</summary>
        internal static TameInfo Of(Character creature)
        {
            TameInfo info = Infos.GetValue(creature, Make);
            if (info.Version != Settings.Version)
            {
                Refresh(info);
            }
            if (info.PensEpoch != pensEpoch)
            {
                info.ForgetPens();
                info.PensEpoch = pensEpoch;
            }
            return info;
        }

        /// <summary>Every tame's pens timers and notes forgotten (each at its next look): the pens part switched off.</summary>
        internal static void ForgetAllPens() => pensEpoch++;

        private static TameInfo Create(Character creature)
        {
            var info = new TameInfo
            {
                Prefab = Utils.GetPrefabName(creature.gameObject),
                Token = creature.m_name ?? string.Empty,
            };
            info.Shown = Localization.instance != null ? Localization.instance.Localize(info.Token) : info.Token;
            CapsuleCollider capsule = creature.GetComponent<CapsuleCollider>();
            info.BodyRadius = capsule != null ? capsule.radius : 0.5f;
            if (capsule != null)
            {
                Body(capsule.direction, capsule.center.y, capsule.radius, capsule.height, out info.FootOffset, out info.BodyHeight);
            }
            else
            {
                info.BodyHeight = 1.5f;
            }
            if (!HeadReach.TryKnown(info.Prefab, out info.Reach))
            {
                info.Reach = capsule != null
                    ? HeadReach.FromCapsule(capsule.direction, capsule.center.y, capsule.center.z, capsule.radius, capsule.height)
                    : new Reach(1f + HeadReach.Unknown, 1.5f, 0.25f);
            }
            LevelEffects stars = creature.GetComponentInChildren<LevelEffects>();
            info.Model = stars != null && stars.transform != creature.transform ? stars.transform : null;
            BaseAI ai = creature.GetBaseAI();
            info.OwnStep = ai != null ? Step(ai.m_pathAgentType) : 0.3f;
            MovementDamage trample = creature.GetComponent<MovementDamage>();
            info.HasTrample = trample != null && trample.m_runDamageObject != null;
            Tameable tameable = creature.GetComponent<Tameable>();
            info.Commandable = tameable != null && tameable.m_commandable;
            return info;
        }

        /// <summary>A root capsule's lowest point above the root, and how far it reaches up from there: an upright capsule
        /// (direction 1) reaches half its height (at least its radius) each way from its centre, a lying one its radius
        /// (code review 1, findings 2 and 4).</summary>
        internal static void Body(int direction, float centerY, float radius, float height, out float footOffset, out float bodyHeight)
        {
            float half = direction == 1 ? Mathf.Max(height / 2f, radius) : radius;
            footOffset = centerY - half;
            bodyHeight = 2f * half;
        }

        /// <summary>How high a tame's paths are planned to step (the navmesh agent's climb, Pathfinding.SetupAgents):
        /// 0.6 m for the troll-sized and bigger agents (the Lox's), 0.3 m for the rest (review F11).</summary>
        internal static float Step(Pathfinding.AgentType agent)
        {
            switch (agent)
            {
                case Pathfinding.AgentType.TrollSize:
                case Pathfinding.AgentType.HugeSize:
                case Pathfinding.AgentType.Abomination:
                case Pathfinding.AgentType.SeekerQueen:
                    return 0.6f;
                default:
                    return 0.3f;
            }
        }

        private static void Refresh(TameInfo info)
        {
            int version = Settings.Version;
            if (listsVersion != version)
            {
                listsVersion = version;
                spareList = Names.From(Settings.SpareList.Value);
                pensList = Names.From(Settings.PensList.Value);
                neverList = Names.From(Settings.FfNever.Value);
            }
            info.Spared = Names.Chooses(Settings.SpareWhich.Value, spareList, info.Prefab, info.Token, info.Shown);
            info.Penned = Names.Chooses(Settings.PensWhich.Value, pensList, info.Prefab, info.Token, info.Shown);
            info.FriendlyNever = neverList.HasAny(info.Prefab, info.Token, info.Shown);
            info.Version = version;
        }
    }
}
