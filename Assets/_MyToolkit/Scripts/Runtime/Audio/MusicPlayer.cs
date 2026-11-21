using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

namespace MyToolkit.Audio
{
    /// <summary>Two-source music player with crossfade. Plain class owned by <see cref="AudioSystem"/>.</summary>
    public sealed class MusicPlayer
    {
        private readonly AudioSource[] _sources = new AudioSource[2];
        private readonly float _defaultCrossfade;
        private int _activeIndex;

        public MusicPlayer(Transform root, AudioMixerGroup group, float defaultCrossfade)
        {
            _defaultCrossfade = defaultCrossfade;

            for (int i = 0; i < _sources.Length; i++)
                _sources[i] = CreateSource(root, group, i);
        }

        public bool IsPlaying => _sources[_activeIndex].isPlaying;

        public SoundData Current { get; private set; }

        public void Play(SoundData sound, bool loop) =>
            Play(sound, _defaultCrossfade, loop);

        public void Play(SoundData sound, float crossfadeDuration, bool loop)
        {
            if (sound == null || sound.Clip == null)
                return;

            if (!IsPlaying || crossfadeDuration <= 0f)
            {
                PlayImmediate(sound, loop);
                return;
            }

            Crossfade(sound, crossfadeDuration, loop);
        }

        public void Crossfade(SoundData sound, float duration, bool loop)
        {
            if (sound == null || sound.Clip == null)
                return;

            if (duration <= 0f || !IsPlaying)
            {
                PlayImmediate(sound, loop);
                return;
            }

            int nextIndex = 1 - _activeIndex;
            AudioSource current = _sources[_activeIndex];
            AudioSource next = _sources[nextIndex];

            DOTween.Kill(current);
            DOTween.Kill(next);

            Configure(next, sound, loop);
            next.volume = 0f;
            next.Play();

            current.DOFade(0f, duration).SetUpdate(true).OnComplete(() => current.Stop());
            next.DOFade(sound.Volume, duration).SetUpdate(true);

            _activeIndex = nextIndex;
            Current = sound;
        }

        public void Stop(float fadeOutDuration)
        {
            AudioSource active = _sources[_activeIndex];
            DOTween.Kill(active);

            if (fadeOutDuration <= 0f)
            {
                active.Stop();
                Current = null;
                return;
            }

            active
                .DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    active.Stop();
                    Current = null;
                });
        }

        public void SetLoop(bool loop) =>
            _sources[_activeIndex].loop = loop;

        public void KillTweens()
        {
            foreach (AudioSource source in _sources)
                DOTween.Kill(source);
        }

        private void PlayImmediate(SoundData sound, bool loop)
        {
            AudioSource active = _sources[_activeIndex];
            AudioSource inactive = _sources[1 - _activeIndex];

            DOTween.Kill(active);
            DOTween.Kill(inactive);

            inactive.Stop();
            Configure(active, sound, loop);
            active.volume = sound.Volume;
            active.Play();
            Current = sound;
        }

        private static void Configure(AudioSource source, SoundData sound, bool loop)
        {
            source.clip = sound.Clip;
            source.loop = loop;
            source.pitch = Random.Range(sound.PitchRange.x, sound.PitchRange.y);
        }

        private static AudioSource CreateSource(Transform root, AudioMixerGroup group, int index)
        {
            var sourceObject = new GameObject($"MusicSource_{index}");
            sourceObject.transform.SetParent(root, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = group;
            return source;
        }
    }
}
