using QoLMods.Chests;
using UnityEngine;

namespace ReserveStock
{
    /// <summary>
    /// Writes Reserve Stock's mark (ChestReserve.Key, a bool in the chest's own record), always as the chest's owner: a
    /// change made anywhere else can be lost, or undo the owner's. The window's button writes at once, since a chest is
    /// his while its window is open. The key, pointing at a chest, first has the chest service claim it from its owner
    /// through the game's own open request, as opening it would, and writes in the frame it becomes his; its owner says no
    /// while someone has it open. Anyone who could open a chest may reserve or free it (his answer 17). After writing,
    /// the chest's record is sent at once, so every player nearby has the mark within a moment; it lasts with the world.
    /// </summary>
    internal static class Mark
    {
        /// <summary>How long a press of the key waits for the chest service to be free (another module taking from chests)
        /// before giving up, in seconds.</summary>
        internal const float WaitForService = 5f;

        // A press of the key waiting for the chest's owner: the chest, what to write, and since when.
        private static Pending pending;

        /// <summary>Whether a chest is one that can carry the mark: the game's own kind of chest (carts and ships' chests
        /// too), built by a player; never a gravestone, a chest the world made, or another mod's container; the Obliterator
        /// only while it carries a mark from before the pack left it out (card A80), so the padlock and the key can free it,
        /// and never reserve one.</summary>
        internal static bool Reservable(Container chest)
        {
            if (chest == null || chest.GetType() != typeof(Container) || chest.GetComponentInParent<TombStone>() != null ||
                (ChestFinder.IsObliterator(chest) && !ChestReserve.IsReserved(chest)))
            {
                return false;
            }
            Piece piece = chest.GetComponentInParent<Piece>();
            return piece != null && piece.IsPlacedByPlayer();
        }

