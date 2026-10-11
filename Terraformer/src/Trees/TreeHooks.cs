using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Terraformer.Trees
{
    /// <summary>Which part of the trees a patch serves, for what its failure costs (TreeHooks.Patching).</summary>
    internal enum TreePart
    {
        /// <summary>The preview and the lock's writing: lost whole.</summary>
        Preview,

        /// <summary>The build bar's line: only that is lost.</summary>
        Bar,

        /// <summary>The grow: locked saplings grow as the game picks.</summary>
        Grow,

        /// <summary>Keeping the turn as the game remakes the ghost: only "Keep my turn" is lost.</summary>
        Turn,
    }

    /// <summary>
    /// The game members the grown tree's preview uses, looked up once at startup (Module.Resolve) and checked by name and
    /// type in the installed game. A missing one costs only the trees, never the rest of Terraformer: it's named in the
    /// log as missing ("Running without …"), and each patch class whose target is missing is skipped (its Prepare).
    /// </summary>
    internal static class TreeHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Plant.Grow(): the three random draws the lock replaces.</summary>
        internal static MethodInfo Grow { get; private set; }

        /// <summary>Plant.Awake(): a fresh sapling, as the game places it.</summary>
        internal static MethodInfo PlantAwake { get; private set; }

        /// <summary>Player.PlacePiece(Piece, Vector3, Quaternion, bool, bool): his click places a piece.</summary>
        internal static MethodInfo PlacePiece { get; private set; }

        /// <summary>Hud.SetupPieceInfo(Piece): the build bar's name line, set every frame.</summary>
        internal static MethodInfo SetupPieceInfo { get; private set; }

        /// <summary>Player.SetupPlacementGhost() (private): the game remakes the ghost, with a new random turn for a sapling.
        /// Missing, only "Keep my turn" is lost.</summary>
        internal static MethodInfo SetupPlacementGhost { get; private set; }

        /// <summary>UnityEngine.Random.Range(int, int) and Range(float, float), the draws Plant.Grow makes.</summary>
        internal static MethodInfo RangeInt { get; private set; }

        internal static MethodInfo RangeFloat { get; private set; }

        // The build bar's name line (Hud.m_buildSelection, a TextMeshPro text) and its text, by reflection: Terraformer
        // doesn't reference TextMeshPro, and a game update that moves the line loses only the bar's words.
        private static FieldInfo buildSelection;
        private static PropertyInfo selectionText;

        /// <summary>The members found, by name, for a harness to check.</summary>
        internal static readonly List<string> Missing = new List<string>();

        internal static void Resolve(List<string> missingOptional)
        {
            Missing.Clear();
            Grow = Method(typeof(Plant), nameof(Plant.Grow), typeof(GameObject));
            PlantAwake = Method(typeof(Plant), nameof(Plant.Awake), typeof(void));
            PlacePiece = Method(typeof(Player), nameof(Player.PlacePiece), typeof(void),
                typeof(Piece), typeof(Vector3), typeof(Quaternion), typeof(bool), typeof(bool));
            SetupPieceInfo = Method(typeof(Hud), "SetupPieceInfo", typeof(void), typeof(Piece));
            SetupPlacementGhost = Method(typeof(Player), "SetupPlacementGhost", typeof(void));
            RangeInt = typeof(UnityEngine.Random).GetMethod(nameof(UnityEngine.Random.Range), BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(int), typeof(int) }, null);
            RangeFloat = typeof(UnityEngine.Random).GetMethod(nameof(UnityEngine.Random.Range), BindingFlags.Static | BindingFlags.Public,
                null, new[] { typeof(float), typeof(float) }, null);
            if (RangeInt == null || RangeInt.ReturnType != typeof(int))
            {
                RangeInt = null;
                Missing.Add("UnityEngine.Random.Range(int, int)");
            }
            if (RangeFloat == null || RangeFloat.ReturnType != typeof(float))
            {
                RangeFloat = null;
                Missing.Add("UnityEngine.Random.Range(float, float)");
            }
            buildSelection = typeof(Hud).GetField("m_buildSelection", AnyInstance);
            selectionText = buildSelection?.FieldType.GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (selectionText == null || selectionText.PropertyType != typeof(string) || !selectionText.CanRead || !selectionText.CanWrite)
            {
                buildSelection = null;
                selectionText = null;
                Missing.Add("Hud.m_buildSelection.text");
            }
            if (Missing.Count > 0)
            {
                missingOptional.Add($"{string.Join(", ", Missing)} ({Losses()})");
            }
        }

        // What the missing members cost, in words.
        private static string Losses()
        {
            var lost = new List<string>();
            if (Lost)
            {
                lost.Add("the grown tree's preview is off");
            }
            if (!CanNameInBar || SetupPieceInfo == null)
            {
                lost.Add("the build bar says nothing after a tool's name (the grown tree, \"paint only\")");
            }
            if (!Lost && SetupPlacementGhost == null)
            {
                lost.Add("\"Keep my turn\" doesn't hold after planting");
            }
            return string.Join("; ", lost);
        }

        /// <summary>The trees can't work at all: a member the preview or the lock needs is missing, or one of their patches
        /// couldn't be applied.</summary>
        internal static bool Lost => Failed || Grow == null || PlantAwake == null || PlacePiece == null || RangeInt == null || RangeFloat == null;

        /// <summary>A patch the preview needs couldn't be applied this session (<see cref="Patching"/>).</summary>
        internal static bool Failed { get; private set; }

        /// <summary>The build bar's patch couldn't be applied, or failed: the bar says nothing after a tool's name this
        /// session (neither the grown tree nor "paint only").</summary>
        internal static bool BarFailed { get; private set; }

        /// <summary>The bar patch's own guard: an error there costs only the bar's words for the session, said once, never the
        /// rest of Terraformer (MODULES.md rule 6). Never throws.</summary>
        internal static void BarBroke(Exception error)
        {
            if (BarFailed)
            {
                return;
            }
            BarFailed = true;
            try
            {
                Module.Log.LogWarning("The build bar couldn't be written, so it says nothing after a tool's name this session (the grown " +
                                      $"tree, \"paint only\"); the rest of Terraformer works. {error}");
            }
            catch (Exception)
            {
                // Nothing to say it with.
            }
        }

        /// <summary>
        /// A tree patch class's Cleanup (MODULES.md rule 7, Bar None's and Outfitter's way): nothing when its patch went on;
        /// otherwise only the trees are lost for the session (or only the build bar's line, or only the lock's grow), with
        /// a warning, and the class's own patch methods are taken back off the method, leaving every other patch of
        /// Terraformer's on it. Always null, so the kit goes on to the next class and the rest of Terraformer isn't
        /// touched. Never throws.
        /// </summary>
        internal static Exception Patching(string what, TreePart part, Type patchClass, MethodBase original, Exception error, HarmonyLib.Harmony harmony)
        {
            if (error == null)
            {
                return null;
            }
            string lost;
            switch (part)
            {
                case TreePart.Bar:
                    BarFailed = true;
                    lost = "the build bar says nothing after a tool's name (the grown tree, \"paint only\")";
                    break;
                case TreePart.Grow:
                    lost = "saplings grow as the game picks, even those planted with the preview";
                    break;
                case TreePart.Turn:
                    lost = "\"Keep my turn\" doesn't hold after planting";
                    break;
                default:
                    Failed = true;
                    lost = "the grown tree's preview is off";
                    break;
            }
            try
            {
                if (original != null && harmony != null)
                {
                    foreach (MethodInfo patch in patchClass.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (patch.Name == "Prefix" || patch.Name == "Postfix" || patch.Name == "Transpiler" || patch.Name == "Finalizer")
                        {
                            harmony.Unpatch(original, patch);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Taking the patch back can fail the same way the patch did; the part is lost either way.
            }
            try
            {
                Module.Log.LogWarning($"Couldn't patch {what}, so {lost} this session; the rest of Terraformer works. {error}");
            }
            catch (Exception)
            {
                // Nothing to say it with.
            }
            return null;
        }

        /// <summary>The build bar's name line was found, so the tree can be named there.</summary>
        internal static bool CanNameInBar => buildSelection != null && selectionText != null;

        /// <summary>The build bar's name line's text, or null.</summary>
        internal static string BarText(Hud hud)
        {
            object label = hud != null && buildSelection != null ? buildSelection.GetValue(hud) : null;
            return label != null ? selectionText.GetValue(label, null) as string : null;
        }

        internal static void SetBarText(Hud hud, string text)
        {
            object label = hud != null && buildSelection != null ? buildSelection.GetValue(hud) : null;
            if (label != null)
            {
                selectionText.SetValue(label, text, null);
            }
        }

        private static MethodInfo Method(Type type, string name, Type returns, params Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, AnyInstance | BindingFlags.DeclaredOnly, null, parameters, null);
            if (method == null || method.ReturnType != returns)
            {
                Missing.Add($"{type.Name}.{name}");
                return null;
            }
            return method;
        }
    }
}
