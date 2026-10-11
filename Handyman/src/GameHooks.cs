using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Handyman
{
    /// <summary>
    /// The game method this mod hooks, and the private members it uses, looked up once at startup. Checked against the
    /// decompiled game 1.0.16 (2026-09-30): Player.Update reads the "Hide" button (ZInput.GetButtonDown) and puts both
    /// hands away, only while Player.TakeInput says the game takes input; Player.m_placeRotation is the ghost's turn in
    /// steps of 22.5 degrees.
    /// </summary>
    internal static class GameHooks
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>The game's name for its "Hide/show weapon" button (ZInput's button list; R by default).</summary>
        internal const string HideButton = "Hide";

        private static Func<Player, bool> takeInput;
        private static AccessTools.FieldRef<Player, int> placeRotation;

        /// <summary>Player.Update(): where the game reads the Hide key.</summary>
        internal static MethodInfo PlayerUpdate { get; private set; }

        internal static void Resolve(List<string> missing, List<string> missingOptional)
        {
            PlayerUpdate = typeof(Player).GetMethod("Update", AnyInstance, null, Type.EmptyTypes, null);
            if (PlayerUpdate == null)
            {
                missing.Add("Player.Update");
            }

            MethodInfo takes = typeof(Player).GetMethod("TakeInput", AnyInstance, null, Type.EmptyTypes, null);
            if (takes == null || takes.ReturnType != typeof(bool))
            {
                missing.Add("Player.TakeInput");
            }
            else
            {
                takeInput = (Func<Player, bool>)Delegate.CreateDelegate(typeof(Func<Player, bool>), takes);
            }

            FieldInfo rotation = typeof(Player).GetField("m_placeRotation", AnyInstance);
            if (rotation == null || rotation.FieldType != typeof(int))
            {
                missingOptional.Add("Player.m_placeRotation (going back to a piece then turns it as the game does)");
            }
            else
            {
                placeRotation = AccessTools.FieldRefAccess<Player, int>(rotation);
            }
        }

        /// <summary>Whether the game takes the player's input now: false in chat, the console, the inventory, the map,
        /// the menu, the build menu's filter box and the like, as the game itself decides before reading the Hide key.</summary>
        internal static bool TakesInput(Player player) => takeInput(player);

        /// <summary>The ghost's turn, in the game's steps of 22.5 degrees.</summary>
        internal static int GetTurn(Player player) => placeRotation == null ? 0 : placeRotation(player);

        internal static void SetTurn(Player player, int turn)
        {
            if (placeRotation != null)
            {
                placeRotation(player) = turn;
            }
        }
    }
}
