using System;
using System.Collections.Generic;
using _Project.City;
using _Project.Comedy;
using _Project.FireHose;
using _Project.Pedestrians;
using _Project.Player;
using UnityEngine;
using Zenject;

namespace _Project.Mission
{
    /// <summary>
    /// The "test task": the dispatcher's intro, then the fires catch one call at a time, nearest to the start first,
    /// the last two together. Each call is announced over the radio with its direction and distance and gets
    /// on-screen markers; the next one comes a few seconds after the last is put out. On the first call the
    /// dispatcher walks the player through it. When the city is saved: fireworks, closing words, the report.
    /// Player input is off while the dispatcher speaks a monologue.
    /// </summary>
    public sealed class MissionDirector : MonoBehaviour
    {
        // With this many flames or fewer left in a building the dispatcher points at the markers.
        private const int LastFlamesHint = 3;
        private const float ReigniteLineCooldown = 40f;

        [SerializeField] private MissionScript script;
        [SerializeField] private RadioView radio;
        [SerializeField] private TargetIndicator targets;
        [SerializeField] private PlayerModeController modes;

        private readonly List<List<BuildingFire>> _stages = new List<List<BuildingFire>>();
        private readonly List<string> _monologue = new List<string>(4);

        private FireService _fires;
        private PlayerInputs _inputs;
        private DamageBill _bill;
        private PedestrianSystem _pedestrians;

        private Phase _phase;
        private float _timer;
        private int _stage = -1;
        private bool _saidArrived;
        private bool _saidHose;
        private bool _saidLastFlames;
        private bool _saidPedestrian;
        private float _nextReigniteLine;

        private enum Phase
        {
            Waiting,
            Intro,
            Active,
            Between,
            Fireworks,
            Outro,
            Done,
        }

        /// <summary>Raised when the HUD line with the call number changes.</summary>
        public event Action StatusChanged;

        /// <summary>Raised the moment the last fire goes out.</summary>
        public event Action AllFiresOut;

        /// <summary>Raised after the closing words, when the report is due.</summary>
        public event Action Completed;

        /// <summary>The HUD line: which call it is and how many fires burn.</summary>
        public string Status { get; private set; } = string.Empty;

        public int StageCount => _stages.Count;

        [Inject]
        public void Construct(FireService fires, PlayerInputs inputs, DamageBill bill, PedestrianSystem pedestrians)
        {
            _fires = fires;
            _inputs = inputs;
            _bill = bill;
            _pedestrians = pedestrians;
        }

        private void Start()
        {
            BuildStages(modes.MapFocus.position);
            _fires.BuildingSaved += OnBuildingSaved;
            _pedestrians.Hit += OnPedestrianHit;
            foreach (var fire in _fires.Fires)
            {
                fire.Reignited += OnReignited;
            }

            _phase = Phase.Waiting;
            _timer = script.introDelay;
        }

        private void OnDestroy()
        {
            _fires.BuildingSaved -= OnBuildingSaved;
            _pedestrians.Hit -= OnPedestrianHit;
            foreach (var fire in _fires.Fires)
            {
                fire.Reignited -= OnReignited;
            }
        }

        /// <summary>One fire per call, nearest to the start first; the two farthest make the last call together.</summary>
        private void BuildStages(Vector3 start)
        {
            var order = new List<BuildingFire>(_fires.Fires);
            order.Sort((a, b) => Distance(a, start).CompareTo(Distance(b, start)));
            var singles = order.Count >= 3 ? order.Count - 2 : order.Count;
            for (var i = 0; i < singles; i++)
            {
                _stages.Add(new List<BuildingFire> { order[i] });
            }

            if (singles < order.Count)
            {
                _stages.Add(order.GetRange(singles, order.Count - singles));
            }
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Waiting:
                    if (Tick())
                    {
                        _phase = Phase.Intro;
                        SetLocked(true);
                        radio.Monologue(script.intro, OnIntroDone);
                    }

                    break;
                case Phase.Active:
                    UpdateHints();
                    break;
                case Phase.Between:
                    if (Tick())
                    {
                        StartStage(_stage + 1);
                    }

                    break;
                case Phase.Fireworks:
                    if (Tick())
                    {
                        StartOutro();
                    }

                    break;
            }
        }

        private bool Tick()
        {
            _timer -= Time.deltaTime;
            return _timer <= 0f;
        }

        private void OnIntroDone()
        {
            SetLocked(false);
            StartStage(0);
        }

