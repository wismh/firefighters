using System.Collections.Generic;
using UnityEngine;

namespace _Project.Traffic
{
    public sealed class RoadGraph
    {
        public List<Lane> Lanes { get; } = new List<Lane>();

        /// <summary>Lanes between intersections (no connectors): where vehicles spawn.</summary>
        public List<Lane> RoadLanes { get; } = new List<Lane>();

        public Dictionary<Vector2Int, Intersection> Intersections { get; } =
            new Dictionary<Vector2Int, Intersection>();
    }
}
