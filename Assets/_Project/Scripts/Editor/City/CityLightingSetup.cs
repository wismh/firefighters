using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace _Project.Editor.City
{
    /// <summary>
    /// Cartoon dusk for the city on PC: gradient ambient, baked sky reflection (no lightmaps),
    /// 4 shadow cascades, SSAO, Neutral tonemapping, a tuned procedural sky and matte pack materials.
    /// Everything is applied by code so a scene rebuild never loses it; the values live in assets
    /// (volume profile, sky material, lighting settings) and can be tweaked there.
    /// </summary>
    public static class CityLightingSetup
    {
        private const string SettingsFolder = "Assets/_Project/Settings";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string VolumeProfilePath = SettingsFolder + "/CityVolumeProfile.asset";
        private const string LightingSettingsPath = SettingsFolder + "/CityLightingSettings.lighting";
        private const string RendererPath = SettingsFolder + "/UniversalRenderer.asset";
        private const string SkyPath = MaterialFolder + "/CitySky.mat";
        private const string SsaoTypeName =
            "UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime";

        // Gradient ambient, tuned against screenshots: blue from above, walls out of the light stay readable.
        private static readonly Color AmbientSky = new Color(0.25f, 0.29f, 0.5f);
        private static readonly Color AmbientEquator = new Color(0.31f, 0.26f, 0.36f);
        private static readonly Color AmbientGround = new Color(0.13f, 0.12f, 0.17f);
        private static readonly Color FogColor = new Color(0.5f, 0.3f, 0.4f);
        private static readonly Quaternion SunRotation = Quaternion.Euler(7f, -50f, 0f);

        [MenuItem("Tools/City/Apply Lighting")]
        public static void ApplyMenu()
        {
            Apply(true);
            EditorSceneManager.SaveOpenScenes();
        }

        /// <summary>Applies lighting to the open scene. Bake generates the sky reflection only (no lightmaps).</summary>
        public static void Apply(bool bakeReflections)
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[city] exit Play mode first");
                return;
            }

            Directory.CreateDirectory(SettingsFolder);
            Directory.CreateDirectory(MaterialFolder);

            CityMaterialTuning.Apply();
            ConfigurePipeline();
            EnsureSsao();
            var sun = EnsureSun();
            ConfigureEnvironment(sun);
            EnsureVolume();
            ConfigureCameras();
            EditorSceneManager.MarkSceneDirty(sun.gameObject.scene);

            if (bakeReflections)
            {
                BakeEnvironment();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[city] lighting applied");
        }

        private static void ConfigurePipeline()
        {
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.defaultRenderPipeline;
            var so = new SerializedObject(pipeline);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetInt(so, "m_MainLightShadowmapResolution", 4096);
            SetFloat(so, "m_ShadowDistance", 140f);
            SetInt(so, "m_ShadowCascadeCount", 4);
            // Splits as fractions of the shadow distance: 8 m, 22 m, 55 m.
            var split = so.FindProperty("m_Cascade4Split");
            if (split != null)
            {
                split.vector3Value = new Vector3(8f / 140f, 22f / 140f, 55f / 140f);
            }

            SetFloat(so, "m_ShadowDepthBias", 1f);
            SetFloat(so, "m_ShadowNormalBias", 0.6f);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_SoftShadowQuality", 2);
            SetInt(so, "m_MSAA", 4);
            SetBool(so, "m_SupportsHDR", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        private static void EnsureSsao()
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var ssaoType = System.Type.GetType(SsaoTypeName, true);
            var feature = rendererData.rendererFeatures.FirstOrDefault(f => f != null && f.GetType() == ssaoType);
            if (feature == null)
            {
                feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
                feature.name = "SSAO";
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);
                var dataSo = new SerializedObject(rendererData);
                var features = dataSo.FindProperty("m_RendererFeatures");
                var map = dataSo.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                dataSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // City scale is ~1.4x real: a wider radius reads better on buildings and under cars.
            var so = new SerializedObject(feature);
            SetFloat(so, "m_Settings.Intensity", 0.9f);
            SetFloat(so, "m_Settings.Radius", 0.6f);
            SetFloat(so, "m_Settings.DirectLightingStrength", 0.2f);
            SetFloat(so, "m_Settings.Falloff", 60f);
            SetBool(so, "m_Settings.Downsample", false);
            SetBool(so, "m_Settings.AfterOpaque", false);
            so.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
        }

        private static Light EnsureSun()
        {
            var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional);
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.transform.rotation = SunRotation;
            sun.color = new Color(1f, 0.5f, 0.32f);
            sun.intensity = 0.8f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;
            if (!sun.TryGetComponent<UniversalAdditionalLightData>(out var data))
            {
                data = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            }

            data.usePipelineSettings = true;
            return sun;
        }

        private static void ConfigureEnvironment(Light sun)
        {
            RenderSettings.sun = sun;
            RenderSettings.skybox = EnsureSkyMaterial();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 128;
            RenderSettings.reflectionIntensity = 0.6f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 520f;
        }

        private static Material EnsureSkyMaterial()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            var shader = Shader.Find("Project/Dusk Sky");
            if (sky == null)
            {
                sky = new Material(shader);
                AssetDatabase.CreateAsset(sky, SkyPath);
            }

            // The colours live in the material; its shader's defaults are the tuned dusk.
            sky.shader = shader;
            EditorUtility.SetDirty(sky);
            return sky;
        }

        private static void EnsureVolume()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            var tonemapping = Component<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            var color = Component<ColorAdjustments>(profile);
            color.postExposure.Override(0.25f);
            color.contrast.Override(8f);
            color.saturation.Override(12f);

            var bloom = Component<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.6f);

            var vignette = Component<Vignette>(profile);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.4f);

            EditorUtility.SetDirty(profile);

            var volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).FirstOrDefault(v => v.isGlobal);
            if (volume == null)
            {
                volume = new GameObject("Global Volume").AddComponent<Volume>();
                volume.isGlobal = true;
            }

            volume.sharedProfile = profile;
            volume.priority = 0f;
        }

        private static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var existing))
            {
                existing.active = true;
                return existing;
            }

            var component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void ConfigureCameras()
        {
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
                {
                    data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }

                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None;
                data.renderShadows = true;
                camera.allowHDR = true;
                camera.allowMSAA = true;
                EditorUtility.SetDirty(data);
            }
        }

        private static void BakeEnvironment()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "CityLightingSettings" };
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
            }

            // No lightmaps: the city is rebuilt from data and the cars move. The bake only renders the
            // sky into the default reflection cubemap.
            settings.bakedGI = false;
            settings.realtimeGI = false;
            EditorUtility.SetDirty(settings);
            Lightmapping.lightingSettings = settings;
            if (!Lightmapping.Bake())
            {
                Debug.LogError("[city] environment bake failed");
            }
        }

        private static void SetBool(SerializedObject so, string path, bool value)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogWarning($"[city] property not found: {so.targetObject.GetType().Name}.{path}");
                return;
            }

            p.boolValue = value;
        }

        private static void SetInt(SerializedObject so, string path, int value)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogWarning($"[city] property not found: {so.targetObject.GetType().Name}.{path}");
                return;
            }

            // intValue writes the underlying value for enums too (MsaaQuality._4x == 4).
            p.intValue = value;
        }

        private static void SetFloat(SerializedObject so, string path, float value)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogWarning($"[city] property not found: {so.targetObject.GetType().Name}.{path}");
                return;
            }

            p.floatValue = value;
        }
    }
}
