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

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = Rect("Minimap", canvasGo.transform, Vector2.one * Diameter);
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
            root.anchoredPosition = new Vector2(-CornerInset, -CornerInset);

            var circle = Rect("Circle", root, Vector2.zero, stretch: true);
            var backdrop = AddImage(circle, MinimapSprites.Circle, Backdrop);
            circle.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            backdrop.raycastTarget = false;

            var pivot = Rect("MapPivot", circle, Vector2.zero);
            var map = Rect("Map", pivot, Vector2.one * 100f).gameObject.AddComponent<RawImage>();
            map.raycastTarget = false;
            map.texture = data.Map;

            AddImage(Rect("Ring", root, Vector2.zero, stretch: true), MinimapSprites.Ring, Color.white);
            var icons = Rect("Icons", root, Vector2.zero, stretch: true);
            var fire = CreateMarker(icons, "FireTemplate", MinimapSprites.Flame, 30f);
            var truck = CreateMarker(icons, "Truck", MinimapSprites.Truck, 26f);
            AddImage(Rect("PlayerArrow", root, Vector2.one * 24f), MinimapSprites.PlayerArrow, ArrowColor);
            var north = CreateNorth(root);

            var view = root.gameObject.AddComponent<MinimapView>();
            CitySceneSetup.SetRefs(view, ("data", data), ("player", Object.FindFirstObjectByType<PlayerModeController>()),
                ("circle", circle), ("mapPivot", pivot), ("map", map), ("icons", icons), ("north", north),
                ("fireTemplate", fire), ("truckMarker", truck), ("steamSprite", MinimapSprites.Steam));
            Debug.Log("[city] minimap set up");
        }

        private static MinimapMarker CreateMarker(Transform parent, string name, Sprite sprite, float size)
        {
            var root = Rect(name, parent, Vector2.zero);
            // The pointer turns around the marker's centre; its tip sits just outside the icon.
            var pointer = Rect("Pointer", root, Vector2.zero);
            var tip = Rect("Tip", pointer, Vector2.one * 12f);
            tip.anchoredPosition = new Vector2(0f, size * 0.5f + 5f);
            AddImage(tip, MinimapSprites.Pointer, new Color(1f, 0.85f, 0.4f));
            var icon = AddImage(Rect("Icon", root, Vector2.one * size), sprite, Color.white);
            var distance = AddText(Rect("Distance", root, new Vector2(80f, 22f)), 17);
            distance.fontStyle = FontStyle.Bold;

            var marker = root.gameObject.AddComponent<MinimapMarker>();
            CitySceneSetup.SetRefs(marker, ("root", root), ("icon", icon), ("pointer", pointer), ("distance", distance));
            return marker;
        }

        private static RectTransform CreateNorth(Transform parent)
        {
            var north = Rect("North", parent, Vector2.one * 26f);
            AddImage(north, MinimapSprites.Circle, new Color(0.11f, 0.13f, 0.17f));
            var label = AddText(Rect("Label", north, Vector2.zero, stretch: true), 12);
            label.text = "Пн";
            label.fontStyle = FontStyle.Bold;
            return north;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, bool stretch = false)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                rect.sizeDelta = size;
            }

            return rect;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(RectTransform rect, int size)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            rect.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }
    }
}
