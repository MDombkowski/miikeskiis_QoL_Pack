using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Handyman
{
    /// <summary>
    /// The mod's work in the game, once a frame just before the game's Player.Update reads the Hide key (Patches.cs):
    /// note the piece the tool in hand has picked, read the key, and act on a tap or a hold (KeyWatch, TapRules).
    /// It takes the game's Hide press only for that frame (ZInput.ResetButtonStatus), never touches
    /// Humanoid.HideHandItems' other callers (swimming, stations, sitting, eating and the barber use it too), and puts
    /// the tool away itself, through the game's own HideHandItems and its own conditions, only when a hold or a setting
    /// ("Tap again", "With no piece to go back to") says to.
    /// </summary>
    internal static class Hammer
    {
        private const string NoPieceYetText = "No piece to go back to yet";

        private static readonly KeyWatch Watch = new KeyWatch();
        private static readonly ToolMemory<PieceTable, Piece> Memory = new ToolMemory<PieceTable, Piece>();

        /// <summary>Tools whose Repair the game's own pick gets wrong (Pick): the key is left to the game with them for
        /// the rest of the session. Compared by reference.</summary>
        private static readonly System.Collections.Generic.List<PieceTable> WrongPickTools = new System.Collections.Generic.List<PieceTable>();
        private static readonly Action Work = DoFrame;

        private static Player player;
        private static long characterId;
        private static bool saidNoHideButton;
        private static bool saidWrongPick;

        /// <summary>From the prefix on Player.Update, for the local player, while the module is on.</summary>
        internal static void BeforePlayerUpdate(Player localPlayer)
        {
            player = localPlayer;
            try
            {
                Module.Guard("reading the Hide key", Work);
            }
            finally
            {
                player = null;
            }
        }

        /// <summary>While the module is off: forget a press being waited on, so switching it on later can't act on it.</summary>
        internal static void Idle() => Watch.Reset();

        /// <summary>Undo, when the module fails: nothing of the game's to give back (a piece picked stays picked, as if
        /// picked in the build menu), so only its own state.</summary>
        internal static void Forget()
        {
            Watch.Reset();
            Memory.Clear();
            WrongPickTools.Clear();
        }

        private static void DoFrame()
        {
            if (!Settings.RPicksRepair.Value)
            {
                Watch.Reset();
                return;
            }

            // Another character (logged out and in as someone else): the pieces kept were the other one's. The same
            // character after dying or a teleport keeps them.
            long id = player.GetPlayerID();   // 0 while the player's network object isn't ready: no change then
            if (id != 0 && id != characterId)
            {
                characterId = id;
                Memory.Clear();
            }

            ItemDrop.ItemData item = player.RightItem;
            PieceTable table = player.InPlaceMode() ? item?.m_shared?.m_buildPieces : null;
            Note(table);

            bool usesHideKey = Settings.Key.Value == KeyChoice.GameHideKey;
            bool down = usesHideKey ? ZInput.GetButtonDown(GameHooks.HideButton) : Settings.Pressed(Settings.OwnKey.Value);
            if (!down && !Watch.Waiting)
            {
                return;   // most frames
            }

            Piece keyPiece = table == null ? null : KeyPieceOf(table, item);
            InputAction hideAction = usesHideKey ? HideAction() : null;
            KeyFrame frame = new KeyFrame
            {
                // Application.isFocused: a press held while the game loses the focus (Alt + Tab) reads as let go, since
                // the input system resets the keyboard then; dropping it keeps that from counting as a tap.
                Ready = keyPiece != null && Application.isFocused && GameHooks.TakesInput(player)
                    && (!usesHideKey || (hideAction != null && GameReadsKeyboardHide())),
                Tool = item,
                UsesHideKey = usesHideKey,
                Down = down,
                Held = hideAction != null && hideAction.IsPressed(),
                Shift = ZInput.GetKey(KeyCode.LeftShift, false) || ZInput.GetKey(KeyCode.RightShift, false),
                MenuOpen = Hud.IsPieceSelectionVisible(),
                Now = Time.unscaledTime,
                PutAway = Settings.PutAway.Value,
                HoldSeconds = Mathf.Clamp(Settings.HoldSeconds.Value, Settings.ShortestHold, Settings.LongestHold),
                Menu = Settings.BuildMenuOpen.Value,
            };

            KeyStep step = Watch.Step(frame);
            if (step.TakeGamePress)
            {
                ZInput.ResetButtonStatus(GameHooks.HideButton);
            }
            switch (step.Event)
            {
                case KeyEvent.Tap:
                    Do(TapRules.OnTap(step.MenuOpenAtPress, IsKeyPiece(player.GetSelectedPiece()), Settings.TapAgain.Value,
                        Memory.TryGet(table, out _, out _), Settings.NoPiece.Value), table, keyPiece);
                    break;
                case KeyEvent.Hold:
                    Do(TapRules.OnHold(), table, keyPiece);
                    break;
            }
        }

        /// <summary>Keep the piece the tool has picked, and its turn, while it isn't Repair: the way back goes to it,
        /// however Repair was then picked (the key, or the build menu's hammer button).</summary>
        private static void Note(PieceTable table)
        {
            if (table == null)
            {
                return;
            }
            Piece piece = player.GetSelectedPiece();
            if (piece == null || IsKeyPiece(piece))
            {
                return;
            }
            Memory.Note(table, piece, GameHooks.GetTurn(player));
        }

        private static bool IsKeyPiece(Piece piece) => piece != null && (piece.m_repairPiece || piece.m_removePiece);

        /// <summary>The piece the key picks with this tool, or null if the key is the game's with it (TapRules.KeyPiece).
        /// Read from the tool's available pieces, the list the game's own pick searches.</summary>
        private static Piece KeyPieceOf(PieceTable table, ItemDrop.ItemData item)
        {
            foreach (PieceTable wrong in WrongPickTools)
            {
                if (ReferenceEquals(wrong, table))
                {
                    return null;
                }
            }
            return TapRules.KeyPiece(table.m_availablePieces, piece => piece.m_repairPiece, piece => piece.m_removePiece,
                item.m_shared.m_name == "$item_hammer", Settings.Tools.Value, Settings.ServingTrayRemove.Value);
        }

        /// <summary>The input action behind the game's Hide button, to tell whether the key is still held after its
        /// press was taken: taking it clears the button's own held state and timers (ZInput.ButtonDef.ResetState), so the
        /// game's GetButton and GetButtonPressedTimer read nothing until the next press.</summary>
        private static InputAction HideAction()
        {
            InputAction action = ZInput.instance?.GetButtonDef(GameHooks.HideButton)?.ButtonAction;
            if (action == null && !saidNoHideButton)
            {
                saidNoHideButton = true;
                Module.Log.LogWarning($"The game has no \"{GameHooks.HideButton}\" button, so its Hide key can't pick Repair; " +
                    "\"Own key\" still can (the setting \"Key\").");
            }
            return action;
        }

        /// <summary>The game reads the keyboard's Hide button in this input layout (Player.Update: the default layout, or
        /// no gamepad in use); with a gamepad in another layout it doesn't, and neither does the mod.</summary>
        private static bool GameReadsKeyboardHide() => ZInput.InputLayout == InputLayout.Default || !ZInput.IsGamepadActive();

        private static void Do(Act act, PieceTable table, Piece keyPiece)
        {
            switch (act)
            {
                case Act.PickRepair:
                    Pick(table, keyPiece);
                    break;
                case Act.PickRepairAndCloseMenu:
                    if (Pick(table, keyPiece) && Hud.IsPieceSelectionVisible())
                    {
                        Hud.CloseBuildUi();   // as the menu's own buttons close it (BuildUi.OnSelectPiece)
                    }
                    break;
                case Act.BackToPiece:
                    Back(table);
                    break;
                case Act.PutAway:
                    PutAway();
                    break;
                case Act.OpenMenu:
                    if (!Hud.IsPieceSelectionVisible() && Hud.instance != null)
                    {
                        Hud.instance.TogglePieceSelection();
                    }
                    break;
                case Act.NoPieceYet:
                    if (Settings.Message.Value)
                    {
                        player.Message(MessageHud.MessageType.TopLeft, NoPieceYetText);
                    }
                    break;
            }
        }

        /// <summary>Pick the tool's Repair (or Remove); false if it wasn't picked, and then nothing has changed.</summary>
        private static bool Pick(PieceTable table, Piece keyPiece)
        {
            if (keyPiece == null)
            {
                return false;
            }
            // The selection as it is, to undo a wrong pick exactly: its tab, its slot there, the turn, and the slot of
            // the tool's first tab, which the game's pick of an All-tabs piece writes (PieceTable.GetSelectedCategory).
            Piece.PieceCategory category = table.GetSelectedCategory();
            Vector2Int index = table.GetSelectedIndex();
            int turn = GameHooks.GetTurn(player);
            int firstTab = table.m_categories.Count > 0 ? (int)table.m_categories[0] : (int)Piece.PieceCategory.Misc;
            bool firstTabKept = firstTab >= 0 && firstTab < table.m_selectedPiece.Length;
            Vector2Int firstTabSlot = firstTabKept ? table.m_selectedPiece[firstTab] : default;

            if (!player.SetSelectedPiece(keyPiece))
            {
                return false;
            }
            if (player.GetSelectedPiece() == keyPiece)
            {
                Tell(keyPiece);
                return true;
            }

            // The game finds the piece's slot in the table's first list but files it under the tool's first tab, which
            // agree only when it is first in the tool's pieces (the game's hammer and serving tray; the menu's own button
            // has the same quirk). Another mod's tool with its Repair further down gets some other piece: put the
            // selection back as it was, and leave the key to the game with this tool from now on (KeyPieceOf).
            if (firstTabKept)
            {
                table.m_selectedPiece[firstTab] = firstTabSlot;
            }
            player.SetBuildCategory(category);
            player.SetSelectedPiece(index);
            GameHooks.SetTurn(player, turn);
            WrongPickTools.Add(table);
            if (!saidWrongPick)
            {
                saidWrongPick = true;
                string what = keyPiece.m_repairPiece ? "Repair" : "Remove";
                string setting = keyPiece.m_repairPiece ? "\"Tools\" at HammerOnly" : "\"Serving tray picks its Remove\" off";
                Module.Log.LogWarning($"The game picked another piece instead of this tool's {what} (it isn't first in the " +
                    $"tool's pieces), so R is left to the game with this tool. With {setting}, such tools are left to the " +
                    "game from the start.");
            }
            return false;
        }

        private static void Back(PieceTable table)
        {
            if (!Memory.TryGet(table, out Piece piece, out int turn))
            {
                return;
            }
            if (piece == null)   // Unity's null: the piece is gone from the game (another mod's, unloaded)
            {
                Memory.Forget(table);
                Do(TapRules.OnTap(false, true, AgainChoice.BackToMyPiece, false, Settings.NoPiece.Value), table, null);
                return;
            }
            if (!player.SetSelectedPiece(piece))
            {
                // Not available now (out of season, or no longer known): say so, and do what "With no piece to go back
                // to" says (put the hammer away, or open the build menu; with Nothing, the message is all). The piece is
                // kept for a later tap, until another piece is picked.
                if (Settings.Message.Value)
                {
                    // The game's message localizes the piece's "$piece_…" name inside the sentence.
                    player.Message(MessageHud.MessageType.TopLeft, $"{piece.m_name} can't be picked now");
                }
                Act instead = TapRules.OnTap(false, true, AgainChoice.BackToMyPiece, false, Settings.NoPiece.Value);
                if (instead != Act.NoPieceYet)
                {
                    Do(instead, table, null);
                }
                return;
            }
            if (Settings.RestoreTurn.Value)
            {
                GameHooks.SetTurn(player, turn);   // after the pick, which gives a piece that turns at random a new turn
            }
            Tell(piece);
        }

        /// <summary>Put the tool away as the game's Hide key does (Player.Update), with the game's own conditions.</summary>
        private static void PutAway()
        {
            if ((player.RightItem != null || player.LeftItem != null) && !player.InAttack() && !player.InDodge())
            {
                player.HideHandItems();
            }
        }

        private static void Tell(Piece piece)
        {
            if (Settings.Sound.Value)
            {
                player.PlayButtonSound();
            }
            if (Settings.Message.Value)
            {
                player.Message(MessageHud.MessageType.TopLeft, piece.m_name);
            }
        }
    }
}
