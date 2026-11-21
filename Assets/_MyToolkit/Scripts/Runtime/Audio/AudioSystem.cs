using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

namespace MyToolkit.Audio
{
    /// <summary>
    /// Audio facade: one-shot SFX pool and looping SFX live here, music and mixer volume are delegated to
    /// <see cref="MusicPlayer"/> and <see cref="MixerVolumeController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioSystem : MonoBehaviour, IAudioSystem
    {
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField] private AudioMixerGroup _musicGroup;
        [SerializeField] private AudioMixerVolumeParameters _volumeParameters = new();
        [SerializeField] private int _sfxPoolSize = 12;
        [SerializeField] private float _defaultMusicCrossfade = 1f;
        [SerializeField] private float _defaultLoopingSfxFade = 0.1f;

        private SfxAudioSourcePool _sfxPool;
        private LoopingSfxRegistry _loopingSfx;
        private MusicPlayer _music;
        private MixerVolumeController _volume;
        private CancellationTokenSource _lifetimeCts;
        private bool _isInitialized;

        public bool IsMusicPlaying => _isInitialized && _music.IsPlaying;
        public SoundData CurrentMusic => _isInitialized ? _music.Current : null;
        public float MasterVolume => EnsureInitializedVolume().Master;
        public float MusicVolume => EnsureInitializedVolume().Music;
        public float SfxVolume => EnsureInitializedVolume().Sfx;

        private void Awake() =>
            EnsureInitialized();

        private void LateUpdate()
        {
            if (!_isInitialized || _loopingSfx == null)
                return;

            foreach (LoopingSfxChannel channel in _loopingSfx.AllChannels)
                TickLoopingPitchRandomization(channel);
        }

