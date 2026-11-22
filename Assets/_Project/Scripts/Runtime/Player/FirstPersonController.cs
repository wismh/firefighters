using _Project.FireHose;
using UnityEngine;
using Zenject;

namespace _Project.Player
{
    /// <summary>Walking, sprinting, jumping and mouse look on a CharacterController.</summary>
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private CharacterController character;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float walkSpeed = 5.5f;
        [SerializeField] private float sprintSpeed = 9f;
        [SerializeField] private float acceleration = 40f;
        [SerializeField] private float jumpHeight = 1.4f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float stickLookSpeed = 160f;

        private PlayerInputs _inputs;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;
        private HoseRope _leash;
        private bool _leashed;

        public Transform CameraPivot => cameraPivot;

        [Inject]
        public void Construct(PlayerInputs inputs)
        {
            _inputs = inputs;
        }

        /// <summary>Holds the body with a hose: it can walk until the hose is pulled taut, wherever the hose runs.</summary>
        public void SetLeash(HoseRope hose)
        {
            _leash = hose;
            _leashed = true;
        }

        public void ClearLeash()
        {
            _leashed = false;
            _leash = null;
        }

        /// <summary>Moves the body without the CharacterController fighting the teleport.</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            character.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            character.enabled = true;
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var look = _inputs.LookDelta(mouseSensitivity, stickLookSpeed);
                transform.Rotate(0f, look.x, 0f, Space.Self);
                _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            var move = _inputs.Move.ReadValue<Vector2>();
            var speed = _inputs.Sprint.IsPressed() ? sprintSpeed : walkSpeed;
            var target = (transform.right * move.x + transform.forward * move.y) * speed;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, acceleration * dt);

            if (character.isGrounded)
            {
                _verticalVelocity = -2f;
                if (_inputs.Jump.WasPressedThisFrame())
                {
                    _verticalVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
                }
            }
            else
            {
                _verticalVelocity += gravity * dt;
            }

            var horizontal = _leashed ? ApplyLeash(dt) : _horizontalVelocity;
            character.Move((horizontal + Vector3.up * _verticalVelocity) * dt);
        }

        /// <summary>
        /// While the hose is taut, the velocity away from it is dropped; once it is stretched past a little give the
        /// body is pulled back along it. The hose straightens at the same moment, so the stop reads as the hose's.
        /// </summary>
        private Vector3 ApplyLeash(float dt)
        {
            const float taut = 0.02f;
            const float give = 0.15f;
            const float pullBackRate = 6f;
            var stretch = _leash.Stretch;
            if (stretch < taut)
            {
                return _horizontalVelocity;
            }

            var outward = -_leash.Pull;
            var along = Vector3.Dot(_horizontalVelocity, outward);
            if (along > 0f)
            {
                _horizontalVelocity -= outward * along;
            }

            var overshoot = stretch - give;
            return overshoot > 0f
                ? _horizontalVelocity - outward * Mathf.Min(overshoot * pullBackRate, overshoot / dt)
                : _horizontalVelocity;
        }
    }
}
