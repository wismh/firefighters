using _Project.City;
using UnityEngine;

namespace _Project.Traffic
{
    /// <summary>Signal head for one approach of an intersection. Placed by the city builder.</summary>
    public sealed class TrafficLightView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Vector2Int cell;
        [SerializeField] private GridDirection approach;
        [SerializeField] private Renderer lamp;

        private MaterialPropertyBlock _block;

        public Vector2Int Cell => cell;
        public int Axis => approach.Axis();

        public void Setup(Vector2Int nodeCell, GridDirection approachDirection, Renderer lampRenderer)
        {
            cell = nodeCell;
            approach = approachDirection;
            lamp = lampRenderer;
        }

        public void Show(SignalState state)
        {
            _block ??= new MaterialPropertyBlock();
            var color = state switch
            {
                SignalState.Green => new Color(0.1f, 1f, 0.25f),
                SignalState.Yellow => new Color(1f, 0.8f, 0.1f),
                _ => new Color(1f, 0.1f, 0.08f),
            };
            _block.SetColor(BaseColorId, color);
            lamp.SetPropertyBlock(_block);
        }
    }
}
