using System;
using UnityEngine;

namespace Terraformer.Trees
{
    /// <summary>
    /// The grown tree's keys, read right after the game's own placement input each frame (a postfix on
    /// Player.UpdatePlacement), with a sapling in hand:
    /// - **The preview key** shows the tree while held, or turns it on and off ("Hold or toggle").
    /// - **Previous and next tree** (Q and E) step through the trees the sapling can grow into, while it shows.
    /// - **The sharpness key and the wheel** (Terraformer's "Sharpness key", Right Alt by default) set the tree's size
    ///   within the sapling's range, 5 % of it a notch,
    ///   with "The tree's size" on "Picked with the key"; while it's held the game's own turn of the sapling is undone for
    ///   that frame and the camera doesn't zoom (<see cref="Adjusting"/>, asked by Controls.Adjusting).
    /// Each pick is remembered per sapling (TreeMemory) and said where "Messages" says.
    /// </summary>
    internal static class TreeControls
    {
        // The game steps its build turn when the wheel adds up past this; a notch here is the same (as Controls).
        private const float NotchThreshold = 0.1f;

        private static float wheelSum;
        private static bool toggledOn;
        private static bool wasBuilding;

        /// <summary>A sapling the preview works for, read once from its prefab: the trees it can grow into and its sizes.
        /// A plant that grows against a wall (the vines: the game places the grown plant where it finds the wall, as it
        /// grows) or that has no grown tree isn't one.</summary>
        internal sealed class Sapling
        {
            internal Piece Piece;
            internal Plant Plant;
            internal string Name;
            internal GameObject[] Trees;
            internal string[] TreeNames;
            internal string[] TreesShown;
            internal float Min;
            internal float Max;
            internal float GrowRadius;

            internal int Count => Trees.Length;
        }

        private static Piece cachedPiece;
        private static Sapling cachedSapling;
        private static Sapling placedFor;

        /// <summary>The sapling this piece is, or null.</summary>
        internal static Sapling SaplingOf(Piece piece)
        {
            if (piece == null)
            {
                return null;
            }
            if (ReferenceEquals(piece, cachedPiece))
            {
                return cachedSapling;
            }
            cachedPiece = piece;
            cachedSapling = Read(piece);
            return cachedSapling;
        }

        private static Sapling Read(Piece piece)
        {
            Plant plant = piece.GetComponent<Plant>();
            if (plant == null || plant.m_attachDistance > 0f || plant.m_grownPrefabs == null)
            {
                return null;
            }
            int count = 0;
            foreach (GameObject tree in plant.m_grownPrefabs)
            {
                if (tree != null)
                {
                    count++;
                }
            }
            // A list with a gap would put the lock's index out of step with the game's: such a sapling isn't previewed.
            if (count == 0 || count != plant.m_grownPrefabs.Length)
            {
                return null;
            }
            var sapling = new Sapling
            {
                Piece = piece,
                Plant = plant,
                Name = piece.gameObject.name,
                Trees = plant.m_grownPrefabs,
                TreeNames = new string[count],
                TreesShown = new string[count],
                Min = Math.Min(plant.m_minScale, plant.m_maxScale),
                Max = Math.Max(plant.m_minScale, plant.m_maxScale),
                GrowRadius = plant.m_growRadius,
            };
            for (int i = 0; i < count; i++)
            {
                sapling.TreeNames[i] = plant.m_grownPrefabs[i].name;
                sapling.TreesShown[i] = NameOf(plant.m_grownPrefabs[i]);
            }
            return sapling;
        }

        // A grown tree's name as the game shows it on hover ("Birch"; a crop by its item), else its prefab's name.
        private static string NameOf(GameObject tree)
        {
            try
            {
                Hoverable hover = tree.GetComponent<Hoverable>();
                string name = hover?.GetHoverName();
                if (!string.IsNullOrEmpty(name))
                {
                    return Localize(name);
                }
            }
            catch (Exception)
            {
                // A hover name that needs the object in the world; the prefab's name will do.
            }
            return tree.name;
        }

        private static string Localize(string text) =>
            Localization.instance != null ? Localization.instance.Localize(text) : text;

        /// <summary>Which of the sapling's trees is picked: the one he last showed, else the first.</summary>
        internal static int IndexOf(Sapling sapling)
        {
            string tree = TreeMemory.Of(sapling.Name)?.Tree;
            int index = tree != null ? Array.IndexOf(sapling.TreeNames, tree) : -1;
            return index >= 0 ? index : 0;
        }

        /// <summary>The size the sapling would be locked to now (null: the game picks), from "The tree's size".</summary>
        internal static float? SizeOf(Sapling sapling) =>
            TreeRules.SizeFor(TreeSettings.Size.Value, TreeMemory.Of(sapling.Name)?.Size, sapling.Min, sapling.Max);

