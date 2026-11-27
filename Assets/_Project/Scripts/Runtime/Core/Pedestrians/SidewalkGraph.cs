using System.Collections.Generic;
using _Project.City;
using _Project.Traffic;
using UnityEngine;

namespace _Project.Pedestrians
{
    /// <summary>
    /// Where pedestrians walk, derived from the city layout: the sidewalk loop around every block and the
    /// crosswalks on the first tile of each road. Every block corner has three points: the corner itself and the
    /// two crosswalk ends next to it.
    /// </summary>
    public sealed class SidewalkGraph
    {
        // Distance outside the block edge: clear of the lamps and bins nearer the kerb.
        private const float EdgeOffset = 0.05f;
        // Crosswalks run down the middle of the first road tile.
        private const float CrosswalkInset = CityLayout.TileStep * 0.5f;
        private const int PointsPerCorner = 3;
        private const int Corner = 0;
        private const int AlongX = 1;
        private const int AlongZ = 2;

        private readonly CityLayout _layout;
        private readonly RoadGraph _roads;
        private readonly List<SidewalkLink>[] _links;

        public SidewalkGraph(CityLayout layout, RoadGraph roads)
        {
            _layout = layout;
            _roads = roads;
            var count = layout.blocksX * layout.blocksZ * 4 * PointsPerCorner;
            Points = new Vector3[count];
            _links = new List<SidewalkLink>[count];
            for (var i = 0; i < count; i++)
            {
                _links[i] = new List<SidewalkLink>(4);
            }

            for (var bx = 0; bx < layout.blocksX; bx++)
            {
                for (var bz = 0; bz < layout.blocksZ; bz++)
                {
                    AddBlock(bx, bz);
                }
            }
        }

        public Vector3[] Points { get; }

        public List<SidewalkLink> Links(int point) => _links[point];

        /// <summary>The asphalt of some road is under this point.</summary>
        public bool IsOnRoad(Vector3 position)
        {
            const float halfAsphalt = CityLayout.HalfRoad - CityLayout.SidewalkWidth;
            return RoadDistance(position.x) < halfAsphalt || RoadDistance(position.z) < halfAsphalt;
        }

        public float GroundHeight(Vector3 position)
        {
            return IsOnRoad(position) ? CityLayout.AsphaltHeight : 0f;
        }

        private float RoadDistance(float coordinate)
        {
            var period = _layout.Period;
            return Mathf.Abs(coordinate - Mathf.Round(coordinate / period) * period);
        }

        private int Index(int bx, int bz, int cx, int cz, int kind)
        {
            return (((bx * _layout.blocksZ + bz) * 2 + cx) * 2 + cz) * PointsPerCorner + kind;
        }

        private void AddBlock(int bx, int bz)
        {
            var min = new Vector2(bx, bz) * _layout.Period + Vector2.one * CityLayout.HalfRoad;
            for (var cx = 0; cx < 2; cx++)
            {
                for (var cz = 0; cz < 2; cz++)
                {
                    var sx = cx == 0 ? -1f : 1f;
                    var sz = cz == 0 ? -1f : 1f;
                    var edgeX = min.x + cx * _layout.blockSize;
                    var edgeZ = min.y + cz * _layout.blockSize;
                    var corner = Index(bx, bz, cx, cz, Corner);
                    var alongX = Index(bx, bz, cx, cz, AlongX);
                    var alongZ = Index(bx, bz, cx, cz, AlongZ);
                    Points[corner] = new Vector3(edgeX + sx * EdgeOffset, 0f, edgeZ + sz * EdgeOffset);
                    Points[alongX] = new Vector3(edgeX - sx * CrosswalkInset, 0f, edgeZ + sz * EdgeOffset);
                    Points[alongZ] = new Vector3(edgeX + sx * EdgeOffset, 0f, edgeZ - sz * CrosswalkInset);
                    Connect(corner, alongX);
                    Connect(corner, alongZ);
                }
            }

            for (var c = 0; c < 2; c++)
            {
                Connect(Index(bx, bz, 0, c, AlongX), Index(bx, bz, 1, c, AlongX));
                Connect(Index(bx, bz, c, 0, AlongZ), Index(bx, bz, c, 1, AlongZ));

                // Over the road south of this block, and over the one west of it.
                if (bz > 0)
                {
                    Connect(Index(bx, bz, c, 0, AlongX), Index(bx, bz - 1, c, 1, AlongX),
                        _roads.Intersections[new Vector2Int(bx + c, bz)], GridDirection.East.Axis());
                }

                if (bx > 0)
                {
                    Connect(Index(bx, bz, 0, c, AlongZ), Index(bx - 1, bz, 1, c, AlongZ),
                        _roads.Intersections[new Vector2Int(bx, bz + c)], GridDirection.North.Axis());
                }
            }
        }

        private void Connect(int a, int b, Intersection crossing = null, int roadAxis = 0)
        {
            _links[a].Add(new SidewalkLink(b, crossing, roadAxis));
            _links[b].Add(new SidewalkLink(a, crossing, roadAxis));
        }
    }
}
