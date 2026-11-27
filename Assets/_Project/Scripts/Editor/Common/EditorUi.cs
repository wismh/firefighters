using UnityEngine;
using UnityEngine.UI;

namespace _Project.Editor
{
    /// <summary>uGUI building blocks for the HUD builders: rects, images and legacy texts (they carry Cyrillic).</summary>
    public static class EditorUi
    {
        public static RectTransform Rect(string name, Transform parent, Vector2 size, bool stretch = false)
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

        /// <summary>Anchors and pivots the rect at the parent's top-left and puts it at <paramref name="topLeft"/>.</summary>
        public static void Place(RectTransform rect, Vector2 topLeft)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
        }

        /// <summary>Anchors and pivots the rect at <paramref name="anchor"/> (0..1 of the parent) with an offset.</summary>
        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
        }

        public static Image AddImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A label in the built-in font; <paramref name="outline"/> draws an outline instead of a drop shadow.</summary>
        public static Text AddText(RectTransform rect, int size, TextAnchor alignment = TextAnchor.MiddleCenter,
            bool outline = false)
        {
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (outline)
            {
                rect.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            }
            else
            {
                rect.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.85f);
            }

            return text;
        }

        /// <summary>A screen-space overlay canvas that scales from 1920×1080.</summary>
        public static RectTransform Canvas(string name, int sortingOrder)
        {
            var canvasGo = new GameObject(name, typeof(RectTransform));
            var canvas = canvasGo.AddComponent<UnityEngine.Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return (RectTransform)canvasGo.transform;
        }
    }
}
