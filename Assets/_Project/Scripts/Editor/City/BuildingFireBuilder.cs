using System.Collections.Generic;
using System.Linq;
using _Project.City;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.Editor.City
{
    /// <summary>
    /// Sets a building on fire, mostly from the bottom up: flames at the base of every wall, fire out of
    /// windows that thins out with height, flames on the roof surface, a dark smoke column, embers and one
    /// flickering light. Every flame is placed on the actual mesh (rays against a temporary mesh collider),
    /// so nothing hangs in the air in front of porches or above pitched roofs.
    /// The effects are authored for 1–3 m props, so every system runs in local space with hierarchy
    /// scaling and is scaled up to the building.
    /// </summary>
    public static class BuildingFireBuilder
    {
        private const string Fx = "Assets/ThirdParty/EpicToonFX/Prefabs/Environment/";
        private const string GroundFire = Fx + "Fire/Cartoon/Field Intense/FireFieldIntenseRed.prefab";
        private const string TallFire = Fx + "Fire/Soft/SoftTallFire/SoftTallFireRed.prefab";
        private const string BodyFire = Fx + "Fire/Soft/SoftFireBig/SoftFireBigRed.prefab";
        private const string SmokeColumn = Fx + "Smoke/Dark/SmokeDark.prefab";
        private const string SmokeSoft = Fx + "Smoke/Dark/SmokeDarkSoft.prefab";
        private const string Embers = Fx + "Fireflies/FireFliesRed.prefab";
        private const string Materials = "Assets/ThirdParty/EpicToonFX/Materials/";
        private const string OwnMaterials = "Assets/_Project/Art/Materials/";

        // Against a bright daytime sky additive flames burn out to white under HDR and bloom, so the
        // flames use alpha-blended copies (they stay orange) and the glows a dimmer additive copy.
        private const float GlowStrength = 0.4f;
        private const float FloorHeight = 3.6f;
        private const float WallSpacing = 3.8f;
        private const int MaxWallFlames = 18;

        private static readonly Vector3[] Sides = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };

        /// <summary>Adds the fire under the building. The building's front faces its local -Z.</summary>
        public static void AddFire(GameObject building, System.Random random)
        {
            var renderers = building.GetComponentsInChildren<MeshRenderer>(true);
            var local = LocalBounds(building, renderers);
            var root = new GameObject("Fire").transform;
            root.SetParent(building.transform, false);
            var top = local.max.y;

            var probe = building.AddComponent<MeshCollider>();
            probe.sharedMesh = building.GetComponent<MeshFilter>().sharedMesh;
            try
            {
                AddWallFire(building.transform, probe, local, root, random);
                AddGroundFire(building.transform, probe, local, root, random);
                AddRoofFire(building.transform, probe, local, root, random);
            }
            finally
            {
                Object.DestroyImmediate(probe);
            }

            // Smoke: one dark column and a softer, wider plume, leaning with a light wind.
            var center = new Vector3(local.center.x, top, local.center.z);
            var smoke = Spawn(SmokeColumn, root, center + Vector3.up * 2f, 9f);
            smoke.transform.rotation = Quaternion.Euler(-78f, 25f, 0f);
            var plume = Spawn(SmokeSoft, root, center + new Vector3(2f, 1f, 1f), 6.5f);
            plume.transform.rotation = Quaternion.Euler(-80f, 25f, 0f);
            Spawn(Embers, root, new Vector3(local.center.x, top * 0.6f, local.center.z), 8f);

            // Effects bring their own lights; one flickering light per building is enough. It sits low in
            // front of the building so the street and the facade glow orange.
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }

            var lightObject = new GameObject("FireLight");
            lightObject.transform.SetParent(root, false);
            lightObject.transform.localPosition = new Vector3(local.center.x, Mathf.Min(4f, top * 0.4f),
                local.min.z - 4f);
            var fireLight = lightObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.5f, 0.15f);
            fireLight.range = 28f;
            fireLight.intensity = 20f;
            fireLight.shadows = LightShadows.None;

            var fire = root.gameObject.AddComponent<BuildingFire>();
            fire.Setup(fireLight, renderers);
        }

        /// <summary>
        /// Flames on all four walls, one storey at a time. The ground floor burns almost everywhere and the
        /// chance falls with height, so the building burns from the bottom up.
        /// </summary>
        private static void AddWallFire(Transform building, Collider probe, Bounds local, Transform root,
            System.Random random)
        {
            var spots = new List<(Vector3 position, int storey)>();
            foreach (var side in Sides)
            {
                var along = side.x != 0f ? Vector3.forward : Vector3.right;
                var min = Vector3.Dot(local.min, along) + 1.2f;
                var max = Vector3.Dot(local.max, along) - 1.2f;
                var count = Mathf.Max(1, Mathf.RoundToInt((max - min) / WallSpacing));
                for (var i = 0; i < count; i++)
                {
                    var u = Mathf.Lerp(min, max, (i + 0.5f) / count) + Jitter(random, 0.6f);
                    for (var storey = 0; ; storey++)
                    {
                        var y = 1.3f + storey * FloorHeight;
                        if (y > local.max.y - 1.5f)
                        {
                            break;
                        }

                        var chance = storey == 0 ? 0.9f : Mathf.Lerp(0.5f, 0.15f, y / local.max.y);
                        if (random.NextDouble() > chance)
                        {
                            continue;
                        }

                        if (TryWall(building, probe, local, side, along * u + Vector3.up * y, out var spot))
                        {
                            spots.Add((spot, storey));
                        }
                    }
                }
            }

            // Keep the cost bounded on big buildings; ground-floor fire wins.
            var chosen = spots.OrderBy(s => s.storey).ThenBy(_ => random.Next()).Take(MaxWallFlames);
            foreach (var (position, storey) in chosen)
            {
                if (storey == 0)
                {
                    Spawn(BodyFire, root, position - Vector3.up * 0.8f, 3.2f + Jitter(random, 0.4f));
                }
                else
                {
                    Spawn(TallFire, root, position - Vector3.up * 1f, 4.5f + Jitter(random, 0.8f));
                }
            }
        }

        /// <summary>Burning ground along the front and the sides: a fire field just outside the walls.</summary>
        private static void AddGroundFire(Transform building, Collider probe, Bounds local, Transform root,
            System.Random random)
        {
            foreach (var side in new[] { Vector3.back, Vector3.left, Vector3.right })
            {
                var along = side.x != 0f ? Vector3.forward : Vector3.right;
                var u = Mathf.Lerp(Vector3.Dot(local.min, along), Vector3.Dot(local.max, along),
                    0.3f + (float)random.NextDouble() * 0.4f);
                if (TryWall(building, probe, local, side, along * u + Vector3.up * 1f, out var spot))
                {
                    var ground = new Vector3(spot.x, 0.05f, spot.z) + side * 1.2f;
                    Spawn(GroundFire, root, ground, 2.4f + Jitter(random, 0.3f));
                }
            }
        }

        /// <summary>Flames standing on the roof surface (pitched or flat), plus a few tall tongues.</summary>
        private static void AddRoofFire(Transform building, Collider probe, Bounds local, Transform root,
            System.Random random)
        {
            var top = local.max.y;
            var columns = Mathf.Clamp(Mathf.RoundToInt(local.size.x / 6f), 1, 3);
            var rows = Mathf.Clamp(Mathf.RoundToInt(local.size.z / 6f), 1, 3);
            var tongues = 0;
            for (var cx = 0; cx < columns; cx++)
            {
                for (var cz = 0; cz < rows; cz++)
                {
                    var x = Mathf.Lerp(local.min.x, local.max.x, (cx + 0.5f) / columns) + Jitter(random, 1f);
                    var z = Mathf.Lerp(local.min.z, local.max.z, (cz + 0.5f) / rows) + Jitter(random, 1f);
                    var y = RoofHeight(probe, building, x, z, top);
                    // Porches, awnings and the ground are not the roof.
                    if (y < top * 0.6f)
                    {
                        continue;
                    }

                    var spot = new Vector3(x, y - 0.4f, z);
                    if (tongues < 2 && random.NextDouble() < 0.5)
                    {
                        Spawn(TallFire, root, spot, 6.5f + Jitter(random, 1f));
                        tongues++;
                    }
                    else
                    {
                        Spawn(BodyFire, root, spot, 3.6f + Jitter(random, 0.5f));
                    }
                }
            }
        }

        /// <summary>
        /// Casts from outside the building toward one wall at a local aim point (its coordinate along the
        /// side's normal is ignored). Returns a spot just outside the wall surface that was hit.
        /// </summary>
        private static bool TryWall(Transform building, Collider probe, Bounds local, Vector3 side, Vector3 aim,
            out Vector3 spot)
        {
            var outward = Vector3.Scale(local.extents, side).magnitude + 4f;
            var start = new Vector3(
                side.x != 0f ? local.center.x : aim.x,
                aim.y,
                side.z != 0f ? local.center.z : aim.z) + side * outward;
            var ray = new Ray(building.TransformPoint(start), building.TransformDirection(-side));
            if (!probe.Raycast(ray, out var hit, outward + 2f) ||
                Vector3.Dot(building.InverseTransformDirection(hit.normal), side) < 0.6f)
            {
                spot = default;
                return false;
            }

            spot = building.InverseTransformPoint(hit.point) + side * 0.6f;
            return true;
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

        /// <summary>
        /// Picks burning buildings, one per block, spread over the city. Low buildings (houses, shops,
        /// garages) read best on fire, so blocks offer one of those; towers only when no low one is left.
        /// </summary>
        public static List<GameObject> PickBuildings(Transform blocksRoot, int count, System.Random random)
        {
            const float minHeight = 6f;
            const float maxLowHeight = 15f;
            var candidates = new List<GameObject>();
            var fallback = new List<GameObject>();
            foreach (Transform block in blocksRoot)
            {
                var buildings = block.GetComponentsInChildren<BoxCollider>(true)
                    .Select(c => c.gameObject)
                    .Where(g => g.name.StartsWith("Building_"))
                    .Select(g => (building: g, height: g.GetComponent<Renderer>().bounds.size.y))
                    .Where(b => b.height > minHeight)
                    .ToList();
                var low = buildings.Where(b => b.height <= maxLowHeight).ToList();
                if (low.Count > 0)
                {
                    candidates.Add(low[random.Next(low.Count)].building);
                }
                else if (buildings.Count > 0)
                {
                    fallback.Add(buildings.OrderBy(b => b.height).First().building);
                }
            }

            if (candidates.Count < count)
            {
                candidates.AddRange(fallback);
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
