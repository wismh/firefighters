using _Project.City;
using UnityEngine;

namespace _Project.Traffic
{
    /// <summary>Signal head for one approach of an intersection. Placed by the city builder.</summary>
    public sealed class TrafficLightView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color OffColor = new Color(0.06f, 0.06f, 0.06f);

        [SerializeField] private Vector2Int cell;
        [SerializeField] private GridDirection approach;
        [SerializeField] private Renderer lamp;
        [SerializeField] private KnockableProp pole;

        private MaterialPropertyBlock _block;

        public Vector2Int Cell => cell;
        public int Axis => approach.Axis();

        public void Setup(Vector2Int nodeCell, GridDirection approachDirection, Renderer lampRenderer,
            KnockableProp knockable)
        {
            cell = nodeCell;
            approach = approachDirection;
            lamp = lampRenderer;
            pole = knockable;
        }

        /// <summary>A knocked-over signal goes dark; the intersection keeps cycling its phases.</summary>
        public void Show(SignalState state)
        {
            if (pole.IsKnocked)
            {
                return;
            }

            var color = state switch
            {
                SignalState.Green => new Color(0.1f, 1f, 0.25f),
                SignalState.Yellow => new Color(1f, 0.8f, 0.1f),
                _ => new Color(1f, 0.1f, 0.08f),
            };
            SetColor(color);
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
            SetColor(OffColor);
        }

        private void SetColor(Color color)
        {
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color);
            lamp.SetPropertyBlock(_block);
        }
    }
}
