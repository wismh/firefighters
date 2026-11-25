using _Project.City;
using _Project.FireHose;
using _Project.Traffic;
using _Project.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;
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
    public sealed class PlayerModeController : MonoBehaviour
    {
        private const int MaxNearbyColliders = 16;
        private const float CarStoppedSpeed = 0.5f;
        private const float RunawayBrakeSpeed = 1.5f;
        // A car that will not settle (on its roof, in the air) is let go after this long.
        private const float MaxRunawaySeconds = 12f;
        // Only the layers cars are on, so debris and ragdolls cannot crowd a car out of the buffer.
        private const int VehicleMask = (1 << PhysicsLayers.Default) | (1 << PhysicsLayers.Traffic);

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

        private PlayerInputs _inputs;
        private TrafficSystem _traffic;
        private FireService _fires;
        private string _fireText;
        private Vehicle _vehicle;
        private Vehicle _runaway;
        private float _runawayTime;
        private Vehicle _hintVehicle;
        private FireHoseReel _hintReel;
        private FireHoseReel _reel;
        private bool _atReel;

        /// <summary>Raised when the player gets into a car.</summary>
        public event System.Action<Vehicle> VehicleEntered;

        /// <summary>Raised when the player jumps out of a moving car: the car and its speed, m/s.</summary>
        public event System.Action<Vehicle, float> JumpedOut;

        /// <summary>Raised when the player leaves a car; after a jump, once the car has come to a stop.</summary>
        public event System.Action<Vehicle> VehicleExited;

        public bool IsDriving => _vehicle != null;

        /// <summary>The car being driven, or null on foot.</summary>
        public Vehicle Driven => _vehicle;

        /// <summary>The car being driven, or the one that still rolls after the player jumped out of it.</summary>
        public Vehicle MovingCar => _vehicle != null ? _vehicle : _runaway;
        public bool IsHosing => _reel != null;

        /// <summary>What the minimap centres on: the car while driving, the body on foot.</summary>
        public Transform MapFocus => IsDriving ? _vehicle.transform : walker.transform;

        /// <summary>Where the player looks, as a yaw in degrees; the camera's in both modes.</summary>
        public float Heading => cameraTransform.eulerAngles.y;

        /// <summary>Speed of the driven car, m/s; 0 on foot.</summary>
        public float DrivingSpeed => IsDriving ? Mathf.Abs(_vehicle.Controller.ForwardSpeed) : 0f;

        /// <summary>The last fire truck the player drove or took the hose from; the minimap shows it on foot.</summary>
        public Vehicle FireTruck { get; private set; }

        [Inject]
        public void Construct(PlayerInputs inputs, TrafficSystem traffic, FireService fires)
        {
            _inputs = inputs;
            _traffic = traffic;
            _fires = fires;
        }

        private void Start()
        {
            nozzle.Show(false);
            vehicleCamera.Stop();
            AttachCameraToHead();
            _traffic.SetFocus(walker.transform, onFootRadius, null);
            LockCursor(true);
            Enter(startVehicle);
            _fires.FireOut += UpdateFireText;
            UpdateFireText(_fires.Total);
        }

        private void OnDestroy()
        {
            _fires.FireOut -= UpdateFireText;
        }

        private void UpdateFireText(int burning)
        {
            _fireText = burning > 0 ? $"Пожежі: {burning} / {_fires.Total}" : "Усі пожежі згашено!";
        }

        private void Update()
        {
            UpdateCursor();
            UpdateRunaway();
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
            var flowing = _inputs.Attack.IsPressed() && Cursor.lockState == CursorLockMode.Locked;
            nozzle.SetFlowing(flowing);

            var distance = HorizontalDistance(walker.transform.position, _reel.AttachPoint.position);
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
            var near = HorizontalDistance(walker.transform.position, reel.AttachPoint.position) < reelRadius;
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

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void UpdateDriving()
        {
            var move = _inputs.Move.ReadValue<Vector2>();
            _vehicle.Controller.SetInput(move.y, move.x, _inputs.Jump.IsPressed());
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

            if (_runaway != null)
            {
                // Back in the runaway it is simply driven again; any other car leaves it to stop by itself.
                ReleaseRunaway(vehicle != _runaway);
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
            var velocity = vehicle.Body.linearVelocity;
            var jumping = velocity.magnitude > jumpOutSpeed;
            var exit = FindExitPosition(vehicle);
            _vehicle = null;
            if (jumping)
            {
                // Still "driven" while it rolls, so it knocks lamps, cars and people over like one.
                _runaway = vehicle;
                _runawayTime = 0f;
                vehicle.Controller.SetInput(0f, 0f, false);
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

        private void UpdateRunaway()
        {
            if (_runaway == null)
            {
                return;
            }

            _runawayTime += Time.deltaTime;
            var braking = _runawayTime > coastSeconds;
            var forward = _runaway.Controller.ForwardSpeed;
            // Throttle against the motion is the foot brake.
            var pedal = braking && Mathf.Abs(forward) > RunawayBrakeSpeed ? -Mathf.Sign(forward) : 0f;
            _runaway.Controller.SetInput(pedal, 0f, braking);
            // The traffic may have taken a far car back into its pool meanwhile.
            var gone = !_runaway.isActiveAndEnabled || _runaway.Mode != VehicleMode.Driven;
            var stopped = _runawayTime > 0.5f && _runaway.Speed < CarStoppedSpeed;
            if (gone || stopped || _runawayTime > MaxRunawaySeconds)
            {
                ReleaseRunaway(!gone);
            }
        }

        private void ReleaseRunaway(bool park)
        {
            var vehicle = _runaway;
            _runaway = null;
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
            foreach (var candidate in new[] { left, right })
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

        private static void UpdateCursor()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                LockCursor(false);
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor(true);
            }
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnGUI()
        {
            var style = GUI.skin.label;
            style.fontSize = 18;
            GUI.Label(new Rect(20f, Screen.height - 40f, 900f, 30f), HintText(), style);
            GUI.Label(new Rect(20f, 16f, 400f, 30f), _fireText, style);
            if (IsHosing)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 5f, Screen.height * 0.5f - 13f, 20f, 26f), "+", style);
            }
        }

        private string HintText()
        {
            if (IsDriving)
            {
                return "WASD — кермо, Space — ручник, E — вийти (на ходу — вистрибнути)";
            }

            if (IsHosing)
            {
                if (_atReel)
                {
                    return "ЛКМ — вода, E — повернути шланг";
                }

                return hose.Stretch > 0.02f
                    ? "Шланг натягнуто — далі не дістає"
                    : "ЛКМ — вода; щоб сісти в машину, поверни шланг на котушку";
            }

            if (_hintReel != null)
            {
                return "E — взяти шланг";
            }

            return _hintVehicle != null ? "E — сісти в машину" : "WASD — ходити, Shift — біг, Space — стрибок";
        }
    }
}
