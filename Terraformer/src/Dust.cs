using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// The dust each terrain tool kicks up where it lands (his ask, 2026-09-30: a "Dust" section with a switch per tool).
    /// The dust is the visual half of the effects the game plays for a placed stroke: the piece's place effect (the
    /// game's `vfx_Place_mud_road` and `vfx_Place_replant`) and the stroke's own on-placed effect (Raise ground's
    /// `vfx_Place_raise`, the pickaxe dig's `vfx_Place_digg`, which Lower ground plays); the sounds beside them stay. A
    /// switch that's off sets the dust entries' own "enabled" flag, which the game's effect list reads as it plays them,
    /// on that one tool's prefab; each entry keeps the flag it came with, so an entry the game itself had off stays off,
    /// and switching Terraformer off gives every tool its own back at once (MODULES.md rule 5).
    /// </summary>
    internal static class Dust
    {
        internal sealed class Entry
        {
            /// <summary>Its switch's key in the Dust section, permanent.</summary>
            internal string Key;

            internal string EnglishName;

            /// <summary>The game's own piece, by prefab name; null for one of Terraformer's (<see cref="Tool"/>).</summary>
            internal string GamePrefab;

            internal Tool Tool;
        }

        /// <summary>The game's six terrain tools, then Terraformer's, in the order the settings list them.</summary>
        internal static readonly Entry[] Entries = Build();

        private static Entry[] Build()
        {
            var entries = new List<Entry>
            {
                new Entry { Key = "LevelGround", EnglishName = "Level ground", GamePrefab = "mud_road_v2" },
                new Entry { Key = "RaiseGround", EnglishName = "Raise ground", GamePrefab = "raise_v2" },
                new Entry { Key = "Pathen", EnglishName = "Pathen", GamePrefab = "path_v2" },
                new Entry { Key = "PavedRoad", EnglishName = "Paved road", GamePrefab = "paved_road_v2" },
                new Entry { Key = "Cultivate", EnglishName = "Cultivate", GamePrefab = "cultivate_v2" },
                new Entry { Key = "Grass", EnglishName = "Grass", GamePrefab = "replant_v2" },
            };
            foreach (Tool tool in Tool.All)
            {
                entries.Add(new Entry { Key = tool.Id, EnglishName = tool.EnglishName, Tool = tool });
            }
            return entries.ToArray();
        }

        // Each dust entry's own flag (as it came, or as another mod last set it) and the flag Terraformer last wrote, by the
        // entry itself (each prefab's effect lists are its own objects: the game makes each prefab's, and Terraformer copies
        // Lower ground's rather than sharing the pickaxe's).
        private static readonly Dictionary<EffectList.EffectData, (bool Own, bool Written)> Flags =
            new Dictionary<EffectList.EffectData, (bool, bool)>();

        // The game's own pieces, found by name once the game has listed its terrain pieces.
        private static readonly Dictionary<string, GameObject> GamePieces = new Dictionary<string, GameObject>();

        // Whether a prefab is a visual effect (dust), by the prefab, measured once.
        private static readonly Dictionary<GameObject, bool> Visual = new Dictionary<GameObject, bool>();

        /// <summary>Every frame: each tool's dust follows its switch (and comes back while Terraformer is off).</summary>
        internal static void Sync()
        {
            ObjectDB db = ObjectDB.instance;
            foreach (Entry entry in Entries)
            {
                GameObject prefab = entry.Tool != null ? entry.Tool.Object : GamePiece(db, entry.GamePrefab);
                if (prefab == null)
                {
                    continue;
                }
                bool dust = !Module.IsOn || Settings.DustOn(entry.Key);
                Apply(prefab.GetComponent<Piece>()?.m_placeEffect, dust);
                Apply(prefab.GetComponent<TerrainOp>()?.m_onPlacedEffect, dust);
            }
        }

        /// <summary>Gives every entry it touched its own flag back (after an error).</summary>
        internal static void Restore()
        {
            foreach (KeyValuePair<EffectList.EffectData, (bool Own, bool Written)> pair in Flags)
            {
                if (pair.Key != null)
                {
                    pair.Key.m_enabled = pair.Value.Own;
                }
            }
        }

        private static GameObject GamePiece(ObjectDB db, string name)
        {
            if (name == null)
            {
                return null;
            }
            if (GamePieces.TryGetValue(name, out GameObject found) && found != null)
            {
                return found;
            }
            if (db == null || db.m_terrainOps == null)
            {
                return null;
            }
            foreach (TerrainOp op in db.m_terrainOps)
            {
                if (op != null && op.gameObject.name == name)
                {
                    GamePieces[name] = op.gameObject;
                    return op.gameObject;
                }
            }
            return null;
        }

        private static void Apply(EffectList list, bool dust)
        {
            if (list?.m_effectPrefabs == null)
            {
                return;
            }
            foreach (EffectList.EffectData data in list.m_effectPrefabs)
            {
                if (data == null || !IsDust(data.m_prefab))
                {
                    continue;
                }
                // An entry seen for the first time, or changed since by another mod: its flag now is its own (review round 1:
                // otherwise the two would flip it back and forth every frame).
                if (!Flags.TryGetValue(data, out (bool Own, bool Written) flags) || data.m_enabled != flags.Written)
                {
                    flags = (data.m_enabled, data.m_enabled);
                }
                bool wanted = flags.Own && dust;
                if (data.m_enabled != wanted)
                {
                    data.m_enabled = wanted;
                }
                if (flags.Written != wanted || !Flags.ContainsKey(data))
                {
                    Flags[data] = (flags.Own, wanted);
                }
            }
        }

        /// <summary>A visual effect: particles and no sound (the game's `vfx_…` prefabs; its `sfx_…` ones play a sound
        /// through the game's own sound component).</summary>
        private static bool IsDust(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }
            if (!Visual.TryGetValue(prefab, out bool visual))
            {
                visual = !prefab.name.StartsWith("sfx_", System.StringComparison.OrdinalIgnoreCase) &&
                         prefab.GetComponentInChildren<ParticleSystem>(true) != null &&
                         prefab.GetComponentInChildren<ZSFX>(true) == null;
                Visual[prefab] = visual;
            }
            return visual;
        }

        // EffectData's own shallow copy (object.MemberwiseClone): every field is a reference to a prefab or a plain value.
        private static readonly MethodInfo CloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>A copy of an effect list with entries of its own, so switching one tool's dust off never reaches
        /// another's (Lower ground plays the pickaxe dig's effects: a shared list would silence the pickaxe too).</summary>
        internal static EffectList Copy(EffectList list)
        {
            var copy = new EffectList();
            if (list?.m_effectPrefabs == null)
            {
                return copy;
            }
            var entries = new EffectList.EffectData[list.m_effectPrefabs.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                EffectList.EffectData data = list.m_effectPrefabs[i];
                entries[i] = data != null ? (EffectList.EffectData)CloneMethod.Invoke(data, null) : null;
            }
            copy.m_effectPrefabs = entries;
            return copy;
        }
    }
}
