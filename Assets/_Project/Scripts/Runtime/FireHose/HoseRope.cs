using UnityEngine;

namespace _Project.FireHose
{
    /// <summary>
    /// The hose between the truck reel and the nozzle: a Verlet chain of fixed total length drawn with a
    /// LineRenderer. Slack falls to the ground and lies there; nodes are kept out of walls with short sweeps.
    /// Only stretching is resisted, so the hose bends and coils freely.
    /// </summary>
    public sealed class HoseRope : MonoBehaviour
    {
        private const int Iterations = 24;
        private const float MaxStep = 1f / 30f;

        [SerializeField] private LineRenderer line;
        [SerializeField] private int nodeCount = 40;
        [SerializeField] private float radius = 0.06f;
        [SerializeField] private float damping = 0.02f;
        [SerializeField] private float groundFriction = 0.5f;

        private Vector3[] _positions;
        private Vector3[] _previous;
        private bool[] _grounded;
        private Transform _start;
        private Transform _end;
        private float _segment;
        private bool _attached;

        public void Attach(Transform start, Transform end, float length)
        {
            _start = start;
            _end = end;
            _segment = length / (nodeCount - 1);
            _attached = true;
            var from = start.position;
            var to = end.position;
            for (var i = 0; i < nodeCount; i++)
            {
                _positions[i] = Vector3.Lerp(from, to, i / (nodeCount - 1f));
                _previous[i] = _positions[i];
                _grounded[i] = false;
            }

            line.enabled = true;
            line.SetPositions(_positions);
        }

        public void Detach()
        {
            _attached = false;
            line.enabled = false;
        }

        private void Awake()
        {
            _positions = new Vector3[nodeCount];
            _previous = new Vector3[nodeCount];
            _grounded = new bool[nodeCount];
            line.positionCount = nodeCount;
            line.useWorldSpace = true;
            line.enabled = false;
        }

        private void LateUpdate()
        {
            if (!_attached)
            {
                return;
            }

            Integrate(Mathf.Min(Time.deltaTime, MaxStep));
            for (var k = 0; k < Iterations; k++)
            {
                Pin();
                Constrain();
            }

            Pin();
            ClampToGround();
            line.SetPositions(_positions);
        }

        private void Integrate(float dt)
        {
            var gravity = Physics.gravity * (dt * dt);
            var keep = 1f - damping;
            for (var i = 1; i < nodeCount - 1; i++)
            {
                var position = _positions[i];
                var velocity = (position - _previous[i]) * keep;
                if (_grounded[i])
                {
                    velocity.x *= 1f - groundFriction;
                    velocity.z *= 1f - groundFriction;
                }

                _previous[i] = position;
                var next = position + velocity + gravity;
                if (Physics.Linecast(position, next, out var hit, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    next = hit.point + hit.normal * radius;
                }

                _positions[i] = next;
            }
        }

        private void Pin()
        {
            _positions[0] = _start.position;
            _positions[nodeCount - 1] = _end.position;
        }

        private void Constrain()
        {
            var last = nodeCount - 2;
            for (var i = 0; i <= last; i++)
            {
                var delta = _positions[i + 1] - _positions[i];
                var distance = delta.magnitude;
                if (distance <= _segment)
                {
                    continue;
                }

                var correction = delta * ((distance - _segment) / distance);
                if (i == 0)
                {
                    _positions[1] -= correction;
                }
                else if (i == last)
                {
                    _positions[i] += correction;
                }
                else
                {
                    _positions[i] += correction * 0.5f;
                    _positions[i + 1] -= correction * 0.5f;
                }
            }
        }

        private void ClampToGround()
        {
            const float probe = 0.5f;
            for (var i = 1; i < nodeCount - 1; i++)
            {
                var position = _positions[i];
                _grounded[i] = false;
                if (!Physics.Raycast(position + Vector3.up * probe, Vector3.down, out var hit, probe + radius * 1.5f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                _grounded[i] = true;
                var floor = hit.point.y + radius;
                if (position.y < floor)
                {
                    position.y = floor;
                    _positions[i] = position;
                }
            }
        }
    }
}
