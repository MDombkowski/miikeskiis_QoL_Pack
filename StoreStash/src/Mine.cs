using System;
using QoLMods.Chests;
using UnityEngine;

namespace StoreStash
{
    /// <summary>
    /// The "mine" mark (card A43): Left Alt + H, pointing at a chest or with its window open, marks it as his, or takes his
    /// mark off. The personal quick stack fills a chest he marked, and friends on the pack leave it alone. It's written into
    /// the chest's own record (ChestMine: this character's player ID and name), always as the chest's owner, as Reserve
    /// Stock writes its mark: a chest open in his window is his, so it's written at once; one he points at is first claimed
    /// from its owner through the chest service, as opening it would, and written in the frame it becomes his (its owner
    /// says no while someone else has it open). Then the record is sent at once, so every player nearby has the mark within
    /// a moment; it lasts with the world. Anyone may mark a chest he could open by hand; a chest someone else marked stays
    /// theirs until they take their mark off.
    /// </summary>
    internal static class Mine
    {
        /// <summary>How long a press waits for the chest service to be free (another module taking from chests) before
        /// giving up, in seconds.</summary>
        internal const float WaitForService = 5f;

        /// <summary>The game's clock (Time.time). Only the scratch test harness sets another.</summary>
        internal static Func<float> Clock = () => Time.time;

        /// <summary>This character's name, as it's written with the mark. Only the scratch test harness sets another.</summary>
        internal static Func<string> MyName = () => Game.instance != null ? Game.instance.GetPlayerProfile().GetName() : string.Empty;

        /// <summary>Whether a chest can carry the mark: the game's own kind of chest (carts' and ships' too), built by a
        /// player; never a gravestone, the Obliterator (its lever destroys what it holds: ChestFinder.IsObliterator), a chest
        /// the world made, or another mod's container. Only the scratch test harness sets another: it has no engine to ask
        /// about pieces.</summary>
        internal static Func<Container, bool> Markable = chest =>
        {
            if (chest == null || chest.GetType() != typeof(Container) || chest.GetComponentInParent<TombStone>() != null ||
                ChestFinder.IsObliterator(chest))
            {
                return false;
            }
            Piece piece = chest.GetComponentInParent<Piece>();
            return piece != null && piece.IsPlacedByPlayer();
        };

        // A press waiting for the chest service or the chest's owner: the chest, what to write, and since when.
        private static Pending pending;

        /// <summary>Every frame: a press still waiting for the chest service moves on.</summary>
        internal static void Update()
        {
            if (pending != null)
            {
                Advance();
            }
        }

        /// <summary>The undo step after an error: forgets a press still waiting (a claim already asked for ends by itself,
        /// and then writes nothing, as it does when the mod is switched off meanwhile: Advance and Claimed).</summary>
        internal static void Cancel() => pending = null;

        /// <summary>The key (Keys): marks or unmarks the chest open in his window, else the one he points at.</summary>
        internal static void Press()
        {
            Container chest = Target(Player.m_localPlayer);
            if (chest != null)
            {
                PressOn(chest);
            }
        }

        /// <summary>The chest the key acts on: the one open in his window, else the one he points at; null if the key does
        /// nothing there (KeyActs).</summary>
        internal static Container Target(Player player)
        {
            Container chest;
            if (InventoryGui.IsVisible())
            {
                chest = GameHooks.OpenChest(InventoryGui.instance);   // the window hides the world: never what's behind it
            }
            else
            {
                GameObject hover = player != null ? player.GetHoverObject() : null;
                chest = hover != null ? hover.GetComponentInParent<Container>() : null;
            }
            return chest != null && KeyActs(chest, ChestClaims.PlayerId()) ? chest : null;
        }

        /// <summary>Whether the key (and the chest window's button) acts on this chest for character <paramref name="me"/>:
        /// one that can carry the mark; or an Obliterator that already carries his, marked before the pack left it out (card
        /// A80's review), so that he can take the mark off. A mark comes off anything; only a new one is refused there.</summary>
        internal static bool KeyActs(Container chest, long me) =>
            chest != null && (Markable(chest) || (ChestMine.IsMine(chest, me) && ChestAccess.IsObliterator(chest)));

