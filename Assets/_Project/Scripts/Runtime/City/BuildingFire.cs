using UnityEngine;

namespace _Project.City
{
    /// <summary>
    /// A burning building: flickering fire light and soot-darkened walls. The flames and smoke are
    /// particle systems placed around the building by the city builder.
    /// </summary>
    public sealed class BuildingFire : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Light fireLight;
        [SerializeField] private Renderer[] charredRenderers;
        [SerializeField] private Color sootTint = new Color(0.42f, 0.36f, 0.33f);
        [SerializeField] private float flickerSpeed = 7f;
        [SerializeField] private float flickerAmount = 0.35f;

        private float _baseIntensity;
        private float _seed;

        public void Setup(Light light, Renderer[] renderers)
        {
            fireLight = light;
            charredRenderers = renderers;
        }

        private void Awake()
        {
            _baseIntensity = fireLight.intensity;
            _seed = Random.value * 100f;

            var block = new MaterialPropertyBlock();
            foreach (var r in charredRenderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColorId, sootTint);
                r.SetPropertyBlock(block);
            }
        }

        private void Update()
        {
            var t = Time.time * flickerSpeed + _seed;
            // Two noise octaves: slow breathing plus fast crackle.
            var noise = Mathf.PerlinNoise(t, _seed) * 0.7f + Mathf.PerlinNoise(t * 3.1f, _seed + 17f) * 0.3f;
            fireLight.intensity = _baseIntensity * (1f - flickerAmount + 2f * flickerAmount * noise);
        }
    }
}
