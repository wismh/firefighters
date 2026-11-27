using UnityEngine;

namespace _Project.Common
{
    /// <summary>Ground-plane maths: the city is flat, so most distances ignore height.</summary>
    public static class VectorExtensions
    {
        /// <summary>The vector with its height dropped.</summary>
        public static Vector3 Flat(this Vector3 vector)
        {
            vector.y = 0f;
            return vector;
        }

        /// <summary>Distance between two points on the ground plane, ignoring height.</summary>
        public static float HorizontalDistance(this Vector3 from, Vector3 to)
        {
            var dx = to.x - from.x;
            var dz = to.z - from.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
