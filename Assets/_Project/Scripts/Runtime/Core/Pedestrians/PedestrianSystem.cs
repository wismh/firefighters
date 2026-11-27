using System;
using System.Collections.Generic;
using _Project.City;
using _Project.Common;
using _Project.Fire;
using _Project.FireHose;
using _Project.Player;
using _Project.Traffic;
using _Project.Vehicles;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace _Project.Pedestrians
{
    /// <summary>
    /// Keeps passers-by around the player and tells them what happens: the player's car coming or hitting them,
    /// the hose stream, fires to gawk at and buildings saved. Traffic is told who is on the road and gives way.
    /// </summary>
    public sealed class PedestrianSystem : MonoBehaviour, IRoadObstacles
    {
        private const float MinHitSpeed = 3f;
        private const float HitMargin = 0.5f;
        private const float HitHeight = 3f;
        private const float ScareSpeed = 7f;
        private const float ScareSeconds = 1.1f;
        private const float ScareWidth = 2.2f;
        private const float FleeSeconds = 4f;
        private const float GawkDistance = 16f;
        private const float GawkChance = 0.3f;
        private const float GawkRetrySeconds = 3f;
        private const float GawkCooldown = 20f;
        private const float CheerDistance = 60f;
        private const float ClapShare = 0.6f;
        // Most random picks are out of range; enough tries a frame to refill the streets for a speeding car.
        private const int SpawnTriesPerFrame = 6;
        private const float GhostDelay = 0.8f;
        private const float OutOfWorldY = -6f;

        private struct PendingGhost
        {
            public Pedestrian Body;
            public float Time;
        }

        [SerializeField] private Pedestrian[] prefabs;
        [SerializeField] private Ghost[] ghosts;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private int count = 36;
        [SerializeField] private float spawnRadius = 65f;
        [Tooltip("Nearer than this, pedestrians appear only outside the view.")]
        [SerializeField] private float hiddenSpawnRadius = 45f;
        [SerializeField] private float minSpawnRadius = 18f;
        [SerializeField] private float despawnRadius = 85f;

        private readonly List<Pedestrian> _active = new List<Pedestrian>(64);
        private readonly List<Pedestrian> _pool = new List<Pedestrian>(64);
        private readonly List<Vector3> _onRoad = new List<Vector3>(32);
        private readonly List<PendingGhost> _pendingGhosts = new List<PendingGhost>(8);

        private SidewalkGraph _graph;
        private RoadNetwork _network;
        private IPlayerState _player;
        private FireService _fires;
        private WaterNozzle _nozzle;
        private int _created;
        private int _gawkIndex;

        /// <summary>The player's car sent someone flying.</summary>
        public event Action<Pedestrian> Hit;

        /// <summary>Someone starts running from the player's car.</summary>
        public event Action<Pedestrian> Scared;

        /// <summary>The hose knocked someone over.</summary>
        public event Action<Pedestrian> Soaked;

        /// <summary>The ghost of someone hit is out.</summary>
        public event Action<Ghost> GhostAppeared;

        public IReadOnlyList<Pedestrian> Active => _active;

        /// <summary>Pedestrians on the asphalt this frame, whom the traffic gives way to.</summary>
        public IReadOnlyList<Vector3> PointsOnRoad => _onRoad;

        [Inject]
        public void Construct(RoadNetwork network, IPlayerState player, FireService fires, WaterNozzle nozzle)
        {
            _network = network;
            _player = player;
            _fires = fires;
            _nozzle = nozzle;
        }

        /// <summary>Throws everyone standing near <paramref name="point"/> into the air.</summary>
        public void LaunchNear(Vector3 point, float radius, float upSpeed)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                var pedestrian = _active[i];
                if (pedestrian.Position.HorizontalDistance(point) < radius)
                {
                    pedestrian.Launch(Vector3.up * upSpeed + Random.insideUnitSphere * 2f,
                        Random.insideUnitSphere * 6f);
                }
            }
        }

        private void Start()
        {
            _graph = new SidewalkGraph(_network.Layout, _network.Graph);
            for (var i = 0; i < ghosts.Length; i++)
            {
                ghosts[i].gameObject.SetActive(false);
            }

            _fires.BuildingSaved += OnBuildingSaved;
            for (var attempts = count * 60; _active.Count < count && attempts > 0; attempts--)
            {
                TrySpawn(minSpawnRadius);
            }
        }

        private void OnDestroy()
        {
            _fires.BuildingSaved -= OnBuildingSaved;
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            var focus = _player.MapFocus.position;
            _onRoad.Clear();
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var pedestrian = _active[i];
                pedestrian.Tick(dt);
                var position = pedestrian.Position;
                if (position.HorizontalDistance(focus) > despawnRadius || position.y < OutOfWorldY)
                {
                    Despawn(i);
                }
                else if (_graph.IsOnRoad(position))
                {
                    _onRoad.Add(position);
                }
            }

            for (var tries = SpawnTriesPerFrame; tries > 0 && _active.Count < count; tries--)
            {
                TrySpawn(hiddenSpawnRadius);
            }

            CheckWater();
            CheckGawkers();
            UpdateGhosts();
        }

        private void FixedUpdate()
        {
            var car = _player.MovingCar;
            if (car == null)
            {
                return;
            }

            var velocity = car.PreStepVelocity.Flat();
            var speed = velocity.magnitude;
            if (speed < MinHitSpeed)
            {
                return;
            }

            var direction = velocity / speed;
            for (var i = 0; i < _active.Count; i++)
            {
                var pedestrian = _active[i];
                if (pedestrian.State == PedestrianState.Ragdoll)
                {
                    continue;
                }

                if (Touches(car, pedestrian.Position))
                {
                    Strike(pedestrian, velocity, speed);
                }
                else if (speed > ScareSpeed && IsInTheWay(car, pedestrian.Position, direction, speed))
                {
                    Scare(pedestrian, car.transform.position);
                }
            }
        }

        private static bool Touches(Vehicle car, Vector3 position)
        {
            var local = car.transform.InverseTransformPoint(position);
            return Mathf.Abs(local.x) < car.Width * 0.5f + HitMargin &&
                   Mathf.Abs(local.z) < car.HalfLength + HitMargin && Mathf.Abs(local.y) < HitHeight;
        }

        private static bool IsInTheWay(Vehicle car, Vector3 position, Vector3 direction, float speed)
        {
            var offset = (position - car.transform.position).Flat();
            var ahead = Vector3.Dot(offset, direction);
            var side = (offset - direction * ahead).magnitude;
            return ahead > 0f && ahead < car.HalfLength + speed * ScareSeconds && side < car.Width * 0.5f + ScareWidth;
        }

        private void Strike(Pedestrian pedestrian, Vector3 velocity, float speed)
        {
            // Faster than the car, so the body flies off the bumper instead of being dragged under it.
            var launch = velocity * 1.15f + Vector3.up * (2.5f + Mathf.Min(speed, 25f) * 0.25f);
            var spin = Vector3.Cross(Vector3.up, velocity).normalized * -6f;
            if (!pedestrian.Launch(launch, spin))
            {
                return;
            }

            _pendingGhosts.Add(new PendingGhost { Body = pedestrian, Time = Time.time + GhostDelay });
            Hit?.Invoke(pedestrian);
        }

        private void Scare(Pedestrian pedestrian, Vector3 threat)
        {
            var wasFleeing = pedestrian.State == PedestrianState.Flee;
            pedestrian.Flee(threat, FleeSeconds);
            if (!wasFleeing && pedestrian.State == PedestrianState.Flee)
            {
                Scared?.Invoke(pedestrian);
            }
        }

        private void CheckWater()
        {
            if (!_nozzle.IsWaterArriving || !_nozzle.Hit.collider.TryGetComponent<Pedestrian>(out var pedestrian))
            {
                return;
            }

            if (pedestrian.KnockBack(_nozzle.transform.position))
            {
                Soaked?.Invoke(pedestrian);
            }
        }

        // One pedestrian a frame looks around for a fire.
        private void CheckGawkers()
        {
            if (_active.Count == 0)
            {
                return;
            }

            _gawkIndex = (_gawkIndex + 1) % _active.Count;
            var pedestrian = _active[_gawkIndex];
            if (!pedestrian.IsCalm || Time.time < pedestrian.NextGawkTime)
            {
                return;
            }

            pedestrian.NextGawkTime = Time.time + GawkRetrySeconds;
            for (var i = 0; i < _fires.Fires.Count; i++)
            {
                var fire = _fires.Fires[i];
                var area = fire.Area;
                var near = fire.IsBurning && area.SqrDistance(pedestrian.Position) < GawkDistance * GawkDistance;
                if (near && Random.value < GawkChance)
                {
                    pedestrian.Emote(PedestrianEmote.Yell, area.center, Random.Range(5f, 9f));
                    pedestrian.NextGawkTime = Time.time + GawkCooldown;
                    return;
                }
            }
        }

        private void OnBuildingSaved(BuildingFire fire)
        {
            var area = fire.Area;
            for (var i = 0; i < _active.Count; i++)
            {
                if (area.SqrDistance(_active[i].Position) < CheerDistance * CheerDistance)
                {
                    var emote = Random.value < ClapShare ? PedestrianEmote.Clap : PedestrianEmote.Dance;
                    _active[i].Emote(emote, area.center, Random.Range(6f, 9f));
                }
            }
        }

        private void UpdateGhosts()
        {
            for (var i = _pendingGhosts.Count - 1; i >= 0; i--)
            {
                var pending = _pendingGhosts[i];
                if (Time.time < pending.Time)
                {
                    continue;
                }

                _pendingGhosts.RemoveAt(i);
                if (pending.Body.isActiveAndEnabled && pending.Body.State == PedestrianState.Ragdoll)
                {
                    SendGhost(pending.Body);
                }
            }
        }

        private void SendGhost(Pedestrian body)
        {
            for (var i = 0; i < ghosts.Length; i++)
            {
                if (!ghosts[i].IsOut)
                {
                    ghosts[i].Appear(body);
                    GhostAppeared?.Invoke(ghosts[i]);
                    return;
                }
            }
        }

        private void TrySpawn(float visibleFrom)
        {
            var point = Random.Range(0, _graph.Points.Length);
            var links = _graph.Links(point);
            var link = links[Random.Range(0, links.Count)];
            if (link.IsCrossing)
            {
                return;
            }

            var along = Random.value;
            var position = Vector3.Lerp(_graph.Points[point], _graph.Points[link.To], along);
            var distance = position.HorizontalDistance(_player.MapFocus.position);
            var outOfRange = distance < minSpawnRadius || distance > spawnRadius;
            if (outOfRange || (distance < visibleFrom && IsInView(position)))
            {
                return;
            }

            var pedestrian = Take();
            pedestrian.gameObject.SetActive(true);
            pedestrian.Spawn(_graph, point, link, along);
            pedestrian.NextGawkTime = Time.time + Random.Range(0f, GawkRetrySeconds);
            _active.Add(pedestrian);
        }

        private bool IsInView(Vector3 position)
        {
            var view = viewCamera.WorldToViewportPoint(position + Vector3.up);
            return view.z > 0f && view.x > -0.1f && view.x < 1.1f && view.y > -0.1f && view.y < 1.1f;
        }

        private Pedestrian Take()
        {
            if (_pool.Count == 0)
            {
                return Instantiate(prefabs[_created++ % prefabs.Length], transform);
            }

            var index = Random.Range(0, _pool.Count);
            var pooled = _pool[index];
            _pool.RemoveAt(index);
            return pooled;
        }

        private void Despawn(int index)
        {
            var pedestrian = _active[index];
            _active.RemoveAt(index);
            pedestrian.gameObject.SetActive(false);
            _pool.Add(pedestrian);
        }
    }
}