        /// <summary>The key's work on one chest that can carry the mark: why not, at once if it's his already, or once its
        /// owner hands it over.</summary>
        internal static void PressOn(Container chest)
        {
            long me = ChestClaims.PlayerId();
            if (ChestMine.MarkedByOther(chest, me))
            {
                Messages.Center($"{Whose(chest)} chest: only they can take their mark off.");
                return;
            }
            bool mark = !ChestMine.IsMine(chest, me);
            string why = WhyNot(chest);
            if (!mark && why == ChestFinder.ObliteratorWhy)
            {
                why = null;   // an old mark on an Obliterator comes off; nothing new goes on (KeyActs)
            }
            if (why != null)
            {
                Messages.Center($"Can't {Verb(mark)} it: the chest is {why}.");
                return;
            }
            if (Write(chest, mark))
            {
                Wrote(chest, mark);   // his already: at once, whatever an earlier press still waits for
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
            pending = new Pending { Chest = chest, Mark = mark, Since = Clock() };
            Advance();
        }

        /// <summary>
        /// Writes the mark (this character's ID and name, and what other players may do there: card A80, ChestAccess) or
        /// takes it off (0, "" and 0), on a chest this computer owns, and sends the chest's record at once. False, changing
        /// nothing, on a chest it doesn't own, one that isn't loaded, or one someone else has marked (read again here: the
        /// record came with the ownership).
        /// </summary>
        internal static bool Write(Container chest, bool mark)
        {
            ZNetView view = chest != null ? ChestHooks.NetView(chest) : null;
            if (view == null || !view.IsValid() || !view.IsOwner())
            {
                return false;
            }
            long me = ChestClaims.PlayerId();
            if (me == 0L || ChestMine.MarkedByOther(chest, me))
            {
                return false;
            }
            ZDO record = view.GetZDO();
            record.Set(ChestMine.Key, mark ? me : 0L);
            record.Set(ChestMine.NameKey, mark ? Plain(MyName()) : string.Empty);
            record.Set(ChestAccess.Key, mark ? MineSettings.Wanted() : 0);
            ZDOMan.instance?.ForceSendZDO(record.m_uid);
            return true;
        }

        /// <summary>Why the key can't mark or unmark this chest, as words that follow "the chest is"; null if it can. The
        /// chest service's own rules for using a chest (ChestFinder.WhyNot, through its Judge), so the hover's key hint
        /// shows exactly where the key would act.</summary>
        internal static string WhyNot(Container chest) => ChestFinder.Judge(chest, new ChestRules(), ChestClaims.PlayerId());

        /// <summary>Whose a chest someone else marked is, as a hover line and a message say it: "Ada's", or "Someone
        /// else's" ("someone else's" inside a sentence) when no name came with the mark. The chest service's (ChestMine.Whose,
        /// since card A80: its messages to other players name the chest the same way).</summary>
        internal static string Whose(Container chest, bool capital = true) => ChestMine.Whose(chest, capital);

        // A name as plain text, whatever another game wrote into the record (ChestMine.Plain, where the reasons are).
        private static string Plain(string name) => ChestMine.Plain(name);

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
                Messages.Center($"Couldn't {Verb(waiting.Mark)} it: the chest is gone.");
                return;
            }
            if (Clock() - waiting.Since > WaitForService)
            {
                pending = null;
                Messages.Center($"Couldn't {Verb(waiting.Mark)} it: other chests were being taken from. Try again.");
                return;
            }
            if (ChestClaims.Busy)
            {
                return;
            }
            waiting.Started = true;   // before Start: the claim may settle inside it, when the chest is already his
            if (!ChestClaims.Start(Module.Chests, new[] { waiting.Chest }, result => Module.Guard("marking a chest as yours", () => Claimed(waiting, result))))
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
            string verb = Verb(waiting.Mark);
            if (result.Held.Contains(waiting.Chest))
            {
                if (ChestMine.MarkedByOther(waiting.Chest, ChestClaims.PlayerId()))
                {
                    Messages.Center($"Can't {verb} it: it's {Whose(waiting.Chest, capital: false)} chest now.");
                }
                else if (Write(waiting.Chest, waiting.Mark))
                {
                    Said(waiting.Mark);
                }
                else
                {
                    Messages.Center($"Couldn't {verb} it: the chest is gone.");
                }
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

        private static void Said(bool mark) => Messages.Center(mark ? "Marked as yours." : "No longer marked as yours.");

        // After a write of his own: a press on the same chest still waiting for the chest service is dropped, so it can't
        // land later and undo this one; then he's told.
        private static void Wrote(Container chest, bool mark)
        {
            if (pending != null && !pending.Started && pending.Chest == chest)
            {
                pending = null;
            }
            Said(mark);
        }

        private static string Verb(bool mark) => mark ? "mark" : "unmark";

        private sealed class Pending
        {
            internal Container Chest;
            internal bool Mark;
            internal float Since;
            internal bool Started;
        }
    }
}
