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

        [SerializeField] private FirstPersonController walker;
        [SerializeField] private CharacterController character;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private VehicleCamera vehicleCamera;
        [Tooltip("The player starts the game driving this car.")]
        [SerializeField] private Vehicle startVehicle;
        [SerializeField] private float enterRadius = 4f;
        [SerializeField] private float maxExitSpeed = 4f;
        [SerializeField] private float onFootRadius = 0.6f;
        [SerializeField] private float drivenRadius = 2.5f;
        [SerializeField] private WaterNozzle nozzle;
        [SerializeField] private HoseRope hose;
        [SerializeField] private float reelRadius = 3f;
        [Tooltip("The hose drops out of the hands when the truck ends up this much farther than the hose reaches.")]
        [SerializeField] private float hoseBreakMargin = 3f;
        [Tooltip("Part of the hose length the player can walk away; the rest is slack that sags to the ground.")]
        [SerializeField] private float leashFraction = 0.92f;

        private readonly Collider[] _nearby = new Collider[MaxNearbyColliders];

        private PlayerInputs _inputs;
        private TrafficSystem _traffic;
        private FireService _fires;
        private string _fireText;
        private Vehicle _vehicle;
        private Vehicle _hintVehicle;
        private FireHoseReel _hintReel;
        private FireHoseReel _reel;
        private bool _atReel;

        public bool IsDriving => _vehicle != null;
        public bool IsHosing => _reel != null;

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
            reel.Take();
            nozzle.Show(true);
            hose.Attach(reel.AttachPoint, nozzle.HoseEnd, reel.Length);
            walker.SetLeash(reel.AttachPoint, reel.Length * leashFraction);
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
            var slowEnough = Mathf.Abs(_vehicle.Controller.ForwardSpeed) < maxExitSpeed;
            if (slowEnough && _inputs.Interact.WasPressedThisFrame())
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

            _vehicle = vehicle;
            vehicle.SetMode(VehicleMode.Driven);
            _traffic.SetFocus(vehicle.transform, drivenRadius, vehicle);
            walker.gameObject.SetActive(false);
            cameraTransform.SetParent(null, true);
            vehicleCamera.Follow(vehicle);
        }

        private void Exit()
        {
            var vehicle = _vehicle;
            _vehicle = null;
            vehicle.SetMode(VehicleMode.Parked);
            vehicleCamera.Stop();

            walker.gameObject.SetActive(true);
            walker.Teleport(FindExitPosition(vehicle), vehicle.transform.eulerAngles.y);
            AttachCameraToHead();
            _traffic.SetFocus(walker.transform, onFootRadius, null);
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
                ~0, QueryTriggerInteraction.Ignore);
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
                return "WASD — кермо, Space — ручник, E — вийти (на малій швидкості)";
            }

            if (IsHosing)
            {
                return _atReel
                    ? "ЛКМ — вода, E — повернути шланг"
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
