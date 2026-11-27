using _Project.City;
using _Project.Minimap;
using _Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.Editor.City
{
    /// <summary>
    /// Bakes the city map and builds the HUD canvas with the round minimap in the top-right corner: a masked map
    /// that turns under a fixed player arrow, a rim with the north mark, and marker templates for fires and the
    /// truck. Re-running it replaces the HUD it built before.
    /// </summary>
    public static class MinimapBuilder
    {
        private const float Diameter = 260f;
        private const float CornerInset = 28f;

        private static readonly Color Backdrop = new Color(0.09f, 0.11f, 0.14f, 0.9f);
        private static readonly Color ArrowColor = new Color(0.35f, 0.85f, 1f);

        [MenuItem("Tools/City/Setup Minimap")]
        public static void SetupMinimap()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[city] exit Play mode first");
                return;
            }

            Apply(AssetDatabase.LoadAssetAtPath<CityLayout>(CitySceneSetup.LayoutPath));
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void Apply(CityLayout layout)
        {
            var data = MinimapBaker.Bake(layout);
            var old = GameObject.Find("HUD");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var canvasGo = EditorUi.Canvas("HUD", 10).gameObject;

            var root = EditorUi.Rect("Minimap", canvasGo.transform, Vector2.one * Diameter);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
            root.anchoredPosition = new Vector2(-CornerInset, -CornerInset);

            var circle = EditorUi.Rect("Circle", root, Vector2.zero, stretch: true);
            var backdrop = EditorUi.AddImage(circle, MinimapSprites.Circle, Backdrop);
            circle.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            backdrop.raycastTarget = false;

            var pivot = EditorUi.Rect("MapPivot", circle, Vector2.zero);
            var map = EditorUi.Rect("Map", pivot, Vector2.one * 100f).gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            map.texture = data.Map;

            EditorUi.AddImage(EditorUi.Rect("Ring", root, Vector2.zero, stretch: true), MinimapSprites.Ring, Color.white);
            var icons = EditorUi.Rect("Icons", root, Vector2.zero, stretch: true);
            var fire = CreateMarker(icons, "FireTemplate", MinimapSprites.Flame, 30f);
            var truck = CreateMarker(icons, "Truck", MinimapSprites.Truck, 26f);
            EditorUi.AddImage(EditorUi.Rect("PlayerArrow", root, Vector2.one * 24f), MinimapSprites.PlayerArrow, ArrowColor);
            var north = CreateNorth(root);

            var view = root.gameObject.AddComponent<MinimapView>();
            CitySceneSetup.SetRefs(view, ("data", data),
                ("circle", circle), ("mapPivot", pivot), ("map", map), ("icons", icons), ("north", north),
                ("fireTemplate", fire), ("truckMarker", truck), ("steamSprite", MinimapSprites.Steam));
            Debug.Log("[city] minimap set up");
        }

        private static MinimapMarker CreateMarker(Transform parent, string name, Sprite sprite, float size)
        {
            var root = EditorUi.Rect(name, parent, Vector2.zero);
            // The pointer turns around the marker's centre; its tip sits just outside the icon.
            var pointer = EditorUi.Rect("Pointer", root, Vector2.zero);
            var tip = EditorUi.Rect("Tip", pointer, Vector2.one * 12f);
            tip.anchoredPosition = new Vector2(0f, size * 0.5f + 5f);
            EditorUi.AddImage(tip, MinimapSprites.Pointer, new Color(1f, 0.85f, 0.4f));
            var icon = EditorUi.AddImage(EditorUi.Rect("Icon", root, Vector2.one * size), sprite, Color.white);
            var distance = EditorUi.AddText(EditorUi.Rect("Distance", root, new Vector2(80f, 22f)), 17, outline: true);
            distance.fontStyle = FontStyle.Bold;

            var marker = root.gameObject.AddComponent<MinimapMarker>();
            CitySceneSetup.SetRefs(marker, ("root", root), ("icon", icon), ("pointer", pointer), ("distance", distance));
            return marker;
        }

        private static RectTransform CreateNorth(Transform parent)
        {
            var north = EditorUi.Rect("North", parent, Vector2.one * 26f);
            EditorUi.AddImage(north, MinimapSprites.Circle, new Color(0.11f, 0.13f, 0.17f));
            var label = EditorUi.AddText(EditorUi.Rect("Label", north, Vector2.zero, stretch: true), 12, outline: true);
            label.text = "Пн";
            label.fontStyle = FontStyle.Bold;
            return north;
        }
    }
}
