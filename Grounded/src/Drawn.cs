using System.Collections.Generic;
using UnityEngine;

namespace Grounded
{
    /// <summary>
    /// What is drawn of a piece, from its renderers: the top of a floor's drawing, the bottom of a placed rug's, and the
    /// box around the ghost's drawing (its footprint, and how far its lowest drawn part is above its centre point). Only
    /// renderers that are switched on, in parts that are switched on, and that draw a mesh count: a floor's worn and
    /// broken looks, its snow, and a rug's pieces for when it breaks are switched off until needed, and are left out.
    /// </summary>
    internal static class Drawn
    {
        private const string GhostOnly = "_GhostOnly";

        private static readonly List<Renderer> renderers = new List<Renderer>();
        private static readonly List<Collider> colliders = new List<Collider>();
        private static readonly List<ColliderSpan> spans = new List<ColliderSpan>();
        private static readonly Vector3[] corners = new Vector3[8];
        private static int footprintOf;
        private static Footprint footprint = Footprint.Empty;

        /// <summary>The highest point drawn of a placed piece, in the world; NaN if nothing of it is drawn.</summary>
        internal static float Top(Component piece)
        {
            float top = float.NaN;
            foreach (Renderer renderer in Shown(piece))
            {
                float y = renderer.bounds.max.y;
                top = float.IsNaN(top) ? y : Mathf.Max(top, y);
            }
            return top;
        }

        /// <summary>The lowest point drawn of a placed piece, in the world; NaN if nothing of it is drawn.</summary>
        internal static float Bottom(Component piece)
        {
            float bottom = float.NaN;
            foreach (Renderer renderer in Shown(piece))
            {
                float y = renderer.bounds.min.y;
                bottom = float.IsNaN(bottom) ? y : Mathf.Min(bottom, y);
            }
            return bottom;
        }

        /// <summary>
        /// The box around what is drawn of the ghost, from its centre point, unturned. Worked out from its meshes rather than
        /// from where it stands, since the ghost may be hidden (the game hides it while the ray hits nothing) or still
        /// stand where last frame put it; kept for as long as the same ghost is in hand. Empty if nothing is drawn.
        /// </summary>
        internal static Footprint FootprintOf(GameObject ghost)
        {
            int id = ghost.GetInstanceID();
            if (id == footprintOf && !footprint.IsEmpty)
            {
                return footprint;
            }
            Transform root = ghost.transform;
            Quaternion unturn = Quaternion.Inverse(root.rotation);
            Vector3 origin = root.position;
            bool any = false;
            Vector3 min = Vector3.zero;
            Vector3 max = Vector3.zero;
            renderers.Clear();
            ghost.GetComponentsInChildren(true, renderers);
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = MeshOf(renderer);
                if (mesh == null || !renderer.enabled || !SwitchedOnBelow(root, renderer.transform))
                {
                    continue;
                }
                Bounds local = mesh.bounds;
                Vector3 c = local.center;
                Vector3 e = local.extents;
                int i = 0;
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            corners[i++] = new Vector3(c.x + sx * e.x, c.y + sy * e.y, c.z + sz * e.z);
                        }
                    }
                }
                Transform part = renderer.transform;
                foreach (Vector3 corner in corners)
                {
                    Vector3 offset = unturn * (part.TransformPoint(corner) - origin);
                    if (!any)
                    {
                        min = offset;
                        max = offset;
                        any = true;
                    }
                    else
                    {
                        min = Vector3.Min(min, offset);
                        max = Vector3.Max(max, offset);
                    }
                }
            }
            renderers.Clear();
            float pickTop = PickTop(ghost, root, unturn, origin, any ? (max.x - min.x) * (max.z - min.z) : 0f);
            footprintOf = id;
            footprint = any ? new Footprint(min.x, max.x, min.y, max.y, min.z, max.z, pickTop) : Footprint.Empty;
            return footprint;
        }

        /// <summary>
        /// The height above the ghost's centre point the hammer's ray is sure to meet (Heights.PickTop) from the colliders
        /// the game left switched on in the ghost (SetupPlacementGhost switches off every collider on a layer its aiming
        /// ray doesn't look at, which are the layers the hammer's remove looks at too), solid ones only: each as the span
        /// of heights its box covers, turned into the world and back as the ghost stands. NaN for none.
        /// </summary>
        private static float PickTop(GameObject ghost, Transform root, Quaternion unturn, Vector3 origin, float drawnArea)
        {
            spans.Clear();
            colliders.Clear();
            ghost.GetComponentsInChildren(true, colliders);
            foreach (Collider collider in colliders)
            {
                if (!collider.enabled || collider.isTrigger || !SwitchedOnBelow(root, collider.transform))
                {
                    continue;
                }
                Vector3 centre;
                Vector3 extents;
                bool isMesh = false;
                switch (collider)
                {
                    case BoxCollider box:
                        centre = box.center;
                        extents = box.size / 2f;
                        break;
                    case MeshCollider meshCollider when meshCollider.sharedMesh != null:
                        centre = meshCollider.sharedMesh.bounds.center;
                        extents = meshCollider.sharedMesh.bounds.extents;
                        isMesh = true;
                        break;
                    case SphereCollider sphere:
                        centre = sphere.center;
                        extents = Vector3.one * sphere.radius;
                        break;
                    case CapsuleCollider capsule:
                        centre = capsule.center;
                        extents = Vector3.one * Mathf.Max(capsule.radius, capsule.height / 2f);
                        break;
                    default:
                        continue;
                }
                Transform part = collider.transform;
                float bottom = float.MaxValue;
                float top = float.MinValue;
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var corner = new Vector3(centre.x + sx * extents.x, centre.y + sy * extents.y, centre.z + sz * extents.z);
                            Vector3 offset = unturn * (part.TransformPoint(corner) - origin);
                            bottom = Mathf.Min(bottom, offset.y);
                            top = Mathf.Max(top, offset.y);
                            minX = Mathf.Min(minX, offset.x);
                            maxX = Mathf.Max(maxX, offset.x);
                            minZ = Mathf.Min(minZ, offset.z);
                            maxZ = Mathf.Max(maxZ, offset.z);
                        }
                    }
                }
                spans.Add(new ColliderSpan(isMesh, bottom, top, (maxX - minX) * (maxZ - minZ)));
            }
            colliders.Clear();
            float pick = Heights.PickTop(spans, drawnArea);
            spans.Clear();
            return pick;
        }

        /// <summary>Forgets the ghost's footprint (an undo step).</summary>
        internal static void Forget()
        {
            footprintOf = 0;
            footprint = Footprint.Empty;
            renderers.Clear();
            colliders.Clear();
        }

        /// <summary>The renderers of a placed piece that draw something now. The list is reused: read it at once.</summary>
        private static List<Renderer> Shown(Component piece)
        {
            renderers.Clear();
            piece.GetComponentsInChildren(false, renderers);
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                if (!renderers[i].enabled || MeshOf(renderers[i]) == null)
                {
                    renderers.RemoveAt(i);
                }
            }
            return renderers;
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }
            if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                return filter != null ? filter.sharedMesh : null;
            }
            return null;
        }

        /// <summary>Whether every part from this one up to the ghost's root (not the root itself, which the game may hide)
        /// is switched on, and none is the ghost-only part the game shows only while placing.</summary>
        private static bool SwitchedOnBelow(Transform root, Transform part)
        {
            for (Transform t = part; t != null && t != root; t = t.parent)
            {
                if (!t.gameObject.activeSelf || t.name == GhostOnly)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
