namespace HouseTrained
{
    /// <summary>Who dealt a hit on a building, for the buildings part.</summary>
    internal enum SpareAttacker
    {
        /// <summary>A player, a wild creature, a tame not chosen, or nobody known: the game decides.</summary>
        NotCovered,
        ChosenTame,
        /// <summary>A creature a staff called up that the game doesn't count as tamed (the Trollstav's troll, the Staff of
        /// the Wild's roots): of the Players or PlayerSpawned faction, not a player, not tamed.</summary>
        Summon,
    }

    /// <summary>What a hit landed on, for the buildings part.</summary>
    internal enum SpareTarget
    {
        NotCovered,
        PlayerBuilt,
        ShipOrCart,
        WorldOwn,
        Crop,
    }

    /// <summary>The buildings part's settings as plain values, read once per hit.</summary>
    internal struct SpareChoices
    {
        internal bool PlayerBuilt;
        internal bool Ships;
        internal bool World;
        internal bool Crops;
        internal bool Summons;
        internal bool Trample;
        internal int SharePercent;
    }

    /// <summary>
    /// Tames spare buildings, as plain rules (decisions 2 to 4, 9 to 12): which hits are spared, and how much of a hit
    /// still lands. The patches gather the facts; this decides. The harness checks every combination.
    /// </summary>
    internal static class SpareRules
    {
        /// <summary>Who a hit's attacker is: a player is never covered; a tame only when chosen (every tame, or one on the
        /// list); an untamed creature of the players' factions is a summon.</summary>
        internal static SpareAttacker AttackerOf(bool isPlayer, bool isTamed, bool chosen, bool playersFaction)
        {
            if (isPlayer)
            {
                return SpareAttacker.NotCovered;
            }
            if (isTamed)
            {
                return chosen ? SpareAttacker.ChosenTame : SpareAttacker.NotCovered;
            }
            return playersFaction ? SpareAttacker.Summon : SpareAttacker.NotCovered;
        }

        /// <summary>What a building piece is: a ship or a cart by its own switch, else a piece a player placed (its
        /// creator isn't 0), else the world's own.</summary>
        internal static SpareTarget PieceOf(bool shipOrCart, long creator) =>
            shipOrCart ? SpareTarget.ShipOrCart : creator != 0L ? SpareTarget.PlayerBuilt : SpareTarget.WorldOwn;

        /// <summary>Whether the hit is spared (wholly, at 0%, or down to the share where it lands).</summary>
        internal static bool Spares(SpareAttacker attacker, bool trample, SpareTarget target, in SpareChoices choices)
        {
            if (attacker == SpareAttacker.NotCovered || (attacker == SpareAttacker.Summon && !choices.Summons) ||
                (trample && !choices.Trample))
            {
                return false;
            }
            switch (target)
            {
                case SpareTarget.PlayerBuilt:
                    return choices.PlayerBuilt;
                case SpareTarget.ShipOrCart:
                    return choices.Ships;
                case SpareTarget.WorldOwn:
                    return choices.World;
                case SpareTarget.Crop:
                    return choices.Crops;
                default:
                    return false;
            }
        }

        /// <summary>What part of a spared hit still lands: 0 to 1 (decision 9: applied once, where the hit lands).</summary>
        internal static float Factor(int sharePercent) => sharePercent <= 0 ? 0f : sharePercent >= 100 ? 1f : sharePercent / 100f;
    }

    /// <summary>What becomes of a hit on a character while friendly fire is on.</summary>
    internal enum FriendlyOutcome
    {
        /// <summary>Not a friendly hit (or one this part leaves to the game): as the game has it.</summary>
        AsTheGame,
        /// <summary>A friendly hit dropped: it isn't sent.</summary>
        Drop,
        /// <summary>A friendly hit sent, its damage scaled by the share (1: as it is).</summary>
        Scale,
    }

    /// <summary>The facts about one hit on a character, gathered where it is sent.</summary>
    internal struct FriendlyFacts
    {
        /// <summary>An untamed, non-player creature dealt it.</summary>
        internal bool WildAttacker;
        internal bool AttackerIsBoss;
        internal bool AttackerInNever;
        /// <summary>Friendly fire may come from this attacker now ("only when every player nearby runs the pack").</summary>
        internal bool AttackerMayDeal;
        internal bool TargetIsSelf;
        internal bool TargetIsPlayer;
        internal bool TargetIsTamed;
        /// <summary>The game's own BaseAI.IsEnemy(attacker, target): true means an ordinary hit, not friendly fire.</summary>
        internal bool TargetIsEnemy;
        /// <summary>The two are of one kind (the same Character.m_name, the game's own "same" test).</summary>
        internal bool SameKind;
    }

