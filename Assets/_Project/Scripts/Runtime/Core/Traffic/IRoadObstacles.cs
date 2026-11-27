using System.Collections.Generic;
using UnityEngine;

namespace _Project.Traffic
{
    /// <summary>Things on the asphalt that traffic gives way to (people crossing). Read every physics step.</summary>
    public interface IRoadObstacles
    {
        IReadOnlyList<Vector3> PointsOnRoad { get; }
    }
}
