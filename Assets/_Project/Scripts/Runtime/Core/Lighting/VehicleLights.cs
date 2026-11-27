using _Project.Vehicles;
using UnityEngine;

namespace _Project.Lighting
{
    /// <summary>Headlights and tail lights: on while somebody drives the car, off when it is parked or wrecked.</summary>
    public sealed class VehicleLights : MonoBehaviour
    {
        [SerializeField] private Vehicle vehicle;
        [SerializeField] private Light beam;
        [SerializeField] private Renderer[] lamps;

        private bool _on = true;

        public void Setup(Vehicle owner, Light beamLight, Renderer[] lampRenderers)
        {
            vehicle = owner;
            beam = beamLight;
            lamps = lampRenderers;
        }

        private void Update()
        {
            var on = vehicle.Mode != VehicleMode.Parked;
            if (on == _on)
            {
                return;
            }

            _on = on;
            beam.enabled = on;
            for (var i = 0; i < lamps.Length; i++)
            {
                lamps[i].enabled = on;
            }
        }
    }
}
