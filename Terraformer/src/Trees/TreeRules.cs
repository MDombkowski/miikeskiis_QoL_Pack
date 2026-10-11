using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Terraformer.Trees
{
    /// <summary>
    /// What a sapling is locked to, as its own record carries it: the grown tree (by its prefab's name, hashed), its size
    /// and its turn. A value that's null was not locked, and the game picks it at random when the sapling grows, as ever.
    /// </summary>
    internal readonly struct Pick
    {
        internal Pick(int? tree, float? size, float? turn)
        {
            Tree = tree;
            Size = size;
            Turn = turn;
        }

        /// <summary>The grown tree's prefab name, hashed as the game hashes prefab names (GetStableHashCode).</summary>
        internal int? Tree { get; }

        /// <summary>The tree's size: its scale, as the game sets it (1 = the tree's own size).</summary>
        internal float? Size { get; }

        /// <summary>How far the tree turns from the sapling's own facing, in degrees; 0 = exactly the sapling's.</summary>
        internal float? Turn { get; }

        internal bool IsEmpty => Tree == null && Size == null && Turn == null;
    }

    /// <summary>A sapling's record, as the lock reads and writes it: a ZDO in the game (TreeLock.ZdoRecord), a dictionary
    /// in a harness.</summary>
    internal interface IRecord
    {
        bool TryInt(int key, out int value);

        bool TryFloat(int key, out float value);

        void SetInt(int key, int value);

        void SetFloat(int key, float value);
    }

    /// <summary>
    /// The rules of the grown tree's preview and lock, on plain values (no Unity), so a harness can run them:
    /// - **The record's keys** and how a pick is written into a sapling's record and read back.
    /// - **The grow:** each of the game's three random draws, or the locked value in its place.
    /// - **The window** in which a fresh sapling gets the pick of the click that placed it.
    /// - **The size's steps**, and what the build bar says.
    /// </summary>
    internal static class TreeRules
    {
        /// <summary>The record's keys, hashed as the game hashes every key (GetStableHashCode, from the game's own
        /// utilities). The names are permanent once released: a sapling planted with one version grows with the next.</summary>
        internal const string TreeKeyName = "modprojects.terraformer.tree";

        internal const string SizeKeyName = "modprojects.terraformer.size";

        internal const string TurnKeyName = "modprojects.terraformer.turn";

        internal static int TreeKey => TreeKeyName.GetStableHashCode();

        internal static int SizeKey => SizeKeyName.GetStableHashCode();

        internal static int TurnKey => TurnKeyName.GetStableHashCode();

        /// <summary>The game turns a grown tree up to this far either way from its sapling's facing (Plant.Grow).</summary>
        internal const float GameTurn = 11.25f;

        /// <summary>The size key and the wheel step through a sapling's range in this many steps (5 % of it a notch).</summary>
        internal const int SizeSteps = 20;

        /// <summary>A fresh sapling of the clicked prefab still gets the click's pick this long after the click, or after
        /// the last sapling that got it: PlantEasily places a grid's extra saplings a few a frame after the click.</summary>
        internal const float QuietSeconds = 3f;

        /// <summary>…and this many frames: the window stays open while either is still short.</summary>
        internal const int QuietFrames = 30;

        /// <summary>Writes a pick into a sapling's record: only the values it holds, so "Lock its facing" off writes no
        /// turn, and "as the game picks" no size.</summary>
        internal static void Write(IRecord record, Pick pick)
        {
            if (pick.Tree.HasValue)
            {
                record.SetInt(TreeKey, pick.Tree.Value);
            }
            if (pick.Size.HasValue)
            {
                record.SetFloat(SizeKey, pick.Size.Value);
            }
            if (pick.Turn.HasValue)
            {
                record.SetFloat(TurnKey, pick.Turn.Value);
            }
        }

        /// <summary>What a sapling's record is locked to (empty for a sapling planted without the preview).</summary>
        internal static Pick Read(IRecord record)
        {
            int? tree = record.TryInt(TreeKey, out int hash) ? hash : (int?)null;
            float? size = record.TryFloat(SizeKey, out float scale) && IsNumber(scale) ? scale : (float?)null;
            float? turn = record.TryFloat(TurnKey, out float degrees) && IsNumber(degrees) ? degrees : (float?)null;
            return new Pick(tree, size, turn);
        }

        /// <summary>Whether the record already carries a lock (a sapling is never locked twice).</summary>
        internal static bool IsLocked(IRecord record) => record.TryInt(TreeKey, out _);

        /// <summary>Which of the sapling's trees grows: the locked one, found by its name's hash among the trees the game
        /// would pick from (min to max − 1), else the game's own draw with the game's own arguments.</summary>
        internal static int ChooseTree(int min, int max, Pick pick, IList<string> names, Func<int, int, int> draw)
        {
            if (pick.Tree.HasValue && names != null)
            {
                int index = IndexOf(pick.Tree.Value, names);
                if (index >= min && index < max)
                {
                    return index;
                }
            }
            return draw(min, max);
        }

        /// <summary>The place of the tree whose name hashes to <paramref name="hash"/>, or -1.</summary>
        internal static int IndexOf(int hash, IList<string> names)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (names[i] != null && names[i].GetStableHashCode() == hash)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The tree's turn from its sapling's facing: the locked one, held to the game's own range, else the
        /// game's draw.</summary>
        internal static float ChooseTurn(float min, float max, Pick pick, Func<float, float, float> draw) =>
            pick.Turn.HasValue ? Clamp(pick.Turn.Value, min, max) : draw(min, max);

        /// <summary>The tree's size: the locked one, held to the sapling's own range (so a lock never makes a tree the game
        /// couldn't), else the game's draw.</summary>
        internal static float ChooseSize(float min, float max, Pick pick, Func<float, float, float> draw) =>
            pick.Size.HasValue ? Clamp(pick.Size.Value, min, max) : draw(min, max);

        /// <summary>Held between the two ends, whichever way round they are.</summary>
        internal static float Clamp(float value, float a, float b)
        {
            float low = Math.Min(a, b);
            float high = Math.Max(a, b);
            return value < low ? low : value > high ? high : value;
        }

        internal static bool IsNumber(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>The size after a notch of the wheel: a step of 5 % of the sapling's range, landing on the steps counted
        /// from its smallest, held to the range.</summary>
        internal static float StepSize(float size, int notch, float min, float max)
        {
            if (max <= min)
            {
                return min;
            }
            float step = (max - min) / SizeSteps;
            double place = Math.Round((Clamp(size, min, max) - min) / step) + notch;
            place = Math.Max(0, Math.Min(SizeSteps, place));
            return place >= SizeSteps ? max : (float)(min + place * step);
        }

        /// <summary>The size a pick uses, from the setting and what he picked with the key (null: the largest, where the
        /// key starts). Null for "as the game picks".</summary>
        internal static float? SizeFor(TreeSizeChoice choice, float? picked, float min, float max)
        {
            switch (choice)
            {
                case TreeSizeChoice.AsTheGamePicks:
                    return null;
                case TreeSizeChoice.Smallest:
                    return Math.Min(min, max);
                case TreeSizeChoice.Largest:
                    return Math.Max(min, max);
                default:
                    return picked.HasValue ? Clamp(picked.Value, min, max) : Math.Max(min, max);
            }
        }

        /// <summary>The pick a click writes, from the settings: nothing at all with "Lock the tree it becomes" off; the tree
        /// always; the size unless the game picks it; a turn of 0 with "Lock its facing" on.</summary>
        internal static Pick PickToWrite(bool lockTree, bool lockFacing, string treeName, float? size)
        {
            if (!lockTree || string.IsNullOrEmpty(treeName))
            {
                return default;
            }
            return new Pick(treeName.GetStableHashCode(), size, lockFacing ? 0f : (float?)null);
        }

        /// <summary>A size as the build bar and messages say it: its scale in percent ("110 %").</summary>
        internal static string Percent(float size) =>
            Math.Round(size * 100f).ToString("0", CultureInfo.CurrentCulture) + " %";

        /// <summary>What the build bar adds after the sapling's name: the tree (and which of how many), its size and its
        /// range, and "preview only" when nothing will be locked.</summary>
        internal static string BarText(string treeName, int index, int count, float? size, float min, float max, bool locks)
        {
            var text = new StringBuilder(" · ").Append(treeName);
            if (count > 1)
            {
                text.Append(' ').Append(index + 1).Append(" of ").Append(count);
            }
            string range = max > min ? $" ({Percent(min)} to {Percent(max)})" : string.Empty;
            text.Append(" · ").Append(size.HasValue ? Percent(size.Value) : "random size").Append(range);
            if (!locks)
            {
                text.Append(" · preview only");
            }
            return text.ToString();
        }

        /// <summary>The tree after a press of the previous (-1) or next (+1) key, going round.</summary>
        internal static int StepTree(int index, int step, int count)
        {
            if (count <= 0)
            {
                return 0;
            }
            int next = (index + step) % count;
            return next < 0 ? next + count : next;
        }

        /// <summary>The build turn the game leaves for the next sapling after a click: with "Keep my turn" on and the preview
        /// showing at the click, the turn it had before the click (the game draws a new one at random); else the game's.</summary>
        internal static int TurnAfterPlacing(int before, int after, bool previewing, bool keepTurn) =>
            previewing && keepTurn ? before : after;

        /// <summary>
        /// Whether the turn is kept across the game's remaking of the sapling's ghost (Player.SetupPlacementGhost, which
        /// gives a sapling a new random turn every time: after each planting, since using up the seed changes the
        /// inventory, and on any other change of it, such as picking something up; review round 1, finding 1). Kept with
        /// Terraformer on and "Keep my turn" on, while the tree shows or a click's pick still waits for its saplings
        /// (PlantEasily's grid, whose coroutine remakes the ghost after its last sapling).
        /// </summary>
        internal static bool KeepsTurn(bool on, bool keepTurn, bool showing, bool waiting) => on && keepTurn && (showing || waiting);

        /// <summary>Whether the kept turn is put back once the ghost is remade: only while the piece in hand is still a
        /// sapling (another piece takes the game's own turn).</summary>
        internal static bool RestoresTurn(bool kept, bool saplingInHand) => kept && saplingInHand;

        /// <summary>
        /// What the preview does this frame (review round 1, finding 2):
        /// - **Clear** (every copy destroyed) at the real ends: Terraformer off, out of building, the build menu open, no
        ///   sapling in hand, the key off.
        /// - **Hide** while all of those hold but the game hides its ghost (aiming at the sky or past building range): the
        ///   copies are kept, so sweeping along the edge of range doesn't rebuild them every frame.
        /// - **Show** otherwise.
        /// </summary>
        internal static PreviewAction ActionFor(bool on, bool building, bool menuOpen, bool saplingInHand, bool keyOn, bool ghostShown)
        {
            if (!on || !building || menuOpen || !saplingInHand || !keyOn)
            {
                return PreviewAction.Clear;
            }
            return ghostShown ? PreviewAction.Show : PreviewAction.Hide;
        }

        /// <summary>
        /// What a click (Player.PlacePiece) does to the lock window (review round 1, finding 4a): a sapling planted with the
        /// tree showing arms it with that click's pick; a sapling planted without it shuts it; any other piece (the hammer,
        /// a piece an attack places) leaves it as it is, so a grid still being planted keeps its pick.
        /// </summary>
        internal static ClickAction ClickFor(bool sapling, bool previewing) =>
            !sapling ? ClickAction.Leave : previewing ? ClickAction.Arm : ClickAction.Disarm;

        /// <summary>What the one bar patch adds after the piece's name: the tree's text (a sapling with its tree showing),
        /// else "paint only"'s (a painting tool with paint only on), else nothing. Never both: a piece is a sapling or a
        /// painting tool, and the tree's text goes first if ever both were offered.</summary>
        internal static string BarSuffix(string tree, string paintOnly) => tree ?? paintOnly;
    }

    internal enum PreviewAction
    {
        Clear,
        Hide,
        Show,
    }

    internal enum ClickAction
    {
        Leave,
        Disarm,
        Arm,
    }

    /// <summary>
    /// The click's pick, waiting for the saplings that click makes. Armed as the game places a sapling with the preview
    /// showing; every fresh sapling of the same prefab that appears while it's armed (the game's own, and the extra ones
    /// PlantEasily places a few a frame after) takes it. Shut by the next sapling planted without the preview, or once no
    /// sapling has taken it for <see cref="TreeRules.QuietSeconds"/> AND <see cref="TreeRules.QuietFrames"/> frames (so a
    /// save's hitch of several seconds mid-grid doesn't shut it; review round 1, finding 4c).
    /// Known and accepted (finding 4b): PlantEasily's replant on harvest makes the crop's sapling fresh on his game, so a
    /// replant of the same crop within the window takes the click's pick (its size and facing: a crop's range is only
    /// 0.9 to 1.1, so nothing he'd notice).
    /// </summary>
    internal sealed class LockWindow
    {
        private int prefab;
        private Pick pick;
        private float last;
        private int lastFrame;
        private bool armed;

        internal bool Armed => armed;

        internal int Prefab => prefab;

        /// <summary>A click with the preview showing: its saplings get this pick.</summary>
        internal void Arm(int prefabHash, Pick pickToWrite, float now, int frame)
        {
            armed = !pickToWrite.IsEmpty;
            prefab = prefabHash;
            pick = pickToWrite;
            last = now;
            lastFrame = frame;
        }

        internal void Disarm() => armed = false;

        /// <summary>Whether a click's saplings may still be coming (review round 2, finding 1): armed, and the quiet seconds
        /// and quiet frames since the click or the last sapling not both past. What "Keep my turn" and the fresh test ask,
        /// so neither outlasts the click.</summary>
        internal bool Open(float now, int frame) =>
            armed && !(now - last > TreeRules.QuietSeconds && frame - lastFrame > TreeRules.QuietFrames);

        /// <summary>Shuts the window once it's past (every frame, from TreeControls.Tick).</summary>
        internal void Expire(float now, int frame)
        {
            if (armed && !Open(now, frame))
            {
                armed = false;
            }
        }

        /// <summary>The pick for a sapling as it wakes, or null: only a fresh one (just placed by this game, which owns it),
        /// of the clicked prefab, not locked yet, while the window is open. Each one that takes it keeps the window open.</summary>
        internal Pick? Take(int prefabHash, bool fresh, bool alreadyLocked, float now, int frame)
        {
            if (!armed)
            {
                return null;
            }
            if (!Open(now, frame))
            {
                armed = false;
                return null;
            }
            if (!fresh || alreadyLocked || prefabHash != prefab)
            {
                return null;
            }
            last = now;
            lastFrame = frame;
            return pick;
        }
    }

    /// <summary>
    /// The build bar's text for the tree shown, made once for each sapling, tree, size and lock and kept while they stay
    /// the same, rather than a new string every frame (review round 1, finding 7).
    /// </summary>
    internal sealed class BarCache
    {
        private object sapling;
        private int index = -1;
        private float? size;
        private bool locks;
        private string text;

        internal string Get(object forSapling, int forIndex, float? forSize, bool forLocks, Func<string> make)
        {
            if (Stale(forSapling, forIndex, forSize, forLocks))
            {
                text = make();
            }
            return text;
        }

        /// <summary>The same, made from the values themselves (TreeRules.BarText), with no closure to allocate each frame
        /// (review round 2, finding 2): what the bar uses.</summary>
        internal string Get(object forSapling, int forIndex, float? forSize, bool forLocks, string treeShown, int count, float min, float max)
        {
            if (Stale(forSapling, forIndex, forSize, forLocks))
            {
                text = TreeRules.BarText(treeShown, forIndex, count, forSize, min, max, forLocks);
            }
            return text;
        }

        // Whether the text kept is for something else; if so, the new key is kept.
        private bool Stale(object forSapling, int forIndex, float? forSize, bool forLocks)
        {
            if (text == null || !ReferenceEquals(forSapling, sapling) || forIndex != index || forSize != size || forLocks != locks)
            {
                sapling = forSapling;
                index = forIndex;
                size = forSize;
                locks = forLocks;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Each sapling's pick, remembered by the sapling's prefab name: the tree he last showed (by the grown prefab's name)
    /// and the size he set. Written as one line, "Birch_Sapling:t=Birch2,s=0.85;Beech_Sapling:s=1.5", with a full stop
    /// for decimals whatever the computer's language.
    /// </summary>
    internal sealed class TreeChoice
    {
        internal string Tree;
        internal float? Size;

        internal bool IsEmpty => string.IsNullOrEmpty(Tree) && Size == null;

        internal static string Format(IDictionary<string, TreeChoice> choices)
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, TreeChoice> pair in choices)
            {
                if (pair.Value == null || pair.Value.IsEmpty || !Plain(pair.Key))
                {
                    continue;
                }
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(pair.Value.Tree) && Plain(pair.Value.Tree))
                {
                    parts.Add("t=" + pair.Value.Tree);
                }
                if (pair.Value.Size.HasValue && TreeRules.IsNumber(pair.Value.Size.Value))
                {
                    parts.Add("s=" + pair.Value.Size.Value.ToString("0.####", CultureInfo.InvariantCulture));
                }
                if (parts.Count == 0)
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append(';');
                }
                text.Append(pair.Key).Append(':').Append(string.Join(",", parts));
            }
            return text.ToString();
        }

        internal static Dictionary<string, TreeChoice> Parse(string text)
        {
            var choices = new Dictionary<string, TreeChoice>();
            if (string.IsNullOrEmpty(text))
            {
                return choices;
            }
            foreach (string item in text.Split(';'))
            {
                int colon = item.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }
                string sapling = item.Substring(0, colon).Trim();
                var choice = new TreeChoice();
                foreach (string part in item.Substring(colon + 1).Split(','))
                {
                    int equals = part.IndexOf('=');
                    if (equals <= 0)
                    {
                        continue;
                    }
                    string key = part.Substring(0, equals).Trim();
                    string value = part.Substring(equals + 1).Trim();
                    if (key == "t" && value.Length > 0)
                    {
                        choice.Tree = value;
                    }
                    else if (key == "s" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float size) &&
                             TreeRules.IsNumber(size))
                    {
                        choice.Size = size;
                    }
                }
                if (sapling.Length > 0 && !choice.IsEmpty)
                {
                    choices[sapling] = choice;
                }
            }
            return choices;
        }

        // A name the line can carry: none of its separators.
        private static bool Plain(string name) => !string.IsNullOrEmpty(name) && name.IndexOfAny(new[] { ':', ';', ',', '=' }) < 0;
    }
}
