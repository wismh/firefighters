using System.Collections.Generic;
using _Project.City;
using UnityEngine;

namespace _Project.Mission
{
    /// <summary>
    /// Screen markers over the fires of the current call. A fire in view gets a pulsing flame over its roof with the
    /// distance; one out of view (behind, to the side) is pinned to the screen edge with an arrow towards it. Close
    /// up a marker fades out: the flames and their own markers take over. A put-out fire's marker fades away.
    /// </summary>
    // After the cameras have moved for the frame (VehicleCamera follows in LateUpdate).
    [DefaultExecutionOrder(100)]
    public sealed class TargetIndicator : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [Tooltip("The full-screen rect markers live in.")]
        [SerializeField] private RectTransform area;
        [SerializeField] private TargetMarker template;
        [SerializeField] private int poolSize = 3;
        [Tooltip("Metres above the roof the marker floats.")]
        [SerializeField] private float heightAbove = 5f;
        [Tooltip("Pixels between a pinned marker and the screen edge.")]
        [SerializeField] private float edgeInset = 64f;
        [Tooltip("Closer than this, metres, the marker is gone...")]
        [SerializeField] private float hideDistance = 28f;
        [Tooltip("...and from this far it is fully shown.")]
        [SerializeField] private float showDistance = 45f;
        [SerializeField] private float fadeSpeed = 3f;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseAmount = 0.12f;

        private readonly List<TargetMarker> _markers = new List<TargetMarker>();
        private readonly List<BuildingFire> _targets = new List<BuildingFire>();

        /// <summary>Shows markers over these fires while they burn.</summary>
        public void SetTargets(IReadOnlyList<BuildingFire> fires)
        {
            _targets.Clear();
            for (var i = 0; i < fires.Count && i < _markers.Count; i++)
            {
                _targets.Add(fires[i]);
                _markers[i].Alpha = 0f;
                _markers[i].SetVisible(true);
            }
        }

        public void Clear()
        {
            _targets.Clear();
            foreach (var marker in _markers)
            {
                marker.SetVisible(false);
            }
        }

        private void Awake()
        {
            for (var i = 0; i < poolSize; i++)
            {
                var marker = Instantiate(template, template.transform.parent);
                marker.name = "Target_" + i;
                marker.SetVisible(false);
                _markers.Add(marker);
            }

            template.SetVisible(false);
        }

        private void LateUpdate()
        {
            var dt = Time.unscaledDeltaTime;
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;
            for (var i = 0; i < _targets.Count; i++)
            {
                UpdateMarker(_markers[i], _targets[i], pulse, dt);
            }
        }

        private void UpdateMarker(TargetMarker marker, BuildingFire fire, float pulse, float dt)
        {
            var world = fire.Top + Vector3.up * heightAbove;
            var eye = viewCamera.transform.position;
            var flat = world - eye;
            flat.y = 0f;
            var metres = flat.magnitude;

            var target = fire.IsBurning ? Mathf.InverseLerp(hideDistance, showDistance, metres) : 0f;
            marker.Alpha = Mathf.MoveTowards(marker.Alpha, target, fadeSpeed * dt);

            var screen = viewCamera.WorldToScreenPoint(world);
            var behind = screen.z < 0f;
            if (behind)
            {
                // Behind the camera the projection is mirrored through the screen centre.
                screen.x = Screen.width - screen.x;
                screen.y = Screen.height - screen.y;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            var rect = area.rect;
            var half = new Vector2(rect.width * 0.5f - edgeInset, rect.height * 0.5f - edgeInset);
            var offset = local - rect.center;
            var inside = !behind && Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y;
            if (inside)
            {
                marker.Place(local, false, Vector2.down, metres, pulse);
                return;
            }

            // Behind the camera and straight back: the arrow points down, towards turning around.
            var direction = offset.sqrMagnitude > 1f ? offset.normalized : Vector2.down;
            if (behind && direction.y > 0f)
            {
                direction.y = -direction.y;
            }

            var t = Mathf.Min(half.x / Mathf.Max(Mathf.Abs(direction.x), 1e-4f),
                half.y / Mathf.Max(Mathf.Abs(direction.y), 1e-4f));
            marker.Place(rect.center + direction * t, true, direction, metres, pulse * 0.85f);
        }
    }
}
