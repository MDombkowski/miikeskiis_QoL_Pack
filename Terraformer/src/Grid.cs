using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Terraformer
{
    /// <summary>How the grid shows a stroke: its colour, and which points count as changed.</summary>
    internal enum GridLook
    {
        /// <summary>A tool that paints (the game's Level ground, Pathen, Paved road, Cultivate, Grass, and Terraformer's
        /// squares and paths): every point it paints or moves is lit, in the finish it lays.</summary>
        Finish,

        /// <summary>A dig: only the points that will go down are lit, orange.</summary>
        Dig,

        /// <summary>A raise: only the points that will go up are lit, green.</summary>
        Raise,

        /// <summary>Flatten: the ground as it will be, blue.</summary>
        Flatten,

        /// <summary>Remove terrain changes: the points it puts back, magenta.</summary>
        Remove,

        /// <summary>Slope: the ground as it will be, points that rise green, those that drop orange (B02).</summary>
        Slope,
    }

    /// <summary>
    /// The preview grid (his idea, 2026-09-30): a stroke's points on the ground, joined along the ground's own grid and
    /// filled between them, each as bright as the stroke acts on it, so a soft tool fades toward its edge and a sharp one
    /// doesn't; points in reach that won't change are only faintly drawn. It lies a few centimetres above the ground (for
    /// Flatten, above the ground as it will be), each 1 m cell split along the same diagonal the game draws the ground
    /// with (x + z constant; research D), so it follows the ground's own shape. One mesh, remade only when the stroke
    /// changes, or four times a second while it stays (strokes change the ground under it).
    /// </summary>
    internal static class Grid
    {
        private const float AboveGround = 0.05f;
        private const float FillAlpha = 0.24f;
        private const float LineAlpha = 0.9f;
        private const float StillAlpha = 0.14f;
        private const float RemakeAfter = 0.25f;

        private static readonly Color Invalid = new Color(1f, 0.3f, 0.25f);
        private static readonly Color DigColour = new Color(1f, 0.55f, 0.15f);
        private static readonly Color RaiseColour = new Color(0.45f, 1f, 0.45f);
        private static readonly Color FlattenColour = new Color(0.45f, 0.8f, 1f);
        private static readonly Color RemoveColour = new Color(1f, 0.4f, 0.85f);

        private static GameObject holder;
        private static Mesh mesh;
        private static MeshRenderer renderer;
        private static Material material;
        private static bool noShader;

        private static long madeFor;
        private static bool made;
        private static float madeAt = -1f;

        private static readonly List<Vector3> Vertices = new List<Vector3>();
        private static readonly List<Color> Colours = new List<Color>();
        private static readonly List<int> Cells = new List<int>();
        private static readonly List<int> Lines = new List<int>();
        private static readonly Dictionary<long, int> Point = new Dictionary<long, int>();

        /// <summary>The colour of a stroke's finish (its paint), for the grid.</summary>
        internal static Color FinishOf(TerrainModifier.PaintType type)
        {
            switch (type)
            {
                case TerrainModifier.PaintType.Paved: return new Color(0.78f, 0.8f, 0.86f);
                case TerrainModifier.PaintType.Cultivate: return new Color(0.62f, 0.42f, 0.24f);
                case TerrainModifier.PaintType.Reset:
                case TerrainModifier.PaintType.ClearVegetation: return new Color(0.5f, 0.92f, 0.35f);
                default: return new Color(0.9f, 0.7f, 0.45f);
            }
        }

        /// <summary>Draws the grid for these points. <paramref name="key"/> is a fingerprint of what they show: the same key
        /// within a quarter of a second keeps the grid as it is. False when it can't be drawn (no shader for it).</summary>
        internal static bool Show(long key, List<PointChange> changes, GridLook look, Color finish, bool valid)
        {
            if (!Make())
            {
                return false;
            }
            if (Wants(key))
            {
                Build(changes, look, finish, valid);
                madeFor = key;
                made = true;
                madeAt = Time.unscaledTime;
            }
            renderer.enabled = true;
            return true;
        }

        /// <summary>Whether the grid for this fingerprint must be (re)made: a new stroke, or a quarter of a second gone. The
        /// preview is worked out only then (review round 1).</summary>
        internal static bool Wants(long key) =>
            !noShader && (!made || key != madeFor || madeAt < 0f || Time.unscaledTime - madeAt >= RemakeAfter);

        internal static void Hide()
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
            made = false;
        }

        /// <summary>Destroys what it made (switching off, or after an error).</summary>
        internal static void Clear()
        {
            if (holder != null)
            {
                Object.Destroy(holder);
            }
            if (mesh != null)
            {
                Object.Destroy(mesh);
            }
            if (material != null)
            {
                Object.Destroy(material);
            }
            holder = null;
            mesh = null;
            renderer = null;
            material = null;
            made = false;
        }

        /// <summary>The height a point is drawn at: Flatten's on the ground as it will be where it fills, and on the ground as
        /// it is where it cuts (the grid is drawn behind the ground like everything else, so a result below the ground wouldn't
        /// show: review round 1); every other tool's on the ground as it is.</summary>
        internal static float DrawHeight(PointChange change, GridLook look) =>
            look == GridLook.Flatten || look == GridLook.Slope ? Mathf.Max(change.Height, change.After) : change.Height;

        /// <summary>A point's colour: Slope's by which way it moves, every other look's one colour.</summary>
        internal static Color ColourOf(PointChange change, GridLook look, Color colour) =>
            look != GridLook.Slope ? colour : change.After > change.Height ? RaiseColour : DigColour;

        /// <summary>Whether a point is one the stroke changes, as this look counts it.</summary>
        internal static bool Lit(PointChange change, GridLook look) =>
            look == GridLook.Finish ? change.Moves || change.Painted :
            look == GridLook.Remove ? change.Moves || change.Painted :
            change.Moves;

        private static void Build(List<PointChange> changes, GridLook look, Color finish, bool valid)
        {
            Vertices.Clear();
            Colours.Clear();
            Cells.Clear();
            Lines.Clear();
            Point.Clear();
            Color all = !valid ? Invalid : look == GridLook.Dig ? DigColour : look == GridLook.Raise ? RaiseColour :
                        look == GridLook.Flatten ? FlattenColour : look == GridLook.Remove ? RemoveColour : finish;

            // Two vertices per point, at the same place: one for the fill, one for the lines.
            foreach (PointChange change in changes)
            {
                bool lit = Lit(change, look);
                float strength = Mathf.Clamp01(change.Strength);
                Color colour = valid ? ColourOf(change, look, all) : all;
                var at = new Vector3(change.X, DrawHeight(change, look) + AboveGround, change.Z);
                Point[Key(change.X, change.Z)] = Vertices.Count;
                Vertices.Add(at);
                Colours.Add(new Color(colour.r, colour.g, colour.b, lit ? FillAlpha * strength : 0f));
                Vertices.Add(at);
                Colours.Add(new Color(colour.r, colour.g, colour.b, lit ? Mathf.Max(StillAlpha, LineAlpha * strength) : StillAlpha));
            }
            foreach (PointChange change in changes)
            {
                int here = Point[Key(change.X, change.Z)];
                bool east = Point.TryGetValue(Key(change.X + 1, change.Z), out int toEast);
                bool north = Point.TryGetValue(Key(change.X, change.Z + 1), out int toNorth);
                if (east)
                {
                    Lines.Add(here + 1);
                    Lines.Add(toEast + 1);
                }
                if (north)
                {
                    Lines.Add(here + 1);
                    Lines.Add(toNorth + 1);
                }
                // The cell to the north-east, split along x + z = constant, as the game draws the ground.
                if (east && north && Point.TryGetValue(Key(change.X + 1, change.Z + 1), out int toCorner))
                {
                    Cells.Add(here);
                    Cells.Add(toNorth);
                    Cells.Add(toEast);
                    Cells.Add(toEast);
                    Cells.Add(toNorth);
                    Cells.Add(toCorner);
                }
            }
            mesh.Clear();
            mesh.SetVertices(Vertices);
            mesh.SetColors(Colours);
            mesh.subMeshCount = 2;
            mesh.SetIndices(Cells, MeshTopology.Triangles, 0, false);
            mesh.SetIndices(Lines, MeshTopology.Lines, 1, false);
            mesh.RecalculateBounds();
        }

        private static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        private static bool Make()
        {
            if (holder != null)
            {
                return true;
            }
            if (noShader)
            {
                return false;
            }
            Shader shader = null;
            foreach (string name in new[] { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit", "UI/Default" })
            {
                shader = Shader.Find(name);
                if (shader != null)
                {
                    break;
                }
            }
            if (shader == null)
            {
                noShader = true;
                Module.Log.LogWarning("No shader for the preview grid was found, so terrain tools show the game's round marker.");
                return false;
            }
            material = new Material(shader) { name = "Terraformer_Grid" };
            holder = new GameObject("Terraformer_Grid");
            Object.DontDestroyOnLoad(holder);
            mesh = new Mesh { name = "Terraformer_Grid" };
            mesh.MarkDynamic();
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material, material };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return true;
        }
    }
}
