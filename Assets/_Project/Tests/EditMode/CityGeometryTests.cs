using _Project.City;
using _Project.Common;
using _Project.Pedestrians;
using _Project.Traffic;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests
{
    public sealed class CityGeometryTests
    {
        [Test]
        public void HorizontalDistance_IgnoresHeight()
        {
            Assert.AreEqual(5f, new Vector3(0f, 10f, 0f).HorizontalDistance(new Vector3(3f, -4f, 4f)), 1e-5f);
            Assert.AreEqual(new Vector3(1f, 0f, 2f), new Vector3(1f, 7f, 2f).Flat());
        }

        [Test]
        public void GridDirection_OppositeAndAxis()
        {
            foreach (var direction in GridDirections.All)
            {
                Assert.AreEqual(direction, direction.Opposite().Opposite());
                Assert.AreEqual(direction.Axis(), direction.Opposite().Axis());
                Assert.AreEqual(-direction.ToVector(), direction.Opposite().ToVector());
            }
        }

        [Test]
        public void RoadGraph_HasAnIntersectionPerNode_AndTwoLanesPerStreet()
        {
            var layout = ScriptableObject.CreateInstance<CityLayout>();
            var settings = ScriptableObject.CreateInstance<TrafficSettings>();
            try
            {
                layout.blocksX = 3;
                layout.blocksZ = 2;

                var graph = RoadGraphBuilder.Build(layout, settings);

                Assert.AreEqual(layout.NodesX * layout.NodesZ, graph.Intersections.Count);
                var streets = layout.blocksX * layout.NodesZ + layout.blocksZ * layout.NodesX;
                Assert.AreEqual(streets * 2, graph.RoadLanes.Count);
            }
            finally
            {
                Object.DestroyImmediate(layout);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void SidewalkGraph_KnowsAsphaltFromSidewalk()
        {
            var layout = ScriptableObject.CreateInstance<CityLayout>();
            var settings = ScriptableObject.CreateInstance<TrafficSettings>();
            try
            {
                layout.blocksX = 2;
                layout.blocksZ = 2;
                var sidewalks = new SidewalkGraph(layout, RoadGraphBuilder.Build(layout, settings));
                var middleOfBlock = new Vector3(layout.Period * 0.5f, 0f, layout.Period * 0.5f);

                Assert.IsTrue(sidewalks.IsOnRoad(Vector3.zero));
                Assert.IsFalse(sidewalks.IsOnRoad(middleOfBlock));
                Assert.AreEqual(CityLayout.AsphaltHeight, sidewalks.GroundHeight(Vector3.zero));
                Assert.Greater(sidewalks.Points.Length, 0);
            }
            finally
            {
                Object.DestroyImmediate(layout);
                Object.DestroyImmediate(settings);
            }
        }
    }
}
