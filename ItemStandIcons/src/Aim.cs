using System.Collections.Generic;
using QoLMods.ItemStands;
using UnityEngine;

namespace ItemStandIcons
{
    /// <summary>Which places "Only while aiming at a stand" holds back.</summary>
    internal enum AimPlaces
    {
        /// <summary>His inventory and the hotbar: the items he'd put on the stand.</summary>
        YourItems,

        /// <summary>Every place the marks show.</summary>
        EveryPlace,
    }

    /// <summary>What aiming at a stand shows.</summary>
    internal enum AimMarks
    {
        /// <summary>At an item stand the arrows, at an armor stand the mannequin.</summary>
        ThatStandsMarks,

        /// <summary>Only the items the stand aimed at takes (on an armor stand, the slots behind what he aims at).</summary>
        WhatFitsIt,

        /// <summary>Every mark, whichever stand.</summary>
        EveryMark,
    }

    /// <summary>What the crosshair is on, as far as the marks care.</summary>
    internal enum AimTarget
    {
        None,
        ItemStand,
        ArmorStand,
    }

    /// <summary>
    /// "Only while aiming at a stand" (his 0.16.0 ask): what he aims at, by the game's own test for showing a stand's
    /// "Attach item" or "Take" line (Hud.UpdateCrosshair): the player's hover object, and the first Hoverable above
    /// it, with no text window open. An item stand is that Hoverable itself; an armor stand's is one of its switches (the
    /// game's own armor stand has one, "block body", for all its slots), and the pose switch, which shows no attach line,
    /// doesn't count. The stand must be one the rule counts (so never a boss stone), and his to use (the game shows "no
    /// access" otherwise). The game finds the player's hover every frame, the inventory open or not, and the camera
    /// stops turning while it's open, so he aims, then opens it (walking still moves the aim). Read once a frame; whether
    /// the stand aimed at counts is kept until he aims at another or the rule changes.
    /// </summary>
    internal static class Aim
    {
        private static int frame = -1;
        private static AimTarget target;
        private static ItemStand stand;
        private static ArmorStand armor;
        private static Switch armorSwitch;

        /// <summary>The stand or switch the answers below are for, and the rule they were asked by.</summary>
        private static object fitsFor;
        private static StandRule fitsRule;
        private static readonly Dictionary<GameObject, bool> Fits = new Dictionary<GameObject, bool>();

        /// <summary>
        /// The mark an item shows, with aiming as the settings say: <paramref name="fit"/> as it is while "Only while aiming
        /// at a stand" is off or doesn't cover <paramref name="yourItems"/>'s place; else only while he aims at a stand,
        /// and then as "Aiming shows" says. <paramref name="prefab"/> is the item's, for "WhatFitsIt".
        /// </summary>
        internal static Fit Filter(Fit fit, GameObject prefab, bool yourItems)
        {
            if (fit == Fit.None || !Settings.WhileAiming.Value)
            {
                return fit;
            }
            bool covered = yourItems || Settings.AimCovers.Value == AimPlaces.EveryPlace;
            if (!covered)
            {
                return fit;
            }
            StandRule rule = Settings.CurrentRule;
            Read(rule);
            AimMarks marks = Settings.AimShows.Value;
            bool takes = target != AimTarget.None && marks == AimMarks.WhatFitsIt && TakesAimed(prefab, rule);
            return Choose(fit, true, true, target, marks, takes);
        }

        /// <summary>
        /// The rule on plain facts (the harness checks it): with aiming off or not covering the place, the fit as it is;
        /// aiming at nothing, no mark; else every mark, that stand's kind of mark, or that kind only if the stand aimed at
        /// takes the item (<paramref name="takesIt"/>).
        /// </summary>
        internal static Fit Choose(Fit fit, bool whileAiming, bool covered, AimTarget aimed, AimMarks marks, bool takesIt)
        {
            if (!whileAiming || !covered || fit == Fit.None)
            {
                return fit;
            }
            if (aimed == AimTarget.None)
            {
                return Fit.None;
            }
            if (marks == AimMarks.EveryMark)
            {
                return fit;
            }
            Fit kind = fit & (aimed == AimTarget.ItemStand ? Fit.Both : Fit.Armor);
            if (kind == Fit.None || marks == AimMarks.ThatStandsMarks)
            {
                return kind;
            }
            return takesIt ? kind : Fit.None;
        }

