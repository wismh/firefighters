using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace _Project.Editor.City
{
    /// <summary>
    /// Readable, denser copies of car body meshes for dents. SimpleTown bodies are a few hundred triangles with edges
    /// up to 7 m, so a pole hitting the middle of a bumper would touch no vertex at all. Every edge longer than
    /// <see cref="MaxEdge"/> is split in half until none is left; the split depends on the edge alone, so both
    /// triangles on an edge split it the same way and no cracks appear. Vertices split in the source (hard edges,
    /// UV seams) stay split, so panel creases stay sharp.
    /// </summary>
    public static class DeformableMeshBuilder
    {
        public const string OutputFolder = "Assets/_Project/Art/Vehicles/Deformable";
        private const float MaxEdge = 0.45f;
        private const int MaxPasses = 8;

        private static readonly Dictionary<Mesh, Mesh> Built = new Dictionary<Mesh, Mesh>();

        /// <summary>Dense copy of <paramref name="source"/>, built once per source mesh and saved as an asset.</summary>
        public static Mesh Ensure(Mesh source)
        {
            if (Built.TryGetValue(source, out var built) && built != null)
            {
                return built;
            }

            Directory.CreateDirectory(OutputFolder);
            var path = $"{OutputFolder}/{source.name}_deformable.asset";
            var mesh = Subdivide(source);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Overwrite in place: prefabs keep their reference to the asset.
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                mesh = existing;
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }

            Debug.Log($"[city] deformable {source.name}: {source.triangles.Length / 3} -> " +
                      $"{mesh.triangles.Length / 3} triangles, {mesh.vertexCount} vertices");
            Built[source] = mesh;
            return mesh;
        }

        /// <summary>Forgets the meshes built in this session, so the next prefab build regenerates them.</summary>
        public static void Reset()
        {
            Built.Clear();
        }

        private static Mesh Subdivide(Mesh source)
        {
            var positions = new List<Vector3>(source.vertices);
            var normals = new List<Vector3>(source.normals);
            var uvs = new List<Vector2>(source.uv);
            var tangents = new List<Vector4>(source.tangents);
            var triangles = new List<int>(source.triangles);
            var maxSq = MaxEdge * MaxEdge;

            for (var pass = 0; pass < MaxPasses; pass++)
            {
                var midpoints = new Dictionary<long, int>();
                var next = new List<int>(triangles.Count * 2);
                var split = false;
                for (var i = 0; i < triangles.Count; i += 3)
                {
                    var a = triangles[i];
                    var b = triangles[i + 1];
                    var c = triangles[i + 2];
                    var ab = IsLong(a, b) ? Midpoint(a, b) : -1;
                    var bc = IsLong(b, c) ? Midpoint(b, c) : -1;
                    var ca = IsLong(c, a) ? Midpoint(c, a) : -1;
                    var count = (ab >= 0 ? 1 : 0) + (bc >= 0 ? 1 : 0) + (ca >= 0 ? 1 : 0);
                    split |= count > 0;
                    switch (count)
                    {
                        case 0:
                            Add(a, b, c);
                            break;
                        case 3:
                            Add(a, ab, ca);
                            Add(ab, b, bc);
                            Add(ca, bc, c);
                            Add(ab, bc, ca);
                            break;
                        case 1:
                            // Rotate so the split edge is a-b.
                            if (bc >= 0)
                            {
                                (a, b, c, ab) = (b, c, a, bc);
                            }
                            else if (ca >= 0)
                            {
                                (a, b, c, ab) = (c, a, b, ca);
                            }

                            Add(a, ab, c);
                            Add(ab, b, c);
                            break;
                        default:
                            // Two split edges: rotate so the unsplit one is c-a, then cut a quad and a triangle.
                            if (ab < 0)
                            {
                                (a, b, c, ab, bc) = (b, c, a, bc, ca);
                            }
                            else if (bc < 0)
                            {
                                (a, b, c, ab, bc) = (c, a, b, ca, ab);
                            }

                            Add(ab, b, bc);
                            Add(a, ab, bc);
                            Add(a, bc, c);
                            break;
                    }
                }

                triangles = next;
                if (!split)
                {
                    break;
                }

                bool IsLong(int from, int to)
                {
                    return (positions[from] - positions[to]).sqrMagnitude > maxSq;
                }

                int Midpoint(int from, int to)
                {
                    var key = from < to ? ((long)from << 32) | (uint)to : ((long)to << 32) | (uint)from;
                    if (midpoints.TryGetValue(key, out var index))
                    {
                        return index;
                    }

                    index = positions.Count;
                    positions.Add((positions[from] + positions[to]) * 0.5f);
                    normals.Add((normals[from] + normals[to]).normalized);
                    uvs.Add((uvs[from] + uvs[to]) * 0.5f);
                    if (tangents.Count > 0)
                    {
                        var t = (tangents[from] + tangents[to]) * 0.5f;
                        var xyz = new Vector3(t.x, t.y, t.z).normalized;
                        tangents.Add(new Vector4(xyz.x, xyz.y, xyz.z, tangents[from].w));
                    }

                    midpoints.Add(key, index);
                    return index;
                }

                void Add(int x, int y, int z)
                {
                    next.Add(x);
                    next.Add(y);
                    next.Add(z);
                }
            }

            var mesh = new Mesh { name = source.name + "_deformable" };
            mesh.indexFormat = positions.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            if (tangents.Count > 0)
            {
                mesh.SetTangents(tangents);
            }

            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
