using System.Collections.Generic;

namespace HouseTrained
{
    /// <summary>How far a tame's head reaches ahead of its centre, how high it is held, and how wide it is.</summary>
    internal readonly struct Reach
    {
        /// <summary>From the creature's centre to the front of its model, in metres, at scale 1.</summary>
        internal readonly float Ahead;

        /// <summary>The top of its head above its root, in metres, at scale 1.</summary>
        internal readonly float Top;

        /// <summary>The head's half width, the radius of the cast that looks ahead of it, in metres, at scale 1.</summary>
        internal readonly float Radius;

        internal Reach(float ahead, float top, float radius)
        {
            Ahead = ahead;
            Top = top;
            Radius = radius;
        }
    }

    /// <summary>
    /// Tames in pens, point 2 (decision 30): how far each tame's head reaches past its centre, from the game's own data
    /// (research, tames in pens, report C: each model's front and top in its rest pose, read from the game's files; the
    /// Lox's runs 4.42 m ahead of its centre against a body 1.5 m in radius). A creature not in the table reaches as far
    /// as its colliders' front plus 0.3 m. The head's width is the game's own for the Lox (its head capsule, 0.86 m) and
    /// the spirit bear (its head sphere, 0.54 m); for the others it is estimated from their models' widths (report C).
    /// Plain values, so the harness checks it.
    /// </summary>
    internal static class HeadReach
    {
        /// <summary>Added to a creature's colliders' front when the table doesn't know it.</summary>
        internal const float Unknown = 0.3f;

        // By prefab name (report C, sections 1 and 2: "Model in root space" and "Head bone"). The spirit companions share
        // their kin's models.
        private static readonly Dictionary<string, Reach> Known = new Dictionary<string, Reach>
        {
            ["Lox"] = new Reach(4.42f, 2.33f, 0.86f),          // the head capsule's top and radius; the model's 3.70 is its back
            ["Lox_Calf"] = new Reach(1.61f, 1.42f, 0.35f),
            ["Wolf"] = new Reach(1.21f, 1.40f, 0.25f),
            ["Wolf_cub"] = new Reach(0.61f, 0.70f, 0.12f),
            ["Wolf_spiritcaller"] = new Reach(1.21f, 1.40f, 0.25f),
            ["Boar"] = new Reach(1.05f, 1.03f, 0.25f),
            ["Boar_piggy"] = new Reach(0.52f, 0.52f, 0.12f),
            ["Boar_spiritcaller"] = new Reach(1.05f, 1.03f, 0.25f),
            ["Hen"] = new Reach(0.46f, 0.82f, 0.15f),
            ["Chicken"] = new Reach(0.61f, 1.07f, 0.15f),
            ["Asksvin"] = new Reach(1.80f, 2.03f, 0.40f),
            ["Asksvin_hatchling"] = new Reach(1.80f, 2.03f, 0.40f),   // the asksvin's: its root's 0.3 scale shrinks it (code review 2, finding 6)
            ["Moose"] = new Reach(2.64f, 3.43f, 0.50f),
            ["Moose_calf"] = new Reach(1.85f, 2.50f, 0.35f),
            ["Moose_spiritcaller"] = new Reach(2.64f, 3.43f, 0.50f),
            ["Bjorn_spiritcaller"] = new Reach(2.49f, 2.61f, 0.54f),   // its head sphere
        };

        /// <summary>The table's reach for a prefab name, if it has one.</summary>
        internal static bool TryKnown(string prefab, out Reach reach) => Known.TryGetValue(prefab ?? string.Empty, out reach);

        /// <summary>Every prefab the table knows, for the harness.</summary>
        internal static IEnumerable<string> KnownNames => Known.Keys;

        /// <summary>The reach of a creature the table doesn't know, from its root capsule: its front (its direction's half
        /// length along the body, else its radius, ahead of its centre) plus 0.3 m; its top, the capsule's top; its head
        /// half as wide as the capsule.</summary>
        /// <param name="direction">The capsule's axis: 0 x, 1 y (upright), 2 z (along the body).</param>
        internal static Reach FromCapsule(int direction, float centerY, float centerZ, float radius, float height)
        {
            float halfAlong = direction == 2 ? (height > 2f * radius ? height / 2f : radius) : radius;
            float top = direction == 1 ? centerY + (height > 2f * radius ? height / 2f : radius) : centerY + radius;
            float head = radius * 0.5f;
            return new Reach(centerZ + halfAlong + Unknown, top, head < 0.1f ? 0.1f : head > 0.9f ? 0.9f : head);
        }
    }
}
