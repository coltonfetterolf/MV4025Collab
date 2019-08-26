using UnityEditor;
using Pathfinding;

[CustomGraphEditor(typeof(TerrainGridGraph), "Terrain Grid Graph")]
public class TerrainGridGeneratorEditor : GraphEditor
{
    // Here goes the GUI
    public override void OnInspectorGUI(NavGraph target)
    {
        var graph = target as TerrainGridGraph;

        graph.width = EditorGUILayout.IntField("Width (nodes)", graph.width);
        graph.depth = EditorGUILayout.IntField("Depth (nodes)", graph.depth);
        graph.node_size = EditorGUILayout.FloatField("Node size", graph.node_size);
        graph.center = EditorGUILayout.Vector3Field("Center", graph.center);
        graph.ground_plane_name = EditorGUILayout.TextField("Ground Plane Name", graph.ground_plane_name);
    }
}