        private void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;
            _lifetimeCts = new CancellationTokenSource();
            _sfxPool = new SfxAudioSourcePool(CreateChildRoot("SfxPool"), _sfxGroup, _sfxPoolSize);
            _loopingSfx = new LoopingSfxRegistry(CreateChildRoot("LoopingSfx"), _sfxGroup);
            _music = new MusicPlayer(CreateChildRoot("Music"), _musicGroup, _defaultMusicCrossfade);
            _volume = new MixerVolumeController(_mixer, _volumeParameters);
            _volume.ApplyAll();
        }

        private MixerVolumeController EnsureInitializedVolume()
        {
            EnsureInitialized();
            return _volume;
        }

        private void OnDestroy()
        {
            if (!_isInitialized)
                return;

            _lifetimeCts.Cancel();
            _lifetimeCts.Dispose();

            _music.KillTweens();

            foreach (LoopingSfxChannel channel in _loopingSfx.AllChannels)
                DOTween.Kill(channel.Source);

            _loopingSfx.DisposeAll();

            DOTween.Kill(this);
        }

        public void PlaySfx(SoundData sound, float volumeScale = 1f)
        {
            EnsureInitialized();

            if (sound == null || sound.Clip == null)
                return;

            PlaySfxInternal(sound.Clip, sound.Volume * volumeScale, sound.PitchRange);
        }

        public void PlaySfx(AudioClip clip, float volume = 1f, Vector2 pitchRange = default)
        {
            EnsureInitialized();

            if (clip == null)
                return;

            Vector2 resolvedPitchRange = pitchRange == default ? Vector2.one : pitchRange;
            PlaySfxInternal(clip, volume, resolvedPitchRange);
        }

        public LoopingSfxHandle CreateLoopingSfxHandle()
        {
            EnsureInitialized();
            return _loopingSfx.Allocate();
        }

        public void ReleaseLoopingSfxHandle(LoopingSfxHandle handle, float? fadeOutDuration = null)
        {
            EnsureInitialized();

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            float fade = ResolveLoopingFadeOut(fadeOutDuration);

            if (fade <= 0f)
            {
                StopLoopingSource(channel, 0f);
                _loopingSfx.Remove(handle);
                return;
            }

            StopLoopingSource(channel, fade, () => _loopingSfx.Remove(handle));
        }

        public void UpdateLoopingSfx(
            LoopingSfxHandle handle,
            bool shouldPlay,
            SoundData sound,
            Vector2? pitchRandomRangeOverride = null,
            float? fadeDuration = null)
        {
            EnsureInitialized();

            if (!handle.IsValid)
                return;

            if (!shouldPlay)
            {
                StopLoopingSfx(handle, fadeDuration);
                return;
            }

            if (sound == null || sound.Clip == null)
                return;

            if (!_loopingSfx.TryGet(handle, out _))
                return;

            Vector2 range = pitchRandomRangeOverride ?? sound.PitchRange;
            ConfigureAndPlayLooping(handle, sound.Clip, sound.Volume, range, randomizePitchEachLoop: true, fadeDuration);
        }

        public void PlayLoopingSfx(LoopingSfxHandle handle, SoundData sound, float? fadeInDuration = null)
        {
            if (sound == null || sound.Clip == null || !handle.IsValid)
                return;

            UpdateLoopingSfx(handle, true, sound, fadeDuration: fadeInDuration);
        }

        public void PlayLoopingSfx(LoopingSfxHandle handle, AudioClip clip, float volume = 1f, float pitch = 1f, float? fadeInDuration = null)
        {
            EnsureInitialized();

            if (clip == null || !handle.IsValid)
                return;

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            float fadeIn = ResolveLoopingFadeIn(fadeInDuration);
            AudioSource source = channel.Source;

            channel.TargetVolume = volume;
            channel.RandomizePitchEachLoop = false;
            channel.ManualPitchOverride = false;
            channel.PitchRandomRange = Vector2.one;

            bool sameClipPlaying = source.isPlaying && source.clip == clip;

            if (sameClipPlaying && Mathf.Approximately(source.pitch, pitch))
            {
                ApplyLoopingVolume(source, volume, fadeIn);
                return;
            }

            DOTween.Kill(source);

            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.loop = true;
            channel.ResetLoopTimeTracking();
            source.volume = fadeIn > 0f ? 0f : volume;
            source.Play();
            channel.LastPlaybackTime = source.time;

            ApplyLoopingVolume(source, volume, fadeIn);
        }

        public void PlayLoopingSfx(
            LoopingSfxHandle handle,
            AudioClip clip,
            float volume,
            Vector2 pitchRandomRange,
            float? fadeInDuration = null)
        {
            EnsureInitialized();

            if (clip == null || !handle.IsValid)
                return;

            if (!_loopingSfx.TryGet(handle, out _))
                return;

            ConfigureAndPlayLooping(handle, clip, volume, pitchRandomRange, randomizePitchEachLoop: true, fadeInDuration);
        }

        public void StopLoopingSfx(LoopingSfxHandle handle, float? fadeOutDuration = null)
        {
            EnsureInitialized();

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            StopLoopingSource(channel, ResolveLoopingFadeOut(fadeOutDuration));
        }

        public void StopAllLoopingSfx(float? fadeOutDuration = null)
        {
            EnsureInitialized();

            float fadeOut = ResolveLoopingFadeOut(fadeOutDuration);

            foreach (LoopingSfxChannel channel in _loopingSfx.AllChannels)
                StopLoopingSource(channel, fadeOut);
        }

        public bool IsLoopingSfxPlaying(LoopingSfxHandle handle) =>
            handle.IsValid &&
            _isInitialized &&
            _loopingSfx != null &&
            _loopingSfx.TryGet(handle, out LoopingSfxChannel channel) &&
            channel.Source.isPlaying;

        public void SetLoopingSfxVolume(LoopingSfxHandle handle, float volume, float? fadeDuration = null)
        {
            EnsureInitialized();

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            channel.TargetVolume = volume;
            ApplyLoopingVolume(channel.Source, volume, fadeDuration ?? 0f);
        }

        public void SetLoopingSfxPitch(LoopingSfxHandle handle, float pitch)
        {
            EnsureInitialized();

            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            channel.ManualPitchOverride = true;
            channel.RandomizePitchEachLoop = false;
            channel.Source.pitch = pitch;
        }

        public void PlayMusic(SoundData sound, bool loop = true)
        {
            EnsureInitialized();
            _music.Play(sound, loop);
        }

        public void PlayMusic(SoundData sound, float crossfadeDuration, bool loop = true)
        {
            EnsureInitialized();
            _music.Play(sound, crossfadeDuration, loop);
        }

        public void CrossfadeMusic(SoundData sound, float duration, bool loop = true)
        {
            EnsureInitialized();
            _music.Crossfade(sound, duration, loop);
        }

        public void StopMusic(float fadeOutDuration = 0f)
        {
            EnsureInitialized();
            _music.Stop(fadeOutDuration);
        }

        public void SetMusicLoop(bool loop)
        {
            EnsureInitialized();
            _music.SetLoop(loop);
        }

        public void SetMasterVolume(float volume) =>
            EnsureInitializedVolume().SetMaster(volume);

        public void SetMusicVolume(float volume) =>
            EnsureInitializedVolume().SetMusic(volume);

        public void SetSfxVolume(float volume) =>
            EnsureInitializedVolume().SetSfx(volume);

        private float ResolveLoopingFadeIn(float? fadeDuration) =>
            fadeDuration ?? _defaultLoopingSfxFade;

        private float ResolveLoopingFadeOut(float? fadeDuration) =>
            fadeDuration ?? _defaultLoopingSfxFade;

        private void ConfigureAndPlayLooping(
            LoopingSfxHandle handle,
            AudioClip clip,
            float volume,
            Vector2 pitchRandomRange,
            bool randomizePitchEachLoop,
            float? fadeInDuration)
        {
            if (!_loopingSfx.TryGet(handle, out LoopingSfxChannel channel))
                return;

            float fadeIn = ResolveLoopingFadeIn(fadeInDuration);
            AudioSource source = channel.Source;

            channel.TargetVolume = volume;
            channel.PitchRandomRange = pitchRandomRange;
            channel.RandomizePitchEachLoop = randomizePitchEachLoop;
            channel.ManualPitchOverride = false;

            bool sameClipPlaying = source.isPlaying && source.clip == clip;

            if (sameClipPlaying)
            {
                ApplyLoopingVolume(source, volume, fadeIn);
                return;
            }

            DOTween.Kill(source);

            source.Stop();
            source.clip = clip;
            source.loop = true;
            channel.ApplyRandomPitchFromRange();
            channel.ResetLoopTimeTracking();
            source.volume = fadeIn > 0f ? 0f : volume;
            source.Play();
            channel.LastPlaybackTime = source.time;

            ApplyLoopingVolume(source, volume, fadeIn);
        }

        private static void TickLoopingPitchRandomization(LoopingSfxChannel channel)
        {
            if (channel.ManualPitchOverride || !channel.RandomizePitchEachLoop)
                return;

            AudioSource source = channel.Source;

            if (!source.isPlaying || source.clip == null)
            {
                channel.LastPlaybackTime = -1f;
                return;
            }

            float t = source.time;

            if (channel.LastPlaybackTime >= 0f && t < channel.LastPlaybackTime - 0.02f)
                channel.ApplyRandomPitchFromRange();

            channel.LastPlaybackTime = t;
        }

        private void StopLoopingSource(LoopingSfxChannel channel, float fadeOutDuration, System.Action onFullyStopped = null)
        {
            AudioSource source = channel.Source;
            DOTween.Kill(source);

            void FinishStop()
            {
                source.Stop();
                channel.ResetLoopTimeTracking();
                channel.RandomizePitchEachLoop = false;
                channel.ManualPitchOverride = false;
                onFullyStopped?.Invoke();
            }

            if (!source.isPlaying || fadeOutDuration <= 0f)
            {
                FinishStop();
                return;
            }

            source
                .DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(FinishStop);
        }

        private static void ApplyLoopingVolume(AudioSource source, float volume, float fadeDuration)
        {
            if (fadeDuration <= 0f)
            {
                source.volume = volume;
                return;
            }

            source.DOFade(volume, fadeDuration).SetUpdate(true);
        }

        private void PlaySfxInternal(AudioClip clip, float volume, Vector2 pitchRange)
        {
            if (!_sfxPool.TryAcquire(out AudioSource source))
            {
                LoggingSystem.LogWarning("SFX pool exhausted; skipping playback.", this);
                return;
            }

            source.clip = clip;
            source.volume = volume;
            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.loop = false;
            source.Play();
            ReturnSfxSourceWhenFinished(source).Forget();
        }

        private Transform CreateChildRoot(string name)
        {
            var rootObject = new GameObject(name);
            rootObject.transform.SetParent(transform, false);
            return rootObject.transform;
        }

        private async UniTaskVoid ReturnSfxSourceWhenFinished(AudioSource source)
        {
            CancellationToken token = _lifetimeCts.Token;

            try
            {
                await UniTask.WaitWhile(() => source.isPlaying, cancellationToken: token);
                _sfxPool.Release(source);
            }
            catch (System.OperationCanceledException)
            {
            }
        }
    }
}
