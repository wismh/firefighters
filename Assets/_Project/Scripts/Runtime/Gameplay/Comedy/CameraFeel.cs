using UnityEngine;

namespace _Project.Comedy
{
    /// <summary>
    /// Screen shake and hit-stop for the player camera. The shake is a local offset added after every other camera
    /// script has placed the camera this frame and taken off again before they run next frame, so the chase camera
    /// and the head-mounted camera both keep their own placement. Hit-stop drops the time scale for a few real
    /// milliseconds, the cartoon freeze-frame of a hard hit.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class CameraFeel : MonoBehaviour
    {
        [SerializeField] private float maxOffset = 0.45f;
        [SerializeField] private float maxAngle = 4f;
        [Tooltip("Trauma lost per real second.")]
        [SerializeField] private float decay = 1.8f;
        [SerializeField] private float frequency = 24f;
        [SerializeField] private float hitStopScale = 0.05f;

        private float _trauma;
        private float _seed;
        private Vector3 _appliedOffset;
        private Quaternion _appliedRotation = Quaternion.identity;
        private Transform _appliedParent;
        private bool _applied;
        private float _hitStopEnd;
        private bool _stopped;

        /// <summary>Adds trauma, 0..1; the shake grows with its square, so small knocks stay small.</summary>
        public void Shake(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        /// <summary>Freezes the game for <paramref name="seconds"/> of real time.</summary>
        public void HitStop(float seconds)
        {
            _hitStopEnd = Mathf.Max(_hitStopEnd, Time.unscaledTime + seconds);
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            Time.timeScale = hitStopScale;
        }

        private void Awake()
        {
            _seed = Random.value * 100f;
        }

        private void Update()
        {
            Undo();
            if (_stopped && Time.unscaledTime >= _hitStopEnd)
            {
                _stopped = false;
                Time.timeScale = 1f;
            }
        }

        private void LateUpdate()
        {
            _trauma = Mathf.Max(0f, _trauma - decay * Time.unscaledDeltaTime);
            if (_trauma <= 0f)
            {
                return;
            }

            var shake = _trauma * _trauma;
            var t = Time.unscaledTime * frequency;
            var offset = new Vector3(Noise(t, 0f), Noise(t, 1f), Noise(t, 2f) * 0.5f) * (maxOffset * shake);
            var rotation = Quaternion.Euler(Noise(t, 3f) * maxAngle * shake, Noise(t, 4f) * maxAngle * shake,
                Noise(t, 5f) * maxAngle * shake);
            transform.localPosition += offset;
            transform.localRotation *= rotation;
            _appliedOffset = offset;
            _appliedRotation = rotation;
            _appliedParent = transform.parent;
            _applied = true;
        }

        private void OnDisable()
        {
            Undo();
            if (_stopped)
            {
                _stopped = false;
                Time.timeScale = 1f;
            }
        }

        // Takes last frame's shake off, unless the camera moved to another parent since (it was re-placed then).
        private void Undo()
        {
            if (!_applied)
            {
                return;
            }

            _applied = false;
            if (transform.parent != _appliedParent)
            {
                return;
            }

            transform.localRotation *= Quaternion.Inverse(_appliedRotation);
            transform.localPosition -= _appliedOffset;
        }

        private float Noise(float t, float channel)
        {
            return Mathf.PerlinNoise(t, _seed + channel * 7.31f) * 2f - 1f;
        }
    }
}