        /// <summary>What the preview does now (TreeRules.ActionFor): shown with Terraformer on, building with a sapling in
        /// hand, the menu closed, the key held (or toggled on) and the sapling's ghost placed; hidden, its copies kept, when
        /// only the ghost is missing (aiming at the sky or past building range); else cleared.</summary>
        internal static PreviewAction StateOf(Player player, out Sapling sapling)
        {
            sapling = null;
            bool local = player != null && player == Player.m_localPlayer;
            bool on = Module.IsOn && !TreeHooks.Lost && local;
            bool building = on && player.InPlaceMode() && !player.IsDead();
            bool menuOpen = building && Hud.IsPieceSelectionVisible();
            sapling = building && !menuOpen ? SaplingOf(player.GetSelectedPiece()) : null;
            bool keyOn = sapling != null && KeyOn();
            GameObject ghost = keyOn ? GameHooks.PlacementGhost(player) : null;
            return TreeRules.ActionFor(on, building, menuOpen, sapling != null, keyOn, ghost != null && ghost.activeSelf);
        }

        /// <summary>Whether the grown tree shows now (<see cref="StateOf"/> says Show).</summary>
        internal static bool Showing(Player player, out Sapling sapling) => StateOf(player, out sapling) == PreviewAction.Show;

        /// <summary>Whether the grown tree shows now for this very piece.</summary>
        internal static bool ShowingFor(Player player, Piece piece) =>
            Showing(player, out Sapling sapling) && ReferenceEquals(sapling.Piece, piece);

        private static bool KeyOn() =>
            TreeSettings.Mode.Value == PreviewMode.Toggle ? toggledOn : Settings.Held(TreeSettings.PreviewKey);

        /// <summary>The pick a click with this sapling writes into its record.</summary>
        internal static Pick PickToWrite(Piece piece)
        {
            Sapling sapling = SaplingOf(piece);
            if (sapling == null)
            {
                return default;
            }
            return TreeRules.PickToWrite(TreeSettings.LockTree.Value, TreeSettings.LockFacing.Value,
                                         sapling.TreeNames[IndexOf(sapling)], SizeOf(sapling));
        }

        /// <summary>Whether the size key is held with the tree showing: then the wheel sets its size, and the camera doesn't
        /// zoom (asked by Controls.Adjusting).</summary>
        internal static bool Adjusting(Player player) =>
            TreeSettings.Size != null && TreeSettings.Size.Value == TreeSizeChoice.PickedWithTheKey &&
            Settings.Held(Settings.SharpnessKey) && Showing(player, out _);

        /// <summary>What the build bar adds after this piece's name while its tree shows, or null. Made once per sapling,
        /// tree, size and lock (BarCache), not every frame.</summary>
        internal static string BarText(Player player, Piece piece)
        {
            if (!TreeSettings.NameInBar.Value || !Showing(player, out Sapling sapling) || !ReferenceEquals(sapling.Piece, piece))
            {
                return null;
            }
            int index = IndexOf(sapling);
            // No lambda here: a closure would be made on every call, every frame in build mode (review round 2, finding 2).
            return Bar.Get(sapling, index, SizeOf(sapling), TreeSettings.LockTree.Value, sapling.TreesShown[index], sapling.Count,
                           sapling.Min, sapling.Max);
        }

        private static readonly BarCache Bar = new BarCache();

        /// <summary>
        /// Whether the build turn is kept across the game's remaking of the ghost (a prefix on Player.SetupPlacementGhost;
        /// TreeRules.KeepsTurn): with "Keep my turn" on, while the tree shows or a click's pick still waits for its saplings.
        /// </summary>
        internal static bool KeepsTurn(Player player) =>
            player != null && player == Player.m_localPlayer &&
            TreeRules.KeepsTurn(Module.IsOn && !TreeHooks.Lost, TreeSettings.KeepTurn.Value, Showing(player, out _), TreeLock.Waiting);

        /// <summary>Whether the kept turn goes back once the ghost is remade: only with a sapling still in hand.</summary>
        internal static bool RestoresTurn(Player player, bool kept) =>
            TreeRules.RestoresTurn(kept && Module.IsOn, player != null && SaplingOf(player.GetSelectedPiece()) != null);

        /// <summary>After the game has placed the sapling's ghost this frame (a postfix on Player.UpdatePlacementGhost, which
        /// the game runs every frame): the grown tree shown there, turned as the ghost is, at the size it will grow to (the
        /// largest when the game picks it), with the ring of its grow space if asked; hidden while the game hides its ghost;
        /// else gone.</summary>
        internal static void Place(Player player)
        {
            PreviewAction action = StateOf(player, out Sapling sapling);
            if (action == PreviewAction.Clear)
            {
                TreeGhost.Clear();
                placedFor = null;
                return;
            }
            if (action == PreviewAction.Hide)
            {
                TreeGhost.Hide();
                return;
            }
            if (!ReferenceEquals(sapling, placedFor))
            {
                TreeGhost.Clear();   // another sapling picked while it shows: the last one's trees go at once
                placedFor = sapling;
            }
            Transform ghost = GameHooks.PlacementGhost(player).transform;
            TreeGhost.Show(sapling.Trees[IndexOf(sapling)], ghost.position, ghost.rotation, SizeOf(sapling) ?? sapling.Max,
                           TreeSettings.Opacity.Value);
            TreeGhost.ShowRing(ghost.position, TreeSettings.GrowSpace.Value ? sapling.GrowRadius : (float?)null);
        }

