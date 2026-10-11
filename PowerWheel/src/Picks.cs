using UnityEngine;

namespace PowerWheel
{
    /// <summary>What the game says about a power, for this character (PowerRules.PowerFacts).</summary>
    internal static class Powers
    {
        internal static PowerFacts FactsOf(Player player, string power)
        {
            string bossKey = PowerRules.BossKeyOf(power);
            return new PowerFacts
            {
                Name = power,
                IsCurrent = player.GetGuardianPowerName() == power,
                // The game's own record of a power taken at its stone: Player.SetGuardianPower writes the name there.
                TakenAtStone = player.HaveUniqueKey(power),
                BossBeaten = bossKey == null ? (bool?)null : ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(bossKey),
                // Read only when everything Trophies reads was found at start (a member gone in a game update would
                // otherwise fail where it's compiled, inside the key's guard).
                TrophyPlaced = GameHooks.TrophiesKnown && Trophies.Placed(player, power),
            };
        }

        /// <summary>The power's status effect in the game's ObjectDB, or null.</summary>
        internal static StatusEffect Find(string power)
        {
            if (ObjectDB.instance == null)
            {
                return null;
            }
            foreach (StatusEffect effect in ObjectDB.instance.m_StatusEffects)
            {
                if (effect != null && effect.name == power)
                {
                    return effect;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Picking a power in the wheel (each power's element's Interact, which the game's wheel calls on a click, the key let
    /// go, or the game's gamepad hover). The pick is the game's own Player.SetGuardianPower, the call its stones make, and
    /// only for a power the settings' rule says is earned, checked again here: that call also records the power as the
    /// character's own for good. The cooldown is never touched. "Use" is the game's own StartGuardianPower, with its own
    /// refusals and messages, so every other mod's patch on it still applies.
    /// </summary>
    internal static class Picks
    {
        /// <summary>The element's Interact; true when something happened. The wheel closes either way.</summary>
        internal static bool Choose(string power)
        {
            bool done = false;
            Module.Guard("picking a power", () => done = ChooseNow(power));
            return done;
        }

        /// <summary>A message where "Message" says (the top left one with the power's icon).</summary>
        internal static void Tell(Player player, string text, Sprite icon)
        {
            switch (Settings.Message.Value)
            {
                case MessageChoice.Centre:
                    player.Message(MessageHud.MessageType.Center, text);
                    break;
                case MessageChoice.TopLeft:
                    player.Message(MessageHud.MessageType.TopLeft, text, 0, icon);
                    break;
            }
        }

        private static bool ChooseNow(string power)
        {
            Via via = Controls.Via;
            Controls.Via = Via.Click;
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return false;
            }
            Outcome outcome = PowerRules.OutcomeOf(via, Settings.LetGo.Value, Settings.LeftClick.Value, Settings.RightClick.Value);
            if (outcome == Outcome.Nothing)
            {
                KeyLog.Line($"{power} chosen by {via}: its setting says to do nothing.");
                return false;
            }
            StatusEffect effect = Powers.Find(power);
            if (effect == null)
            {
                KeyLog.Line($"{power} chosen by {via}: it's gone from the game's list, so nothing changes.");
                return false;   // gone from the game meanwhile: picking it would leave him no power
            }

            EarnedChoice rule = Settings.Earned.Value;
            PowerFacts facts = Powers.FactsOf(player, power);
            PickResult result = PowerRules.Decide(facts, Settings.WhichPowers.Value, rule, player.m_guardianPowerCooldown > 0f,
                Settings.PickWhileCooling.Value);
            string shownName = Localization.instance.Localize(effect.m_name);
            KeyLog.Line($"{shownName} chosen by {via}: {result}" + (PowerRules.Uses(outcome, result, via) ? ", and the power is started (the game may still refuse it, while it cools down say)." : "."));
            switch (result)
            {
                case PickResult.Picked:
                    player.SetGuardianPower(power);
                    Tell(player, PowerRules.PickedText(shownName), effect.m_icon);
                    break;
                case PickResult.AlreadyCurrent:
                    break;
                case PickResult.Locked:
                    // Shown as a padlock alone (card B06), it stays nameless in the message too.
                    bool hidden = Settings.LockedLook.Value == LockedLookChoice.PadlockOnly;
                    Tell(player, PowerRules.LockedText(hidden ? null : shownName, rule, facts.BossBeaten != null), hidden ? null : effect.m_icon);
                    return false;
                default:
                    Tell(player, "$hud_powernotready", effect.m_icon);
                    return false;
            }
            if (PowerRules.Uses(outcome, result, via))
            {
                player.StartGuardianPower();
            }
            return true;
        }
    }
}
