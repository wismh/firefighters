using UnityEngine;

namespace _Project.FireHose
{
    /// <summary>
    /// The ballistic path of the hose water, traced with short raycasts. The nozzle uses it every frame; the
    /// fire builder uses it to check that every flame can be reached from the street.
    /// </summary>
    public static class WaterArc
    {
        public const int MaxSteps = 40;
        public const float Step = 0.05f;

        /// <summary>Returns true when the water hits a collider within MaxSteps * Step seconds.</summary>
        public static bool Trace(Vector3 origin, Vector3 velocity, Vector3 gravity, out RaycastHit hit,
            out float time)
        {
            var position = origin;
            for (var i = 0; i < MaxSteps; i++)
            {
                var next = position + velocity * Step + gravity * (0.5f * Step * Step);
                velocity += gravity * Step;
                if (Physics.Linecast(position, next, out hit, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    time = (i + 1) * Step;
                    return true;
                }

                position = next;
            }

            hit = default;
            time = 0f;
            return false;
        }
    }
}
