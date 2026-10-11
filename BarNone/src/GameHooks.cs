using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace BarNone
{
    /// <summary>
    /// The game methods this mod hooks, and the private members it uses, looked up once at startup. Checked against the
    /// decompiled game 1.0.16 (2026-09-30). Each part needs only its own: one missing after a game update switches off only
    /// that part, and the start's warning names it (the patch class's Prepare then skips it).
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static FieldInfo interactMask;
        private static FieldInfo fireBlocked;
        private static FieldInfo fireSolidMask;
        private static FieldInfo projectileOwner;
        private static FieldInfo plantRoofMask;
        private static FieldInfo plantSpaceMask;
        private static FieldInfo windmillCover;
        private static FieldInfo coverRays;
        private static FieldInfo coverMask;

        private static Vector3[] rays;
        private static int roofMask;
        private static int spaceMask;
        private static int solidMask;

        /// <summary>Player.FindHoverObject(out GameObject, out Character): what he points at, every frame.</summary>
        internal static MethodInfo FindHoverObject { get; private set; }

        /// <summary>Smoke.Awake(): each smoke puff as it's made.</summary>
        internal static MethodInfo SmokeAwake { get; private set; }

        /// <summary>Fireplace.CheckUnderTerrain(): a fire's "is anything right above me" test, every 4 s.</summary>
        internal static MethodInfo CheckUnderTerrain { get; private set; }

        /// <summary>Projectile.OnHit(Collider, Vector3, bool, Vector3): each hit along a projectile's step.</summary>
        internal static MethodInfo ProjectileOnHit { get; private set; }

        /// <summary>Plant.HaveRoof(): the plant's "no sun" ray.</summary>
        internal static MethodInfo HaveRoof { get; private set; }

        /// <summary>Plant.HaveGrowSpace(): the plant's "no space" sphere.</summary>
        internal static MethodInfo HaveGrowSpace { get; private set; }

        /// <summary>Beehive.HaveFreeSpace(): a beehive's (or birds' nest's) cover test.</summary>
        internal static MethodInfo BeehiveFreeSpace { get; private set; }

        /// <summary>WispSpawner.HaveFreeSpace(): the Wisp Fountain's and the Eternal Pyre's cover test.</summary>
        internal static MethodInfo WispFreeSpace { get; private set; }

        /// <summary>Windmill.CheckCover(): a windmill's cover, every 5 s.</summary>
        internal static MethodInfo WindmillCheckCover { get; private set; }

        /// <summary>ItemDrop.Awake(): each loose item as it's made or loaded.</summary>
        internal static MethodInfo ItemDropAwake { get; private set; }

        internal static bool ReachReady => FindHoverObject != null && interactMask != null;

        internal static bool SmokeReady => SmokeAwake != null;

        internal static bool FireReady => CheckUnderTerrain != null && fireBlocked != null;

        internal static bool ProjectilesReady => ProjectileOnHit != null && projectileOwner != null;

        internal static bool PlantsReady => HaveRoof != null;

        internal static bool SpaceReady => HaveGrowSpace != null;

        internal static bool CoverReady => coverRays != null && coverMask != null;

        internal static bool BeehivesReady => BeehiveFreeSpace != null && CoverReady;

        internal static bool WispsReady => WispFreeSpace != null && CoverReady;

        internal static bool WindmillsReady => WindmillCheckCover != null && windmillCover != null && CoverReady;

        internal static bool ItemsReady => ItemDropAwake != null;

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            const string reach = "reaching through pieces is then off";
            FindHoverObject = typeof(Player).GetMethod("FindHoverObject", AnyInstance, null,
                new[] { typeof(GameObject).MakeByRefType(), typeof(Character).MakeByRefType() }, null);
            Need(FindHoverObject, "Player.FindHoverObject", reach, missingOptional);
            interactMask = Field(typeof(Player), "m_interactMask", AnyInstance, typeof(int), reach, missingOptional);

            const string smoke = "smoke passing through pieces is then off";
            SmokeAwake = typeof(Smoke).GetMethod("Awake", AnyInstance, null, Type.EmptyTypes, null);
            Need(SmokeAwake, "Smoke.Awake", smoke, missingOptional);

            const string fire = "fires under listed pieces then follow the game's own test";
            CheckUnderTerrain = typeof(Fireplace).GetMethod("CheckUnderTerrain", AnyInstance, null, Type.EmptyTypes, null);
            Need(CheckUnderTerrain, "Fireplace.CheckUnderTerrain", fire, missingOptional);
            fireBlocked = Field(typeof(Fireplace), "m_blocked", AnyInstance, typeof(bool), fire, missingOptional);
            fireSolidMask = Field(typeof(Fireplace), "m_solidRayMask", AnyStatic, typeof(int), null, null);

            const string shots = "projectiles passing through pieces is then off";
            ProjectileOnHit = typeof(Projectile).GetMethod("OnHit", AnyInstance, null,
                new[] { typeof(Collider), typeof(Vector3), typeof(bool), typeof(Vector3) }, null);
            Need(ProjectileOnHit, "Projectile.OnHit", shots, missingOptional);
            projectileOwner = Field(typeof(Projectile), "m_owner", AnyInstance, typeof(Character), shots, missingOptional);

            const string plants = "crops then see the sky as in the game";
            HaveRoof = BoolMethod(typeof(Plant), "HaveRoof", plants, missingOptional);
            plantRoofMask = Field(typeof(Plant), "m_roofMask", AnyStatic, typeof(int), null, null);
            HaveGrowSpace = BoolMethod(typeof(Plant), "HaveGrowSpace", "growing space then follows the game's own test", missingOptional);
            plantSpaceMask = Field(typeof(Plant), "m_spaceMask", AnyStatic, typeof(int), null, null);

            const string cover = "beehives, windmills and wisp fountains then count cover as in the game";
            coverRays = Field(typeof(Cover), "m_coverRays", AnyStatic, typeof(Vector3[]), cover, missingOptional);
            coverMask = Field(typeof(Cover), "m_coverRayMask", AnyStatic, typeof(int), cover, missingOptional);
            BeehiveFreeSpace = BoolMethod(typeof(Beehive), "HaveFreeSpace", "beehives then count cover as in the game", missingOptional);
            WispFreeSpace = BoolMethod(typeof(WispSpawner), "HaveFreeSpace", "wisp fountains then count cover as in the game", missingOptional);
            const string windmills = "windmills then count cover as in the game";
            WindmillCheckCover = typeof(Windmill).GetMethod("CheckCover", AnyInstance, null, Type.EmptyTypes, null);
            Need(WindmillCheckCover, "Windmill.CheckCover", windmills, missingOptional);
            windmillCover = Field(typeof(Windmill), "m_cover", AnyInstance, typeof(float), windmills, missingOptional);

            ItemDropAwake = typeof(ItemDrop).GetMethod("Awake", AnyInstance, null, Type.EmptyTypes, null);
            Need(ItemDropAwake, "ItemDrop.Awake", "items falling through pieces is then off", missingOptional);
        }

        /// <summary>The player's own hover mask (set in its Awake): what his pointing ray can hit.</summary>
        internal static int InteractMask(Player player) => (int)interactMask.GetValue(player);

        /// <summary>Sets a fire's "blocked" flag, which its IsBurning reads.</summary>
        internal static void SetBlocked(Fireplace fire, bool blocked) => fireBlocked.SetValue(fire, blocked);

        /// <summary>The character who fired this projectile, or null (a ballista's, a catapult's, the world's own).</summary>
        internal static Character OwnerOf(Projectile projectile) => (Character)projectileOwner.GetValue(projectile);

        internal static void SetWindmillCover(Windmill windmill, float cover) => windmillCover.SetValue(windmill, cover);

        /// <summary>The fire's own ray mask (the game sets it in the first fire's Awake), or the same layers by name.</summary>
        internal static int FireSolidMask
        {
            get
            {
                if (solidMask == 0)
                {
                    int fromGame = fireSolidMask != null ? (int)fireSolidMask.GetValue(null) : 0;
                    solidMask = fromGame != 0 ? fromGame : LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
                }
                return solidMask;
            }
        }

        /// <summary>The plant's own roof mask (the game sets it at the first plant's test), or the same layers by name.</summary>
        internal static int PlantRoofMask
        {
            get
            {
                if (roofMask == 0)
                {
                    int fromGame = plantRoofMask != null ? (int)plantRoofMask.GetValue(null) : 0;
                    roofMask = fromGame != 0 ? fromGame : LayerMask.GetMask("Default", "static_solid", "piece");
                }
                return roofMask;
            }
        }

        /// <summary>The plant's own space mask, or the same layers by name.</summary>
        internal static int PlantSpaceMask
        {
            get
            {
                if (spaceMask == 0)
                {
                    int fromGame = plantSpaceMask != null ? (int)plantSpaceMask.GetValue(null) : 0;
                    spaceMask = fromGame != 0 ? fromGame : LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");
                }
                return spaceMask;
            }
        }

        /// <summary>The game's 17 cover rays (not normalised: the game's own loop to normalise them changes copies), or
        /// null until the game has made them at its first cover test.</summary>
        internal static Vector3[] CoverRays => rays ??= (Vector3[])coverRays.GetValue(null);

        /// <summary>The game's cover mask, or 0 until the game has set it at its first cover test.</summary>
        internal static int CoverMask => (int)coverMask.GetValue(null);

        private static MethodInfo BoolMethod(Type type, string name, string without, List<string> missingOptional)
        {
            MethodInfo method = type.GetMethod(name, AnyInstance, null, Type.EmptyTypes, null);
            if (method != null && method.ReturnType != typeof(bool))
            {
                method = null;
            }
            Need(method, $"{type.Name}.{name}", without, missingOptional);
            return method;
        }

        private static void Need(MethodInfo method, string name, string without, List<string> missingOptional)
        {
            if (method == null)
            {
                missingOptional.Add($"{name} ({without})");
            }
        }

        // A field of exactly this type, or null. A field the mod can do without (a mask it can make itself) is looked up
        // with no list to add to.
        private static FieldInfo Field(Type type, string name, BindingFlags flags, Type fieldType, string without, List<string> missingOptional)
        {
            FieldInfo field = type.GetField(name, flags);
            if (field != null && field.FieldType == fieldType)
            {
                return field;
            }
            missingOptional?.Add($"{type.Name}.{name} ({without})");
            return null;
        }
    }
}
