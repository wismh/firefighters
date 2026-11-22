using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>
    /// The minimap's flat icons, drawn from signed distance shapes into PNG sprites: crisp anti-aliased edges at
    /// any size, and every shape stays editable as code instead of a painted file. Shapes live in a [-1, 1] square,
    /// +Y up; each sprite paints layers of (distance function, colour) in order.
    /// </summary>
    public static class MinimapSprites
    {
        private static readonly Color Ink = new Color(0.09f, 0.1f, 0.13f, 1f);

        public static Sprite Circle => Ensure("Circle", 256, (p => Length(p) - 0.98f, Color.white));

        /// <summary>A dark rim with a thin light line on its inner side.</summary>
        public static Sprite Ring => Ensure("Ring", 256,
            (p => Band(Length(p), 0.86f, 0.99f), new Color(0.11f, 0.13f, 0.17f)),
            (p => Band(Length(p), 0.86f, 0.885f), new Color(0.85f, 0.88f, 0.92f)));

        public static Sprite PlayerArrow
        {
            get
            {
                var shape = Polygon(new Vector2(0f, 0.85f), new Vector2(0.62f, -0.7f), new Vector2(0f, -0.32f),
                    new Vector2(-0.62f, -0.7f));
                return Ensure("PlayerArrow", 64, (p => shape(p) - 0.12f, Ink), (shape, Color.white));
            }
        }

        public static Sprite Flame
        {
            get
            {
                Func<Vector2, float> shape = Teardrop;
                return Ensure("Flame", 64,
                    (p => shape(p) - 0.1f, Ink),
                    (shape, new Color(1f, 0.42f, 0.08f)),
                    (p => Teardrop((p - new Vector2(0f, -0.22f)) / 0.55f) * 0.55f, new Color(1f, 0.85f, 0.3f)));
            }
        }

        public static Sprite Steam
        {
            get
            {
                Func<Vector2, float> shape = p => Mathf.Min(Length(p - new Vector2(-0.38f, -0.18f)) - 0.38f,
                    Mathf.Min(Length(p - new Vector2(0.34f, -0.22f)) - 0.4f, Length(p - new Vector2(0f, 0.2f)) - 0.46f));
                return Ensure("Steam", 64, (p => shape(p) - 0.08f, new Color(0.4f, 0.45f, 0.5f)), (shape, Color.white));
            }
        }

        /// <summary>A fire truck from above, front up: red body, dark windscreen, white stripe.</summary>
        public static Sprite Truck
        {
            get
            {
                Func<Vector2, float> body = p => RoundedBox(p, new Vector2(0.42f, 0.86f), 0.14f);
                return Ensure("Truck", 64,
                    (p => body(p) - 0.1f, Ink),
                    (body, new Color(0.86f, 0.16f, 0.12f)),
                    (p => RoundedBox(p - new Vector2(0f, 0.5f), new Vector2(0.3f, 0.11f), 0.04f), new Color(0.16f, 0.21f, 0.3f)),
                    (p => RoundedBox(p - new Vector2(0f, -0.15f), new Vector2(0.3f, 0.06f), 0f), Color.white));
            }
        }

        public static Sprite Pointer
        {
            get
            {
                var shape = Polygon(new Vector2(0f, 0.8f), new Vector2(0.8f, -0.6f), new Vector2(-0.8f, -0.6f));
                return Ensure("Pointer", 32, (p => shape(p) - 0.18f, Ink), (shape, Color.white));
            }
        }

        private static Sprite Ensure(string name, int size, params (Func<Vector2, float> sdf, Color color)[] layers)
        {
            var path = $"{MinimapBaker.Folder}/Icon_{name}.png";
            Directory.CreateDirectory(MinimapBaker.Folder);
            var pixels = new Color[size * size];
            var unit = 2f / size;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(-1f + (x + 0.5f) * unit, -1f + (y + 0.5f) * unit);
                    var color = Color.clear;
                    foreach (var (sdf, layerColor) in layers)
                    {
                        // One pixel of anti-aliasing across the edge.
                        var coverage = Mathf.Clamp01(0.5f - sdf(p) / unit) * layerColor.a;
                        color = Over(color, layerColor, coverage);
                    }

                    pixels[y * size + x] = color;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Color Over(Color under, Color over, float coverage)
        {
            var alpha = coverage + under.a * (1f - coverage);
            if (alpha <= 0f)
            {
                return Color.clear;
            }

            var rgb = ((Vector4)over * coverage + (Vector4)under * (under.a * (1f - coverage))) / alpha;
            return new Color(rgb.x, rgb.y, rgb.z, alpha);
        }

        private static float Length(Vector2 p) => p.magnitude;

        /// <summary>Inside between two radii.</summary>
        private static float Band(float r, float inner, float outer) => Mathf.Max(r - outer, inner - r);

        private static float RoundedBox(Vector2 p, Vector2 half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * radius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        /// <summary>A round bottom melted into a pointed top.</summary>
        private static float Teardrop(Vector2 p)
        {
            var round = Length(p - new Vector2(0f, -0.28f)) - 0.52f;
            var tip = Polygon(new Vector2(-0.46f, -0.12f), new Vector2(0.05f, 0.92f), new Vector2(0.46f, -0.12f))(p);
            return SmoothMin(round, tip, 0.18f);
        }

        private static float SmoothMin(float a, float b, float k)
        {
            var h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        /// <summary>Signed distance to any simple polygon (negative inside).</summary>
        private static Func<Vector2, float> Polygon(params Vector2[] v)
        {
            return p =>
            {
                var d = Vector2.Dot(p - v[0], p - v[0]);
                var s = 1f;
                for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
                {
                    var e = v[j] - v[i];
                    var w = p - v[i];
                    var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                    d = Mathf.Min(d, Vector2.Dot(b, b));
                    var c1 = p.y >= v[i].y;
                    var c2 = p.y < v[j].y;
                    var c3 = e.x * w.y > e.y * w.x;
                    if ((c1 && c2 && c3) || (!c1 && !c2 && !c3))
                    {
                        s = -s;
                    }
                }

                return s * Mathf.Sqrt(d);
            };
        }
    }
}
