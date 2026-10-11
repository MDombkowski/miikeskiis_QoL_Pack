using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;

namespace SpareKeys
{
    /// <summary>A key a mod's code reads with a fixed KeyCode: which key, whether only held (GetKey) or pressed
    /// (GetKeyDown, GetKeyUp), and where ("MapIconPicker.TryEditPinUnderCursor").</summary>
    internal sealed class CodeHit
    {
        internal KeyCode Key;
        internal bool Held;
        internal string Where;
    }

    /// <summary>
    /// Reads one mod's assembly with Mono.Cecil (it ships with BepInEx) for calls to GetKey, GetKeyDown or GetKeyUp on
    /// Unity's Input, the game's ZInput or BepInEx's UnityInput whose KeyCode is a constant. It finds what a mod reads with
    /// no setting behind it; it can't see keys kept in arrays or variables, read by name, read through the Input System
    /// or IMGUI, or passed through a mod's own helper, and never knows when a key applies (the research's part 2). Only
    /// reads the file, into memory, so it is never held open. Plain code: the harness runs it on his plugins.
    /// </summary>
    internal static class CodeReader
    {
        private static readonly HashSet<string> Readers = new HashSet<string>(StringComparer.Ordinal) { "GetKey", "GetKeyDown", "GetKeyUp" };

        /// <summary>Every fixed key the assembly at <paramref name="path"/> reads, each key and place once.</summary>
        internal static List<CodeHit> Scan(string path)
        {
            var hits = new List<CodeHit>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            using (var stream = new MemoryStream(File.ReadAllBytes(path)))
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(stream, new ReaderParameters { ReadSymbols = false }))
            {
                foreach (ModuleDefinition module in assembly.Modules)
                {
                    foreach (TypeDefinition type in module.GetTypes())
                    {
                        foreach (MethodDefinition method in type.Methods)
                        {
                            if (!method.HasBody)
                            {
                                continue;
                            }
                            ScanMethod(type, method, hits, seen);
                        }
                    }
                }
            }
            return hits;
        }

        private static void ScanMethod(TypeDefinition type, MethodDefinition method, List<CodeHit> hits, HashSet<string> seen)
        {
            IList<Instruction> code = method.Body.Instructions;
            for (int i = 0; i < code.Count; i++)
            {
                OpCode op = code[i].OpCode;
                if ((op.Code != Code.Call && op.Code != Code.Callvirt) || !(code[i].Operand is MethodReference called))
                {
                    continue;
                }
                if (!Readers.Contains(called.Name) || called.Parameters.Count < 1 || called.Parameters.Count > 2 ||
                    called.Parameters[0].ParameterType.FullName != "UnityEngine.KeyCode")
                {
                    continue;
                }
                string owner = called.DeclaringType.FullName;
                if (owner != "UnityEngine.Input" && owner != "ZInput" && !owner.StartsWith("BepInEx.", StringComparison.Ordinal))
                {
                    continue;
                }
                // The KeyCode is the first argument: just before the call, or before the one constant that follows it
                // (ZInput's logWarning flag).
                int at = i - called.Parameters.Count;
                if (at < 0 || (called.Parameters.Count == 2 && Constant(code[i - 1]) == null))
                {
                    continue;
                }
                int? value = Constant(code[at]);
                if (value == null || !Enum.IsDefined(typeof(KeyCode), value.Value) || value.Value == 0)
                {
                    continue;
                }
                var key = (KeyCode)value.Value;
                bool held = called.Name == "GetKey";
                string where = Where(type, method);
                if (seen.Add($"{(int)key}|{held}|{where}"))
                {
                    hits.Add(new CodeHit { Key = key, Held = held, Where = where });
                }
            }
        }

        // A type's own name and the method's, with a compiler-made type ("<>c__DisplayClass") named by the type it is in.
        private static string Where(TypeDefinition type, MethodDefinition method)
        {
            TypeDefinition named = type;
            while (named.Name.StartsWith("<", StringComparison.Ordinal) && named.DeclaringType != null)
            {
                named = named.DeclaringType;
            }
            string name = method.Name;
            if (name.StartsWith("<", StringComparison.Ordinal))
            {
                int end = name.IndexOf('>');
                name = end > 1 ? name.Substring(1, end - 1) : name;
            }
            return named.Name + "." + name;
        }

        private static int? Constant(Instruction instruction)
        {
            switch (instruction.OpCode.Code)
            {
                case Code.Ldc_I4:
                    return (int)instruction.Operand;
                case Code.Ldc_I4_S:
                    return (sbyte)instruction.Operand;
                case Code.Ldc_I4_0:
                    return 0;
                case Code.Ldc_I4_1:
                    return 1;
                case Code.Ldc_I4_2:
                    return 2;
                case Code.Ldc_I4_3:
                    return 3;
                case Code.Ldc_I4_4:
                    return 4;
                case Code.Ldc_I4_5:
                    return 5;
                case Code.Ldc_I4_6:
                    return 6;
                case Code.Ldc_I4_7:
                    return 7;
                case Code.Ldc_I4_8:
                    return 8;
                case Code.Ldc_I4_M1:
                    return -1;
                default:
                    return null;
            }
        }
    }
}
