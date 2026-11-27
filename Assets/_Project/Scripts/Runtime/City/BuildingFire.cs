using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace _Project.City
{
    /// <summary>
    /// A burning building: its flames, smoke, embers, one flickering fire light and charred walls.
    /// Every flame has a strength that water lowers around the point it lands on; a doused flame stays as weak
    /// as the water left it. A put-out flame catches again from a strong neighbour that nobody has touched, so
    /// a building has to be put out completely. Then the smoke turns pale, thins out and stops; the char stays.
    /// A building starts dormant, with clean walls and nothing burning, until the mission calls <see cref="Ignite"/>;
    /// then its flames grow from small to full over a few seconds.
    /// </summary>
    public sealed class BuildingFire : MonoBehaviour
    {
        // Array size in CharredBuilding.shader.
        private const int MaxShaderFlames = 32;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlamesId = Shader.PropertyToID("_Flames");
        private static readonly int FlameCountId = Shader.PropertyToID("_FlameCount");
        private static readonly int CharGrowId = Shader.PropertyToID("_CharGrow");

        [SerializeField] private Light fireLight;
        [SerializeField] private Renderer[] charredRenderers;
        [SerializeField] private FlameSpot[] flames;
        [SerializeField] private ParticleSystem[] smoke;
        [SerializeField] private ParticleSystem[] embers;
        [Tooltip("World height of the building's base; set by the builder.")]
        [SerializeField] private float baseY;
        [Tooltip("Charred Building material; each burning building gets its own copy with its own texture.")]
        [SerializeField] private Material charredMaterial;
        [Tooltip("How fast the embers in the walls heat up and cool down after their flame, per second.")]
        [SerializeField] private float emberRate = 0.3f;
        [SerializeField] private float flickerSpeed = 7f;
        [SerializeField] private float flickerAmount = 0.35f;

        [Header("Water")]
        [Tooltip("Flames within this distance of the point the water lands on lose strength.")]
        [SerializeField] private float douseRadius = 3.5f;
        [Tooltip("Strength per second taken from a flame right where the water lands.")]
        [SerializeField] private float douseRate = 1f;
        [Tooltip("How far behind the wet surface a flame still gets water (under a porch, eaves, a window), metres.")]
        [SerializeField] private float douseDepth = 2.5f;
        [Tooltip("A roof flame gets water from any wet spot of the building within this horizontal distance...")]
        [SerializeField] private float roofRadius = 7f;
        [Tooltip("...if the spot is at least this high above the building's base (the upper wall, not the ground).")]
        [SerializeField] private float roofMinHeight = 2.5f;
        [Tooltip("The smallest a flame shrinks to before it goes out, as a part of its full size. Kept large " +
                 "enough that a weak flame by a wall is still easy to spot.")]
        [SerializeField] private float minVisualScale = 0.55f;

        [Header("Leftovers")]
        [Tooltip("Below this average strength, with no flame stronger than finishFlameStrength, the rest dies out alone.")]
        [SerializeField] private float finishStrength = 0.15f;
        [SerializeField] private float finishFlameStrength = 0.3f;
        [Tooltip("Strength per second the leftover flames lose while dying out on their own.")]
        [SerializeField] private float finishRate = 0.1f;
        [Tooltip("With this many flames or fewer still burning, each gets a marker so the player can find it.")]
        [SerializeField] private int markLastFlames = 3;

        [Header("Fighting back")]
        [Tooltip("A put-out flame can catch again only after it has been dry this long.")]
        [SerializeField] private float reigniteDryTime = 5f;
        [SerializeField] private float reigniteRadius = 4f;
        [SerializeField] private float reigniteNeighbourStrength = 0.8f;
        [SerializeField] private Vector2 reigniteDelay = new Vector2(10f, 16f);
        [SerializeField] private float reigniteStrength = 0.3f;

        [Header("Heat")]
        [Tooltip("A player this close to a flame, metres, starts to burn.")]
        [SerializeField] private float flameHeatRadius = 2.6f;
        [SerializeField] private float flameHeatHeight = 3.5f;

        [Header("Ignition")]
        [Tooltip("Seconds the flames take to grow to full strength after the building catches fire.")]
        [SerializeField] private float igniteTime = 4f;
        [Tooltip("Strength every flame starts at when the building catches fire.")]
        [SerializeField] private float igniteStrength = 0.2f;
        [Tooltip("Seconds the char takes to spread over the walls around the flames.")]
        [SerializeField] private float charSpreadTime = 12f;

        [Header("Burn-out")]
        [SerializeField] private Color steamColor = new Color(0.86f, 0.86f, 0.84f, 0.6f);
        [SerializeField] private float steamTime = 4f;

        private readonly List<int> _neighbours = new List<int>(16);
        private readonly List<Material> _charred = new List<Material>(4);
        private readonly Vector4[] _shaderFlames = new Vector4[MaxShaderFlames];
        private int _shaderFlameCount;
        private float[] _smokeRates;
        private float[] _emberRates;
        private float _baseIntensity;
        private float _seed;
        private float _outTime;
        private float _growUntil;
        private float _charGrow;

        /// <summary>Raised once, when the dormant building catches fire.</summary>
        public event Action<BuildingFire> Ignited;

        /// <summary>Raised once, when the last flame goes out.</summary>
        public event Action<BuildingFire> Extinguished;

        /// <summary>Raised when a put-out flame catches again from a neighbour.</summary>
        public event Action<BuildingFire> Reignited;

        /// <summary>Not burning yet: waits for <see cref="Ignite"/>.</summary>
        public bool IsDormant { get; private set; } = true;

        public bool IsBurning { get; private set; }

        /// <summary>Average strength of the flames, 0..1.</summary>
        public float Strength { get; private set; }

        /// <summary>Flames still burning.</summary>
        public int BurningFlames { get; private set; }

        /// <summary>Top centre of the building itself, for markers.</summary>
        public Vector3 Top { get; private set; }

        /// <summary>The building's bounds grown by the douse radius: water landing outside cannot reach a flame.</summary>
        public Bounds Area { get; private set; }

        public void Setup(Light light, Renderer[] renderers, FlameSpot[] flameSpots, ParticleSystem[] smokeSystems,
            ParticleSystem[] emberSystems, Material charred)
        {
            charredMaterial = charred;
            fireLight = light;
            charredRenderers = renderers;
            flames = flameSpots;
            smoke = smokeSystems;
            embers = emberSystems;
            baseY = float.MaxValue;
            foreach (var r in renderers)
            {
                baseY = Mathf.Min(baseY, r.bounds.min.y);
            }
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
                var reach = Reach(flame, point, normal);
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

        /// <summary>How deep in the flames a point is, 0..1.</summary>
        public float HeatAt(Vector3 point)
        {
            if (!IsBurning || !Area.Contains(point))
            {
                return 0f;
            }

            var heat = 0f;
            for (var i = 0; i < flames.Length; i++)
            {
                var offset = point - flames[i].root.position;
                var height = offset.y;
                offset.y = 0f;
                if (height < -flameHeatHeight * 0.4f || height > flameHeatHeight)
                {
                    continue;
                }

                var near = Mathf.InverseLerp(flameHeatRadius, flameHeatRadius * 0.45f, offset.magnitude);
                heat = Mathf.Max(heat, near * Mathf.Sqrt(flames[i].Strength));
            }

            return heat;
        }

        /// <summary>
        /// How much of the water landing at <paramref name="point"/> reaches a flame, 0..1. In front of the wet
        /// surface it is the plain distance; behind it (a flame under a porch or eaves) the water soaks in up to
        /// douseDepth, counted along the surface plus half the depth. The far side of a wall gets nothing.
        /// </summary>
        public float Reach(FlameSpot flame, Vector3 point, Vector3 normal)
        {
            var reach = SurfaceReach(flame.root.position, point, normal);
            if (!flame.onRoof || point.y - baseY < roofMinHeight)
            {
                return reach;
            }

            // The roof is hard to see from the street, so water on the upper wall below a roof flame counts too.
            var flat = flame.root.position - point;
            flat.y = 0f;
            var t = flat.magnitude / roofRadius;
            return t >= 1f ? reach : Mathf.Max(reach, 1f - t * t);
        }

        private float SurfaceReach(Vector3 flame, Vector3 point, Vector3 normal)
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

            var area = charredRenderers[0].bounds;
            foreach (var r in charredRenderers)
            {
                area.Encapsulate(r.bounds);
                CharMaterials(r);
            }

            Top = new Vector3(area.center.x, area.max.y, area.center.z);
            area.Expand(douseRadius * 2f);
            Area = area;

            _smokeRates = BaseRates(smoke);
            _emberRates = BaseRates(embers);
            foreach (var flame in flames)
            {
                flame.BaseRates = BaseRates(flame.systems);
                // Dormant: nothing burns until Ignite.
                flame.Strength = 1f;
                SetStrength(flame, 0f);
                Clear(flame.systems);
            }

            Clear(smoke);
            Clear(embers);
            fireLight.enabled = false;

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

            _shaderFlameCount = Mathf.Min(flames.Length, MaxShaderFlames);
            for (var i = 0; i < _shaderFlameCount; i++)
            {
                var position = flames[i].root.position;
                _shaderFlames[i] = new Vector4(position.x, position.y, position.z, 0f);
            }

            PushShaderFlames();
        }

        /// <summary>Sets the dormant building on fire; its flames grow to full strength over igniteTime.</summary>
        public void Ignite()
        {
            if (!IsDormant)
            {
                return;
            }

            IsDormant = false;
            IsBurning = true;
            _growUntil = Time.time + igniteTime;
            foreach (var flame in flames)
            {
                SetStrength(flame, igniteStrength);
            }

            Play(smoke);
            Play(embers);
            fireLight.enabled = true;
            Ignited?.Invoke(this);
        }

        private void OnDestroy()
        {
            foreach (var material in _charred)
            {
                Destroy(material);
            }
        }

        /// <summary>Gives the renderer charred copies of its materials, each with the texture of the one it replaces.</summary>
        private void CharMaterials(Renderer target)
        {
            var materials = target.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var charred = new Material(charredMaterial);
                charred.SetTexture(BaseMapId, materials[i].GetTexture(BaseMapId));
                charred.SetColor(BaseColorId, materials[i].GetColor(BaseColorId));
                materials[i] = charred;
                _charred.Add(charred);
            }

            target.sharedMaterials = materials;
        }

        /// <summary>The embers in the walls follow their flames with a delay.</summary>
        private void UpdateEmbers(float dt)
        {
            for (var i = 0; i < _shaderFlameCount; i++)
            {
                _shaderFlames[i].w = Mathf.MoveTowards(_shaderFlames[i].w, flames[i].Strength, emberRate * dt);
            }

            if (IsBurning)
            {
                _charGrow = Mathf.Min(1f, _charGrow + dt / charSpreadTime);
            }

            PushShaderFlames();
        }

        private void PushShaderFlames()
        {
            foreach (var material in _charred)
            {
                material.SetVectorArray(FlamesId, _shaderFlames);
                material.SetFloat(FlameCountId, _shaderFlameCount);
                material.SetFloat(CharGrowId, _charGrow);
            }
        }

        private void Update()
        {
            if (IsDormant)
            {
                return;
            }

            UpdateEmbers(Time.deltaTime);
            if (!IsBurning)
            {
                UpdateBurnOut();
                return;
            }

            var now = Time.time;
            var total = 0f;
            var strongest = 0f;
            var burning = 0;
            foreach (var flame in flames)
            {
                total += flame.Strength;
                strongest = Mathf.Max(strongest, flame.Strength);
                burning += flame.Strength > 0f ? 1 : 0;
            }

            // The last scraps are not worth hunting for: once only weak flames are left, they go out by themselves.
            var finishing = total / flames.Length < finishStrength && strongest <= finishFlameStrength;
            BurningFlames = burning;
            var mark = burning <= markLastFlames;
            var dt = Time.deltaTime;
            var growing = now < _growUntil;
            total = 0f;
            foreach (var flame in flames)
            {
                // While catching fire a flame grows, unless the water is on it right now.
                var dry = now - flame.LastWetTime > 0.5f;
                if (growing && dry && flame.Strength > 0f)
                {
                    SetStrength(flame, flame.Strength + (1f - igniteStrength) / igniteTime * dt);
                }
                else if (flame.Strength > 0f && finishing)
                {
                    SetStrength(flame, flame.Strength - finishRate * dt);
                }
                else if (flame.Strength <= 0f && !finishing)
                {
                    UpdateReignite(flame, now);
                }

                flame.marker.SetActive(mark && flame.Strength > 0f);
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
                Reignited?.Invoke(this);
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
            BurningFlames = 0;
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

        private static void Clear(ParticleSystem[] systems)
        {
            foreach (var system in systems)
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static void Play(ParticleSystem[] systems)
        {
            foreach (var system in systems)
            {
                system.Play(false);
            }
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
