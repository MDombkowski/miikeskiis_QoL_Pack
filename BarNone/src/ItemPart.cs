using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BarNone
{
    /// <summary>
    /// Items: loose items fall through listed pieces (his addition, 2026-09-30). Every loose item is an ItemDrop with a
    /// Rigidbody, moved only by the game that owns it: on every other game its copy has gravity off and follows the
    /// owner's. While the part is on, every loose item carries an ItemPasser (not a fish, nor the game's throwaway copies of
    /// items: ItemRules.Carries), which at the item's first touch of a listed piece tells the physics engine to ignore that
    /// pair from then on and wakes the item, so it falls on. Switched on, or
    /// either list changed, the items already there pass the listed pieces they rest against (and pairs no longer wanted
    /// are restored); an item loaded with its area, or taken over from another game, is asleep (the game loads every
    /// object asleep) and touches nothing new, so its helper passes what it rests against once its game owns it, within
    /// seconds. Switched off, every pair is restored and the helpers go, so nothing is left behind. Food set out as a
    /// piece has no Rigidbody (the game removes it) and is left alone.
    /// </summary>
    internal static class ItemPart
    {
        private static readonly Collider[] touching = new Collider[64];
        private static readonly List<Collider> kept = new List<Collider>();
        private static readonly int[] masks = new int[32];
        private static readonly bool[] maskKnown = new bool[32];

        private static bool wasOn;
        private static string pieceList;
        private static string exceptList;

        /// <summary>The part is switched on: the module on, "Items: fall through pieces" on, and its hook on ItemDrop.Awake
        /// found and applied.</summary>
        internal static bool On => Module.IsOn && Settings.ItemsThrough.Value && !Broken.Is(Feature.Items) && GameHooks.ItemsReady;

        /// <summary>After ItemDrop.Awake: a new loose item gets its helper while the part is on.</summary>
        internal static void AfterItemDropAwake(ItemDrop item)
        {
            if (item != null && ShouldCarry(item) && item.GetComponent<ItemPasser>() == null)
            {
                item.gameObject.AddComponent<ItemPasser>();
            }
        }

        // Whether this item carries a helper (ItemRules.Carries), as the item is now.
        private static bool ShouldCarry(ItemDrop item) =>
            ItemRules.Carries(item.GetComponent<Rigidbody>() != null, item.TryGetComponent(out ZNetView view) && view.IsValid(), item.GetComponent<Fish>() != null);

        /// <summary>Every frame (while the module runs): switched on, the items already there get their helpers and pass
        /// what they rest against; switched off, every pair is restored and the helpers removed; either list changed,
        /// every live item follows it. An unchanged frame allocates nothing.</summary>
        internal static void Follow()
        {
            bool on = On;
            string pieces = on ? Settings.ItemPieces.Value : null;
            string except = on ? Settings.ExceptItems.Value : null;
            ItemChange change = ItemRules.ChangeOf(wasOn, pieceList, exceptList, on, pieces, except);
            if (change == ItemChange.None)
            {
                return;
            }
            wasOn = on;
            pieceList = pieces;
            exceptList = except;
            switch (change)
            {
                case ItemChange.SwitchedOn:
                    PassEveryItem();
                    break;
                case ItemChange.SwitchedOff:
                    RestorePairs();
                    break;
                default:
                    for (int i = 0; i < ItemPasser.Live.Count; i++)
                    {
                        ItemPasser passer = ItemPasser.Live[i];
                        if (passer != null)
                        {
                            passer.FollowTheLists();
                        }
                    }
                    break;
            }
        }

        /// <summary>Every ignored pair restored and every helper removed (an undo step after an error).</summary>
        internal static void RestoreAll() => RestorePairs();

        private static void RestorePairs()
        {
            ItemPasser.RestoreEvery();
            wasOn = false;
        }

        // Every loose item in the world gets its helper and passes the listed pieces it rests against now (resting, it
        // sends no new touch).
        private static void PassEveryItem()
        {
            foreach (ItemDrop item in Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None))
            {
                if (item == null || !ShouldCarry(item))
                {
                    continue;
                }
                ItemPasser passer = item.GetComponent<ItemPasser>();
                if (passer == null)
                {
                    passer = item.gameObject.AddComponent<ItemPasser>();
                }
                passer.PassWhatItTouches();
            }
        }

        /// <summary>The listed colliders overlapping, or within a hair of, any of this item's solid colliders now. Only
        /// the layers each collider collides with are searched, its own layer (other items) left out, so a pile of items
        /// can't crowd the piece out of the search.</summary>
        internal static List<Collider> ListedTouching(Collider[] own)
        {
            kept.Clear();
            foreach (Collider part in own)
            {
                if (part == null || !part.enabled || part.isTrigger)
                {
                    continue;
                }
                Bounds bounds = part.bounds;
                int count = Physics.OverlapSphereNonAlloc(bounds.center, bounds.extents.magnitude + 0.05f, touching, MaskFor(part.gameObject.layer), QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider other = touching[i];
                    if (other != null && !kept.Contains(other) && PieceLists.Items.Has(other))
                    {
                        kept.Add(other);
                    }
                }
            }
            return kept;
        }

        // The layers a collider on this layer collides with, by the game's own collision matrix, but not its own layer.
        private static int MaskFor(int layer)
        {
            if (layer < 0 || layer > 31)
            {
                return 0;
            }
            if (!maskKnown[layer])
            {
                int mask = 0;
                for (int other = 0; other < 32; other++)
                {
                    if (other != layer && !Physics.GetIgnoreLayerCollision(layer, other))
                    {
                        mask |= 1 << other;
                    }
                }
                masks[layer] = mask;
                maskKnown[layer] = true;
            }
            return masks[layer];
        }
    }

    /// <summary>What changed for the items part since the last frame.</summary>
    internal enum ItemChange
    {
        None,
        SwitchedOn,
        SwitchedOff,
        ListsChanged,
    }

    /// <summary>The items part's rules, apart from Unity so the harness can check them.</summary>
    internal static class ItemRules
    {
        /// <summary>Whether an item falls through the piece it touched (or keeps falling through one it already passes): a
        /// listed piece, and an item not excepted.</summary>
        internal static bool Falls(bool listed, bool excepted) => listed && !excepted;

        /// <summary>Which items carry a helper: a loose item, with a Rigidbody and its network object live (the game's
        /// throwaway copies of items, made to fill an inventory or dress a creature, and the ghost of food being set out,
        /// have none), and never a fish, swimming or caught (the game makes it one object, a creature in the water).</summary>
        internal static bool Carries(bool hasBody, bool liveView, bool isFish) => hasBody && liveView && !isFish;

        /// <summary>What the frame must do: the switch decides first; while on, a changed text of either list (compared
        /// exactly, so an unchanged frame allocates nothing) makes every item follow it.</summary>
        internal static ItemChange ChangeOf(bool wasOn, string wasPieces, string wasExcept, bool on, string pieces, string except)
        {
            if (on != wasOn)
            {
                return on ? ItemChange.SwitchedOn : ItemChange.SwitchedOff;
            }
            if (on && (!string.Equals(pieces, wasPieces, StringComparison.Ordinal) || !string.Equals(except, wasExcept, StringComparison.Ordinal)))
            {
                return ItemChange.ListsChanged;
            }
            return ItemChange.None;
        }

        /// <summary>The slow check's rule: an item passes what it rests against once each time its game comes to own it
        /// (never while another game moves it).</summary>
        internal static bool PassOnCheck(bool owned, ref bool passed)
        {
            if (!owned)
            {
                passed = false;
                return false;
            }
            if (passed)
            {
                return false;
            }
            passed = true;
            return true;
        }

        /// <summary>The log's line for an item's first touch of a piece.</summary>
        internal static string Describe(string item, string piece, bool listed, bool excepted) =>
            $"Item {item} touched the piece {piece}" +
            (!listed ? ": stays." : excepted ? ", a listed piece: stays (an excepted item)." : ", a listed piece: falls through.");
    }

    /// <summary>
    /// Rides on a loose item while the items part is on. At the item's first touch of a listed piece's collider, the
    /// physics engine is told to ignore every collider of the item against it, and the item is woken, so it falls on.
    /// The collision's collider is read at once and the collision itself never kept: Valheim Performance Optimizations
    /// has the engine reuse one collision object for every callback. Every touch ignores its pair again: the engine
    /// forgets a pair when either collider is switched off and on. An item the game loaded with its area, or took over
    /// from another game, is asleep and touches nothing new: a slow check (every 2 s, as the game hands out ownership)
    /// passes what it rests against once its game owns it, and wakes it.
    /// </summary>
    internal sealed class ItemPasser : MonoBehaviour
    {
        /// <summary>How often each helper checks whether its game has come to own its item, in seconds.</summary>
        internal const float CheckEvery = 2f;

        /// <summary>Every helper alive, so switching off can restore them all. Removed by swapping in the last one (the
        /// game keeps its own items the same way), so unloading many items at once stays cheap.</summary>
        internal static readonly List<ItemPasser> Live = new List<ItemPasser>();

        private static readonly Action touchWaiting = TouchWaiting;
        private static readonly Action checkWaiting = CheckWaiting;
        private static ItemPasser waitingPasser;
        private static Collider waitingOther;

        private readonly List<Collider> ignored = new List<Collider>();
        private readonly List<Collider> logged = new List<Collider>();
        private Collider[] own;
        private Rigidbody body;
        private ZNetView nview;
        private string itemName;
        private string itemKey;
        private int liveIndex = -1;
        private bool passedAsOwner;

        private void Awake()
        {
            own = GetComponentsInChildren<Collider>();
            body = GetComponent<Rigidbody>();
            nview = GetComponent<ZNetView>();
            itemName = Utils.GetPrefabName(gameObject);
            itemKey = itemName.ToLowerInvariant();
            liveIndex = Live.Count;
            Live.Add(this);
        }

        private void Start()
        {
            InvokeRepeating(nameof(SlowCheck), UnityEngine.Random.Range(0.5f, 1.5f), CheckEvery);
        }

        private void OnDestroy()
        {
            if (liveIndex >= 0 && liveIndex < Live.Count && ReferenceEquals(Live[liveIndex], this))
            {
                int last = Live.Count - 1;
                ItemPasser moved = Live[last];
                Live[liveIndex] = moved;
                moved.liveIndex = liveIndex;
                Live.RemoveAt(last);
            }
            liveIndex = -1;
        }

        /// <summary>Every helper's pairs restored and every helper removed (the part switched off, or an error).</summary>
        internal static void RestoreEvery()
        {
            foreach (ItemPasser passer in Live.ToArray())
            {
                if (passer != null)
                {
                    passer.Restore();
                    passer.liveIndex = -1;
                    Object.Destroy(passer);
                }
            }
            Live.Clear();
        }

        private void OnCollisionEnter(Collision collision)
        {
            Collider other = collision.collider;   // read at once: the collision may be reused for the next callback
            if (own == null || own.Length == 0 || other == null || body == null || !ItemPart.On)
            {
                return;
            }
            waitingPasser = this;
            waitingOther = other;
            Module.Guard("letting an item fall through a piece", touchWaiting);
        }

        // Every 2 s, inside the module's guard (SlowWork).
        private void SlowCheck()
        {
            waitingPasser = this;
            Module.Guard("letting a resting item fall through a piece", checkWaiting);
        }

        // An item whose Rigidbody the game took away (food set out as a piece; flametal and snowballs, which ride a body
        // of the game's own once they touch the ground) gets its pairs back and drops its helper; an item whose game has
        // come to own it passes what it rests against, once, and is woken.
        private void SlowWork()
        {
            if (body == null)
            {
                Restore();
                Destroy(this);
                return;
            }
            if (ItemPart.On)
            {
                CheckOwner();
            }
        }

        /// <summary>Whether this item is on "Items: except these items" now.</summary>
        internal bool Excepted => PieceLists.ExceptItems.HasName(itemKey);

        internal int IgnoredCount => ignored.Count;

        /// <summary>Lets the item through the listed pieces it already rests against (when the part is switched on, a list
        /// changes, or its game has come to own it, while it lies on one).</summary>
        internal void PassWhatItTouches()
        {
            if (own == null || body == null || !ItemRules.Falls(true, Excepted))
            {
                return;
            }
            foreach (Collider other in ItemPart.ListedTouching(own))
            {
                Ignore(other);
            }
        }

        /// <summary>A list changed: a pair whose piece is no longer listed, or every pair of an item now excepted, is
        /// restored, and the listed pieces the item rests against now are passed.</summary>
        internal void FollowTheLists()
        {
            bool excepted = Excepted;
            for (int i = ignored.Count - 1; i >= 0; i--)
            {
                Collider other = ignored[i];
                if (other != null && ItemRules.Falls(PieceLists.Items.Has(other), excepted))
                {
                    continue;
                }
                if (other != null)
                {
                    SetIgnored(other, false);
                }
                ignored.RemoveAt(i);
            }
            PassWhatItTouches();
        }

        /// <summary>Every pair this item ignores is restored.</summary>
        internal void Restore()
        {
            foreach (Collider other in ignored)
            {
                if (other != null)
                {
                    SetIgnored(other, false);
                }
            }
            ignored.Clear();
        }

        private void Ignore(Collider other)
        {
            SetIgnored(other, true);
            if (!ignored.Contains(other))
            {
                ignored.Add(other);
            }
            if (body != null)
            {
                body.WakeUp();
            }
        }

        // Only colliders that are on: the engine refuses a pair with one that's switched off, and forgets the pair anyway.
        private void SetIgnored(Collider other, bool ignore)
        {
            if (!other.enabled || !other.gameObject.activeInHierarchy)
            {
                return;
            }
            foreach (Collider part in own)
            {
                if (part != null && part.enabled && part.gameObject.activeInHierarchy)
                {
                    Physics.IgnoreCollision(part, other, ignore);
                }
            }
        }

        // The item touched something: through it if it's a listed piece and the item isn't excepted; and, while the log
        // is on, a line for its first touch of each piece.
        private void Touched(Collider other)
        {
            bool listed = PieceLists.Items.Has(other);
            bool excepted = Excepted;
            if (ItemRules.Falls(listed, excepted))
            {
                Ignore(other);
            }
            if (Settings.LogItems.Value && !logged.Contains(other))
            {
                string piece = PieceNames.Of(other);
                if (piece != null)
                {
                    logged.Add(other);
                    Module.Log.LogInfo(ItemRules.Describe(itemName, piece, listed, excepted));
                }
            }
        }

        // The slow check's work: once its game owns the item (again), what it rests against is passed and it's woken,
        // pairs passed while another game moved it included.
        private void CheckOwner()
        {
            bool owned = nview != null && nview.IsValid() && nview.IsOwner();
            if (!ItemRules.PassOnCheck(owned, ref passedAsOwner))
            {
                return;
            }
            PassWhatItTouches();
            if (ignored.Count > 0 && body != null)
            {
                body.WakeUp();
            }
        }

        private static void TouchWaiting()
        {
            ItemPasser passer = waitingPasser;
            Collider other = waitingOther;
            waitingPasser = null;
            waitingOther = null;
            if (passer != null && other != null)
            {
                passer.Touched(other);
            }
        }

        private static void CheckWaiting()
        {
            ItemPasser passer = waitingPasser;
            waitingPasser = null;
            if (passer != null)
            {
                passer.SlowWork();
            }
        }
    }
}
