using System;
using System.Collections.Generic;
using QoLMods.Chests;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// Keeps his choice for other players ("Other players at my marked chests", card A80) written into every chest he marked
    /// (ChestAccess.Key, beside the "mine" mark), so it reaches the other players' games. Marking a chest writes it at once
    /// (Mine.Write); after he changes the setting, each chest of his takes the new choice the next time his game holds it:
    /// once a second, every loaded chest marked as his that this computer owns and may change now (ChestClaims.MayChange:
    /// for a chest claimed beyond the server's hand-out, only between the server's rounds) and that holds another number is
    /// written and sent. Only his game writes it, only as the chest's owner, and never a removal. A chest another game holds
    /// (a friend near it first, the server with nobody near) keeps its old choice until then.
    /// </summary>
    internal static class MineAccess
    {
        /// <summary>How often the chests are looked over, in seconds.</summary>
        internal const float Every = 1f;

        /// <summary>The game's clock (Time.time). Only the scratch test harness sets another.</summary>
        internal static Func<float> Clock = () => Time.time;

        /// <summary>The chests loaded on this computer (ChestFinder.Loaded). Only the scratch test harness sets another.</summary>
        internal static Func<IEnumerable<Container>> Loaded = ChestFinder.Loaded;

        private static float next;

        /// <summary>Every frame: once a second, while StoreStash is on and he has a character, his marked chests are brought
        /// up to his setting.</summary>
        internal static void Update()
        {
            if (!Module.IsOn || Player.m_localPlayer == null)   // Unity's own null test: no character at the main menu
            {
                return;
            }
            float now = Clock();
            if (now < next)
            {
                return;
            }
            next = now + Every;
            Refresh(ChestClaims.PlayerId(), MineSettings.Wanted());
        }

        /// <summary>Writes <paramref name="wanted"/> (this version's bits of it, ChestAccess.Mixed) into every loaded chest
        /// marked by character <paramref name="me"/> that this computer may change now and that holds another choice, never
        /// an Obliterator; returns how many it wrote.</summary>
        internal static int Refresh(long me, int wanted)
        {
            if (me == 0L)
            {
                return 0;
            }
            int written = 0;
            foreach (Container chest in Loaded())
            {
                if (chest == null || !ChestMine.IsMine(chest, me))
                {
                    continue;
                }
                // Only this version's bits are compared and written; any a later version wrote are kept (two installs of
                // different versions never rewrite each other's). Never an Obliterator: an old mark there means nothing.
                int stored = ChestAccess.Stored(chest);
                int mixed = ChestAccess.Mixed(stored, wanted);
                if (mixed != stored && !ChestAccess.IsObliterator(chest) && ChestClaims.MayChange(chest) && ChestAccess.Write(chest, mixed))
                {
                    written++;
                }
            }
            return written;
        }
    }
}
