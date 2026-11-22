using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Project.City
{
    /// <summary>
    /// A burning building: its flames, smoke, embers, one flickering fire light and soot-darkened walls.
    /// Every flame has a strength that water lowers around the point it lands on; a doused flame stays as weak
    /// as the water left it. A put-out flame catches again from a strong neighbour that nobody has touched, so
    /// a building has to be put out completely. Then the smoke turns pale, thins out and stops; the soot stays.
    /// </summary>
    public sealed class BuildingFire : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Light fireLight;
        [SerializeField] private Renderer[] charredRenderers;
        [SerializeField] private FlameSpot[] flames;
        [SerializeField] private ParticleSystem[] smoke;
        [SerializeField] private ParticleSystem[] embers;
        [SerializeField] private Color sootTint = new Color(0.42f, 0.36f, 0.33f);
        [SerializeField] private float flickerSpeed = 7f;
        [SerializeField] private float flickerAmount = 0.35f;

        [Header("Water")]
        [Tooltip("Flames within this distance of the point the water lands on lose strength.")]
        [SerializeField] private float douseRadius = 3.5f;
        [Tooltip("Strength per second taken from a flame right where the water lands.")]
        [SerializeField] private float douseRate = 1f;
        [Tooltip("How far behind the wet surface a flame still gets water (under a porch, eaves, a window), metres.")]
        [SerializeField] private float douseDepth = 2.5f;
        [Tooltip("The smallest a flame shrinks to before it goes out, as a part of its full size.")]
        [SerializeField] private float minVisualScale = 0.25f;

        [Header("Fighting back")]
        [Tooltip("A put-out flame can catch again only after it has been dry this long.")]
        [SerializeField] private float reigniteDryTime = 5f;
        [SerializeField] private float reigniteRadius = 4f;
        [SerializeField] private float reigniteNeighbourStrength = 0.8f;
        [SerializeField] private Vector2 reigniteDelay = new Vector2(10f, 16f);
        [SerializeField] private float reigniteStrength = 0.3f;

        [Header("Burn-out")]
        [SerializeField] private Color steamColor = new Color(0.86f, 0.86f, 0.84f, 0.6f);
        [SerializeField] private float steamTime = 4f;

        private readonly List<int> _neighbours = new List<int>(16);
        private float[] _smokeRates;
        private float[] _emberRates;
        private float _baseIntensity;
        private float _seed;
        private float _outTime;

        /// <summary>Raised once, when the last flame goes out.</summary>
        public event Action<BuildingFire> Extinguished;

        public bool IsBurning { get; private set; } = true;

        /// <summary>Average strength of the flames, 0..1.</summary>
        public float Strength { get; private set; } = 1f;

        /// <summary>The building's bounds grown by the douse radius: water landing outside cannot reach a flame.</summary>
        public Bounds Area { get; private set; }

        public void Setup(Light light, Renderer[] renderers, FlameSpot[] flameSpots, ParticleSystem[] smokeSystems,
            ParticleSystem[] emberSystems)
        {
            fireLight = light;
            charredRenderers = renderers;
            flames = flameSpots;
            smoke = smokeSystems;
            embers = emberSystems;
        }

        /// <summary>
        /// Water landed at <paramref name="point"/> on a surface facing <paramref name="normal"/> for
        /// <paramref name="dt"/> seconds. Returns true when it hit a flame that was still burning.
        /// </summary>
        public bool Douse(Vector3 point, Vector3 normal, float dt)
        {
            if (!IsBurning)
            {
                return false;
            }

            var hitFlame = false;
            var now = Time.time;
            for (var i = 0; i < flames.Length; i++)
            {
                var flame = flames[i];
                var reach = Reach(flame.root.position, point, normal);
                if (reach <= 0f)
                {
                    continue;
                }

                flame.LastWetTime = now;
                flame.ReigniteAt = 0f;
                if (flame.Strength <= 0f)
                {
                    continue;
                }

                hitFlame = true;
                SetStrength(flame, flame.Strength - douseRate * reach * dt);
            }

            return hitFlame;
        }

        /// <summary>
        /// How much of the water landing at <paramref name="point"/> reaches a flame, 0..1. In front of the wet
        /// surface it is the plain distance; behind it (a flame under a porch or eaves) the water soaks in up to
        /// douseDepth, counted along the surface plus half the depth. The far side of a wall gets nothing.
        /// </summary>
        public float Reach(Vector3 flame, Vector3 point, Vector3 normal)
        {
            var offset = flame - point;
            var depth = -Vector3.Dot(offset, normal);
            if (depth > douseDepth)
            {
                return 0f;
            }

            var distance = depth > 0f
                ? (offset + normal * depth).magnitude + depth * 0.5f
                : offset.magnitude;
            var t = distance / douseRadius;
            return t >= 1f ? 0f : 1f - t * t;
        }

        private void Awake()
        {
            _baseIntensity = fireLight.intensity;
            _seed = Random.value * 100f;

            var block = new MaterialPropertyBlock();
            var area = charredRenderers[0].bounds;
            foreach (var r in charredRenderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, sootTint);
                r.SetPropertyBlock(block);
                area.Encapsulate(r.bounds);
            }

            area.Expand(douseRadius * 2f);
            Area = area;

            _smokeRates = BaseRates(smoke);
            _emberRates = BaseRates(embers);
            foreach (var flame in flames)
            {
                flame.Strength = 1f;
                flame.BaseRates = BaseRates(flame.systems);
            }

            for (var i = 0; i < flames.Length; i++)
            {
                _neighbours.Clear();
                for (var j = 0; j < flames.Length; j++)
                {
                    var near = Vector3.Distance(flames[i].root.position, flames[j].root.position) <= reigniteRadius;
                    if (i != j && near)
                    {
                        _neighbours.Add(j);
                    }
                }

                flames[i].Neighbours = _neighbours.ToArray();
            }
        }

        private void Update()
        {
            if (!IsBurning)
            {
                UpdateBurnOut();
                return;
            }

            var now = Time.time;
            var total = 0f;
            for (var i = 0; i < flames.Length; i++)
            {
                var flame = flames[i];
                if (flame.Strength <= 0f)
                {
                    UpdateReignite(flame, now);
                }

                total += flame.Strength;
            }

            Strength = total / flames.Length;
            if (Strength <= 0f)
            {
                BurnOut();
                return;
            }

            SetRates(smoke, _smokeRates, Mathf.Lerp(0.3f, 1f, Strength));
            SetRates(embers, _emberRates, Strength);
            var t = now * flickerSpeed + _seed;
            // Two noise octaves: slow breathing plus fast crackle.
            var noise = Mathf.PerlinNoise(t, _seed) * 0.7f + Mathf.PerlinNoise(t * 3.1f, _seed + 17f) * 0.3f;
            fireLight.intensity = _baseIntensity * Mathf.Lerp(0.15f, 1f, Strength) *
                                  (1f - flickerAmount + 2f * flickerAmount * noise);
        }

        /// <summary>A put-out flame catches again after a while if a neighbour still burns strongly.</summary>
        private void UpdateReignite(FlameSpot flame, float now)
        {
            var strongNeighbour = false;
            foreach (var j in flame.Neighbours)
            {
                if (flames[j].Strength >= reigniteNeighbourStrength)
                {
                    strongNeighbour = true;
                    break;
                }
            }

            if (!strongNeighbour || now - flame.LastWetTime < reigniteDryTime)
            {
                flame.ReigniteAt = 0f;
                return;
            }

            if (flame.ReigniteAt <= 0f)
            {
                flame.ReigniteAt = now + Random.Range(reigniteDelay.x, reigniteDelay.y);
            }
            else if (now >= flame.ReigniteAt)
            {
                flame.ReigniteAt = 0f;
                SetStrength(flame, reigniteStrength);
            }
        }

        private void SetStrength(FlameSpot flame, float strength)
        {
            var wasBurning = flame.Strength > 0f;
            flame.Strength = Mathf.Clamp01(strength);
            var burning = flame.Strength > 0f;
            flame.root.localScale = Vector3.one * (flame.baseScale * Mathf.Lerp(minVisualScale, 1f, flame.Strength));
            SetRates(flame.systems, flame.BaseRates, flame.Strength);
            if (burning == wasBurning)
            {
                return;
            }

            foreach (var system in flame.systems)
            {
                if (burning)
                {
                    system.Play(false);
                }
                else
                {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        private void BurnOut()
        {
            IsBurning = false;
            Strength = 0f;
            _outTime = Time.time;
            fireLight.enabled = false;
            foreach (var system in embers)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            // The last smoke is pale steam; it thins out over steamTime and stops.
            foreach (var system in smoke)
            {
                var main = system.main;
                main.startColor = steamColor;
            }

            Extinguished?.Invoke(this);
        }

        private void UpdateBurnOut()
        {
            var fade = 1f - (Time.time - _outTime) / steamTime;
            if (fade > 0f)
            {
                SetRates(smoke, _smokeRates, 0.5f * fade);
                return;
            }

            foreach (var system in smoke)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            enabled = false;
        }

        private static float[] BaseRates(ParticleSystem[] systems)
        {
            var rates = new float[systems.Length];
            for (var i = 0; i < systems.Length; i++)
            {
                rates[i] = systems[i].emission.rateOverTimeMultiplier;
            }

            return rates;
        }

        private static void SetRates(ParticleSystem[] systems, float[] baseRates, float factor)
        {
            for (var i = 0; i < systems.Length; i++)
            {
                var emission = systems[i].emission;
                emission.rateOverTimeMultiplier = baseRates[i] * factor;
            }
        }
    }
}
