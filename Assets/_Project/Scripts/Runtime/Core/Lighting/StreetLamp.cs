using _Project.City;
using UnityEngine;

namespace _Project.Lighting
{
    /// <summary>The lit head of a street lamp. Knocked over, it flickers for a moment and dies.</summary>
    public sealed class StreetLamp : MonoBehaviour
    {
        private const float DyingSeconds = 1.8f;
        private const float FlickerRate = 17f;

        [SerializeField] private Light source;
        [SerializeField] private Renderer bulb;

        private KnockableProp _prop;
        private float _intensity;
        private float _diesAt = float.PositiveInfinity;

        public Vector3 Position => source.transform.position;

        public void Setup(Light lampLight, Renderer lampBulb)
        {
            source = lampLight;
            bulb = lampBulb;
        }

        /// <summary>Shows the lamp at a share of its brightness, 0..1. The bulb glows at any distance.</summary>
        public void Show(float share)
        {
            if (float.IsPositiveInfinity(_diesAt))
            {
                source.enabled = share > 0f;
                source.intensity = _intensity * share;
                return;
            }

            var alive = Time.time < _diesAt;
            var on = alive && Mathf.PerlinNoise(Time.time * FlickerRate, _diesAt) > 0.45f;
            source.enabled = on && share > 0f;
            source.intensity = _intensity * share;
            bulb.enabled = on;
        }

        private void Awake()
        {
            _intensity = source.intensity;
            _prop = GetComponentInParent<KnockableProp>();
            _prop.Knocked += OnKnocked;
        }

        private void OnDestroy()
        {
            _prop.Knocked -= OnKnocked;
        }

        private void OnKnocked(KnockableProp prop)
        {
            _diesAt = Time.time + DyingSeconds;
        }
    }
}
