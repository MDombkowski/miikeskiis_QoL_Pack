using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using QoLMods.Chests;
using QoLMods.ItemMarks;
using QoLMods.Kit;

namespace StoreStash
{
    /// <summary>
    /// StoreStash as a module of miikeskii's QoL Pack: the pack's own quick stack, replacing HexQuickStackStorage's, written
    /// from scratch (card A52, with A18's hotbar). P, or a button beside the inventory, puts what he carries into the chests
    /// nearby that already hold it, part-stacks first; trophies go to a chest holding a trophy; Place stacks and holding E
    /// at a chest do the same into that chest; favourites (Left Shift + right-click, a gold border) stay with him. H (and
    /// a button of its own, on out of the box since card A63) does the same into his personal chests only: the game's Personal Chests he
    /// built and chests he marked as his with Left Alt + H, which friends on the pack leave alone (card A43, built inside
    /// StoreStash at his pick). Every chest is written through the pack's chest service (QoLMods\parts\Chests): claimed from
    /// its owner first, added to before his stack is taken from, sent and held, so nothing is lost or copied with friends at
    /// the same chests. Since 0.3.0 (card A53, job 32) it also sorts his inventory and chests (src\Sorting) and has a trash
    /// can for item kinds he marks (src\Trash), each a part of its settings with its own switch. Since 0.4.0 (card A41, job
    /// 24) a chest can be set up to take only the kinds of things he picks (Setup*.cs, Setups.cs), and the quick stack
    /// follows the setups through the chest service; each feature's settings fold under one row in F1 (Folds.cs). Since
    /// 0.5.0 (his first look at chest setups) the settings fold in small groups too, "Which chest first" is a list of rules
    /// he drags into order (ChestOrderList, ChestOrderDrawer), and the message after a quick stack can say where things went
    /// in place of its usual line and stay a little longer per line (Messages, MessageHold); the setup window searches,
    /// renames the chest and takes things out with its marks or a right-click (SetupWindow, SetupName, SetupTyping,
    /// SetupMarks), and pairs biomes with families in either order. Since 0.6.0 (card A42, job 27, his pick to build it
    /// here) chests set up alike act as one chest, Stack Overflow (src\Overflow): a tidy, by a key (Left Alt + N), a button
    /// off out of the box, or by itself, moves things between them so each kind sits in as few of them as possible, through
    /// the chest service's move. It stays off while
    /// HexQuickStackStorage is installed. Client-side only. The same code runs in the bundle and in its single release
    /// (standalone\Plugin.cs); see QoLMods\MODULES.md.
    /// </summary>
    internal sealed class Module : QoLModule
    {
        /// <summary>The single release's plugin ID, which also names its settings file. Permanent.</summary>
        internal const string Guid = "modprojects.storestash";

        internal const string Name = "StoreStash";

        internal const string Version = "0.7.0";

        /// <summary>HexQuickStackStorage's plugin ID. With it running, this module stays off: two quick stacks on one key,
        /// and two replacements of Place stacks, would fight over the same items. The bundle and the single release list it
        /// as a soft dependency, so it has started, and can be seen, by the time this module looks.</summary>
        internal const string HexGuid = "com.hex.quickstackstorage";

        internal const string HexName = "HexQuickStackStorage";

        // Marks a rival in the missing list, so DescribeMissing can say what to do about it.
        private const string RivalMark = "rival:";

        private static Module current;

        internal Module()
        {
            current = this;
        }

        internal override string Id => "StoreStash";

        internal override string DisplayName => Name;

        internal override string ModuleVersion => Version;

        internal override string SingleGuid => Guid;

        internal override string EnabledDescription =>
            "Turns the mod on or off. Off: no quick stack keys or buttons, Place stacks and holding E are the game's own, no " +
            "sorting or trash can, no favourite or trash borders or marking, no chest setups and no tidying of chests set up " +
            "alike, and in your game a chest marked as yours or someone else's is like any other (your favourites, your trash " +
            "marks, the chests' marks and their setups are kept for when it's on again). On brings it all back.";

        internal override string SwitchedOffNote => "Place stacks and holding E work as in the game; your favourites are kept.";

        internal static ManualLogSource Log => current.Host.Log;

        /// <summary>True while the module runs and its Enabled switch is on. Every patch asks this first.</summary>
        internal static bool IsOn => current != null && current.Host != null && current.Host.IsOn;

