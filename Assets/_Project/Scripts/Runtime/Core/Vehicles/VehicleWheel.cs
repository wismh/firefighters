using System;
using UnityEngine;

namespace _Project.Vehicles
{
    [Serializable]
    public sealed class VehicleWheel
    {
        public WheelCollider collider;
        public Transform visual;
        public bool steer;
        public bool drive;
        public bool handbrake;
    }
}
