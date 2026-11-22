using System.Collections.Generic;
using _Project.City;
using _Project.FireHose;
using _Project.Player;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace _Project.Minimap
{
    /// <summary>
    /// The round minimap in the HUD. The baked city picture turns and slides under a fixed player arrow, so up on
    /// the map is always where the player looks. Burning buildings get a pulsing flame; one outside the circle is
    /// pinned to its edge with the distance. A put-out fire turns into white steam and fades away. On foot the
    /// player's fire truck is shown too, since the hose has to go back on its reel.
    /// </summary>
    // After the cameras have moved for the frame (VehicleCamera follows in LateUpdate).
    [DefaultExecutionOrder(100)]
    public sealed class MinimapView : MonoBehaviour
    {
        [SerializeField] private MinimapData data;
        [SerializeField] private PlayerModeController player;
        [SerializeField] private RectTransform circle;
        [SerializeField] private RectTransform mapPivot;
        [SerializeField] private RawImage map;
        [SerializeField] private RectTransform icons;
        [SerializeField] private RectTransform north;
        [SerializeField] private MinimapMarker fireTemplate;
        [SerializeField] private MinimapMarker truckMarker;

        [Header("Zoom")]
        [Tooltip("Metres from the centre to the edge of the circle on foot.")]
        [SerializeField] private float walkRange = 80f;
        [Tooltip("Metres from the centre to the edge at full driving speed.")]
        [SerializeField] private float driveRange = 140f;
        [SerializeField] private float fullSpeed = 18f;
        [SerializeField] private float zoomSharpness = 3f;

        [Header("Icons")]
        [Tooltip("Pixels between a pinned icon's centre and the edge of the circle.")]
        [SerializeField] private float edgeInset = 18f;
        [SerializeField] private float pulseSpeed = 4f;
        [SerializeField] private float pulseAmount = 0.18f;
        [Tooltip("Shown in place of the flame for a moment after the building is put out.")]
        [SerializeField] private Sprite steamSprite;
        [SerializeField] private float steamTime = 1f;

        private readonly List<FireMarker> _fires = new List<FireMarker>();

        private FireService _fireService;
        private float _range;

        [Inject]
        public void Construct(FireService fires)
        {
            _fireService = fires;
        }

        private void Start()
        {
            map.texture = data.Map;
            _range = walkRange;
            foreach (var fire in _fireService.Fires)
            {
                var marker = Instantiate(fireTemplate, icons);
                marker.name = "Fire_" + fire.transform.parent.name;
                _fires.Add(new FireMarker(fire, marker));
                fire.Extinguished += OnExtinguished;
            }

            fireTemplate.SetVisible(false);
        }

        private void OnDestroy()
        {
            foreach (var fire in _fires)
            {
                fire.Fire.Extinguished -= OnExtinguished;
            }
        }

        private void OnExtinguished(BuildingFire fire)
        {
            foreach (var marker in _fires)
            {
                if (marker.Fire == fire)
                {
                    marker.OutTime = Time.time;
                    marker.Marker.Icon.sprite = steamSprite;
                }
            }
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            var speed01 = Mathf.Clamp01(player.DrivingSpeed / fullSpeed);
            var targetRange = player.IsDriving ? Mathf.Lerp(walkRange * 1.25f, driveRange, speed01) : walkRange;
            _range = Mathf.Lerp(_range, targetRange, 1f - Mathf.Exp(-zoomSharpness * dt));

            var radius = circle.rect.width * 0.5f;
            var scale = radius / _range;
            var focus = player.MapFocus.position;
            var heading = player.Heading;

            mapPivot.localRotation = Quaternion.Euler(0f, 0f, heading);
            var center = data.WorldCenter;
            map.rectTransform.sizeDelta = Vector2.one * (data.WorldSize * scale);
            map.rectTransform.anchoredPosition = new Vector2(center.x - focus.x, center.y - focus.z) * scale;

            var rotation = Quaternion.Euler(0f, 0f, heading);
            north.anchoredPosition = rotation * Vector2.up * radius;

            var pulse = Mathf.Sin(Time.time * pulseSpeed);
            foreach (var fire in _fires)
            {
                UpdateFire(fire, focus, rotation, scale, radius, pulse);
            }

            UpdateTruck(focus, rotation, scale, radius, heading);
        }

        private void UpdateFire(FireMarker fire, Vector3 focus, Quaternion rotation, float scale, float radius,
            float pulse)
        {
            var building = fire.Fire;
            if (!building.IsBurning && Time.time - fire.OutTime > steamTime)
            {
                fire.Marker.SetVisible(false);
                return;
            }

            var pinned = Place(fire.Marker, building.Area.center, focus, rotation, scale, radius);
            if (building.IsBurning)
            {
                // A weaker fire pulses less and pales, so the nearly-done building reads at a glance.
                var strength = building.Strength;
                var color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 1f, strength));
                fire.Marker.SetLook(1f + pulse * pulseAmount * strength, color, pinned);
                return;
            }

            var t = (Time.time - fire.OutTime) / steamTime;
            fire.Marker.SetLook(1f + t * 0.6f, new Color(1f, 1f, 1f, 1f - t), pinned);
        }

        private void UpdateTruck(Vector3 focus, Quaternion rotation, float scale, float radius, float heading)
        {
            var truck = player.FireTruck;
            var show = truck != null && !player.IsDriving;
            truckMarker.SetVisible(show);
            if (!show)
            {
                return;
            }

            var pinned = Place(truckMarker, truck.transform.position, focus, rotation, scale, radius);
            truckMarker.SetLook(1f, Color.white, pinned);
            // The icon points along the truck as the map shows it: its yaw relative to where the player looks.
            truckMarker.SetIconRotation(heading - truck.transform.eulerAngles.y);
        }

        /// <summary>Puts the marker where <paramref name="world"/> falls on the map; true when pinned to the edge.</summary>
        private bool Place(MinimapMarker marker, Vector3 world, Vector3 focus, Quaternion rotation, float scale,
            float radius)
        {
            marker.SetVisible(true);
            var offset = new Vector2(world.x - focus.x, world.z - focus.z);
            var position = (Vector2)(rotation * offset) * scale;
            var limit = radius - edgeInset;
            var pinned = position.sqrMagnitude > limit * limit;
            if (pinned)
            {
                position = position.normalized * limit;
            }

            marker.Place(position, pinned, offset.magnitude);
            return pinned;
        }

        private sealed class FireMarker
        {
            public readonly BuildingFire Fire;
            public readonly MinimapMarker Marker;
            public float OutTime;

            public FireMarker(BuildingFire fire, MinimapMarker marker)
            {
                Fire = fire;
                Marker = marker;
            }
        }
    }
}
