using System;
using System.Collections.Generic;
using _Project.Common;
using _Project.Economy;
using _Project.Fire;
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

        private readonly List<List<BuildingFire>> _stages = new List<List<BuildingFire>>();
        private readonly List<string> _monologue = new List<string>(4);

        private FireService _fires;
        private PlayerInputs _inputs;
        private DamageBill _bill;
        private PedestrianSystem _pedestrians;
        private IPlayerState _player;
        private RadioView _radio;
        private TargetIndicator _targets;

        private MissionPhase _phase;
        private float _timer;
        private int _stage = -1;
        private bool _saidArrived;
        private bool _saidHose;
        private bool _saidLastFlames;
        private bool _saidPedestrian;
        private float _nextReigniteLine;

        /// <summary>Raised when the phase, the call or the number of fires burning in it changes.</summary>
        public event Action StatusChanged;

        /// <summary>Raised the moment the last fire goes out.</summary>
        public event Action AllFiresOut;

        /// <summary>Raised after the closing words, when the report is due.</summary>
        public event Action Completed;

        public MissionScript Script => script;

        public MissionPhase Phase => _phase;

        /// <summary>The call under way or just finished, from 0; -1 before the first.</summary>
        public int Stage => _stage;

        public int StageCount => _stages.Count;

        /// <summary>Fires of the current call; empty before the first.</summary>
        public IReadOnlyList<BuildingFire> StageFires => _stage >= 0 ? _stages[_stage] : Array.Empty<BuildingFire>();

        public int BurningInStage
        {
            get
            {
                var burning = 0;
                foreach (var fire in StageFires)
                {
                    burning += fire.IsBurning ? 1 : 0;
                }

                return burning;
            }
        }

        [Inject]
        public void Construct(FireService fires, PlayerInputs inputs, DamageBill bill, PedestrianSystem pedestrians,
            IPlayerState player, RadioView radio, TargetIndicator targets)
        {
            _fires = fires;
            _inputs = inputs;
            _bill = bill;
            _pedestrians = pedestrians;
            _player = player;
            _radio = radio;
            _targets = targets;
        }

        private void Start()
        {
            BuildStages(_player.MapFocus.position);
            _fires.BuildingSaved += OnBuildingSaved;
            _pedestrians.Hit += OnPedestrianHit;
            foreach (var fire in _fires.Fires)
            {
                fire.Reignited += OnReignited;
            }

            _phase = MissionPhase.Waiting;
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

        private void BuildStages(Vector3 start)
        {
            var fires = _fires.Fires;
            var tops = new Vector3[fires.Count];
            for (var i = 0; i < fires.Count; i++)
            {
                tops[i] = fires[i].Top;
            }

            foreach (var indices in MissionPlan.BuildStages(tops, start))
            {
                var stage = new List<BuildingFire>(indices.Count);
                foreach (var index in indices)
                {
                    stage.Add(fires[index]);
                }

                _stages.Add(stage);
            }
        }

        private void Update()
        {
            switch (_phase)
            {
                case MissionPhase.Waiting:
                    if (Tick())
                    {
                        SetPhase(MissionPhase.Intro);
                        SetLocked(true);
                        _radio.Monologue(script.intro, OnIntroDone);
                    }

                    break;
                case MissionPhase.Active:
                    UpdateHints();
                    break;
                case MissionPhase.Between:
                    if (Tick())
                    {
                        StartStage(_stage + 1);
                    }

                    break;
                case MissionPhase.Fireworks:
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

        private void SetPhase(MissionPhase phase)
        {
            _phase = phase;
            StatusChanged?.Invoke();
        }

        private void OnIntroDone()
        {
            SetLocked(false);
            StartStage(0);
        }

        private void StartStage(int index)
        {
            _stage = index;
            var stage = _stages[index];
            foreach (var fire in stage)
            {
                fire.Ignite();
            }

            _targets.SetTargets(stage);
            var focus = _player.MapFocus.position;
            var nearest = stage[0];
            foreach (var fire in stage)
            {
                if (fire.Top.HorizontalDistance(focus) < nearest.Top.HorizontalDistance(focus))
                {
                    nearest = fire;
                }
            }

            var line = index == 0 ? script.firstCall
                : stage.Count > 1 ? script.finalCall
                : script.nextCall;
            var direction = script.directions[MissionPlan.CompassSector(focus, nearest.Top)];
            var distance = MissionPlan.RadioDistance(nearest.Top.HorizontalDistance(focus));
            _radio.Say(string.Format(line, direction, distance), true);
            SetPhase(MissionPhase.Active);
        }

        private void UpdateHints()
        {
            var stage = _stages[_stage];
            var arrived = stage[0].Top.HorizontalDistance(_player.MapFocus.position) < script.arrivalDistance;
            if (_stage == 0 && !_saidArrived && arrived)
            {
                _saidArrived = true;
                _radio.Say(script.arrived);
            }

            if (_stage == 0 && !_saidHose && _player.IsHosing)
            {
                _saidHose = true;
                _radio.Say(script.hoseTaken);
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
                    _radio.Say(script.lastFlames);
                    return;
                }
            }
        }

        private void OnBuildingSaved(BuildingFire fire)
        {
            if (BurningInStage > 0)
            {
                StatusChanged?.Invoke();
                return;
            }

            _targets.Clear();
            if (_stage == _stages.Count - 1)
            {
                _timer = script.outroDelay;
                SetPhase(MissionPhase.Fireworks);
                AllFiresOut?.Invoke();
                return;
            }

            _timer = script.stagePause;
            _radio.Say(script.callSaved[_stage % script.callSaved.Length]);
            SetPhase(MissionPhase.Between);
        }

        private void StartOutro()
        {
            SetPhase(MissionPhase.Outro);
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
            _radio.Monologue(_monologue, OnOutroDone);
        }

        private void OnOutroDone()
        {
            SetLocked(false);
            SetPhase(MissionPhase.Done);
            Completed?.Invoke();
        }

        private void OnReignited(BuildingFire fire)
        {
            if (_phase != MissionPhase.Active || Time.time < _nextReigniteLine)
            {
                return;
            }

            _nextReigniteLine = Time.time + ReigniteLineCooldown;
            _radio.Say(script.reignited);
        }

        private void OnPedestrianHit(Pedestrian pedestrian)
        {
            if (_saidPedestrian || _phase == MissionPhase.Intro || _phase == MissionPhase.Outro)
            {
                return;
            }

            _saidPedestrian = true;
            _radio.Say(script.pedestrianHit);
        }

        private void SetLocked(bool locked)
        {
            _inputs.SetPlayerEnabled(!locked);
        }
    }
}
