using UnityEngine;

namespace _Project.Minimap
{
    /// <summary>The baked top-down picture of the city and the square of the world it covers.</summary>
    [CreateAssetMenu(menuName = "City/Minimap Data", fileName = "MinimapData")]
    public sealed class MinimapData : ScriptableObject
    {
        [SerializeField] private Texture2D map;
        [Tooltip("World X/Z of the texture's bottom-left corner.")]
        [SerializeField] private Vector2 worldMin;
        [Tooltip("Side of the covered square, metres.")]
        [SerializeField] private float worldSize;

        public Texture2D Map => map;
        public float WorldSize => worldSize;
        public Vector2 WorldCenter => worldMin + Vector2.one * (worldSize * 0.5f);

        public void Setup(Texture2D texture, Vector2 min, float size)
        {
            map = texture;
            worldMin = min;
            worldSize = size;
        }
    }
}
