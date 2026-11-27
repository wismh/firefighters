using _Project.Vehicles;
using UnityEngine;

namespace _Project.Traffic
{
    [CreateAssetMenu(menuName = "Configurations/City/" + nameof(TrafficSettings),
        fileName = nameof(TrafficSettings) + "_Default")]
    public sealed class TrafficSettings : ScriptableObject
    {
        [Header("Population")]
        public Vehicle[] vehiclePrefabs;
        [Min(0)] public int vehicleCount = 40;
        [Tooltip("New cars appear only this far from the player.")]
        public float respawnMinDistance = 70f;
        [Tooltip("Parked or wrecked cars this far from the player return to the pool.")]
        public float despawnDistance = 90f;

        [Header("Speeds, m/s")]
        public float roadSpeed = 13f;
        public float straightThroughSpeed = 11f;
        public float turnSpeed = 6f;

        [Header("Car following (IDM)")]
        public float maxAcceleration = 2.5f;
        public float comfortableBraking = 4f;
        public float maxBraking = 9f;
        public float minGap = 2.5f;
        public float timeHeadway = 1.2f;
        public float lookAhead = 60f;

        [Header("Signals, s")]
        public float greenTime = 12f;
        public float yellowTime = 3f;
        public float allRedTime = 1.5f;

        public float SignalCycle => 2f * (greenTime + yellowTime + allRedTime);
    }
}
