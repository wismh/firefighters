using _Project.Vehicles;
using UnityEngine;

namespace _Project.Comedy
{
    /// <summary>Red and blue beacons on the fire truck's cab that flash in turn while someone drives it.</summary>
    public sealed class SirenLights : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Vehicle vehicle;
        [SerializeField] private Light redLight;
        [SerializeField] private Light blueLight;
        [SerializeField] private Renderer redLamp;
        [SerializeField] private Renderer blueLamp;
        [Tooltip("Full red-blue cycles per second.")]
        [SerializeField] private float rate = 2.5f;
        [SerializeField] private Color red = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private Color blue = new Color(0.15f, 0.35f, 1f);
        [SerializeField] private float dim = 0.25f;

        private MaterialPropertyBlock _block;
        private bool _wasOn = true;

        public void Setup(Vehicle owner, Light redSource, Light blueSource, Renderer redRenderer, Renderer blueRenderer)
        {
            vehicle = owner;
            redLight = redSource;
            blueLight = blueSource;
            redLamp = redRenderer;
            blueLamp = blueRenderer;
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            var on = vehicle.Mode == VehicleMode.Driven;
            if (!on && !_wasOn)
            {
                return;
            }

            _wasOn = on;
            var phase = Mathf.Repeat(Time.time * rate, 1f);
            var redOn = on && phase < 0.5f;
            var blueOn = on && phase >= 0.5f;
            redLight.enabled = redOn;
            blueLight.enabled = blueOn;
            Paint(redLamp, redOn ? red : red * dim);
            Paint(blueLamp, blueOn ? blue : blue * dim);
        }

        private void Paint(Renderer lamp, Color color)
        {
            color.a = 1f;
            _block.SetColor(BaseColorId, color);
            lamp.SetPropertyBlock(_block);
        }
    }
}
