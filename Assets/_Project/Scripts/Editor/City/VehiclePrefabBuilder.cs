using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Project.Comedy;
using _Project.FireHose;
using _Project.Lighting;
using _Project.Vehicles;
using UnityEditor;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>
    /// Wraps SimpleTown "*_seperate" car models (wheels are separate meshes) into game vehicle prefabs:
    /// Rigidbody, body collider, WheelColliders, <see cref="Vehicle"/> and <see cref="VehicleController"/>.
    /// The source prefab stays nested, so pack updates still flow in.
    /// </summary>
    public static class VehiclePrefabBuilder
    {
        public const string OutputFolder = ProjectPaths.Prefabs + "/Vehicles";
        private const string SourceFolder = "Assets/ThirdParty/SimpleTown/Prefabs/Vehicles/";
        private const float BaseMass = 1200f;
        private const float SuspensionDistance = 0.15f;

        private static readonly (string source, float mass)[] Sources =
        {
            ("car_seperate_blue", 1200f),
            ("car_seperate_green", 1200f),
            ("car_seperate_red", 1200f),
            ("taxi_seperate_mesh", 1250f),
            ("cop_seperate_mesh", 1300f),
            ("van_seperate_blue", 1600f),
            ("van_seperate_green", 1600f),
            ("van_seperate_red", 1600f),
            ("ute_seperate_blue", 1500f),
            ("ute_seperate_red", 1500f),
            ("ute_seperate_yellow", 1500f),
            ("ute_empty_seperate_blue", 1400f),
            ("ambo_seperate", 2200f),
            ("bus_seperate_blue", 4000f),
            ("fire_truck_seperate_mesh", 4500f),
            ("rubbish_truck_seperate_mesh", 4500f),
        };

        [MenuItem("Tools/City/Build Vehicle Prefabs")]
        public static List<Vehicle> BuildAll()
        {
            Directory.CreateDirectory(OutputFolder);
            DeformableMeshBuilder.Reset();
            var result = new List<Vehicle>();
            foreach (var (source, mass) in Sources)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourceFolder + source + ".prefab");
                if (prefab == null)
                {
                    Debug.LogError($"[city] vehicle source not found: {source}");
                    continue;
                }

                result.Add(Build(prefab, mass));
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[city] built {result.Count} vehicle prefabs in {OutputFolder}");
            return result;
        }

        private static Vehicle Build(GameObject source, float mass)
        {
            var name = "Vehicle_" + source.name.Replace("_seperate", string.Empty).Replace("_mesh", string.Empty);
            var root = new GameObject(name);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                model.name = "Model";
                // SimpleTown cars face -Z; the vehicle faces +Z.
                model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

                var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
                var wheelRenderers = renderers.Where(r => r.name.Contains("_wheel")).ToArray();
                var bodyRenderers = renderers.Except(wheelRenderers).ToArray();
                var bodyBounds = Encapsulate(bodyRenderers);
                var radius = wheelRenderers.Max(r => r.bounds.size.y) * 0.5f;
                var massScale = mass / BaseMass;

                var body = root.AddComponent<Rigidbody>();
                body.mass = mass;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.automaticCenterOfMass = false;
                body.centerOfMass = new Vector3(0f, radius * 1.3f, 0f);

                var box = root.AddComponent<BoxCollider>();
                var bottom = Mathf.Max(bodyBounds.min.y, radius * 1.2f);
                box.center = new Vector3(bodyBounds.center.x, (bottom + bodyBounds.max.y) * 0.5f, bodyBounds.center.z);
                box.size = new Vector3(bodyBounds.size.x * 0.95f, bodyBounds.max.y - bottom, bodyBounds.size.z);

                var wheelsRoot = new GameObject("WheelColliders").transform;
                wheelsRoot.SetParent(root.transform, false);
                var maxZ = wheelRenderers.Max(r => r.bounds.center.z);
                var wheels = new List<VehicleWheel>();
                foreach (var wheelRenderer in wheelRenderers)
                {
                    var center = wheelRenderer.bounds.center;
                    var front = center.z > 0f && Mathf.Approximately(center.z, maxZ);
                    var colliderObject = new GameObject("WC_" + wheelRenderer.name);
                    colliderObject.transform.SetParent(wheelsRoot, false);
                    colliderObject.transform.localPosition = center + Vector3.up * (SuspensionDistance * 0.5f);
                    var wheelCollider = colliderObject.AddComponent<WheelCollider>();
                    wheelCollider.radius = wheelRenderer.bounds.size.y * 0.5f;
                    wheelCollider.mass = 20f * massScale;
                    wheelCollider.suspensionDistance = SuspensionDistance;
                    wheelCollider.forceAppPointDistance = 0.1f;
                    wheelCollider.suspensionSpring = new JointSpring
                    {
                        spring = 35000f * massScale,
                        damper = 4500f * massScale,
                        targetPosition = 0.5f,
                    };
                    var sideways = wheelCollider.sidewaysFriction;
                    sideways.stiffness = 1.6f;
                    wheelCollider.sidewaysFriction = sideways;
                    var forward = wheelCollider.forwardFriction;
                    forward.stiffness = 1.4f;
                    wheelCollider.forwardFriction = forward;

                    wheels.Add(new VehicleWheel
                    {
                        collider = wheelCollider,
                        visual = wheelRenderer.transform,
                        steer = front,
                        drive = !front,
                        handbrake = !front,
                    });
                }

                var controller = root.AddComponent<VehicleController>();
                controller.Setup(body, wheels.ToArray(), massScale);
                controller.enabled = false;

                var exit = new GameObject("ExitPoint").transform;
                exit.SetParent(root.transform, false);
                exit.localPosition = new Vector3(-(bodyBounds.extents.x + 0.9f), 0.1f, bodyBounds.center.z + 0.3f);

                var cameraTarget = new GameObject("CameraTarget").transform;
                cameraTarget.SetParent(root.transform, false);
                cameraTarget.localPosition = new Vector3(0f, bodyBounds.max.y + 0.3f, 0f);

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.Setup(body, controller, exit, cameraTarget, bodyBounds.size.z, bodyBounds.size.x);
                vehicle.SetDamage(AddDamage(root, bodyRenderers, bodyBounds));
                AddLights(root, vehicle, bodyRenderers, bodyBounds);
                if (source.name.Contains("fire_truck"))
                {
                    vehicle.SetHoseReel(AddHoseReel(root, bodyRenderers, bodyBounds));
                    AddSiren(root, vehicle, bodyRenderers, bodyBounds);
                }

                var path = $"{OutputFolder}/{name}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                return saved.GetComponent<Vehicle>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A red drum on the right side near the back, sitting on the body panel. The body bounds can't place it:
        /// the cab's mirrors make them half a metre wider than the box behind, which left the drum floating beside
        /// the truck, so a ray finds the panel itself.
        /// </summary>
        private static FireHoseReel AddHoseReel(GameObject root, IReadOnlyList<MeshRenderer> bodyRenderers,
            Bounds bodyBounds)
        {
            // A primitive cylinder is 2 units tall, so its Y scale is the drum's half thickness.
            const float drumHalfThickness = 0.08f;
            const float hoseOffset = 0.04f;
            var height = bodyBounds.min.y + bodyBounds.size.y * 0.35f;
            var z = bodyBounds.min.z + 1.4f;
            var side = FindSide(bodyRenderers, new Vector3(bodyBounds.max.x + 1f, height, z), bodyBounds.max.x);

            var attach = new GameObject("HoseReel").transform;
            attach.SetParent(root.transform, false);
            attach.localPosition = new Vector3(side + drumHalfThickness * 2f + hoseOffset, height, z);

            var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            drum.name = "Drum";
            Object.DestroyImmediate(drum.GetComponent<Collider>());
            drum.transform.SetParent(attach, false);
            drum.transform.SetLocalPositionAndRotation(new Vector3(-(drumHalfThickness + hoseOffset), 0f, 0f),
                Quaternion.Euler(0f, 0f, 90f));
            drum.transform.localScale = new Vector3(0.7f, drumHalfThickness, 0.7f);
            drum.GetComponent<MeshRenderer>().sharedMaterial =
                CitySceneSetup.EnsureFlatMaterial("HoseReel", new Color(0.75f, 0.12f, 0.1f));

            var reel = root.AddComponent<FireHoseReel>();
            reel.Setup(attach);
            return reel;
        }

        /// <summary>
        /// Two headlights with one beam between them and two tail lights, each stuck to the body where a ray from
        /// the front or the back finds it.
        /// </summary>
        private static void AddLights(GameObject root, Vehicle vehicle, IReadOnlyList<MeshRenderer> bodyRenderers,
            Bounds bodyBounds)
        {
            const float lampSize = 0.34f;
            var height = bodyBounds.min.y + bodyBounds.size.y * 0.3f;
            var lights = new GameObject("Lights").transform;
            lights.SetParent(root.transform, false);
            var head = CityNightBuilder.EnsureGlow("Headlight", new Color(4.5f, 4.1f, 3.2f));
            var tail = CityNightBuilder.EnsureGlow("TailLight", new Color(3f, 0.15f, 0.1f));
            var lamps = new List<Renderer>();
            foreach (var x in new[] { -0.33f * bodyBounds.size.x, 0.33f * bodyBounds.size.x })
            {
                var front = new Vector3(x, height, bodyBounds.max.z + 1f);
                var back = new Vector3(x, height, bodyBounds.min.z - 1f);
                front.z = FindEnd(bodyRenderers, front, Vector3.back, bodyBounds.max.z);
                back.z = FindEnd(bodyRenderers, back, Vector3.forward, bodyBounds.min.z);
                lamps.Add(AddLamp(lights, front, lampSize, head));
                lamps.Add(AddLamp(lights, back, lampSize * 0.8f, tail));
            }

            var beam = new GameObject("Beam").AddComponent<Light>();
            beam.transform.SetParent(lights, false);
            beam.transform.localPosition = new Vector3(0f, height + 0.2f, bodyBounds.max.z + 0.3f);
            beam.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
            beam.type = LightType.Spot;
            beam.color = new Color(1f, 0.93f, 0.78f);
            beam.spotAngle = 75f;
            beam.innerSpotAngle = 30f;
            beam.range = 32f;
            beam.intensity = 220f;
            beam.shadows = LightShadows.None;
            root.AddComponent<VehicleLights>().Setup(vehicle, beam, lamps.ToArray());
        }

        private static Renderer AddLamp(Transform parent, Vector3 position, float size, Material material)
        {
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = material.name;
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = position;
            lamp.transform.localScale = new Vector3(size, size * 0.7f, 0.12f);
            var renderer = lamp.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        /// <summary>Local Z of the body surface a ray hits; <paramref name="fallback"/> if none.</summary>
        private static float FindEnd(IReadOnlyList<MeshRenderer> bodyRenderers, Vector3 outside, Vector3 direction,
            float fallback)
        {
            var colliders = new List<MeshCollider>();
            foreach (var renderer in bodyRenderers)
            {
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                colliders.Add(collider);
            }

            Physics.SyncTransforms();
            var end = fallback;
            var nearest = 3f;
            foreach (var collider in colliders)
            {
                if (collider.Raycast(new Ray(outside, direction), out var hit, nearest))
                {
                    nearest = hit.distance;
                    end = hit.point.z;
                }
            }

            foreach (var collider in colliders)
            {
                Object.DestroyImmediate(collider);
            }

            return end;
        }

        /// <summary>
        /// A red and a blue beacon side by side on the cab roof, each with a point light that lights the street
        /// around the truck while it flashes.
        /// </summary>
        private static void AddSiren(GameObject root, Vehicle vehicle, IReadOnlyList<MeshRenderer> bodyRenderers,
            Bounds bodyBounds)
        {
            const float lampSize = 0.32f;
            var z = bodyBounds.max.z - 1.2f;
            var roof = FindTop(bodyRenderers, new Vector3(0f, bodyBounds.max.y + 2f, z), bodyBounds.max.y);
            var siren = new GameObject("Siren").transform;
            siren.SetParent(root.transform, false);
            siren.localPosition = new Vector3(0f, roof + lampSize * 0.5f, z);

            var red = AddBeacon(siren, "Red", -0.4f, new Color(1f, 0.15f, 0.1f), lampSize, out var redLight);
            var blue = AddBeacon(siren, "Blue", 0.4f, new Color(0.15f, 0.35f, 1f), lampSize, out var blueLight);
            root.AddComponent<SirenLights>().Setup(vehicle, redLight, blueLight, red, blue);
        }

        private static Renderer AddBeacon(Transform siren, string name, float x, Color color, float size,
            out Light light)
        {
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.name = name;
            Object.DestroyImmediate(lamp.GetComponent<Collider>());
            lamp.transform.SetParent(siren, false);
            lamp.transform.localPosition = new Vector3(x, 0f, 0f);
            lamp.transform.localScale = new Vector3(size, size * 0.8f, size);
            var renderer = lamp.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = CitySceneSetup.EnsureUnlitMaterial("SirenLamp", Color.white);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            light = new GameObject(name + "Light").AddComponent<Light>();
            light.transform.SetParent(lamp.transform.parent, false);
            light.transform.localPosition = new Vector3(x, 0.4f, 0f);
            light.type = LightType.Point;
            light.color = color;
            light.range = 14f;
            light.intensity = 6f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return renderer;
        }

        /// <summary>
        /// Local X of the body surface hit by a ray cast from <paramref name="outside"/> towards the centre line,
        /// against throwaway mesh colliders on the body; <paramref name="fallback"/> if nothing is hit.
        /// The vehicle root sits at the origin unrotated while it is built, so world and local space agree.
        /// </summary>
        private static float FindSide(IReadOnlyList<MeshRenderer> bodyRenderers, Vector3 outside, float fallback)
        {
            var colliders = new List<MeshCollider>();
            foreach (var renderer in bodyRenderers)
            {
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                colliders.Add(collider);
            }

            Physics.SyncTransforms();
            var ray = new Ray(outside, Vector3.left);
            var side = fallback;
            var nearest = float.MaxValue;
            foreach (var collider in colliders)
            {
                if (collider.Raycast(ray, out var hit, 10f) && hit.distance < nearest)
                {
                    nearest = hit.distance;
                    side = hit.point.x;
                }
            }

            foreach (var collider in colliders)
            {
                Object.DestroyImmediate(collider);
            }

            if (nearest == float.MaxValue)
            {
                Debug.LogWarning($"[city] hose reel: no body panel found on {bodyRenderers[0].name}, using bounds");
            }

            return side;
        }

        private static VehicleDamage AddDamage(GameObject root, IReadOnlyList<MeshRenderer> bodyRenderers,
            Bounds bodyBounds)
        {
            // SimpleTown bodies are one mesh each; the hose reel drum is added after this.
            var filter = bodyRenderers[0].GetComponent<MeshFilter>();
            var intact = filter.sharedMesh;
            var damage = root.AddComponent<VehicleDamage>();
            // Dent sizes are tuned on the 5.3 m car; a bus or a truck dents in proportion.
            var sizeScale = Mathf.Max(1f, bodyBounds.size.z / 5.3f);
            damage.Setup(filter, intact, DeformableMeshBuilder.Ensure(intact), sizeScale, AddSparks(root),
                AddSmoke(root, bodyRenderers, bodyBounds, sizeScale));
            return damage;
        }

        /// <summary>Short yellow streaks that fly off a dent and drop; emitted by hand, so nothing plays alone.</summary>
        private static ParticleSystem AddSparks(GameObject root)
        {
            var go = new GameObject("DentSparks");
            go.transform.SetParent(root.transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.6f, 0.15f));
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.15f;

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.04f;
            renderer.lengthScale = 1f;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FireHoseBuilder.StretchyDrop);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        /// <summary>
        /// Dark smoke from under the bonnet of a battered car: soft blobs, like the hose steam but grey, rising and
        /// trailing behind in world space while the car drives on.
        /// </summary>
        private static ParticleSystem AddSmoke(GameObject root, IReadOnlyList<MeshRenderer> bodyRenderers,
            Bounds bodyBounds, float sizeScale)
        {
            // On the body surface near the front: the bonnet of a car, the cab roof of a cab-over truck.
            var z = bodyBounds.max.z - 0.9f * sizeScale;
            var top = FindTop(bodyRenderers, new Vector3(0f, bodyBounds.max.y + 1f, z), bodyBounds.max.y);
            var go = new GameObject("DamageSmoke");
            go.transform.SetParent(root.transform, false);
            go.transform.SetLocalPositionAndRotation(new Vector3(0f, top + 0.1f, z), Quaternion.Euler(-90f, 0f, 0f));
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f * sizeScale, 1.1f * sizeScale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            main.gravityModifier = -0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;

            var emission = system.emission;
            emission.rateOverTime = 0f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.4f * sizeScale;

            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1.2f), new Keyframe(1f, 2.6f)));

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.1f),
                    new GradientAlphaKey(0.25f, 0.6f), new GradientAlphaKey(0f, 1f),
                });
            color.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(FireHoseBuilder.SoftGlow);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        /// <summary>Local Y of the body surface under <paramref name="above"/>; <paramref name="fallback"/> if none.</summary>
        private static float FindTop(IReadOnlyList<MeshRenderer> bodyRenderers, Vector3 above, float fallback)
        {
            var colliders = new List<MeshCollider>();
            foreach (var renderer in bodyRenderers)
            {
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                colliders.Add(collider);
            }

            Physics.SyncTransforms();
            var ray = new Ray(above, Vector3.down);
            var top = fallback;
            var nearest = float.MaxValue;
            foreach (var collider in colliders)
            {
                if (collider.Raycast(ray, out var hit, 20f) && hit.distance < nearest)
                {
                    nearest = hit.distance;
                    top = hit.point.y;
                }
            }

            foreach (var collider in colliders)
            {
                Object.DestroyImmediate(collider);
            }

            return top;
        }

        private static Bounds Encapsulate(IReadOnlyList<Renderer> renderers)
        {
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Count; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }
    }
}
