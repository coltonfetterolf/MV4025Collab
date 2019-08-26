using System.Collections.Generic;
using UnityEngine;
// Include the Pathfinding namespace to gain access to a lot of useful classes
using Pathfinding;
// Required to save the settings
using Pathfinding.Serialization;
using Pathfinding.Util;

using UnityEditor;

// Inherit our new graph from a base graph type
[JsonOptIn]
public class TerrainGridGraph : NavGraph
{
    [JsonMember]
    public int width = 10;
    [JsonMember]
    public int depth = 10;

    [JsonMember]
    public float node_size = 10f;

    [JsonMember]
    public Vector3 center = Vector3.zero;

    [JsonMember]
    public string ground_plane_name;

    public GameObject ground;

    public int steps = 1;

    // Here we will store all nodes in the graph
    public PointNode[] nodes;

    GraphTransform transform;

    PointNode CreateNode(Vector3 position)
    {
        var node = new PointNode(active);

        // Node positions are stored as Int3. We can convert a Vector3 to an Int3 like this
        node.position = (Int3)position;
        return node;
    }

    Vector3 CalculateNodePosition(int i, int j)
    {
        float x_offset = center.x - (width - 1) * node_size / 2f;
        float z_offset = center.z - (depth - 1) * node_size / 2f;
        float x = x_offset + i * node_size;
        float z = z_offset + j * node_size;
        var pos = new Vector3(x, center.y, z);
        return pos;
    }

    float SpeedOnGrade(float grade)
    {
        float scale = 100f;
        float max_speed = 1f;
        return max_speed / (1f + scale * Mathf.Abs(grade));
    }

    protected override IEnumerable<Progress> ScanInternal()
    {
        float max_edge_time = -1f;
        Vector3 bump_up = new Vector3(0f, 1f, 0f);

        ground = (GameObject)GameObject.Find(ground_plane_name);
        PointNode[][] gridNodes = new PointNode[width][];
        (int, int)[] neighbor_delta = new(int, int)[] { (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1) };

        for (int i = 0; i < width; i++)
        {
            gridNodes[i] = new PointNode[depth];
            for (int j = 0; j < depth; j++)
            {
                Vector3 pos = CalculateNodePosition(i, j);
                gridNodes[i][j] = CreateNode(pos);
            }
        }

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                PointNode start_node = gridNodes[i][j];
                foreach ((int, int) delta in neighbor_delta)
                {
                    int a = i + delta.Item1;
                    int b = j + delta.Item2;
                    if (a < 0 || a >= width || b < 0 || b >= depth) continue;
                    PointNode end_node = gridNodes[a][b];
                    Vector3 start = CalculateNodePosition(i, j);
                    Vector3 end = CalculateNodePosition(a, b);
                    Vector3 step = (end - start) / steps;
                    float edgeTime = 0f;
                    float last_height = Terrain.activeTerrain.SampleHeight(start);
                    for (int k = 0; k < steps; k++)
                    {
                        Vector3 eval_point_ground = start + k * step;
                        Bounds tb = Terrain.activeTerrain.terrainData.bounds;
                        Bounds gb = ground.GetComponent<MeshRenderer>().bounds;
                        Vector3 frac_ground = (eval_point_ground - gb.min);
                        Vector3 ext_g = gb.extents;
                        frac_ground.Scale(new Vector3(1f / (2 * ext_g.x), 1f, 1f / (2 * ext_g.z)));
                        Vector3 ext_t = tb.extents;
                        Vector3 eval_point_terrain = frac_ground;
                        eval_point_terrain.Scale(new Vector3(2 * ext_t.x, 1f, 2 * ext_t.z));
                        eval_point_terrain += Terrain.activeTerrain.transform.position;
                        float grade = (Terrain.activeTerrain.SampleHeight(eval_point_terrain) - last_height) / step.magnitude;
                        edgeTime += step.magnitude / SpeedOnGrade(grade);
                    }
                    if (edgeTime > max_edge_time)
                        max_edge_time = edgeTime;
                    start_node.AddConnection(end_node, (uint)edgeTime * 1000); // Note that 1f is represented as (uint)1000
                }
            }
        }

        //Debug.Log("max edge time: " + max_edge_time);

        List<PointNode> allNodes = new List<PointNode>();
        for (int i = 0; i < width; i++)
            for (int j = 0; j < depth; j++)
                allNodes.Add(gridNodes[i][j]);
        nodes = allNodes.ToArray();

        // Set all the nodes to be walkable
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].Walkable = true;
        }
        yield break;
    }

    public override void GetNodes(System.Action<GraphNode> action)
    {
        if (nodes == null) return;

        for (int i = 0; i < nodes.Length; i++)
        {
            // Call the delegate
            action(nodes[i]);
        }
    }
}