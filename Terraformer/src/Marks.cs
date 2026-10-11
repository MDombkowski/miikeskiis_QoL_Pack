using System;
using UnityEngine;

namespace Terraformer
{
    /// <summary>
    /// What the terrain tools show while he aims them:
    /// - **A square tool:** a line on the ground around the square it will change, turned with it and following the
    ///   ground, half a metre outside its outer points (the ground it changes); for Raise ground (precision), a second
    ///   line at the lift above it. White, or red where the stroke can't be placed. While it shows, the game's own round
    ///   marker is hidden (Ghost); with "Square outline" off, that marker shows instead.
    /// - **A round tool resized:** the game's own marker, grown or shrunk to the new radius, with its post kept as it was.
    /// </summary>
    internal static class Marks
    {
        private const float Width = 0.06f;
        private const float AboveGround = 0.06f;
        private const float StepAlongEdge = 0.5f;
        private const int MostStepsPerEdge = 64;

        // The ground under the outline is measured again only when the square moves, turns or resizes, or every quarter of
        // a second (strokes change it): up to 256 measurements, not every frame (review round 1).
        private const float RemeasureAfter = 0.25f;

        private sealed class Drawn
        {
            internal readonly Vector3[] Points = new Vector3[4 * MostStepsPerEdge];
            internal (double X, double Z)[] Corners;
            internal float Height;
            internal float At = -1f;
        }

        private static readonly Drawn GroundDrawn = new Drawn();
        private static readonly Drawn LiftDrawn = new Drawn();

        private static readonly Color Valid = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color Invalid = new Color(1f, 0.25f, 0.2f, 0.9f);
        private static readonly Color LiftColour = new Color(0.55f, 0.85f, 1f, 0.8f);

        private static GameObject holder;
        private static LineRenderer ground;
        private static LineRenderer lifted;
        private static LineRenderer ring;
        private static Material material;
        private static bool noShader;

        // The ring's points: the edge of a round stroke, a point about every half metre along it.
        private const int MostRingPoints = 256;
        private static readonly Vector3[] RingPoints = new Vector3[MostRingPoints];
        private static Vector3 ringCentre;
        private static float ringRadius = -1f;
        private static float ringAt = -1f;

        /// <summary>Draws a square tool's outline. False when it isn't drawn ("Square outline" off, or no shader for it).</summary>
        internal static bool ShowSquare(Footprint square, float height, float lift, bool valid)
        {
            if (!Settings.Outline.Value || !Make())
            {
                HideSquare();
                return false;
            }
            (double X, double Z)[] corners = square.Outline();
            Draw(ground, GroundDrawn, corners, 0f, valid ? Valid : Invalid, alongGround: true);
            if (lift > 0.001f)
            {
                Draw(lifted, LiftDrawn, corners, height + lift, LiftColour, alongGround: false);
            }
            else
            {
                lifted.enabled = false;
            }
            return true;
        }

        internal static void HideSquare()
        {
            if (ground != null)
            {
                ground.enabled = false;
            }
            if (lifted != null)
            {
                lifted.enabled = false;
            }
        }

