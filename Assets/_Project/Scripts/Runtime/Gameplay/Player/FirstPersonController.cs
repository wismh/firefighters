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
        [Tooltip("Deceleration while sliding after a jump from a car.")]
        [SerializeField] private float slideDeceleration = 11f;
        [SerializeField] private float tumbleAngle = 38f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float stickLookSpeed = 160f;

        [Header("Head bob")]
        [Tooltip("Metres walked per step; the head dips once per step and sways side to side once per two.")]
        [SerializeField] private float bobStride = 1.7f;
        [SerializeField] private float bobHeight = 0.045f;
        [SerializeField] private float bobSway = 0.03f;
        [SerializeField] private float bobRoll = 0.5f;
        [Tooltip("How fast the bob fades in when walking starts and out when it stops, per second.")]
        [SerializeField] private float bobBlend = 6f;

        private PlayerInputs _inputs;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _tumble;
        private bool _sliding;
        private float _pitch;
        private HoseRope _leash;
        private bool _leashed;
        private Vector3 _pivotRest;
        private float _bobPhase;
        private float _bobWeight;

        public Transform CameraPivot => cameraPivot;
        public bool IsGrounded => character.isGrounded;
        public float HorizontalSpeed => _horizontalVelocity.magnitude;
        public float VerticalSpeed => _verticalVelocity;

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

        /// <summary>Throws the body at a velocity; it slides to a halt while the view tumbles.</summary>
        public void Throw(Vector3 velocity)
        {
            _horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            _sliding = true;
            _tumble = Mathf.Clamp01(_horizontalVelocity.magnitude / 15f);
            Launch(Mathf.Max(velocity.y, 2.5f));
        }

        /// <summary>Throws the body up at <paramref name="upSpeed"/> m/s unless it already rises faster.</summary>
        public void Launch(float upSpeed)
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity, upSpeed);
            // A grounded controller resets the vertical speed next frame; lift it off the ground first.
            character.Move(Vector3.up * 0.05f);
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

        private void Awake()
        {
            _pivotRest = cameraPivot.localPosition;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                var look = _inputs.LookDelta(mouseSensitivity, stickLookSpeed);
                transform.Rotate(0f, look.x, 0f, Space.Self);
                _pitch = Mathf.Clamp(_pitch - look.y, -85f, 85f);
            }

            // The tumble dies out as a few rolls from side to side.
            _tumble = Mathf.MoveTowards(_tumble, 0f, dt * 0.8f);
            var roll = Mathf.Sin(_tumble * 14f) * _tumble * tumbleAngle;
            var bobRollAngle = ApplyHeadBob(dt);
            cameraPivot.localRotation = Quaternion.Euler(_pitch + Mathf.Abs(roll) * 0.4f, 0f, roll + bobRollAngle);

            var move = _inputs.Move.ReadValue<Vector2>();
            var speed = _inputs.Sprint.IsPressed() ? sprintSpeed : walkSpeed;
            var target = (transform.right * move.x + transform.forward * move.y) * speed;
            _sliding &= _horizontalVelocity.magnitude > walkSpeed * 0.4f;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, _sliding ? Vector3.zero : target,
                (_sliding ? slideDeceleration : acceleration) * dt);

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
        /// Moves the camera pivot with the steps: a dip per step and a sway per stride, grown with the walking speed and
        /// faded out in the air and while sliding. Returns the roll that goes with the sway.
        /// </summary>
        private float ApplyHeadBob(float dt)
        {
            var speed = _horizontalVelocity.magnitude;
            var walking = character.isGrounded && !_sliding;
            var targetWeight = walking ? Mathf.Clamp(speed / walkSpeed, 0f, 1.4f) : 0f;
            _bobWeight = Mathf.MoveTowards(_bobWeight, targetWeight, bobBlend * dt);
            if (walking)
            {
                _bobPhase = Mathf.Repeat(_bobPhase + speed * dt * Mathf.PI / bobStride, Mathf.PI * 2f);
            }

            var sway = Mathf.Sin(_bobPhase);
            // The head is highest between steps and drops as each foot lands.
            var dip = Mathf.Abs(Mathf.Cos(_bobPhase)) - 0.5f;
            var offset = new Vector3(sway * bobSway, dip * bobHeight * 2f, 0f);
            cameraPivot.localPosition = _pivotRest + offset * _bobWeight;
            return -sway * bobRoll * _bobWeight;
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
