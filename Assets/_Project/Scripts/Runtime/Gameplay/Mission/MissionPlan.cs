using System.Collections.Generic;
using _Project.Common;
using UnityEngine;

namespace _Project.Mission
{
    /// <summary>The mission's arithmetic, apart from the scene: call order, compass sectors, distances on the radio.</summary>
    public static class MissionPlan
    {
        // The radio rounds distances to this many metres and never says less.
        private const int DistanceStep = 50;

        /// <summary>
        /// Splits the fires (their positions) into calls: one fire per call, nearest to <paramref name="start"/> first;
        /// with three or more fires the two farthest make the last call together. Returns indices into
        /// <paramref name="positions"/>.
        /// </summary>
        public static List<List<int>> BuildStages(IReadOnlyList<Vector3> positions, Vector3 start)
        {
            var order = new List<int>(positions.Count);
            for (var i = 0; i < positions.Count; i++)
            {
                order.Add(i);
            }

            order.Sort((a, b) => positions[a].HorizontalDistance(start).CompareTo(positions[b].HorizontalDistance(start)));
            var stages = new List<List<int>>();
            var singles = order.Count >= 3 ? order.Count - 2 : order.Count;
            for (var i = 0; i < singles; i++)
            {
                stages.Add(new List<int> { order[i] });
            }

            if (singles < order.Count)
            {
                stages.Add(order.GetRange(singles, order.Count - singles));
            }

            return stages;
        }

        /// <summary>Where <paramref name="to"/> is from <paramref name="from"/>: 0 north, clockwise in eighths.</summary>
        public static int CompassSector(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            return Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8;
        }

        /// <summary>A distance as the dispatcher says it: rounded to 50 m, at least 50 m.</summary>
        public static int RadioDistance(float metres)
        {
            return Mathf.Max(DistanceStep, Mathf.RoundToInt(metres / DistanceStep) * DistanceStep);
        }
    }
}
