using UnityEngine;

namespace _Project.Vehicles
{
    /// <summary>WheelCollider car physics for the player-driven and parked modes.</summary>
    public sealed class VehicleController : MonoBehaviour
    {
        // The meshes face -Z inside a model rotated by 180 degrees.
        private static readonly Quaternion VisualFix = Quaternion.Euler(0f, 180f, 0f);

        [SerializeField] private Rigidbody body;
        [SerializeField] private VehicleWheel[] wheels;
        [SerializeField] private float motorTorque = 1800f;
        [SerializeField] private float brakeTorque = 4000f;
        [SerializeField] private float handbrakeTorque = 6000f;
        [SerializeField] private float maxSteerAngle = 32f;
        [SerializeField] private float highSpeedSteerAngle = 10f;
        [SerializeField] private float maxSpeed = 28f;
        [SerializeField] private float reverseSpeed = 8f;
        [SerializeField] private float downforce = 40f;

        private float _throttle;
        private float _steer;
        private bool _handbrake;
        private bool _hasDriver;

        public float ForwardSpeed => Vector3.Dot(body.linearVelocity, transform.forward);
        public VehicleWheel[] Wheels => wheels;

        /// <summary>Editor-time wiring from the vehicle prefab builder. Torques scale with the vehicle mass.</summary>
        public void Setup(Rigidbody rigidbody, VehicleWheel[] vehicleWheels, float massScale)
        {
            body = rigidbody;
            wheels = vehicleWheels;
            motorTorque = 1800f * massScale;
            brakeTorque = 4000f * massScale;
            handbrakeTorque = 6000f * massScale;
        }

        public void SetDriver(bool hasDriver)
        {
            _hasDriver = hasDriver;
            SetInput(0f, 0f, !hasDriver);
        }

        public void SetInput(float throttle, float steer, bool handbrake)
        {
            _throttle = Mathf.Clamp(throttle, -1f, 1f);
            _steer = Mathf.Clamp(steer, -1f, 1f);
            _handbrake = handbrake;
        }

        private void FixedUpdate()
        {
            if (body.isKinematic)
            {
                return;
            }

            var speed = ForwardSpeed;
            var speedFactor = Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed);
            var steerAngle = _steer * Mathf.Lerp(maxSteerAngle, highSpeedSteerAngle, speedFactor);

            // Pressing against the motion brakes first, then reverses.
            var braking = _hasDriver && Mathf.Abs(speed) > 1f && Mathf.Sign(_throttle) != Mathf.Sign(speed) &&
                          Mathf.Abs(_throttle) > 0.1f;
            var limit = _throttle >= 0f ? maxSpeed : reverseSpeed;
            var torque = braking || Mathf.Abs(speed) > limit ? 0f : _throttle * motorTorque;
            var brake = braking ? brakeTorque : 0f;
            if (!_hasDriver)
            {
                brake = brakeTorque * 0.3f;
            }

            for (var i = 0; i < wheels.Length; i++)
            {
                var wheel = wheels[i];
                if (wheel.steer)
                {
                    wheel.collider.steerAngle = steerAngle;
                }

                wheel.collider.motorTorque = wheel.drive ? torque : 0f;
                var handbrake = _handbrake && wheel.handbrake ? handbrakeTorque : 0f;
                wheel.collider.brakeTorque = Mathf.Max(brake, handbrake);
            }

            body.AddForce(-transform.up * (downforce * body.linearVelocity.magnitude));
        }

        private void LateUpdate()
        {
            if (body.isKinematic)
            {
                return;
            }

            for (var i = 0; i < wheels.Length; i++)
            {
                wheels[i].collider.GetWorldPose(out var position, out var rotation);
                wheels[i].visual.SetPositionAndRotation(position, rotation * VisualFix);
            }
        }
    }
}
