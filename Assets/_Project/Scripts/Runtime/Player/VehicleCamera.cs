using _Project.Vehicles;
using UnityEngine;
using Zenject;

namespace _Project.Player
{
    /// <summary>Chase camera behind the driven car. Mouse orbits; the view drifts back behind the car.</summary>
    public sealed class VehicleCamera : MonoBehaviour
    {
        [SerializeField] private float distance = 11f;
        [SerializeField] private float followSharpness = 8f;
        [SerializeField] private float recenterDelay = 1.5f;
        [SerializeField] private float recenterSpeed = 90f;
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float stickLookSpeed = 140f;
        [SerializeField] private LayerMask obstacleMask = ~0;

        private PlayerInputs _inputs;
        private Vehicle _target;
        private float _yawOffset;
        private float _pitch = 12f;
        private float _idleTime;
        private float _smoothYaw;

        [Inject]
        public void Construct(PlayerInputs inputs)
        {
            _inputs = inputs;
        }

        public void Follow(Vehicle target)
        {
            _target = target;
            _yawOffset = 0f;
            _pitch = 12f;
            _smoothYaw = target.transform.eulerAngles.y;
            enabled = true;
        }

        public void Stop()
        {
            _target = null;
            enabled = false;
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            var look = Cursor.lockState == CursorLockMode.Locked
                ? _inputs.LookDelta(mouseSensitivity, stickLookSpeed)
                : Vector2.zero;
            if (look.sqrMagnitude > 0.0001f)
            {
                _yawOffset += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, -5f, 60f);
                _idleTime = 0f;
            }
            else
            {
                _idleTime += dt;
                if (_idleTime > recenterDelay)
                {
                    _yawOffset = Mathf.MoveTowardsAngle(_yawOffset, 0f, recenterSpeed * dt);
                }
            }

            var pivot = _target.CameraTarget.position;
            _smoothYaw = Mathf.LerpAngle(_smoothYaw, _target.transform.eulerAngles.y, 1f - Mathf.Exp(-followSharpness * dt));
            var rotation = Quaternion.Euler(_pitch, _smoothYaw + _yawOffset, 0f);
            var wanted = pivot - rotation * Vector3.forward * distance;

            // Do not let walls and buildings come between the camera and the car.
            var toCamera = wanted - pivot;
            if (Physics.SphereCast(pivot, 0.3f, toCamera.normalized, out var hit, toCamera.magnitude, obstacleMask,
                    QueryTriggerInteraction.Ignore) && hit.rigidbody != _target.Body)
            {
                wanted = pivot + toCamera.normalized * Mathf.Max(1.5f, hit.distance);
            }

            transform.SetPositionAndRotation(wanted, Quaternion.LookRotation(pivot - wanted, Vector3.up));
        }
    }
}
