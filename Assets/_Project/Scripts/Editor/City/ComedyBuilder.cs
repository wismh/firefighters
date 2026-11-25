using System.Collections.Generic;
using System.IO;
using _Project.City;
using _Project.Comedy;
using _Project.Player;
using MyToolkit.Audio;
using MyToolkit.ParticleFx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>
    /// Adds the slapstick layer to the city scene: pooled cartoon effects for the installer, sound assets, the
    /// comedy director with its flight trail, camera shake, prop kinds, and siren beacons on the fire truck.
    /// Re-running it replaces what it built before.
    /// </summary>
    public static class ComedyBuilder
    {
        private const string Etfx = "Assets/ThirdParty/EpicToonFX/Prefabs/";
        private const string Hovl = "Assets/ThirdParty/HovlStudio/Epic Toon VFX 2/Prefabs/";
        private const string StarTrail = "Assets/ThirdParty/ParticleTrails/Prefab/FX_ParticleTrail_Star.prefab";
        private const string EtfxSound = "Assets/ThirdParty/EpicToonFX/Sound/";
        private const string Sfx = "Assets/_Project/Audio/Sfx/";
        private const string SoundFolder = Sfx + "Data";
        private const string FxFolder = "Assets/_Project/Prefabs/Fx/Comedy";

        private readonly struct FxSource
        {
            public readonly ComedyFxId Id;
            public readonly string Path;
            public readonly float Scale;
            public readonly int Pool;
            // Above 0: the effect loops in the pack; it plays this long and then stops emitting.
            public readonly float Hold;

            public FxSource(ComedyFxId id, string path, float scale, int pool, float hold = 0f)
            {
                Id = id;
                Path = path;
                Scale = scale;
                Pool = pool;
                Hold = hold;
            }
        }

        private static readonly FxSource[] Sources =
        {
            new FxSource(ComedyFxId.TextPow, Etfx + "Combat/Text/Pow.prefab", 5f, 3),
            new FxSource(ComedyFxId.TextKaPow, Etfx + "Combat/Text/KaPow.prefab", 5f, 3),
            new FxSource(ComedyFxId.TextCrack, Etfx + "Combat/Text/Crack.prefab", 5f, 3),
            new FxSource(ComedyFxId.TextZap, Etfx + "Combat/Text/Zap.prefab", 5f, 3),
            new FxSource(ComedyFxId.Punch, Etfx + "Combat/Brawling/Cartoony/CartoonyPunchHeavy.prefab", 3.5f, 3),
            new FxSource(ComedyFxId.ToonPunch, Hovl + "Punches/Toon punch.prefab", 3f, 3),
            new FxSource(ComedyFxId.MetalSparks, Etfx + "Combat/Explosions/- Misc/MetalExplosion.prefab", 2.5f, 4),
            new FxSource(ComedyFxId.DustPoof, Etfx + "Environment/Dust/DustDirtyPoof.prefab", 3f, 6),
            new FxSource(ComedyFxId.Feathers, Etfx + "Interactive/Feathers/FeatherExplosion.prefab", 3f, 3),
            new FxSource(ComedyFxId.DollarBlast, Etfx + "Interactive/Money/Bills/DollarbillBlast.prefab", 3f, 4),
            new FxSource(ComedyFxId.CoinBlast, Etfx + "Interactive/Money/Coins/GoldCoinBlast.prefab", 4f, 2),
            new FxSource(ComedyFxId.Confetti, Etfx + "Environment/Confetti/Blast/ConfettiBlastRainbow.prefab", 6f, 2),
            new FxSource(ComedyFxId.FireworkRed, Etfx + "Environment/Firework/FireworkRedCluster.prefab", 5f, 3),
            new FxSource(ComedyFxId.FireworkBlue, Etfx + "Environment/Firework/FireworkBlueCluster.prefab", 5f, 3),
            new FxSource(ComedyFxId.FireworkYellow, Etfx + "Environment/Firework/FireworkYellowCluster.prefab", 5f, 3),
            new FxSource(ComedyFxId.Geyser, Etfx + "Environment/Water/Flowing/Sharp/WaterGeysirSharp.prefab", 3.2f, 3, 7f),
            new FxSource(ComedyFxId.StunStars, Etfx + "Combat/Brawling/Stun/StunnedCirclingStars.prefab", 3.5f, 3, 4f),
            new FxSource(ComedyFxId.EmojiAngry, Etfx + "Interactive/Emojis/EmojiAngry.prefab", 4f, 2),
            new FxSource(ComedyFxId.EmojiShocked, Etfx + "Interactive/Emojis/EmojiShocked.prefab", 4f, 2),
            new FxSource(ComedyFxId.EmojiCry, Etfx + "Interactive/Emojis/EmojiCry.prefab", 4f, 2),
            new FxSource(ComedyFxId.EmojiTeary, Etfx + "Interactive/Emojis/EmojiTearyEyes.prefab", 4f, 2),
            new FxSource(ComedyFxId.EmojiDerp, Etfx + "Interactive/Emojis/EmojiDerp.prefab", 4f, 2),
            new FxSource(ComedyFxId.EmojiClenchTeeth, Etfx + "Interactive/Emojis/EmojiClenchTeeth.prefab", 4f, 2),
            new FxSource(ComedyFxId.EmojiNervous, Etfx + "Interactive/Emojis/EmojiNervous.prefab", 4f, 2),
            new FxSource(ComedyFxId.EmojiDisappointed, Etfx + "Interactive/Emojis/EmojiDisappointed.prefab", 4f, 2),
            new FxSource(ComedyFxId.EmojiLaughCry, Etfx + "Interactive/Emojis/EmojiLaughCry.prefab", 4f, 2),
            new FxSource(ComedyFxId.EmojiCool, Etfx + "Interactive/Emojis/EmojiCool.prefab", 4f, 2),
        };

        [MenuItem("Tools/City/Setup Comedy")]
        public static void SetupComedy()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[city] exit Play mode first");
                return;
            }

            VehiclePrefabBuilder.BuildAll();
            Apply();
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>Builds everything into the open city scene. The vehicle prefabs must already have sirens.</summary>
        public static void Apply()
        {
            VfxMaterialUpgrade.Upgrade();
            Directory.CreateDirectory(FxFolder);
            var installer = Object.FindFirstObjectByType<CityInstaller>();
            var entries = new List<ComedyFxEntry>(Sources.Length);
            foreach (var source in Sources)
            {
                entries.Add(new ComedyFxEntry
                {
                    id = source.Id,
                    prefab = EnsureFx(source),
                    poolSize = source.Pool,
                });
            }

            var so = new SerializedObject(installer);
            var list = so.FindProperty("comedyFx");
            list.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").enumValueIndex = (int)entries[i].id;
                element.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab;
                element.FindPropertyRelative("poolSize").intValue = entries[i].poolSize;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            ClassifyProps();
            CreateDirector();
            Debug.Log("[city] comedy set up");
        }

        /// <summary>What a knockable prop is, from the pack prefab it was made of or the name the city gave it.</summary>
        public static PropKind KindOf(GameObject instance)
        {
            var name = instance.name;
            if (name.StartsWith("Signal_") || name.Contains("traffic_light"))
            {
                return PropKind.TrafficLight;
            }

            if (name.Contains("lamp"))
            {
                return PropKind.Lamp;
            }

            if (name.Contains("Roadsign"))
            {
                return PropKind.RoadSign;
            }

            if (name.Contains("hydrant"))
            {
                return PropKind.Hydrant;
            }

            return name.Contains("bin") || name.Contains("trash") ? PropKind.Bin : PropKind.Other;
        }

        private static void ClassifyProps()
        {
            var counts = new Dictionary<PropKind, int>();
            foreach (var prop in Object.FindObjectsByType<KnockableProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var kind = KindOf(prop.gameObject);
                var so = new SerializedObject(prop);
                so.FindProperty("kind").enumValueIndex = (int)kind;
                so.ApplyModifiedPropertiesWithoutUndo();
                counts[kind] = counts.TryGetValue(kind, out var n) ? n + 1 : 1;
            }

            foreach (var pair in counts)
            {
                Debug.Log($"[city] props {pair.Key}: {pair.Value}");
            }
        }

        private static void CreateDirector()
        {
            var modes = Object.FindFirstObjectByType<PlayerModeController>();
            var refs = new SerializedObject(modes);
            var cameraTransform = (Transform)refs.FindProperty("cameraTransform").objectReferenceValue;
            var walker = (FirstPersonController)refs.FindProperty("walker").objectReferenceValue;

            if (!cameraTransform.TryGetComponent<CameraFeel>(out var feel))
            {
                feel = cameraTransform.gameObject.AddComponent<CameraFeel>();
            }

            var old = GameObject.Find("Comedy");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var root = new GameObject("Comedy");
            var director = root.AddComponent<ComedyDirector>();
            var trail = CreateTrail(root.transform);
            CitySceneSetup.SetRefs(director, ("modes", modes), ("walker", walker), ("feel", feel),
                ("viewCamera", cameraTransform.GetComponent<Camera>()), ("flightTrail", trail));

            var so = new SerializedObject(director);
            var sounds = so.FindProperty("sounds");
            SetSound(sounds, "bigCrash", EtfxSound + "etfx_target_hit.wav", 0.9f, 0.9f, 1.1f);
            SetSound(sounds, "bigCrashThud", Sfx + "SFX_MeleeAttack_03.wav", 0.9f, 0.85f, 1f);
            SetSound(sounds, "smallCrash", EtfxSound + "etfx_explosion_bullet.wav", 0.5f, 0.9f, 1.2f);
            SetSound(sounds, "propDown", EtfxSound + "etfx_explosion_poof.wav", 0.8f, 0.9f, 1.15f);
            SetSound(sounds, "knockout", Sfx + "SFX_LoseInRound.wav", 0.6f, 1f, 1f);
            SetSound(sounds, "geyser", EtfxSound + "etfx_explosion_liquid.wav", 1f, 0.8f, 0.9f);
            SetSound(sounds, "driverSoaked", Sfx + "SFX_UI_ChestShake.wav", 0.7f, 1.1f, 1.3f);
            SetSound(sounds, "coins", Sfx + "SFX_CoinsGranted.wav", 0.8f, 1f, 1f);
            SetSound(sounds, "buildingSaved", Sfx + "SFX_VictoryInRound.wav", 0.8f, 1f, 1f);
            SetSound(sounds, "allSaved", Sfx + "SFX_VictoryInMatch.mp3", 0.9f, 1f, 1f);
            SetSound(sounds, "firework", EtfxSound + "etfx_explosion_sparkle2.wav", 0.7f, 0.85f, 1.15f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSound(SerializedProperty sounds, string field, string clipPath, float volume,
            float pitchMin, float pitchMax)
        {
            sounds.FindPropertyRelative(field).objectReferenceValue = EnsureSound(field, clipPath, volume,
                new Vector2(pitchMin, pitchMax));
        }

        private static SoundData EnsureSound(string name, string clipPath, float volume, Vector2 pitch)
        {
            Directory.CreateDirectory(SoundFolder);
            var path = $"{SoundFolder}/{name}.asset";
            var sound = AssetDatabase.LoadAssetAtPath<SoundData>(path);
            if (sound == null)
            {
                sound = ScriptableObject.CreateInstance<SoundData>();
                AssetDatabase.CreateAsset(sound, path);
            }

            var so = new SerializedObject(sound);
            so.FindProperty("_clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            so.FindProperty("_volume").floatValue = volume;
            so.FindProperty("_pitchRange").vector2Value = pitch;
            so.ApplyModifiedPropertiesWithoutUndo();
            return sound;
        }

        private static ParticleSystem CreateTrail(Transform parent)
        {
            var trail = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StarTrail),
                parent);
            trail.name = "FlightTrail";
            PrefabUtility.UnpackPrefabInstance(trail, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            trail.transform.localScale = Vector3.one * 1.5f;
            foreach (var system in trail.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = system.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.loop = true;
            }

            // The pack moves the trail with its own demo script; the director carries it behind the car instead.
            foreach (var behaviour in trail.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Object.DestroyImmediate(behaviour);
            }

            return EnsureRootSystem(trail);
        }

        private static PooledParticleFxView EnsureFx(FxSource source)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source.Path);
            if (prefab == null)
            {
                throw new FileNotFoundException("[city] missing effect " + source.Path);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "Comedy" + source.Id;
                instance.transform.localScale = Vector3.one * source.Scale;
                // Demo helpers (sound players, movers) would fight the pool; the director plays the sounds.
                // The pack's own ETFX rotators stay: the stun stars circle with one.
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (!behaviour.GetType().Name.StartsWith("ETFX"))
                    {
                        Object.DestroyImmediate(behaviour);
                    }
                }

                var root = EnsureRootSystem(instance);
                foreach (var system in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.playOnAwake = false;
                    if (source.Hold > 0f && main.loop)
                    {
                        main.duration = source.Hold;
                    }

                    main.loop = false;
                }

                var view = instance.AddComponent<PooledParticleFxView>();
                var so = new SerializedObject(view);
                so.FindProperty("_particleSystem").objectReferenceValue = root;
                so.FindProperty("_maxLifetime").floatValue = Mathf.Max(5f, source.Hold + 4f);
                so.ApplyModifiedPropertiesWithoutUndo();
                var saved = PrefabUtility.SaveAsPrefabAsset(instance, $"{FxFolder}/Comedy{source.Id}.prefab");
                return saved.GetComponent<PooledParticleFxView>();
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // The pool plays the root system with its children; a pack prefab without one gets a silent root.
        private static ParticleSystem EnsureRootSystem(GameObject root)
        {
            if (root.TryGetComponent<ParticleSystem>(out var system))
            {
                return system;
            }

            system = root.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = system.emission;
            emission.enabled = false;
            var main = system.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            root.GetComponent<ParticleSystemRenderer>().enabled = false;
            return system;
        }
    }
}