        /// <summary>Draws the edge of a round stroke on the ground: a circle of this radius round the ground's point the
        /// stroke is measured from, white, or red where it can't be placed (with the preview grid). False when it isn't
        /// drawn (no shader for it).</summary>
        internal static bool ShowRing(Vector3 centre, float radius, bool valid)
        {
            if (!Make())
            {
                HideRing();
                return false;
            }
            float now = Time.unscaledTime;
            bool same = ringAt >= 0f && now - ringAt < RemeasureAfter && ringCentre == centre && Mathf.Abs(ringRadius - radius) < 1e-4f;
            if (!same)
            {
                int count = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * radius / StepAlongEdge), 24, MostRingPoints);
                for (int i = 0; i < count; i++)
                {
                    float angle = 2f * Mathf.PI * i / count;
                    var point = new Vector3(centre.x + radius * Mathf.Cos(angle), centre.y, centre.z + radius * Mathf.Sin(angle));
                    if (ZoneSystem.instance != null)
                    {
                        point.y = ZoneSystem.instance.GetGroundHeight(point) + AboveGround;
                    }
                    RingPoints[i] = point;
                }
                ring.positionCount = count;
                ring.SetPositions(RingPoints);   // only the first positionCount are drawn
                ringCentre = centre;
                ringRadius = radius;
                ringAt = now;
            }
            Color colour = valid ? Valid : Invalid;
            if (ring.startColor != colour)
            {
                ring.startColor = colour;
                ring.endColor = colour;
            }
            ring.enabled = true;
            return true;
        }

        internal static void HideRing()
        {
            if (ring != null)
            {
                ring.enabled = false;
            }
            ringAt = -1f;
        }

        internal static void Hide()
        {
            HideSquare();
            HideRing();
        }

        /// <summary>Destroys what it drew (switching off, or after an error).</summary>
        internal static void Clear()
        {
            if (holder != null)
            {
                UnityEngine.Object.Destroy(holder);
            }
            holder = null;
            ground = null;
            lifted = null;
            ring = null;
            GroundDrawn.At = -1f;
            LiftDrawn.At = -1f;
            ringAt = -1f;
            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
            }
            material = null;
        }

        /// <summary>Grows or shrinks the ghost's own round marker to a radius <paramref name="factor"/> times the tool's own,
        /// keeping its post (Level ground's and Paved road's) the size it was. The ghost is a fresh copy of the tool each
        /// time the game rebuilds it, so the size is taken from the tool itself every frame, never compounded.</summary>
        internal static void ScaleRing(GameObject ghost, Piece tool, float factor)
        {
            Transform marker = ghost.transform.Find("_GhostOnly");
            Transform own = tool.transform.Find("_GhostOnly");
            if (marker == null || own == null)
            {
                return;
            }
            ParticleSystem system = marker.GetComponentInChildren<ParticleSystem>(true);
            ParticleSystem ownSystem = own.GetComponentInChildren<ParticleSystem>(true);
            if (system == null || ownSystem == null)
            {
                return;
            }
            Vector3 scale = ownSystem.transform.localScale * factor;
            if (system.transform.localScale != scale)
            {
                system.transform.localScale = scale;
            }
            // Renderers that aren't particles below the scaled one (a post) keep their own size.
            foreach (MeshRenderer renderer in system.GetComponentsInChildren<MeshRenderer>(true))
            {
                Transform child = renderer.transform;
                Transform ownChild = own.Find(PathFrom(marker, child));
                if (ownChild != null && child.parent == system.transform && factor > 0f)
                {
                    child.localScale = ownChild.localScale / factor;
                }
            }
        }

        /// <summary>The radius the game draws a tool's round marker at, in metres: half its particles' size, times the
        /// marker's own scale (research B: its particles are 5.5 m across for Level ground, Paved road and Cultivate, 4 m
        /// for Pathen, 5 m for Grass, and only 2.5 m for Raise ground, which reaches 5 m across: the "far too small" circle
        /// of his first test). 0 where it can't be read.</summary>
        internal static float MarkerRadius(Piece tool)
        {
            Transform own = tool != null ? tool.transform.Find("_GhostOnly") : null;
            ParticleSystem system = own != null ? own.GetComponentInChildren<ParticleSystem>(true) : null;
            if (system == null)
            {
                return 0f;
            }
            float size = system.main.startSize.constantMax;
            return size / 2f * Mathf.Abs(system.transform.localScale.x);
        }

        private static string PathFrom(Transform root, Transform child)
        {
            string path = child.name;
            for (Transform parent = child.parent; parent != null && parent != root; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }
            return path;
        }

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
            foreach (string name in new[] { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Particles/Standard Unlit", "Unlit/Color", "UI/Default" })
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
                Module.Log.LogWarning("No shader for the square tools' outline was found, so squares show no outline.");
                return false;
            }
            material = new Material(shader) { name = "Terraformer_Outline" };
            holder = new GameObject("Terraformer_Marks");
            UnityEngine.Object.DontDestroyOnLoad(holder);
            ground = Line("Terraformer_Square");
            lifted = Line("Terraformer_Lift");
            ring = Line("Terraformer_Ring");
            return true;
        }

        private static LineRenderer Line(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(holder.transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = Width;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            return line;
        }

        private static void Draw(LineRenderer line, Drawn drawn, (double X, double Z)[] corners, float height, Color colour, bool alongGround)
        {
            float now = Time.unscaledTime;
            bool same = drawn.At >= 0f && now - drawn.At < RemeasureAfter && drawn.Height == height && SameCorners(drawn.Corners, corners);
            if (!same)
            {
                int count = 0;
                for (int i = 0; i < corners.Length; i++)
                {
                    (double X, double Z) from = corners[i];
                    (double X, double Z) to = corners[(i + 1) % corners.Length];
                    double length = Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Z - from.Z) * (to.Z - from.Z));
                    int steps = alongGround ? Math.Max(1, Math.Min(MostStepsPerEdge, (int)Math.Ceiling(length / StepAlongEdge))) : 1;
                    for (int step = 0; step < steps; step++)
                    {
                        double t = step / (double)steps;
                        var point = new Vector3((float)(from.X + (to.X - from.X) * t), height, (float)(from.Z + (to.Z - from.Z) * t));
                        if (alongGround && ZoneSystem.instance != null)
                        {
                            point.y = ZoneSystem.instance.GetGroundHeight(point) + AboveGround;
                        }
                        drawn.Points[count++] = point;
                    }
                }
                drawn.Corners = corners;
                drawn.Height = height;
                drawn.At = now;
                line.positionCount = count;
                line.SetPositions(drawn.Points);   // only the first positionCount are drawn
            }
            if (line.startColor != colour)
            {
                line.startColor = colour;
                line.endColor = colour;
            }
            line.enabled = true;
        }

        private static bool SameCorners((double X, double Z)[] a, (double X, double Z)[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].X != b[i].X || a[i].Z != b[i].Z)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
