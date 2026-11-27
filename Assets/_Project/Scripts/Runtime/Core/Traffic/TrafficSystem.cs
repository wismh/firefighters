using System.Collections.Generic;
using _Project.City;
using _Project.Vehicles;
using UnityEngine;
using Zenject;

namespace _Project.Traffic
{
    /// <summary>
    /// Spawns, pools and drives every traffic car. Cars follow the lane graph with the Intelligent Driver
    /// Model, stop at red lights, reserve their path through intersections and brake for the player.
    /// </summary>
    public sealed class TrafficSystem : MonoBehaviour
    {
        private const float RespawnInterval = 0.5f;
        private const float DespawnCheckInterval = 1f;
        private const float SpawnSpacing = 14f;
        private const int PathLength = 3;
        private const float ObstacleLookAhead = 35f;
        private const float StopLineMargin = 1f;
        private const float PedestrianRadius = 0.7f;

        [SerializeField] private TrafficSettings settings;

        private readonly List<TrafficAgent> _agents = new List<TrafficAgent>(64);
        private readonly Stack<TrafficAgent> _agentPool = new Stack<TrafficAgent>(64);
        private readonly Dictionary<Vehicle, TrafficAgent> _agentByVehicle = new Dictionary<Vehicle, TrafficAgent>(64);
        private readonly List<Vehicle> _detached = new List<Vehicle>(16);

        private RoadNetwork _network;
        private IRoadObstacles _obstacles;
        private TrafficVehiclePool _pool;
        private Transform _focus;
        private float _focusRadius;
        private Vehicle _playerVehicle;
        private float _respawnTimer;
        private float _despawnTimer;

        /// <summary>Raised after a traffic car hit by the player was taken out of traffic.</summary>
        public event System.Action<Vehicle> KnockedOut;

        public TrafficSettings Settings => settings;
        public int ActiveCount => _agents.Count;

        [Inject]
        public void Construct(RoadNetwork network, IRoadObstacles obstacles)
        {
            _network = network;
            _obstacles = obstacles;
        }

        /// <summary>What the traffic brakes for and keeps spawning away from: the player or the player's car.</summary>
        public void SetFocus(Transform focus, float radius, Vehicle playerVehicle)
        {
            _focus = focus;
            _focusRadius = radius;
            if (_playerVehicle != null && _playerVehicle != playerVehicle)
            {
                _detached.Add(_playerVehicle);
            }

            _playerVehicle = playerVehicle;
            if (playerVehicle != null)
            {
                _detached.Remove(playerVehicle);
            }
        }

        /// <summary>Takes a car out of traffic (the player enters it, or it was knocked out) and parks it.</summary>
        public void Detach(Vehicle vehicle)
        {
            if (!_agentByVehicle.TryGetValue(vehicle, out var agent))
            {
                return;
            }

            RemoveAgent(agent);
            vehicle.SetMode(VehicleMode.Parked);
            if (vehicle != _playerVehicle)
            {
                _detached.Add(vehicle);
            }
        }

        private void Awake()
        {
            _pool = new TrafficVehiclePool(settings.vehiclePrefabs, transform, OnKnockedOut);
        }

        private void Start()
        {
            var attempts = settings.vehicleCount * 20;
            while (_agents.Count < settings.vehicleCount && attempts-- > 0)
            {
                TrySpawn(20f);
            }
        }

        private void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            _network.TickSignals(dt);

            for (var i = 0; i < _agents.Count; i++)
            {
                _agents[i].Acceleration = ComputeAcceleration(_agents[i]);
            }

            for (var i = 0; i < _agents.Count; i++)
            {
                Integrate(_agents[i], dt);
            }

            _respawnTimer -= dt;
            if (_respawnTimer <= 0f)
            {
                _respawnTimer = RespawnInterval;
                if (_agents.Count < settings.vehicleCount)
                {
                    TrySpawn(settings.respawnMinDistance);
                }
            }

