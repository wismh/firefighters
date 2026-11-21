using UnityEngine;
using UnityEngine.Audio;

namespace MyToolkit.Audio
{
    /// <summary>Holds master / music / SFX volume and pushes it to the mixer as decibels.</summary>
    public sealed class MixerVolumeController
    {
        private const float MinLinearVolume = 0.0001f;
        private const float SilentDecibels = -80f;

        private readonly AudioMixer _mixer;
        private readonly AudioMixerVolumeParameters _parameters;

        public MixerVolumeController(AudioMixer mixer, AudioMixerVolumeParameters parameters)
        {
            _mixer = mixer;
            _parameters = parameters;
        }

        public float Master { get; private set; } = 1f;

        public float Music { get; private set; } = 1f;

        public float Sfx { get; private set; } = 1f;

        public void ApplyAll()
        {
            Apply(_parameters.MasterVolume, Master);
            Apply(_parameters.MusicVolume, Music);
            Apply(_parameters.SfxVolume, Sfx);
        }

        public void SetMaster(float volume)
        {
            Master = Mathf.Clamp01(volume);
            Apply(_parameters.MasterVolume, Master);
        }

        public void SetMusic(float volume)
        {
            Music = Mathf.Clamp01(volume);
            Apply(_parameters.MusicVolume, Music);
        }

        public void SetSfx(float volume)
        {
            Sfx = Mathf.Clamp01(volume);
            Apply(_parameters.SfxVolume, Sfx);
        }

        public static float LinearToDecibels(float linearVolume) =>
            linearVolume > MinLinearVolume
                ? Mathf.Log10(linearVolume) * 20f
                : SilentDecibels;

        private void Apply(string parameterName, float linearVolume)
        {
            if (string.IsNullOrEmpty(parameterName))
                return;

            _mixer.SetFloat(parameterName, LinearToDecibels(linearVolume));
        }
    }
}
