
using UnityEngine;
using UnityEditor;
using Pathfinding;
using System.Collections.Generic;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

[CustomEditor(typeof(AnalyticPlanner))]
public class AnalyticPlannerEditor : Editor
{

    float markerHeight = 2f;

    int observerCount(Int3 pos, float eyeHeight, GameObject[] observers)
    {

        int obCount = 0;
        foreach (GameObject observer in observers)
        {
            Vector3 fromPos = observer.transform.position;
            fromPos.y = Terrain.activeTerrain.SampleHeight(fromPos) + eyeHeight;
            Vector3 toPos = (Vector3)pos;
            toPos.y += eyeHeight;

            Vector3 diff = toPos - fromPos;

            if (!Physics.Raycast(fromPos, diff, diff.magnitude))
            {
                ++obCount;
            }
        }

        return obCount;
    }

    float SpeedOnGrade(float grade)
    {
        float scale = 1000f;
        float max_speed = 1f;
        return max_speed / (1f + scale * Mathf.Abs(grade));
    }

    public override void OnInspectorGUI()
    {
        AnalyticPlanner planner = (AnalyticPlanner)target;
        planner.pointVisualizer = (GameObject)EditorGUILayout.ObjectField("pointVisualizer",planner.pointVisualizer, typeof(Object), true);
        planner.lineVisualizer = (GameObject)EditorGUILayout.ObjectField("lineVisualizer", planner.lineVisualizer, typeof(Object), true);
        planner.moveObserverPenalty = EditorGUILayout.FloatField("moveObserverPenalty", planner.moveObserverPenalty);

        float eyeHeight = 1.62f;

        GameObject pv = (GameObject)planner.pointVisualizer;
        NodeMarkerGenerator nmg = pv.GetComponent<NodeMarkerGenerator>();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical();

        if (GUILayout.Button("Compute Observer Data"))
        {
            AstarPath.active.data.Awake();
            AstarPath.active.Scan();
            NavGraph graph = AstarPath.active.graphs[0];

            GameObject[] observers = GameObject.FindGameObjectsWithTag("RedForce");

            // Count observers at each waypoint
            planner.observerCount = new Dictionary<Int3, float>();
            graph.GetNodes(node =>
            {
                int c = observerCount(node.position, eyeHeight, observers);
                planner.observerCount[node.position] = c;
            });
        }

        if (GUILayout.Button("Recompute Costs"))
        {
            AstarPath.active.data.Awake();
            AstarPath.active.Scan();
            NavGraph graph = AstarPath.active.graphs[0];
            graph.GetNodes(node =>
            {
                PointNode pnodeA = (PointNode)node;
                for (int i=0;i<pnodeA.connections.Length;i++)
                {
                    Connection conn = pnodeA.connections[i];
                    PointNode pnodeB = (PointNode)conn.node;
                    float dist = ((Vector3)pnodeA.position - (Vector3)pnodeB.position).magnitude;
                    float grade = (pnodeA.position.y - pnodeB.position.y)/dist;
                    float ave_observers = planner.observerCount[pnodeA.position] + planner.observerCount[pnodeB.position]/2f;
                    uint obs_penalty = (uint)(dist * ave_observers * planner.moveObserverPenalty);
                    float edgeTime = 1f/SpeedOnGrade(grade);

                    Connection new_conn = new Connection(pnodeB, (uint)edgeTime*10 + obs_penalty, conn.shapeEdge);
                    pnodeA.connections[i] = new_conn;
                }
            });

        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.BeginVertical();

        if (GUILayout.Button("Clear Visualization"))
        {
            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;
        }

        if (GUILayout.Button("Visualize Nodes"))
        {
            nmg.ClearMarkers();
            NavGraph graph = AstarPath.active.graphs[0];
            TerrainGridGraph gridGraph = (TerrainGridGraph)graph;
            float spacing = gridGraph.node_size;
            float scale = spacing / 2f;
            graph.GetNodes(node => {
                Vector3 markerPos = (Vector3)node.position;
                markerPos.y += markerHeight;
                nmg.CreateMarker(markerPos, Color.blue, scale);
            });
        }

        if (GUILayout.Button("Visualize Frac Observers"))
        {
            nmg.ClearMarkers();
            NavGraph graph = AstarPath.active.graphs[0];
            TerrainGridGraph gridGraph = (TerrainGridGraph)graph;
            float spacing = gridGraph.node_size;
            float scale = spacing / 2f;
            GameObject[] observers = GameObject.FindGameObjectsWithTag("RedForce");
            graph.GetNodes(node => {
                float frac = (float) planner.observerCount[node.position] / observers.Length;
                Vector3 markerPos = (Vector3)node.position;
                markerPos.y += markerHeight;
                nmg.CreateMarker(markerPos, new Color(frac,frac,frac), scale);
            });
        }

        if (GUILayout.Button("Visualize Edge Cost"))
        {
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }     
}