using UnityEngine;
using UnityEngine.UI;

namespace _Project.Mission
{
    /// <summary>
    /// One fire marker on the screen: a flame icon with the distance under it. Pinned to the edge of the screen for
    /// a fire out of view, it also shows an arrow pointing towards the fire.
    /// </summary>
    public sealed class TargetMarker : MonoBehaviour
    {
        private const float DistanceStep = 10f;

        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform icon;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private Text distance;
        [Tooltip("Pixels from the icon's centre to the arrow's.")]
        [SerializeField] private float arrowOffset = 38f;

        private int _shownDistance = -1;
        private bool _arrowShown = true;

        public float Alpha
        {
            get => group.alpha;
            set => group.alpha = value;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Puts the marker at <paramref name="position"/> in its parent's space. <paramref name="pinned"/> means the
        /// fire is out of view in <paramref name="direction"/> (a unit vector on the screen).
        /// </summary>
        public void Place(Vector2 position, bool pinned, Vector2 direction, float metres, float scale)
        {
            root.anchoredPosition = position;
            icon.localScale = Vector3.one * scale;
            if (_arrowShown != pinned)
            {
                _arrowShown = pinned;
                arrow.gameObject.SetActive(pinned);
            }

            if (pinned)
            {
                arrow.anchoredPosition = direction * arrowOffset;
                arrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
            }

            // A new string only when the rounded distance changes, not every frame.
            var rounded = Mathf.RoundToInt(metres / DistanceStep) * (int)DistanceStep;
            if (rounded != _shownDistance)
            {
                _shownDistance = rounded;
                distance.text = rounded + " м";
            }
        }
    }
}
