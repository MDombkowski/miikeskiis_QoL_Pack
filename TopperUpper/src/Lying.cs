using System.Collections.Generic;
using UnityEngine;

namespace TopperUpper
{
    /// <summary>
    /// Items lying on the ground near a point: a physics search on the game's "item" layer, the way the game's own merge
    /// of nearby stacks finds them (ItemDrop.AutoStackItems: each collider's rigidbody's ItemDrop). For what lies at a
    /// station's output; the loose-item pickup can use it too.
    /// </summary>
    internal static class Lying
    {
        private static readonly Collider[] Hits = new Collider[256];
        private static int itemMask;

        /// <summary>The items lying within <paramref name="radius"/> of <paramref name="point"/>, each once (an item placed
        /// as a piece, which has no rigidbody, isn't one).</summary>
        internal static List<ItemDrop> At(Vector3 point, float radius)
        {
            if (itemMask == 0)
            {
                itemMask = LayerMask.GetMask("item");
            }
            var found = new List<ItemDrop>();
            int count = Physics.OverlapSphereNonAlloc(point, radius, Hits, itemMask);
            for (int i = 0; i < count; i++)
            {
                Rigidbody body = Hits[i] != null ? Hits[i].attachedRigidbody : null;
                ItemDrop drop = body != null ? body.GetComponent<ItemDrop>() : null;
                if (drop != null && !found.Contains(drop))
                {
                    found.Add(drop);
                }
                Hits[i] = null;
            }
            return found;
        }
    }
}
