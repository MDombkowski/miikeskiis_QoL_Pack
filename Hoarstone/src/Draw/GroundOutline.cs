using System.Collections.Generic;
using Hoarstone.Shapes;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// The outline on the ground around the player, drawn with the dashes the game draws a Ward's circle with (the
    /// Ward's area marker's own segment, Circle_section), so it looks like the game's own markers and needs no shader of
    /// its own. A dash every 1.6 m along a settlement kept free, every 3.2 m along one that isn't, each laid on the ground
    /// and turned along the line, within 110 m of the player. Laid again only when the lines or the colours change or the
    /// player has moved 12 m (review round 1: each lay is up to 2,000 casts to the ground). A dash is tinted with the
    /// outline's colour, where the game's material takes a tint, only when its colour changes.
    /// </summary>
    internal static class GroundOutline
    {
        private const float KeptSpacing = 1.6f;
        private const float OtherSpacing = 3.2f;
        private const float Reach = 110f;
        private const float MoveAgain = 12f;
        private const int MostDashes = 2000;

        private static GameObject holder;
        private static GameObject dash;
        private static bool noDash;
        private static readonly List<GameObject> pool = new List<GameObject>();
        private static readonly List<Color> tints = new List<Color>();
        private static int used;
        private static object laidFor;
        private static Vector3 laidAround;
        private static Color laidKept, laidOther;

        internal static void Show(IReadOnlyList<OutlineLine> lines, bool wanted, float now)
        {
            Player player = Player.m_localPlayer;
            if (!wanted || lines.Count == 0 || player == null || ZoneSystem.instance == null)
            {
                Hide();
                return;
            }
            if (!Make())
            {
                return;
            }
            Vector3 around = player.transform.position;
            Color kept = Settings.KeptColour.Value, other = Settings.OthersColour.Value;
            if (ReferenceEquals(laidFor, lines) && (around - laidAround).sqrMagnitude < MoveAgain * MoveAgain &&
                kept == laidKept && other == laidOther)
            {
                return;
            }
            laidFor = lines;
            laidAround = around;
            laidKept = kept;
            laidOther = other;
            Lay(lines, around, kept, other);
        }

        private static void Lay(IReadOnlyList<OutlineLine> lines, Vector3 around, Color kept, Color other)
        {
            used = 0;
            foreach (OutlineLine line in lines)
            {
                float spacing = line.Kept ? KeptSpacing : OtherSpacing;
                Color colour = line.Kept ? kept : other;
                float carry = spacing / 2f;
                for (int k = 1; k < line.Points.Count && used < MostDashes; k++)
                {
                    Flat a = line.Points[k - 1], b = line.Points[k];
                    var from = new Vector3((float)a.X, 0f, (float)a.Z);
                    var to = new Vector3((float)b.X, 0f, (float)b.Z);
                    Vector3 along = to - from;
                    float length = along.magnitude;
                    if (length <= 0f)
                    {
                        continue;
                    }
                    Vector3 step = along / length;
                    float d = carry;
                    for (; d <= length && used < MostDashes; d += spacing)
                    {
                        Vector3 at = from + step * d;
                        float dx = at.x - around.x, dz = at.z - around.z;
                        if (dx * dx + dz * dz > Reach * Reach)
                        {
                            continue;
                        }
                        at.y = ZoneSystem.instance.GetGroundHeight(at) + 0.05f;
                        Place(at, step, colour);
                    }
                    carry = d - length;
                }
            }
            for (int i = used; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].activeSelf)
                {
                    pool[i].SetActive(false);
                }
            }
        }

        private static void Place(Vector3 at, Vector3 along, Color colour)
        {
            GameObject piece;
            bool made = false;
            if (used < pool.Count && pool[used] != null)
            {
                piece = pool[used];
            }
            else
            {
                piece = Object.Instantiate(dash, holder.transform);
                piece.name = "Hoarstone_Dash";
                made = true;
                if (used < pool.Count)
                {
                    pool[used] = piece;
                }
                else
                {
                    pool.Add(piece);
                    tints.Add(Color.clear);
                }
            }
            int at_ = used++;
            piece.transform.SetPositionAndRotation(at, Quaternion.LookRotation(along, Vector3.up));
            if (!piece.activeSelf)
            {
                piece.SetActive(true);
            }
            if (MaterialMan.instance != null && (made || tints[at_] != colour))
            {
                MaterialMan.instance.SetValue(piece, ShaderProps._Color, colour);
                tints[at_] = colour;
            }
        }

        private static bool Make()
        {
            if (holder != null && dash != null)
            {
                return true;
            }
            if (noDash || ZNetScene.instance == null)
            {
                return false;
            }
            GameObject ward = ZNetScene.instance.GetPrefab(Records.WardPrefab);
            CircleProjector marker = ward != null ? ward.GetComponentInChildren<CircleProjector>(true) : null;
            dash = marker != null ? marker.m_prefab : null;
            if (dash == null)
            {
                noDash = true;
                Module.Log.LogWarning("The Ward's circle marker wasn't found, so outlines show on the map only.");
                return false;
            }
            holder = new GameObject("Hoarstone_GroundOutline");
            Object.DontDestroyOnLoad(holder);
            return true;
        }

        private static void Hide()
        {
            if (used == 0)
            {
                return;
            }
            for (int i = 0; i < used && i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    pool[i].SetActive(false);
                }
            }
            used = 0;
            laidFor = null;
        }

        /// <summary>Destroys every dash (switching off, or after an error).</summary>
        internal static void Clear()
        {
            if (holder != null)
            {
                Object.Destroy(holder);
            }
            holder = null;
            pool.Clear();
            tints.Clear();
            used = 0;
            laidFor = null;
        }
    }
}