        /// <summary>The stand whose counting was last asked, its answer, and the rule it was asked by.</summary>
        private static Component countedFor;
        private static bool countedAnswer;
        private static StandRule countedRule;

        // Whether a stand in the world counts, asked again only for another stand or rule (the answer reads its piece's
        // name, a string made each time).
        private static bool Counted(Component piece, StandRule rule)
        {
            if (!ReferenceEquals(piece, countedFor) || !rule.SameAs(countedRule))
            {
                countedFor = piece;
                countedRule = rule;
                countedAnswer = piece is ItemStand itemStand ? StandFit.IsCounted(itemStand, rule) : StandFit.IsCounted((ArmorStand)piece, rule);
            }
            return countedAnswer;
        }

        private static void Read(StandRule rule)
        {
            int now = Time.frameCount;
            if (now == frame)
            {
                return;
            }
            frame = now;
            target = AimTarget.None;
            stand = null;
            armor = null;
            armorSwitch = null;
            Player player = Player.m_localPlayer;
            GameObject hover = player != null ? player.GetHoverObject() : null;
            if (hover == null || (TextViewer.instance != null && TextViewer.instance.IsVisible()))
            {
                return;   // the game shows no line under the crosshair while a text window is open
            }
            Hoverable hoverable = hover.GetComponentInParent<Hoverable>();
            if (hoverable is ItemStand aimedStand)
            {
                if (Counted(aimedStand, rule) && Usable(aimedStand))
                {
                    target = AimTarget.ItemStand;
                    stand = aimedStand;
                }
            }
            else if (hoverable is Switch aimedSwitch)
            {
                ArmorStand owner = aimedSwitch.GetComponentInParent<ArmorStand>();
                if (owner != null && HoldsSlots(owner, aimedSwitch) && Counted(owner, rule) && Usable(owner))
                {
                    target = AimTarget.ArmorStand;
                    armor = owner;
                    armorSwitch = aimedSwitch;
                }
            }
        }

        // The game shows a stand's attach line only to someone who may use it (a ward's "no access" otherwise).
        private static bool Usable(Component piece) => PrivateArea.CheckAccess(piece.transform.position, 0f, false);

        // A switch that stands for slots (the pose switch stands for none).
        private static bool HoldsSlots(ArmorStand owner, Switch aimed)
        {
            if (owner.m_slots == null)
            {
                return false;
            }
            foreach (ArmorStand.ArmorStandSlot slot in owner.m_slots)
            {
                if (slot != null && slot.m_switch == aimed)
                {
                    return true;
                }
            }
            return false;
        }

        // Whether the stand aimed at takes this item: an item stand by its own check, an armor stand by any slot behind the
        // switch aimed at (the game tries them in order). Kept per item until he aims at something else.
        private static bool TakesAimed(GameObject prefab, StandRule rule)
        {
            object now = target == AimTarget.ItemStand ? stand : (object)armorSwitch;
            if (!ReferenceEquals(now, fitsFor) || !rule.SameAs(fitsRule))
            {
                Fits.Clear();
                fitsFor = now;
                fitsRule = rule;
            }
            if (prefab == null)
            {
                return false;
            }
            if (!Fits.TryGetValue(prefab, out bool takes))
            {
                takes = target == AimTarget.ItemStand ? StandFit.TakenBy(stand, prefab, rule) : ArmorTakes(prefab, rule);
                Fits[prefab] = takes;
            }
            return takes;
        }

        private static bool ArmorTakes(GameObject prefab, StandRule rule)
        {
            foreach (ArmorStand.ArmorStandSlot slot in armor.m_slots)
            {
                if (slot != null && slot.m_switch == armorSwitch && StandFit.TakenBy(armor, slot, prefab, rule))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Forgets what he aimed at (the module switched off).</summary>
        internal static void Forget()
        {
            frame = -1;
            target = AimTarget.None;
            stand = null;
            armor = null;
            armorSwitch = null;
            fitsFor = null;
            fitsRule = null;
            Fits.Clear();
            countedFor = null;
            countedRule = null;
        }
    }
}
