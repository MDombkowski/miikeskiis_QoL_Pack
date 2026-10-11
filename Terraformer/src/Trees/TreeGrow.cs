using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace Terraformer.Trees
{
    /// <summary>
    /// The rewrite of Plant.Grow that the lock needs: the game's three random draws, in their order (which tree, the
    /// turn, the size: Plant.Grow as read 2026-10-10, one int Range and two float Range calls), each replaced by a call
    /// to TreeLock's method for it, taking the same two arguments and the plant (`ldarg.0`, pushed just before the call).
    /// Anything else in the method is left exactly as it is. If the method doesn't hold exactly those three calls in that
    /// order (a game update, or another mod's rewrite of the same method), nothing is changed: the caller keeps the
    /// original and says so once in the log, and saplings grow as the game picks.
    /// </summary>
    internal static class TreeGrow
    {
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static MethodInfo TreeMethod => typeof(TreeLock).GetMethod(nameof(TreeLock.GrowTree), AnyStatic);

        internal static MethodInfo TurnMethod => typeof(TreeLock).GetMethod(nameof(TreeLock.GrowTurn), AnyStatic);

        internal static MethodInfo SizeMethod => typeof(TreeLock).GetMethod(nameof(TreeLock.GrowSize), AnyStatic);

        /// <summary>The method's instructions with the three draws replaced, as new instructions (the ones passed in are
        /// never changed); or null, with <paramref name="problem"/> saying why, when the pattern isn't exactly there.</summary>
        internal static List<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodInfo rangeInt, MethodInfo rangeFloat,
                                                      MethodInfo tree, MethodInfo turn, MethodInfo size, out string problem)
        {
            List<CodeInstruction> original = instructions.ToList();
            if (tree == null || turn == null || size == null)
            {
                // Said plainly, rather than failing inside HarmonyX as "Couldn't patch" (review round 1, finding 8).
                problem = "Terraformer couldn't find its own replacement draws (TreeLock.GrowTree, GrowTurn, GrowSize): a fault " +
                          "in Terraformer, not in the game";
                return null;
            }
            var ints = new List<int>();
            var floats = new List<int>();
            for (int i = 0; i < original.Count; i++)
            {
                if (Calls(original[i], rangeInt))
                {
                    ints.Add(i);
                }
                else if (Calls(original[i], rangeFloat))
                {
                    floats.Add(i);
                }
            }
            if (ints.Count != 1 || floats.Count != 2)
            {
                problem = $"Plant.Grow holds {ints.Count} whole-number and {floats.Count} decimal random draws where Terraformer " +
                          "expects 1 and 2 (a game update, or another mod's change to it)";
                return null;
            }
            if (!(ints[0] < floats[0] && floats[0] < floats[1]))
            {
                problem = "Plant.Grow's random draws aren't in the order Terraformer expects (tree, turn, size)";
                return null;
            }
            problem = null;
            var replaced = new Dictionary<int, MethodInfo> { [ints[0]] = tree, [floats[0]] = turn, [floats[1]] = size };
            var result = new List<CodeInstruction>(original.Count + 3);
            for (int i = 0; i < original.Count; i++)
            {
                // The copy constructor keeps the labels and exception blocks; HarmonyX's Clone() drops them (the harness
                // caught that: every jump in Plant.Grow would have lost its target).
                var copy = new CodeInstruction(original[i]);
                if (replaced.TryGetValue(i, out MethodInfo method))
                {
                    // The plant, pushed after the draw's two arguments; whatever jumped to the call now lands on it.
                    var plant = new CodeInstruction(OpCodes.Ldarg_0);
                    plant.labels.AddRange(copy.labels);
                    plant.blocks.AddRange(copy.blocks);
                    copy.labels.Clear();
                    copy.blocks.Clear();
                    result.Add(plant);
                    copy.opcode = OpCodes.Call;
                    copy.operand = method;
                }
                result.Add(copy);
            }
            return result;
        }

        private static bool warned;

        /// <summary>The log line for a rewrite left undone, said once a session (review round 2, finding 8: HarmonyX runs the
        /// transpiler again whenever another mod patches Plant.Grow later): the problem, then what it costs. Null after the
        /// first time.</summary>
        internal static string WarningOnce(string problem)
        {
            if (warned)
            {
                return null;
            }
            warned = true;
            return $"{problem}, so Plant.Grow is left as the game has it: saplings grow as the game picks, even those planted " +
                   "with the grown tree showing. The preview still shows.";
        }

        /// <summary>Whether this instruction calls that method (matched by its type, name and parameters, so a method
        /// found by another lookup still matches).</summary>
        internal static bool Calls(CodeInstruction instruction, MethodInfo method)
        {
            if (method == null || (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt) ||
                !(instruction.operand is MethodInfo called))
            {
                return false;
            }
            if (called == method)
            {
                return true;
            }
            if (called.DeclaringType != method.DeclaringType || called.Name != method.Name || called.ReturnType != method.ReturnType)
            {
                return false;
            }
            ParameterInfo[] a = called.GetParameters();
            ParameterInfo[] b = method.GetParameters();
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].ParameterType != b[i].ParameterType)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
