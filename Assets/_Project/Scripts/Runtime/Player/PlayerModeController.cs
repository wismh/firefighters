using _Project.Traffic;
using _Project.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace _Project.Player
{
    /// <summary>
    /// Switches the player between walking and driving: E next to any car (traffic or parked) takes it,
    /// E while driving slowly gets out.
    /// </summary>
    public sealed class PlayerModeController : MonoBehaviour
    {
        private const int MaxNearbyColliders = 16;

        [SerializeField] private FirstPersonController walker;
        [SerializeField] private CharacterController character;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private VehicleCamera vehicleCamera;
        [SerializeField] private float enterRadius = 4f;
        [SerializeField] private float maxExitSpeed = 4f;
        [SerializeField] private float onFootRadius = 0.6f;
        [SerializeField] private float drivenRadius = 2.5f;

        private readonly Collider[] _nearby = new Collider[MaxNearbyColliders];

        private PlayerInputs _inputs;
        private TrafficSystem _traffic;
        private Vehicle _vehicle;
        private Vehicle _hintVehicle;

        public bool IsDriving => _vehicle != null;

        [Inject]
        public void Construct(PlayerInputs inputs, TrafficSystem traffic)
        {
            _inputs = inputs;
            _traffic = traffic;
        }

        private void Start()
        {
            vehicleCamera.Stop();
            AttachCameraToHead();
            _traffic.SetFocus(walker.transform, onFootRadius, null);
            LockCursor(true);
        }

        private void Update()
        {
            UpdateCursor();
            if (IsDriving)
            {
                UpdateDriving();
                return;
            }

            _hintVehicle = FindNearestVehicle();
            if (_hintVehicle != null && _inputs.Interact.WasPressedThisFrame())
            {
                Enter(_hintVehicle);
            }
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
            var text = IsDriving
                ? "WASD — кермо, Space — ручник, E — вийти (на малій швидкості)"
                : _hintVehicle != null
                    ? "E — сісти в машину"
                    : "WASD — ходити, Shift — біг, Space — стрибок";
            var style = GUI.skin.label;
            style.fontSize = 18;
            GUI.Label(new Rect(20f, Screen.height - 40f, 900f, 30f), text, style);
        }
    }
}
