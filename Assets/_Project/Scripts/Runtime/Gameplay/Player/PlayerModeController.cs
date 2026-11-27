using System;
using _Project.City;
using _Project.Common;
using _Project.FireHose;
using _Project.Traffic;
using _Project.Vehicles;
using UnityEngine;
using Zenject;

namespace _Project.Player
{
    /// <summary>
    /// Switches the player between walking, driving and holding the fire hose: E next to any car (traffic or
    /// parked) takes it, E while driving slowly gets out; E at a fire truck's reel takes or returns the hose,
    /// and the left mouse button sprays water while it is in hand.
    /// </summary>
    // Before TrafficSystem.Start, so the first traffic spawns already keep away from the player's start truck.
    [DefaultExecutionOrder(-10)]
    public sealed class PlayerModeController : MonoBehaviour, IPlayerState
    {
        private const int MaxNearbyColliders = 16;
        // Only the layers cars are on, so debris and ragdolls cannot crowd a car out of the buffer.
        private const int VehicleMask = (1 << PhysicsLayers.Default) | (1 << PhysicsLayers.Traffic);
        // Rope stretch from which the hint says the hose reaches no farther.
        private const float StretchedHose = 0.02f;

        [SerializeField] private FirstPersonController walker;
        [SerializeField] private CharacterController character;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private VehicleCamera vehicleCamera;
        [Tooltip("The player starts the game driving this car.")]
        [SerializeField] private Vehicle startVehicle;
        [SerializeField] private float enterRadius = 4f;
        [Tooltip("Getting out faster than this is a jump: the car rolls on without a driver.")]
        [SerializeField] private float jumpOutSpeed = 4f;
        [Tooltip("Share of the car's velocity the player keeps when jumping out.")]
        [Range(0f, 1f)] [SerializeField] private float jumpOutVelocityKept = 0.7f;
        [SerializeField] private float jumpOutSideSpeed = 4f;
        [Tooltip("The driverless car rolls freely this long, then brakes.")]
        [SerializeField] private float coastSeconds = 1.5f;
        [SerializeField] private float onFootRadius = 0.6f;
        [SerializeField] private float drivenRadius = 2.5f;
        [SerializeField] private WaterNozzle nozzle;
        [SerializeField] private HoseRope hose;
        [SerializeField] private float reelRadius = 3f;
        [Tooltip("The hose drops out of the hands when the truck ends up this much farther than the hose reaches.")]
        [SerializeField] private float hoseBreakMargin = 3f;

        private readonly Collider[] _nearby = new Collider[MaxNearbyColliders];
        private readonly Vector3[] _exits = new Vector3[2];

        private PlayerInputs _inputs;
        private CursorLock _cursor;
        private RunawayCar _runaway;
        private TrafficSystem _traffic;
        private Vehicle _vehicle;
        private Vehicle _hintVehicle;
        private FireHoseReel _hintReel;
        private FireHoseReel _reel;
        private bool _atReel;

        public event Action<Vehicle> VehicleEntered;

        public event Action<Vehicle, float> JumpedOut;

        public event Action<Vehicle> VehicleExited;

        public bool IsDriving => _vehicle != null;

        public Vehicle Driven => _vehicle;

        public Vehicle MovingCar => _vehicle != null ? _vehicle : _runaway.Car;

        public bool IsHosing => _reel != null;

        public Transform MapFocus => IsDriving ? _vehicle.transform : walker.transform;

        public float Heading => cameraTransform.eulerAngles.y;

        public float DrivingSpeed => IsDriving ? Mathf.Abs(_vehicle.ForwardSpeed) : 0f;

        public Vehicle FireTruck { get; private set; }

        public InteractionHint Hint
        {
            get
            {
                if (IsDriving)
                {
                    return InteractionHint.Driving;
                }

                if (IsHosing)
                {
                    if (_atReel)
                    {
                        return InteractionHint.HoseAtReel;
                    }

                    return hose.Stretch > StretchedHose ? InteractionHint.HoseStretched : InteractionHint.Hosing;
                }

                if (_hintReel != null)
                {
                    return InteractionHint.TakeHose;
                }

                return _hintVehicle != null ? InteractionHint.EnterCar : InteractionHint.Walking;
            }
        }

        [Inject]
        public void Construct(PlayerInputs inputs, CursorLock cursor, RunawayCar runaway, TrafficSystem traffic)
        {
            _inputs = inputs;
            _cursor = cursor;
            _runaway = runaway;
            _traffic = traffic;
        }

        private void Start()
        {
            _runaway.Released += OnRunawayReleased;
            nozzle.Show(false);
            vehicleCamera.Stop();
            AttachCameraToHead();
            _traffic.SetFocus(walker.transform, onFootRadius, null);
            Enter(startVehicle);
        }

        private void OnDestroy()
        {
            _runaway.Released -= OnRunawayReleased;
        }

        private void Update()
        {
            if (IsDriving)
            {
                UpdateDriving();
                return;
            }

            if (IsHosing)
            {
                UpdateHosing();
                return;
            }

            var nearest = FindNearestVehicle();
            _hintReel = FindFreeReel(nearest);
            _hintVehicle = _hintReel == null ? nearest : null;
            if (!_inputs.Interact.WasPressedThisFrame())
            {
                return;
            }

            if (_hintReel != null)
            {
                TakeHose(nearest, _hintReel);
            }
            else if (_hintVehicle != null)
            {
                Enter(_hintVehicle);
            }
        }