            _despawnTimer -= dt;
            if (_despawnTimer <= 0f)
            {
                _despawnTimer = DespawnCheckInterval;
                DespawnFarDetached();
            }
        }

        private float ComputeAcceleration(TrafficAgent agent)
        {
            var gap = float.MaxValue;
            var leaderSpeed = 0f;
            var desiredSpeed = agent.Lane.SpeedLimit;
            ScanAhead(agent, ref gap, ref leaderSpeed, ref desiredSpeed);
            ScanObstacles(agent, ref gap, ref leaderSpeed);
            return Idm(agent.Speed, desiredSpeed, gap, agent.Speed - leaderSpeed);
        }

        private void ScanAhead(TrafficAgent agent, ref float gap, ref float leaderSpeed, ref float desiredSpeed)
        {
            var lane = agent.Lane;
            var distance = -agent.S;
            var pathIndex = 0;
            while (true)
            {
                ScanLane(agent, lane, lane, distance, ref gap, ref leaderSpeed);
                if (lane.IsConnector)
                {
                    // Cars that took another connector from the same approach lane are still beside us at first.
                    var connectors = lane.Intersection.Connectors;
                    for (var i = 0; i < connectors.Count; i++)
                    {
                        if (connectors[i] != lane && connectors[i].EntryLane == lane.EntryLane)
                        {
                            ScanLane(agent, connectors[i], lane, distance, ref gap, ref leaderSpeed);
                        }
                    }
                }

                if (gap < float.MaxValue)
                {
                    return;
                }

                distance += lane.Length;
                if (distance - agent.HalfLength > settings.lookAhead || pathIndex >= agent.Path.Count)
                {
                    return;
                }

                var next = agent.Path[pathIndex++];
                var toNext = Mathf.Max(0f, distance - agent.HalfLength);
                if (next.IsConnector && agent.CommittedConnector != next)
                {
                    var stopGap = toNext - StopLineMargin;
                    if (!TryCommit(agent, lane, next, stopGap))
                    {
                        gap = stopGap;
                        leaderSpeed = 0f;
                        return;
                    }
                }

                if (next.SpeedLimit < desiredSpeed)
                {
                    var limit = Mathf.Sqrt(next.SpeedLimit * next.SpeedLimit +
                                           2f * settings.comfortableBraking * toNext);
                    desiredSpeed = Mathf.Min(desiredSpeed, limit);
                }

                lane = next;
            }
        }

        private static void ScanLane(TrafficAgent agent, Lane lane, Lane pathLane, float distance, ref float gap,
            ref float leaderSpeed)
        {
            var agents = lane.Agents;
            var onOwnLane = pathLane == agent.Lane;
            for (var i = 0; i < agents.Count; i++)
            {
                var other = agents[i];
                if (other == agent || (onOwnLane && other.S <= agent.S))
                {
                    continue;
                }

                var g = distance + other.S - other.HalfLength - agent.HalfLength;
                if (g < gap)
                {
                    gap = g;
                    leaderSpeed = other.Speed;
                }
            }
        }

        private bool TryCommit(TrafficAgent agent, Lane approach, Lane connector, float stopGap)
        {
            var intersection = connector.Intersection;
            var signal = intersection.GetSignal(approach.EndDirection.Axis());
            var canStop = stopGap > agent.Speed * agent.Speed / (2f * settings.maxBraking);
            if (signal != SignalState.Green && canStop)
            {
                return false;
            }

            var exit = connector.Next[0];
            var needed = agent.Vehicle.Length + settings.minGap;
            if (!intersection.CanEnter(connector) || exit.FreeSpaceAtStart() < needed)
            {
                return false;
            }

            // Far from the line the way is clear, but nothing is reserved yet.
            var commitDistance = agent.Speed * agent.Speed / (2f * settings.comfortableBraking) + 4f;
            if (stopGap > commitDistance)
            {
                return true;
            }

            Commit(agent, connector);
            return true;
        }

