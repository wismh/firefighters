using UnityEditor;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>
    /// Matte surfaces for the pack materials. The Standard → URP upgrade left every material at Smoothness 0.5,
    /// which makes asphalt, sidewalks and walls shine like plastic. Re-run after a pack update.
    /// </summary>
    public static class CityMaterialTuning
    {
        private const string Folder = ProjectPaths.ThirdParty;

        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int SpecularHighlightsId = Shader.PropertyToID("_SpecularHighlights");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");

        [MenuItem("Tools/City/Tune Pack Materials")]
        public static void Apply()
        {
            var tuned = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material.HasProperty(SmoothnessId))
                {
                    continue;
                }

                var (smoothness, highlights) = Classify(material.name);
                material.SetFloat(SmoothnessId, smoothness);
                material.SetFloat(GlossinessId, smoothness);
                material.SetFloat(MetallicId, 0f);
                material.SetFloat(SpecularHighlightsId, highlights ? 1f : 0f);
                if (highlights)
                {
                    material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                }
                else
                {
                    material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                }

                EditorUtility.SetDirty(material);
                tuned++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[city] tuned {tuned} pack materials");
        }

        /// <summary>Smoothness and specular highlights by material family.</summary>
        private static (float smoothness, bool highlights) Classify(string name)
        {
            // Car paint keeps a soft sheen; SimpleTown cars share one atlas material with their windows.
            if (name.StartsWith("Vehicle") || name.StartsWith("Vehice") || name.StartsWith("Cars_"))
            {
                return (0.4f, true);
            }

            // Asphalt must never flare in the sun.
            if (name == "Road" || name == "Asphalt" || name == "Road_divider")
            {
                return (0.05f, false);
            }

            // Walls, roofs, sidewalks, grass, props: matte.
            return (0.1f, true);
        }
    }
}