        /// <summary>
        /// Sets the mark on a chest this computer owns, and sends the chest's record at once. Returns false, changing
        /// nothing, on a chest it doesn't own, or one that isn't loaded.
        /// </summary>
        internal static bool Write(Container chest, bool reserved)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return false;
            }
            ZDO record = view.GetZDO();
            record.Set(ChestReserve.Key, reserved);
            ZDOMan.instance?.ForceSendZDO(record.m_uid);
            return true;
        }

        /// <summary>From the window's button: reserves the open chest, or frees it. It's his while its window is open.</summary>
        internal static void ToggleOpen(Container chest)
        {
            if (!Reservable(chest))
            {
                return;
            }
            bool reserve = !ChestReserve.IsReserved(chest);
            string kept = ChestAccess.WhyNotByHand(chest, Game.instance.GetPlayerProfile().GetPlayerID());
            if (kept != null)
            {
                Messages.Center($"Can't {Verb(reserve)} it: the chest is {kept}.");   // card A80: the button hides there too
                return;
            }
            if (Write(chest, reserve))
            {
                Wrote(chest, reserve);
            }
            else
            {
                Messages.Center($"Couldn't {Verb(reserve)} it: the chest isn't yours yet. Try again in a moment.");
            }
        }

        /// <summary>Every frame: the key, and a press still waiting for the chest service or the chest's owner.</summary>
        internal static void Update()
        {
            if (pending != null)
            {
                Advance();
            }
            if (!Module.IsOn || !Settings.Pressed(Settings.Key.Value) || !TakesKeys())
            {
                return;
            }
            Player player = Player.m_localPlayer;
            Container chest = Pointed(player);
            if (chest == null)
            {
                return;   // pointing at nothing reservable: the key does nothing, as E does on nothing
            }
            bool reserve = !ChestReserve.IsReserved(chest);
            string why = WhyNot(chest, reserve);
            if (why != null)
            {
                Messages.Center($"Can't {Verb(reserve)} it: the chest is {why}.");
                return;
            }
            if (Write(chest, reserve))
            {
                Wrote(chest, reserve);   // his already: at once, whatever an earlier press still waits for
                return;
            }
            if (pending != null)
            {
                // One chest is claimed at a time; a press meanwhile is refused with word why, never dropped unsaid. A press
                // on the chest already waiting is the same wish: it lands by itself, and pressing again would undo it.
                Messages.Center(!pending.Started
                    ? pending.Chest == chest ? "Still waiting: other chests are being taken from; this one is next." : "Still waiting: other chests are being taken from. Try again in a moment."
                    : pending.Chest == chest ? "Still waiting for this chest's owner to answer." : "Still waiting for the last chest's owner to answer.");
                return;
            }
            pending = new Pending { Chest = chest, Reserve = reserve, Since = Time.time };
            Advance();
        }

        /// <summary>The undo step, and switching off: forgets a press still waiting (a claim already asked for ends by
        /// itself, and then writes nothing).</summary>
        internal static void Cancel() => pending = null;

        // Starts the claim once the chest service is free, or gives up after a while.
        private static void Advance()
        {
            Pending waiting = pending;
            if (waiting.Started)
            {
                return;   // the chest service answers (Claimed), within its own time limits
            }
            if (!Module.IsOn)
            {
                pending = null;
                return;
            }
            if (waiting.Chest == null)   // unloaded or destroyed while waiting
            {
                pending = null;
                Messages.Center($"Couldn't {Verb(waiting.Reserve)} it: the chest is gone.");
                return;
            }
            if (Time.time - waiting.Since > WaitForService)
            {
                pending = null;
                Messages.Center($"Couldn't {Verb(waiting.Reserve)} it: other chests were being taken from. Try again.");
                return;
            }
            if (ChestClaims.Busy)
            {
                return;
            }
            waiting.Started = true;   // before Start: the claim may settle inside it, when the chest is already his
            if (!ChestClaims.Start(Module.Chests, new[] { waiting.Chest }, result => Module.Guard("reserving a chest", () => Claimed(waiting, result))))
            {
                waiting.Started = false;
            }
        }

        // In the frame the claim settled (inside the module's Guard): the chest is his now, or why not.
        private static void Claimed(Pending waiting, ClaimResult result)
        {
            if (pending == waiting)
            {
                pending = null;
            }
            if (!Module.IsOn)
            {
                return;   // switched off meanwhile: nothing is written
            }
            string verb = Verb(waiting.Reserve);
            if (result.Held.Contains(waiting.Chest) && Write(waiting.Chest, waiting.Reserve))
            {
                Said(waiting.Reserve);
            }
            else if (result.InUse > 0)
            {
                Messages.Center($"Can't {verb} it: someone has the chest open, or is pulling its cart.");
            }
            else if (result.NoAnswer > 0)
            {
                Messages.Center($"Couldn't {verb} it: the chest's owner didn't answer in time. Try again.");
            }
            else
            {
                Messages.Center($"Couldn't {verb} it: the chest is gone.");
            }
        }

        private static void Said(bool reserved) => Messages.Center(reserved ? "Chest reserved." : "Chest no longer reserved.");

        // After a write of his own: a press on the same chest still waiting for the chest service is dropped, so it can't
        // land later and undo this one; then he's told.
        private static void Wrote(Container chest, bool reserve)
        {
            if (pending != null && !pending.Started && pending.Chest == chest)
            {
                pending = null;
            }
            Said(reserve);
        }

        /// <summary>Why the key can't reserve or free this chest, as words that follow "the chest is"; null if it can.
        /// The chest service's own rules for using a chest (ChestFinder.WhyNot), so the hover's key hint shows exactly
        /// where the key would act; and, the key being his hand, another player's personal chest he may only look at or is
        /// kept out of, even where they let other players' mods use it (card A80, ChestAccess).</summary>
        internal static string WhyNot(Container chest, bool reserve = true)
        {
            long me = Game.instance.GetPlayerProfile().GetPlayerID();
            string why = ChestFinder.WhyNot(chest, new ChestRules(), me);
            if (!reserve && why == ChestFinder.ObliteratorWhy)
            {
                why = null;   // an Obliterator reserved before the pack left it out may be freed, never reserved (Reservable)
            }
            return why ?? ChestAccess.WhyNotByHand(chest, me);
        }

        private static string Verb(bool reserve) => reserve ? "reserve" : "unreserve";

        /// <summary>The chest he's pointing at, if it can carry the mark, else null.</summary>
        internal static Container Pointed(Player player)
        {
            GameObject hover = player != null ? player.GetHoverObject() : null;
            Container chest = hover != null ? hover.GetComponentInParent<Container>() : null;
            return Reservable(chest) ? chest : null;
        }

        // As the game decides whether a key is meant for it: a player, and no chat, console, text box, menu or window in
        // the way. With a chest's window open, its button is the way.
        private static bool TakesKeys()
        {
            Player player = Player.m_localPlayer;
            return player != null && !player.IsDead() && !player.InCutscene() &&
                   (Chat.instance == null || !Chat.instance.HasFocus()) && !global::Console.IsVisible() && !TextInput.IsVisible() &&
                   !Menu.IsVisible() && (TextViewer.instance == null || !TextViewer.instance.IsVisible()) && !Minimap.IsOpen() &&
                   !InventoryGui.IsVisible() && !StoreGui.IsVisible() && !Hud.InRadial();
        }

        private sealed class Pending
        {
            internal Container Chest;
            internal bool Reserve;
            internal float Since;
            internal bool Started;
        }
    }
}
