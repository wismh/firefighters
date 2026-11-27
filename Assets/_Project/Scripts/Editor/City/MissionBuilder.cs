using System.IO;
using _Project.Mission;
using _Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Editor.City
{
    /// <summary>
    /// The "test task" mission: the MissionScript asset with the dispatcher's lines, a MissionHUD canvas with her
    /// radio (portrait, static, typed text) on the left and the fire markers, and the Mission object that runs the
    /// calls. Art comes from Tools/Art/gen_radio_art.py, sounds from Tools/Audio/gen_sfx.py. Re-running it replaces
    /// what it built and keeps the edited lines.
    /// </summary>
    public static class MissionBuilder
    {
        private const string ArtFolder = "Assets/_Project/Art/Radio";
        private const string ScriptPath = ProjectPaths.Configurations + "/Mission/MissionScript.asset";

        private static readonly Color SpeakerColor = new Color(1f, 0.72f, 0.3f);
        private static readonly Color HintColor = new Color(0.85f, 0.88f, 0.95f);
        private static readonly Color PointerColor = new Color(1f, 0.6f, 0.2f);

        [MenuItem("Tools/City/Setup Mission")]
        public static void SetupMission()
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
            foreach (var name in new[] { "MissionHUD", "Mission" })
            {
                var old = GameObject.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old);
                }
            }

            var script = EnsureScript();
            var modes = Object.FindFirstObjectByType<PlayerModeController>();
            var cameraTransform = (Transform)new SerializedObject(modes).FindProperty("cameraTransform").objectReferenceValue;

            // Over the minimap HUD.
            var canvas = EditorUi.Canvas("MissionHUD", 11);
            var radio = CreateRadio(canvas, script);
            var targets = CreateTargets(canvas, cameraTransform.GetComponent<Camera>());

            var director = new GameObject("Mission").AddComponent<MissionDirector>();
            CitySceneSetup.SetRefs(director, ("script", script));
            Debug.Log("[city] mission set up");
        }

        private static MissionScript EnsureScript()
        {
            var script = AssetDatabase.LoadAssetAtPath<MissionScript>(ScriptPath);
            if (script != null)
            {
                return script;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ScriptPath));
            script = ScriptableObject.CreateInstance<MissionScript>();
            AssetDatabase.CreateAsset(script, ScriptPath);
            AssetDatabase.SaveAssets();
            return script;
        }

        // ---------------------------------------------------------------- radio

        private static RadioView CreateRadio(RectTransform canvas, MissionScript script)
        {
            var panel = EditorUi.Rect("Radio", canvas, new Vector2(740f, 196f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0f, 0.5f);
            panel.anchoredPosition = new Vector2(28f, 60f);
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            var frame = EditorUi.AddImage(panel, Art("PanelFrame", 6), Color.white);
            frame.type = Image.Type.Sliced;
            // Pixels of the frame drawn twice as large as the sprite's.
            frame.pixelsPerUnitMultiplier = 0.5f;

            var portraitBox = EditorUi.Rect("PortraitBox", panel, new Vector2(168f, 168f));
            portraitBox.anchorMin = portraitBox.anchorMax = portraitBox.pivot = new Vector2(0f, 0.5f);
            portraitBox.anchoredPosition = new Vector2(14f, 0f);
            var portrait = EditorUi.AddImage(EditorUi.Rect("Portrait", portraitBox, new Vector2(156f, 156f)), Art("Dispatcher_Idle"),
                Color.white);
            var noise = EditorUi.Rect("Static", portrait.rectTransform, Vector2.zero, true).gameObject.AddComponent<RawImage>();
            noise.texture = Texture("Static");
            noise.color = new Color(1f, 1f, 1f, 0.05f);
            noise.uvRect = new UnityEngine.Rect(0f, 0f, 0.6f, 0.6f);
            noise.raycastTarget = false;
            var portraitFrame = EditorUi.AddImage(EditorUi.Rect("Frame", portraitBox, Vector2.zero, true), Art("PortraitFrame", 6),
                Color.white);
            portraitFrame.type = Image.Type.Sliced;
            portraitFrame.pixelsPerUnitMultiplier = 0.5f;

            var speaker = EditorUi.AddText(EditorUi.Rect("Speaker", panel, new Vector2(520f, 28f)), 19, TextAnchor.UpperLeft);
            EditorUi.Place(speaker.rectTransform, new Vector2(198f, -16f));
            speaker.fontStyle = FontStyle.Bold;
            speaker.color = SpeakerColor;
            speaker.text = $"{script.callSign} · {script.speakerName}";

            var body = EditorUi.AddText(EditorUi.Rect("Body", panel, new Vector2(520f, 120f)), 23, TextAnchor.UpperLeft);
            EditorUi.Place(body.rectTransform, new Vector2(198f, -48f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.supportRichText = true;
            body.lineSpacing = 1.05f;

            var hint = EditorUi.AddText(EditorUi.Rect("Hint", panel, new Vector2(440f, 22f)), 15, TextAnchor.LowerRight);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = new Vector2(1f, 0f);
            hint.rectTransform.anchoredPosition = new Vector2(-16f, 10f);
            hint.color = HintColor;
            hint.text = "Пробіл — далі    Tab — пропустити  ▼";

            var radio = panel.gameObject.AddComponent<RadioView>();
            CitySceneSetup.SetRefs(radio, ("panel", panel), ("group", group), ("portrait", portrait),
                ("staticNoise", noise), ("body", body), ("hint", hint), ("blinkFrame", Art("Dispatcher_Blink")),
                ("openSound", SoundscapeBuilder.Sound("radioOpen", SoundscapeBuilder.Generated + "Sfx/radio_open.wav", 0.45f)),
                ("closeSound", SoundscapeBuilder.Sound("radioClose", SoundscapeBuilder.Generated + "Sfx/radio_close.wav", 0.4f)),
                ("blips", SoundscapeBuilder.Bank("radioBlips", 0.32f, 0.94f, 1.08f,
                    SoundscapeBuilder.Clips(SoundscapeBuilder.Generated + "Sfx/", "radio_blip_*.wav"))));
            var so = new SerializedObject(radio);
            var frames = so.FindProperty("talkFrames");
            var names = new[] { "Dispatcher_Idle", "Dispatcher_Talk1", "Dispatcher_Talk2" };
            frames.arraySize = names.Length;
            for (var i = 0; i < names.Length; i++)
            {
                frames.GetArrayElementAtIndex(i).objectReferenceValue = Art(names[i]);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return radio;
        }

        // ---------------------------------------------------------------- targets

        private static TargetIndicator CreateTargets(RectTransform canvas, Camera viewCamera)
        {
            var area = EditorUi.Rect("Targets", canvas, Vector2.zero, true);
            var root = EditorUi.Rect("TargetTemplate", area, Vector2.zero);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var icon = EditorUi.Rect("Icon", root, new Vector2(46f, 46f));
            EditorUi.AddImage(EditorUi.Rect("Backdrop", icon, new Vector2(58f, 58f)), MinimapSprites.Circle,
                new Color(0.08f, 0.1f, 0.16f, 0.75f));
            EditorUi.AddImage(EditorUi.Rect("Flame", icon, Vector2.zero, true), MinimapSprites.Flame, Color.white);
            var arrow = EditorUi.Rect("Arrow", root, new Vector2(22f, 22f));
            EditorUi.AddImage(arrow, MinimapSprites.Pointer, PointerColor);
            var distance = EditorUi.AddText(EditorUi.Rect("Distance", root, new Vector2(120f, 26f)), 19, TextAnchor.MiddleCenter);
            distance.rectTransform.anchoredPosition = new Vector2(0f, -44f);
            distance.fontStyle = FontStyle.Bold;

            var marker = root.gameObject.AddComponent<TargetMarker>();
            CitySceneSetup.SetRefs(marker, ("root", root), ("group", group), ("icon", icon), ("arrow", arrow),
                ("distance", distance));
            var indicator = area.gameObject.AddComponent<TargetIndicator>();
            CitySceneSetup.SetRefs(indicator, ("viewCamera", viewCamera), ("area", area), ("template", marker));
            return indicator;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>A pixel-art sprite from the radio folder: point filtered, uncompressed, optional 9-slice border.</summary>
        private static Sprite Art(string name, int border = 0)
        {
            var path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var wanted = new Vector4(border, border, border, border);
            var dirty = importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point ||
                        importer.spriteBorder != wanted || importer.mipmapEnabled;
            if (dirty)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.spriteBorder = wanted;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Texture2D Texture(string name)
        {
            var path = $"{ArtFolder}/{name}.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.wrapMode != TextureWrapMode.Repeat || importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