        private void StartStage(int index)
        {
            _stage = index;
            _phase = Phase.Active;
            var stage = _stages[index];
            foreach (var fire in stage)
            {
                fire.Ignite();
            }

            targets.SetTargets(stage);
            var focus = modes.MapFocus.position;
            var nearest = stage[0];
            foreach (var fire in stage)
            {
                if (Distance(fire, focus) < Distance(nearest, focus))
                {
                    nearest = fire;
                }
            }

            var line = index == 0 ? script.firstCall
                : stage.Count > 1 ? script.finalCall
                : script.nextCall;
            radio.Say(string.Format(line, Direction(nearest, focus), RoundedDistance(nearest, focus)), true);
            UpdateStatus();
        }

        private void UpdateHints()
        {
            var stage = _stages[_stage];
            if (_stage == 0 && !_saidArrived && Distance(stage[0], modes.MapFocus.position) < script.arrivalDistance)
            {
                _saidArrived = true;
                radio.Say(script.arrived);
            }

            if (_stage == 0 && !_saidHose && modes.IsHosing)
            {
                _saidHose = true;
                radio.Say(script.hoseTaken);
            }

            if (_saidLastFlames)
            {
                return;
            }

            foreach (var fire in stage)
            {
                if (fire.IsBurning && fire.BurningFlames <= LastFlamesHint)
                {
                    _saidLastFlames = true;
                    radio.Say(script.lastFlames);
                    return;
                }
            }
        }

        private void OnBuildingSaved(BuildingFire fire)
        {
            UpdateStatus();
            var stage = _stages[_stage];
            foreach (var other in stage)
            {
                if (other.IsBurning)
                {
                    return;
                }
            }

            targets.Clear();
            if (_stage == _stages.Count - 1)
            {
                _phase = Phase.Fireworks;
                _timer = script.outroDelay;
                AllFiresOut?.Invoke();
                UpdateStatus();
                return;
            }

            _phase = Phase.Between;
            _timer = script.stagePause;
            radio.Say(script.callSaved[_stage % script.callSaved.Length]);
            UpdateStatus();
        }

        private void StartOutro()
        {
            _phase = Phase.Outro;
            _monologue.Clear();
            _monologue.Add(script.outroStart);
            if (_bill.Total == 0)
            {
                _monologue.Add(script.outroClean);
            }
            else if (_bill.Total > script.messyBill)
            {
                _monologue.Add(script.outroMessy);
            }

            _monologue.Add(script.outroEnd);
            SetLocked(true);
            radio.Monologue(_monologue, OnOutroDone);
        }

        private void OnOutroDone()
        {
            SetLocked(false);
            _phase = Phase.Done;
            Completed?.Invoke();
        }

        private void OnReignited(BuildingFire fire)
        {
            if (_phase != Phase.Active || Time.time < _nextReigniteLine)
            {
                return;
            }

            _nextReigniteLine = Time.time + ReigniteLineCooldown;
            radio.Say(script.reignited);
        }

        private void OnPedestrianHit(Pedestrian pedestrian)
        {
            if (_saidPedestrian || _phase == Phase.Intro || _phase == Phase.Outro)
            {
                return;
            }

            _saidPedestrian = true;
            radio.Say(script.pedestrianHit);
        }

        private void SetLocked(bool locked)
        {
            _inputs.SetPlayerEnabled(!locked);
        }

        private void UpdateStatus()
        {
            Status = _phase switch
            {
                Phase.Active => ActiveStatus(),
                Phase.Between => $"Виклик {_stage + 1} / {_stages.Count} виконано. Чекай наступного...",
                Phase.Fireworks or Phase.Outro or Phase.Done => "Усі пожежі згашено!",
                _ => string.Empty,
            };
            StatusChanged?.Invoke();
        }

        private string ActiveStatus()
        {
            var burning = 0;
            foreach (var fire in _stages[_stage])
            {
                burning += fire.IsBurning ? 1 : 0;
            }

            var call = $"Виклик {_stage + 1} / {_stages.Count}";
            return _stages[_stage].Count > 1 ? $"{call}   Пожеж: {burning}" : call;
        }

        private string Direction(BuildingFire fire, Vector3 from)
        {
            var offset = fire.Top - from;
            var angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            var sector = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8;
            return script.directions[sector];
        }

        private static int RoundedDistance(BuildingFire fire, Vector3 from)
        {
            return Mathf.Max(50, Mathf.RoundToInt(Distance(fire, from) / 50f) * 50);
        }

        private static float Distance(BuildingFire fire, Vector3 from)
        {
            var offset = fire.Top - from;
            offset.y = 0f;
            return offset.magnitude;
        }
    }
}
