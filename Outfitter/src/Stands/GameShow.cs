using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Outfitter.Loadouts;
using UnityEngine;

namespace Outfitter.Stands
{
    /// <summary>
    /// Draws what a stand's loadout lacks (StandShow decides what) on the game's own armour stand, in this game only (job
    /// 30, Session 3; A46's decision 48). Read from the game 1.0.17 and the stand's prefab on 2026-10-06:
    /// - The stand's figure is a VisEquipment (on its child "Player Pose") whose network view is the stand's own
    ///   (m_nViewOverride), so it draws, every frame (UpdateEquipmentVisuals), what the stand's record says under the
    ///   game's equipment keys ("chest item", "shoulder item" ...), which the stand's owner writes from the stand's places
    ///   (ArmorStand.SetVisualItem calls VisEquipment.SetItem, which writes the record as its owner). A change made through
    ///   SetVisualItem would therefore be written into the stand's record on its owner, and seen by every player (with or
    ///   without the pack), and change nothing on any other computer.
    /// - ArmorStand.UseItem picks the place E takes from by its drawn item (m_visualHash): one drawn there without being in
    ///   the record would make E do nothing at all, on every place of the stand (they share one switch).
    /// So nothing of the stand or its record is changed: a prefix on each of the figure's own drawing steps (SetChestEquipped,
    /// SetLegEquipped, SetHelmetEquipped, SetShoulderEquipped, SetUtilityEquipped, SetBackEquipped: what UpdateEquipmentVisuals
    /// hands each, read from the record) puts the shown piece in where the record holds none for that slot. The game's own
    /// E, its "take" line (GetNrOfAttachedItems), a hand attach (HaveAttachment), a destroyed stand's drop (OnDestroyed) and
    /// every other player go on reading the record. The stand's support pieces (its arms, under some chest pieces) follow
    /// through a postfix on ArmorStand.UpdateSupports, run again when what's shown changes. Worked out again only when the
    /// stand's record, his list, a setting, he or the switch changes (StandShow.Cached), so a frame costs a few comparisons;
    /// any of those changing redraws the stand at the next frame. An error, or a patch that can't be applied, turns only
    /// this drawing off (Stands.ShowLost); switched off (the stands, or Outfitter), stands show what they hold at the next
    /// frame.
    /// </summary>
    internal static class GameShow
    {
        // The game's VisSlot values (HandLeft ... Hair): the stand's places use Chest, Legs, Helmet, BackLeft, BackRight,
        // Shoulder and Utility.
        private const int SlotCount = 11;

        // What one stand's figure draws besides its record: by VisSlot, the shown item's hash (0: none), style and quality,
        // and the $ names, for the supports.
        private sealed class Drawn
        {
            internal ArmorStand Stand;
            internal GameStand Counted;
            internal readonly int[] Hash = new int[SlotCount];
            internal readonly int[] Variant = new int[SlotCount];
            internal readonly int[] Quality = new int[SlotCount];
            internal readonly List<string> Names = new List<string>();
            internal bool Any;

            /// <summary>What StandShow last gave for it, as drawn (a new answer, a new instance: drawn again).</summary>
            internal Dictionary<int, LoadoutItem> Source;
        }

        // Each armour stand figure seen, with its stand (null: not the hammer's stand: a mannequin, another mod's).
        private static readonly Dictionary<VisEquipment, Drawn> Known = new Dictionary<VisEquipment, Drawn>();

        // The figure being drawn now, between UpdateEquipmentVisuals' prefix and its drawing steps; null for none to change.
        private static Drawn drawing;

        private static MethodInfo updateSupports;

