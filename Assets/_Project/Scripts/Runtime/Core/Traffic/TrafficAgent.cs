using System.Collections.Generic;
using _Project.Vehicles;

namespace _Project.Traffic
{
    /// <summary>Traffic state of one kinematic car. Owned and ticked by <see cref="TrafficSystem"/>.</summary>
    public sealed class TrafficAgent
    {
        public Vehicle Vehicle { get; set; }
        public Lane Lane { get; set; }

        /// <summary>Distance of the vehicle centre along <see cref="Lane"/>.</summary>
        public float S { get; set; }

        public float Speed { get; set; }
        public float Acceleration { get; set; }

        /// <summary>Lanes chosen ahead of the current one; the leader search and the turn slowdown read them.</summary>
        public List<Lane> Path { get; } = new List<Lane>(4);

        /// <summary>Connector reserved in the intersection ahead, kept until the car leaves it.</summary>
        public Lane CommittedConnector { get; set; }

        public Lane ReservedExitLane { get; set; }
        public float ReservedAmount { get; set; }

        /// <summary>Connector just left: its reservation is released once the rear bumper clears it.</summary>
        public Lane ReleasePendingConnector { get; set; }

        public float HalfLength => Vehicle.HalfLength;

        public void Clear()
        {
            Vehicle = null;
            Lane = null;
            Path.Clear();
            CommittedConnector = null;
            ReservedExitLane = null;
            ReservedAmount = 0f;
            ReleasePendingConnector = null;
            S = 0f;
            Speed = 0f;
            Acceleration = 0f;
        }
    }
}
