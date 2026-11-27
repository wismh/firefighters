using UnityEngine;

namespace _Project.City
{
    /// <summary>
    /// Data that defines the whole city: a Manhattan grid of intersections with square blocks between them.
    /// The scene builder and the traffic lane graph both read this, so roads and traffic never drift apart.
    /// </summary>
    [CreateAssetMenu(menuName = "Configurations/City/" + nameof(CityLayout), fileName = nameof(CityLayout) + "_Default")]
    public sealed class CityLayout : ScriptableObject
    {
        /// <summary>Road cross-section of SimpleTown tiles: 2.5 m sidewalk + 15 m asphalt + 2.5 m sidewalk.</summary>
        public const float RoadWidth = 20f;
        public const float HalfRoad = RoadWidth * 0.5f;
        public const float SidewalkWidth = 2.5f;
        public const float LaneOffset = 3.75f;
        /// <summary>Asphalt sits 0.19 m below the sidewalk top (y = 0).</summary>
        public const float AsphaltHeight = -0.19f;
        public const float TileStep = 5f;

        [Min(1)] public int blocksX = 5;
        [Min(1)] public int blocksZ = 5;
        [Tooltip("Block side in metres, multiple of 5.")]
        [Min(20)] public int blockSize = 50;
        public int seed = 12345;
        [Tooltip("How many buildings are on fire.")]
        [Min(0)] public int burningBuildings = 4;

        [Header("Road tiles (SimpleTown)")]
        public GameObject roadStraightHalf;
        public GameObject roadCrossingHalf;
        public GameObject roadQuarterCorner;
        public GameObject roadT;
        public GameObject roadCornerTurn;
        public GameObject grassTile;

        [Header("Buildings")]
        public GameObject[] downtownBuildings;
        public GameObject[] suburbBuildings;

        [Header("Props")]
        public GameObject streetLamp;
        public GameObject trafficLight;
        public GameObject[] trees;
        public GameObject[] sidewalkProps;

        [Header("Road signs over the road (SimpleTown)")]
        [Tooltip("Gantry: poles at local x = 0 and x = 17, spans the whole road.")]
        public GameObject signGantry;
        [Tooltip("Cantilever: pole at the origin, arm along local +X.")]
        public GameObject signArmPlusX;
        [Tooltip("Cantilever: pole at the origin, arm along local -X.")]
        public GameObject signArmMinusX;

        public float Period => RoadWidth + blockSize;
        public int NodesX => blocksX + 1;
        public int NodesZ => blocksZ + 1;
        public Vector2 Size => new Vector2(blocksX * Period, blocksZ * Period);

        public Vector3 NodePosition(int ix, int iz) => new Vector3(ix * Period, 0f, iz * Period);

        public Vector3 Center => new Vector3(blocksX * Period * 0.5f, 0f, blocksZ * Period * 0.5f);

        public bool IsNode(int ix, int iz) => ix >= 0 && iz >= 0 && ix < NodesX && iz < NodesZ;
    }
}
