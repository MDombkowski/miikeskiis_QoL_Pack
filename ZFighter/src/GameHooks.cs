using System;
using System.Collections.Generic;
using System.Reflection;

namespace ZFighter
{
    /// <summary>
    /// The game methods this mod hooks, looked up once at startup. Checked against the game's WearNTear (Awake, where a
    /// piece sets itself up only when it has a saved record, and OnDestroy). Everything else it reads is public:
    /// WearNTear.m_new, Piece.m_usage, Piece.GetCreator, Utils.GetPrefabName.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>WearNTear.Awake(): a piece waking, the placement ghost included.</summary>
        internal static MethodInfo Awake { get; private set; }

        /// <summary>WearNTear.OnDestroy(): a piece going.</summary>
        internal static MethodInfo OnDestroy { get; private set; }

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            Awake = typeof(WearNTear).GetMethod("Awake", AnyInstance, null, Type.EmptyTypes, null);
            if (Awake == null)
            {
                missing.Add("WearNTear.Awake");
            }

            OnDestroy = typeof(WearNTear).GetMethod("OnDestroy", AnyInstance, null, Type.EmptyTypes, null);
            if (OnDestroy == null)
            {
                missing.Add("WearNTear.OnDestroy");
            }
        }
    }
}
