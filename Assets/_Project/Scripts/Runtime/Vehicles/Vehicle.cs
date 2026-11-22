using System;
using _Project.City;
using _Project.FireHose;
using UnityEngine;

namespace _Project.Vehicles
{
    /// <summary>
    /// One car prefab, three modes: kinematic traffic car, player-driven physics car, parked physics car.
    /// The player can take any traffic car, so both behaviours live on the same object.
    /// </summary>
    public sealed class Vehicle : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private VehicleController controller;
        [SerializeField] private Transform exitPoint;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private float length = 5f;
        [SerializeField] private float width = 3f;
        [SerializeField] private float knockoutSpeed = 4f;
        [Tooltip("Optional: only fire trucks carry a hose reel.")]
        [SerializeField] private FireHoseReel hoseReel;
        [Tooltip("Placed in the scene as a parked car instead of being spawned by traffic.")]
        [SerializeField] private bool parkedOnStart;

        private Collider[] _colliders;
        private Vector3[] _wheelBasePositions;
        private Quaternion[] _wheelBaseRotations;
        private float _wheelSpin;

        /// <summary>Raised when a driven car hits this traffic car hard enough to knock it out of traffic.</summary>
        public event Action<Vehicle> KnockedOut;

        /// <summary>Raised when this car knocks a street prop over: the contact point and the velocity lost.</summary>
        public event Action<Vector3, Vector3> PropHit;

        public VehicleMode Mode { get; private set; } = VehicleMode.Traffic;
        public Rigidbody Body => body;
        public VehicleController Controller => controller;
        public Transform ExitPoint => exitPoint;
        public Transform CameraTarget => cameraTarget;
        public float Length => length;
        public float Width => width;
        public float HalfLength => length * 0.5f;

        /// <summary>The fire hose reel, or null for cars without one.</summary>
        public FireHoseReel HoseReel => hoseReel;
        public float Speed => body.isKinematic ? TrafficSpeed : body.linearVelocity.magnitude;

        /// <summary>Speed set by the traffic system while kinematic.</summary>
        public float TrafficSpeed { get; set; }

        public void Setup(Rigidbody rigidbody, VehicleController vehicleController, Transform exit, Transform camera,
            float vehicleLength, float vehicleWidth)
        {
            body = rigidbody;
            controller = vehicleController;
            exitPoint = exit;
            cameraTarget = camera;
            length = vehicleLength;
            width = vehicleWidth;
        }

        public void SetHoseReel(FireHoseReel reel)
        {
            hoseReel = reel;
        }

        public void SetMode(VehicleMode mode)
        {
            var wasKinematic = body.isKinematic;
            Mode = mode;
            var kinematic = mode == VehicleMode.Traffic;
            if (!kinematic && wasKinematic)
            {
                body.isKinematic = false;
                body.linearVelocity = transform.forward * TrafficSpeed;
                body.angularVelocity = Vector3.zero;
            }
            else if (kinematic)
            {
                body.isKinematic = true;
            }

            controller.enabled = !kinematic;
            controller.SetDriver(mode == VehicleMode.Driven);
            ApplyLayer();
        }

        /// <summary>
        /// The car just knocked a prop over: the hit already stopped it against the prop's static collider, so it
        /// gets its speed before the hit back, less what the prop took.
        /// </summary>
        public void HitProp(Vector3 point, float speedKept)
        {
            var before = controller.PreStepVelocity;
            var after = before * speedKept;
            body.linearVelocity = after;
            body.angularVelocity = controller.PreStepAngularVelocity;
            PropHit?.Invoke(point, before - after);
        }

        /// <summary>Kinematic move along the lane; wheels spin by the travelled distance.</summary>
        public void MoveKinematic(Vector3 position, Quaternion rotation, float distance)
        {
            body.MovePosition(position);
            body.MoveRotation(rotation);
            var wheels = controller.Wheels;
            var radius = wheels[0].collider.radius;
            _wheelSpin = (_wheelSpin + distance / radius * Mathf.Rad2Deg) % 360f;
            // Visual wheels sit under a model turned by 180 degrees, so their local X points to the car's left.
            var spin = Quaternion.Euler(-_wheelSpin, 0f, 0f);
            for (var i = 0; i < wheels.Length; i++)
            {
                wheels[i].visual.localRotation = _wheelBaseRotations[i] * spin;
            }
        }

        /// <summary>Puts a pooled car back into its traffic state.</summary>
        public void ResetForTraffic(Vector3 position, Quaternion rotation)
        {
            SetMode(VehicleMode.Traffic);
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            TrafficSpeed = 0f;
            _wheelSpin = 0f;
            var wheels = controller.Wheels;
            for (var i = 0; i < wheels.Length; i++)
            {
                wheels[i].visual.SetLocalPositionAndRotation(_wheelBasePositions[i], _wheelBaseRotations[i]);
            }
        }

        private void Awake()
        {
            ApplyLayer();
            var wheels = controller.Wheels;
            _wheelBasePositions = new Vector3[wheels.Length];
            _wheelBaseRotations = new Quaternion[wheels.Length];
            for (var i = 0; i < wheels.Length; i++)
            {
                _wheelBasePositions[i] = wheels[i].visual.localPosition;
                _wheelBaseRotations[i] = wheels[i].visual.localRotation;
            }
        }

        private void Start()
        {
            // The player may already have taken this car (the start truck), so only an unclaimed car parks.
            if (parkedOnStart && Mode == VehicleMode.Traffic)
            {
                SetMode(VehicleMode.Parked);
            }
        }

        // Traffic cars sit on their own layer, which ignores knocked-over props.
        private void ApplyLayer()
        {
            _colliders ??= GetComponentsInChildren<Collider>(true);
            var layer = Mode == VehicleMode.Traffic ? PhysicsLayers.Traffic : PhysicsLayers.Default;
            for (var i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].gameObject.layer = layer;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Mode != VehicleMode.Traffic || collision.rigidbody == null)
            {
                return;
            }

            var other = collision.rigidbody.GetComponent<Vehicle>();
            var hitByPlayer = other != null && other.Mode == VehicleMode.Driven;
            if (hitByPlayer && collision.relativeVelocity.magnitude > knockoutSpeed)
            {
                KnockedOut?.Invoke(this);
            }
        }
    }
}