    /// <summary>The friendly fire part's settings that judge a hit, as plain values.</summary>
    internal struct FriendlyChoices
    {
        internal bool OwnKind;
        internal bool Bosses;
        internal bool HurtTames;
        internal int SharePercent;
    }

    /// <summary>
    /// Friendly fire, as a plain rule (decisions 14 to 16, 19): a hit from a wild creature on a character that isn't its
    /// enemy is a friendly hit; players never take one, his tames only by their setting, its own kind only by its setting,
    /// and the share scales the rest once, where the hit is sent. The game's own friendly fire is judged the same way
    /// while the part is on, and left alone while it's off (the patch doesn't ask then).
    /// </summary>
    internal static class FriendlyRules
    {
        /// <summary>Whether a creature deals friendly fire now (decisions 7, 16, 21, 42): a wild one (not a player, a tame
        /// or a staff's summon), a boss only by its setting, not on the list, and with "only when every player nearby runs
        /// the pack", only while they all do.</summary>
        internal static bool Deals(bool wild, bool boss, bool bossesOn, bool never, bool onlyAll, bool everyoneNearbyDeals) =>
            wild && (!boss || bossesOn) && !never && (!onlyAll || everyoneNearbyDeals);

        /// <summary>Whether an attack's own copy is one to hit friends: an area attack by its switch, a wide swing (melee
        /// that hits several things) by its own (decision 17); an attack already hitting friends (the game's own) is left
        /// as it is. Whether its attacker deals friendly fire is asked after (Deals), only of these.</summary>
        internal static bool FlagsAttack(Attack.AttackType type, bool multiHit, bool alreadyFriendly, bool areaOn, bool meleeOn)
        {
            if (alreadyFriendly)
            {
                return false;
            }
            bool area = type == Attack.AttackType.Area && areaOn;
            bool swing = (type == Attack.AttackType.Horizontal || type == Attack.AttackType.Vertical) && multiHit && meleeOn;
            return area || swing;
        }

        /// <summary>Whether an area object set up by its spawner is one to hit friends: one that damages (never a heal or
        /// buff cloud), not a trample, by its switch. Its owner is asked after (Deals).</summary>
        internal static bool FlagsArea(bool hitEnemy, bool trample, bool objectsOn) => objectsOn && hitEnemy && !trample;

        /// <summary>A projectile's two flags for one blast (decision 44): with "exploding projectiles' blasts" on and a
        /// shooter that deals friendly fire, friends are hit and harmed; with it off, a flight flag this mod set ("projectiles
        /// stop on friends") is lowered for the blast; otherwise as they were.</summary>
        internal static void ForBlast(bool blastsOn, bool deals, bool ours, ref bool hitFriendly, ref bool noDamageFriendly)
        {
            if (blastsOn)
            {
                if (deals)
                {
                    hitFriendly = true;
                    noDamageFriendly = false;
                }
            }
            else if (hitFriendly && ours)
            {
                hitFriendly = false;
            }
        }

        internal static FriendlyOutcome Judge(in FriendlyFacts facts, in FriendlyChoices choices, out float factor)
        {
            factor = 1f;
            if (!facts.WildAttacker || facts.TargetIsSelf || facts.TargetIsEnemy)
            {
                return FriendlyOutcome.AsTheGame;
            }
            // Players and his tames first, whatever else: an attack or area object flagged while every player nearby ran the
            // pack can still be hitting after that stopped (code review 1, finding 3). The game itself never sends a
            // friendly hit to a player with its attacker named, so this drop changes nothing of the game's.
            if (facts.TargetIsPlayer || (facts.TargetIsTamed && !choices.HurtTames))
            {
                return FriendlyOutcome.Drop;
            }
            if (!facts.AttackerMayDeal)
            {
                return FriendlyOutcome.AsTheGame;
            }
            if ((facts.SameKind && !choices.OwnKind) || facts.AttackerInNever || (facts.AttackerIsBoss && !choices.Bosses) || choices.SharePercent <= 0)
            {
                return FriendlyOutcome.Drop;
            }
            factor = choices.SharePercent / 100f;
            return FriendlyOutcome.Scale;
        }
    }
}
