using System;
using System.Collections.Generic;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// Terraformer's tools as pieces of the game (research C):
    /// - **Made once per game session,** as copies of the game's own terrain pieces, under a hidden holder of its own that
    ///   is never active (a terrain piece that wakes up applies its stroke at once) and never unloaded.
    /// - **Registered after every rebuild of the game's list of terrain strokes** (a postfix on ObjectDB.UpdateRegisters,
    ///   which the game runs in the main menu and at every world load), into both its list and its lookup, so a stroke of
    ///   Terraformer's is found wherever it's applied. They stay registered while Terraformer is switched off, so a stroke
    ///   already on its way still applies.
    /// - **Put into the hoe's and cultivator's menus, and taken out, by reference,** after the game's own piece they follow,
    ///   whenever the switches say so (checked every frame, so a change in F1 shows at once), and the menu of a tool in his
    ///   hand is refreshed as the game refreshes its own.
    /// </summary>
    internal static class Pieces
    {
        private static GameObject holder;
        private static bool made;
        private static bool warnedUnmade;
        private static readonly HashSet<string> WarnedCollisions = new HashSet<string>();

        // The game's own pieces the copies need, by name, found when the copies are made.
        private static TerrainOp digPrefab;
        private static CraftingStation workbench;

        // Raise ground's cost and station, for Slope's "Costs as Raise ground" (B02).
        private static Piece raisePiece;

        private static readonly Piece.Requirement[] NoCost = new Piece.Requirement[0];

        /// <summary>After the game (re)built its list of terrain strokes: make the copies if they aren't yet, then
        /// register them.</summary>
        internal static void AfterRegistry(ObjectDB db)
        {
            if (db == null || db.m_terrainOps == null || db.m_terrainOps.Count == 0)
            {
                return;
            }
            if (!made)
            {
                Make(db);
            }
            if (made)
            {
                Register(db);
            }
        }

        private static void Make(ObjectDB db)
        {
            var bases = new Dictionary<string, TerrainOp>();
            foreach (TerrainOp op in db.m_terrainOps)
            {
                if (op != null && !bases.ContainsKey(op.gameObject.name))
                {
                    bases[op.gameObject.name] = op;
                }
            }
            bases.TryGetValue("digg_v3", out digPrefab);
            raisePiece = bases.TryGetValue("raise_v2", out TerrainOp raise) ? raise.GetComponent<Piece>() : null;

            var missing = new List<string>();
            foreach (Tool tool in Tool.All)
            {
                if (!bases.ContainsKey(tool.Base) && !missing.Contains(tool.Base))
                {
                    missing.Add(tool.Base);
                }
            }
            if (missing.Count > 0)
            {
                if (!warnedUnmade)
                {
                    warnedUnmade = true;
                    Module.Log.LogWarning($"The game's terrain pieces {string.Join(", ", missing)} weren't found, so Terraformer's " +
                                          "tools copied from them aren't made. The game's own tools still resize.");
                }
            }

            holder = new GameObject("Terraformer_Tools");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);

            foreach (Tool tool in Tool.All)
            {
                if (!bases.TryGetValue(tool.Base, out TerrainOp source))
                {
                    continue;
                }
                GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, holder.transform, false);
                copy.name = tool.Prefab;
                tool.Object = copy;
                tool.Piece = copy.GetComponent<Piece>();
                tool.Op = copy.GetComponent<TerrainOp>();
                if (tool.Piece == null || tool.Op == null)
                {
                    tool.Object = null;
                    UnityEngine.Object.Destroy(copy);
                    continue;
                }
                if (tool.IsPrecisionRaise || tool.Digs)
                {
                    Piece basePiece = source.GetComponent<Piece>();
                    if (basePiece != null && basePiece.m_craftingStation != null)
                    {
                        workbench = basePiece.m_craftingStation;
                    }
                }
                Sprite own = source.GetComponent<Piece>()?.m_icon;
                Sprite shape = tool.PictureFrom != null && bases.TryGetValue(tool.PictureFrom, out TerrainOp from) ? from.GetComponent<Piece>()?.m_icon : null;
                Shape(tool, source, own, shape);
            }
            made = true;
            Module.Log.LogInfo($"Terraformer made its {CountMade()} tools.");
        }

        private static int CountMade()
        {
            int count = 0;
            foreach (Tool tool in Tool.All)
            {
                if (tool.Object != null)
                {
                    count++;
                }
            }
            return count;
        }

        // The copy's own name, words, picture and settings.
        private static void Shape(Tool tool, TerrainOp source, Sprite own, Sprite shape)
        {
            Piece piece = tool.Piece;
            piece.m_name = tool.Name;
            piece.m_description = Description(tool);
            piece.m_icon = Icons.Make(own, shape, tool.Picture, tool.Mark);
            TerrainOp.Settings settings = tool.Op.m_settings;
            tool.Adjust?.Invoke(settings);
            if (tool.Square || tool.IsSlope)
            {
                // The turn travels with the stroke (research C and D); the game's own paint ignores it without m_sides.
                // The game's round marker stays on the copy: the ghost hides it while the square's own outline or the
                // grid shows, and shows it with both off (Ghost).
                settings.m_rotation = true;
                settings.m_sides = false;
                settings.m_square = false;
            }
            if (tool.Digs)
            {
                piece.m_resources = new Piece.Requirement[0];
                piece.m_craftingStation = null;
                // It digs: the pickaxe dig's effects, as a list of its own (so its dust switch never reaches the
                // pickaxe's; Dust), and the raise marker turned to point down.
                if (digPrefab != null)
                {
                    tool.Op.m_onPlacedEffect = Dust.Copy(digPrefab.m_onPlacedEffect);
                }
                Transform marker = tool.Object.transform.Find("_GhostOnly");
                if (marker != null)
                {
                    marker.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    marker.localPosition += new Vector3(0f, 2f, 0f);
                }
            }
            Follow(tool);
        }

        private static string Description(Tool tool)
        {
            string text = char.ToUpperInvariant(tool.EnglishDescription[0]) + tool.EnglishDescription.Substring(1);
            if (tool.IsSlope)
            {
                text += " Square, the wheel turns it and the width key and the wheel make it a rectangle (with two clicks it " +
                        "turns toward where you aim); the radius key and the wheel resize it, round or square; the sharpness " +
                        "key and the wheel soften or sharpen its edge.";
            }
            else if (tool.Square)
            {
                text += " The wheel turns it; the radius key and the wheel resize it" +
                        (tool.IsPrecisionRaise ? "; the sharpness key and the wheel set the lift." :
                         tool.IsPrecisionLower ? "; the sharpness key and the wheel set the depth." : ".");
            }
            else
            {
                text += " The radius key and the wheel resize it" +
                        (tool.IsLowerGround || tool.Flattens ? "; the sharpness key and the wheel sharpen it." : ".");
            }
            return text + " (Terraformer)";
        }

        /// <summary>Settings that live on the copies themselves follow the settings: whether square tools take the wheel's
        /// turn, and the two Lower grounds' workbench and stone.</summary>
        private static void Follow(Tool tool)
        {
            if (tool.Piece == null)
            {
                return;
            }
            if (tool.Square || tool.IsSlope)
            {
                // Slope turns only as a square of its own (not round, not a two-click strip, which turns toward the aim).
                bool turns = Settings.Turn.Value != TurnChoice.DontTurn &&
                             (!tool.IsSlope || (tool.Square && SlopeTool.Way.Value != SlopeWay.TwoClicks));
                if (tool.Piece.m_canRotate != turns)
                {
                    tool.Piece.m_canRotate = turns;
                }
            }
            if (tool.IsSlope)
            {
                // Free and needing no workbench, as Flatten; "Costs as Raise ground" asks Raise ground's stone and bench.
                bool costs = SlopeTool.CostsAsRaise.Value && raisePiece != null;
                Piece.Requirement[] resources = costs ? raisePiece.m_resources : NoCost;
                CraftingStation station = costs ? raisePiece.m_craftingStation : null;
                if (tool.Piece.m_resources != resources)
                {
                    tool.Piece.m_resources = resources;
                }
                if (tool.Piece.m_craftingStation != station)
                {
                    tool.Piece.m_craftingStation = station;
                }
            }
            if (tool.Digs)
            {
                CraftingStation station = Settings.LowerNeedsWorkbench.Value ? workbench : null;
                if (tool.Piece.m_craftingStation != station)
                {
                    tool.Piece.m_craftingStation = station;
                }
                GameObject stone = Settings.LowerDropsStone.Value && digPrefab != null ? digPrefab.m_spawnOnPlaced : null;
                if (tool.Op.m_spawnOnPlaced != stone)
                {
                    tool.Op.m_spawnOnPlaced = stone;
                    tool.Op.m_chanceToSpawn = digPrefab != null ? digPrefab.m_chanceToSpawn : 0.33f;
                    tool.Op.m_maxSpawned = 1;
                    tool.Op.m_spawnAtMaxLevelDepth = false;
                }
            }
        }

        private static void Register(ObjectDB db)
        {
            foreach (Tool tool in Tool.All)
            {
                if (tool.Op == null)
                {
                    continue;
                }
                // A name another mod's piece has already is left to that piece: checked before the copy joins the list,
                // since the game rebuilds its lookup from the list at the next world load (review round 1).
                int hash = db.GetPrefabHash(tool.Object);
                if (db.m_terrainOpsByHash.TryGetValue(hash, out TerrainOp other) && other != null && other != tool.Op)
                {
                    if (WarnedCollisions.Add(tool.Prefab))
                    {
                        Module.Log.LogWarning($"Another mod's terrain piece has the name {tool.Prefab} already, so Terraformer's " +
                                              $"{tool.EnglishName} can't be used.");
                    }
                    continue;
                }
                if (!db.m_terrainOps.Contains(tool.Op))
                {
                    db.m_terrainOps.Add(tool.Op);
                }
                db.m_terrainOpsByHash[hash] = tool.Op;
            }
        }

        /// <summary>Every frame: the menus follow the switches, and the copies follow their settings.</summary>
        internal static void Sync()
        {
            if (!made)
            {
                return;
            }
            ObjectDB db = ObjectDB.instance;
            if (db == null)
            {
                return;
            }
            List<PieceTable> changed = null;
            foreach (Tool tool in Tool.All)
            {
                if (tool.Object == null)
                {
                    continue;
                }
                Follow(tool);
                PieceTable table = TableOf(db, tool.Table);
                if (table == null || table.m_pieces == null)
                {
                    continue;
                }
                bool wanted = Module.IsOn && Settings.ToolsInMenus.Value && Settings.IsSwitchedOn(tool);
                bool there = table.m_pieces.Contains(tool.Object);
                if (wanted == there)
                {
                    continue;
                }
                if (wanted)
                {
                    table.m_pieces.Insert(PlaceFor(table, tool), tool.Object);
                }
                else
                {
                    table.m_pieces.Remove(tool.Object);
                }
                (changed ??= new List<PieceTable>()).Add(table);
            }
            if (changed != null)
            {
                Refresh(changed);
            }
        }

        /// <summary>Takes every tool out of the menus (switching off, or after an error).</summary>
        internal static void TakeOut()
        {
            ObjectDB db = ObjectDB.instance;
            if (db == null)
            {
                return;
            }
            var changed = new List<PieceTable>();
            foreach (Tool tool in Tool.All)
            {
                PieceTable table = tool.Object != null ? TableOf(db, tool.Table) : null;
                if (table != null && table.m_pieces != null && table.m_pieces.Remove(tool.Object))
                {
                    changed.Add(table);
                }
            }
            if (changed.Count > 0)
            {
                Refresh(changed);
            }
        }

        private static PieceTable TableOf(ObjectDB db, ToolTable which)
        {
            GameObject item = db.GetItemPrefab(which == ToolTable.Hoe ? "Hoe" : "Cultivator");
            return item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces : null;
        }

        // Right after the piece it follows; if that isn't there, after the one that one follows, and so on back to the
        // game's own; if none is there, after the menu's last terrain piece.
        private static int PlaceFor(PieceTable table, Tool tool)
        {
            string after = tool.After;
            for (int guard = 0; after != null && guard < Tool.All.Length + 2; guard++)
            {
                for (int i = 0; i < table.m_pieces.Count; i++)
                {
                    GameObject piece = table.m_pieces[i];
                    if (piece != null && piece.name == after)
                    {
                        return i + 1;
                    }
                }
                after = Tool.Named(after)?.After;
            }
            int last = -1;
            for (int i = 0; i < table.m_pieces.Count; i++)
            {
                if (table.m_pieces[i] != null && table.m_pieces[i].GetComponent<TerrainOp>() != null)
                {
                    last = i;
                }
            }
            return last + 1;
        }

        // What the game itself does when a menu's pieces change (research C, section 4): learn the new pieces, rebuild the
        // held tool's lists and ghost, keep the piece he had picked, and redraw an open build menu.
        private static void Refresh(List<PieceTable> changed)
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }
            PieceTable held = player.GetBuildTool();
            Piece picked = held != null && changed.Contains(held) ? held.GetSelectedPiece() : null;
            GameHooks.UpdateKnownRecipesList(player);
            if (held == null || !changed.Contains(held))
            {
                return;
            }
            player.SetBuildCategory(held.GetSelectedCategory());
            if (picked != null)
            {
                Tool gone = Tool.Named(picked.gameObject.name);
                if (gone != null && !held.m_pieces.Contains(picked.gameObject))
                {
                    GameObject back = held.m_pieces.Find(piece => piece != null && piece.name == gone.Base);
                    picked = back != null ? back.GetComponent<Piece>() : null;
                }
                if (picked != null)
                {
                    player.SetSelectedPiece(picked);
                }
            }
            if (Hud.IsPieceSelectionVisible() && Hud.instance != null && Hud.instance.m_buildUi != null)
            {
                Hud.instance.m_buildUi.OpenBuildMenu();
            }
        }
    }
}
