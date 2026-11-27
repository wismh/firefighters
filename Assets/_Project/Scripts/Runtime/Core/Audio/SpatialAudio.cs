using UnityEngine;

namespace _Project.Audio
{
    /// <summary>The game's 2D attenuation: every sound is a plain SFX, quieter the farther it is from the listener.</summary>
    public static class SpatialAudio
    {
        /// <summary>Linear falloff, 1 at the listener and 0 at <paramref name="range"/> metres.</summary>
        public static float Falloff(Vector3 source, Vector3 listener, float range)
        {
            return Mathf.Clamp01(1f - Vector3.Distance(source, listener) / range);
        }
    }
}
