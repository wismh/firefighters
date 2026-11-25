using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Project.City;
using _Project.Pedestrians;
using _Project.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace _Project.Editor.City
{
    /// <summary>
    /// Adds pedestrians to the city scene: imports the stickman pack and the Mixamo clips as humanoid, builds the
    /// animator and a ragdoll prefab per character, the ghost, the physics layer and the scene object.
    /// Re-running it replaces what it built before.
    /// </summary>
    public static class PedestrianBuilder
    {
        private const string Pack = "Assets/ThirdParty/StickmanFantasyPack/";
        private const string Model = Pack + "Models/Stickman-Pack.fbx";
        private const string BodyMaterial = Pack + "Materials/Stickman-Material.mat";
        private const string ClipFolder = "Assets/_Project/Art/Animations/Pedestrians/";
        private const string ControllerPath = ClipFolder + "Pedestrian.controller";
        private const string GhostMaterialPath = "Assets/_Project/Art/Materials/Ghost.mat";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Pedestrians";
        private const string GhostMesh = "Monster-0";
        private const string Bone = "mixamorig:";
        private const string LayerName = "Pedestrian";
        private const int GhostCount = 4;
        private const float CapsuleHeight = 1.7f;
        // The pack's people are 1.7 m; the town is built around a 2.4 m player and 3 m high cars.
        private const float Scale = 1.75f;

        // The pack's everyday looks; its cloaks, horns and crowns stay out of town.
        private static readonly (string mesh, string name)[] Characters =
        {
            ("Character-1", "Brown"), ("Character-4", "Jogger"), ("Character-5", "Spiky"), ("Character-6", "Punk"),
            ("Character-13", "Hoodie"), ("Monster-6", "Karate"),
        };

        // Clip, playback speed (stride matched to the walking speed by eye), loops.
        private static readonly (string name, float speed, bool loop)[] Clips =
        {
            ("Walk", 1.4f, true), ("Idle", 1f, true), ("Run", 1.4f, true), ("FallBack", 1.3f, false),
            ("GetUp", 1.1f, false), ("Yell", 1f, true), ("Clap", 1f, true), ("Dance", 1f, true),
        };

        // Bone, the child it points at, radius in metres, mass, the bone its joint hangs on.
        private static readonly (string bone, string tip, float radius, float mass, string parent)[] RagdollParts =
        {
            ("Hips", "Spine2", 0.14f, 12f, null), ("Spine2", "Neck", 0.16f, 12f, "Hips"),
            ("Head", "HeadTop_End", 0.22f, 6f, "Spine2"),
            ("LeftArm", "LeftForeArm", 0.07f, 3f, "Spine2"), ("LeftForeArm", "LeftHand", 0.06f, 2f, "LeftArm"),
            ("RightArm", "RightForeArm", 0.07f, 3f, "Spine2"), ("RightForeArm", "RightHand", 0.06f, 2f, "RightArm"),
            ("LeftUpLeg", "LeftLeg", 0.09f, 5f, "Hips"), ("LeftLeg", "LeftFoot", 0.08f, 4f, "LeftUpLeg"),
            ("RightUpLeg", "RightLeg", 0.09f, 5f, "Hips"), ("RightLeg", "RightFoot", 0.08f, 4f, "RightUpLeg"),
        };

        [MenuItem("Tools/City/Setup Pedestrians")]
        public static void SetupPedestrians()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[city] exit Play mode first");
                return;
            }

            Apply();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Builds everything into the open city scene.</summary>
        public static void Apply()
        {
            EnsureLayer();
            ImportModels();
            Directory.CreateDirectory(PrefabFolder);
            var controller = BuildController();
            var prefabs = Characters.Select(c => BuildCharacter(c.mesh, c.name, controller)).ToList();
            var ghost = BuildGhost();

            var old = GameObject.Find("Pedestrians");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var system = new GameObject("Pedestrians").AddComponent<PedestrianSystem>();
            var modes = Object.FindFirstObjectByType<PlayerModeController>();
            var camera = (Transform)new SerializedObject(modes).FindProperty("cameraTransform").objectReferenceValue;
            CitySceneSetup.SetRefs(system, ("modes", modes), ("viewCamera", camera.GetComponent<Camera>()));
            var ghosts = new List<Object>();
            for (var i = 0; i < GhostCount; i++)
            {
                ghosts.Add(PrefabUtility.InstantiatePrefab(ghost, system.transform));
            }

            SetArray(system, "prefabs", prefabs);
            SetArray(system, "ghosts", ghosts);

            var vehicleCamera = Object.FindFirstObjectByType<VehicleCamera>(FindObjectsInactive.Include);
            var so = new SerializedObject(vehicleCamera);
            so.FindProperty("obstacleMask").intValue &= ~(1 << PhysicsLayers.Pedestrian);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[city] pedestrians set up: {prefabs.Count} characters");
        }

        private static void SetArray(Object target, string field, IReadOnlyList<Object> values)
        {
            var so = new SerializedObject(target);
            var array = so.FindProperty(field);
            array.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureLayer()
        {
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            manager.FindProperty("layers").GetArrayElementAtIndex(PhysicsLayers.Pedestrian).stringValue = LayerName;
            manager.ApplyModifiedPropertiesWithoutUndo();
            for (var layer = 0; layer < 32; layer++)
            {
                Physics.IgnoreLayerCollision(PhysicsLayers.Pedestrian, layer, true);
            }

            AssetDatabase.SaveAssets();
        }

        private static void ImportModels()
        {
            var model = (ModelImporter)AssetImporter.GetAtPath(Model);
            if (model.animationType != ModelImporterAnimationType.Human)
            {
                model.animationType = ModelImporterAnimationType.Human;
                model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                model.importAnimation = false;
                model.SaveAndReimport();
            }

            foreach (var (name, _, loop) in Clips)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(ClipFolder + name + ".fbx");
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.name = name;
                    clip.loopTime = loop;
                    // The code moves the body, so all of the clip's own motion stays in the pose.
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
        }

        private static AnimatorController BuildController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            foreach (var (name, speed, _) in Clips)
            {
                var state = machine.AddState(name);
                state.motion = AssetDatabase.LoadAllAssetsAtPath(ClipFolder + name + ".fbx").OfType<AnimationClip>()
                    .First(c => !c.name.StartsWith("__preview"));
                state.speed = speed;
            }

            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>The pack's model with every mesh but one removed, unpacked and standing at the origin.</summary>
        private static GameObject CreateBody(string mesh, string name, Material material)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = name;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name != mesh)
                {
                    Object.DestroyImmediate(renderer.gameObject);
                    continue;
                }

                renderer.sharedMaterial = material;
            }

            return root;
        }

        private static Pedestrian BuildCharacter(string mesh, string name, AnimatorController controller)
        {
            var root = CreateBody(mesh, "Pedestrian_" + name, AssetDatabase.LoadAssetAtPath<Material>(BodyMaterial));
            try
            {
                root.layer = PhysicsLayers.Pedestrian;
                var animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = CapsuleHeight;
                capsule.radius = 0.3f;
                capsule.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);

                var all = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(Bone)).ToArray();
                var byName = all.ToDictionary(t => t.name.Substring(Bone.Length));
                var bodies = new List<Rigidbody>();
                var colliders = new List<Collider>();
                foreach (var part in RagdollParts)
                {
                    AddRagdollPart(byName, part, bodies, colliders);
                }

                var pedestrian = root.AddComponent<Pedestrian>();
                pedestrian.Setup(animator, capsule, byName["Hips"], byName["Head"], bodies.ToArray(),
                    colliders.ToArray(), all);
                root.transform.localScale = Vector3.one * Scale;
                var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{root.name}.prefab");
                return saved.GetComponent<Pedestrian>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AddRagdollPart(IReadOnlyDictionary<string, Transform> bones,
            (string bone, string tip, float radius, float mass, string parent) part, List<Rigidbody> bodies,
            List<Collider> colliders)
        {
            var bone = bones[part.bone];
            bone.gameObject.layer = PhysicsLayers.Debris;
            var scale = bone.lossyScale.x;
            // A missing tip is a leaf bone the export dropped: the head then gets a ball above its joint.
            var tip = bones.TryGetValue(part.tip, out var tipBone)
                ? bone.InverseTransformPoint(tipBone.position)
                : bone.InverseTransformPoint(bone.position + Vector3.up * (part.radius * 2f));

            var capsule = bone.gameObject.AddComponent<CapsuleCollider>();
            capsule.center = tip * 0.5f;
            capsule.radius = part.radius / scale;
            capsule.height = Mathf.Max(tip.magnitude, capsule.radius * 2f);
            var axis = new Vector3(Mathf.Abs(tip.x), Mathf.Abs(tip.y), Mathf.Abs(tip.z));
            capsule.direction = axis.x > axis.y && axis.x > axis.z ? 0 : axis.y > axis.z ? 1 : 2;
            capsule.enabled = false;
            colliders.Add(capsule);

            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = part.mass;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.maxDepenetrationVelocity = 3f;
            body.angularDamping = 0.5f;
            bodies.Add(body);
            if (part.parent == null)
            {
                return;
            }

            var joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = bones[part.parent].GetComponent<Rigidbody>();
            joint.axis = Vector3.right;
            joint.swingAxis = Vector3.forward;
            joint.lowTwistLimit = new SoftJointLimit { limit = -35f };
            joint.highTwistLimit = new SoftJointLimit { limit = 60f };
            joint.swing1Limit = new SoftJointLimit { limit = 45f };
            joint.swing2Limit = new SoftJointLimit { limit = 30f };
            joint.enableProjection = true;
        }

        private static Ghost BuildGhost()
        {
            var root = CreateBody(GhostMesh, "Ghost", EnsureGhostMaterial());
            try
            {
                Object.DestroyImmediate(root.GetComponent<Animator>());
                var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var bones = root.GetComponentsInChildren<Transform>(true);
                var ghost = root.AddComponent<Ghost>();
                ghost.Setup(bones.First(b => b.name == Bone + "LeftArm"), bones.First(b => b.name == Bone + "RightArm"));
                root.transform.localScale = Vector3.one * Scale;
                var saved = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Ghost.prefab");
                return saved.GetComponent<Ghost>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Material EnsureGhostMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.8f, 0.93f, 1f, 0.7f));
            material.SetColor("_EmissionColor", new Color(0.15f, 0.2f, 0.25f));
            material.EnableKeyword("_EMISSION");
            material.SetFloat("_Smoothness", 0.2f);
            // Transparent, but writing depth: the arms and the far side do not show through the body.
            material.SetFloat("_Surface", 1f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 1f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, GhostMaterialPath);
            return material;
        }
    }
}