        private static void Commit(TrafficAgent agent, Lane connector)
        {
            connector.Intersection.Occupy(connector);
            var exit = connector.Next[0];
            var reserve = agent.Vehicle.Length + 2f;
            exit.IncomingReserved += reserve;
            agent.CommittedConnector = connector;
            agent.ReservedExitLane = exit;
            agent.ReservedAmount = reserve;
        }

        private void ScanObstacles(TrafficAgent agent, ref float gap, ref float leaderSpeed)
        {
            var position = agent.Vehicle.transform.position;
            var forward = agent.Lane.Tangent(agent.S);
            var right = new Vector3(forward.z, 0f, -forward.x);
            var halfWidth = agent.Vehicle.Width * 0.5f;

            if (_focus != null)
            {
                CheckObstacle(agent, position, forward, right, halfWidth, _focus.position, _focusRadius, ref gap,
                    ref leaderSpeed);
            }

            for (var i = 0; i < _detached.Count; i++)
            {
                var parked = _detached[i];
                CheckObstacle(agent, position, forward, right, halfWidth, parked.transform.position,
                    parked.HalfLength * 0.8f, ref gap, ref leaderSpeed);
            }

            var pedestrians = _obstacles.PointsOnRoad;
            for (var i = 0; i < pedestrians.Count; i++)
            {
                CheckObstacle(agent, position, forward, right, halfWidth, pedestrians[i], PedestrianRadius, ref gap,
                    ref leaderSpeed);
            }
        }

        private static void CheckObstacle(TrafficAgent agent, Vector3 position, Vector3 forward, Vector3 right,
            float halfWidth, Vector3 obstacle, float radius, ref float gap, ref float leaderSpeed)
        {
            var offset = obstacle - position;
            var ahead = Vector3.Dot(offset, forward);
            if (ahead <= 0f || ahead > ObstacleLookAhead)
            {
                return;
            }

            if (Mathf.Abs(Vector3.Dot(offset, right)) > halfWidth + radius + 0.3f)
            {
                return;
            }

            var g = ahead - agent.HalfLength - radius;
            if (g < gap)
            {
                gap = g;
                leaderSpeed = 0f;
            }
        }

        private float Idm(float speed, float desiredSpeed, float gap, float approachRate)
        {
            var a = settings.maxAcceleration;
            var b = settings.comfortableBraking;
            var ratio = speed / Mathf.Max(desiredSpeed, 0.1f);
            var free = 1f - ratio * ratio * ratio * ratio;
            if (gap >= float.MaxValue)
            {
                return a * free;
            }

            var desiredGap = settings.minGap +
                             Mathf.Max(0f, speed * settings.timeHeadway + speed * approachRate / (2f * Mathf.Sqrt(a * b)));
            var g = Mathf.Max(gap, 0.1f);
            var interaction = desiredGap / g;
            return Mathf.Max(a * (free - interaction * interaction), -settings.maxBraking * 1.5f);
        }

        private void Integrate(TrafficAgent agent, float dt)
        {
            agent.Speed = Mathf.Max(0f, agent.Speed + agent.Acceleration * dt);
            var step = agent.Speed * dt;
            agent.S += step;

            while (agent.S > agent.Lane.Length)
            {
                var next = agent.Path[0];
                if (next.IsConnector && agent.CommittedConnector != next)
                {
                    // Overshot the line without a reservation (hard braking was not enough): claim it now.
                    Commit(agent, next);
                }

                agent.S -= agent.Lane.Length;
                var previous = agent.Lane;
                previous.Agents.Remove(agent);
                agent.Path.RemoveAt(0);
                agent.Lane = next;
                next.Agents.Add(agent);
                FillPath(agent);

                if (previous.IsConnector)
                {
                    agent.ReleasePendingConnector = previous;
                    agent.ReservedExitLane.IncomingReserved -= agent.ReservedAmount;
                    agent.ReservedExitLane = null;
                    agent.ReservedAmount = 0f;
                    agent.CommittedConnector = null;
                }
            }

            if (agent.ReleasePendingConnector != null && agent.S - agent.HalfLength > 0f)
            {
                agent.ReleasePendingConnector.Intersection.Release(agent.ReleasePendingConnector);
                agent.ReleasePendingConnector = null;
            }

            var position = agent.Lane.Evaluate(agent.S);
            var rotation = Quaternion.LookRotation(agent.Lane.Tangent(agent.S), Vector3.up);
            agent.Vehicle.TrafficSpeed = agent.Speed;
            agent.Vehicle.MoveKinematic(position, rotation, step);
        }

