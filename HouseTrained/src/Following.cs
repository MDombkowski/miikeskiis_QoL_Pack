using UnityEngine;

namespace HouseTrained
{
    /// <summary>
    /// Following (card B25, his idea of 2026-10-11: every tameable creature follows you when you interact with it, as wolves
    /// do; on by default). The game already has the whole command: Tameable.Interact pets a tame, and only when its prefab
    /// is commandable (only the wolf is, by the installed game's prefabs) sends its "Command" to the game that looks after
    /// it, whose RPC_Command toggles follow and stay, writes the follower's name on the creature (s_follow) and tells the
    /// player; it never asks whether the creature is commandable, and the game looking after it picks the follow up again
    /// from the record whenever it loads it (UpdateSavedFollowTarget). So for the press of a chosen tame, the creature is
    /// commandable for the one call (InteractPatch), and the game does the rest: the pet, the command, the stats, the
    /// message, and every game, with House Trained or without, follows the record. Nothing else of the game changes.
    /// </summary>
    internal static class Following
    {
        /// <summary>A tame's mark that this part told it to stay: written by the game that looks after it when the game's
        /// command runs there (StayMarkPatch), so "Tames in pens" keeps its spot as it keeps a wolf's. Permanent: it's in
        /// players' worlds.</summary>
        internal static readonly int StayHash = "modprojects.housetrained.stay".GetStableHashCode();

        /// <summary>The tame made commandable for this one press (InteractPatch, between its prefix and its finalizer), so
        /// what runs inside the press (the game's command on this game, StayMarkPatch) still sees the prefab's own flag
        /// (code review 1, F2).</summary>
        internal static Tameable Opened;

        /// <summary>Whether the game itself commands this tame (its prefab: only the wolf), not the one press opened here.</summary>
        internal static bool CommandableByGame(Tameable tameable) => tameable.m_commandable && !ReferenceEquals(tameable, Opened);

        /// <summary>Whether E on this tame commands it where the game would only pet it: chosen and the part on, or, where
        /// the part wouldn't, to make one left following stay.</summary>
        internal static bool CommandsHere(Tameable tameable) => CommandedHere(tameable) || Undone(tameable);

        // Chosen and the part on.
        private static bool CommandedHere(Tameable tameable)
        {
            Character creature = tameable.GetComponent<Character>();
            if (creature == null)
            {
                return false;
            }
            TameInfo info = Tames.Of(creature);
            return FollowRules.Commands(Parts.FollowOn, tameable.IsTamed(), tameable.GetComponent<MonsterAI>() != null, CommandableByGame(tameable),
                                        info.Follows, info.Summon, Settings.FollowSummons.Value);
        }

        // Following, where the part wouldn't command it: E makes it stay.
        private static bool Undone(Tameable tameable)
        {
            Character creature = tameable.GetComponent<Character>();
            if (creature == null)
            {
                return false;
            }
            TameInfo info = Tames.Of(creature);
            return FollowRules.Undoes(Parts.ModuleOn, tameable.IsTamed(), tameable.GetComponent<MonsterAI>() != null, CommandableByGame(tameable),
                                      FollowsSomeone(tameable), info.Summon);
        }

        /// <summary>Whether a tame's patrol point is a stay a player gave, for "Tames in pens": a wolf's, or one this part
        /// marked; the mark read again after a second (it's asked at every patrol point lookup).</summary>
        internal static bool ToldToStay(Character tame, TameInfo info)
        {
            if (info.Commandable)
            {
                return true;
            }
            float now = Time.time;
            if (now >= info.StayMarkUntil)
            {
                info.StayMarkUntil = now + 1f;
                ZNetView view = tame.GetComponent<ZNetView>();
                ZDO record = view != null && view.IsValid() ? view.GetZDO() : null;
                info.StayMark = record != null && record.GetBool(StayHash);
            }
            return FollowRules.ToldToStay(false, info.StayMark);
        }

        /// <summary>Whether the tame follows someone on this game, the one that runs its AI (before the game's command).</summary>
        internal static bool FollowingHere(Tameable tameable)
        {
            MonsterAI ai = tameable != null ? tameable.GetComponent<MonsterAI>() : null;
            return ai != null && ai.GetFollowTarget() != null;
        }

