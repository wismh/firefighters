using System.Collections.Generic;
using System.Linq;
using _Project.City;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.Editor.City
{
    /// <summary>
    /// Sets a building on fire: Epic Toon FX flames spread over the roof, tall flames on the roof edges,
    /// fire bursting from street-side windows, a dark smoke column, embers and one flickering light.
    /// The effects are authored for 1–3 m props, so every system runs in local space with hierarchy
    /// scaling and is scaled up to the building.
    /// </summary>
    public static class BuildingFireBuilder
    {
        private const string Fx = "Assets/ThirdParty/EpicToonFX/Prefabs/Environment/";
        private const string RoofFire = Fx + "Fire/Cartoon/Field Intense/FireFieldIntenseRed.prefab";
        private const string TallFire = Fx + "Fire/Soft/SoftTallFire/SoftTallFireRed.prefab";
        private const string BodyFire = Fx + "Fire/Soft/SoftFireBig/SoftFireBigRed.prefab";
        private const string WindowFire = Fx + "Fire/Spiky/Additive/SpikyFireAdditiveRed.prefab";
        private const string SmokeColumn = Fx + "Smoke/Dark/SmokeDark.prefab";
        private const string SmokeSoft = Fx + "Smoke/Dark/SmokeDarkSoft.prefab";
        private const string Embers = Fx + "Fireflies/FireFliesRed.prefab";
        private const string Materials = "Assets/ThirdParty/EpicToonFX/Materials/";
        private const string OwnMaterials = "Assets/_Project/Art/Materials/";

        // Against a bright daytime sky additive flames burn out to white under HDR and bloom, so the
        // flames use alpha-blended copies (they stay orange) and the glows a dimmer additive copy.
        private const float GlowStrength = 0.4f;

        /// <summary>Adds the fire under the building. The building's front faces its local -Z.</summary>
        public static void AddFire(GameObject building, System.Random random)
        {
            var renderers = building.GetComponentsInChildren<MeshRenderer>(true);
            var local = LocalBounds(building, renderers);
            var root = new GameObject("Fire").transform;
            root.SetParent(building.transform, false);
            var top = local.max.y;
            var footprint = new Vector2(local.size.x, local.size.z);

            // Roofs are often pitched: flames sit on the actual roof surface, found by a ray against a
            // temporary mesh collider, not at the ridge height (they would float as flat streaks).
            var probe = building.AddComponent<MeshCollider>();
            probe.sharedMesh = building.GetComponent<MeshFilter>().sharedMesh;
            float Roof(float x, float z) => RoofHeight(probe, building.transform, x, z, top);
            // Porches, awnings and the ground are not the roof: skip spots that low.
            bool OnRoof(float y) => y > top * 0.6f;

            // Flames over the roof, on a grid that fits the footprint.
            var columns = Mathf.Clamp(Mathf.RoundToInt(footprint.x / 6f), 1, 3);
            var rows = Mathf.Clamp(Mathf.RoundToInt(footprint.y / 6f), 1, 3);
            for (var cx = 0; cx < columns; cx++)
            {
                for (var cz = 0; cz < rows; cz++)
                {
                    var x = Mathf.Lerp(local.min.x, local.max.x, (cx + 0.5f) / columns) + Jitter(random, 1f);
                    var z = Mathf.Lerp(local.min.z, local.max.z, (cz + 0.5f) / rows) + Jitter(random, 1f);
                    var y = Roof(x, z);
                    if (OnRoof(y))
                    {
                        Spawn(RoofFire, root, new Vector3(x, y - 0.2f, z), 3.2f + Jitter(random, 0.4f));
                    }
                }
            }

            // The main body of flames over the roof.
            Spawn(BodyFire, root, new Vector3(local.center.x, Roof(local.center.x, local.center.z) - 0.5f,
                local.center.z), 5f);

            // Tall flames licking up along the street-side roof edge.
            var tongues = Mathf.Clamp(Mathf.RoundToInt(footprint.x / 5f), 2, 4);
            for (var i = 0; i < tongues; i++)
            {
                var x = Mathf.Lerp(local.min.x + 1.5f, local.max.x - 1.5f, (float)i / (tongues - 1)) +
                        Jitter(random, 0.8f);
                var z = local.min.z + 1.5f + (float)random.NextDouble() * Mathf.Min(4f, footprint.y * 0.4f);
                var y = Roof(x, z);
                if (!OnRoof(y))
                {
                    // Over a porch: move the flame back onto the roof.
                    z = local.center.z;
                    y = Roof(x, z);
                }

                Spawn(TallFire, root, new Vector3(x, y - 0.5f, z), 8f + Jitter(random, 1.5f));
            }

            Object.DestroyImmediate(probe);

            // Fire bursting out of street-side windows on a few upper floors.
            var windows = Mathf.Clamp(Mathf.RoundToInt(local.size.y / 6f), 2, 4);
            for (var i = 0; i < windows; i++)
            {
                var x = Mathf.Lerp(local.min.x + 1.5f, local.max.x - 1.5f, (float)random.NextDouble());
                var y = Mathf.Lerp(local.size.y * 0.3f, local.size.y * 0.85f, (float)random.NextDouble());
                Spawn(WindowFire, root, new Vector3(x, y, local.min.z - 0.4f), 3.4f + Jitter(random, 0.4f));
            }

            // Smoke: one dark column and a softer, wider plume, leaning with a light wind.
            var center = new Vector3(local.center.x, top, local.center.z);
            var smoke = Spawn(SmokeColumn, root, center + Vector3.up * 2f, 9f);
            smoke.transform.rotation = Quaternion.Euler(-78f, 25f, 0f);
            var plume = Spawn(SmokeSoft, root, center + new Vector3(2f, 1f, 1f), 6.5f);
            plume.transform.rotation = Quaternion.Euler(-80f, 25f, 0f);

            Spawn(Embers, root, center + Vector3.up * 3f, 8f);

            // Effects bring their own lights; one flickering light per building is enough.
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }

            var lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(local.center.x, top + 2f, local.min.z - 3f);
            var fireLight = lightObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.5f, 0.15f);
            fireLight.range = Mathf.Max(25f, local.size.y * 1.6f);
            fireLight.intensity = 25f;
            fireLight.shadows = LightShadows.None;

            var fire = root.gameObject.AddComponent<BuildingFire>();
            fire.Setup(fireLight, renderers);
        }

        /// <summary>Local roof height at (x, z): a downward ray against the building mesh.</summary>
        private static float RoofHeight(Collider probe, Transform building, float x, float z, float top)
        {
            var origin = building.TransformPoint(new Vector3(x, top + 5f, z));
            var ray = new Ray(origin, -building.up);
            return probe.Raycast(ray, out var hit, top + 10f)
                ? building.InverseTransformPoint(hit.point).y
                : top;
        }

        private static GameObject Spawn(string path, Transform parent, Vector3 localPosition, float scale)
        {
            var swaps = MaterialSwaps();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = localPosition;
            instance.transform.localScale = Vector3.one * scale;
            foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                // A flat ground glow: on a roof it is seen edge-on as a bright streak.
                if (system.name == "GlowFlat")
                {
                    system.gameObject.SetActive(false);
                    continue;
                }

                var main = system.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.cullingMode = ParticleSystemCullingMode.Automatic;
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer.sharedMaterial != null && swaps.TryGetValue(renderer.sharedMaterial.name, out var swap))
                {
                    renderer.sharedMaterial = swap;
                }

                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            return instance;
        }

        private static Dictionary<string, Material> MaterialSwaps()
        {
            var flameAlpha = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Misc/Fire/fire_soft_blank_AB.mat");
            var spikyTexture = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Misc/Fire/fire_soft_spiky_ADD.mat")
                .GetTexture("_BaseMap");
            var spikyAlpha = DerivedMaterial("FireSpiky_AB", flameAlpha, m => m.SetTexture("_BaseMap", spikyTexture));
            var softGlow = DerivedMaterial("FireGlowSoft_ADD",
                AssetDatabase.LoadAssetAtPath<Material>(Materials + "Basics/glow1_ADD.mat"),
                m => m.SetColor("_BaseColor", new Color(1f, 1f, 1f, GlowStrength)));
            return new Dictionary<string, Material>
            {
                ["fire_soft_blank_ADD"] = flameAlpha,
                ["fire_soft_spiky_ADD"] = spikyAlpha,
                ["glow1_ADD"] = softGlow,
            };
        }

        private static Material DerivedMaterial(string name, Material source, System.Action<Material> configure)
        {
            var path = OwnMaterials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }

            configure(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static float Jitter(System.Random random, float amount) =>
            ((float)random.NextDouble() * 2f - 1f) * amount;

        /// <summary>Bounds of the building meshes in the building's own space (unrotated).</summary>
        private static Bounds LocalBounds(GameObject building, IReadOnlyList<MeshRenderer> renderers)
        {
            var toLocal = building.transform.worldToLocalMatrix;
            var bounds = new Bounds();
            var first = true;
            foreach (var r in renderers)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                var toBuilding = toLocal * r.transform.localToWorldMatrix;
                var b = mesh.bounds;
                foreach (var corner in Corners(b))
                {
                    var p = toBuilding.MultiplyPoint3x4(corner);
                    if (first)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return bounds;
        }

        private static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (var i = 0; i < 8; i++)
            {
                yield return new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
            }
        }

        /// <summary>Picks burning buildings: tall enough, in different blocks, spread over the city.</summary>
        public static List<GameObject> PickBuildings(Transform blocksRoot, int count, System.Random random)
        {
            var candidates = new List<GameObject>();
            foreach (Transform block in blocksRoot)
            {
                var tall = block.GetComponentsInChildren<BoxCollider>(true)
                    .Select(c => c.gameObject)
                    .Where(g => g.name.StartsWith("Building_") && g.GetComponent<Renderer>().bounds.size.y > 11f)
                    .ToList();
                if (tall.Count > 0)
                {
                    candidates.Add(tall[random.Next(tall.Count)]);
                }
            }

            var picked = new List<GameObject>();
            while (picked.Count < count && candidates.Count > 0)
            {
                // Farthest-point sampling keeps the fires apart.
                var best = candidates[0];
                if (picked.Count == 0)
                {
                    best = candidates[random.Next(candidates.Count)];
                }
                else
                {
                    var bestDistance = -1f;
                    foreach (var c in candidates)
                    {
                        var d = picked.Min(p => Vector3.Distance(p.transform.position, c.transform.position));
                        if (d > bestDistance)
                        {
                            bestDistance = d;
                            best = c;
                        }
                    }
                }

                picked.Add(best);
                candidates.Remove(best);
            }

            return picked;
        }
    }
}
