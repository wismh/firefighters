using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Project.City;
using _Project.Player;
using _Project.Traffic;
using _Project.Vehicles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zenject;

namespace _Project.Editor.City
{
    /// <summary>
    /// Builds MainScene as the city: road tiles, blocks with buildings, props, signals, light, player,
    /// traffic and DI. Everything is generated from <see cref="CityLayout"/>, so re-running it after a
    /// layout change rebuilds the city and the lane graph consistently.
    /// </summary>
    public static class CitySceneSetup
    {
        private const string ConfigFolder = "Assets/_Project/Configurations/City";
        internal const string LayoutPath = ConfigFolder + "/CityLayout_Default.asset";
        private const string TrafficPath = ConfigFolder + "/TrafficSettings_Default.asset";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string SignalMaterialPath = MaterialFolder + "/SignalLamp.mat";
        private const string DebrisMaterialPath = MaterialFolder + "/Debris.physicMaterial";
        private const string ScenePath = "Assets/_Project/Scenes/MainScene.unity";
        private const string InputPath = "Assets/_Project/InputSystem_Actions.inputactions";
        private const string Town = "Assets/ThirdParty/SimpleTown/Prefabs/";
        private const float BuildingSetback = 0.5f;
        private const float BuildingGap = 1f;
        private const float LampSpacing = 25f;
        private const float SidewalkPropOffset = CityLayout.HalfRoad - 1.1f;
        // Road sign poles stand this far along a block from its corner, at least 9 m from every lamp and bin.
        private const float SignAlongBlock = 28f;
        private const float SignChance = 0.35f;
        private const float GantryChance = 0.3f;
        private const float GantrySpan = 17f;
        // Outskirts rows: deep enough for flats and offices, narrow enough to leave the cinema and the mall out.
        private const float OutskirtsDepth = 16f;
        private const float OutskirtsMaxWidth = 21f;
        private const float OutskirtsSkylineHeight = 16f;
        // Sign panels in SimpleTown face local -Z, like its buildings.
        private static readonly Vector3 SignFront = Vector3.back;

        [MenuItem("Tools/City/Setup Main Scene (full rebuild)")]
        public static void SetupMainScene()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("[city] exit Play mode first");
                return;
            }