        /// <summary>The prefix on VisEquipment.UpdateEquipmentVisuals: for an armour stand's figure, what to draw besides its
        /// record this frame. Never throws (an error turns this drawing off).</summary>
        internal static void Begin(VisEquipment figure)
        {
            drawing = null;
            if (figure == null || !figure.m_isArmorStand || Stands.ShowLost)
            {
                return;   // players and creatures cost one field's look
            }
            try
            {
                if (!Known.TryGetValue(figure, out Drawn drawn))
                {
                    if (Known.Count > 256)
                    {
                        Prune();   // destroyed figures linger as keys (a stand unloaded, the hammer's ghost): drop only those
                    }
                    ArmorStand stand = figure.GetComponentInParent<ArmorStand>();
                    bool counts = stand != null && stand.m_visEquipment == figure && Utils.GetPrefabName(stand.gameObject) == GameStand.Prefab;
                    Known[figure] = drawn = new Drawn { Stand = counts ? stand : null };
                }
                if (drawn.Stand == null)
                {
                    return;
                }
                if (drawn.Counted == null || !drawn.Counted.Valid)
                {
                    drawn.Counted = GameStand.Of(drawn.Stand);   // null until its record is in: asked again next frame
                }
                if (drawn.Counted == null)
                {
                    if (drawn.Any)
                    {
                        Fill(drawn, null);
                    }
                    return;
                }
                // Drawn again whenever StandShow's answer is another than the one drawn (review round 1 of job 30's Session 3,
                // M2: a figure seen afresh, or StandShow's own memory started again, draws at once).
                Dictionary<int, LoadoutItem> shown = StandShow.Cached(drawn.Counted, drawn.Counted.Revision, out bool _);
                if (!ReferenceEquals(shown, drawn.Source))
                {
                    Fill(drawn, shown);
                    drawn.Source = shown;
                }
                drawing = drawn.Any && !Stands.ShowLost ? drawn : null;
            }
            catch (Exception error)
            {
                drawing = null;
                Stands.ShowFailed(error);
            }
        }

        // Drops the figures that are gone (Unity's destroyed objects compare equal to null), keeping every stand still loaded
        // as it's drawn.
        private static void Prune()
        {
            var gone = new List<VisEquipment>();
            foreach (KeyValuePair<VisEquipment, Drawn> each in Known)
            {
                if (each.Key == null)
                {
                    gone.Add(each.Key);
                }
            }
            foreach (VisEquipment figure in gone)
            {
                Known.Remove(figure);
            }
        }

        /// <summary>The postfix on UpdateEquipmentVisuals: its drawing steps are over.</summary>
        internal static void End() => drawing = null;

        /// <summary>A drawing step of one slot (chest, legs, helmet, belt): the shown piece where the record holds none.</summary>
        internal static void Body(VisSlot slot, ref int hash)
        {
            Drawn drawn = drawing;
            if (drawn != null && hash == 0 && drawn.Hash[(int)slot] != 0)
            {
                hash = drawn.Hash[(int)slot];
            }
        }

        /// <summary>A drawing step with a style and a quality (the cape).</summary>
        internal static void Styled(VisSlot slot, ref int hash, ref int variant, ref int quality)
        {
            Drawn drawn = drawing;
            int at = (int)slot;
            if (drawn != null && hash == 0 && drawn.Hash[at] != 0)
            {
                hash = drawn.Hash[at];
                variant = drawn.Variant[at];
                quality = drawn.Quality[at];
            }
        }

        /// <summary>The back's drawing step: the shield (left) and the weapon (right), each where the record holds none.</summary>
        internal static void Back(ref int left, ref int right, ref int leftVariant, ref int leftQuality, ref int rightQuality)
        {
            Drawn drawn = drawing;
            if (drawn == null)
            {
                return;
            }
            const int l = (int)VisSlot.BackLeft, r = (int)VisSlot.BackRight;
            if (left == 0 && drawn.Hash[l] != 0)
            {
                left = drawn.Hash[l];
                leftVariant = drawn.Variant[l];
                leftQuality = drawn.Quality[l];
            }
            if (right == 0 && drawn.Hash[r] != 0)
            {
                right = drawn.Hash[r];
                rightQuality = drawn.Quality[r];
            }
        }

