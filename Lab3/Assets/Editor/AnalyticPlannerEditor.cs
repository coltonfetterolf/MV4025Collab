
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

    float markerHeight = 10f;
    Quaternion nullRotation = new Quaternion(0f,0f,0f,0f);
    float SESectorWidth = 180f;
    float FoFwidth = 15f;


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
        float scale = 500f;  /*Changed from 1000f to 500f to make less hill adverse */
        float max_speed = 1f;
        return max_speed / (1f + scale * Mathf.Abs(grade));
    }

    public override void OnInspectorGUI()
    {
        AnalyticPlanner planner = (AnalyticPlanner)target;
        planner.pointVisualizer = (GameObject)EditorGUILayout.ObjectField("pointVisualizer",planner.pointVisualizer, typeof(Object), true);
        planner.lineVisualizer = (GameObject)EditorGUILayout.ObjectField("lineVisualizer", planner.lineVisualizer, typeof(Object), true);
        planner.assaultPosMarker = (GameObject)EditorGUILayout.ObjectField("assaultPosMarker", planner.assaultPosMarker, typeof(Object), true);
        planner.moveObserverPenalty = EditorGUILayout.FloatField("moveObserverPenalty", planner.moveObserverPenalty);
        
        planner.unitWidth = EditorGUILayout.FloatField("unitWidth", planner.unitWidth);
        planner.minAssaultDist = EditorGUILayout.FloatField("minAssaultDist", planner.minAssaultDist);
        planner.maxAssaultDist = EditorGUILayout.FloatField("maxAssaultDist", planner.maxAssaultDist);
        planner.sectorWidthDegrees = EditorGUILayout.FloatField("sectorWidthDegrees", planner.sectorWidthDegrees);

        EditorGUILayout.LabelField("Planning Factor Weights");
        planner.observationWeight = EditorGUILayout.FloatField("observationWeight", planner.observationWeight);
        planner.pathWeight = EditorGUILayout.FloatField("pathWeight", planner.pathWeight);
        planner.assaultDistWeight = EditorGUILayout.FloatField("assaultDistWeight", planner.assaultDistWeight);
        

        float eyeHeight = 1.62f;

        GameObject pv = (GameObject)planner.pointVisualizer;
        NodeMarkerGenerator nmg = pv.GetComponent<NodeMarkerGenerator>();


        if (GUILayout.Button("Compute Observer Data"))
        {
            AstarPath.active.data.Awake();
            AstarPath.active.Scan();
            NavGraph graph = AstarPath.active.graphs[0];

            int count = 0;
            graph.GetNodes(node =>
            {
                ++count;
            });

            GameObject[] observers = GameObject.FindGameObjectsWithTag("RedForce");

            System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
            stopWatch.Start();
            
            // Count observers at each waypoint
            planner.observerCount = new Dictionary<Int3, float>();
            graph.GetNodes(node =>
            {
                int c = observerCount(node.position, eyeHeight, observers);
                planner.observerCount[node.position] = c;
            });

            Debug.Log("At finish, nodes processed: " + count + " Elapsed: " + stopWatch.Elapsed);
        }

        if (GUILayout.Button("Average Over Unit Size"))
        {
            planner.aveObsCount = new Dictionary<Int3, float>();
            NavGraph graph = AstarPath.active.graphs[0];
            GameObject[] observers = GameObject.FindGameObjectsWithTag("RedForce");
            Vector3 size = planner.unitWidth * Vector3.one;
            int pad = (int)Mathf.Floor(planner.unitWidth / 2 / (graph as TerrainGridGraph).node_size);
            int expectedNumNodes = Mathf.RoundToInt(Mathf.Pow(1f + 2 * pad, 2f));
            float maxCount = observers.Length;
            graph.GetNodes(node => {
                Bounds bounds = new Bounds((Vector3)node.position, size);
                List<GraphNode> neighbors = (graph as TerrainGridGraph).GetNodesInRegion(bounds);
                if (neighbors.Count < expectedNumNodes)
                {
                    // Not enough space for unit here (hangs off edge of map)
                    // Treat as a location that can be seen by all observers everywhere
                    planner.aveObsCount[node.position] = maxCount;
                }
                else
                {
                    float score = 0f;
                    foreach (GraphNode nd in neighbors)
                    {
                        if (!nd.Walkable)
                        {
                            score = maxCount;
                            break;
                        }
                        score += planner.observerCount[nd.position];
                    }
                    planner.aveObsCount[node.position] = score / expectedNumNodes;
                }
            });
        }

        if (GUILayout.Button("Recompute Costs"))
        {
            AstarPath.active.data.Awake();
            AstarPath.active.Scan();
            NavGraph graph = AstarPath.active.graphs[0];
            planner.nodePentalty = new Dictionary<Vector3, float>();
            graph.GetNodes(node =>
            {
                PointNode pnodeA = (PointNode)node;
                for (int i = 0; i < pnodeA.connections.Length; i++)
                {
                    Connection conn = pnodeA.connections[i];
                    PointNode pnodeB = (PointNode)conn.node;
                    float dist = ((Vector3)pnodeA.position - (Vector3)pnodeB.position).magnitude;
                    float ave_observers = (planner.observerCount[pnodeA.position] + planner.observerCount[pnodeB.position]) / 2f;
                    uint obs_penalty = (uint)(dist * ave_observers * planner.moveObserverPenalty);
                    planner.nodePentalty[(Vector3)pnodeB.position] = obs_penalty;
                    int steps = 5;
                    Vector3 start = (Vector3)pnodeA.position;
                    Vector3 end = (Vector3)pnodeB.position;
                    Vector3 step = (end - start) / steps;
                    float edgeTime = 0f;
                    float last_height = Terrain.activeTerrain.SampleHeight(start);
                    for (int k = 1; k <= steps; k++)
                    {
                        Vector3 eval_point_ground = start + k * step;
                        float eval_height = Terrain.activeTerrain.SampleHeight(eval_point_ground);
                        float grade = (eval_height - last_height) / step.magnitude;
                        last_height = eval_height;
                        edgeTime += step.magnitude / SpeedOnGrade(grade);
                    }

                    Connection new_conn = new Connection(pnodeB, (uint)edgeTime * 10 + obs_penalty, conn.shapeEdge);
                    pnodeA.connections[i] = new_conn;

                }
            });

        }

        EditorGUILayout.BeginHorizontal();
        planner.seed = EditorGUILayout.IntField("seed", planner.seed);
        if (GUILayout.Button("Randomize")) { planner.seed = Mathf.FloorToInt(int.MaxValue * Random.value); }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("Place Forces"))
        {
            Random.InitState(planner.seed);
            float largeRadius = 800f;
            float smallRadius = 20f;
            Vector3 mapCenter = new Vector3(1002f, 0f, 1002f);

            float theta = Random.Range(0f, 2f * Mathf.PI);
            Vector3 redCenter = mapCenter + new Vector3(largeRadius * Mathf.Cos(theta), 0f, largeRadius * Mathf.Sin(theta));
            Vector3 blueCenter = mapCenter + new Vector3(largeRadius * Mathf.Cos(theta + Mathf.PI), 0f, largeRadius * Mathf.Sin(theta + Mathf.PI));
            // Place reds
            GameObject[] objs = GameObject.FindGameObjectsWithTag("RedForce");
            Vector3 pos;
            foreach (GameObject go in objs)
            {
                pos = redCenter + smallRadius * uniformDiskVariate();
                pos.y = Terrain.activeTerrain.SampleHeight(pos);
                go.transform.position = pos;
            }
            // Place blues
            objs = GameObject.FindGameObjectsWithTag("BlueForce");
            foreach (GameObject go in objs)
            {
                pos = blueCenter + smallRadius * uniformDiskVariate();
                pos.y = Terrain.activeTerrain.SampleHeight(pos);
                go.transform.position = pos;
            }

            GameObject blueSphere = GameObject.Find("Blue Sphere");
            pos = CMOfTag("BlueForce");
            pos.y = Terrain.activeTerrain.SampleHeight(pos) + 50f;
            blueSphere.transform.position = pos;

            GameObject redSphere = GameObject.Find("Red Sphere");
            pos = CMOfTag("RedForce");
            pos.y = Terrain.activeTerrain.SampleHeight(pos) + 50f;
            redSphere.transform.position = pos;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("MEsectorAxis: " + planner.MEsectorAxis);
        if (GUILayout.Button("Set left")) { planner.MEsectorAxis = "left"; }
        if (GUILayout.Button("Set center")) { planner.MEsectorAxis = "center"; }
        if (GUILayout.Button("Set right")) { planner.MEsectorAxis = "right"; }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical();


        if (GUILayout.Button("ME Analysis"))
        {

            planner.MEpositionCost = new Dictionary<Int3, float>();
            planner.MEpathCost = new Dictionary<Int3, float>();
            planner.MEassaultDistCost = new Dictionary<Int3, float>();
            planner.MEsectorNodes = new List<GraphNode>();

            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;

            NavGraph graph = AstarPath.active.graphs[0];

            Vector3 rawRedCM = CMOfTag("RedForce");
            Vector3 redCM = rawRedCM;
            redCM.y = 0f;
            Vector3 rawBlueCM = CMOfTag("BlueForce");
            Vector3 blueCM = rawBlueCM;
            blueCM.y = 0f;
            Vector3 mainAxisDir = (redCM - blueCM).normalized;
            Vector3 leftFlank = Vector3.Cross(mainAxisDir, Vector3.up);
            Vector3 MEsectorAxisVec = Vector3.zero;
            switch (planner.MEsectorAxis)
            {
                case "left": MEsectorAxisVec = leftFlank; break;
                case "center": MEsectorAxisVec = -mainAxisDir; break;
                case "right": MEsectorAxisVec = -leftFlank; break;
            }

            float acceptanceAngle = (planner.sectorWidthDegrees / 2f) * (Mathf.PI / 180f);
            float acceptanceProduct = Mathf.Cos(acceptanceAngle);

            graph.GetNodes(node =>
            {
                Vector3 nodePos = (Vector3)node.position;
                nodePos.y = 0f;
                float nodeDist = (nodePos - redCM).magnitude;
                if (nodeDist >= planner.minAssaultDist && nodeDist <= planner.maxAssaultDist)
                {
                    Vector3 redToNode = (nodePos - redCM).normalized;
                    if (Vector3.Dot(MEsectorAxisVec, redToNode) >= acceptanceProduct)
                        planner.MEsectorNodes.Add(node);
                }
            });

            // Compute path length
            // Inputted functions for calulating path cost and assault dist cost
            foreach (GraphNode node in planner.MEsectorNodes)
            {   Vector3 nodePos = (Vector3)node.position;
                nodePos.y = 0f;
                planner.MEpathCost[node.position] = planner.pathWeight * planner.nodePentalty[(Vector3)node.position];//(nodePos - blueCM).magnitude; //change to obs penalty
                planner.MEassaultDistCost[node.position] = (nodePos - redCM).magnitude * planner.observationWeight;
            }
        }

        if (GUILayout.Button("ME Observation Cost")) { VisualizeCost(planner.aveObsCount, planner.MEsectorNodes);  }

        if (GUILayout.Button("ME Path Cost")) { VisualizeCost(planner.MEpathCost, planner.MEsectorNodes); }

        if (GUILayout.Button("ME Assault Distance Cost")) { VisualizeCost(planner.MEassaultDistCost, planner.MEsectorNodes);  }

        if (GUILayout.Button("ME Assault Position"))
        {
            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;

            foreach (GraphNode node in planner.MEsectorNodes)
            {
                //Removed weight calculations from equation since the weights are added above.
                planner.MEpositionCost[node.position] = planner.MEpathCost[node.position]
                    + planner.observationWeight * planner.observerCount[node.position] 
                    + planner.MEassaultDistCost[node.position];
            }


            // find min
            Int3 minPos = Int3.zero;
            float min = float.MaxValue;
            foreach (GraphNode node in planner.MEsectorNodes)
            {
                if (planner.MEpositionCost[node.position] < min)
                {
                    min = planner.MEpositionCost[node.position];
                    minPos = node.position;
                    planner.MEassaltLoc = node;
                }
            }

            VisualizeCost(planner.MEpositionCost, planner.MEsectorNodes);

            Vector3 rawBlueCM = CMOfTag("BlueForce");
            ABPath path = ABPath.Construct(rawBlueCM, (Vector3)minPos);
            AstarPath.StartPath(path);
            Gizmos.color = Color.white;
            lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.widthMultiplier = 5f;
            lr.positionCount = path.vectorPath.Count;
            Vector3[] positions = new Vector3[path.vectorPath.Count];

            for (int i = 0; i < path.vectorPath.Count; i++)
            {
                positions[i] = path.vectorPath[i];
                positions[i].y += 5f;
            }
            lr.SetPositions(positions);
        }

        //As of right now strict copy from ME Analysis
        //Will change the dot product information to allow for seperate planning then ME
        if (GUILayout.Button("SE1 Analysis")) 
        {
            planner.SE1positionCost = new Dictionary<Int3, float>();
            planner.SE1pathCost = new Dictionary<Int3, float>();
            planner.SE1assaultDistCost = new Dictionary<Int3, float>();
            planner.SE1sectorNodes = new List<GraphNode>();
            List<GraphNode> MEFoF = new List<GraphNode>();

            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;

            NavGraph graph = AstarPath.active.graphs[0];

            Vector3 rawRedCM = CMOfTag("RedForce");
            Vector3 redCM = rawRedCM;
            redCM.y = 0f;
            Vector3 rawBlueCM = CMOfTag("BlueForce");
            Vector3 blueCM = rawBlueCM;
            blueCM.y = 0f;
            Vector3 mainAxisDir = (redCM - blueCM).normalized;
            Vector3 MEAxixDir = (redCM - (Vector3)planner.MEassaltLoc.position).normalized;
            Vector3 leftFlank = Vector3.Cross(mainAxisDir, Vector3.up);
            Vector3 SE1sectorAxisVec = Vector3.zero;
            //Adjusted Sectors Based on Location of Main Effort
            SE1sectorAxisVec = -mainAxisDir;
            float acceptanceAngle = (SESectorWidth / 2f) * (Mathf.PI / 180f);
            float acceptanceProduct = Mathf.Cos(acceptanceAngle);
            float MEFofAngle = (FoFwidth/2f) * (Mathf.PI / 180f);
            float MEFoFProduct = Mathf.Cos(MEFofAngle);

            graph.GetNodes(node =>
            {
                Vector3 nodePos = (Vector3)node.position;
                nodePos.y = 0f;
                float nodeDist = (nodePos - redCM).magnitude;
                if (nodeDist >= planner.minAssaultDist && nodeDist <= planner.maxAssaultDist)
                {
                    Vector3 redToNode = (nodePos - redCM).normalized;
                    float meSEangle = Vector3.Dot(MEAxixDir, -redToNode);
                    if ((Vector3.Dot(SE1sectorAxisVec, redToNode) >= acceptanceProduct) && meSEangle >= 0f && meSEangle <= .5)
                        planner.SE1sectorNodes.Add(node);
                }
            });

            // Compute path length
            // Inputted functions for calulating path cost and assault dist cost
            foreach (GraphNode node in planner.SE1sectorNodes)
            {   Vector3 nodePos = (Vector3)node.position;
                nodePos.y = 0f;
                planner.SE1pathCost[node.position] = planner.pathWeight * planner.nodePentalty[(Vector3)node.position]; //(nodePos - blueCM).magnitude;
                planner.SE1assaultDistCost[node.position] = (nodePos - redCM).magnitude * planner.observationWeight;
            }
         }
        //Visualize SE1 Assault Distance
        if (GUILayout.Button("SE1 Assault Distance")) { VisualizeCost(planner.SE1assaultDistCost, planner.SE1sectorNodes); }
        //Visualize SE1 Path Cost
        if (GUILayout.Button("SE1 Path Cost")) { VisualizeCost(planner.SE1pathCost, planner.SE1sectorNodes); }

        if (GUILayout.Button("SE1 Assault Position")) 
        { 
            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;

            foreach (GraphNode node in planner.SE1sectorNodes)
            {
                planner.SE1positionCost[node.position] = planner.SE1pathCost[node.position]
                    + planner.observationWeight * planner.observerCount[node.position] 
                    + planner.SE1assaultDistCost[node.position];
            }


            // find min
            Int3 minPos = Int3.zero;
            float min = float.MaxValue;
            foreach (GraphNode node in planner.SE1sectorNodes)
            {
                if (planner.SE1positionCost[node.position] < min)
                {
                    min = planner.SE1positionCost[node.position];
                    minPos = node.position;
                    planner.SE1assaultLoc = node;
                }
            }

            VisualizeCost(planner.SE1positionCost, planner.SE1sectorNodes);

            Vector3 rawBlueCM = CMOfTag("BlueForce");
            ABPath path = ABPath.Construct(rawBlueCM, (Vector3)minPos);
            AstarPath.StartPath(path);
            Gizmos.color = Color.white;
            lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.widthMultiplier = 5f;
            lr.positionCount = path.vectorPath.Count;
            Vector3[] positions = new Vector3[path.vectorPath.Count];

            for (int i = 0; i < path.vectorPath.Count; i++)
            {
                positions[i] = path.vectorPath[i];
                positions[i].y += 5f;
            }
            lr.SetPositions(positions);
        }


        if (GUILayout.Button("SE2 Analysis")) 
        { 
            planner.SE2positionCost = new Dictionary<Int3, float>();
            planner.SE2pathCost = new Dictionary<Int3, float>();
            planner.SE2assaultDistCost = new Dictionary<Int3, float>();
            planner.SE2sectorNodes = new List<GraphNode>();
            List<GraphNode> MEFoF = new List<GraphNode>();

            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;

            NavGraph graph = AstarPath.active.graphs[0];

            Vector3 rawRedCM = CMOfTag("RedForce");
            Vector3 redCM = rawRedCM;
            redCM.y = 0f;
            Vector3 rawBlueCM = CMOfTag("BlueForce");
            Vector3 blueCM = rawBlueCM;
            blueCM.y = 0f;
            Vector3 mainAxisDir = (redCM - blueCM).normalized;
            Vector3 MEAxixDir = (redCM - (Vector3)planner.MEassaltLoc.position).normalized;
            Vector3 leftFlank = Vector3.Cross(mainAxisDir, Vector3.up);
            Vector3 SE2sectorAxisVec = Vector3.zero;           
            SE2sectorAxisVec = -mainAxisDir;
            float acceptanceAngle = (SESectorWidth / 2f) * (Mathf.PI / 180f);
            float acceptanceProduct = Mathf.Cos(acceptanceAngle);
            float MEFofAngle = (FoFwidth/2f) * (Mathf.PI / 180f);
            float MEFoFProduct = Mathf.Cos(MEFofAngle);

            graph.GetNodes(node =>
            {
                Vector3 nodePos = (Vector3)node.position;
                nodePos.y = 0f;
                float nodeDist = (nodePos - redCM).magnitude;
                if (nodeDist >= planner.minAssaultDist && nodeDist <= planner.maxAssaultDist)
                {
                    Vector3 redToNode = (nodePos - redCM).normalized;
                    float meSEangle = Vector3.Dot(-MEAxixDir, redToNode);
                    if ((Vector3.Dot(SE2sectorAxisVec, redToNode) >= acceptanceProduct) && meSEangle >= 0f && meSEangle <= .5)
                        planner.SE2sectorNodes.Add(node);
                }
            });

            // Compute path length
            // Inputted functions for calulating path cost and assault dist cost
            foreach (GraphNode node in planner.SE2sectorNodes)
            {   Vector3 nodePos = (Vector3)node.position;
                nodePos.y = 0f;
                planner.SE2pathCost[node.position] = planner.pathWeight * planner.nodePentalty[(Vector3)node.position]; //(nodePos - blueCM).magnitude;
                planner.SE2assaultDistCost[node.position] = (nodePos - redCM).magnitude * planner.observationWeight;
            }
        }

        if (GUILayout.Button("SE2 Assault Distance")) { VisualizeCost(planner.SE2assaultDistCost, planner.SE2sectorNodes); }

        if (GUILayout.Button("SE2 Path Cost")) { VisualizeCost(planner.SE2pathCost, planner.SE2sectorNodes); }

        if (GUILayout.Button("SE2 Assault Position")) 
        {
            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;

            foreach (GraphNode node in planner.SE2sectorNodes)
            {
                planner.SE2positionCost[node.position] = planner.SE2pathCost[node.position]
                    + planner.observationWeight * planner.observerCount[node.position] 
                    + planner.SE2assaultDistCost[node.position];
            }


            // find min
            Int3 minPos = Int3.zero;
            float min = float.MaxValue;
            float SE12seperationMin = 20f;
            float SE12seperationMax = 80f;
            
            foreach (GraphNode node in planner.SE2sectorNodes)
            {   
                float SE12dist = Vector3.Distance((Vector3)planner.SE1assaultLoc.position, (Vector3)node.position);//(planner.SE1assaultLoc.position - node.position).magnitude;
                Debug.Log(SE12dist);
                if (planner.SE2positionCost[node.position] < min && SE12dist >= SE12seperationMin && SE12dist <= SE12seperationMax)
                {
                    min = planner.SE2positionCost[node.position];
                    minPos = node.position;
                    planner.SE2assaultLoc = node;
                }
            }

            VisualizeCost(planner.SE2positionCost, planner.SE2sectorNodes);

            Vector3 rawBlueCM = CMOfTag("BlueForce");
            ABPath path = ABPath.Construct(rawBlueCM, (Vector3)minPos);
            AstarPath.StartPath(path);
            Gizmos.color = Color.white;
            lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.widthMultiplier = 5f;
            lr.positionCount = path.vectorPath.Count;
            Vector3[] positions = new Vector3[path.vectorPath.Count];

            for (int i = 0; i < path.vectorPath.Count; i++)
            {
                positions[i] = path.vectorPath[i];
                positions[i].y += 5f;
            }
            lr.SetPositions(positions);
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

        if (GUILayout.Button("Visualize Ave Observer Count"))
        {
            nmg.ClearMarkers();
            NavGraph graph = AstarPath.active.graphs[0];
            TerrainGridGraph gridGraph = (TerrainGridGraph)graph;
            float spacing = gridGraph.node_size;
            float scale = spacing / 2f;
            float min = float.MaxValue;
            float max = float.MinValue;
            graph.GetNodes(node => {
                float score = planner.aveObsCount[node.position];
                if (score < min)
                    min = score;
                if (score > max)
                    max = score;
            });
            graph.GetNodes(node => {
                float maxHue = 250f / 360f;
                float frac = (float)(planner.aveObsCount[node.position] - min) / (max - min);
                float hue = (1f - frac) * maxHue;
                Color c = Color.HSVToRGB(hue, 1f, 1f);
                Vector3 markerPos = (Vector3)node.position;
                markerPos.y += markerHeight;
                nmg.CreateMarker(markerPos, c, scale);
            });
        }

        if (GUILayout.Button("Visualize Edge Cost"))
        {
            NavGraph graph = AstarPath.active.graphs[0];
            uint min_cost = uint.MaxValue;
            uint max_cost = uint.MinValue;
            graph.GetNodes(node =>
            {
                PointNode pnodeA = (PointNode)node;
                foreach (Connection conn in pnodeA.connections)
                {
                    if (conn.cost > max_cost) max_cost = conn.cost;
                    if (conn.cost < min_cost) min_cost = conn.cost;
                }
            });

            graph.GetNodes(node =>
            {
                PointNode pnodeA = (PointNode)node;
                foreach (Connection conn in pnodeA.connections)
                {
                    PointNode pnodeB = (PointNode)conn.node;
                    Vector3 from = (Vector3)pnodeA.position;
                    float frac = (float)(conn.cost - min_cost) / (max_cost - min_cost);
                    Color color = new Color(frac, 1 - frac, 0, 1);
                    Vector3 to = (Vector3)pnodeB.position;
                    from.y += 1f;
                    to.y += 1f;
                    Debug.DrawLine(from, to, color, 5, false);
                }
            });
        }

        if(GUILayout.Button("Visualize Assault Positions"))
        {
            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;
           
            
            Color MEcolor = Color.blue;
            Color SE1color = Color.black;
            Color SE2color = Color.grey;
            
            nmg.CreateMarker((Vector3)planner.MEassaltLoc.position, MEcolor, 15f);
            nmg.CreateMarker((Vector3)planner.SE1assaultLoc.position, SE1color, 15f);
            nmg.CreateMarker((Vector3)planner.SE2assaultLoc.position, SE2color, 15f);

        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

    }
  
    Vector3 uniformDiskVariate()
    {
        float rho = Mathf.Sqrt(Random.value);
        float theta = 2f * Mathf.PI * Random.value;
        return new Vector3(rho * Mathf.Cos(theta), 0f, rho * Mathf.Sin(theta));
    }

    Vector3 CMOfTag(string tag)
    {
        GameObject[] objs = GameObject.FindGameObjectsWithTag(tag);
        Vector3 accum = Vector3.zero;
        foreach (GameObject go in objs)
            accum += go.transform.position;
        return accum / objs.Length;
    }
    //Added List as a argument of the method to allow for multiple sectors throughout program
    void VisualizeCost(Dictionary<Int3, float> cost, List<GraphNode> sector)
    {
        AnalyticPlanner planner = (AnalyticPlanner)target;
        GameObject pv = (GameObject)planner.pointVisualizer;
        NodeMarkerGenerator nmg = pv.GetComponent<NodeMarkerGenerator>();
        nmg.ClearMarkers();
        NavGraph graph = AstarPath.active.graphs[0];
        TerrainGridGraph gridGraph = (TerrainGridGraph)graph;
        float spacing = gridGraph.node_size;
        float scale = spacing / 2f;
        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (GraphNode node in sector)
        {
            float score = cost[node.position];
            if (score < min)
                min = score;
            if (score > max)
                max = score;
        }
        foreach (GraphNode node in sector)
        { 
            float maxHue = 250f / 360f;
            float frac = (float)(cost[node.position] - min) / (max - min);
            float hue = (1f - frac) * maxHue;

            Color c = Color.HSVToRGB(hue, 1f, 1f);
            Vector3 markerPos = (Vector3)node.position;
            markerPos.y += markerHeight;
            nmg.CreateMarker(markerPos, c, scale);
        }
    }
}