using System;
using System.Collections.Generic;
using _Project.Vehicles;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace _Project.Traffic
{
    /// <summary>
    /// Traffic cars that are not on the road: a random one comes out for each spawn, so the colours mix, and new
    /// ones are made from the prefabs only when the pool is empty.
    /// </summary>
    public sealed class TrafficVehiclePool
    {
        private readonly List<Vehicle> _free = new List<Vehicle>(64);
        private readonly Vehicle[] _prefabs;
        private readonly Transform _parent;
        private readonly Action<Vehicle> _knockedOut;

        /// <param name="knockedOut">Subscribed once to every car this pool makes.</param>
        public TrafficVehiclePool(Vehicle[] prefabs, Transform parent, Action<Vehicle> knockedOut)
        {
            _prefabs = prefabs;
            _parent = parent;
            _knockedOut = knockedOut;
        }

        public Vehicle Take()
        {
            if (_free.Count > 0)
            {
                var index = Random.Range(0, _free.Count);
                var pooled = _free[index];
                _free[index] = _free[_free.Count - 1];
                _free.RemoveAt(_free.Count - 1);
                return pooled;
            }

            var prefab = _prefabs[Random.Range(0, _prefabs.Length)];
            var vehicle = Object.Instantiate(prefab, _parent);
            vehicle.KnockedOut += _knockedOut;
            return vehicle;
        }

        public void Return(Vehicle vehicle)
        {
            vehicle.gameObject.SetActive(false);
            _free.Add(vehicle);
        }
    }
}
