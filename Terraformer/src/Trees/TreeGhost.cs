using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Terraformer.Trees
{
    /// <summary>
    /// The grown tree's see-through copy, and the ring of the room it needs to grow.
    /// - **Built from the tree's drawn parts only:** a new object holding, for each mesh the tree draws at full detail (its
    ///   first level of detail, and any mesh outside its levels), a copy of that part's place in the tree and the same
    ///   mesh. Nothing of the tree's own (its network view, its colliders, its scripts, its particles) is ever made, so no
    ///   game code runs for it, nothing is sent, and nothing in the world can bump into it or see it.
    /// - **See-through:** each of the tree's materials is drawn with a copy on the grid's shader (Grid's chain, Sprites/Default
    ///   first, which every build of the game has), keeping its texture, at "Preview opacity". No shadows. Unlit; it writes
    ///   no depth, so even at 1 it isn't quite solid and leaves may sort imperfectly; it ignores a texture's tiling (the
    ///   game's trees use none). Of the chain only Sprites/Default and UI/Default are in the game's build, and both
    ///   multiply by the mesh's vertex colour, which the trees' meshes lack: Unity then reads white, so it should look
    ///   right; if a tree ever comes out black, the fix is another approach (a shader of the game's own), not another
    ///   order of this chain (review round 1, finding 5).
    /// - **Placed** at the sapling's ghost, turned as it is, at the tree's size: the game sets a grown tree's scale to the
    ///   size outright (ZNetView.SetLocalScale), so the copy's is the size too.
    /// - **Kept per tree while the preview shows** (Birch1 and Birch2 each built once while he steps between them) and
    ///   destroyed with its materials the moment the preview ends (<see cref="Clear"/>: Tick, the ghost's postfix, the
    ///   undo step).
    /// </summary>
    internal static class TreeGhost
    {
        private const int RingPoints = 64;
        private const float RingAbove = 0.08f;
        private const float RingWidth = 0.06f;
        private static readonly Color RingColour = new Color(0.45f, 1f, 0.45f, 0.9f);

        private static readonly Dictionary<GameObject, GameObject> Built = new Dictionary<GameObject, GameObject>();
        private static readonly Dictionary<Material, Material> SeeThrough = new Dictionary<Material, Material>();
        private static readonly List<Material> Made = new List<Material>();

        private static GameObject shown;
        private static float madeOpacity = -1f;
        private static Shader shader;
        private static bool noShader;

        private static GameObject ring;
        private static LineRenderer line;
        private static Material ringMaterial;
        private static Vector3 ringAt;
        private static float ringRadius = -1f;

        /// <summary>Shows this tree at this place, turn and size (built the first time it's asked for).</summary>
        internal static void Show(GameObject tree, Vector3 position, Quaternion rotation, float size, float opacity)
        {
            if (tree == null || !FindShader())
            {
                Hide();
                return;
            }
            if (!Built.TryGetValue(tree, out GameObject copy) || copy == null)
            {
                copy = Build(tree);
                Built[tree] = copy;
            }
            if (shown != null && shown != copy)
            {
                shown.SetActive(false);
            }
            shown = copy;
            if (!Mathf.Approximately(opacity, madeOpacity))
            {
                madeOpacity = opacity;
                foreach (Material material in Made)
                {
                    if (material != null)
                    {
                        Color colour = material.color;
                        colour.a = Mathf.Clamp01(opacity);
                        material.color = colour;
                    }
                }
            }
            copy.transform.SetPositionAndRotation(position, rotation);
            copy.transform.localScale = new Vector3(size, size, size);
            if (!copy.activeSelf)
            {
                copy.SetActive(true);
            }
        }

        /// <summary>Shows the ring of the room the sapling needs to grow, on the ground round it; null radius hides it.</summary>
        internal static void ShowRing(Vector3 centre, float? radius)
        {
            if (radius == null || radius.Value <= 0f || !FindShader())
            {
                if (ring != null)
                {
                    ring.SetActive(false);
                }
                return;
            }
            if (ring == null)
            {
                if (ringMaterial != null)
                {
                    Object.Destroy(ringMaterial);   // its ring went with the world it was made in
                }
                ring = new GameObject("Terraformer_TreeRing");
                line = ring.AddComponent<LineRenderer>();
                ringMaterial = new Material(shader) { name = "Terraformer_TreeRing", color = RingColour };
                line.sharedMaterial = ringMaterial;
                line.loop = true;
                line.useWorldSpace = true;
                line.positionCount = RingPoints;
                line.widthMultiplier = RingWidth;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                ringRadius = -1f;
            }
            if (!ring.activeSelf)
            {
                ring.SetActive(true);
            }
            if ((centre - ringAt).sqrMagnitude < 1e-4f && Mathf.Approximately(radius.Value, ringRadius))
            {
                return;
            }
            ringAt = centre;
            ringRadius = radius.Value;
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = i * Mathf.PI * 2f / RingPoints;
                var point = new Vector3(centre.x + Mathf.Cos(angle) * ringRadius, centre.y, centre.z + Mathf.Sin(angle) * ringRadius);
                point.y = GroundAt(point, centre.y) + RingAbove;
                line.SetPosition(i, point);
            }
        }

        // The ground's height there, as the game's zones have it, else the sapling's own.
        private static float GroundAt(Vector3 point, float fallback)
        {
            ZoneSystem zones = ZoneSystem.instance;
            return zones != null && zones.GetGroundHeight(point, out float height) ? height : fallback;
        }

        /// <summary>Hides what shows, keeping what's built (the preview still on, but nothing to show this frame).</summary>
        internal static void Hide()
        {
            if (shown != null)
            {
                shown.SetActive(false);
            }
            if (ring != null)
            {
                ring.SetActive(false);
            }
        }

        /// <summary>Destroys every copy, the ring and the materials made for them (the preview ended, switching off, or after
        /// an error).</summary>
        internal static void Clear()
        {
            if (Built.Count == 0 && ring == null && Made.Count == 0)
            {
                return;
            }
            foreach (GameObject copy in Built.Values)
            {
                if (copy != null)
                {
                    Object.Destroy(copy);
                }
            }
            Built.Clear();
            foreach (Material material in Made)
            {
                if (material != null)
                {
                    Object.Destroy(material);
                }
            }
            Made.Clear();
            SeeThrough.Clear();
            shown = null;
            madeOpacity = -1f;
            if (ring != null)
            {
                Object.Destroy(ring);
            }
            if (ringMaterial != null)
            {
                Object.Destroy(ringMaterial);
            }
            ring = null;
            line = null;
            ringMaterial = null;
            ringRadius = -1f;
        }

        // The shader the grid uses, found once: false when none is there (then nothing is shown, and it's said once).
        private static bool FindShader()
        {
            if (shader != null)
            {
                return true;
            }
            if (noShader)
            {
                return false;
            }
            foreach (string name in new[] { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit", "UI/Default" })
            {
                shader = Shader.Find(name);
                if (shader != null)
                {
                    return true;
                }
            }
            noShader = true;
            Module.Log.LogWarning("No shader for the grown tree's preview was found, so the tree doesn't show (the lock still works).");
            return false;
        }

        // The copy: the tree's full-detail meshes, each at its own place under a new root, with see-through materials. A
        // copy half made when something throws is destroyed before the error goes on (review round 1, finding 6): it isn't
        // in Built yet, so Clear couldn't find it. The materials made so far are in Made, which Clear destroys.
        private static GameObject Build(GameObject tree)
        {
            var root = new GameObject("Terraformer_TreePreview_" + tree.name);
            try
            {
                Fill(root, tree);
            }
            catch (System.Exception)
            {
                Object.Destroy(root);
                throw;
            }
            return root;
        }

        private static void Fill(GameObject root, GameObject tree)
        {
            root.SetActive(false);
            var copies = new Dictionary<Transform, Transform> { [tree.transform] = root.transform };
            var lesser = new HashSet<Renderer>();
            foreach (LODGroup group in tree.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                var first = new HashSet<Renderer>(lods.Length > 0 ? lods[0].renderers ?? new Renderer[0] : new Renderer[0]);
                for (int level = 1; level < lods.Length; level++)
                {
                    foreach (Renderer renderer in lods[level].renderers ?? new Renderer[0])
                    {
                        if (renderer != null && !first.Contains(renderer))
                        {
                            lesser.Add(renderer);
                        }
                    }
                }
            }
            foreach (MeshRenderer renderer in tree.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (!renderer.enabled || lesser.Contains(renderer) || filter == null || filter.sharedMesh == null ||
                    !ActiveUnder(renderer.transform, tree.transform))
                {
                    continue;
                }
                Transform part = CopyOf(renderer.transform, copies);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                MeshRenderer drawn = part.gameObject.AddComponent<MeshRenderer>();
                Material[] materials = renderer.sharedMaterials;
                var seeThrough = new Material[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    seeThrough[i] = SeeThroughOf(materials[i]);
                }
                drawn.sharedMaterials = seeThrough;
                drawn.shadowCastingMode = ShadowCastingMode.Off;
                drawn.receiveShadows = false;
                drawn.lightProbeUsage = LightProbeUsage.Off;
                drawn.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        // Whether every object from this one up to the tree's root is active in the prefab (a part the tree hides isn't drawn).
        private static bool ActiveUnder(Transform part, Transform root)
        {
            for (Transform each = part; each != null; each = each.parent)
            {
                if (!each.gameObject.activeSelf)
                {
                    return false;
                }
                if (each == root)
                {
                    return true;
                }
            }
            return false;
        }

        // The copy of a part of the tree, made with its parents' copies, each at its own place, turn and scale under them.
        private static Transform CopyOf(Transform part, Dictionary<Transform, Transform> copies)
        {
            if (copies.TryGetValue(part, out Transform copy))
            {
                return copy;
            }
            Transform parent = CopyOf(part.parent, copies);
            copy = new GameObject("Terraformer_TreePreview_" + part.name).transform;
            copy.SetParent(parent, false);
            copy.localPosition = part.localPosition;
            copy.localRotation = part.localRotation;
            copy.localScale = part.localScale;
            copies[part] = copy;
            return copy;
        }

        // One see-through material per material of the tree's, with its texture and the opacity.
        private static Material SeeThroughOf(Material source)
        {
            if (source != null && SeeThrough.TryGetValue(source, out Material made))
            {
                return made;
            }
            made = new Material(shader) { name = "Terraformer_TreePreview", color = new Color(1f, 1f, 1f, Mathf.Clamp01(madeOpacity < 0f ? 0.5f : madeOpacity)) };
            if (source != null && source.HasProperty("_MainTex"))
            {
                made.mainTexture = source.mainTexture;
                made.mainTextureScale = source.mainTextureScale;
                made.mainTextureOffset = source.mainTextureOffset;
            }
            Made.Add(made);
            if (source != null)
            {
                SeeThrough[source] = made;
            }
            return made;
        }
    }
}
