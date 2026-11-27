using System.Collections.Generic;
using System.Linq;
using _Project.City;
using _Project.Lighting;
using _Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.Editor.City
{
    /// <summary>
    /// The evening lights of the city: windows, street lamps and, through the vehicle prefabs, headlights.
    /// Works on the open scene and can be re-run; the dusk itself is <see cref="CityLightingSetup"/>.
    /// </summary>
    public static class CityNightBuilder
    {
        private const string Materials = "Assets/_Project/Art/Materials";
        private const string NightMaterials = Materials + "/Night";
        private const string LampPrefabPath = "Assets/_Project/Prefabs/StreetLampLight.prefab";
        private const string LampChild = "LampLight";
        private const string SystemName = "StreetLamps";

        // What grey the pack paints window panes in each texture, sRGB 0..255.
        private static readonly (string texture, float grey)[] Panes =
        {
            ("SimpleTown", 52f), ("House_01", 89f), ("House_02", 58f), ("House_03", 58f), ("Building", 64f),
        };

        [MenuItem("Tools/City/Setup Evening")]
        public static void SetupEvening()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[city] exit Play mode first");
                return;
            }

            VehiclePrefabBuilder.BuildAll();
            Apply();
            CityLightingSetup.Apply(true);
            EditorSceneManager.SaveOpenScenes();
        }

        /// <summary>Lights windows and street lamps in the open city scene.</summary>
        public static void Apply()
        {
            LightWindows();
            LightLamps();
        }

        /// <summary>An unlit HDR material bright enough to bloom.</summary>
        internal static Material EnsureGlow(string name, Color color)
        {
            return CitySceneSetup.EnsureUnlitMaterial(name, color);
        }

        private static void LightWindows()
        {
            if (!AssetDatabase.IsValidFolder(NightMaterials))
            {
                AssetDatabase.CreateFolder(Materials, "Night");
            }

            var count = 0;
            var renderers = GameObject.Find("City/Blocks").GetComponentsInChildren<MeshRenderer>(true)
                .Concat(GameObject.Find("City/Outskirts").GetComponentsInChildren<MeshRenderer>(true));
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var night = NightVersion(materials[i]);
                    changed |= night != materials[i];
                    materials[i] = night;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                    count++;
                }
            }

            Debug.Log($"[city] lit the windows of {count} buildings");
        }

        private static Material NightVersion(Material day)
        {
            var texture = day.mainTexture;
            if (texture == null || AssetDatabase.GetAssetPath(day).StartsWith(NightMaterials))
            {
                return day;
            }

            var pane = Panes.FirstOrDefault(p => texture.name.StartsWith(p.texture));
            if (pane.texture == null)
            {
                return day;
            }

            var path = $"{NightMaterials}/{day.name}_Night.mat";
            var night = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (night == null)
            {
                night = new Material(Shader.Find("Project/Night Building"));
                AssetDatabase.CreateAsset(night, path);
            }

            night.SetTexture("_BaseMap", texture);
            night.SetColor("_BaseColor", day.GetColor("_BaseColor"));
            night.SetFloat("_PaneGrey", pane.grey / 255f);
            EditorUtility.SetDirty(night);
            return night;
        }

        private static void LightLamps()
        {
            var prefab = BuildLampPrefab();
            var lamps = new List<StreetLamp>();
            foreach (var prop in Object.FindObjectsByType<KnockableProp>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (prop.Kind != PropKind.Lamp)
                {
                    continue;
                }

                FaceRoad(prop.transform);
                var old = prop.transform.Find(LampChild);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                var lamp = (StreetLamp)PrefabUtility.InstantiatePrefab(prefab, prop.transform);
                lamp.name = LampChild;
                lamps.Add(lamp);
            }

            var previous = GameObject.Find(SystemName);
            if (previous != null)
            {
                Object.DestroyImmediate(previous);
            }

            var modes = Object.FindFirstObjectByType<PlayerModeController>();
            var camera = (Transform)new SerializedObject(modes).FindProperty("cameraTransform").objectReferenceValue;
            var system = new GameObject(SystemName).AddComponent<StreetLampSystem>();
            system.Setup(camera.GetComponent<Camera>(), lamps.ToArray());
            EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
            Debug.Log($"[city] lit {lamps.Count} street lamps");
        }

        /// <summary>
        /// Cities built before the lamps were turned round have the heads over the house walls: such a lamp turns
        /// about its pole.
        /// </summary>
        private static void FaceRoad(Transform lamp)
        {
            var pole = lamp.TransformPoint(0f, 0f, 1.375f);
            var head = lamp.TransformPoint(0f, 0f, -1.43f);
            if (RoadDistance(head) > RoadDistance(pole))
            {
                lamp.RotateAround(pole, Vector3.up, 180f);
            }
        }

        private static float RoadDistance(Vector3 position)
        {
            var period = AssetDatabase.LoadAssetAtPath<CityLayout>(CitySceneSetup.LayoutPath).Period;
            return Mathf.Min(Mathf.Abs(position.x - Mathf.Round(position.x / period) * period),
                Mathf.Abs(position.z - Mathf.Round(position.z / period) * period));
        }

        private static StreetLamp BuildLampPrefab()
        {
            // The head of lamp_mesh: 0.54 x 0.94 m, its underside 7.84 m up.
            var head = new Vector3(0f, 7.82f, -1.43f);
            var root = new GameObject(LampChild);
            try
            {
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bulb.name = "Bulb";
                Object.DestroyImmediate(bulb.GetComponent<Collider>());
                bulb.transform.SetParent(root.transform, false);
                bulb.transform.localPosition = head;
                bulb.transform.localScale = new Vector3(0.44f, 0.12f, 0.84f);
                var renderer = bulb.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = EnsureGlow("LampBulb", new Color(6f, 3.6f, 1.3f));
                renderer.shadowCastingMode = ShadowCastingMode.Off;

                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.transform.localPosition = head + Vector3.down * 0.15f;
                light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                light.type = LightType.Spot;
                light.color = new Color(1f, 0.72f, 0.4f);
                light.spotAngle = 135f;
                light.innerSpotAngle = 60f;
                light.range = 20f;
                light.intensity = 130f;
                light.shadows = LightShadows.None;

                root.AddComponent<StreetLamp>().Setup(light, renderer);
                return PrefabUtility.SaveAsPrefabAsset(root, LampPrefabPath).GetComponent<StreetLamp>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
