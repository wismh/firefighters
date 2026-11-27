using _Project.City;
using UnityEngine;

namespace _Project.Traffic
{
    /// <summary>Builds the lane graph from <see cref="CityLayout"/>. Right-hand traffic, one lane per direction.</summary>
    public static class RoadGraphBuilder
    {
        // Bezier handle length that approximates a quarter circle.
        private const float ArcHandle = 0.552f;

        public static RoadGraph Build(CityLayout layout, TrafficSettings settings)
        {
            var graph = new RoadGraph();
            var nodesX = layout.NodesX;
            var nodesZ = layout.NodesZ;
            var incoming = new Lane[nodesX, nodesZ, 4];
            var outgoing = new Lane[nodesX, nodesZ, 4];
            var random = new System.Random(layout.seed);

            for (var ix = 0; ix < nodesX; ix++)
            {
                for (var iz = 0; iz < nodesZ; iz++)
                {
                    var cell = new Vector2Int(ix, iz);
                    var position = layout.NodePosition(ix, iz) + Vector3.up * CityLayout.AsphaltHeight;
                    var offset = (float)random.NextDouble() * settings.SignalCycle;
                    var intersection = new Intersection(cell, position, CountSides(layout, ix, iz) >= 3,
                        settings.greenTime, settings.yellowTime, settings.allRedTime, offset);
                    graph.Intersections.Add(cell, intersection);
                }
            }

            for (var ix = 0; ix < nodesX; ix++)
            {
                for (var iz = 0; iz < nodesZ; iz++)
                {
                    foreach (var dir in new[] { GridDirection.East, GridDirection.North })
                    {
                        var offset = dir.ToOffset();
                        var jx = ix + offset.x;
                        var jz = iz + offset.y;
                        if (!layout.IsNode(jx, jz))
                        {
                            continue;
                        }

                        AddRoadLane(graph, layout, settings, ix, iz, jx, jz, dir, incoming, outgoing);
                        AddRoadLane(graph, layout, settings, jx, jz, ix, iz, dir.Opposite(), incoming, outgoing);
                    }
                }
            }

            foreach (var intersection in graph.Intersections.Values)
            {
                AddConnectors(graph, settings, intersection, incoming, outgoing);
                intersection.BuildConflicts();
            }

            return graph;
        }

        private static int CountSides(CityLayout layout, int ix, int iz)
        {
            var count = 0;
            foreach (var dir in GridDirections.All)
            {
                var o = dir.ToOffset();
                if (layout.IsNode(ix + o.x, iz + o.y))
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddRoadLane(RoadGraph graph, CityLayout layout, TrafficSettings settings,
            int fromX, int fromZ, int toX, int toZ, GridDirection dir, Lane[,,] incoming, Lane[,,] outgoing)
        {
            var forward = dir.ToVector();
            var side = dir.Right() * CityLayout.LaneOffset + Vector3.up * CityLayout.AsphaltHeight;
            var start = layout.NodePosition(fromX, fromZ) + forward * CityLayout.HalfRoad + side;
            var end = layout.NodePosition(toX, toZ) - forward * CityLayout.HalfRoad + side;
            var third = (end - start) / 3f;
            var lane = new Lane(graph.Lanes.Count, start, start + third, end - third, end, settings.roadSpeed)
            {
                EndDirection = dir,
                EndIntersection = graph.Intersections[new Vector2Int(toX, toZ)],
            };
            graph.Lanes.Add(lane);
            graph.RoadLanes.Add(lane);
            outgoing[fromX, fromZ, (int)dir] = lane;
            incoming[toX, toZ, (int)dir] = lane;
        }

        private static void AddConnectors(RoadGraph graph, TrafficSettings settings, Intersection intersection,
            Lane[,,] incoming, Lane[,,] outgoing)
        {
            var cell = intersection.Cell;
            foreach (var inDir in GridDirections.All)
            {
                var entry = incoming[cell.x, cell.y, (int)inDir];
                if (entry == null)
                {
                    continue;
                }

                foreach (var outDir in GridDirections.All)
                {
                    var exit = outgoing[cell.x, cell.y, (int)outDir];
                    if (exit == null || outDir == inDir.Opposite())
                    {
                        continue;
                    }

                    var connector = CreateConnector(graph.Lanes.Count, entry, exit, inDir, outDir, settings);
                    connector.Intersection = intersection;
                    connector.EntryLane = entry;
                    connector.EndDirection = outDir;
                    connector.ConnectorIndex = intersection.Connectors.Count;
                    connector.Next.Add(exit);
                    entry.Next.Add(connector);
                    intersection.Connectors.Add(connector);
                    graph.Lanes.Add(connector);
                }
            }
        }

        private static Lane CreateConnector(int id, Lane entry, Lane exit, GridDirection inDir, GridDirection outDir,
            TrafficSettings settings)
        {
            var p0 = entry.P3;
            var p3 = exit.P0;
            var din = inDir.ToVector();
            var dout = outDir.ToVector();
            if (inDir == outDir)
            {
                var third = (p3 - p0) / 3f;
                return new Lane(id, p0, p0 + third, p3 - third, p3, settings.straightThroughSpeed);
            }

            // The corner is where the entry line meets the exit line.
            var corner = inDir.Axis() == 0 ? new Vector3(p3.x, p0.y, p0.z) : new Vector3(p0.x, p0.y, p3.z);
            var h0 = Vector3.Distance(p0, corner) * ArcHandle;
            var h3 = Vector3.Distance(p3, corner) * ArcHandle;
            return new Lane(id, p0, p0 + din * h0, p3 - dout * h3, p3, settings.turnSpeed);
        }
    }
}
