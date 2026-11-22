using UnityEngine;

namespace _Project.FireHose
{
    /// <summary>
    /// The hose between the truck reel and the nozzle: a Verlet chain drawn with a LineRenderer. Only the part
    /// pulled off the reel is out: it starts just long enough to reach the hands and pays out as the player walks
    /// away, so the hose runs straight back to the truck, and walking back leaves the slack lying on the ground.
    /// Returning the hose winds it all back in.
    /// <para>
    /// The chain is solved follow-the-leader style, as a rope that is dragged: a pass from the hands pulls each
    /// node after the one ahead of it, so a pull reaches the slack anywhere along the hose within the frame,
    /// then a pass from the reel does the same the other way. Whatever the hose still lacks after the first pass
    /// is what the reel pays out.
    /// </para>
    /// <para>
    /// Once the reel is empty, pulling on the hose lifts it off the ground: as the hands near the full length in a
    /// straight line, the nodes are drawn toward that line, until at full length the hose runs straight from the
    /// reel to the hands. Past that is <see cref="Stretch"/>, which holds the player.
    /// </para>
    /// </summary>
    public sealed class HoseRope : MonoBehaviour
    {
        private const int Iterations = 4;
        private const float MaxStep = 1f / 30f;
        // The way the hose runs at the hands is taken over the last few nodes, so one wobbling node cannot swing it.
        private const int PullSpan = 4;

        [SerializeField] private LineRenderer line;
        [SerializeField] private int nodeCount = 40;
        [SerializeField] private float radius = 0.06f;
        [SerializeField] private float damping = 0.02f;
        [SerializeField] private float groundFriction = 0.5f;
        [Tooltip("Extra hose pulled off with the first grab, beyond the straight distance to the hands, metres.")]
        [SerializeField] private float initialSlack = 1f;
        [Tooltip("Part of a solver correction kept as motion; the rest is dropped so a dragged hose doesn't whip.")]
        [SerializeField] private float correctionInertia = 0.15f;
        [Tooltip("With the reel empty, how far short of full length (straight line) the hose starts to lift, metres.")]
        [SerializeField] private float liftRange = 1.5f;
        [Tooltip("How fast the hose snaps taut once pulled, per second.")]
        [SerializeField] private float tightenRate = 4f;

        private Vector3[] _positions;
        private Vector3[] _previous;
        private Vector3[] _solved;
        private bool[] _grounded;
        private Transform _start;
        private Transform _end;
        private float _maxLength;
        private float _segment;
        private Vector3 _lastHand;
        private float _tension;
        private bool _attached;

        /// <summary>Hose off the reel, metres.</summary>
        public float Deployed { get; private set; }

        /// <summary>How far the hands are, in a straight line from the reel, past the full length, metres.</summary>
        public float Stretch { get; private set; }

        /// <summary>Horizontal unit direction the taut hose drags the hand in (back along the hose).</summary>
        public Vector3 Pull { get; private set; }

        public void Attach(Transform start, Transform end, float maxLength)
        {
            _start = start;
            _end = end;
            _maxLength = maxLength;
            _attached = true;
            Stretch = 0f;
            _tension = 0f;
            var from = start.position;
            var to = end.position;
            SetDeployed(Vector3.Distance(from, to) + initialSlack);
            _lastHand = to;
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
            Stretch = 0f;
            line.enabled = false;
        }

        private void Awake()
        {
            _positions = new Vector3[nodeCount];
            _previous = new Vector3[nodeCount];
            _solved = new Vector3[nodeCount];
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
            System.Array.Copy(_positions, _solved, nodeCount);
            for (var k = 0; k < Iterations; k++)
            {
                Pin();
                var shortfall = DragFromHands();
                if (k == 0)
                {
                    PayOut(shortfall);
                }

                DragFromReel();
            }

            Tighten(Time.deltaTime);
            ClampToGround();
            Pin();
            KeepCorrectionsOutOfVelocity();
            MeasureTension();
            line.SetPositions(_positions);
        }

