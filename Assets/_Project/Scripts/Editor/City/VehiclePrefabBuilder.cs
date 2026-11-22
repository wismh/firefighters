using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Project.FireHose;
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
        public const string OutputFolder = "Assets/_Project/Prefabs/Vehicles";
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
                if (source.name.Contains("fire_truck"))
                {
                    vehicle.SetHoseReel(AddHoseReel(root, bodyRenderers, bodyBounds));
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
