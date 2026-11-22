using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>
    /// Upgrades Epic Toon FX particle materials ("Particles/Standard Unlit") to URP Particles/Unlit with
    /// URP's own ParticleUpgrader, which keeps blend mode, colour mode, soft particles and textures.
    /// </summary>
    public static class VfxMaterialUpgrade
    {
        public const string Folder = "Assets/ThirdParty/EpicToonFX";

        [MenuItem("Tools/City/Upgrade VFX Materials")]
        public static int Upgrade()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;
            var upgraderType = Type.GetType(
                "UnityEditor.Rendering.Universal.ParticleUpgrader, Unity.RenderPipelines.Universal.Editor", true);
            var baseType = Type.GetType("UnityEditor.Rendering.MaterialUpgrader, Unity.RenderPipelines.Core.Editor", true);
            var upgraders = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(baseType));
            upgraders.Add(Activator.CreateInstance(upgraderType, flags, null, new object[] { "Particles/Standard Unlit" }, null));
            upgraders.Add(Activator.CreateInstance(upgraderType, flags, null, new object[] { "Particles/Standard Surface" }, null));
            var upgradeFlags = Enum.ToObject(baseType.GetNestedType("UpgradeFlags"), 0);
            var upgrade = Array.Find(baseType.GetMethods(), m =>
                m.Name == "Upgrade" && m.GetParameters().Length == 3 &&
                m.GetParameters()[0].ParameterType == typeof(Material) &&
                m.GetParameters()[1].ParameterType == upgraders.GetType());

            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material.shader.name.StartsWith("Particles/Standard"))
                {
                    continue;
                }

                upgrade.Invoke(null, new[] { material, upgraders, upgradeFlags });
                EditorUtility.SetDirty(material);
                count++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[city] upgraded {count} VFX materials to URP");
            return count;
        }
    }
}
