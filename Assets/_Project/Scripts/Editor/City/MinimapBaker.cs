using System.IO;
using _Project.City;
using _Project.Minimap;
using UnityEditor;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>
    /// Draws the minimap's city picture from the scene's data rather than a camera: block grass, roads with their
    /// sidewalks from <see cref="CityLayout"/>, and building footprints from their box colliders. A camera render
    /// would carry trees, lamps, shadows and smoke; this one is clean and costs nothing at run time.
    /// </summary>
    public static class MinimapBaker
    {
        public const string Folder = "Assets/_Project/Art/Minimap";
        private const string TexturePath = Folder + "/CityMap.png";
        private const string DataPath = ProjectPaths.Configurations + "/City/MinimapData.asset";
        private const int Resolution = 1024;
        // Room past the outer roads up to the invisible walls (25 m past the city edge).
        private const float Margin = 30f;
        private const float OutlineWidth = 0.7f;

        private static readonly Color32 Outside = new Color32(96, 122, 70, 255);
        private static readonly Color32 Grass = new Color32(122, 156, 82, 255);
        private static readonly Color32 Asphalt = new Color32(62, 64, 72, 255);
        private static readonly Color32 Sidewalk = new Color32(150, 150, 146, 255);
        private static readonly Color32 Roof = new Color32(214, 204, 186, 255);
        private static readonly Color32 RoofEdge = new Color32(112, 102, 92, 255);

        public static MinimapData Bake(CityLayout layout)
        {
            var side = Mathf.Max(layout.Size.x, layout.Size.y) + Margin * 2f;
            var min = new Vector2(-Margin, -Margin);
            var metresPerPixel = side / Resolution;
            var pixels = new Color32[Resolution * Resolution];
            Fill(pixels, Outside);

            var h = CityLayout.HalfRoad;
            var size = layout.Size;
            FillRect(pixels, min, metresPerPixel, new Rect(-h, -h, size.x + h * 2f, size.y + h * 2f), Grass);
            for (var ix = 0; ix < layout.NodesX; ix++)
            {
                var x = layout.NodePosition(ix, 0).x;
                DrawRoad(pixels, min, metresPerPixel, new Rect(x - h, -h, h * 2f, size.y + h * 2f), true);
            }

            for (var iz = 0; iz < layout.NodesZ; iz++)
            {
                var z = layout.NodePosition(0, iz).z;
                DrawRoad(pixels, min, metresPerPixel, new Rect(-h, z - h, size.x + h * 2f, h * 2f), false);
            }

            // Asphalt over the crossings, so the sidewalks of one road don't cut across the other.
            for (var ix = 0; ix < layout.NodesX; ix++)
            {
                for (var iz = 0; iz < layout.NodesZ; iz++)
                {
                    var node = layout.NodePosition(ix, iz);
                    // A plus of asphalt: each road's lanes run through, only the four corner curbs stay.
                    // An arm runs out only where a road leaves the node, so the outer sidewalk stays unbroken.
                    var inner = h - CityLayout.SidewalkWidth;
                    var south = layout.IsNode(ix, iz - 1) ? h : inner;
                    var north = layout.IsNode(ix, iz + 1) ? h : inner;
                    var west = layout.IsNode(ix - 1, iz) ? h : inner;
                    var east = layout.IsNode(ix + 1, iz) ? h : inner;
                    FillRect(pixels, min, metresPerPixel,
                        new Rect(node.x - inner, node.z - south, inner * 2f, south + north), Asphalt);
                    FillRect(pixels, min, metresPerPixel,
                        new Rect(node.x - west, node.z - inner, west + east, inner * 2f), Asphalt);
                }
            }

            // The outer sidewalk runs round the whole city; the road strips' own ends cut it at every crossing.
            var sw = CityLayout.SidewalkWidth;
            var outer = new Rect(-h, -h, size.x + h * 2f, size.y + h * 2f);
            FillRect(pixels, min, metresPerPixel, new Rect(outer.xMin, outer.yMin, outer.width, sw), Sidewalk);
            FillRect(pixels, min, metresPerPixel, new Rect(outer.xMin, outer.yMax - sw, outer.width, sw), Sidewalk);
            FillRect(pixels, min, metresPerPixel, new Rect(outer.xMin, outer.yMin, sw, outer.height), Sidewalk);
            FillRect(pixels, min, metresPerPixel, new Rect(outer.xMax - sw, outer.yMin, sw, outer.height), Sidewalk);

            var buildings = 0;
            var blocks = GameObject.Find("City/Blocks").transform;
            foreach (var box in blocks.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!box.name.StartsWith("Building_"))
                {
                    continue;
                }

                DrawFootprint(pixels, min, metresPerPixel, box);
                buildings++;
            }

            var texture = SaveTexture(pixels);
            var data = AssetDatabase.LoadAssetAtPath<MinimapData>(DataPath);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MinimapData>();
                AssetDatabase.CreateAsset(data, DataPath);
            }

            data.Setup(texture, min, side);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log($"[city] minimap baked: {buildings} buildings, {side:F0} m at {Resolution} px");
            return data;
        }

        /// <summary>A road strip: asphalt with a sidewalk along each long side.</summary>
        private static void DrawRoad(Color32[] pixels, Vector2 min, float mpp, Rect road, bool alongZ)
        {
            FillRect(pixels, min, mpp, road, Sidewalk);
            var w = CityLayout.SidewalkWidth;
            var asphalt = alongZ
                ? new Rect(road.xMin + w, road.yMin, road.width - w * 2f, road.height)
                : new Rect(road.xMin, road.yMin + w, road.width, road.height - w * 2f);
            FillRect(pixels, min, mpp, asphalt, Asphalt);
        }

        /// <summary>The collider's bottom face, possibly turned, filled as a roof with a darker rim.</summary>
        private static void DrawFootprint(Color32[] pixels, Vector2 min, float mpp, BoxCollider box)
        {
            var t = box.transform;
            var c = box.center;
            var e = box.size * 0.5f;
            var corners = new Vector2[4];
            var local = new[]
            {
                new Vector3(c.x - e.x, c.y, c.z - e.z), new Vector3(c.x + e.x, c.y, c.z - e.z),
                new Vector3(c.x + e.x, c.y, c.z + e.z), new Vector3(c.x - e.x, c.y, c.z + e.z),
            };
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (var i = 0; i < 4; i++)
            {
                var w = t.TransformPoint(local[i]);
                corners[i] = new Vector2(w.x, w.z);
                x0 = Mathf.Min(x0, w.x);
                y0 = Mathf.Min(y0, w.z);
                x1 = Mathf.Max(x1, w.x);
                y1 = Mathf.Max(y1, w.z);
            }

            // The winding depends on the transform's mirroring; the inside test takes either.
            var px0 = Mathf.Max(0, Mathf.FloorToInt((x0 - min.x) / mpp));
            var py0 = Mathf.Max(0, Mathf.FloorToInt((y0 - min.y) / mpp));
            var px1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt((x1 - min.x) / mpp));
            var py1 = Mathf.Min(Resolution - 1, Mathf.CeilToInt((y1 - min.y) / mpp));
            for (var py = py0; py <= py1; py++)
            {
                for (var px = px0; px <= px1; px++)
                {
                    var p = new Vector2(min.x + (px + 0.5f) * mpp, min.y + (py + 0.5f) * mpp);
                    var inside = InsideDistance(corners, p);
                    if (inside < 0f)
                    {
                        continue;
                    }

                    pixels[py * Resolution + px] = inside < OutlineWidth ? RoofEdge : Roof;
                }
            }
        }

        /// <summary>Distance from <paramref name="p"/> to the nearest edge if inside the convex quad, else -1.</summary>
        private static float InsideDistance(Vector2[] quad, Vector2 p)
        {
            var nearest = float.MaxValue;
            var sign = 0f;
            for (var i = 0; i < 4; i++)
            {
                var a = quad[i];
                var b = quad[(i + 1) % 4];
                var edge = b - a;
                var cross = edge.x * (p.y - a.y) - edge.y * (p.x - a.x);
                if (sign == 0f)
                {
                    sign = Mathf.Sign(cross);
                }
                else if (Mathf.Sign(cross) != sign && Mathf.Abs(cross) > 1e-5f)
                {
                    return -1f;
                }

                nearest = Mathf.Min(nearest, Mathf.Abs(cross) / edge.magnitude);
            }

            return nearest;
        }

        private static void Fill(Color32[] pixels, Color32 color)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
        }

        private static void FillRect(Color32[] pixels, Vector2 min, float mpp, Rect world, Color32 color)
        {
            var px0 = Mathf.Clamp(Mathf.RoundToInt((world.xMin - min.x) / mpp), 0, Resolution);
            var py0 = Mathf.Clamp(Mathf.RoundToInt((world.yMin - min.y) / mpp), 0, Resolution);
            var px1 = Mathf.Clamp(Mathf.RoundToInt((world.xMax - min.x) / mpp), 0, Resolution);
            var py1 = Mathf.Clamp(Mathf.RoundToInt((world.yMax - min.y) / mpp), 0, Resolution);
            for (var py = py0; py < py1; py++)
            {
                for (var px = px0; px < px1; px++)
                {
                    pixels[py * Resolution + px] = color;
                }
            }
        }

        private static Texture2D SaveTexture(Color32[] pixels)
        {
            Directory.CreateDirectory(Folder);
            var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = Resolution;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
    }
}
