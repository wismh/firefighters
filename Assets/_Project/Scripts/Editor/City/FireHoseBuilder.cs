using System.IO;
using System.Linq;
using _Project.City;
using _Project.FireHose;
using _Project.Player;
using _Project.Vehicles;
using MyToolkit.ParticleFx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>
    /// Adds the fire hose to the city scene: the nozzle with its water stream on the player camera, the hose
    /// rope, pooled splash prefabs for the installer, and parked fire trucks next to the nearest fires.
    /// Re-running it replaces what it built before.
    /// </summary>
    public static class FireHoseBuilder
    {
        private const string Fx = "Assets/ThirdParty/EpicToonFX/";
        private const string MuzzleFlash = Fx + "Prefabs/Combat/Muzzleflash/LiquidMuzzle/LiquidMuzzleWater.prefab";
        private const string GroundSplashSource = Fx + "Prefabs/Environment/Water/Flowing/Sharp/WaterSplashSharp.prefab";
        private const string WallSplashSource = Fx + "Prefabs/Combat/Explosions/LiquidExplosion/LiquidExplosionWater.prefab";
        private const string StretchyDrop = Fx + "Materials/Basics/circlestretchy_AB.mat";
        private const string WaterBody = Fx + "Materials/Misc/Liquid/water_3x3.mat";
        private const string FxFolder = "Assets/_Project/Prefabs/Fx";
        private const string NozzlePrefab = FxFolder + "/HoseNozzle.prefab";
        private const string FireTruckPrefab = VehiclePrefabBuilder.OutputFolder + "/Vehicle_fire_truck.prefab";
        private const int IgnoreRaycastLayer = 2;

        /// <summary>
        /// Truck centre distance from the block edge: the whole truck stands on the sidewalk, so traffic in the outer
        /// lane (kinematic, it would shove the truck) passes about a metre clear of it.
        /// </summary>
        private const float TruckFromBlockEdge = 1.65f;

        private static readonly Color WaterColor = new Color(0.78f, 0.9f, 1f, 0.9f);

        [MenuItem("Tools/City/Setup Fire Hose")]
        public static void SetupFireHose()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[city] exit Play mode first");
                return;
            }

            VehiclePrefabBuilder.BuildAll();
            Apply(AssetDatabase.LoadAssetAtPath<CityLayout>(CitySceneSetup.LayoutPath));
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Builds everything into the open city scene. The vehicle prefabs must already have reels.</summary>
        public static void Apply(CityLayout layout)
        {
            VfxMaterialUpgrade.Upgrade();
            var groundSplash = EnsureSplash(GroundSplashSource, "WaterGroundSplash", 1.2f);
            // The pack's bubbles read as soap up close, so the wall splash keeps only the water.
            var wallSplash = EnsureSplash(WallSplashSource, "WaterWallSplash", 0.7f, "Bubbles", "BubbleSubEmitter");

            var installer = Object.FindFirstObjectByType<CityInstaller>();
            CitySceneSetup.SetRefs(installer, ("groundSplash", groundSplash), ("wallSplash", wallSplash));

            var modes = Object.FindFirstObjectByType<PlayerModeController>();
            var refs = new SerializedObject(modes);
            var camera = (Transform)refs.FindProperty("cameraTransform").objectReferenceValue;
            var walker = (FirstPersonController)refs.FindProperty("walker").objectReferenceValue;
            DestroyChild(camera, "HoseNozzle");
            DestroyChild(modes.transform, "Hose");
            // The hose and the water rays use the default raycast layers, so they never hit the player's capsule.
            walker.gameObject.layer = IgnoreRaycastLayer;

            var nozzle = CreateNozzle(camera);
            var hose = CreateHose(modes.transform);
            CitySceneSetup.SetRefs(modes, ("nozzle", nozzle), ("hose", hose));

            PlaceFireTrucks(layout, walker.transform.position);
            Debug.Log("[city] fire hose set up");
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var old = parent.Find(name);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
        }

        private static PooledParticleFxView EnsureSplash(string sourcePath, string name, float scale,
            params string[] dropped)
        {
            Directory.CreateDirectory(FxFolder);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath));
            try
            {
                instance.name = name;
                instance.transform.localScale = Vector3.one * scale;
                foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.playOnAwake = false;
                    main.loop = false;
                    if (dropped.Contains(system.name))
                    {
                        system.gameObject.SetActive(false);
                    }
                }

                var view = instance.AddComponent<PooledParticleFxView>();
                var so = new SerializedObject(view);
                so.FindProperty("_particleSystem").objectReferenceValue = instance.GetComponent<ParticleSystem>();
                so.FindProperty("_maxLifetime").floatValue = 3f;
                so.ApplyModifiedPropertiesWithoutUndo();
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, $"{FxFolder}/{name}.prefab");
                return saved.GetComponent<PooledParticleFxView>();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static WaterNozzle CreateNozzle(Transform camera)
        {
            var root = new GameObject("HoseNozzle").transform;
            root.SetParent(camera, false);
            var localPosition = new Vector3(0.26f, -0.26f, 0.5f);
            // Aimed so the stream crosses the crosshair about 15 m ahead before gravity bends it down.
            root.SetLocalPositionAndRotation(localPosition,
                Quaternion.LookRotation(new Vector3(0f, 0f, 15f) - localPosition, Vector3.up));

            var brass = CitySceneSetup.EnsureFlatMaterial("HoseNozzleBrass", new Color(0.8f, 0.62f, 0.25f));
            var dark = CitySceneSetup.EnsureFlatMaterial("HoseNozzleGrip", new Color(0.12f, 0.12f, 0.13f));
            AddCylinder(root, "Grip", new Vector3(0f, 0f, -0.04f), new Vector3(0.075f, 0.12f, 0.075f), dark);
            AddCylinder(root, "Barrel", new Vector3(0f, 0f, 0.15f), new Vector3(0.05f, 0.1f, 0.05f), brass);
            AddCylinder(root, "Tip", new Vector3(0f, 0f, 0.27f), new Vector3(0.035f, 0.025f, 0.035f), dark);

            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(root, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.3f);
            var hoseEnd = new GameObject("HoseEnd").transform;
            hoseEnd.SetParent(root, false);
            hoseEnd.localPosition = new Vector3(0f, 0f, -0.16f);

            var stream = CreateStream(muzzle);
            var flash = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MuzzleFlash), muzzle);
            flash.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            flash.transform.localScale = Vector3.one * 0.25f;
            foreach (var system in flash.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.playOnAwake = false;
            }

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = IgnoreRaycastLayer;
            }

            var nozzle = root.gameObject.AddComponent<WaterNozzle>();
            CitySceneSetup.SetRefs(nozzle, ("muzzle", muzzle), ("hoseEnd", hoseEnd), ("stream", stream),
                ("muzzleFlash", flash.GetComponent<ParticleSystem>()));
            root.gameObject.SetActive(false);
            // A prefab keeps the particle systems' long YAML out of the scene file.
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, NozzlePrefab, InteractionMode.AutomatedAction);
            return nozzle;
        }

        private static void AddCylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.SetLocalPositionAndRotation(position, Quaternion.Euler(90f, 0f, 0f));
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Three layers, all in world space so the water keeps flying when the player turns: a stretched core of
        /// drops, a thicker body of animated water blobs, and a fine spray that breaks off the sides.
        /// Speed and gravity match <see cref="WaterNozzle"/>, so the traced arc lands where the particles do.
        /// </summary>
        private static ParticleSystem CreateStream(Transform muzzle)
        {
            var drop = AssetDatabase.LoadAssetAtPath<Material>(StretchyDrop);
            var body = AssetDatabase.LoadAssetAtPath<Material>(WaterBody);

            var core = CreateSystem("Stream", muzzle, drop, rate: 260f, speed: (20.5f, 22.5f), size: (0.06f, 0.1f),
                angle: 1f, growth: 5f, alpha: 0.95f);
            var coreRenderer = core.GetComponent<ParticleSystemRenderer>();
            coreRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            coreRenderer.velocityScale = 0.035f;
            coreRenderer.lengthScale = 1.5f;

            var blobs = CreateSystem("Body", core.transform, body, rate: 70f, speed: (20f, 22.5f), size: (0.1f, 0.18f),
                angle: 2f, growth: 7f, alpha: 0.5f);
            var sheet = blobs.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 3;
            sheet.numTilesY = 3;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 8f);
            var rotation = blobs.main;
            rotation.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var spray = CreateSystem("Spray", core.transform, drop, rate: 90f, speed: (13f, 21f), size: (0.04f, 0.09f),
                angle: 5f, growth: 1.5f, alpha: 0.9f);
            var sprayRenderer = spray.GetComponent<ParticleSystemRenderer>();
            sprayRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sprayRenderer.velocityScale = 0.03f;
            sprayRenderer.lengthScale = 1f;
            return core;
        }

        private static ParticleSystem CreateSystem(string name, Transform parent, Material material, float rate,
            (float min, float max) speed, (float min, float max) size, float angle, float growth, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 2f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.min, speed.max);
            main.startSize = new ParticleSystem.MinMaxCurve(size.min, size.max);
            main.startColor = new Color(WaterColor.r, WaterColor.g, WaterColor.b, alpha);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.maxParticles = 1500;

            var emission = system.emission;
            emission.rateOverTime = rate;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = 0.02f;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, growth));

            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            // Drops die where they hit, so the stream never passes through walls; the splash comes from the arc trace.
            var collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.lifetimeLoss = 1f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.collidesWith = Physics.DefaultRaycastLayers;
            collision.radiusScale = 0.3f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return system;
        }

        private static HoseRope CreateHose(Transform player)
        {
            var go = new GameObject("Hose");
            go.transform.SetParent(player, false);
            go.layer = IgnoreRaycastLayer;
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = CitySceneSetup.EnsureFlatMaterial("HoseRubber", new Color(0.55f, 0.1f, 0.08f));
            line.widthMultiplier = 0.11f;
            line.numCornerVertices = 3;
            line.numCapVertices = 2;
            line.generateLightingData = true;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            line.enabled = false;
            var rope = go.AddComponent<HoseRope>();
            CitySceneSetup.SetRefs(rope, ("line", line));
            return rope;
        }

        /// <summary>
        /// One parked fire truck per burning building closest to the player start, along the building's street,
        /// right side (with the reel) towards the building. Slides along the sidewalk until it clears lamps and props.
        /// </summary>
        private static void PlaceFireTrucks(CityLayout layout, Vector3 playerStart)
        {
            var old = GameObject.Find("FireTrucks");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<Vehicle>(FireTruckPrefab);
            var root = new GameObject("FireTrucks").transform;
            var fires = Object.FindObjectsByType<BuildingFire>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(f => Vector3.Distance(f.transform.position, playerStart))
                .Take(layout.parkedFireTrucks);
            Physics.SyncTransforms();
            foreach (var fire in fires)
            {
                var building = fire.transform.parent;
                if (TryFindSpot(building, prefab, out var position, out var rotation))
                {
                    var truck = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, root);
                    truck.name = "FireTruck_" + building.name;
                    truck.transform.SetPositionAndRotation(position, rotation);
                    var so = new SerializedObject(truck.GetComponent<Vehicle>());
                    so.FindProperty("parkedOnStart").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Physics.SyncTransforms();
                }
                else
                {
                    Debug.LogWarning($"[city] no free kerb for a fire truck at {building.name}");
                }
            }
        }

        private static bool TryFindSpot(Transform building, Vehicle truck, out Vector3 position, out Quaternion rotation)
        {
            var front = Snap(building.rotation * Vector3.back);
            var along = new Vector3(-front.z, 0f, front.x);
            var block = building.parent.Find("GroundCollider").GetComponent<BoxCollider>().bounds;
            var abs = new Vector3(Mathf.Abs(front.x), 0f, Mathf.Abs(front.z));
            var edge = Vector3.Dot(block.center, front) + Vector3.Dot(block.extents, abs);
            var blockAlong = Vector3.Dot(block.center, along);
            var blockHalf = Vector3.Dot(block.extents, new Vector3(abs.z, 0f, abs.x));
            var margin = truck.HalfLength + 1f;
            var buildingAlong = Vector3.Dot(building.GetComponent<Renderer>().bounds.center, along);

            // Right side (+X, the reel) faces the building.
            var right = -front;
            rotation = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
            var halfExtents = new Vector3(truck.Width * 0.47f, 1.2f, truck.HalfLength);
            for (var i = 0; i < 16; i++)
            {
                var shift = (i + 1) / 2 * 2f * (i % 2 == 0 ? 1f : -1f);
                var t = Mathf.Clamp(buildingAlong + shift, blockAlong - blockHalf + margin, blockAlong + blockHalf - margin);
                position = front * (edge + TruckFromBlockEdge) + along * t + Vector3.up * 0.15f;
                if (!Physics.CheckBox(position + Vector3.up * 1.6f, halfExtents, rotation, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    return true;
                }
            }

            position = default;
            return false;
        }

        private static Vector3 Snap(Vector3 direction)
        {
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.z)
                ? new Vector3(Mathf.Sign(direction.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(direction.z));
        }
    }
}