            var layout = EnsureLayout();
            var traffic = EnsureTrafficSettings();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            var views = BuildCity(layout);
            var network = CreateTraffic(layout, traffic, views);
            CreatePlayer(layout);
            CreateSceneContext();
            FireHoseBuilder.Apply(layout);
            MinimapBuilder.Apply(layout);
            ComedyBuilder.Apply();
            PedestrianBuilder.Apply();
            CityNightBuilder.Apply();
            BurningBuilder.Apply();
            SoundscapeBuilder.Apply();
            MissionBuilder.Apply();
            CityLightingSetup.Apply(true);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = network.gameObject;
            Debug.Log("[city] MainScene rebuilt");
        }

        [MenuItem("Tools/City/Rebuild City Geometry Only")]
        public static void RebuildCityOnly()
        {
            var layout = EnsureLayout();
            var old = GameObject.Find("City");
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            var views = BuildCity(layout);
            var network = Object.FindFirstObjectByType<RoadNetwork>();
            network.Setup(layout, EnsureTrafficSettings(), views);
            EditorUtility.SetDirty(network);
            EditorSceneManager.MarkSceneDirty(network.gameObject.scene);
        }

        [MenuItem("Tools/City/Rebuild Fires")]
        public static void RebuildFires()
        {
            var blocks = GameObject.Find("City/Blocks").transform;
            foreach (var fire in blocks.GetComponentsInChildren<BuildingFire>(true))
            {
                BuildingFireBuilder.RemoveFire(fire);
            }

            AddFires(EnsureLayout(), blocks);
            EditorSceneManager.MarkSceneDirty(blocks.gameObject.scene);
        }

        private static void AddFires(CityLayout layout, Transform blocks)
        {
            VfxMaterialUpgrade.Upgrade();
            // Own random stream: changing the fire count does not reshuffle the buildings.
            var random = new System.Random(layout.seed + 7919);
            foreach (var building in BuildingFireBuilder.PickBuildings(blocks, layout.burningBuildings, random))
            {
                BuildingFireBuilder.AddFire(building, random);
            }
        }

        private static CityLayout EnsureLayout()
        {
            Directory.CreateDirectory(ConfigFolder);
            var layout = AssetDatabase.LoadAssetAtPath<CityLayout>(LayoutPath);
            if (layout != null)
            {
                EnsureRoadSigns(layout);
                return layout;
            }

            layout = ScriptableObject.CreateInstance<CityLayout>();
            layout.roadStraightHalf = Load(Town + "Environemnt/road_straight_mesh");
            layout.roadCrossingHalf = Load(Town + "Environemnt/road_crossing_mesh");
            layout.roadQuarterCorner = Load(Town + "Environemnt/road_corner_mesh");
            layout.roadT = Load(Town + "Environemnt/road_t_mesh");
            layout.roadCornerTurn = Load(Town + "Environemnt/road_cornerLines_mesh");
            layout.grassTile = Load(Town + "Props/grass_square_mesh");
            layout.streetLamp = Load(Town + "Props/lamp_mesh");
            layout.trafficLight = Load(Town + "Props/traffic_light_mesh");
            layout.trees = new[]
            {
                Load(Town + "Props/tree_large_mesh"), Load(Town + "Props/tree_medium_mesh"),
                Load(Town + "Props/tree_small_mesh"), Load(Town + "Props/Prop_Tree_01"),
                Load(Town + "Props/Prop_Tree_02"), Load(Town + "Props/bush_large_mesh"),
            };
            layout.sidewalkProps = new[]
            {
                Load(Town + "Props/hydrant_mesh"), Load(Town + "Props/bin_mesh"), Load(Town + "Props/trash_mesh"),
            };
            layout.downtownBuildings = LoadBuildings(n =>
                n.Contains("Office") || n.Contains("Apartment") || n.Contains("Shop") || n.Contains("Store") ||
                n.Contains("Cinema") || n.Contains("Mall") || n.Contains("Coffee") || n.Contains("Gym") ||
                n.Contains("Police") || n.Contains("Baber"));
            layout.suburbBuildings = LoadBuildings(n =>
                n.Contains("House") || n.Contains("Garage") || n.Contains("AutoRepair") || n.Contains("Shop_0"));
            AssetDatabase.CreateAsset(layout, LayoutPath);
            return layout;
        }

        private static TrafficSettings EnsureTrafficSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TrafficSettings>(TrafficPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TrafficSettings>();
                AssetDatabase.CreateAsset(settings, TrafficPath);
            }

            settings.vehiclePrefabs = VehiclePrefabBuilder.BuildAll().ToArray();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static GameObject Load(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".prefab");
            if (prefab == null)
            {
                Debug.LogError($"[city] missing prefab {path}");
            }

            return prefab;
        }

        private static GameObject[] LoadBuildings(System.Func<string, bool> filter)
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { Town + "Buildings" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => filter(Path.GetFileNameWithoutExtension(p)))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .ToArray();
        }

        // Layouts made before road signs existed get them filled in, the rest of the layout untouched.
        private static void EnsureRoadSigns(CityLayout layout)
        {
            if (layout.signGantry != null)
            {
                return;
            }

            layout.signGantry = Load(Town + "Props/Prop_Roadsign_01");
            layout.signArmPlusX = Load(Town + "Props/Prop_Roadsign_02");
            layout.signArmMinusX = Load(Town + "Props/Prop_Roadsign_03");
            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssetIfDirty(layout);
        }

        private static List<TrafficLightView> BuildCity(CityLayout layout)
        {
            var random = new System.Random(layout.seed);
            var city = new GameObject("City").transform;
            var roads = Child(city, "Roads");
            var blocks = Child(city, "Blocks");
            var props = Child(city, "Props");
            var signals = Child(city, "Signals");

            BuildGround(layout, city);
            BuildRoads(layout, roads);
            for (var bx = 0; bx < layout.blocksX; bx++)
            {
                for (var bz = 0; bz < layout.blocksZ; bz++)
                {
                    BuildBlock(layout, blocks, bx, bz, random);
                }
            }

            BuildOutskirts(layout, Child(city, "Outskirts"));
            BuildStreetProps(layout, props, random);
            BuildRoadSigns(layout, Child(city, "RoadSigns"));
            var views = BuildSignals(layout, signals);
            AddFires(layout, blocks);

            // Signal lamps change colour at runtime through a property block, so they stay out of batching.
            var lamps = new HashSet<GameObject>(views.Select(v => v.transform.Find("Lamp").gameObject));
            foreach (var t in city.GetComponentsInChildren<Transform>(true))
            {
                // Props the car can knock over have to move, so they cannot be static batched either.
                if (lamps.Contains(t.gameObject) || t.GetComponentInParent<BuildingFire>(true) != null ||
                    t.GetComponentInParent<KnockableProp>(true) != null)
                {
                    continue;
                }

                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);
            }

            return views;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void BuildGround(CityLayout layout, Transform city)
        {
            var size = layout.Size;
            var center = layout.Center;
            // Wide enough that the sky below the horizon never shows past the fog.
            var margin = 1500f;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(city, false);
            ground.transform.position = new Vector3(center.x, -0.21f, center.z);
            ground.transform.localScale = new Vector3((size.x + margin * 2f) / 10f, 1f, (size.y + margin * 2f) / 10f);
            // The grass tile samples a texture atlas, so the big plane gets a flat colour instead.
            ground.GetComponent<MeshRenderer>().sharedMaterial = EnsureFlatMaterial("Ground", new Color(0.55f, 0.7f, 0.33f));
        }

        /// <summary>
        /// Closes the city with two rings of buildings across the outer road: a row of shops, flats and offices
        /// facing the road, and a row of tall buildings behind it for the skyline. Each row stands shoulder to
        /// shoulder, so there is no way out between them. A wall behind both rings is only a backstop for a car
        /// thrown over the roofs; nobody walking can reach it.
        /// </summary>
        private static void BuildOutskirts(CityLayout layout, Transform parent)
        {
            // Own random stream, so the outskirts do not reshuffle the blocks.
            var random = new System.Random(layout.seed + 15485);
            var front = layout.downtownBuildings.Concat(layout.suburbBuildings).Distinct().Where(p =>
            {
                var s = FootprintAtYawZero(p);
                return s.x <= OutskirtsMaxWidth && s.y <= OutskirtsDepth;
            }).ToArray();
            var skyline = front.Where(p => PrefabHeight(p) >= OutskirtsSkylineHeight).ToArray();

            var size = layout.Size;
            var h = CityLayout.HalfRoad;
            var inner = new Rect(-h, -h, size.x + h * 2f, size.y + h * 2f);
            var backInset = BuildingSetback + OutskirtsDepth + BuildingGap;
            BuildRing(front, Child(parent, "Front"), random, inner, BuildingSetback);
            BuildRing(skyline, Child(parent, "Skyline"), random, inner, backInset);

            var walls = Child(parent, "Backstop");
            var reach = backInset + OutskirtsDepth + 1f;
            var outer = new Rect(inner.xMin - reach, inner.yMin - reach, inner.width + reach * 2f,
                inner.height + reach * 2f);
            AddWall(walls, new Vector3(outer.xMin, 0f, outer.center.y), new Vector3(1f, 60f, outer.height));
            AddWall(walls, new Vector3(outer.xMax, 0f, outer.center.y), new Vector3(1f, 60f, outer.height));
            AddWall(walls, new Vector3(outer.center.x, 0f, outer.yMin), new Vector3(outer.width, 60f, 1f));
            AddWall(walls, new Vector3(outer.center.x, 0f, outer.yMax), new Vector3(outer.width, 60f, 1f));
        }

        /// <summary>
        /// Four rows facing <paramref name="inner"/> from <paramref name="inset"/> metres out. The north and south
        /// rows run through the corners and the east and west rows fill between their fronts, so the corners close.
        /// </summary>
        private static void BuildRing(GameObject[] pool, Transform parent, System.Random random, Rect inner,
            float inset)
        {
            var reach = inset + OutskirtsDepth;
            BuildTightRow(pool, parent, random, inner.xMin - reach, inner.xMax + reach,
                (from, depth) => new Vector2(from, inner.yMin - inset - depth), 180f);
            BuildTightRow(pool, parent, random, inner.xMin - reach, inner.xMax + reach,
                (from, depth) => new Vector2(from, inner.yMax + inset), 0f);
            BuildTightRow(pool, parent, random, inner.yMin - inset, inner.yMax + inset,
                (from, depth) => new Vector2(inner.xMax + inset, from), 90f);
            BuildTightRow(pool, parent, random, inner.yMin - inset, inner.yMax + inset,
                (from, depth) => new Vector2(inner.xMin - inset - depth, from), 270f);
        }

        private delegate Vector2 TightRowPlacement(float from, float depth);

        /// <summary>
        /// Picks buildings until [from, to] is full, then spreads what is left over between them: with a few dozen
        /// buildings in a row each joint stays far narrower than the player.
        /// </summary>
        private static void BuildTightRow(GameObject[] pool, Transform parent, System.Random random, float from,
            float to, TightRowPlacement placement, float yaw)
        {
            var picked = new List<GameObject>();
            var used = 0f;
            while (true)
            {
                var room = to - from - used;
                var fitting = pool.Where(p => FootprintAtYawZero(p).x <= room).ToList();
                if (fitting.Count == 0)
                {
                    break;
                }

                var prefab = fitting[random.Next(fitting.Count)];
                picked.Add(prefab);
                used += FootprintAtYawZero(prefab).x;
            }

            var joint = picked.Count > 1 ? (to - from - used) / (picked.Count - 1) : 0f;
            var cursor = from;
            foreach (var prefab in picked)
            {
                var footprint = FootprintAtYawZero(prefab);
                var building = Place(prefab, parent, placement(cursor, footprint.y), yaw);
                building.AddComponent<BoxCollider>();
                cursor += footprint.x + joint;
            }
        }

        private static float PrefabHeight(GameObject prefab)
        {
            var bounds = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Select(f => f.sharedMesh.bounds).Aggregate((a, b) =>
                {
                    a.Encapsulate(b);
                    return a;
                });
            return bounds.size.y * prefab.transform.localScale.y;
        }

        private static void AddWall(Transform parent, Vector3 position, Vector3 size)
        {
            var wall = new GameObject("Wall");
            wall.transform.SetParent(parent, false);
            wall.transform.position = position + Vector3.up * (size.y * 0.5f - 1f);
            wall.AddComponent<BoxCollider>().size = size;
        }

        private static void BuildRoads(CityLayout layout, Transform parent)
        {
            var cells = Mathf.RoundToInt(layout.blockSize / CityLayout.TileStep);
            for (var ix = 0; ix < layout.NodesX; ix++)
            {
                for (var iz = 0; iz < layout.NodesZ; iz++)
                {
                    var node = layout.NodePosition(ix, iz);
                    BuildNode(layout, parent, ix, iz, node);

                    if (layout.IsNode(ix + 1, iz))
                    {
                        var segment = Child(parent, $"Road_X_{ix}_{iz}");
                        var x0 = node.x + CityLayout.HalfRoad;
                        for (var k = 0; k < cells; k++)
                        {
                            var prefab = k == 0 || k == cells - 1 ? layout.roadCrossingHalf : layout.roadStraightHalf;
                            var xs = x0 + k * CityLayout.TileStep;
                            Place(prefab, segment, new Vector2(xs, node.z), 0f);
                            Place(prefab, segment, new Vector2(xs, node.z - CityLayout.HalfRoad), 180f);
                        }
                    }

                    if (layout.IsNode(ix, iz + 1))
                    {
                        var segment = Child(parent, $"Road_Z_{ix}_{iz}");
                        var z0 = node.z + CityLayout.HalfRoad;
                        for (var k = 0; k < cells; k++)
                        {
                            var prefab = k == 0 || k == cells - 1 ? layout.roadCrossingHalf : layout.roadStraightHalf;
                            var zs = z0 + k * CityLayout.TileStep;
                            Place(prefab, segment, new Vector2(node.x, zs), 90f);
                            Place(prefab, segment, new Vector2(node.x - CityLayout.HalfRoad, zs), 270f);
                        }
                    }
                }
            }

            foreach (var filter in parent.GetComponentsInChildren<MeshFilter>(true))
            {
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }
        }

        private static void BuildNode(CityLayout layout, Transform parent, int ix, int iz, Vector3 node)
        {
            var open = GridDirections.All.Where(d =>
            {
                var o = d.ToOffset();
                return layout.IsNode(ix + o.x, iz + o.y);
            }).ToList();
            var holder = Child(parent, $"Node_{ix}_{iz}");
            var h = CityLayout.HalfRoad;
            var min = new Vector2(node.x - h, node.z - h);

            if (open.Count == 4)
            {
                Place(layout.roadQuarterCorner, holder, new Vector2(node.x, node.z), 0f);
                Place(layout.roadQuarterCorner, holder, new Vector2(node.x, node.z - h), 90f);
                Place(layout.roadQuarterCorner, holder, new Vector2(node.x - h, node.z - h), 180f);
                Place(layout.roadQuarterCorner, holder, new Vector2(node.x - h, node.z), 270f);
            }
            else if (open.Count == 3)
            {
                var closed = GridDirections.All.First(d => !open.Contains(d));
                // The T tile's closed side faces +Z.
                Place(layout.roadT, holder, min, closed.YawFromForward());
            }
            else
            {
                // The corner tile opens to +X and +Z.
                var yaw = open.Contains(GridDirection.East)
                    ? open.Contains(GridDirection.North) ? 0f : 90f
                    : open.Contains(GridDirection.South) ? 180f : 270f;
                Place(layout.roadCornerTurn, holder, min, yaw);
            }
        }

        private static void BuildBlock(CityLayout layout, Transform parent, int bx, int bz, System.Random random)
        {
            var period = layout.Period;
            var h = CityLayout.HalfRoad;
            var size = (float)layout.blockSize;
            var x0 = bx * period + h;
            var z0 = bz * period + h;
            var x1 = x0 + size;
            var z1 = z0 + size;
            var block = Child(parent, $"Block_{bx}_{bz}");

            var grass = (GameObject)PrefabUtility.InstantiatePrefab(layout.grassTile, block);
            grass.transform.localScale = new Vector3(size / CityLayout.TileStep, 1f, size / CityLayout.TileStep);
            AlignMin(grass, new Vector2(x0, z0));
            var groundCollider = new GameObject("GroundCollider");
            groundCollider.transform.SetParent(block, false);
            groundCollider.transform.position = new Vector3(x0 + size * 0.5f, -0.5f, z0 + size * 0.5f);
            groundCollider.AddComponent<BoxCollider>().size = new Vector3(size, 1f, size);

            var outer = bx == 0 || bz == 0 || bx == layout.blocksX - 1 || bz == layout.blocksZ - 1;
            var pool = outer ? layout.suburbBuildings : layout.downtownBuildings;
            var maxDepth = size * 0.5f - BuildingGap;

            // North and south rows run the full block width; east and west rows fill the gap between them.
            var northDepth = BuildRow(pool, block, random, x0 + BuildingSetback, x1 - BuildingSetback, maxDepth,
                (from, width, depth) => new Vector2(from, z1 - BuildingSetback - depth), 180f);
            var southDepth = BuildRow(pool, block, random, x0 + BuildingSetback, x1 - BuildingSetback, maxDepth,
                (from, width, depth) => new Vector2(from, z0 + BuildingSetback), 0f);
            var zFrom = z0 + BuildingSetback + southDepth + BuildingGap;
            var zTo = z1 - BuildingSetback - northDepth - BuildingGap;
            var sideDepth = Mathf.Min(maxDepth, size * 0.5f - 4f);
            var eastDepth = BuildRow(pool, block, random, zFrom, zTo, sideDepth,
                (from, width, depth) => new Vector2(x1 - BuildingSetback - depth, from), 270f);
            var westDepth = BuildRow(pool, block, random, zFrom, zTo, sideDepth,
                (from, width, depth) => new Vector2(x0 + BuildingSetback, from), 90f);

            // A few trees in the yard left in the middle.
            var yardMin = new Vector2(x0 + BuildingSetback + westDepth + 2f, zFrom + 1f);
            var yardMax = new Vector2(x1 - BuildingSetback - eastDepth - 2f, zTo - 1f);
            if (yardMax.x - yardMin.x < 4f || yardMax.y - yardMin.y < 4f)
            {
                return;
            }

            var trees = random.Next(2, 6);
            for (var i = 0; i < trees; i++)
            {
                var tree = layout.trees[random.Next(layout.trees.Length)];
                var position = new Vector3(Mathf.Lerp(yardMin.x, yardMax.x, (float)random.NextDouble()), 0f,
                    Mathf.Lerp(yardMin.y, yardMax.y, (float)random.NextDouble()));
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(tree, block);
                instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, random.Next(360), 0f));
                AddCapsule(instance, Vector3.zero, 0.5f, 4f);
            }
        }

        private delegate Vector2 RowPlacement(float from, float width, float depth);

        /// <summary>Fills [from, to] along the row with random buildings. Returns the deepest building used.</summary>
        private static float BuildRow(GameObject[] pool, Transform parent, System.Random random, float from, float to,
            float maxDepth, RowPlacement placement, float yaw)
        {
            var deepest = 0f;
            var cursor = from;
            while (true)
            {
                var room = to - cursor;
                var fitting = pool.Where(p =>
                {
                    var s = FootprintAtYawZero(p);
                    return s.x <= room && s.y <= maxDepth;
                }).ToList();
                if (fitting.Count == 0)
                {
                    return deepest;
                }

                var prefab = fitting[random.Next(fitting.Count)];
                var footprint = FootprintAtYawZero(prefab);
                var min = placement(cursor, footprint.x, footprint.y);
                var building = Place(prefab, parent, min, yaw);
                building.AddComponent<BoxCollider>();
                deepest = Mathf.Max(deepest, footprint.y);
                cursor += footprint.x + BuildingGap;
            }
        }

        private static readonly Dictionary<GameObject, Vector2> FootprintCache = new Dictionary<GameObject, Vector2>();

        /// <summary>Building frontage (local X) and depth (local Z).</summary>
        private static Vector2 FootprintAtYawZero(GameObject prefab)
        {
            if (FootprintCache.TryGetValue(prefab, out var cached))
            {
                return cached;
            }

            var bounds = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Select(f => f.sharedMesh.bounds).Aggregate((a, b) =>
                {
                    a.Encapsulate(b);
                    return a;
                });
            var scale = prefab.transform.localScale;
            var footprint = new Vector2(bounds.size.x * scale.x, bounds.size.z * scale.z);
            FootprintCache[prefab] = footprint;
            return footprint;
        }

        private static void BuildStreetProps(CityLayout layout, Transform parent, System.Random random)
        {
            for (var ix = 0; ix < layout.NodesX; ix++)
            {
                for (var iz = 0; iz < layout.NodesZ; iz++)
                {
                    var node = layout.NodePosition(ix, iz);
                    if (layout.IsNode(ix + 1, iz))
                    {
                        for (var d = LampSpacing * 0.5f; d < layout.blockSize; d += LampSpacing)
                        {
                            var x = node.x + CityLayout.HalfRoad + d;
                            AddLamp(layout, parent, new Vector3(x, 0f, node.z + SidewalkPropOffset), Vector3.back);
                            AddLamp(layout, parent, new Vector3(x + 6f, 0f, node.z - SidewalkPropOffset), Vector3.forward);
                            AddSidewalkProp(layout, parent, random, new Vector3(x + 3f, 0f, node.z + SidewalkPropOffset));
                        }
                    }

                    if (layout.IsNode(ix, iz + 1))
                    {
                        for (var d = LampSpacing * 0.5f; d < layout.blockSize; d += LampSpacing)
                        {
                            var z = node.z + CityLayout.HalfRoad + d;
                            AddLamp(layout, parent, new Vector3(node.x + SidewalkPropOffset, 0f, z), Vector3.left);
                            AddLamp(layout, parent, new Vector3(node.x - SidewalkPropOffset, 0f, z + 6f), Vector3.right);
                            AddSidewalkProp(layout, parent, random, new Vector3(node.x - SidewalkPropOffset, 0f, z + 3f));
                        }
                    }
                }
            }
        }

        private static void AddLamp(CityLayout layout, Transform parent, Vector3 position, Vector3 towardRoad)
        {
            var lamp = (GameObject)PrefabUtility.InstantiatePrefab(layout.streetLamp, parent);
            // The pole of lamp_mesh stands 1.4 m off its pivot along local +Z; the head overhangs along -Z. The pole
            // goes to the kerb, and the head over the road.
            var rotation = Quaternion.LookRotation(-towardRoad);
            lamp.transform.SetPositionAndRotation(position + towardRoad * (2f * 1.375f), rotation);
            AddCapsule(lamp, new Vector3(0f, 0f, 1.375f), 0.4f, 7.9f);
            AddBox(lamp, new Vector3(0f, 8.1f, -0.2f), new Vector3(0.6f, 0.9f, 3.4f));
            AddKnockable(lamp, 120f, 6f, 0.6f);
        }

        private static void AddSidewalkProp(CityLayout layout, Transform parent, System.Random random, Vector3 position)
        {
            if (random.NextDouble() > 0.5)
            {
                return;
            }

            var prefab = layout.sidewalkProps[random.Next(layout.sidewalkProps.Length)];
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            prop.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, random.Next(4) * 90f, 0f));
            prop.AddComponent<BoxCollider>();
            if (prefab.name.Contains("hydrant"))
            {
                AddKnockable(prop, 150f, 5f, 0.8f);
            }
            else
            {
                AddKnockable(prop, 30f, 2f, 0.2f);
            }
        }

        private static void BuildRoadSigns(CityLayout layout, Transform parent)
        {
            // Own random stream, so adding signs does not reshuffle the props placed from the shared one.
            var random = new System.Random(layout.seed + 4481);
            for (var ix = 0; ix < layout.NodesX; ix++)
            {
                for (var iz = 0; iz < layout.NodesZ; iz++)
                {
                    var node = layout.NodePosition(ix, iz);
                    if (layout.IsNode(ix + 1, iz))
                    {
                        TryAddRoadSign(layout, parent, random, node, Vector3.right);
                    }

                    if (layout.IsNode(ix, iz + 1))
                    {
                        TryAddRoadSign(layout, parent, random, node, Vector3.forward);
                    }
                }
            }
        }

        private static void TryAddRoadSign(CityLayout layout, Transform parent, System.Random random, Vector3 node,
            Vector3 along)
        {
            if (random.NextDouble() > SignChance)
            {
                return;
            }

            // The sign serves one direction of travel and faces the cars coming towards it.
            var travel = random.Next(2) == 0 ? along : -along;
            var face = -travel;
            var center = node + along * (CityLayout.HalfRoad + SignAlongBlock);
            // Turns the panel's local front to the oncoming cars.
            var rotation = Quaternion.LookRotation(-face) * Quaternion.Inverse(Quaternion.LookRotation(-SignFront));
            // Where the model's local +X ends up in the world.
            var localX = rotation * Vector3.right;
            if (random.NextDouble() < GantryChance)
            {
                var sign = (GameObject)PrefabUtility.InstantiatePrefab(layout.signGantry, parent);
                sign.transform.SetPositionAndRotation(center - localX * (GantrySpan * 0.5f), rotation);
                AddCapsule(sign, Vector3.zero, 0.4f, 10f);
                AddCapsule(sign, new Vector3(GantrySpan, 0f, 0f), 0.4f, 10f);
                AddBox(sign, new Vector3(GantrySpan * 0.5f, 8.4f, 0f), new Vector3(GantrySpan + 0.8f, 3.6f, 0.4f));
                AddKnockable(sign, 400f, 8f, 0.8f);
                return;
            }

            // Cantilever: the pole on the right-hand sidewalk of the travel direction, the arm over the lane.
            var right = Vector3.Cross(Vector3.up, travel);
            var armPlusX = Vector3.Dot(localX, -right) > 0f;
            var prefab = armPlusX ? layout.signArmPlusX : layout.signArmMinusX;
            var arm = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            arm.transform.SetPositionAndRotation(center + right * SidewalkPropOffset, rotation);
            AddCapsule(arm, Vector3.zero, 0.4f, 9.4f);
            var armSide = armPlusX ? 1f : -1f;
            AddBox(arm, new Vector3(armSide * 3.9f, 8.25f, 0f), new Vector3(8.4f, 2.3f, 0.4f));
            AddKnockable(arm, 150f, 6f, 0.7f);
        }

        private static void AddKnockable(GameObject instance, float mass, float breakSpeed, float dentHardness)
        {
            var prop = instance.AddComponent<KnockableProp>();
            prop.Setup(mass, breakSpeed, dentHardness, 0.9f, EnsureDebrisMaterial());
            prop.SetKind(ComedyBuilder.KindOf(instance));
        }

        private static PhysicsMaterial EnsureDebrisMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(DebrisMaterialPath);
            if (material != null)
            {
                return material;
            }

            Directory.CreateDirectory(MaterialFolder);
            // Minimum wins over the default Average, so a car on top of a fallen lamp slides instead of sticking.
            material = new PhysicsMaterial("Debris")
            {
                dynamicFriction = 0.1f,
                staticFriction = 0.1f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(material, DebrisMaterialPath);
            return material;
        }

        private static void AddCapsule(GameObject instance, Vector3 basePoint, float radius, float height)
        {
            var capsule = instance.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = height;
            capsule.center = basePoint + new Vector3(0f, height * 0.5f, 0f);
        }

        private static void AddBox(GameObject instance, Vector3 center, Vector3 size)
        {
            var box = instance.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        private static List<TrafficLightView> BuildSignals(CityLayout layout, Transform parent)
        {
            var views = new List<TrafficLightView>();
            var lampMaterial = EnsureSignalMaterial();
            for (var ix = 0; ix < layout.NodesX; ix++)
            {
                for (var iz = 0; iz < layout.NodesZ; iz++)
                {
                    var incoming = GridDirections.All.Where(d =>
                    {
                        // A car travelling in d arrives from the neighbour on the opposite side.
                        var o = d.Opposite().ToOffset();
                        return layout.IsNode(ix + o.x, iz + o.y);
                    }).ToList();
                    if (incoming.Count < 3)
                    {
                        continue;
                    }

                    var node = layout.NodePosition(ix, iz);
                    foreach (var dir in incoming)
                    {
                        var forward = dir.ToVector();
                        var right = dir.Right();
                        var position = node - forward * (CityLayout.HalfRoad + 1f) + right * SidewalkPropOffset;
                        var towardRoad = -right;
                        var light = (GameObject)PrefabUtility.InstantiatePrefab(layout.trafficLight, parent);
                        light.name = $"Signal_{ix}_{iz}_{dir}";
                        light.transform.SetPositionAndRotation(position,
                            Quaternion.Euler(0f, YawToFace(layout.trafficLight, towardRoad), 0f));
                        AddCapsule(light, new Vector3(0f, 0f, -0.15f), 0.4f, 7.9f);
                        AddBox(light, new Vector3(0f, 7.1f, 0.2f), new Vector3(1.1f, 1.5f, 1f));
                        AddKnockable(light, 200f, 8f, 0.7f);

                        var bounds = WorldBounds(light);
                        var reach = Mathf.Max(0.6f, Vector3.Dot(bounds.center - position, towardRoad) * 2f - 0.6f);
                        var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        lamp.name = "Lamp";
                        Object.DestroyImmediate(lamp.GetComponent<Collider>());
                        lamp.transform.SetParent(light.transform, true);
                        lamp.transform.position = position + towardRoad * reach + Vector3.up * (bounds.max.y - 1.2f) -
                                                  forward * 0.4f;
                        lamp.transform.localScale = Vector3.one * 0.7f;
                        var lampRenderer = lamp.GetComponent<MeshRenderer>();
                        lampRenderer.sharedMaterial = lampMaterial;
                        lampRenderer.shadowCastingMode = ShadowCastingMode.Off;

                        var view = light.AddComponent<TrafficLightView>();
                        view.Setup(new Vector2Int(ix, iz), dir, lampRenderer, light.GetComponent<KnockableProp>());
                        views.Add(view);
                    }
                }
            }

            return views;
        }

        internal static Material EnsureFlatMaterial(string name, Color color)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Directory.CreateDirectory(MaterialFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static Material EnsureUnlitMaterial(string name, Color color)
        {
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Directory.CreateDirectory(MaterialFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Material EnsureSignalMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SignalMaterialPath);
            if (material != null)
            {
                return material;
            }

            Directory.CreateDirectory(MaterialFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", Color.red);
            AssetDatabase.CreateAsset(material, SignalMaterialPath);
            return material;
        }

        private static RoadNetwork CreateTraffic(CityLayout layout, TrafficSettings settings, List<TrafficLightView> views)
        {
            var traffic = new GameObject("Traffic");
            var network = traffic.AddComponent<RoadNetwork>();
            network.Setup(layout, settings, views);
            var system = traffic.AddComponent<TrafficSystem>();
            var so = new SerializedObject(system);
            so.FindProperty("settings").objectReferenceValue = settings;
            so.ApplyModifiedPropertiesWithoutUndo();
            return network;
        }

        private static void CreatePlayer(CityLayout layout)
        {
            var player = new GameObject("Player");
            var body = new GameObject("Body");
            body.transform.SetParent(player.transform, false);
            var start = layout.NodePosition(layout.blocksX / 2, layout.blocksZ / 2) +
                        new Vector3(CityLayout.HalfRoad + 12f, 0.05f, CityLayout.HalfRoad - 1.25f);
            body.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, -90f, 0f));

            var character = body.AddComponent<CharacterController>();
            character.height = 2.4f;
            character.radius = 0.45f;
            character.center = new Vector3(0f, 1.2f, 0f);
            character.stepOffset = 0.45f;
            character.slopeLimit = 50f;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(body.transform, false);
            pivot.localPosition = new Vector3(0f, 2.2f, 0f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(pivot, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 700f;
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraObject.AddComponent<AudioListener>();
            var vehicleCamera = cameraObject.AddComponent<VehicleCamera>();
            vehicleCamera.enabled = false;

            var walker = body.AddComponent<FirstPersonController>();
            SetRefs(walker, ("character", character), ("cameraPivot", pivot));

            var modes = player.AddComponent<PlayerModeController>();
            SetRefs(modes, ("walker", walker), ("character", character), ("cameraTransform", cameraObject.transform),
                ("vehicleCamera", vehicleCamera));
        }

        private static void CreateSceneContext()
        {
            var contextObject = new GameObject("SceneContext");
            var installer = contextObject.AddComponent<CityInstaller>();
            SetRefs(installer, ("inputActions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath)));
            var context = contextObject.AddComponent<SceneContext>();
            var so = new SerializedObject(context);
            var installers = so.FindProperty("_monoInstallers");
            installers.arraySize = 1;
            installers.GetArrayElementAtIndex(0).objectReferenceValue = installer;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                so.FindProperty(field).objectReferenceValue = value;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Place(GameObject prefab, Transform parent, Vector2 rectMin, float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            AlignMin(instance, rectMin);
            return instance;
        }

        /// <summary>Shifts the object so its world bounds start at rectMin on X/Z. Tile pivots are at a corner.</summary>
        private static void AlignMin(GameObject instance, Vector2 rectMin)
        {
            var bounds = WorldBounds(instance);
            instance.transform.position += new Vector3(rectMin.x - bounds.min.x, 0f, rectMin.y - bounds.min.z);
        }

        private static Bounds WorldBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            return bounds;
        }

        /// <summary>Yaw that turns the prefab's overhang (lamp arm, signal arm) toward the given direction.</summary>
        private static float YawToFace(GameObject prefab, Vector3 direction)
        {
            var bounds = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Select(f => f.sharedMesh.bounds).Aggregate((a, b) =>
                {
                    a.Encapsulate(b);
                    return a;
                });
            var overhang = new Vector3(bounds.center.x, 0f, bounds.center.z);
            if (overhang.sqrMagnitude < 0.01f)
            {
                return Quaternion.LookRotation(direction).eulerAngles.y;
            }

            return Vector3.SignedAngle(overhang, direction, Vector3.up);
        }
    }
}
