using UnityEngine;

namespace _Project.Lighting
{
    /// <summary>Keeps real lights only in the lamps near the camera; farther ones fade to a glowing bulb.</summary>
    public sealed class StreetLampSystem : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private StreetLamp[] lamps;
        [SerializeField] private float fullDistance = 75f;
        [SerializeField] private float cutDistance = 105f;

        public void Setup(Camera view, StreetLamp[] streetLamps)
        {
            viewCamera = view;
            lamps = streetLamps;
        }

        private void Update()
        {
            var eye = viewCamera.transform.position;
            for (var i = 0; i < lamps.Length; i++)
            {
                var distance = Vector3.Distance(lamps[i].Position, eye);
                lamps[i].Show(Mathf.InverseLerp(cutDistance, fullDistance, distance));
            }
        }
    }
}
