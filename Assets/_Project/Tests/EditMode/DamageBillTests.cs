using _Project.City;
using _Project.Economy;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests
{
    public sealed class DamageBillTests
    {
        private DamagePrices _prices;
        private DamageBill _bill;

        [SetUp]
        public void SetUp()
        {
            _prices = ScriptableObject.CreateInstance<DamagePrices>();
            _bill = new DamageBill(_prices);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_prices);
        }

        [Test]
        public void AddProp_ChargesThePriceOfItsKind()
        {
            var cost = _bill.AddProp(PropKind.TrafficLight);

            Assert.AreEqual(_prices.PriceOf(PropKind.TrafficLight), cost);
            Assert.AreEqual(cost, _bill.Total);
            Assert.AreEqual(1, _bill.PropsBroken);
        }

        [Test]
        public void AddProp_Hydrant_CountsAnOpenedHydrant()
        {
            _bill.AddProp(PropKind.Hydrant);
            _bill.AddProp(PropKind.Lamp);

            Assert.AreEqual(1, _bill.HydrantsOpened);
            Assert.AreEqual(2, _bill.PropsBroken);
        }

        [Test]
        public void AddDent_RoundsToTensWithAMinimum()
        {
            Assert.AreEqual(10, _bill.AddDent(0f));
            Assert.AreEqual(600, _bill.AddDent(0.1f));
            Assert.AreEqual(610, _bill.Total);
        }

        [Test]
        public void Counters_DoNotChargeMoney()
        {
            _bill.AddPedestrianHit();
            _bill.AddSoakedDriver();
            _bill.AddSoakedPedestrian();
            _bill.AddSavedBuilding();

            Assert.AreEqual(0, _bill.Total);
            Assert.AreEqual(1, _bill.PedestriansHit);
            Assert.AreEqual(1, _bill.DriversSoaked);
            Assert.AreEqual(1, _bill.PedestriansSoaked);
            Assert.AreEqual(1, _bill.BuildingsSaved);
        }

        [Test]
        public void EveryChange_RaisesChanged()
        {
            var raised = 0;
            _bill.Changed += () => raised++;

            _bill.AddKnockout();
            _bill.AddMoralDamage();
            _bill.AddSavedBuilding();

            Assert.AreEqual(3, raised);
            Assert.AreEqual(_prices.Knockout + _prices.MoralDamage, _bill.Total);
        }
    }
}
