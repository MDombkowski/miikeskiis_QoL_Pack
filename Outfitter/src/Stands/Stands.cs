using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Outfitter.Loadouts;
using UnityEngine;

namespace Outfitter.Stands
{
    /// <summary>
    /// Loadout Stands (card A46, runbook job 30), built inside Outfitter at his pick (2026-10-05): the game's own armour
    /// stand, marked as a loadout's stand, wears that loadout while he doesn't. Left Alt + E at it puts the loadout on (the
    /// stand's gear, the rest from the chests near it, through Outfitter's own equip) and sends what he wore home; the same
    /// key again hangs it back; Left Alt + Left Shift + E saves what he carries and wears to it. Everything of it lives under
    /// src\Stands; Outfitter's own files call the four hooks below (Settings.Bind, Module.Resolve, Module.Update,
    /// Module.UndoSteps). The plan: the vault's working\2026-10-06_loadout-stands-plan.md. 0.4.0 (job 30's Session 3,
    /// working\2026-10-06_outfitter-0-4-0-feedback-plan.md): personal stands (StandPersonal), stands lending gear and the
    /// window's Equip through a stand (StandJob), and a stand drawn wearing what its loadout lacks (StandShow, GameShow).
    ///
    /// The engine's side comes through the seams below (the stand he points at, the stands nearby, him, the pop-up, the
    /// messages), so the scratch harness runs the rest without the engine.
    /// </summary>
    internal static class Stands
    {
        /// <summary>Binds the stands' settings in their own folded section, with the orders kept for them
        /// (Settings.StandsTop down to Settings.StandsBottom). Called once, after Outfitter's own sections.</summary>
        internal static void Bind(SettingsBinder binder)
        {
            StandSettings.Bind(binder);
            StandSettings.On.SettingChanged += (sender, args) => StandHover.Forget();
            StandSettings.Show.SettingChanged += (sender, args) => StandHover.Forget();
            StandSettings.HoverPersonal.SettingChanged += (sender, args) => StandHover.Forget();
        }

        /// <summary>Looks up what the stands need from the game at start. They need nothing Outfitter itself can't do
        /// without: a patch of theirs that can't be applied loses only the stands (its Cleanup, <see cref="Patching"/>).</summary>
        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
        }

        /// <summary>Every frame while Outfitter runs, also while it's switched off (so the stands can follow the switch).</summary>
        internal static void Update()
        {
            bool active = Active;
            if (!active)
            {
                if (wasActive)
                {
                    SwitchedOff();
                }
                wasActive = false;
                return;
            }
            wasActive = true;
            StandClaims.Tick();
            StandJob.Tick();
            StandInput.Update();
        }

        private static bool wasActive;

        // Switched off (the stands' own switch, or Outfitter's): the job under way ends where it stands, holds end, and
        // marked stands are ordinary stands again (nothing of theirs runs; their gear stays on them).
        private static void SwitchedOff()
        {
            StandJob.SwitchedOff();
            StandClaims.Forget();
            StandHover.Forget();
        }

        /// <summary>What to take back after an error switched Outfitter off (MODULES.md rule 7), each step on its own: the
        /// job dropped (each move finished inside its own frame, so every item is on a stand or with him), the holds and
        /// the claim under way forgotten, the hover lines and read codes forgotten.</summary>
        internal static IEnumerable<Action> UndoSteps => new Action[]
        {
            StandJob.Forget,
            StandClaims.Forget,
            StandHover.Forget,
            StandLoadouts.Forget,
            StandInput.Forget,
            StandShow.Forget,
            GameShow.Forget,
        };

        /// <summary>True while the stands work: Outfitter on, their own switch on, and none of their patches lost.</summary>
        internal static bool Active => Module.IsOn && StandSettings.On != null && StandSettings.On.Value && !Lost;

        /// <summary>A patch the stands can't work safely without couldn't be applied: off for the session.</summary>
        internal static bool Lost { get; private set; }

        /// <summary>The hover text's patch couldn't be applied, or failed while drawing: no hover lines this session (the rest
        /// works).</summary>
        internal static bool HoverLost { get; private set; }

