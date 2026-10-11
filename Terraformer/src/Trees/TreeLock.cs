using System;
using System.Collections.Generic;
using UnityEngine;

namespace Terraformer.Trees
{
    /// <summary>
    /// The lock: a sapling planted while the grown tree shows carries the pick in its own record, and grows into exactly
    /// that tree, size and facing.
    /// - **Writing it:** his click (Player.PlacePiece, with the preview showing) arms a <see cref="LockWindow"/> with the
    ///   pick; each fresh sapling of that prefab, as it wakes on his game (Plant.Awake), takes it into its record. That
    ///   reaches the extra saplings PlantEasily places a few a frame after the click as well as the game's own one.
    /// - **Reading it:** the sapling grows in Plant.Grow, on whichever game owns it then; there Terraformer's transpiler
    ///   (TreeGrow) calls <see cref="GrowTree"/>, <see cref="GrowTurn"/> and <see cref="GrowSize"/> in place of the game's
    ///   three random draws. Each gives the locked value, or makes the game's own draw with the game's own arguments where
    ///   the record holds none, so a sapling planted without the preview grows exactly as before. They read the record
    ///   only, so a locked sapling grows locked even while Terraformer is switched off (plan decision 9), and never throw
    ///   into the game's code.
    /// </summary>
    internal static class TreeLock
    {
        private static readonly LockWindow Window = new LockWindow();

        private static bool warnedGrow;

        /// <summary>A sapling's record (its ZDO).</summary>
        internal sealed class ZdoRecord : IRecord
        {
            private readonly ZDO zdo;

            internal ZdoRecord(ZDO zdo)
            {
                this.zdo = zdo;
            }

            public bool TryInt(int key, out int value) => zdo.GetInt(key, out value);

            public bool TryFloat(int key, out float value) => zdo.GetFloat(key, out value);

            public void SetInt(int key, int value) => zdo.Set(key, value);

            public void SetFloat(int key, float value) => zdo.Set(key, value);
        }

        /// <summary>His click is about to place this piece (a prefix on Player.PlacePiece): a sapling planted with the preview
        /// showing arms the window with the pick on show ("Lock the tree it becomes" on); a sapling planted without it shuts
        /// it; any other piece leaves it alone (TreeRules.ClickFor). The window isn't shut when he puts the cultivator away
        /// or picks another piece: PlantEasily may still be planting the click's grid, and only a fresh sapling of the
        /// clicked prefab can take the pick anyway.</summary>
        internal static void BeforePlacing(Piece piece, bool previewing)
        {
            switch (TreeRules.ClickFor(piece != null && TreeControls.SaplingOf(piece) != null, previewing))
            {
                case ClickAction.Arm:
                    Window.Arm(piece.gameObject.name.GetStableHashCode(), TreeControls.PickToWrite(piece), Time.unscaledTime, Time.frameCount);
                    break;
                case ClickAction.Disarm:
                    Window.Disarm();
                    break;
            }
        }

        /// <summary>A click's pick is waiting for its saplings (so a waking plant is worth a look).</summary>
        internal static bool Waiting => Window.Open(Time.unscaledTime, Time.frameCount);

        /// <summary>The window shut once its saplings can't be coming any more (every frame, from TreeControls.Tick), so the
        /// fresh test stops running for every plant that loads.</summary>
        internal static void Expire() => Window.Expire(Time.unscaledTime, Time.frameCount);

        internal static void Forget() => Window.Disarm();

        /// <summary>Whether a sapling waking now is a fresh one this game just placed and owns (a prefix on Plant.Awake,
        /// before the game writes its planting time): only then can it take a pick. Asked only while the window is armed,
        /// so the many saplings and crops that load with a farm cost one test each.</summary>
        internal static bool IsFresh(Plant plant)
        {
            if (!Window.Armed || plant == null)
            {
                return false;
            }
            ZNetView view = plant.GetComponent<ZNetView>();
            ZDO zdo = view != null ? view.GetZDO() : null;
            return zdo != null && zdo.IsOwner() && zdo.GetLong(ZDOVars.s_plantTime, 0L) == 0L;
        }

        /// <summary>A fresh sapling has woken (a postfix on Plant.Awake): if the window holds a pick for its prefab, the pick
        /// goes into its record. The record is new and his, so the server and every other player get it with the sapling.</summary>
        internal static void AfterWaking(Plant plant, bool fresh)
        {
            if (!fresh || plant == null)
            {
                return;
            }
            ZNetView view = plant.GetComponent<ZNetView>();
            ZDO zdo = view != null ? view.GetZDO() : null;
            if (zdo == null || !zdo.IsOwner())
            {
                return;
            }
            var record = new ZdoRecord(zdo);
            Pick? pick = Window.Take(zdo.GetPrefab(), fresh: true, TreeRules.IsLocked(record), Time.unscaledTime, Time.frameCount);
            if (pick.HasValue)
            {
                TreeRules.Write(record, pick.Value);
            }
        }

        // The three draws, in Plant.Grow's order. Each is called from the game's own code with the game's own arguments and
        // the plant (the transpiler pushes it), so it must never throw: a fault falls back to the game's draw, said once.

        internal static int GrowTree(int min, int max, Plant plant)
        {
            try
            {
                Pick pick = PickOf(plant);
                if (pick.Tree.HasValue)
                {
                    return TreeRules.ChooseTree(min, max, pick, NamesOf(plant), UnityEngine.Random.Range);
                }
            }
            catch (Exception error)
            {
                WarnGrow(error);
            }
            return UnityEngine.Random.Range(min, max);
        }

        internal static float GrowTurn(float min, float max, Plant plant)
        {
            try
            {
                Pick pick = PickOf(plant);
                if (pick.Turn.HasValue)
                {
                    return TreeRules.ChooseTurn(min, max, pick, UnityEngine.Random.Range);
                }
            }
            catch (Exception error)
            {
                WarnGrow(error);
            }
            return UnityEngine.Random.Range(min, max);
        }

        internal static float GrowSize(float min, float max, Plant plant)
        {
            try
            {
                Pick pick = PickOf(plant);
                if (pick.Size.HasValue)
                {
                    return TreeRules.ChooseSize(min, max, pick, UnityEngine.Random.Range);
                }
            }
            catch (Exception error)
            {
                WarnGrow(error);
            }
            return UnityEngine.Random.Range(min, max);
        }

        private static Pick PickOf(Plant plant)
        {
            ZNetView view = plant != null ? plant.GetComponent<ZNetView>() : null;
            ZDO zdo = view != null ? view.GetZDO() : null;
            return zdo != null ? TreeRules.Read(new ZdoRecord(zdo)) : default;
        }

        private static IList<string> NamesOf(Plant plant)
        {
            GameObject[] prefabs = plant.m_grownPrefabs;
            var names = new string[prefabs != null ? prefabs.Length : 0];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = prefabs[i] != null ? prefabs[i].name : null;
            }
            return names;
        }

        private static void WarnGrow(Exception error)
        {
            if (warnedGrow)
            {
                return;
            }
            warnedGrow = true;
            try
            {
                Module.Log.LogWarning($"A sapling's locked tree couldn't be read as it grew, so the game picked it: {error}");
            }
            catch (Exception)
            {
                // No log to say it in; the sapling still grows as the game has it.
            }
        }
    }
}
