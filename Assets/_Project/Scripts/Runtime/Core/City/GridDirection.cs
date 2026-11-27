using UnityEngine;

namespace _Project.City
{
    /// <summary>Four street directions on the city grid. Even values run along X, odd values along Z.</summary>
    public enum GridDirection
    {
        East = 0,
        North = 1,
        West = 2,
        South = 3,
    }

    public static class GridDirections
    {
        public static readonly GridDirection[] All =
        {
            GridDirection.East, GridDirection.North, GridDirection.West, GridDirection.South,
        };

        public static Vector3 ToVector(this GridDirection dir) => dir switch
        {
            GridDirection.East => Vector3.right,
            GridDirection.North => Vector3.forward,
            GridDirection.West => Vector3.left,
            _ => Vector3.back,
        };

        public static Vector2Int ToOffset(this GridDirection dir) => dir switch
        {
            GridDirection.East => new Vector2Int(1, 0),
            GridDirection.North => new Vector2Int(0, 1),
            GridDirection.West => new Vector2Int(-1, 0),
            _ => new Vector2Int(0, -1),
        };

        public static GridDirection Opposite(this GridDirection dir) => (GridDirection)(((int)dir + 2) % 4);

        /// <summary>0 for streets running along X, 1 for streets running along Z.</summary>
        public static int Axis(this GridDirection dir) => (int)dir % 2;

        /// <summary>Right-hand side of a car driving in this direction (right-hand traffic).</summary>
        public static Vector3 Right(this GridDirection dir)
        {
            var v = dir.ToVector();
            return new Vector3(v.z, 0f, -v.x);
        }

        /// <summary>Y rotation that turns local +Z into this direction.</summary>
        public static float YawFromForward(this GridDirection dir) => dir switch
        {
            GridDirection.North => 0f,
            GridDirection.East => 90f,
            GridDirection.South => 180f,
            _ => 270f,
        };
    }
}
