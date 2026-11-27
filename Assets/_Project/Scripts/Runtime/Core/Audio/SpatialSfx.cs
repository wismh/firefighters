using MyToolkit.Audio;
using UnityEngine;

namespace _Project.Audio
{
    /// <summary>
    /// One-shot sounds placed in the world: <see cref="ISfxPlayer"/> plus the falloff from the listener.
    /// A sound too far to hear is not played at all.
    /// </summary>
    public sealed class SpatialSfx
    {
        private const float Audible = 0.05f;

        private readonly ISfxPlayer _sfx;
        private readonly Transform _listener;
        private readonly float _range;

        public SpatialSfx(ISfxPlayer sfx, Transform listener, float range)
        {
            _sfx = sfx;
            _listener = listener;
            _range = range;
        }

        /// <param name="everywhere">Heard at full volume wherever the listener is (the player's own voice, fanfares).</param>
        public void Play(SoundData sound, Vector3 at, float volume = 1f, bool everywhere = false)
        {
            var falloff = Falloff(at, everywhere);
            if (falloff > Audible)
            {
                _sfx.PlaySfx(sound, volume * falloff);
            }
        }

        /// <inheritdoc cref="Play(SoundData, Vector3, float, bool)"/>
        public void Play(SoundBank bank, Vector3 at, float volume = 1f, bool everywhere = false)
        {
            var falloff = Falloff(at, everywhere);
            if (falloff > Audible)
            {
                bank.Play(_sfx, volume * falloff);
            }
        }

        private float Falloff(Vector3 at, bool everywhere)
        {
            return everywhere ? 1f : SpatialAudio.Falloff(at, _listener.position, _range);
        }
    }
}
