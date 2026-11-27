using _Project.FireHose;
using _Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace _Project.Editor.City
{
    /// <summary>Adds the burning-screen overlay and its post-processing volume to the city scene. Re-runnable.</summary>
    public static class BurningBuilder
    {
        private const string ObjectName = "BurningScreen";
        private const string MaterialPath = ProjectPaths.Materials + "/ScreenFire.mat";
        private const string ProfilePath = ProjectPaths.Settings + "/BurningVolumeProfile.asset";
        // Under the HUD (10), over the picture.
        private const int SortingOrder = 5;

        [MenuItem("Tools/City/Setup Burning Screen")]
        public static void SetupBurning()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[city] exit Play mode first");
                return;
            }

            Apply();
            EditorSceneManager.SaveOpenScenes();
        }

        public static void Apply()
        {
            var old = GameObject.Find(ObjectName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var root = new GameObject(ObjectName, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var overlay = new GameObject("Flames", typeof(RectTransform)).AddComponent<RawImage>();
            overlay.transform.SetParent(root.transform, false);
            var rect = overlay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            overlay.raycastTarget = false;
            overlay.material = EnsureMaterial();
            overlay.enabled = false;

            var volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 0f;
            volume.sharedProfile = EnsureProfile();

            CitySceneSetup.SetRefs(root.AddComponent<BurningView>(), ("overlay", overlay), ("volume", volume));
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[city] burning screen set up");
        }

        private static Material EnsureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Project/Screen Fire"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            return material;
        }

        private static VolumeProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            Add<ChromaticAberration>(profile).intensity.Override(0.8f);
            var distortion = Add<LensDistortion>(profile);
            distortion.intensity.Override(0.2f);
            distortion.scale.Override(1.04f);
            var color = Add<ColorAdjustments>(profile);
            color.colorFilter.Override(new Color(1f, 0.78f, 0.55f));
            color.saturation.Override(25f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            // Nothing overridden but what the caller sets: the rest stays the city's own grading.
            var component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