        private void UpdateHosing()
        {
            nozzle.SetFlowing(_inputs.Attack.IsPressed() && _cursor.IsLocked);

            var distance = walker.transform.position.HorizontalDistance(_reel.AttachPoint.position);
            _atReel = distance < reelRadius;
            var lost = !_reel.isActiveAndEnabled || distance > _reel.Length + hoseBreakMargin;
            if (lost || (_atReel && _inputs.Interact.WasPressedThisFrame()))
            {
                ReturnHose();
            }
        }

        private FireHoseReel FindFreeReel(Vehicle vehicle)
        {
            if (vehicle == null || vehicle.HoseReel == null || vehicle.HoseReel.IsTaken)
            {
                return null;
            }

            var reel = vehicle.HoseReel;
            var near = walker.transform.position.HorizontalDistance(reel.AttachPoint.position) < reelRadius;
            return near ? reel : null;
        }

        private void TakeHose(Vehicle vehicle, FireHoseReel reel)
        {
            if (vehicle.Mode == VehicleMode.Traffic)
            {
                _traffic.Detach(vehicle);
            }

            _reel = reel;
            _hintReel = null;
            FireTruck = vehicle;
            reel.Take();
            nozzle.Show(true);
            hose.Attach(reel.AttachPoint, nozzle.HoseEnd, reel.Length);
            walker.SetLeash(hose);
        }

        private void ReturnHose()
        {
            nozzle.Show(false);
            hose.Detach();
            walker.ClearLeash();
            _reel.Return();
            _reel = null;
            _atReel = false;
        }

        private void UpdateDriving()
        {
            var move = _inputs.Move.ReadValue<Vector2>();
            _vehicle.Drive(move.y, move.x, _inputs.Jump.IsPressed());
            if (_inputs.Interact.WasPressedThisFrame())
            {
                Exit();
            }
        }

        private void Enter(Vehicle vehicle)
        {
            if (vehicle.Mode == VehicleMode.Traffic)
            {
                _traffic.Detach(vehicle);
            }

            if (_runaway.Car != null)
            {
                // Back in the runaway it is simply driven again; any other car leaves it to stop by itself.
                _runaway.Release(vehicle != _runaway.Car);
            }

            _vehicle = vehicle;
            if (vehicle.HoseReel != null)
            {
                FireTruck = vehicle;
            }

            vehicle.SetMode(VehicleMode.Driven);
            _traffic.SetFocus(vehicle.transform, drivenRadius, vehicle);
            walker.gameObject.SetActive(false);
            cameraTransform.SetParent(null, true);
            vehicleCamera.Follow(vehicle);
            VehicleEntered?.Invoke(vehicle);
        }

        private void Exit()
        {
            var vehicle = _vehicle;
            var velocity = vehicle.Velocity;
            var jumping = velocity.magnitude > jumpOutSpeed;
            var exit = FindExitPosition(vehicle);
            _vehicle = null;
            if (jumping)
            {
                _runaway.Start(vehicle, coastSeconds);
            }
            else
            {
                vehicle.SetMode(VehicleMode.Parked);
            }

            vehicleCamera.Stop();

            walker.gameObject.SetActive(true);
            walker.Teleport(exit, vehicle.transform.eulerAngles.y);
            AttachCameraToHead();
            _traffic.SetFocus(walker.transform, onFootRadius, null);
            if (!jumping)
            {
                VehicleExited?.Invoke(vehicle);
                return;
            }

            var outward = Vector3.ProjectOnPlane(exit - vehicle.transform.position, Vector3.up).normalized;
            walker.Throw(velocity * jumpOutVelocityKept + outward * jumpOutSideSpeed);
            JumpedOut?.Invoke(vehicle, velocity.magnitude);
        }

        private void OnRunawayReleased(Vehicle vehicle, bool park)
        {
            if (park)
            {
                vehicle.SetMode(VehicleMode.Parked);
            }

            VehicleExited?.Invoke(vehicle);
        }

        private Vector3 FindExitPosition(Vehicle vehicle)
        {
            var height = character.height;
            var radius = character.radius;
            var left = vehicle.ExitPoint.position;
            var right = vehicle.transform.position + (vehicle.transform.position - left);
            right.y = left.y;
            _exits[0] = left;
            _exits[1] = right;
            foreach (var candidate in _exits)
            {
                var bottom = candidate + Vector3.up * (radius + 0.05f);
                var top = candidate + Vector3.up * (height - radius);
                if (!Physics.CheckCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
                {
                    return candidate;
                }
            }

            // Both doors are blocked: climb out onto the roof.
            return vehicle.CameraTarget.position + Vector3.up * 0.5f;
        }

        private Vehicle FindNearestVehicle()
        {
            var count = Physics.OverlapSphereNonAlloc(walker.transform.position, enterRadius, _nearby,
                VehicleMask, QueryTriggerInteraction.Ignore);
            Vehicle best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var body = _nearby[i].attachedRigidbody;
                if (body == null || !body.TryGetComponent<Vehicle>(out var vehicle))
                {
                    continue;
                }

                var distance = (vehicle.transform.position - walker.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = vehicle;
                }
            }

            return best;
        }

        private void AttachCameraToHead()
        {
            cameraTransform.SetParent(walker.CameraPivot, false);
            cameraTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }
    }
}
