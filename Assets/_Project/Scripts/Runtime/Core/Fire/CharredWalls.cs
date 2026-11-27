using System.Collections.Generic;
using UnityEngine;

namespace _Project.Fire
{
    /// <summary>
    /// The walls of a burning building: charred copies of its materials, whose shader glows with embers around each
    /// flame (following its strength with a delay) and spreads soot over the walls while the building burns.
    /// </summary>
    public sealed class CharredWalls
    {
        // Array size in CharredBuilding.shader.
        private const int MaxShaderFlames = 32;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int FlamesId = Shader.PropertyToID("_Flames");
        private static readonly int FlameCountId = Shader.PropertyToID("_FlameCount");
        private static readonly int CharGrowId = Shader.PropertyToID("_CharGrow");

        private readonly List<Material> _materials = new List<Material>(4);
        private readonly Vector4[] _flames = new Vector4[MaxShaderFlames];
        private readonly FlameSpot[] _spots;
        private readonly int _count;
        private float _charGrow;

        public CharredWalls(Material template, Renderer[] renderers, FlameSpot[] spots)
        {
            _spots = spots;
            foreach (var target in renderers)
            {
                CharMaterials(target, template);
            }

            _count = Mathf.Min(spots.Length, MaxShaderFlames);
            for (var i = 0; i < _count; i++)
            {
                var position = spots[i].root.position;
                _flames[i] = new Vector4(position.x, position.y, position.z, 0f);
            }

            Push();
        }

        /// <param name="emberRate">How fast the embers heat up and cool down after their flame, per second.</param>
        /// <param name="charSpreadTime">Seconds the char takes to cover the walls while the building burns.</param>
        public void Tick(float dt, bool burning, float emberRate, float charSpreadTime)
        {
            for (var i = 0; i < _count; i++)
            {
                _flames[i].w = Mathf.MoveTowards(_flames[i].w, _spots[i].Strength, emberRate * dt);
            }

            if (burning)
            {
                _charGrow = Mathf.Min(1f, _charGrow + dt / charSpreadTime);
            }

            Push();
        }

        public void Destroy()
        {
            foreach (var material in _materials)
            {
                Object.Destroy(material);
            }

            _materials.Clear();
        }

        /// <summary>Gives the renderer charred copies of its materials, each with the texture of the one it replaces.</summary>
        private void CharMaterials(Renderer target, Material template)
        {
            var materials = target.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
            {
                var charred = new Material(template);
                charred.SetTexture(BaseMapId, materials[i].GetTexture(BaseMapId));
                charred.SetColor(BaseColorId, materials[i].GetColor(BaseColorId));
                materials[i] = charred;
                _materials.Add(charred);
            }

            target.sharedMaterials = materials;
        }

        private void Push()
        {
            foreach (var material in _materials)
            {
                material.SetVectorArray(FlamesId, _flames);
                material.SetFloat(FlameCountId, _count);
                material.SetFloat(CharGrowId, _charGrow);
            }
        }
    }
}
