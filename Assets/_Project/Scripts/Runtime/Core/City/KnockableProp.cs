using System;
using System.Collections.Generic;
using _Project.Vehicles;
using UnityEngine;

namespace _Project.City
{
    /// <summary>
    /// A street prop the player's car can knock over: a lamp, a traffic light, a road sign, a hydrant, a bin.
    /// It stands as a plain static collider and gets a rigidbody only when a driven car hits it hard enough,
    /// then stays where it fell.
    /// </summary>
    public sealed class KnockableProp : MonoBehaviour
    {
        // Share of the car's velocity the prop flies off with; the car keeps pushing it if it catches up.
        private const float PushFactor = 0.8f;
        // Stands in for ground friction, which the slippery fallen material no longer gives.
        private const float FallenDamping = 1f;

        private static readonly List<Collider> Colliders = new List<Collider>(4);

        [SerializeField] private PropKind kind;
        [SerializeField] private float mass = 100f;
        [Tooltip("Relative speed of the hit, m/s, that knocks the prop over.")]
        [SerializeField] private float breakSpeed = 5f;
        [Tooltip("Share of its speed the car keeps after the hit.")]
        [Range(0f, 1f)] [SerializeField] private float carSpeedKept = 0.9f;
        [Tooltip("Tip-over rate, rad/s per m/s of the hit: the top falls away from the car.")]
        [SerializeField] private float tipRate = 0.25f;
        [Tooltip("How much a car dents on hitting this prop while it stands, against 1 for a wall.")]
        [Range(0f, 1f)] [SerializeField] private float dentHardness = 0.6f;
        [Tooltip("Low-friction material for the fallen prop, so a car slides over it instead of beaching on it.")]
        [SerializeField] private PhysicsMaterial fallenMaterial;

        /// <summary>Raised once, when the prop is knocked over.</summary>
        public event Action<KnockableProp> Knocked;

        public bool IsKnocked { get; private set; }
        public float DentHardness => dentHardness;
        public PropKind Kind => kind;

        /// <summary>Velocity of the car at the moment it knocked this prop over.</summary>
        public Vector3 HitVelocity { get; private set; }

        private float _knockedAt;

        /// <summary>Knocked over in an earlier physics step than <paramref name="fixedTime"/>: lying debris.</summary>
        public bool WasLyingBefore(float fixedTime)
        {
            return IsKnocked && _knockedAt < fixedTime;
        }

        public void Setup(float propMass, float propBreakSpeed, float hardness, float speedKept,
            PhysicsMaterial material)
        {
            mass = propMass;
            breakSpeed = propBreakSpeed;
            dentHardness = hardness;
            carSpeedKept = speedKept;
            fallenMaterial = material;
        }

        public void SetKind(PropKind propKind)
        {
            kind = propKind;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsKnocked || collision.rigidbody == null ||
                !collision.rigidbody.TryGetComponent<Vehicle>(out var vehicle))
            {
                return;
            }

            var hardHit = collision.relativeVelocity.magnitude > breakSpeed;
            if (vehicle.Mode == VehicleMode.Driven && hardHit)
            {
                KnockOver(vehicle, collision.GetContact(0).point);
            }
        }

        private void KnockOver(Vehicle vehicle, Vector3 point)
        {
            IsKnocked = true;
            _knockedAt = Time.fixedTime;
            // The solver has already stopped the car against the still static collider; give its speed back.
            var carVelocity = vehicle.PreStepVelocity;
            HitVelocity = carVelocity;
            vehicle.HitProp(point, carSpeedKept);

            SetLayer(transform, PhysicsLayers.Debris);
            GetComponents(Colliders);
            for (var i = 0; i < Colliders.Count; i++)
            {
                Colliders[i].sharedMaterial = fallenMaterial;
            }

            var body = gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = FallenDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var push = Vector3.ProjectOnPlane(carVelocity, Vector3.up);
            body.linearVelocity = push * PushFactor;
            body.angularVelocity = Vector3.Cross(Vector3.up, push) * tipRate;
            Knocked?.Invoke(this);
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (var i = 0; i < root.childCount; i++)
            {
                SetLayer(root.GetChild(i), layer);
            }
        }
    }
}