        private static void FillPath(TrafficAgent agent)
        {
            while (agent.Path.Count < PathLength)
            {
                var last = agent.Path.Count > 0 ? agent.Path[agent.Path.Count - 1] : agent.Lane;
                var options = last.Next;
                agent.Path.Add(options[Random.Range(0, options.Count)]);
            }
        }

        private bool TrySpawn(float minDistanceToFocus)
        {
            var lanes = _network.Graph.RoadLanes;
            var lane = lanes[Random.Range(0, lanes.Count)];
            var s = Random.Range(SpawnSpacing, lane.Length - SpawnSpacing);
            for (var i = 0; i < lane.Agents.Count; i++)
            {
                if (Mathf.Abs(lane.Agents[i].S - s) < SpawnSpacing)
                {
                    return false;
                }
            }

            var position = lane.Evaluate(s);
            if (_focus != null && Vector3.Distance(position, _focus.position) < minDistanceToFocus)
            {
                return false;
            }

            for (var i = 0; i < _detached.Count; i++)
            {
                if (Vector3.Distance(_detached[i].transform.position, position) < SpawnSpacing)
                {
                    return false;
                }
            }

            var vehicle = _pool.Take();
            vehicle.ResetForTraffic(position, Quaternion.LookRotation(lane.Tangent(s), Vector3.up));
            vehicle.gameObject.SetActive(true);

            var agent = _agentPool.Count > 0 ? _agentPool.Pop() : new TrafficAgent();
            agent.Vehicle = vehicle;
            agent.Lane = lane;
            agent.S = s;
            agent.Speed = lane.SpeedLimit * 0.5f;
            lane.Agents.Add(agent);
            FillPath(agent);
            _agents.Add(agent);
            _agentByVehicle.Add(vehicle, agent);
            return true;
        }

        private void OnKnockedOut(Vehicle vehicle)
        {
            Detach(vehicle);
            KnockedOut?.Invoke(vehicle);
        }

        private void RemoveAgent(TrafficAgent agent)
        {
            agent.Lane.Agents.Remove(agent);
            if (agent.CommittedConnector != null)
            {
                agent.CommittedConnector.Intersection.Release(agent.CommittedConnector);
            }

            if (agent.ReservedExitLane != null)
            {
                agent.ReservedExitLane.IncomingReserved -= agent.ReservedAmount;
            }

            if (agent.ReleasePendingConnector != null)
            {
                agent.ReleasePendingConnector.Intersection.Release(agent.ReleasePendingConnector);
            }

            _agents.Remove(agent);
            _agentByVehicle.Remove(agent.Vehicle);
            agent.Clear();
            _agentPool.Push(agent);
        }

        private void DespawnFarDetached()
        {
            if (_focus == null)
            {
                return;
            }

            var focus = _focus.position;
            for (var i = _detached.Count - 1; i >= 0; i--)
            {
                var vehicle = _detached[i];
                if (Vector3.Distance(vehicle.transform.position, focus) < settings.despawnDistance)
                {
                    continue;
                }

                _detached.RemoveAt(i);
                _pool.Return(vehicle);
            }
        }
    }
}
