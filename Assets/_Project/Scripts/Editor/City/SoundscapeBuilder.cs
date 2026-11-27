using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using _Project.Audio;
using _Project.Comedy;
using _Project.Player;
using MyToolkit.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>
    /// Sound for the city scene: import settings for the generated clips (Tools/Audio/gen_*.py) and the voices
    /// picked from HumanVocalsVol2, SoundData and SoundBank assets, the voices and extra sounds of the comedy
    /// director, and the Soundscape object with loops, footsteps and music. Re-running it replaces what it built.
    /// </summary>
    public static class SoundscapeBuilder
    {
        internal const string Generated = ProjectPaths.GeneratedAudio;
        private const string Sfx = ProjectPaths.Sfx;
        private const string DataFolder = Sfx + "Data";
        private const string BankFolder = "Assets/_Project/Audio/Banks";
        private const string Vocals = "Assets/HumanVocalsVol2";
        private const string PlayerVoice = "Walter";

        private static readonly string[] Adults =
            { "Arthur", "Jamie", "Boris", "Hunter", "Agnes", "Alex", "Esmeralda", "Zoe", "Kate", "Grace" };

        private static readonly string[] Men = { "Arthur", "Jamie", "Boris", "Hunter" };
        private static readonly string[] Kids = { "Charlie", "Chloe", "Lily" };
        private static readonly string[] Women = { "Agnes", "Alex", "Zoe" };

        [MenuItem("Tools/City/Setup Sound")]
        public static void SetupSound()
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

        public static void Apply()
        {
            ImportGenerated();
            ApplyComedySounds(Object.FindFirstObjectByType<ComedyDirector>());
            CreateSoundscape();
            AssetDatabase.SaveAssets();
            Debug.Log("[city] sound set up");
        }

        public static void ApplyComedySounds(ComedyDirector director)
        {
            var so = new SerializedObject(director);
            var sounds = so.FindProperty("sounds");
            Set(sounds, "driverSoaked", Sound("driverSoaked", Generated + "Sfx/splash.wav", 0.8f, 0.9f, 1.15f));
            Set(sounds, "horn", Sound("horn", Generated + "Sfx/horn.wav", 0.3f, 0.95f, 1.05f));
            Set(sounds, "boing", Sound("boing", Generated + "Sfx/boing.wav", 0.6f, 0.9f, 1.1f));
            Set(sounds, "whoosh", Sound("whoosh", Generated + "Sfx/whoosh.wav", 0.7f, 0.9f, 1.1f));
            Set(sounds, "moneyDrop", Bank("moneyDrop", 0.6f, 0.95f, 1.08f, Clips(Sfx, "Magic_Coin_Fall_*.wav")));

            Set(sounds, "pedestrianScream", Bank("pedestrianScream", 0.75f, 0.95f, 1.08f, Voice(Adults, "Scream")));
            Set(sounds, "pedestrianHit", Bank("pedestrianHit", 0.9f, 0.95f, 1.08f, Voice(Adults, "Sharp_Pain", "Woah")));
            Set(sounds, "pedestrianSoaked", Bank("pedestrianSoaked", 0.8f, 0.95f, 1.08f, Voice(Adults, "Surprised", "Oh")));
            // A ghost laughs a little lower and slower than the living.
            Set(sounds, "ghostLaugh", Bank("ghostLaugh", 0.8f, 0.72f, 0.82f, Voice(Adults, "Mocking_Laughter")));
            Set(sounds, "driverAngry", Bank("driverAngry", 0.75f, 0.95f, 1.05f, Voice(Men, "Hmmm", "Surprised")));
            Set(sounds, "driverKnocked", Bank("driverKnocked", 0.75f, 0.95f, 1.05f, Voice(Adults, "Ooo", "Pain")));
            Set(sounds, "crowdCheer", Bank("crowdCheer", 0.6f, 0.95f, 1.08f, Voice(Kids, "Wow", "Laughter"),
                Voice(Women, "Wow")));
            Set(sounds, "playerBurning", Bank("playerBurning", 0.85f, 0.97f, 1.05f,
                Voice(new[] { PlayerVoice }, "Ouch", "Sharp_Pain", "Scream", "Coughing_Short")));
            Set(sounds, "playerCheer", Bank("playerCheer", 0.85f, 0.97f, 1.05f,
                Voice(new[] { PlayerVoice }, "Battle_Cry", "Woah", "Laughter")));
            Set(sounds, "playerThrown", Bank("playerThrown", 0.85f, 0.97f, 1.05f,
                Voice(new[] { PlayerVoice }, "Woah", "Effort")));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateSoundscape()
        {
            var modes = Object.FindFirstObjectByType<PlayerModeController>();
            var refs = new SerializedObject(modes);
            var cameraTransform = (Transform)refs.FindProperty("cameraTransform").objectReferenceValue;
            var walker = (FirstPersonController)refs.FindProperty("walker").objectReferenceValue;

            var old = GameObject.Find("Soundscape");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var soundscape = new GameObject("Soundscape").AddComponent<CitySoundscape>();
            CitySceneSetup.SetRefs(soundscape, ("listener", cameraTransform),
                ("engine", Sound("engine", Generated + "Sfx/engine_loop.wav", 0.3f)),
                ("siren", Sound("siren", Generated + "Sfx/siren_loop.wav", 0.1f)),
                ("skid", Sound("skid", Generated + "Sfx/skid_loop.wav", 0.12f)),
                ("hose", Sound("hose", Generated + "Sfx/hose_loop.wav", 0.5f)),
                ("steam", Sound("steam", Generated + "Sfx/steam_loop.wav", 0.45f)),
                ("fire", Sound("fire", Generated + "Sfx/fire_loop.wav", 0.8f)),
                ("ambience", Sound("ambience", Generated + "Sfx/ambience_loop.wav", 0.35f)),
                ("footsteps", Bank("footsteps", 0.45f, 0.9f, 1.1f, Clips(Generated + "Sfx/", "footstep_*.wav"))),
                ("land", Sound("land", Generated + "Sfx/land.wav", 0.8f)),
                ("doorOpen", Sound("doorOpen", Generated + "Sfx/door_open.wav", 0.6f, 0.95f, 1.05f)),
                ("doorClose", Sound("doorClose", Generated + "Sfx/door_close.wav", 0.7f, 0.95f, 1.05f)),
                ("driveMusic", Sound("musicChase", Generated + "Music/music_chase.wav", 0.5f)),
                ("walkMusic", Sound("musicDusk", Generated + "Music/music_dusk.wav", 0.5f)));
        }

        // ---------------------------------------------------------------- import settings

        private static void ImportGenerated()
        {
            foreach (var path in Clips(Generated + "Sfx/", "*.wav"))
            {
                Configure(path, AudioClipLoadType.DecompressOnLoad, AudioCompressionFormat.Vorbis, 0.7f, false);
            }

            foreach (var path in Clips(Generated + "Music/", "*.wav"))
            {
                Configure(path, AudioClipLoadType.Streaming, AudioCompressionFormat.Vorbis, 0.6f, false);
            }
        }

        private static void Configure(string path, AudioClipLoadType loadType, AudioCompressionFormat format,
            float quality, bool mono)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            if (importer.forceToMono == mono && settings.loadType == loadType && settings.compressionFormat == format &&
                Mathf.Approximately(settings.quality, quality))
            {
                return;
            }

            importer.forceToMono = mono;
            settings.loadType = loadType;
            settings.compressionFormat = format;
            settings.quality = quality;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------- assets

        private static void Set(SerializedProperty sounds, string field, Object value)
        {
            sounds.FindPropertyRelative(field).objectReferenceValue = value;
        }

        internal static string[] Clips(string folder, string pattern)
        {
            var files = Directory.GetFiles(folder, pattern);
            for (var i = 0; i < files.Length; i++)
            {
                files[i] = files[i].Replace('\\', '/');
            }

            System.Array.Sort(files);
            return files;
        }

        /// <summary>Clips of the given voices and categories, e.g. NPC_Zoe_Scream_3.wav for ("Zoe", "Scream").</summary>
        private static string[] Voice(string[] voices, params string[] categories)
        {
            var result = new List<string>();
            foreach (var voice in voices)
            {
                foreach (var category in categories)
                {
                    var match = new Regex($"^[A-Za-z]+_{voice}_{category}(_\\d+)?\\.wav$");
                    foreach (var file in Directory.GetFiles(Vocals, $"*_{voice}_{category}*.wav", SearchOption.AllDirectories))
                    {
                        if (match.IsMatch(Path.GetFileName(file)))
                        {
                            var path = file.Replace('\\', '/');
                            // Voices are short and many: compressed in memory, mono.
                            Configure(path, AudioClipLoadType.CompressedInMemory, AudioCompressionFormat.Vorbis, 0.5f,
                                true);
                            result.Add(path);
                        }
                    }
                }
            }

            if (result.Count == 0)
            {
                Debug.LogError($"[city] no voice clips for {string.Join(",", categories)}");
            }

            result.Sort();
            return result.ToArray();
        }

        internal static SoundData Sound(string name, string clipPath, float volume, float pitchMin = 1f,
            float pitchMax = 1f)
        {
            Directory.CreateDirectory(DataFolder);
            var path = $"{DataFolder}/{name}.asset";
            var sound = AssetDatabase.LoadAssetAtPath<SoundData>(path);
            if (sound == null)
            {
                sound = ScriptableObject.CreateInstance<SoundData>();
                AssetDatabase.CreateAsset(sound, path);
            }

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
            {
                Debug.LogError($"[city] missing clip {clipPath}");
            }

            var so = new SerializedObject(sound);
            so.FindProperty("_clip").objectReferenceValue = clip;
            so.FindProperty("_volume").floatValue = volume;
            so.FindProperty("_pitchRange").vector2Value = new Vector2(pitchMin, pitchMax);
            so.ApplyModifiedPropertiesWithoutUndo();
            return sound;
        }

        internal static SoundBank Bank(string name, float volume, float pitchMin, float pitchMax,
            params string[][] clipGroups)
        {
            Directory.CreateDirectory(BankFolder);
            var path = $"{BankFolder}/{name}.asset";
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(path);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, path);
            }

            var clips = new List<AudioClip>();
            foreach (var group in clipGroups)
            {
                foreach (var clipPath in group)
                {
                    clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath));
                }
            }

            var so = new SerializedObject(bank);
            var list = so.FindProperty("clips");
            list.arraySize = clips.Count;
            for (var i = 0; i < clips.Count; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }

            so.FindProperty("volume").floatValue = volume;
            so.FindProperty("pitchRange").vector2Value = new Vector2(pitchMin, pitchMax);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[city] bank {name}: {clips.Count} clips");
            return bank;
        }
    }
}
