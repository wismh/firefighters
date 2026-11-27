using UnityEngine;
using UnityEngine.UI;

namespace _Project.Minimap
{
    /// <summary>
    /// One icon on the minimap. Inside the circle it shows the icon alone; pinned to the edge it shrinks and
    /// shows an arrow pointing outwards and the distance.
    /// </summary>
    public sealed class MinimapMarker : MonoBehaviour
    {
        private const float DistanceStep = 10f;

        [SerializeField] private RectTransform root;
        [SerializeField] private Image icon;
        [SerializeField] private RectTransform pointer;
        [SerializeField] private Text distance;
        [SerializeField] private float pinnedScale = 0.75f;

        private int _shownDistance = -1;

        public RectTransform Root => root;
        public Image Icon => icon;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Places the marker at <paramref name="position"/> in the circle's space. <paramref name="pinned"/> means it
        /// sits on the edge for a target farther out, <paramref name="metres"/> away.
        /// </summary>
        public void Place(Vector2 position, bool pinned, float metres)
        {
            root.anchoredPosition = position;
            pointer.gameObject.SetActive(pinned);
            distance.gameObject.SetActive(pinned);
            if (!pinned)
            {
                return;
            }

            var angle = Mathf.Atan2(position.y, position.x) * Mathf.Rad2Deg;
            pointer.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            // The label goes on the inner side of the icon, so it never sits outside the ring.
            distance.rectTransform.anchoredPosition = -position.normalized * 26f;
            // A new string only when the rounded distance changes, not every frame.
            var rounded = Mathf.RoundToInt(metres / DistanceStep) * (int)DistanceStep;
            if (rounded != _shownDistance)
            {
                _shownDistance = rounded;
                distance.text = rounded + " м";
            }
        }

        /// <summary>Icon scale and opacity; pinned icons are drawn smaller.</summary>
        public void SetLook(float scale, Color color, bool pinned)
        {
            icon.rectTransform.localScale = Vector3.one * (scale * (pinned ? pinnedScale : 1f));
            icon.color = color;
        }

        public void SetIconRotation(float degrees)
        {
            icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }
    }
}
