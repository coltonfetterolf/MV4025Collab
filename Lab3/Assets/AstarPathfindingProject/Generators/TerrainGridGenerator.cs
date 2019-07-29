using System.Collections.Generic;
using UnityEngine;
// Include the Pathfinding namespace to gain access to a lot of useful classes
using Pathfinding;
// Required to save the settings
using Pathfinding.Serialization;
using Pathfinding.Util;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

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
    public float entity_speed = 10f;

    public int steps = 1;

    // Here we will store all nodes in the graph
    public PointNode[] nodes;

    GraphTransform transform;

    PointNode[][] gridNodes;

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
        pos.y = Terrain.activeTerrain.SampleHeight(pos);
        return pos;
    }

    protected override IEnumerable<Progress> ScanInternal()
    {

        Vector3 bump_up = new Vector3(0f, 1f, 0f);

        gridNodes = new PointNode[width][];
        (int, int)[] neighbor_delta = new (int, int)[] { (0, 1), (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1) };

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
                // Should check if start and end nodes are over the terrain
                PointNode start_node = gridNodes[i][j];
                List<Connection> connection_list = new List<Connection>();
                foreach ((int, int) delta in neighbor_delta)
                {
                    int a = i + delta.Item1;
                    int b = j + delta.Item2;
                    if (a < 0 || a >= width || b < 0 || b >= depth) continue;
                    PointNode end_node = gridNodes[a][b];
                    Vector3 start = CalculateNodePosition(i, j);
                    Vector3 end = CalculateNodePosition(a, b);
                    float edgeTime = (start - end).magnitude / entity_speed;
                    connection_list.Add(new Connection(end_node, (uint)edgeTime * 10000));
                }
                start_node.connections = connection_list.ToArray();
            }
        }

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

    public List<GraphNode> GetNodesInRegion(Bounds bounds)
    {

        Vector3 WorldToNode(Vector3 v_world)
        {
            float width_world = (width - 1) * node_size;
            float depth_world = (depth - 1) * node_size;
            Vector3 origin = center - new Vector3(width_world/2,0,depth_world/2);
            return (v_world - origin) / node_size;
        }
        List<GraphNode> result = new List<GraphNode>();
        Vector3 lower_node = WorldToNode(bounds.min);
        Vector3 upper_node = WorldToNode(bounds.max);
        int i_min = (int)Mathf.Max(0,Mathf.Ceil(lower_node.x));
        int i_max = (int)Mathf.Min(width-1,Mathf.FloorToInt(upper_node.x));
        int j_min = (int)Mathf.Max(0,Mathf.CeilToInt(lower_node.z));
        int j_max = (int)Mathf.Min(depth-1,Mathf.FloorToInt(upper_node.z));
        for (int j = j_min; j <= j_max; j++)
            for (int i = i_min; i <= i_max; i++)
                result.Add(gridNodes[i][j]);
        return result;
    }

    // From GridGenerator.cs
    IntRect GetRectFromBounds(Bounds bounds)
    {
        // Take the bounds and transform it using the matrix
        // Then convert that to a rectangle which contains
        // all nodes that might be inside the bounds

        //bounds = transform.InverseTransform(bounds); // Not needed?
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        int minX = Mathf.RoundToInt(min.x - 0.5F);
        int maxX = Mathf.RoundToInt(max.x - 0.5F);

        int minZ = Mathf.RoundToInt(min.z - 0.5F);
        int maxZ = Mathf.RoundToInt(max.z - 0.5F);

        var originalRect = new IntRect(minX, minZ, maxX, maxZ);

        // Rect which covers the whole grid
        var gridRect = new IntRect(0, 0, width - 1, depth - 1);

        // Clamp the rect to the grid
        return IntRect.Intersection(originalRect, gridRect);
    }

    // From GridGenerator.cs
    List<GraphNode> GetNodesInRegionOld (Bounds bounds, GraphUpdateShape shape)
    {
	    var rect = GetRectFromBounds(bounds);

	    if (nodes == null || !rect.IsValid() || nodes.Length != width*depth) {
		    return ListPool<GraphNode>.Claim();
	    }

	    // Get a buffer we can use
	    var inArea = ListPool<GraphNode>.Claim(rect.Width*rect.Height);

	    // Loop through all nodes in the rectangle
	    for (int x = rect.xmin; x <= rect.xmax; x++) {
		    for (int z = rect.ymin; z <= rect.ymax; z++) {
			    int index = z*width+x;

			    GraphNode node = nodes[index];

			    // If it is contained in the bounds (and optionally the shape)
			    // then add it to the buffer
			    if (bounds.Contains((Vector3)node.position) && (shape == null || shape.Contains((Vector3)node.position))) {
				    inArea.Add(node);
			    }
		    }
	    }

	    return inArea;
	}

    public List<GraphNode> GetNodesInRegionOld(Bounds bounds)
    {
        return GetNodesInRegionOld(bounds, null);
    }

    // Copied with minor modifications from PointGenerator.cs
    protected override void SerializeExtraInfo(GraphSerializationContext ctx)
    {
        // Serialize node data

        if (nodes == null) ctx.writer.Write(-1);

        // Length prefixed array of nodes
        ctx.writer.Write(nodes.Length);
        for (int i = 0; i < nodes.Length; i++)
        {
            // -1 indicates a null field
            if (nodes[i] == null) ctx.writer.Write(-1);
            else
            {
                ctx.writer.Write(0);
                nodes[i].SerializeNode(ctx);
            }
        }
    }

    // Copied with minor modifications from PointGenerator.cs
    protected override void DeserializeExtraInfo(GraphSerializationContext ctx)
    {
        int count = ctx.reader.ReadInt32();

        if (count == -1)
        {
            nodes = null;
            return;
        }

        nodes = new PointNode[count];
        //nodeCount = count;

        for (int i = 0; i < nodes.Length; i++)
        {
            if (ctx.reader.ReadInt32() == -1) continue;
            nodes[i] = new PointNode(active);
            nodes[i].DeserializeNode(ctx);
        }
    }
}