        /// <summary>The postfix on ArmorStand.UpdateSupports: after the game set the supports by what the stand holds, those
        /// a shown piece needs too (the stand's arms under a bronze or iron chest piece, say). Never throws.</summary>
        internal static void Supports(ArmorStand stand)
        {
            if (stand == null || Stands.ShowLost || stand.m_visEquipment == null || !Known.TryGetValue(stand.m_visEquipment, out Drawn drawn) ||
                !drawn.Any || stand.m_supports == null)
            {
                return;
            }
            try
            {
                foreach (ArmorStand.ArmorStandSupport support in stand.m_supports)
                {
                    bool needed = false;
                    foreach (ItemDrop item in support.m_items)
                    {
                        needed |= item != null && drawn.Names.Contains(item.m_itemData.m_shared.m_name);
                    }
                    if (!needed)
                    {
                        continue;
                    }
                    foreach (GameObject part in support.m_supports)
                    {
                        if (part != null)
                        {
                            part.SetActive(true);
                        }
                    }
                }
            }
            catch (Exception error)
            {
                Stands.ShowFailed(error);
            }
        }

        // What to draw for each slot, from StandShow's places (null: nothing), as the game draws an item it hangs: the
        // prefab's hash, the loadout's style, the prefab's own quality (ArmorStand.SetVisualItem). When the shown pieces'
        // names change, the stand's supports are set again (the game's UpdateSupports, then ours).
        private static void Fill(Drawn drawn, Dictionary<int, LoadoutItem> shown)
        {
            var names = new List<string>();
            Array.Clear(drawn.Hash, 0, SlotCount);
            Array.Clear(drawn.Variant, 0, SlotCount);
            Array.Clear(drawn.Quality, 0, SlotCount);
            drawn.Any = false;
            if (shown != null && ObjectDB.instance != null)
            {
                foreach (KeyValuePair<int, LoadoutItem> each in shown)
                {
                    if (each.Key < 0 || each.Key >= drawn.Stand.m_slots.Count)
                    {
                        continue;
                    }
                    int slot = (int)drawn.Stand.m_slots[each.Key].m_slot;
                    GameObject prefab = ObjectDB.instance.GetItemPrefab(each.Value.Prefab);
                    ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                    if (slot < 0 || slot >= SlotCount || drawn.Hash[slot] != 0 || drop == null)
                    {
                        continue;
                    }
                    drawn.Hash[slot] = prefab.name.GetStableHashCode();
                    drawn.Variant[slot] = each.Value.Variant;
                    drawn.Quality[slot] = drop.m_itemData.m_quality;
                    names.Add(drop.m_itemData.m_shared.m_name);
                    drawn.Any = true;
                }
            }
            bool same = names.Count == drawn.Names.Count && names.TrueForAll(drawn.Names.Contains);
            drawn.Names.Clear();
            drawn.Names.AddRange(names);
            if (!same)
            {
                Resupport(drawn.Stand);
            }
        }

        // The game's own UpdateSupports (private), once, so the supports follow what is shown now (our postfix adds the
        // shown pieces').
        private static void Resupport(ArmorStand stand)
        {
            if (stand == null)
            {
                return;
            }
            updateSupports = updateSupports ?? AccessTools.Method(typeof(ArmorStand), "UpdateSupports");
            updateSupports?.Invoke(stand, null);
        }

        /// <summary>The undo step: every stand drawn with shown pieces gets its supports back as the game set them, and
        /// nothing more is drawn until it's worked out again (its figure draws what its record says at its next frame).
        /// Never throws.</summary>
        internal static void Forget()
        {
            drawing = null;
            var stands = new List<ArmorStand>();
            foreach (Drawn drawn in Known.Values)
            {
                if (drawn.Any && drawn.Stand != null)
                {
                    stands.Add(drawn.Stand);
                }
            }
            Known.Clear();
            foreach (ArmorStand stand in stands)
            {
                try
                {
                    Resupport(stand);
                }
                catch (Exception)
                {
                    // Each stand on its own: one that's gone mustn't keep the others' supports as they were.
                }
            }
        }
    }
}
