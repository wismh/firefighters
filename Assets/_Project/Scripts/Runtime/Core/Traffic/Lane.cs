using System.Collections.Generic;
using _Project.City;
using UnityEngine;

namespace _Project.Traffic
{
    /// <summary>
    /// One directed lane: a cubic Bezier from P0 to P3. Road lanes run between intersections,
    /// connector lanes run through an intersection from an approach lane to an exit lane.
    /// </summary>
    public sealed class Lane
    {
        private const int LutSamples = 16;

        private readonly float[] _lut = new float[LutSamples + 1];

        public Lane(int id, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float speedLimit)
        {
            Id = id;
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
            SpeedLimit = speedLimit;
            BuildLut();
        }

        public int Id { get; }
        public Vector3 P0 { get; }
        public Vector3 P1 { get; }
        public Vector3 P2 { get; }
        public Vector3 P3 { get; }
        public float Length { get; private set; }
        public float SpeedLimit { get; }

        /// <summary>Lanes a vehicle may take after this one.</summary>
        public List<Lane> Next { get; } = new List<Lane>(3);

        /// <summary>Vehicles currently on this lane (their centre is on it).</summary>
        public List<TrafficAgent> Agents { get; } = new List<TrafficAgent>(4);

        /// <summary>Set for connectors: the intersection they cross.</summary>
        public Intersection Intersection { get; set; }

        /// <summary>Set for road lanes: the intersection at the stop line.</summary>
        public Intersection EndIntersection { get; set; }

        /// <summary>Set for connectors: the approach lane they start from.</summary>
        public Lane EntryLane { get; set; }

        /// <summary>Travel direction at the end of the lane.</summary>
        public GridDirection EndDirection { get; set; }

        /// <summary>Index inside <see cref="Intersection"/>'s connector list.</summary>
        public int ConnectorIndex { get; set; } = -1;

        /// <summary>Metres claimed by vehicles that committed to enter this lane but are not on it yet.</summary>
        public float IncomingReserved { get; set; }

        public bool IsConnector => Intersection != null;

        public Vector3 Evaluate(float s) => EvaluateT(DistanceToT(s));

        public Vector3 Tangent(float s)
        {
            var t = DistanceToT(s);
            var u = 1f - t;
            var d = 3f * u * u * (P1 - P0) + 6f * u * t * (P2 - P1) + 3f * t * t * (P3 - P2);
            return d.sqrMagnitude > 1e-6f ? d.normalized : (P3 - P0).normalized;
        }

        /// <summary>Free length at the lane start: up to the rear of the last vehicle, minus reservations.</summary>
        public float FreeSpaceAtStart()
        {
            var free = Length;
            for (var i = 0; i < Agents.Count; i++)
            {
                var rear = Agents[i].S - Agents[i].HalfLength;
                if (rear < free)
                {
                    free = rear;
                }
            }

            return free - IncomingReserved;
        }

        private Vector3 EvaluateT(float t)
        {
            var u = 1f - t;
            return u * u * u * P0 + 3f * u * u * t * P1 + 3f * u * t * t * P2 + t * t * t * P3;
        }

        private float DistanceToT(float s)
        {
            if (s <= 0f)
            {
                return 0f;
            }

            if (s >= Length)
            {
                return 1f;
            }

            for (var i = 1; i <= LutSamples; i++)
            {
                if (_lut[i] < s)
                {
                    continue;
                }

                var segment = _lut[i] - _lut[i - 1];
                var f = segment > 1e-5f ? (s - _lut[i - 1]) / segment : 0f;
                return (i - 1 + f) / LutSamples;
            }

            return 1f;
        }

        private void BuildLut()
        {
            var prev = P0;
            _lut[0] = 0f;
            for (var i = 1; i <= LutSamples; i++)
            {
                var p = EvaluateT((float)i / LutSamples);
                _lut[i] = _lut[i - 1] + Vector3.Distance(prev, p);
                prev = p;
            }

            Length = _lut[LutSamples];
        }
    }
}
