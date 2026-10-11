using System.Collections.Generic;
using Hoarstone.Shapes;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// The Hoarstone (A88, his design of 2026-10-10): an entry of its own in the hammer's menu, next to the Ward, which
    /// places a real vanilla Ward marked as a Hoarstone in its saved record and switched off. A world object of a kind the
    /// host doesn't know is deleted by the host (LESSONS_LEARNED), so the world only ever holds a Ward:
    /// - **In games with this mod** a marked Ward is no ward at all (unless "It's also a ward" is on, or it was switched
    ///   on): its own name, hover text and E, its tint, and taking it down gives back the Hoarstone's cost.
    /// - **To everyone else** it's a Ward that's switched off: it blocks no one, and only its placer could switch it on.
    /// This class keeps the menu entry and the tint; the patches are in StonePatches.
    /// </summary>
    internal static class Stone
    {
        internal const string EntryName = "Hoarstone";

        private const float TintEvery = 0.5f;

        private static GameObject holder;
        private static GameObject entry;
        private static Piece wardPiece;
        private static float wardRadius = 32f;
        private static readonly int WardHash = Records.WardPrefab.GetStableHashCode();
        private static string costFor;
        private static Piece.Requirement[] cost = new Piece.Requirement[0];
        private static float nextTint;
        private static readonly HashSet<GameObject> tinted = new HashSet<GameObject>();

        /// <summary>The menu entry's object, a copy of the Ward under a holder that is never active (so it never wakes
        /// and never makes a record); null until made.</summary>
        internal static GameObject Entry => entry;

        /// <summary>The game's own Ward, which a Hoarstone entry places.</summary>
        internal static Piece WardPiece => wardPiece;

        /// <summary>Whether a Ward is marked as a Hoarstone.</summary>
        internal static bool IsMarked(PrivateArea ward)
        {
            ZNetView view = ward != null ? GameHooks.WardView(ward) : null;
            ZDO zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo != null && zdo.GetBool(Records.MarkHash);
        }

        internal static bool IsMarked(ZNetView view)
        {
            ZDO zdo = view != null && view.IsValid() ? view.GetZDO() : null;
            return zdo != null && zdo.GetPrefab() == WardHash && zdo.GetBool(Records.MarkHash);
        }

        /// <summary>Whether a Ward is switched on (its record's "enabled").</summary>
        internal static bool IsSwitchedOn(PrivateArea ward)
        {
            ZNetView view = GameHooks.WardView(ward);
            return view != null && view.IsValid() && view.GetZDO().GetBool(ZDOVars.s_enabled);
        }

        /// <summary>Whether a marked Ward is a Ward in this game now (rather than a Hoarstone): with "It's also a ward"
        /// on, or switched on (so its placer can switch it off).</summary>
        internal static bool ActsAsWard(PrivateArea ward) => Settings.AlsoAWard.Value || IsSwitchedOn(ward);

        /// <summary>The Hoarstone's cost as requirements, from the setting (the Ward's own where it gives nothing usable).</summary>
        internal static Piece.Requirement[] Cost()
        {
            string text = Settings.Cost.Value ?? "";
            if (text == costFor && cost.Length > 0)
            {
                return cost;
            }
            if (ObjectDB.instance == null)
            {
                return cost;
            }
            var problems = new List<string>();
            List<Piece.Requirement> made = Requirements(CostText.Parse(text, problems), problems);
            if (made.Count == 0)
            {
                made = Requirements(CostText.Parse(CostText.WardCost, null), null);
                Module.Log.LogWarning("The Hoarstone's cost gives nothing usable, so it costs what a Ward does.");
            }
            if (problems.Count > 0)
            {
                Module.Log.LogWarning($"Left out of the Hoarstone's cost, as no item goes by that name or the amount can't be read: {string.Join(", ", problems)}.");
            }
            costFor = text;
            cost = made.ToArray();
            return cost;
        }

        /// <summary>Requirements as a cost text (the setting's form), for the record of what a Hoarstone cost.</summary>
        internal static string TextOf(Piece.Requirement[] requirements)
        {
            var parts = new List<string>();
            foreach (Piece.Requirement requirement in requirements ?? new Piece.Requirement[0])
            {
                if (requirement?.m_resItem != null && requirement.m_amount > 0)
                {
                    parts.Add($"{requirement.m_resItem.gameObject.name}:{requirement.m_amount}");
                }
            }
            return string.Join(", ", parts);
        }

        /// <summary>What taking a Hoarstone down gives back: what it cost when it was placed (written on its record),
        /// or, for one placed before that was written, the cost setting.</summary>
        internal static Piece.Requirement[] PaidFor(ZDO zdo)
        {
            string paid = zdo != null ? zdo.GetString(Records.PaidHash) : "";
            if (!string.IsNullOrEmpty(paid) && ObjectDB.instance != null)
            {
                List<Piece.Requirement> made = Requirements(CostText.Parse(paid, null), null);
                if (made.Count > 0)
                {
                    return made.ToArray();
                }
            }
            return Cost();
        }

        private static List<Piece.Requirement> Requirements(List<(string Name, int Amount)> entries, List<string> problems)
        {
            var made = new List<Piece.Requirement>();
            foreach ((string name, int amount) in entries)
            {
                GameObject prefab = ObjectDB.instance.GetItemPrefab(name);
                ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (item == null)
                {
                    problems?.Add(name);
                    continue;
                }
                made.Add(new Piece.Requirement { m_resItem = item, m_amount = amount, m_recover = true });
            }
            return made;
        }

        /// <summary>Every frame: the menu entry follows the switches and the cost; a few times a second the tints follow
        /// the Hoarstones loaded.</summary>
        internal static void Tick(float now)
        {
            Menu.Sync();
            if (now >= nextTint)
            {
                nextTint = now + TintEvery;
                TintAll();
            }
        }

        /// <summary>Makes the menu entry, once per game session, from the game's own Ward.</summary>
        internal static bool Make()
        {
            if (entry != null)
            {
                return true;
            }
            if (ZNetScene.instance == null || ObjectDB.instance == null)
            {
                return false;
            }
            GameObject ward = ZNetScene.instance.GetPrefab(Records.WardPrefab);
            wardPiece = ward != null ? ward.GetComponent<Piece>() : null;
            PrivateArea wardArea = ward != null ? ward.GetComponent<PrivateArea>() : null;
            if (wardPiece == null || wardArea == null)
            {
                return false;
            }
            wardRadius = wardArea.m_radius;
            if (holder == null)
            {
                holder = new GameObject("Hoarstone_Entries");
                holder.SetActive(false);
                Object.DontDestroyOnLoad(holder);
            }
            entry = Object.Instantiate(ward, holder.transform, false);
            entry.name = EntryName;
            Piece piece = entry.GetComponent<Piece>();
            piece.m_name = "Hoarstone";
            piece.m_description = "Marks a settlement: monsters don't spawn anywhere in its whole shape, worked out from the " +
                                  "pieces around it. To friends without this mod it's a Ward that's switched off.";
            piece.m_resources = Cost();
            FollowSettings();
            return true;
        }

        /// <summary>The menu entry follows the settings: its cost, and whether it's placed as a Ward would be. A Hoarstone
        /// that is no ward is placed by the game's rule for any piece (not inside a Ward that's on and not yours), not by
        /// a Ward's own (no Ward of another within its 32 m reach of it), and shows no Ward's circle while placed; with
        /// "It's also a ward" on, a Ward's rule and circle.</summary>
        internal static void FollowSettings()
        {
            if (entry == null)
            {
                return;
            }
            Piece piece = entry.GetComponent<Piece>();
            Piece.Requirement[] now = Cost();
            if (piece.m_resources != now)
            {
                piece.m_resources = now;
            }
            PrivateArea area = entry.GetComponent<PrivateArea>();
            bool ward = Settings.AlsoAWard.Value;
            float radius = ward ? wardRadius : 0f;
            if (area != null && area.m_radius != radius)
            {
                area.m_radius = radius;
            }
            if (area != null && area.m_areaMarker != null && area.m_areaMarker.gameObject.activeSelf != ward)
            {
                area.m_areaMarker.gameObject.SetActive(ward);
            }
            // The Hoarstone being placed now follows at once (review round 1), not only once it's picked again.
            Player player = Player.m_localPlayer;
            GameObject ghost = player != null && GameHooks.PlacementGhost != null ? GameHooks.PlacementGhost(player) : null;
            if (ghost != null && player.GetSelectedPiece() != null && player.GetSelectedPiece().gameObject == entry)
            {
                PrivateArea placing = ghost.GetComponent<PrivateArea>();
                if (placing != null && placing.m_radius != radius)
                {
                    placing.m_radius = radius;
                }
                if (placing != null && placing.m_areaMarker != null)
                {
                    placing.m_areaMarker.m_radius = radius;
                    if (placing.m_areaMarker.gameObject.activeSelf != ward)
                    {
                        placing.m_areaMarker.gameObject.SetActive(ward);
                    }
                }
            }
        }

        private static void TintAll()
        {
            if (MaterialMan.instance == null)
            {
                return;
            }
            Color tint = Settings.Tint.Value;
            bool want = Module.IsOn && !(tint.r >= 0.999f && tint.g >= 0.999f && tint.b >= 0.999f);
            var still = new HashSet<GameObject>();
            if (want)
            {
                foreach (PrivateArea ward in GameHooks.Wards())
                {
                    if (ward == null || !IsMarked(ward))
                    {
                        continue;
                    }
                    MaterialMan.instance.SetValue(ward.gameObject, ShaderProps._Color, tint);
                    still.Add(ward.gameObject);
                }
            }
            foreach (GameObject was in tinted)
            {
                if (was != null && !still.Contains(was))
                {
                    MaterialMan.instance.ResetValue(was, ShaderProps._Color);
                }
            }
            tinted.Clear();
            tinted.UnionWith(still);
        }

        /// <summary>Takes every tint off (switching off, or after an error).</summary>
        internal static void Untint()
        {
            if (MaterialMan.instance != null)
            {
                foreach (GameObject was in tinted)
                {
                    if (was != null)
                    {
                        MaterialMan.instance.ResetValue(was, ShaderProps._Color);
                    }
                }
            }
            tinted.Clear();
        }

        /// <summary>Takes the entry out of the hammer's menu (switching off, or after an error).</summary>
        internal static void TakeOut() => Menu.TakeOut();

        /// <summary>The hover text of a Hoarstone that is no ward: its name, its placer, its settlement, and E.</summary>
        internal static string HoverText(PrivateArea ward)
        {
            var text = new System.Text.StringBuilder(256);
            text.Append("Hoarstone");
            ZNetView view = GameHooks.WardView(ward);
            string placer = view != null && view.IsValid() ? view.GetZDO().GetString(ZDOVars.s_creatorName) : "";
            if (!string.IsNullOrEmpty(placer))
            {
                text.Append("\nPlaced by ").Append(placer);
            }
            text.Append('\n').Append(Outlines.Describe(ward.transform.position));
            bool shown = Outlines.IsShownAt(ward.transform.position);
            text.Append("\n[<color=yellow><b>$KEY_Use</b></color>] ").Append(shown ? "Hide its settlement's outline" : "Show its settlement's outline");
            return Localization.instance.Localize(text.ToString());
        }
    }
}
