using MyToolkit.Audio;
using UnityEngine;

namespace _Project.Audio
{
    /// <summary>Variations of one sound (screams, footsteps): each play picks a clip, never the same one twice running.</summary>
    [CreateAssetMenu(menuName = "Project/Audio/Sound Bank", fileName = "SoundBank")]
    public sealed class SoundBank : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;
        [SerializeField] private Vector2 pitchRange = Vector2.one;

        private int _last = -1;

        public void Play(ISfxPlayer sfx, float volumeScale = 1f)
        {
            var index = Random.Range(0, clips.Length);
            if (index == _last && clips.Length > 1)
            {
                index = (index + 1) % clips.Length;
            }

            _last = index;
            sfx.PlaySfx(clips[index], volume * volumeScale, pitchRange);
        }
    }
}
