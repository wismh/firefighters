using UnityEngine;
using UnityEngine.UI;

namespace _Project.Hud
{
    /// <summary>
    /// Short labels that rise from a world point and fade ("-$1200", "ВРЯТОВАНО!"). A fixed pool of uGUI texts on
    /// the HUD canvas, so a label costs no allocation; the oldest one is reused when all are busy.
    /// </summary>
    public sealed class FloatingTextView : MonoBehaviour
    {
        private const int Capacity = 24;
        private const float Lifetime = 1.6f;
        private const float RiseSpeed = 1.4f;
        private const float StackWindow = 0.5f;
        private const float StackStep = 1.1f;
        private const float StackNear = 4f;
        // The label is built at its biggest (the pop) and scaled down, so the font never re-rasterises mid-flight.
        private const float MaxPop = 1.6f;

        [SerializeField] private Camera viewCamera;
        [SerializeField] private RectTransform area;
        [Tooltip("Inactive template label; copies of it are the pool.")]
        [SerializeField] private Text template;

        private readonly Entry[] _entries = new Entry[Capacity];
        private int _next;
        private int _baseSize;

        private struct Entry
        {
            public Text Label;
            public Vector3 Position;
            public Color Color;
            public float Age;
            public float Scale;
            public bool Alive;
        }

        /// <summary>Shows <paramref name="text"/> at <paramref name="position"/>; <paramref name="scale"/> 1 is the normal size.</summary>
        public void Add(Vector3 position, string text, Color color, float scale = 1f)
        {
            // Labels born together stack upwards instead of printing over each other.
            for (var pass = 0; pass < 3; pass++)
            {
                for (var i = 0; i < Capacity; i++)
                {
                    var other = _entries[i];
                    var young = other.Alive && other.Age < StackWindow;
                    var near = Mathf.Abs(other.Position.x - position.x) + Mathf.Abs(other.Position.z - position.z) <
                               StackNear;
                    var overlapping = Mathf.Abs(other.Position.y - position.y) < StackStep;
                    if (young && near && overlapping)
                    {
                        position.y = other.Position.y + StackStep;
                    }
                }
            }

            ref var entry = ref _entries[_next];
            entry.Position = position;
            entry.Color = color;
            entry.Age = 0f;
            entry.Scale = scale;
            entry.Alive = true;
            entry.Label.text = text;
            entry.Label.fontSize = Mathf.RoundToInt(_baseSize * scale * MaxPop);
            _next = (_next + 1) % Capacity;
        }

        private void Awake()
        {
            _baseSize = template.fontSize;
            template.gameObject.SetActive(false);
            for (var i = 0; i < Capacity; i++)
            {
                var label = Instantiate(template, area);
                label.name = "Label" + i;
                _entries[i].Label = label;
            }
        }

        private void LateUpdate()
        {
            var dt = Time.deltaTime;
            for (var i = 0; i < Capacity; i++)
            {
                ref var entry = ref _entries[i];
                if (!entry.Alive)
                {
                    continue;
                }

                entry.Age += dt;
                entry.Position += Vector3.up * (RiseSpeed * dt);
                entry.Alive = entry.Age < Lifetime;
                var label = entry.Label;
                var screen = viewCamera.WorldToScreenPoint(entry.Position);
                var visible = entry.Alive && screen.z > 0f;
                if (label.gameObject.activeSelf != visible)
                {
                    label.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
                label.rectTransform.anchoredPosition = local;
                // Pops in big, settles, then fades over the last third.
                var pop = 1f + 0.6f * Mathf.Clamp01(1f - entry.Age * 8f);
                label.rectTransform.localScale = Vector3.one * (pop / MaxPop);
                var color = entry.Color;
                color.a = Mathf.Clamp01((1f - entry.Age / Lifetime) * 3f);
                label.color = color;
            }
        }
    }
}