        /// <summary>Runs one piece of the mod's work so that an error in it can never reach the game: the first error
        /// switches the mod off for the rest of the session instead.</summary>
        internal static void Guard(string task, Action work) => current.Host.Guard(task, work);

        /// <summary>This module, as the chest service knows it: "Chests beyond the server's reach" is the pack's shared
        /// setting (card A69), and its range is what it asks of Far Fetched on the server (card A70).</summary>
        internal static readonly ChestUser Chests = new ChestUser(Name, () => Log, Guard, () => IsOn,
            reach: () => Settings.Range == null ? 0f : System.Math.Max(ChestSettings.RangeOf(Settings.Range),
                OverflowSettings.Range != null ? OverflowSettings.Range.Value : 0f));

        internal override string UpdateTask => "doing its work each frame (its keys, buttons and marks, sorting, the trash can, tidying)";

        internal override void BindSettings(ModuleSettings settings) => Settings.Bind(settings);

        internal override void Resolve(List<string> missing, List<string> missingOptional)
        {
            // Once, after the bundle has taken a single release's settings over: 0.4.0's three switches into "Which chest
            // first". First, so it's done even when the module then can't start. Then the list's text written the one way.
            Settings.CarryOverOnce();
            Settings.PutOrderRight();
            if (Chainloader.PluginInfos.TryGetValue(HexGuid, out BepInEx.PluginInfo hex) && hex.Instance != null)
            {
                missing.Add(RivalMark + HexName);
            }
            ChestHooks.Resolve(missing, Chests);
            // Optional: without them (a game update), favourites can't be marked or shown, but the list still counts.
            ItemMarks.Resolve(missingOptional, FavoriteMarks.User);
            // Sorting and trash (A53): trash marks just inside the favourites'. The part's missing members were named once,
            // just above.
            ItemMarks.Resolve(new List<string>(), TrashMarks.User);
            GameHooks.Resolve(missing, missingOptional);
            SortHooks.Resolve(missingOptional);   // sorting and trash: without one, only what needs it stays off
            Folds.RebuildList = ManagerLink.Rebuild();   // null with a Jötunn too old to ask
        }

        internal override string DescribeMissing(IList<string> missing)
        {
            List<string> rivals = missing.Where(each => each.StartsWith(RivalMark, StringComparison.Ordinal))
                                         .Select(each => each.Substring(RivalMark.Length)).ToList();
            if (rivals.Count == 0)
            {
                return base.DescribeMissing(missing);
            }
            string names = string.Join(" and ", rivals);
            string text = $"{names} is installed, and two quick stacks, sorts and trash cans (and two kinds of Place stacks) would fight over the same items. " +
                          $"{Name} replaces it: uninstall {names} (or switch it off in the mod manager) and start the game again to use {Name}; " +
                          "its favourites are taken in the first time.";
            List<string> others = missing.Where(each => !each.StartsWith(RivalMark, StringComparison.Ordinal)).ToList();
            if (others.Count > 0)
            {
                text += $" The game has also changed, and these parts were not found: {string.Join(", ", others)}.";
            }
            return $"{text} {SwitchedOffNote}";
        }

        internal override void Update()
        {
            ChestClaims.Tick();
            FavoriteMarks.Update();
            Keys.Update();
            QuickStack.Update();
            Mine.Update();
            MineAccess.Update();   // his marked chests take his choice for other players (card A80)
            Button.Update();
            PersonalButton.Update();
            LightUp.Update();
            Sorting.Update();
            SortButtons.Update();
            TrashMarks.Update();
            TrashDialog.Update();
            TrashCan.Update();
            Folds.Tick();
            ChestOrderDrawer.Tick();
            MessageHold.Tick();
            SetupKeys.Update();
            MineButton.Update();   // the personal chest button (card A80), before the setup button, which sits under it
            SetupButton.Update();
            SetupWindow.Update();
            Tidy.Update();
            TidyButton.Update();   // after the setup button, which it sits under
        }

        internal override IEnumerable<Action> UndoSteps =>
            new Action[]
            {
                QuickStack.Cancel, Mine.Cancel, Button.Remove, PersonalButton.Remove, LightUp.PutOutAll, FavoriteMarks.Hide,
                ChestClaims.ReleaseHolds, TrashDialog.Remove, SortButtons.Remove, TrashCan.Remove, TrashMarks.Hide, SetupKeys.Cancel,
                SetupWindow.Close, SetupButton.Remove, MessageHold.Release, ChestOrderDrawer.Release, Tidy.Cancel, TidyButton.Remove,
                MineButton.Remove,
            };
    }
}
