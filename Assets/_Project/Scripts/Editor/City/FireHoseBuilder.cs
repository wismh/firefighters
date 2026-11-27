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
    /// rope, pooled splash prefabs for the installer, and the fire truck the player starts in.
    /// Re-running it replaces what it built before.
    /// </summary>
    public static class FireHoseBuilder
    {
        private const string Fx = "Assets/ThirdParty/EpicToonFX/";
        private const string MuzzleFlash = Fx + "Prefabs/Combat/Muzzleflash/LiquidMuzzle/LiquidMuzzleWater.prefab";
        private const string GroundSplashSource = Fx + "Prefabs/Environment/Water/Flowing/Sharp/WaterSplashSharp.prefab";
        private const string WallSplashSource = Fx + "Prefabs/Combat/Explosions/LiquidExplosion/LiquidExplosionWater.prefab";
        internal const string SoftGlow = Fx + "Materials/Basics/glow1_AB.mat";
        internal const string StretchyDrop = Fx + "Materials/Basics/circlestretchy_AB.mat";
        private const string WaterBody = Fx + "Materials/Misc/Liquid/water_3x3.mat";
        private const string FxFolder = "Assets/_Project/Prefabs/Fx";
        private const string NozzlePrefab = FxFolder + "/HoseNozzle.prefab";
        private const string FireTruckPrefab = VehiclePrefabBuilder.OutputFolder + "/Vehicle_fire_truck.prefab";
        private const int IgnoreRaycastLayer = 2;

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
            var steam = EnsureSteam();
            CitySceneSetup.SetRefs(installer, ("groundSplash", groundSplash), ("wallSplash", wallSplash),
                ("steam", steam));

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
            var oldTrucks = GameObject.Find("FireTrucks");
            if (oldTrucks != null)
            {
                Object.DestroyImmediate(oldTrucks);
            }

            CitySceneSetup.SetRefs(modes, ("nozzle", nozzle), ("hose", hose), ("startVehicle", PlaceStartTruck(layout)));

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

        /// <summary>
        /// A puff of steam where water hits a flame: a dozen soft round blobs (no cloud texture, which reads as
        /// cartoon cauliflower) overlapping at low alpha, so they merge into a hazy veil that swells, rises and fades.
        /// </summary>
        private static PooledParticleFxView EnsureSteam()
        {
            var go = new GameObject("WaterSteam");
            try
            {
                var system = go.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = system.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.3f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.93f, 0.95f, 0.97f, 1f);
                main.gravityModifier = -0.06f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 32;

                var emission = system.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 9, 13) });

                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.5f;

                var velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                // All three axes must use the same curve mode (here: random between two constants).
                velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
                velocity.y = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

                var rotation = system.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

                var size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1.3f), new Keyframe(1f, 2.2f)));

                var color = system.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.3f, 0.12f),
                        new GradientAlphaKey(0.15f, 0.55f), new GradientAlphaKey(0f, 1f),
                    });
                color.color = gradient;

                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SoftGlow);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var view = go.AddComponent<PooledParticleFxView>();
                var so = new SerializedObject(view);
                so.FindProperty("_particleSystem").objectReferenceValue = system;
                so.FindProperty("_maxLifetime").floatValue = 3f;
                so.ApplyModifiedPropertiesWithoutUndo();
                var saved = PrefabUtility.SaveAsPrefabAsset(go, $"{FxFolder}/WaterSteam.prefab");
                return saved.GetComponent<PooledParticleFxView>();
            }
            finally
            {
                Object.DestroyImmediate(go);
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
            AddStreak(core, drop, 0.02f);

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
            AddStreak(spray, drop, 0.015f);
            return core;
        }

        /// <summary>
        /// Streaks the drops with short trails rather than stretched billboards: a stretched billboard draws its tail
        /// behind the drop from the first frame, so fresh drops trailed back past the nozzle and out of the screen,
        /// while a trail only covers the path the drop has flown and so always starts at the muzzle.
        /// </summary>
        private static void AddStreak(ParticleSystem system, Material material, float lifetime)
        {
            var trails = system.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            // A fraction of the drop's lifetime: 0.02 of 2 s is 0.04 s, under a metre at the stream's speed.
            trails.lifetime = lifetime;
            trails.minVertexDistance = 0.1f;
            trails.dieWithParticles = true;
            trails.sizeAffectsWidth = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.4f));
            trails.inheritParticleColor = true;
            trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverTrail = fade;
            system.GetComponent<ParticleSystemRenderer>().trailMaterial = material;
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
        /// The fire truck the player starts in: in the outer lane of the street next to the player start, facing
        /// the traffic direction (right-hand traffic: driving +X keeps to -Z).
        /// </summary>
        /// <summary>
        /// The road direction (±X, ±Z) from <paramref name="node"/> closest to the way to the nearest burning
        /// building: the mission sends the player there first. East when the scene has no fires.
        /// </summary>
        private static Vector3 TowardsNearestFire(Vector3 node)
        {
            var nearest = Vector3.zero;
            var best = float.MaxValue;
            foreach (var fire in Object.FindObjectsByType<BuildingFire>(FindObjectsSortMode.None))
            {
                // The building's collider, not its pivot: SimpleTown pivots sit at a corner.
                var offset = fire.transform.parent.GetComponent<BoxCollider>().bounds.center - node;
                offset.y = 0f;
                if (offset.sqrMagnitude < best)
                {
                    best = offset.sqrMagnitude;
                    nearest = offset;
                }
            }

            if (best == float.MaxValue)
            {
                return Vector3.right;
            }

            Vector3[] roads = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            var chosen = roads[0];
            foreach (var road in roads)
            {
                if (Vector3.Dot(road, nearest) > Vector3.Dot(chosen, nearest))
                {
                    chosen = road;
                }
            }

            return chosen;
        }

        [MenuItem("Tools/City/Aim Start Truck At First Fire")]
        public static void AimStartTruck()
        {
            var truck = GameObject.Find("StartFireTruck");
            AimStartTruck(truck.transform, AssetDatabase.LoadAssetAtPath<CityLayout>(CitySceneSetup.LayoutPath));
            EditorSceneManager.MarkSceneDirty(truck.scene);
            EditorSceneManager.SaveOpenScenes();
        }

        /// <summary>On the road out of the central crossing that leads towards the first call, in its right lane.</summary>
        private static void AimStartTruck(Transform truck, CityLayout layout)
        {
            var node = layout.NodePosition(layout.blocksX / 2, layout.blocksZ / 2);
            var forward = TowardsNearestFire(node);
            var right = Vector3.Cross(Vector3.up, forward);
            var position = node + forward * (CityLayout.HalfRoad + 16f) + right * CityLayout.LaneOffset +
                           Vector3.up * (CityLayout.AsphaltHeight + 0.3f);
            truck.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
            Debug.Log($"[city] start truck faces {forward}");
        }

        private static Vehicle PlaceStartTruck(CityLayout layout)
        {
            var old = GameObject.Find("StartFireTruck");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<Vehicle>(FireTruckPrefab);
            var truck = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject);
            truck.name = "StartFireTruck";
            AimStartTruck(truck.transform, layout);
            var vehicle = truck.GetComponent<Vehicle>();
            var so = new SerializedObject(vehicle);
            so.FindProperty("parkedOnStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return vehicle;
        }
    }
}
