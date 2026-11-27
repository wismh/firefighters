using System.Collections.Generic;
using _Project.City;
using UnityEngine;

namespace _Project.Traffic
{
    /// <summary>Scene entry point of the lane graph: builds it from the layout and drives the signals.</summary>
    public sealed class RoadNetwork : MonoBehaviour
    {
        [SerializeField] private CityLayout layout;
        [SerializeField] private TrafficSettings settings;
        [SerializeField] private List<TrafficLightView> lightViews = new List<TrafficLightView>();
        [SerializeField] private bool drawLanes = true;

        private readonly Dictionary<Intersection, List<TrafficLightView>> _viewsByIntersection =
            new Dictionary<Intersection, List<TrafficLightView>>();

        private RoadGraph _graph;

        public RoadGraph Graph => _graph ??= RoadGraphBuilder.Build(layout, settings);
        public CityLayout Layout => layout;

        public void Setup(CityLayout cityLayout, TrafficSettings trafficSettings, List<TrafficLightView> views)
        {
            layout = cityLayout;
            settings = trafficSettings;
            lightViews = views;
            _graph = null;
        }

        public void TickSignals(float dt)
        {
            foreach (var pair in _viewsByIntersection)
            {
                if (pair.Key.Tick(dt))
                {
                    ShowSignals(pair.Key, pair.Value);
                }
            }
        }

        private void Awake()
        {
            foreach (var view in lightViews)
            {
                var intersection = Graph.Intersections[view.Cell];
                if (!_viewsByIntersection.TryGetValue(intersection, out var views))
                {
                    views = new List<TrafficLightView>(4);
                    _viewsByIntersection.Add(intersection, views);
                }

                views.Add(view);
            }

            // Signals without a view still have to run.
            foreach (var intersection in Graph.Intersections.Values)
            {
                if (intersection.HasSignal && !_viewsByIntersection.ContainsKey(intersection))
                {
                    _viewsByIntersection.Add(intersection, new List<TrafficLightView>());
                }
            }

            foreach (var pair in _viewsByIntersection)
            {
                ShowSignals(pair.Key, pair.Value);
            }
        }

        private static void ShowSignals(Intersection intersection, List<TrafficLightView> views)
        {
            for (var i = 0; i < views.Count; i++)
            {
                views[i].Show(intersection.GetSignal(views[i].Axis));
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawLanes || layout == null || settings == null)
            {
                return;
            }

            foreach (var lane in Graph.Lanes)
            {
                Gizmos.color = lane.IsConnector ? new Color(1f, 0.6f, 0f) : Color.cyan;
                var lift = Vector3.up * 0.3f;
                var prev = lane.P0 + lift;
                const int steps = 8;
                for (var i = 1; i <= steps; i++)
                {
                    var p = lane.Evaluate(lane.Length * i / steps) + lift;
                    Gizmos.DrawLine(prev, p);
                    prev = p;
                }

                if (lane.IsConnector)
                {
                    continue;
                }

                var mid = lane.Evaluate(lane.Length * 0.5f) + lift;
                var dir = lane.Tangent(lane.Length * 0.5f);
                var right = Vector3.Cross(Vector3.up, dir);
                Gizmos.DrawLine(mid, mid - dir * 1.5f + right * 0.8f);
                Gizmos.DrawLine(mid, mid - dir * 1.5f - right * 0.8f);
            }
        }
    }
}
