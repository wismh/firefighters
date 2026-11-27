using _Project.City;
using UnityEngine;

namespace _Project.Traffic
{
    /// <summary>
    /// Signal head for one approach of an intersection: red, yellow and green lamps in the head's three lenses.
    /// The lit one glows brighter than white (bloom catches it), the others stay faintly tinted. Placed by the
    /// city builder.
    /// </summary>
    public sealed class TrafficLightView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color OffColor = new Color(0.04f, 0.04f, 0.04f);
        private static readonly Color Red = new Color(1f, 0.1f, 0.08f);
        private static readonly Color Yellow = new Color(1f, 0.6f, 0.05f);
        private static readonly Color Green = new Color(0.15f, 1f, 0.2f);
        // HDR multiplier of the lit lamp, and the share of its colour an unlit lamp keeps.
        private const float LitIntensity = 2f;
        private const float UnlitShare = 0.12f;

        [SerializeField] private Vector2Int cell;
        [SerializeField] private GridDirection approach;
        [SerializeField] private Renderer redLamp;
        [SerializeField] private Renderer yellowLamp;
        [SerializeField] private Renderer greenLamp;
        [SerializeField] private KnockableProp pole;

        private MaterialPropertyBlock _block;

        public Vector2Int Cell => cell;
        public int Axis => approach.Axis();
        public GridDirection Approach => approach;

        public void Setup(Vector2Int nodeCell, GridDirection approachDirection, KnockableProp knockable)
        {
            cell = nodeCell;
            approach = approachDirection;
            pole = knockable;
        }

        public void SetLamps(Renderer red, Renderer yellow, Renderer green)
        {
            redLamp = red;
            yellowLamp = yellow;
            greenLamp = green;
        }

        /// <summary>A knocked-over signal goes dark; the intersection keeps cycling its phases.</summary>
        public void Show(SignalState state)
        {
            if (pole.IsKnocked)
            {
                return;
            }

            SetLamp(redLamp, Red, state == SignalState.Red);
            SetLamp(yellowLamp, Yellow, state == SignalState.Yellow);
            SetLamp(greenLamp, Green, state == SignalState.Green);
        }

        private void Awake()
        {
            pole.Knocked += OnKnocked;
        }

        private void OnDestroy()
        {
            pole.Knocked -= OnKnocked;
        }

        private void OnKnocked(KnockableProp _)
        {
            SetColor(redLamp, OffColor);
            SetColor(yellowLamp, OffColor);
            SetColor(greenLamp, OffColor);
        }

        private void SetLamp(Renderer lamp, Color color, bool lit)
        {
            SetColor(lamp, lit ? color * LitIntensity : color * UnlitShare);
        }

        private void SetColor(Renderer lamp, Color color)
        {
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            lamp.SetPropertyBlock(_block);
        }
    }
}
