using UnityEngine;

namespace _Project.Pedestrians
{
    /// <summary>
    /// The soul of a pedestrian who was hit: rises out of the body, hovers there waving its arms in outrage,
    /// and slips back in, after which the body gets up.
    /// </summary>
    public sealed class Ghost : MonoBehaviour
    {
        private const float RiseSeconds = 0.9f;
        private const float HoverSeconds = 2.4f;
        private const float ReturnSeconds = 0.6f;
        private const float HoverHeight = 1.6f;
        private const float HiddenScale = 0.15f;

        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;

        private Pedestrian _body;
        private Quaternion _leftRest;
        private Quaternion _rightRest;
        private float _time;

        public bool IsOut { get; private set; }
        public Vector3 Top => transform.position + Vector3.up * 1.8f;

        public void Setup(Transform left, Transform right)
        {
            leftArm = left;
            rightArm = right;
        }

        public void Appear(Pedestrian body)
        {
            _body = body;
            _body.IsHaunted = true;
            _time = 0f;
            IsOut = true;
            gameObject.SetActive(true);
            Pose();
        }

        private void Awake()
        {
            _leftRest = leftArm.localRotation;
            _rightRest = rightArm.localRotation;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            var done = _time > RiseSeconds + HoverSeconds + ReturnSeconds;
            if (done || !_body.isActiveAndEnabled)
            {
                Vanish();
                return;
            }

            Pose();
        }

        private void Vanish()
        {
            _body.IsHaunted = false;
            IsOut = false;
            gameObject.SetActive(false);
        }

        private void Pose()
        {
            var rise = Mathf.SmoothStep(0f, 1f, _time / RiseSeconds);
            var back = Mathf.SmoothStep(0f, 1f, (_time - RiseSeconds - HoverSeconds) / ReturnSeconds);
            var outOfBody = rise * (1f - back);
            var sway = new Vector3(Mathf.Sin(_time * 2.1f), Mathf.Sin(_time * 3.3f) * 0.4f, Mathf.Cos(_time * 1.7f));
            transform.position = _body.Position + (Vector3.up * HoverHeight + sway * 0.25f) * outOfBody;
            transform.localScale = Vector3.one * Mathf.Lerp(HiddenScale, 1f, outOfBody);
            transform.rotation = Quaternion.Euler(0f, _time * 60f, Mathf.Sin(_time * 4f) * 12f);
            var wave = Mathf.Sin(_time * 14f) * 45f;
            leftArm.localRotation = _leftRest * Quaternion.Euler(wave, 0f, 0f);
            rightArm.localRotation = _rightRest * Quaternion.Euler(-wave, 0f, 0f);
        }
    }
}
