using System.Diagnostics;
using UnityEngine;

namespace UseCase
{
    /// <summary>
    /// The world's index of uses: read once the world is ready (the game's item list, its scene and his character exist,
    /// so other mods' items, recipes and pieces are in), again in a new world, and again when the recipes' and tables'
    /// fingerprint changes (looked at every few seconds). Only while the panel is wanted: a friend who never switches it
    /// on never pays for it.
    /// </summary>
    internal static class Index
    {
        private const float LookEvery = 5f;

        private static ZNetScene builtFor;
        private static int builtSignature;
        private static float lookedAt;

        internal static UseIndex Current { get; private set; }

        internal static void Tick()
        {
            if (ObjectDB.instance == null || ZNetScene.instance == null || Player.m_localPlayer == null)
            {
                return;
            }
            if (Current != null && builtFor == ZNetScene.instance)
            {
                if (Time.unscaledTime - lookedAt < LookEvery)
                {
                    return;
                }
                lookedAt = Time.unscaledTime;
                if (GameReader.Signature() == builtSignature)
                {
                    return;
                }
            }
            Build();
        }

        /// <summary>Everything let go (the module's undo after an error).</summary>
        internal static void Forget()
        {
            Current = null;
            builtFor = null;
            Sources.Clear();
            Panel.Forget();
        }

        private static void Build()
        {
            bool again = Current != null && builtFor == ZNetScene.instance;
            Stopwatch clock = Stopwatch.StartNew();
            GameFactsRead facts = GameReader.Read(message => Module.Log.LogWarning(message));
            var index = new UseIndex(facts);
            builtFor = ZNetScene.instance;
            builtSignature = GameReader.Signature();
            lookedAt = Time.unscaledTime;
            Panel.Forget();
            Current = index;
            Places.Relearn(index);
            Module.Log.LogInfo($"{(again ? "The recipes or pieces changed: read again" : "Read this world's uses")}: {index.Count} uses of " +
                               $"{index.Resources} items, in {clock.ElapsedMilliseconds} ms.");
        }
    }
}
