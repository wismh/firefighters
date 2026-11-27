using System.Collections.Generic;
using UnityEngine;

namespace _Project.Traffic
{
    /// <summary>
    /// A grid node. Owns its connector lanes, a two-axis traffic signal and a reservation table:
    /// a vehicle may enter a connector only while no conflicting connector is occupied, so cars never
    /// drive through each other inside the box.
    /// </summary>
    public sealed class Intersection
    {
        private const float ConflictDistance = 3.2f;
        private const int ConflictSamples = 12;

        // Phase order: axis 0 green, axis 0 yellow, all red, axis 1 green, axis 1 yellow, all red.
        private readonly float[] _phaseDurations;
        private bool[,] _conflicts;
        private int[] _occupancy;
        private int _phase;
        private float _phaseTime;

        public Intersection(Vector2Int cell, Vector3 position, bool hasSignal, float green, float yellow,
            float allRed, float startOffset)
        {
            Cell = cell;
            Position = position;
            HasSignal = hasSignal;
            _phaseDurations = new[] { green, yellow, allRed, green, yellow, allRed };
            AdvanceBy(startOffset);
        }

        public Vector2Int Cell { get; }
        public Vector3 Position { get; }
        public bool HasSignal { get; }
        public List<Lane> Connectors { get; } = new List<Lane>(12);

        public SignalState GetSignal(int axis)
        {
            if (!HasSignal)
            {
                return SignalState.Green;
            }

            var greenPhase = axis == 0 ? 0 : 3;
            if (_phase == greenPhase)
            {
                return SignalState.Green;
            }

            return _phase == greenPhase + 1 ? SignalState.Yellow : SignalState.Red;
        }

        /// <summary>Advances the signal. Returns true when the phase changed.</summary>
        public bool Tick(float dt)
        {
            return HasSignal && AdvanceBy(dt);
        }

        public bool CanEnter(Lane connector)
        {
            var index = connector.ConnectorIndex;
            for (var i = 0; i < _occupancy.Length; i++)
            {
                if (_occupancy[i] > 0 && _conflicts[index, i])
                {
                    return false;
                }
            }

            return true;
        }

        public void Occupy(Lane connector) => _occupancy[connector.ConnectorIndex]++;

        public void Release(Lane connector)
        {
            var index = connector.ConnectorIndex;
            _occupancy[index] = Mathf.Max(0, _occupancy[index] - 1);
        }

        /// <summary>Called once after all connectors are added.</summary>
        public void BuildConflicts()
        {
            var count = Connectors.Count;
            _occupancy = new int[count];
            _conflicts = new bool[count, count];
            var samples = new Vector3[count][];
            for (var i = 0; i < count; i++)
            {
                samples[i] = new Vector3[ConflictSamples + 1];
                var lane = Connectors[i];
                for (var k = 0; k <= ConflictSamples; k++)
                {
                    samples[i][k] = lane.Evaluate(lane.Length * k / ConflictSamples);
                }
            }

            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    // Connectors from one approach lane are ordered by car following, not by reservation.
                    if (Connectors[i].EntryLane == Connectors[j].EntryLane)
                    {
                        continue;
                    }

                    var conflict = PathsCross(samples[i], samples[j]);
                    _conflicts[i, j] = conflict;
                    _conflicts[j, i] = conflict;
                }
            }
        }

        private static bool PathsCross(Vector3[] a, Vector3[] b)
        {
            const float sqr = ConflictDistance * ConflictDistance;
            for (var i = 0; i < a.Length; i++)
            {
                for (var j = 0; j < b.Length; j++)
                {
                    if ((a[i] - b[j]).sqrMagnitude < sqr)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool AdvanceBy(float dt)
        {
            var changed = false;
            _phaseTime += dt;
            while (_phaseTime >= _phaseDurations[_phase])
            {
                _phaseTime -= _phaseDurations[_phase];
                _phase = (_phase + 1) % _phaseDurations.Length;
                changed = true;
            }

            return changed;
        }
    }
}
