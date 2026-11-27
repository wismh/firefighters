using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Project.Pedestrians
{
    /// <summary>
    /// One passer-by. Walks the sidewalk graph, runs from danger, stops to yell, clap or dance, falls over when
    /// hosed and turns into a ragdoll when hit. Nobody dies: they lie a while, then get up and run.
    /// </summary>
    public sealed class Pedestrian : MonoBehaviour
    {
        // At scale 1; the stride, and so the speed, grows with the body.
        private const float WalkSpeed = 1.2f;
        private const float RunSpeed = 4f;
        // Between the lamps and bins on one side and the shop fronts on the other there is little room.
        private const float LaneSpread = 0.12f;
        private const float TurnSharpness = 9f;
        private const float StepHeightSpeed = 1.2f;
        private const float Fade = 0.2f;
        private const float FallSeconds = 3.2f;
        private const float GetUpSeconds = 2.6f;
        private const float GetUpBlend = 0.5f;
        private const float GetUpFleeSeconds = 5f;
        private const float RestSpeed = 0.6f;
        private const float RestHeight = 1.5f;
        private const float MinRagdollSeconds = 1.6f;
        private const float MaxRagdollSeconds = 9f;
        // No ragdoll part lies farther than this from the hips, in metres at scale 1, unless the joints gave way.
        private const float TornApartDistance = 2.5f;
        private const float CrossChance = 0.35f;
        private const float DanceChance = 0.02f;
        // A crossing nobody can use (a wreck on it) is given up after this long.
        private const float MaxWaitSeconds = 25f;

        private static readonly int WalkId = Animator.StringToHash("Walk");
        private static readonly int IdleId = Animator.StringToHash("Idle");
        private static readonly int RunId = Animator.StringToHash("Run");
        private static readonly int FallId = Animator.StringToHash("FallBack");
        private static readonly int GetUpId = Animator.StringToHash("GetUp");

        private static readonly int[] EmoteIds =
        {
            Animator.StringToHash(nameof(PedestrianEmote.Yell)), Animator.StringToHash(nameof(PedestrianEmote.Clap)),
            Animator.StringToHash(nameof(PedestrianEmote.Dance)),
        };

        [SerializeField] private Animator animator;
        [SerializeField] private CapsuleCollider capsule;
        [SerializeField] private Transform hips;
        [SerializeField] private Transform head;
        [SerializeField] private Rigidbody[] ragdollBodies;
        [SerializeField] private Collider[] ragdollColliders;
        [SerializeField] private Transform[] bones;

        private Vector3 _hipsStart;
        private Quaternion[] _boneStart;
        private Vector3[] _boneRest;
        private SidewalkGraph _graph;
        private SidewalkLink _link;
        private int _from;
        private Vector3 _lane;
        private Vector3 _faceTarget;
        private float _timer;
        private float _stateTime;

        public PedestrianState State { get; private set; }

        /// <summary>Set while a ghost is out of the body: it does not get up before the ghost is back.</summary>
        public bool IsHaunted { get; set; }

        /// <summary>Time from which the pedestrian may stop to gawk again; kept by the system.</summary>
        public float NextGawkTime { get; set; }

        /// <summary>Where the body is, also when the ragdoll has flown away from the root.</summary>
        public Vector3 Position => State == PedestrianState.Ragdoll ? hips.position : transform.position;

        public Vector3 HeadPosition => head.position;
        public bool IsDown => State >= PedestrianState.Fall;
        public bool IsCalm => State == PedestrianState.Walk || State == PedestrianState.Wait;

        public void Setup(Animator bodyAnimator, CapsuleCollider body, Transform hipsBone, Transform headBone,
            Rigidbody[] bodies, Collider[] colliders, Transform[] allBones)
        {
            animator = bodyAnimator;
            capsule = body;
            hips = hipsBone;
            head = headBone;
            ragdollBodies = bodies;
            ragdollColliders = colliders;
            bones = allBones;
        }

        /// <summary>Puts a pooled pedestrian on the way from a graph point along one of its links.</summary>
        public void Spawn(SidewalkGraph graph, int point, SidewalkLink link, float along)
        {
            _graph = graph;
            _from = point;
            _link = link;
            _lane = new Vector3(Random.Range(-LaneSpread, LaneSpread), 0f, Random.Range(-LaneSpread, LaneSpread));
            IsHaunted = false;
            SetRagdoll(false);
            animator.Rebind();
            var start = Vector3.Lerp(graph.Points[point], graph.Points[link.To], along) + _lane;
            var direction = graph.Points[link.To] - graph.Points[point];
            transform.SetPositionAndRotation(start, Quaternion.LookRotation(direction));
            Enter(PedestrianState.Walk);
            animator.Play(WalkId, 0, Random.value);
        }

        /// <summary>Runs away from <paramref name="threat"/> for a while. Ignored by someone who is down.</summary>
        public void Flee(Vector3 threat, float seconds)
        {
            if (IsDown)
            {
                return;
            }

            _timer = seconds;
            if (State == PedestrianState.Flee)
            {
                return;
            }

            var ahead = _graph.Points[_link.To] - transform.position;
            if (Vector3.Dot(ahead, threat - transform.position) > 0f)
            {
                Reverse();
            }

            Enter(PedestrianState.Flee, RunId, Fade);
        }

        /// <summary>Stops and emotes towards <paramref name="target"/>. Only someone calm, off the road, stops.</summary>
        public bool Emote(PedestrianEmote emote, Vector3 target, float seconds)
        {
            if (!IsCalm || _graph.IsOnRoad(transform.position))
            {
                return false;
            }

            _faceTarget = target;
            _timer = seconds;
            Enter(PedestrianState.Emote, EmoteIds[(int)emote], Fade);
            return true;
        }

        /// <summary>The hose stream hits the body: it falls on its back, away from the water.</summary>
        public bool KnockBack(Vector3 source)
        {
            if (IsDown)
            {
                return false;
            }

            var toSource = Flat(source - transform.position);
            if (toSource.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(toSource);
            }

            Enter(PedestrianState.Fall, FallId, 0.1f);
            return true;
        }

        /// <summary>Sends the body flying as a ragdoll.</summary>
        public bool Launch(Vector3 velocity, Vector3 spin)
        {
            if (State == PedestrianState.Ragdoll)
            {
                return false;
            }

            Enter(PedestrianState.Ragdoll);
            SetRagdoll(true);
            for (var i = 0; i < ragdollBodies.Length; i++)
            {
                ragdollBodies[i].linearVelocity = velocity + Random.insideUnitSphere;
                ragdollBodies[i].angularVelocity = spin;
            }

            return true;
        }

        public void Tick(float dt)
        {
            _stateTime += dt;
            switch (State)
            {
                case PedestrianState.Walk:
                    Move(WalkSpeed, dt);
                    break;
                case PedestrianState.Wait:
                    TickWait(dt);
                    break;
                case PedestrianState.Flee:
                    Move(RunSpeed, dt);
                    TickTimer(dt);
                    break;
                case PedestrianState.Emote:
                    Face(_faceTarget - transform.position, dt);
                    TickTimer(dt);
                    break;
                case PedestrianState.Fall:
                    if (_stateTime > FallSeconds)
                    {
                        BeginGetUp();
                    }

                    break;
                case PedestrianState.Ragdoll:
                    TickRagdoll();
                    break;
                case PedestrianState.GetUp:
                    if (_stateTime > GetUpSeconds)
                    {
                        _timer = GetUpFleeSeconds;
                        FindWayBack();
                        Enter(PedestrianState.Flee, RunId, Fade);
                    }

                    break;
            }
        }

        private void Awake()
        {
            _boneStart = new Quaternion[bones.Length];
            _boneRest = new Vector3[bones.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                _boneRest[i] = bones[i].localPosition;
            }
        }

        // After the animator: eases from the pose the body was lying in to the get-up animation.
        private void LateUpdate()
        {
            if (State != PedestrianState.GetUp || _stateTime > GetUpBlend)
            {
                return;
            }

            var t = Mathf.SmoothStep(0f, 1f, _stateTime / GetUpBlend);
            for (var i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                if (bone == hips)
                {
                    bone.SetPositionAndRotation(Vector3.Lerp(_hipsStart, bone.position, t),
                        Quaternion.Slerp(_boneStart[i], bone.rotation, t));
                }
                else
                {
                    bone.localRotation = Quaternion.Slerp(_boneStart[i], bone.localRotation, t);
                }
            }
        }

        private void Enter(PedestrianState state)
        {
            State = state;
            _stateTime = 0f;
        }

        private void Enter(PedestrianState state, int animation, float fade)
        {
            Enter(state);
            animator.CrossFadeInFixedTime(animation, fade, 0);
        }

        private void TickTimer(float dt)
        {
            _timer -= dt;
            if (_timer <= 0f)
            {
                Enter(PedestrianState.Walk, WalkId, Fade);
            }
        }

        private void Move(float speed, float dt)
        {
            var position = transform.position;
            var toTarget = Flat(_graph.Points[_link.To] + _lane - position);
            var step = speed * transform.localScale.x * dt;
            if (toTarget.magnitude <= step)
            {
                Arrive();
                return;
            }

            position += toTarget.normalized * step;
            position.y = Mathf.MoveTowards(position.y, _graph.GroundHeight(position), StepHeightSpeed * dt);
            transform.position = position;
            Face(toTarget, dt);
        }

        private void Face(Vector3 direction, float dt)
        {
            direction = Flat(direction);
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction),
                    1f - Mathf.Exp(-TurnSharpness * dt));
            }
        }

        private void Arrive()
        {
            var fleeing = State == PedestrianState.Flee;
            PickNext(!fleeing && Random.value < CrossChance);
            if (fleeing)
            {
                return;
            }

            if (!_link.MayCross)
            {
                Enter(PedestrianState.Wait, IdleId, Fade);
            }
            else if (Random.value < DanceChance)
            {
                Emote(PedestrianEmote.Dance, transform.position + transform.forward, 6f);
            }
        }

        private void PickNext(bool wantCrossing)
        {
            var here = _link.To;
            var links = _graph.Links(here);
            var found = 0;
            var pick = default(SidewalkLink);
            for (var i = 0; i < links.Count; i++)
            {
                if (links[i].To == _from || links[i].IsCrossing != wantCrossing)
                {
                    continue;
                }

                // Reservoir pick among the candidates, without a list.
                found++;
                if (Random.Range(0, found) == 0)
                {
                    pick = links[i];
                }
            }

            if (found == 0)
            {
                if (wantCrossing)
                {
                    PickNext(false);
                    return;
                }

                pick = FindLink(here, _from);
            }

            _from = here;
            _link = pick;
        }

        private SidewalkLink FindLink(int from, int to)
        {
            var links = _graph.Links(from);
            for (var i = 0; i < links.Count; i++)
            {
                if (links[i].To == to)
                {
                    return links[i];
                }
            }

            throw new InvalidOperationException($"[pedestrians] no link {from} -> {to}");
        }

        private void Reverse()
        {
            var to = _link.To;
            _link = FindLink(to, _from);
            _from = to;
        }

        private void TickWait(float dt)
        {
            Face(_graph.Points[_link.To] - transform.position, dt);
            if (_link.MayCross)
            {
                Enter(PedestrianState.Walk, WalkId, Fade);
            }
            else if (_stateTime > MaxWaitSeconds)
            {
                // Already at the near end, so after turning back it picks a way along the sidewalk at once.
                Reverse();
                PickNext(false);
                Enter(PedestrianState.Walk, WalkId, Fade);
            }
        }

        private void TickRagdoll()
        {
            var slow = ragdollBodies[0].linearVelocity.sqrMagnitude < RestSpeed * RestSpeed;
            var resting = _stateTime > MinRagdollSeconds && slow && hips.position.y < RestHeight;
            // A ragdoll jammed into a wall can blow apart; the skinned mesh then stretches over the whole city.
            if (IsTornApart())
            {
                BeginGetUp();
                return;
            }

            if ((resting || _stateTime > MaxRagdollSeconds) && !IsHaunted)
            {
                BeginGetUp();
            }
        }

        private void BeginGetUp()
        {
            _hipsStart = hips.position;
            var lyingAlong = Flat(head.position - _hipsStart);
            for (var i = 0; i < bones.Length; i++)
            {
                _boneStart[i] = bones[i] == hips ? bones[i].rotation : bones[i].localRotation;
            }

            SetRagdoll(false);
            animator.Play(GetUpId, 0, 0f);
            animator.Update(0f);

            // Turn and move the root so the first frame of the animation lies where the body lies.
            var turn = Vector3.SignedAngle(Flat(head.position - hips.position), lyingAlong, Vector3.up);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + turn, 0f);
            var position = transform.position + Flat(_hipsStart - hips.position);
            position.y = _graph.GroundHeight(position);
            transform.position = position;
            Enter(PedestrianState.GetUp);
        }

        // A hit may have thrown the body far from its link: go to the nearest point of the graph.
        private void FindWayBack()
        {
            var points = _graph.Points;
            var best = 0;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < points.Length; i++)
            {
                var distance = (points[i] - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            _from = _graph.Links(best)[0].To;
            _link = FindLink(_from, best);
        }

        private bool IsTornApart()
        {
            var reach = TornApartDistance * transform.localScale.x;
            var hipsPosition = hips.position;
            for (var i = 0; i < ragdollBodies.Length; i++)
            {
                if ((ragdollBodies[i].position - hipsPosition).sqrMagnitude > reach * reach)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetRagdoll(bool on)
        {
            animator.enabled = !on;
            capsule.enabled = !on;
            for (var i = 0; i < ragdollBodies.Length; i++)
            {
                ragdollBodies[i].isKinematic = !on;
                // Interpolating a kinematic body writes stale physics poses over the animated bones.
                ragdollBodies[i].interpolation = on ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
            }

            for (var i = 0; i < ragdollColliders.Length; i++)
            {
                ragdollColliders[i].enabled = on;
            }

            if (on)
            {
                return;
            }

            // The humanoid animator drives only rotations (and the hips), so bone offsets the physics pulled
            // apart would stay that way: put every bone back at its length.
            for (var i = 0; i < bones.Length; i++)
            {
                bones[i].localPosition = _boneRest[i];
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
