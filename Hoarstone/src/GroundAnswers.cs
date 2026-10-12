using Hoarstone.Shapes;
using QoLMods.HoarstoneGround;
using UnityEngine;

namespace Hoarstone
{
    /// <summary>
    /// What Hoarstone answers for the pack's other modules (card B26: House Trained's tames keep to a Hoarstone's ground),
    /// through the Hoarstone ground part: whether it's on, whether a point is inside a settlement's shape (the very lookup
    /// its spawn check makes, Coverage.Inside), and the nearest Hoarstone among the pieces its game has seen this session.
    /// Nothing here changes what Hoarstone does (a lookup may read a zone's saved shape into its cache, as the spawn check
    /// does).
    /// </summary>
    internal sealed class GroundAnswers : IGroundSource
    {
        public bool On => Module.IsOn;

        public bool Inside(Vector3 point, int layers) => Module.IsOn && Coverage.Inside(point, layers);

        public bool NearestStone(Vector3 from, float within, out Vector3 stone)
        {
            stone = from;
            if (!Module.IsOn)
            {
                return false;
            }
            double best = (double)within * within;
            bool found = false;
            foreach (ShapePoint point in Coverage.Points)
            {
                if (!point.Stone)
                {
                    continue;
                }
                double dx = point.X - from.x;
                double dz = point.Z - from.z;
                double distance = dx * dx + dz * dz;
                if (distance <= best)
                {
                    best = distance;
                    stone = new Vector3((float)point.X, from.y, (float)point.Z);
                    found = true;
                }
            }
            return found;
        }
    }
}
