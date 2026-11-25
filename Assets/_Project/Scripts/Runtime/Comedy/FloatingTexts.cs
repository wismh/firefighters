using UnityEngine;

namespace _Project.Comedy
{
    /// <summary>Short labels that rise from a world point and fade ("-$1200"), drawn with IMGUI like the rest of the HUD.</summary>
    public sealed class FloatingTexts
    {
        private const int Capacity = 24;
        private const float Lifetime = 1.6f;
        private const float RiseSpeed = 1.4f;
        private const float StackWindow = 0.5f;
        private const float StackStep = 1.1f;

        private struct Entry
        {
            public Vector3 Position;
            public string Text;
            public Color Color;
            public float Age;
            public float Scale;
            public bool Alive;
        }

        private readonly Entry[] _entries = new Entry[Capacity];
        private int _next;

        public void Add(Vector3 position, string text, Color color, float scale = 1f)
        {
            // Labels born together stack upwards instead of printing over each other.
            for (var pass = 0; pass < 3; pass++)
            {
                for (var i = 0; i < Capacity; i++)
                {
                    var other = _entries[i];
                    var young = other.Alive && other.Age < StackWindow;
                    var near = Mathf.Abs(other.Position.x - position.x) + Mathf.Abs(other.Position.z - position.z) < 4f;
                    var overlapping = Mathf.Abs(other.Position.y - position.y) < StackStep;
                    if (young && near && overlapping)
                    {
                        position.y = other.Position.y + StackStep;
                    }
                }
            }

            _entries[_next] = new Entry
            {
                Position = position,
                Text = text,
                Color = color,
                Scale = scale,
                Alive = true,
            };
            _next = (_next + 1) % Capacity;
        }

        public void Tick(float dt)
        {
            for (var i = 0; i < Capacity; i++)
            {
                if (!_entries[i].Alive)
                {
                    continue;
                }

                _entries[i].Age += dt;
                _entries[i].Position += Vector3.up * (RiseSpeed * dt);
                _entries[i].Alive = _entries[i].Age < Lifetime;
            }
        }

        /// <summary>Call from OnGUI.</summary>
        public void Draw(Camera camera, GUIStyle style, GUIStyle shadow)
        {
            for (var i = 0; i < Capacity; i++)
            {
                var entry = _entries[i];
                if (!entry.Alive)
                {
                    continue;
                }

                var screen = camera.WorldToScreenPoint(entry.Position);
                if (screen.z <= 0f)
                {
                    continue;
                }

                var t = entry.Age / Lifetime;
                // Pops in big, settles, then fades over the last third.
                var pop = 1f + 0.6f * Mathf.Clamp01(1f - entry.Age * 8f);
                var alpha = Mathf.Clamp01((1f - t) * 3f);
                var size = Mathf.RoundToInt(26f * entry.Scale * pop);
                style.fontSize = size;
                shadow.fontSize = size;
                var rect = new Rect(screen.x - 150f, Screen.height - screen.y - 20f, 300f, 40f);
                var color = entry.Color;
                color.a = alpha;
                shadow.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
                style.normal.textColor = color;
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), entry.Text, shadow);
                GUI.Label(rect, entry.Text, style);
            }
        }
    }
}
