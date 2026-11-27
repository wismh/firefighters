using System;
using System.Collections.Generic;
using _Project.City;
using _Project.Pedestrians;
using _Project.Player;
using UnityEngine;
using Zenject;

namespace _Project.Hazards
{
    /// <summary>
    /// A knocked-over hydrant opens a geyser for a few seconds: a column of water that lifts cars bobbing on top of
    /// it and throws the player and passers-by into the air.
    /// </summary>
    public sealed class HydrantGeysers : MonoBehaviour
    {
        [SerializeField] private float seconds = 7f;
        [SerializeField] private float height = 11f;
        [SerializeField] private float radius = 1.8f;
        [Tooltip("Upward acceleration on bodies in the column, m/s², against gravity's 9.8.")]
        [SerializeField] private float lift = 26f;
        [SerializeField] private float maxRise = 13f;
        [Tooltip("Upward speed the player and passers-by are thrown with, m/s.")]
        [SerializeField] private float launchSpeed = 14f;

        private readonly List<Geyser> _geysers = new List<Geyser>(8);
        private readonly Collider[] _overlap = new Collider[32];
        private readonly HashSet<Rigidbody> _pushed = new HashSet<Rigidbody>();

        private List<KnockableProp> _props;
        private FirstPersonController _walker;
        private PedestrianSystem _pedestrians;

        private struct Geyser
        {
            public Vector3 Base;
            public float EndTime;
        }

        /// <summary>Raised when a hydrant starts spouting, at its base.</summary>
        public event Action<Vector3> Opened;

        [Inject]
        public void Construct(List<KnockableProp> props, FirstPersonController walker, PedestrianSystem pedestrians)
        {
            _props = props;
            _walker = walker;
            _pedestrians = pedestrians;
        }

        private void Start()
        {
            foreach (var prop in _props)
            {
                prop.Knocked += OnPropKnocked;
            }
        }

        private void OnDestroy()
        {
            foreach (var prop in _props)
            {
                prop.Knocked -= OnPropKnocked;
            }
        }

        private void OnPropKnocked(KnockableProp prop)
        {
            if (prop.Kind != PropKind.Hydrant)
            {
                return;
            }

            var position = prop.transform.position;
            _geysers.Add(new Geyser { Base = position, EndTime = Time.time + seconds });
            Opened?.Invoke(position);
        }

        private void FixedUpdate()
        {
            var now = Time.time;
            for (var i = _geysers.Count - 1; i >= 0; i--)
            {
                var geyser = _geysers[i];
                if (now > geyser.EndTime)
                {
                    _geysers.RemoveAt(i);
                    continue;
                }

                LiftBodies(geyser);
                LaunchWalker(geyser);
                _pedestrians.LaunchNear(geyser.Base, radius, launchSpeed);
            }
        }

        private void LiftBodies(Geyser geyser)
        {
            var top = geyser.Base + Vector3.up * height;
            var count = Physics.OverlapCapsuleNonAlloc(geyser.Base, top, radius, _overlap, ~0,
                QueryTriggerInteraction.Ignore);
            _pushed.Clear();
            for (var c = 0; c < count; c++)
            {
                var body = _overlap[c].attachedRigidbody;
                if (body == null || body.isKinematic || !_pushed.Add(body))
                {
                    continue;
                }

                // Weaker towards the top of the column, so a car bobs on it instead of shooting off for ever.
                var rise = Mathf.Clamp01((body.worldCenterOfMass.y - geyser.Base.y) / height);
                if (body.linearVelocity.y < maxRise)
                {
                    body.AddForce(Vector3.up * (lift * (1f - rise * 0.7f)), ForceMode.Acceleration);
                }
            }
        }

        private void LaunchWalker(Geyser geyser)
        {
            if (!_walker.isActiveAndEnabled)
            {
                return;
            }

            var offset = _walker.transform.position - geyser.Base;
            var inColumn = offset.y > -0.5f && offset.y < 1f &&
                           new Vector2(offset.x, offset.z).sqrMagnitude < radius * radius;
            if (inColumn)
            {
                _walker.Launch(launchSpeed);
            }
        }
    }
}
