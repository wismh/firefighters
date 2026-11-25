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
        [SerializeField] private VehicleDamage damage;
        [Tooltip("Placed in the scene as a parked car instead of being spawned by traffic.")]
        [SerializeField] private bool parkedOnStart;

        private const float GroundNormalY = 0.7f;
        private const float DebrisHardness = 0.1f;
        // Dents centre this far above the lowest contact: bumper height on the body collider.
        private const float BumperRise = 0.3f;

        private Collider[] _colliders;
        private Vector3[] _wheelBasePositions;
        private Quaternion[] _wheelBaseRotations;
        private float _wheelSpin;

        /// <summary>Raised when a driven car hits this traffic car hard enough to knock it out of traffic.</summary>
        public event Action<Vehicle> KnockedOut;

        /// <summary>Raised when this car knocks a street prop over: the contact point and the velocity lost.</summary>
        public event Action<Vector3, Vector3> PropHit;

        /// <summary>Raised when the body dents: the car, the dent's centre, and its depth in metres.</summary>
        public event Action<Vehicle, Vector3, float> Crashed;

        /// <summary>No wheel touches anything: the car is flying.</summary>
        public bool IsAirborne
        {
            get
            {
                var wheels = controller.Wheels;
                for (var i = 0; i < wheels.Length; i++)
                {
                    if (wheels[i].collider.isGrounded)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

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
        public VehicleDamage Damage => damage;
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

        public void SetDamage(VehicleDamage vehicleDamage)
        {
            damage = vehicleDamage;
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
            damage.Repair();
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

        /// <summary>
        /// Dents the body at the deepest contact. The dent grows with the closing speed along the contact normal,
        /// scaled by how hard the other thing is: a wall fully, a car by its mass against ours, a street prop by its
        /// own hardness, a prop already lying on the road hardly at all.
        /// </summary>
        private void Dent(Collision collision)
        {
            var count = collision.contactCount;
            if (count == 0)
            {
                return;
            }

            // Across the contact patch take its middle, but up and down its lowest point: box-on-box contacts sit at
            // the patch corners, and the middle of a tall patch (a pole, a wall) is the windscreen, not the bumper
            // that takes the hit.
            var point = Vector3.zero;
            var normal = Vector3.zero;
            var lowest = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var contact = collision.GetContact(i);
                point += contact.point;
                normal += contact.normal;
                lowest = Mathf.Min(lowest, contact.point.y);
            }

            point /= count;
            point.y = lowest + BumperRise;
            normal.Normalize();
            // Ground, landings and anything else pushing up from below.
            if (Mathf.Abs(normal.y) > GroundNormalY)
            {
                return;
            }

            var speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal)) * Hardness(collision);
            var depth = damage.Hit(point, normal, speed);
            if (depth > 0f)
            {
                Crashed?.Invoke(this, point, depth);
            }
        }

        private float Hardness(Collision collision)
        {
            if (collision.collider.TryGetComponent<KnockableProp>(out var prop))
            {
                return prop.WasLyingBefore(Time.fixedTime) ? DebrisHardness : prop.DentHardness;
            }

            // Debris that is no prop is a ragdoll: soft.
            if (collision.gameObject.layer == PhysicsLayers.Debris)
            {
                return 0f;
            }

            if (collision.rigidbody != null && collision.rigidbody.TryGetComponent<Vehicle>(out var other))
            {
                return Mathf.Clamp(other.body.mass / body.mass, 0.3f, 1.5f);
            }

            return 1f;
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
            Dent(collision);
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