        private void SetDeployed(float length)
        {
            Deployed = Mathf.Clamp(length, 0.5f, _maxLength);
            _segment = Deployed / (nodeCount - 1);
        }

        /// <summary>
        /// The reel lets out what the hose lacks after the drag from the hands, but no more than the hands moved
        /// away along the hose this frame: the length hanging off the reel also falls short under gravity, and that
        /// alone must not unwind the reel. The new length spreads over all segments, a few millimetres each, so the
        /// hose stays straight back to the truck while it pays out.
        /// </summary>
        private void PayOut(float shortfall)
        {
            var hand = _end.position;
            var along = _positions[nodeCount - 1] - _positions[nodeCount - 1 - PullSpan];
            var away = along.sqrMagnitude > 1e-6f ? Mathf.Max(0f, Vector3.Dot(hand - _lastHand, along.normalized)) : 0f;
            _lastHand = hand;
            var payout = Mathf.Min(shortfall, away);
            if (payout > 0f && Deployed < _maxLength)
            {
                SetDeployed(Deployed + payout);
            }
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

        /// <summary>Each node, from the hands back, is pulled to within a segment of the one ahead of it.</summary>
        /// <returns>How much farther than a segment the first node still is from the reel.</returns>
        private float DragFromHands()
        {
            for (var i = nodeCount - 2; i >= 1; i--)
            {
                _positions[i] = Follow(_positions[i], _positions[i + 1]);
            }

            return Mathf.Max(0f, Vector3.Distance(_positions[0], _positions[1]) - _segment);
        }

        /// <summary>The same from the reel out; whatever is still too long ends up in the last segment.</summary>
        private void DragFromReel()
        {
            for (var i = 1; i <= nodeCount - 2; i++)
            {
                _positions[i] = Follow(_positions[i], _positions[i - 1]);
            }
        }

        private Vector3 Follow(Vector3 node, Vector3 leader)
        {
            var delta = node - leader;
            var distance = delta.magnitude;
            return distance > _segment ? leader + delta * (_segment / distance) : node;
        }

        /// <summary>
        /// Verlet reads any position change as velocity. Most of what the solver moved is taken out of it, or a
        /// dragged hose would keep flying on after the drag.
        /// </summary>
        private void KeepCorrectionsOutOfVelocity()
        {
            var drop = 1f - correctionInertia;
            for (var i = 1; i < nodeCount - 1; i++)
            {
                _previous[i] += (_positions[i] - _solved[i]) * drop;
            }
        }

        /// <summary>
        /// A hose pulled at full length is under tension and does not sag: the nodes are drawn toward the straight
        /// line between the ends, harder the nearer the hands are to the full length. The pull builds up over a
        /// moment, so a hose lying on the ground rises when it is pulled rather than jumping into the air.
        /// </summary>
        private void Tighten(float dt)
        {
            var from = _positions[0];
            var to = _positions[nodeCount - 1];
            var target = 0f;
            if (Deployed >= _maxLength)
            {
                var shortOf = Deployed - Vector3.Distance(from, to);
                target = 1f - Mathf.Clamp01(shortOf / liftRange);
                target *= target;
            }

            _tension = Mathf.MoveTowards(_tension, target, tightenRate * dt);
            if (_tension <= 0f)
            {
                return;
            }

            for (var i = 1; i < nodeCount - 1; i++)
            {
                var line = Vector3.Lerp(from, to, i / (nodeCount - 1f));
                _positions[i] = Vector3.Lerp(_positions[i], line, _tension);
            }
        }

        private void MeasureTension()
        {
            var from = _positions[0];
            var to = _positions[nodeCount - 1];
            Stretch = Deployed >= _maxLength ? Mathf.Max(0f, Vector3.Distance(from, to) - Deployed) : 0f;
            var back = from - to;
            back.y = 0f;
            Pull = back.sqrMagnitude > 1e-6f ? back.normalized : Vector3.zero;
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