        /// <summary>After the game's own placement input, this frame.</summary>
        internal static void Frame(Player player, bool takeInput, int turnBefore)
        {
            if (!Module.IsOn || TreeHooks.Lost || player != Player.m_localPlayer || !player.InPlaceMode())
            {
                wheelSum = 0f;
                return;
            }
            Sapling sapling = SaplingOf(player.GetSelectedPiece());
            if (sapling == null || !takeInput || Hud.IsPieceSelectionVisible())
            {
                wheelSum = 0f;
                return;
            }
            float now = Time.unscaledTime;
            if (TreeSettings.Mode.Value == PreviewMode.Toggle && Settings.Pressed(TreeSettings.PreviewKey))
            {
                toggledOn = !toggledOn;
                Say(player, toggledOn ? "Grown tree shown" : "Grown tree hidden");
            }
            if (!Showing(player, out _))
            {
                wheelSum = 0f;
                return;
            }

            int step = Settings.Pressed(TreeSettings.NextTreeKey) ? 1 : Settings.Pressed(TreeSettings.PreviousTreeKey) ? -1 : 0;
            if (step != 0 && sapling.Count > 1)
            {
                int index = TreeRules.StepTree(IndexOf(sapling), step, sapling.Count);
                TreeMemory.SetTree(sapling.Name, sapling.TreeNames[index], now);
                Say(player, $"{sapling.TreesShown[index]} {index + 1} of {sapling.Count}");
            }

            if (TreeSettings.Size.Value != TreeSizeChoice.PickedWithTheKey || !Settings.Held(Settings.SharpnessKey))
            {
                wheelSum = 0f;
                return;
            }
            // The wheel is the size's while the key is held: the game's own turn of the sapling is undone.
            if (GameHooks.PlaceRotation(player) != turnBefore)
            {
                GameHooks.PlaceRotation(player) = turnBefore;
            }
            int notch = Notch(ZInput.GetMouseScrollWheel());
            if (notch == 0)
            {
                return;
            }
            if (Settings.ReverseWheel.Value)
            {
                notch = -notch;
            }
            float size = TreeRules.StepSize(SizeOf(sapling) ?? sapling.Max, notch, sapling.Min, sapling.Max);
            TreeMemory.SetSize(sapling.Name, size, now);
            Say(player, $"Size {TreeRules.Percent(size)} ({TreeRules.Percent(sapling.Min)} to {TreeRules.Percent(sapling.Max)})");
        }

        // A notch of the wheel, as the game counts one: +1 away from him, -1 toward him, 0 not yet.
        private static int Notch(float wheel)
        {
            wheelSum += wheel;
            if (wheelSum > NotchThreshold)
            {
                wheelSum = 0f;
                return 1;
            }
            if (wheelSum < -NotchThreshold)
            {
                wheelSum = 0f;
                return -1;
            }
            return 0;
        }

        // Says a pick where "Messages" says (Terraformer's one setting for it).
        private static void Say(Player player, string text)
        {
            switch (Settings.Messages.Value)
            {
                case MessageChoice.Centre:
                    player.Message(MessageHud.MessageType.Center, text);
                    break;
                case MessageChoice.TopLeft:
                    player.Message(MessageHud.MessageType.TopLeft, text);
                    break;
            }
        }

        /// <summary>
        /// Every frame, also while switched off (Module.Update): the preview gone at its real ends (the key let go, another
        /// piece, the menu, out of building, Terraformer off) and only hidden while the game hides its ghost, the click's
        /// window shut while Terraformer is off, and the remembered picks written a few seconds after a change, or at once
        /// on leaving building.
        /// </summary>
        internal static void Tick()
        {
            float now = Time.unscaledTime;
            TreeMemory.LoadOnce();
            Player player = Player.m_localPlayer;
            switch (StateOf(player, out _))
            {
                case PreviewAction.Clear:
                    TreeGhost.Clear();
                    break;
                case PreviewAction.Hide:
                    TreeGhost.Hide();
                    break;
            }
            if (!Module.IsOn)
            {
                TreeLock.Forget();
                toggledOn = false;
            }
            TreeLock.Expire();
            bool building = player != null && player.InPlaceMode();
            TreeMemory.WriteIfDue(now, atOnce: wasBuilding && !building);
            wasBuilding = building;
        }
    }
}
