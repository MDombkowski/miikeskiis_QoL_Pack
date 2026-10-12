using QoLMods.Kit;
using UnityEngine;

namespace QuickFix
{
    /// <summary>Which of the two pieces a thing is.</summary>
    internal enum RepairPiece
    {
        None,
        GrindingWheel,
        SmithsAnvil,
    }

    /// <summary>
    /// The Grinding Wheel (forge_ext3) and the Smith's Anvil (forge_ext4): a press of the Use key repairs what each one's
    /// setting covers (decision 3), their hover text says so, and they can be placed with no Forge near (decision 4). Both
    /// are Forge extensions with no Interactable of their own (research part A, sections 4 to 6).
    /// </summary>
    internal static class Pieces
    {
        internal const string WheelName = "$piece_forge_ext3";
        internal const string AnvilName = "$piece_forge_ext4";

        internal static RepairPiece Which(Piece piece)
        {
            if (piece == null)
            {
                return RepairPiece.None;
            }
            switch (piece.m_name)
            {
                case WheelName:
                    return RepairPiece.GrindingWheel;
                case AnvilName:
                    return RepairPiece.SmithsAnvil;
                default:
                    return RepairPiece.None;
            }
        }

        /// <summary>The piece a hovered collider belongs to, when it's one of the two.</summary>
        internal static RepairPiece Which(GameObject hovered, out StationExtension extension)
        {
            extension = hovered != null ? hovered.GetComponentInParent<StationExtension>() : null;
            return extension != null ? Which(extension.GetComponent<Piece>()) : RepairPiece.None;
        }

        internal static WhatItRepairs Setting(RepairPiece piece) =>
            piece == RepairPiece.SmithsAnvil ? Settings.Anvil.Value : Settings.Wheel.Value;

        /// <summary>The piece's name as the game shows it ("Grinding Wheel", "Smith's Anvil" in English).</summary>
        internal static string NameOf(RepairPiece piece)
        {
            string token = piece == RepairPiece.SmithsAnvil ? AnvilName : WheelName;
            string english = piece == RepairPiece.SmithsAnvil ? "Smith's Anvil" : "Grinding Wheel";
            string shown = Localization.instance != null ? Localization.instance.Localize(token) : null;
            return string.IsNullOrEmpty(shown) || shown.StartsWith("[") ? english : shown;
        }

        internal static string WhatItDoes(WhatItRepairs what)
        {
            switch (what)
            {
                case WhatItRepairs.MetallicGear:
                    return "metallic gear";
                case WhatItRepairs.All:
                    return "all gear";
                default:
                    return "non-metallic gear";
            }
        }

        /// <summary>The line added under the piece's name while hovering it.</summary>
        internal static string HoverLine(RepairPiece piece)
        {
            string key = Localization.instance != null
                ? Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]")
                : "[E]";
            return $"\n{key} Repair {WhatItDoes(Setting(piece))}";
        }

        /// <summary>Repairs, to full, everything in his inventory the piece's setting covers, and, when something was
        /// repaired, plays the Forge's repair sound at the piece (once, not per item: it is heard by players near). With
        /// <paramref name="speak"/>, one message says what happened; without, the caller says it.</summary>
        internal static int Repair(Player player, RepairPiece piece, StationExtension extension, bool speak)
        {
            WhatItRepairs what = Setting(piece);
            int mended = Gear.MendAll(player, (item, recipe) => Rules.Covers(what, Gear.IsMetallic(recipe)));
            if (mended > 0)
            {
                CraftingStation forge = extension != null ? extension.m_craftingStation : null;
                if (forge != null && forge.m_repairItemDoneEffects != null)
                {
                    forge.m_repairItemDoneEffects.Create(extension.transform.position, Quaternion.identity);
                }
                if (speak)
                {
                    Say.Center($"The {NameOf(piece)} repaired {Gear.Items(mended)}");
                }
            }
            else if (speak)
            {
                Say.Center($"Nothing to repair: the {NameOf(piece)} repairs {WhatItDoes(what)}");
            }
            return mended;
        }

        /// <summary>Called after the game has worked out where the build ghost may go: for the two pieces, when a Forge
        /// missing is the only thing wrong, they may go there (the game's later checks only ever write their own failure,
        /// so ExtensionMissingStation at the end means nothing else failed: research part A, section 5). The same call
        /// decides the real placement (TryPlacePiece calls it again first).</summary>
        internal static void AllowWithoutForge(Player player)
        {
            if (!Settings.PlaceAnywhere.Value || GameHooks.PlacementStatus(player) != Player.PlacementStatus.ExtensionMissingStation)
            {
                return;
            }
            GameObject ghost = GameHooks.PlacementGhost(player);
            Piece piece = ghost != null ? ghost.GetComponent<Piece>() : null;
            if (Which(piece) == RepairPiece.None)
            {
                return;
            }
            GameHooks.SetPlacementStatus(player, Player.PlacementStatus.Valid);
            piece.SetInvalidPlacementHeightlight(false);
        }
    }
}