        /// <summary>After the game's command ran on this game, the owner (Tameable.RPC_Command): a tame the game doesn't
        /// command is marked as told to stay, or not, as it now stays or follows; only when the command changed which
        /// (it does nothing when the player isn't loaded here: code review 1, F3), and only a change is written.</summary>
        internal static void AfterCommand(Tameable tameable, bool followedBefore)
        {
            if (tameable == null || CommandableByGame(tameable))
            {
                return;
            }
            ZNetView view = tameable.GetComponent<ZNetView>();
            if (view == null || !view.IsValid() || !view.IsOwner() || tameable.GetComponent<MonsterAI>() == null)
            {
                return;
            }
            bool follows = FollowingHere(tameable);
            if (follows == followedBefore)
            {
                return;
            }
            ZDO record = view.GetZDO();
            if (record.GetBool(StayHash) != !follows)
            {
                record.Set(StayHash, !follows);
            }
            Character creature = tameable.GetComponent<Character>();
            if (creature != null)
            {
                Tames.Of(creature).StayMarkUntil = 0f;
            }
        }

        /// <summary>Whether a press of E on this tame follows and stays, by the game or here (for its hover text).</summary>
        internal static bool Follows(Tameable tameable) =>
            FollowRules.Follows(CommandableByGame(tameable) && tameable.IsTamed() && tameable.GetComponent<MonsterAI>() != null, CommandsHere(tameable));

        /// <summary>Whether the tame follows someone now, on any game: the name the game that looks after it wrote on it.</summary>
        internal static bool FollowsSomeone(Tameable tameable)
        {
            ZNetView view = tameable.GetComponent<ZNetView>();
            ZDO record = view != null && view.IsValid() ? view.GetZDO() : null;
            return record != null && !string.IsNullOrEmpty(record.GetString(ZDOVars.s_follow));
        }

        /// <summary>The tame's hover text with E's word: Follow, or Stay while it follows.</summary>
        internal static string Hover(Tameable tameable, string hover)
        {
            // With the part off, a follower whose E makes it stay says so too (code review 1, F11).
            bool show = Parts.FollowOn ? Settings.FollowHover.Value && Follows(tameable) : Undone(tameable);
            if (!show || !tameable.IsTamed())
            {
                return hover;
            }
            string pet = Localization.instance != null ? Localization.instance.Localize("$hud_pet") : "Pet";
            return FollowRules.Reworded(hover, pet, FollowRules.Word(FollowsSomeone(tameable)));
        }

        /// <summary>A saddle's hover text: its key's line, when one is set, for a tame it commands.</summary>
        internal static string SaddleHover(Sadle saddle, string hover)
        {
            Tameable tameable = saddle.GetTameable();
            string key = Keys.Text(Settings.FollowSaddleKey.Value);
            if (!Parts.FollowOn || tameable == null || key.Length == 0 || !Follows(tameable) || SameKeySendsHome(tameable))
            {
                return hover;
            }
            return hover + FollowRules.KeyLine(key, FollowRules.Word(FollowsSomeone(tameable)));
        }

        // With "Return to base" on the same key, off the ground it sends the tame home instead (Ground.Tick).
        private static bool SameKeySendsHome(Tameable tameable) =>
            Settings.GroundReturnKey.Value.Equals(Settings.FollowSaddleKey.Value) && Ground.ReturnApplies(tameable);

        /// <summary>Whether the game's use of a saddle is held back now: its key held by the local player, at a tame it
        /// commands (the key's own press commands it, Tick).</summary>
        internal static bool SkipSaddle(Sadle saddle, Humanoid user)
        {
            if (!Parts.FollowOn || user == null || user != Player.m_localPlayer || !Keys.Held(Settings.FollowSaddleKey.Value))
            {
                return false;
            }
            Tameable tameable = saddle.GetTameable();
            return tameable != null && Follows(tameable);
        }

        /// <summary>Every frame, from the module's Update: the saddle's key, pressed while pointing at a saddle.</summary>
        internal static void Tick()
        {
            if (!Parts.FollowOn || !Keys.Pressed(Settings.FollowSaddleKey.Value) || !Keys.TakesKeys())
            {
                return;
            }
            Player player = Player.m_localPlayer;
            GameObject hovering = player.GetHoverObject();
            Sadle saddle = hovering != null ? hovering.GetComponentInParent<Sadle>() : null;
            Tameable tameable = saddle != null ? saddle.GetTameable() : null;
            if (tameable != null && tameable.IsTamed() && Follows(tameable) && !SameKeySendsHome(tameable))
            {
                // The game's own command, as E on a wolf sends it (its message tells him which).
                tameable.Command(player);
            }
        }
    }
}