        /// <summary>An error while working out a stand's hover lines: they're off for the session, said once; nothing else
        /// of Outfitter is touched (review round 1, m6). Never throws.</summary>
        internal static void HoverFailed(Exception error)
        {
            if (HoverLost)
            {
                return;
            }
            HoverLost = true;
            try
            {
                Module.Warn($"Loadout Stands' hover lines stopped after an error (the rest of Outfitter goes on): {error}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't reach the game.
            }
        }

        /// <summary>A patch that draws what a stand's loadout lacks (GameShow) couldn't be applied, or failed while drawing:
        /// stands show only what they hold this session (the rest works).</summary>
        internal static bool ShowLost { get; private set; }

        /// <summary>An error while drawing what a stand's loadout lacks: that drawing is off for the session, said once;
        /// every stand shows what it holds again at its next redraw, and nothing else of Outfitter is touched. Never
        /// throws.</summary>
        internal static void ShowFailed(Exception error)
        {
            if (ShowLost)
            {
                return;
            }
            ShowLost = true;
            try
            {
                GameShow.Forget();   // the supports as the game set them, first: a log write that throws mustn't skip it (n10)
            }
            catch (Exception)
            {
                // Each step on its own.
            }
            try
            {
                Module.Warn($"Loadout Stands stopped drawing what a stand's loadout lacks after an error (the rest of Outfitter goes on): {error}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't reach the game.
            }
        }

        /// <summary>The Cleanup of a patch class that draws what a stand's loadout lacks: only that drawing is lost if it
        /// can't be applied (<see cref="ShowLost"/>), as for the hover lines.</summary>
        internal static Exception PatchingShow(string what, MethodBase original, Exception error, Harmony harmony)
        {
            if (error == null)
            {
                return null;
            }
            ShowLost = true;
            return Unpatched(what, "marked stands show only what they hold", original, error, harmony);
        }

        /// <summary>A stand patch class's Cleanup: nothing when its patch went on; otherwise the stands (or only their hover
        /// lines) are lost for the session, with a warning, and the class's record is taken back off the method. Always
        /// null, so the kit goes on to the next class and the rest of Outfitter isn't touched. Never throws.</summary>
        internal static Exception Patching(string what, bool hoverOnly, MethodBase original, Exception error, Harmony harmony)
        {
            if (error == null)
            {
                return null;
            }
            if (hoverOnly)
            {
                HoverLost = true;
            }
            else
            {
                Lost = true;
            }
            return Unpatched(what, hoverOnly ? "marked stands show no lines of their own" : "Loadout Stands is off", original, error, harmony);
        }

        // The class's record taken back off the method, and the warning; always null. Never throws.
        private static Exception Unpatched(string what, string lost, MethodBase original, Exception error, Harmony harmony)
        {
            try
            {
                if (original != null && harmony != null)
                {
                    harmony.Unpatch(original, HarmonyPatchType.All, harmony.Id);
                }
            }
            catch (Exception)
            {
                // Taking the record back can fail the same way the patch did; the stands are lost either way.
            }
            try
            {
                Module.Warn($"Loadout Stands couldn't change {what} (a game update, or another mod's patch on it failing first): " +
                            lost + $" until the game is started again. The rest of Outfitter is not affected.\n{error}");
            }
            catch (Exception)
            {
                // A log listener's own error mustn't stop the kit's patching.
            }
            return null;
        }

        // ---------- the seams (the engine's side) ----------

        /// <summary>The clock (the game's time); the harness sets another.</summary>
        internal static Func<float> Clock = () => Time.time;

        /// <summary>The armour stand he points at, or null (Player.GetHoverObject); the harness sets another.</summary>
        internal static Func<IStand> Hovered = GameHovered;

        /// <summary>The loaded armour stands that count, within <c>range</c> metres of a point, nearest first; the harness
        /// sets another.</summary>
        internal static Func<Vector3, float, List<IStand>> Nearby = GameNearby;

        /// <summary>His player ID (the character's), or 0; the harness sets another.</summary>
        internal static Func<long> Me = () => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerID() : 0L;

        /// <summary>His character's name; the harness sets another.</summary>
        internal static Func<string> MyName = () => Player.m_localPlayer != null ? Player.m_localPlayer.GetPlayerName() : string.Empty;

        /// <summary>Tells him something: at the centre (a refusal, as the game says "can't attach") or at the top left (what
        /// happened, as Outfitter's equip says it); the harness sets another.</summary>
        internal static Action<string, bool> Say = (text, centre) =>
        {
            Player player = Player.m_localPlayer;
            if (player != null && !string.IsNullOrEmpty(text))
            {
                player.Message(centre ? MessageHud.MessageType.Center : MessageHud.MessageType.TopLeft, text);
            }
        };

        /// <summary>Asks a question with Outfitter's pop-up (the question, the yes, the no, the answer); the harness sets
        /// another.</summary>
        internal static Func<string, string, string, Action<bool>, int> Ask = Window.Popup.Ask;

        /// <summary>Asks for a name with Outfitter's pop-up (the heading, the name filled in, the answer: not called when he
        /// cancels), returning the box's number; the harness sets another.</summary>
        internal static Func<string, string, Action<string>, int> Name = Window.Popup.Name;

        /// <summary>Asks for a name that may be left empty (a stand's own: empty gives it the game's name back): the
        /// heading, the name filled in, the answer (not called when he cancels), and the words the empty box shows;
        /// returns the box's number. The harness sets another.</summary>
        internal static Func<string, string, Action<string>, string, int> NameOrEmpty = Window.Popup.Name;

        /// <summary>Takes the stands' question or name box down unanswered (a "no"), by its number, when the stands are
        /// switched off while it still shows (Outfitter's pop-up's CloseIf: a box he cancelled, or one another question
        /// replaced, is left alone, so Outfitter's own question never goes down with it: harness area B); the harness sets
        /// another.</summary>
        internal static Action<int> Dismiss = Window.Popup.CloseIf;

        /// <summary>What he carries and wears as a loadout, by "Saving to a stand takes" (Outfitter's Capture); the harness
        /// sets another.</summary>
        internal static Func<StandSaves, string, Loadout> CaptureFor = GameCapture;

        /// <summary>The game's own name for a stand ("Armor stand", in his language), for one he hasn't named; the harness
        /// sets another.</summary>
        internal static Func<IStand, string> GameName = stand =>
            stand is GameStand game && game.Stand != null && Localization.instance != null ? Localization.instance.Localize(game.Stand.m_name) : "Armor stand";

        /// <summary>Whether the stands' keys may be read now: him there, alive, and no window, menu, chat or text box in
        /// front of the game (as Outfitter's window key asks); the harness sets another.</summary>
        internal static Func<bool> CanUseKeys = GameCanUseKeys;

        // The stand behind each switch he has pointed at (the hover text asks every frame), and the counted stand behind each
        // armour stand (null: it doesn't count), so a frame's hover allocates nothing (review round 1, n6).
        private static readonly Dictionary<Switch, ArmorStand> SwitchStands = new Dictionary<Switch, ArmorStand>();
        private static readonly Dictionary<ArmorStand, GameStand> Counted = new Dictionary<ArmorStand, GameStand>();

        /// <summary>The counted stand a switch belongs to, or null.</summary>
        internal static IStand StandOf(Switch control)
        {
            if (control == null)
            {
                return null;
            }
            if (!SwitchStands.TryGetValue(control, out ArmorStand stand))
            {
                if (SwitchStands.Count > 256)
                {
                    SwitchStands.Clear();   // destroyed switches linger as keys: start again now and then
                }
                SwitchStands[control] = stand = control.GetComponentInParent<ArmorStand>();
            }
            return CountedOf(stand);
        }

        // The counted stand behind an armour stand, kept while its record is the same.
        private static GameStand CountedOf(ArmorStand stand)
        {
            if (stand == null)
            {
                return null;
            }
            if (Counted.TryGetValue(stand, out GameStand known) && known.Valid)
            {
                return known;
            }
            if (Counted.Count > 256)
            {
                Counted.Clear();
            }
            GameStand counted = GameStand.Of(stand);   // asked again while it doesn't count: its record may not be in yet
            if (counted != null)
            {
                Counted[stand] = counted;
            }
            else
            {
                Counted.Remove(stand);
            }
            return counted;
        }

        private static IStand GameHovered()
        {
            Player player = Player.m_localPlayer;
            GameObject hovering = player != null ? player.GetHoverObject() : null;
            ArmorStand stand = hovering != null ? hovering.GetComponentInParent<ArmorStand>() : null;
            return CountedOf(stand);
        }

        private static List<IStand> GameNearby(Vector3 point, float range)
        {
            var found = new List<KeyValuePair<float, IStand>>();
            foreach (ArmorStand stand in UnityEngine.Object.FindObjectsByType<ArmorStand>(FindObjectsSortMode.None))
            {
                GameStand counted = GameStand.Of(stand);
                if (counted == null)
                {
                    continue;
                }
                float distance = Vector3.Distance(point, counted.Position);
                if (distance <= range)
                {
                    found.Add(new KeyValuePair<float, IStand>(distance, counted));
                }
            }
            found.Sort((a, b) => a.Key.CompareTo(b.Key));
            return found.ConvertAll(each => each.Value);
        }

        private static Loadout GameCapture(StandSaves saves, string name)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return null;
            }
            switch (saves)
            {
                case StandSaves.WhatIWear:
                    var cells = new List<Vector2i>();
                    ItemDrop.ItemData hiddenRight = GameHooks.HiddenRight(player), hiddenLeft = GameHooks.HiddenLeft(player);
                    foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                    {
                        if (player.IsItemEquiped(item) || ReferenceEquals(item, hiddenRight) || ReferenceEquals(item, hiddenLeft))
                        {
                            cells.Add(item.m_gridPos);
                        }
                    }
                    return Capture.Cells(player, name, cells);
                case StandSaves.TheCellsIPicked when Window.Picks.Count > 0:
                    return Capture.Cells(player, name, Window.Picks.All());
                default:
                    return Capture.Everything(player, name);
            }
        }

        private static bool GameCanUseKeys()
        {
            Player player = Player.m_localPlayer;
            return player != null && !player.IsDead() && !player.InCutscene() && !player.IsTeleporting() && !player.InPlaceMode() &&
                   !Window.Popup.Showing && !Window.LoadoutWindow.Open &&
                   (Chat.instance == null || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible() &&
                   !StoreGui.IsVisible() && !Menu.IsVisible() && !InventoryGui.IsVisible() &&
                   (TextViewer.instance == null || !TextViewer.instance.IsVisible()) && !Minimap.IsOpen() && !GameCamera.InFreeFly() &&
                   !Hud.IsPieceSelectionVisible();
        }
    }
}
