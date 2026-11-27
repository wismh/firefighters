using _Project.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Editor.City
{
    /// <summary>
    /// The GameHUD canvas: the damage bill at the top, the mission status in the top-left corner, the controls hint
    /// and the crosshair, floating labels over the world and the closing report. Re-running it replaces the canvas.
    /// </summary>
    public static class HudBuilder
    {
        private const int SortingOrder = 9;

        private static readonly Color MoneyColor = new Color(1f, 0.35f, 0.3f);
        private static readonly Color GoodColor = new Color(0.45f, 1f, 0.45f);

        [MenuItem("Tools/City/Setup HUD")]
        public static void SetupHud()
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
            var old = GameObject.Find("GameHUD");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var canvas = EditorUi.Canvas("GameHUD", SortingOrder);
            var camera = GameObject.FindWithTag("MainCamera").GetComponent<Camera>();
            CreateFloatingTexts(canvas, camera);
            CreateBill(canvas);
            CreateStatus(canvas);
            CreateHint(canvas);
            CreateReport(canvas);
            Debug.Log("[city] HUD set up");
        }

        private static void CreateFloatingTexts(RectTransform canvas, Camera camera)
        {
            var area = EditorUi.Rect("FloatingTexts", canvas, Vector2.zero, true);
            var template = EditorUi.AddText(EditorUi.Rect("Template", area, new Vector2(800f, 70f)), 26);
            template.fontStyle = FontStyle.Bold;
            // Anchored at the centre of the area, like the local points the view converts screen positions to.
            CitySceneSetup.SetRefs(area.gameObject.AddComponent<FloatingTextView>(), ("viewCamera", camera),
                ("area", area), ("template", template));
        }

        private static void CreateBill(RectTransform canvas)
        {
            var label = EditorUi.AddText(EditorUi.Rect("Bill", canvas, new Vector2(600f, 34f)), 20, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            EditorUi.Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f));
            CitySceneSetup.SetRefs(label.gameObject.AddComponent<DamageBillHud>(), ("label", label));
        }

        private static void CreateStatus(RectTransform canvas)
        {
            var label = EditorUi.AddText(EditorUi.Rect("MissionStatus", canvas, new Vector2(600f, 30f)), 18,
                TextAnchor.MiddleLeft);
            EditorUi.Place(label.rectTransform, new Vector2(20f, -16f));
            CitySceneSetup.SetRefs(label.gameObject.AddComponent<MissionStatusView>(), ("label", label));
        }

        private static void CreateHint(RectTransform canvas)
        {
            var root = EditorUi.Rect("Controls", canvas, Vector2.zero, true);
            var label = EditorUi.AddText(EditorUi.Rect("Hint", root, new Vector2(900f, 30f)), 18, TextAnchor.MiddleLeft);
            EditorUi.Anchor(label.rectTransform, Vector2.zero, new Vector2(20f, 10f));
            var crosshair = EditorUi.AddText(EditorUi.Rect("Crosshair", root, new Vector2(30f, 30f)), 22);
            crosshair.text = "+";
            crosshair.gameObject.SetActive(false);
            CitySceneSetup.SetRefs(root.gameObject.AddComponent<InteractionHintView>(), ("label", label),
                ("crosshair", crosshair.gameObject));
        }

        private static void CreateReport(RectTransform canvas)
        {
            var root = EditorUi.Rect("Report", canvas, Vector2.zero, true);
            var panel = EditorUi.Rect("Panel", root, new Vector2(560f, 290f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.6f);
            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.7f);
            background.raycastTarget = false;

            var y = -14f;
            Text Line(string name, int size, Color color, float height)
            {
                var text = EditorUi.AddText(EditorUi.Rect(name, panel, new Vector2(560f, height)), size);
                text.fontStyle = size >= 24 ? FontStyle.Bold : FontStyle.Normal;
                text.color = color;
                EditorUi.Place(text.rectTransform, new Vector2(0f, y));
                y -= height;
                return text;
            }

            var title = Line("Title", 30, GoodColor, 50f);
            var saved = Line("Saved", 19, Color.white, 28f);
            var props = Line("Props", 19, Color.white, 28f);
            var cars = Line("Cars", 19, Color.white, 40f);
            var bill = Line("Bill", 24, MoneyColor, 44f);
            var verdict = Line("Verdict", 15, new Color(1f, 1f, 1f, 0.7f), 32f);
            var passed = Line("Passed", 20, GoodColor, 30f);
            CitySceneSetup.SetRefs(root.gameObject.AddComponent<MissionReportView>(), ("panel", panel.gameObject),
                ("title", title), ("saved", saved), ("props", props), ("cars", cars), ("bill", bill),
                ("verdict", verdict), ("passed", passed));
        }
    }
}
