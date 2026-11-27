using System;
using _Project.Vehicles;
using UnityEngine;

namespace _Project.Player
{
    /// <summary>
    /// What the rest of the game may know about the player: on foot, at the wheel or with the hose, where the player
    /// is and which cars are involved. Read-only: only the player's own controller changes it.
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>Raised when the player gets into a car.</summary>
        event Action<Vehicle> VehicleEntered;

        /// <summary>Raised when the player jumps out of a moving car: the car and its speed, m/s.</summary>
        event Action<Vehicle, float> JumpedOut;

        /// <summary>Raised when the player leaves a car; after a jump, once the car has come to a stop.</summary>
        event Action<Vehicle> VehicleExited;

        bool IsDriving { get; }

        bool IsHosing { get; }

        /// <summary>The car being driven, or null on foot.</summary>
        Vehicle Driven { get; }

        /// <summary>The car being driven, or the one that still rolls after the player jumped out of it.</summary>
        Vehicle MovingCar { get; }

        /// <summary>The last fire truck the player drove or took the hose from.</summary>
        Vehicle FireTruck { get; }

        /// <summary>The car while driving, the body on foot.</summary>
        Transform MapFocus { get; }

        /// <summary>Where the player looks, as a yaw in degrees.</summary>
        float Heading { get; }

        /// <summary>Speed of the driven car, m/s; 0 on foot.</summary>
        float DrivingSpeed { get; }

        /// <summary>What the player can do right now, for the controls hint.</summary>
        InteractionHint Hint { get; }
    }
}
