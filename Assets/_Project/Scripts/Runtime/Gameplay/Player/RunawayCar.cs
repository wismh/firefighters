using System;
using _Project.Vehicles;
using UnityEngine;
using Zenject;

namespace _Project.Player
{
    /// <summary>
    /// The car the player jumped out of: it rolls freely for a moment, then brakes itself to a stop. It stays in
    /// the Driven mode meanwhile, so it still knocks lamps, cars and people over like a driven car.
    /// </summary>
    public sealed class RunawayCar : ITickable
    {
        private const float StoppedSpeed = 0.5f;
        private const float BrakeSpeed = 1.5f;
        // A car that will not settle (on its roof, in the air) is let go after this long.
        private const float MaxSeconds = 12f;

        private float _time;
        private float _coastSeconds;

        /// <summary>
        /// Raised when the runaway is let go: the car, and whether it should be parked (false when the traffic
        /// already took it back meanwhile).
        /// </summary>
        public event Action<Vehicle, bool> Released;

        /// <summary>The rolling car, or null.</summary>
        public Vehicle Car { get; private set; }

        /// <summary>Lets <paramref name="car"/> roll on its own; it starts braking after <paramref name="coastSeconds"/>.</summary>
        public void Start(Vehicle car, float coastSeconds)
        {
            Car = car;
            _time = 0f;
            _coastSeconds = coastSeconds;
            car.Drive(0f, 0f, false);
        }

        /// <summary>Lets the car go now, e.g. the player took another one.</summary>
        public void Release(bool park)
        {
            var car = Car;
            Car = null;
            Released?.Invoke(car, park);
        }

        public void Tick()
        {
            if (Car == null)
            {
                return;
            }

            _time += Time.deltaTime;
            var braking = _time > _coastSeconds;
            var forward = Car.ForwardSpeed;
            // Throttle against the motion is the foot brake.
            var pedal = braking && Mathf.Abs(forward) > BrakeSpeed ? -Mathf.Sign(forward) : 0f;
            Car.Drive(pedal, 0f, braking);
            // The traffic may have taken a far car back into its pool meanwhile.
            var gone = !Car.isActiveAndEnabled || Car.Mode != VehicleMode.Driven;
            var stopped = _time > 0.5f && Car.Speed < StoppedSpeed;
            if (gone || stopped || _time > MaxSeconds)
            {
                Release(!gone);
            }
        }
    }
}
