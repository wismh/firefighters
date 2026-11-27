using MyToolkit.ParticleFx;
using UnityEngine;
using Zenject;

namespace _Project.FireHose
{
    /// <summary>
    /// The nozzle in the player's hands. While flowing it plays the stream particles and traces the same
    /// ballistic arc with raycasts: that gives the point, normal and collider the water lands on (splashes now,
    /// putting out fires later) without listening to hundreds of particle collisions a frame.
    /// </summary>
    public sealed class WaterNozzle : MonoBehaviour
    {

        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform hoseEnd;
        [SerializeField] private ParticleSystem stream;
        [SerializeField] private ParticleSystem muzzleFlash;
        [SerializeField] private float speed = 21.5f;
        [SerializeField] private float gravityModifier = 1f;

        [Header("Push")]
        [Tooltip("Push of the water stream on loose bodies, newtons.")]
        [SerializeField] private float pushForce = 5000f;
        [Tooltip("Cap on the push as an acceleration, so light bodies are not shot away.")]
        [SerializeField] private float maxPushAcceleration = 10f;
        [SerializeField] private float splashInterval = 0.07f;
        [SerializeField] private float splashJitter = 0.35f;

        private IParticleFxSpawnService<WaterFxId> _fx;
        private RaycastHit _hit;
        private float _hitTime;
        private float _flowTime;
        private float _splashTimer;

        public Transform HoseEnd => hoseEnd;
        public bool IsFlowing { get; private set; }

        /// <summary>True while flowing and the arc ends on a collider (see <see cref="Hit"/>).</summary>
        public bool HasHit { get; private set; }

        /// <summary>True once the water has had time to fly to <see cref="Hit"/>.</summary>
        public bool IsWaterArriving => HasHit && _flowTime >= _hitTime;

        public RaycastHit Hit => _hit;
        public float Speed => speed;
        public float GravityModifier => gravityModifier;

        [Inject]
        public void Construct(IParticleFxSpawnService<WaterFxId> fx)
        {
            _fx = fx;
        }

        public void Show(bool visible)
        {
            if (!visible)
            {
                SetFlowing(false);
            }

            gameObject.SetActive(visible);
        }

        public void SetFlowing(bool flowing)
        {
            if (flowing == IsFlowing)
            {
                return;
            }

            IsFlowing = flowing;
            _flowTime = 0f;
            _splashTimer = 0f;
            if (flowing)
            {
                stream.Play(true);
                muzzleFlash.Play(true);
            }
            else
            {
                stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                HasHit = false;
            }
        }

        private void Update()
        {
            if (!IsFlowing)
            {
                return;
            }

            var dt = Time.deltaTime;
            _flowTime += dt;
            TraceArc();
            if (!IsWaterArriving)
            {
                return;
            }

            _splashTimer -= dt;
            if (_splashTimer > 0f)
            {
                return;
            }

            _splashTimer = splashInterval;
            var onGround = _hit.normal.y > 0.6f;
            var jitter = Vector3.ProjectOnPlane(Random.insideUnitSphere * splashJitter, _hit.normal);
            var position = _hit.point + _hit.normal * 0.05f + jitter;
            _fx.SpawnParticle(onGround ? WaterFxId.GroundSplash : WaterFxId.WallSplash, position);
        }

        // The stream shoves whatever loose body it lands on: parked cars, fallen lamps, ragdolls.
        private void FixedUpdate()
        {
            if (!IsFlowing || !IsWaterArriving)
            {
                return;
            }

            var body = _hit.rigidbody;
            if (body == null || body.isKinematic)
            {
                return;
            }

            var direction = (_hit.point - transform.position).normalized;
            var force = Mathf.Min(pushForce, body.mass * maxPushAcceleration);
            body.AddForceAtPosition(direction * force, _hit.point, ForceMode.Force);
        }

        private void TraceArc()
        {
            HasHit = WaterArc.Trace(muzzle.position, muzzle.forward * speed, Physics.gravity * gravityModifier,
                out _hit, out _hitTime);
        }
    }
}
