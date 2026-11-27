using _Project.Mission;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests
{
    public sealed class MissionPlanTests
    {
        [Test]
        public void BuildStages_NearestFirst_TwoFarthestTogether()
        {
            var fires = new[]
            {
                new Vector3(300f, 0f, 0f), new Vector3(100f, 0f, 0f), new Vector3(400f, 0f, 0f),
                new Vector3(200f, 0f, 0f),
            };

            var stages = MissionPlan.BuildStages(fires, Vector3.zero);

            Assert.AreEqual(3, stages.Count);
            CollectionAssert.AreEqual(new[] { 1 }, stages[0]);
            CollectionAssert.AreEqual(new[] { 3 }, stages[1]);
            CollectionAssert.AreEquivalent(new[] { 0, 2 }, stages[2]);
        }

        [Test]
        public void BuildStages_TwoFires_AreTwoSingleCalls()
        {
            var fires = new[] { new Vector3(0f, 0f, 50f), new Vector3(0f, 0f, 10f) };

            var stages = MissionPlan.BuildStages(fires, Vector3.zero);

            Assert.AreEqual(2, stages.Count);
            CollectionAssert.AreEqual(new[] { 1 }, stages[0]);
            CollectionAssert.AreEqual(new[] { 0 }, stages[1]);
        }

        [Test]
        public void BuildStages_IgnoresHeight()
        {
            var fires = new[] { new Vector3(0f, 500f, 10f), new Vector3(0f, 0f, 20f) };

            var stages = MissionPlan.BuildStages(fires, Vector3.zero);

            CollectionAssert.AreEqual(new[] { 0 }, stages[0]);
        }

        [TestCase(0f, 10f, 0)]
        [TestCase(10f, 10f, 1)]
        [TestCase(10f, 0f, 2)]
        [TestCase(0f, -10f, 4)]
        [TestCase(-10f, 0f, 6)]
        [TestCase(-10f, 10f, 7)]
        public void CompassSector_ClockwiseFromNorth(float x, float z, int sector)
        {
            Assert.AreEqual(sector, MissionPlan.CompassSector(Vector3.zero, new Vector3(x, 0f, z)));
        }

        [TestCase(10f, 50)]
        [TestCase(74f, 50)]
        [TestCase(76f, 100)]
        [TestCase(260f, 250)]
        public void RadioDistance_RoundsToFiftyWithAMinimum(float metres, int said)
        {
            Assert.AreEqual(said, MissionPlan.RadioDistance(metres));
        }
    }
}
