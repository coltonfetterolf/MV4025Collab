
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

    bool LOSExists(Vector3 from, Vector3 to, float eyeHeight)
    {
        Vector3 fromPos = new Vector3(from.x, 0, from.z);
        fromPos.y = Terrain.activeTerrain.SampleHeight(fromPos) + eyeHeight;
        Vector3 toPos = new Vector3(to.x, 0, to.z);
        toPos.y = Terrain.activeTerrain.SampleHeight(toPos) + eyeHeight;
        Vector3 diff = toPos - fromPos;
        return !Physics.Raycast(fromPos, diff, diff.magnitude);
    }

    bool firePossible(Vector3 shooter, Vector3 target, GameObject[] friendlies)
    {
        float dangerAngle = (15f) * (Mathf.PI / 180f);
        float dangerProduct = Mathf.Cos(dangerAngle);
        Vector3 shooterXZ = new Vector3(shooter.x, 0, shooter.z);
        Vector3 targetXZ = new Vector3(target.x, 0, target.z);
        Vector3 fire = (targetXZ - shooterXZ).normalized;
        foreach (GameObject friendly in friendlies)
        {
            Vector3 friendlyPosXZ = new Vector3(friendly.transform.position.x,0, friendly.transform.position.z);
            Vector3 toFriendly = (friendlyPosXZ - shooterXZ).normalized;
            if (Vector3.Dot(fire, toFriendly) > dangerProduct)
                return false;
        }
        return true;
    }

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
                //observer.GetComponent<Entity>().observableWaypoint[pos] = true;
            }
        }

        return obCount;
    }

    float SpeedOnGrade(float grade)
    {
        float scale = 100000f;
        float max_speed = 1f;
        return max_speed / (1f + scale * Mathf.Abs(grade));
    }

    // Compute visibility between each waypoint and each point on a specified grid
    Dictionary<Vector3,Dictionary<Int3,bool>> GridVisibility(Vector3 center, float spacing, int n_per_side)
    {
        AstarPath.active.data.Awake();
        AstarPath.active.Scan();
        NavGraph graph = AstarPath.active.graphs[0];

        float eyeHeight = 1.62f;

        float width = spacing * (n_per_side - 1);
        Vector3 ul = new Vector3(center.x - width / 2, 0, center.z - width / 2);
        Dictionary<Vector3, Dictionary<Int3, bool>> result = new Dictionary<Vector3, Dictionary<Int3, bool>>();
        for (int j=0;j<n_per_side; j++)
        {
            for (int i=0;i<n_per_side;i++)
            {
                Dictionary<Int3, bool> vis = new Dictionary<Int3, bool>();
                Vector3 pos = new Vector3(ul.x + i * spacing, 0, ul.z + j * spacing);
                result[pos] = vis;
                graph.GetNodes(node =>
                {
                    vis[node.position] = LOSExists(pos, (Vector3)node.position, eyeHeight);
                });

            }
        }
        return result;
    }


    public override void OnInspectorGUI()
    {
        AnalyticPlanner planner = (AnalyticPlanner)target;
        planner.pointVisualizer = (GameObject)EditorGUILayout.ObjectField("pointVisualizer",planner.pointVisualizer, typeof(Object), true);
        planner.lineVisualizer = (GameObject)EditorGUILayout.ObjectField("lineVisualizer", planner.lineVisualizer, typeof(Object), true);
        planner.moveObserverPenalty = EditorGUILayout.FloatField("moveObserverPenalty", planner.moveObserverPenalty);
        
        planner.unitWidth = EditorGUILayout.FloatField("unitWidth", planner.unitWidth);
        planner.minAssaultDist = EditorGUILayout.FloatField("minAssaultDist", planner.minAssaultDist);
        planner.maxAssaultDist = EditorGUILayout.FloatField("maxAssaultDist", planner.maxAssaultDist);
        planner.sectorWidthDegrees = EditorGUILayout.FloatField("sectorWidthDegrees", planner.sectorWidthDegrees);

        EditorGUILayout.LabelField("Planning Factor Weights");
        planner.observationWeight = EditorGUILayout.FloatField("observationWeight", planner.observationWeight);
        planner.pathWeight = EditorGUILayout.FloatField("pathWeight", planner.pathWeight);
        planner.assaultDistWeight = EditorGUILayout.FloatField("assaultDistWeight", planner.assaultDistWeight);
        planner.supportingFireWeight = EditorGUILayout.FloatField("supportingFIreWeight", planner.supportingFireWeight);

        planner.minDefenseRange = EditorGUILayout.FloatField("minDefenseRange", planner.minDefenseRange);
        planner.maxDefenseRange = EditorGUILayout.FloatField("maxDefenseRange", planner.maxDefenseRange);

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
        if (GUILayout.Button("Comp Fires"))
        {
            // Initialize
            AstarPath.active.data.Awake();
            NavGraph graph = AstarPath.active.graphs[0];

            List<Int3> defenseNodes = new List<Int3>();

            Vector3 rawRedCM = CMOfTag("RedForce");
            Vector3 redCM = rawRedCM;
            redCM.y = 0f;

            graph.GetNodes(node =>
            {
                Vector3 pos = (Vector3)node.position;
                pos.y = 0;
                float dist = (redCM - pos).magnitude;
                if (dist <= planner.maxDefenseRange && dist >= planner.minDefenseRange)
                    defenseNodes.Add(node.position);
            });

            Debug.Log("Defense node count: " + defenseNodes.Count);

            GameObject[] observers = GameObject.FindGameObjectsWithTag("RedForce");

            // Maps observer ID and node to true/false (can/can't see)
            Dictionary<int, Dictionary<Int3, bool>> targetability = new Dictionary<int, Dictionary<Int3, bool>>();
            foreach (GameObject obs in observers)
            {
                int id = obs.GetInstanceID();
                Dictionary<Int3, bool> obsTargetability = new Dictionary<Int3, bool>();
                targetability[obs.GetInstanceID()] = obsTargetability;
                foreach (Int3 nodePos in defenseNodes)
                {
                    bool hasLOS = LOSExists(obs.transform.position, (Vector3)nodePos, eyeHeight);
                    bool fireIsSafe = firePossible(obs.transform.position, (Vector3)nodePos, observers);
                    obsTargetability[nodePos] = (hasLOS && fireIsSafe);
                }
            }

            float aveFires = 0f;
            planner.numPossibleFires = new Dictionary<Int3, float>();
            foreach (Int3 nodePos in defenseNodes)
            {
                planner.numPossibleFires[nodePos] = 0;
                foreach (GameObject obs in observers)
                {
                    if (targetability[obs.GetInstanceID()][nodePos])
                    {
                        float value = 0;
                        if (planner.numPossibleFires.ContainsKey(nodePos))
                            value = planner.numPossibleFires[nodePos];
                        planner.numPossibleFires[nodePos] = value + 1f;
                    }
                }
                aveFires += planner.numPossibleFires[nodePos];
            }
            aveFires /= defenseNodes.Count;
            planner.aveFiresPerDefenseNode = aveFires;

        }

        if (GUILayout.Button("Viz Fires")) { VisualizeCost2(planner.numPossibleFires); }

        planner.aveFiresPerDefenseNode = EditorGUILayout.FloatField("aveFiresPerDefenseNode", planner.aveFiresPerDefenseNode);
     
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Optimize Defensive Position"))
        {
            GameObject[] defenders = GameObject.FindGameObjectsWithTag("RedForce");
            float currAveFires = planner.aveFiresPerDefenseNode;
            float smRad = planner.minDefenseRange;
            float lrRad = planner.maxDefenseRange / 2;
            Vector3 defCM = CMOfTag("RedForce");
            while(currAveFires >= planner.aveFiresPerDefenseNode)
            {

            }

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

            planner.meAssaultPosSet = false;
            planner.se1AssaultPosSet = false;
            planner.se2AssaultPosSet = false;

            planner.positionCost = new Dictionary<Int3, float>();
            planner.pathCost = new Dictionary<Int3, float>();
            planner.assaultDistCost = new Dictionary<Int3, float>();
            planner.supportingFireCost = new Dictionary<Int3, float>();
            planner.sectorNodes = new List<GraphNode>();

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
                        planner.sectorNodes.Add(node);
                }
            });

            // Compute path length
            foreach (GraphNode node in planner.sectorNodes)
            {
                ABPath path = ABPath.Construct(rawBlueCM, (Vector3)node.position);
                AstarPath.StartPath(path);

                float cost = 0f;
                Vector3 lastPos = rawBlueCM;
                foreach (GraphNode pathNode in path.path)
                {
                    cost += (lastPos - (Vector3)pathNode.position).magnitude;
                    cost += planner.moveObserverPenalty * pathNode.Penalty;
                    lastPos = (Vector3)pathNode.position;
                }
                planner.pathCost[node.position] = cost;
                planner.assaultDistCost[node.position] = (rawRedCM - (Vector3)node.position).magnitude;
                planner.positionCost[node.position] = planner.pathWeight * cost + planner.observationWeight * planner.observerCount[node.position] + planner.assaultDistWeight * planner.assaultDistCost[node.position];
            }
        }

        if (GUILayout.Button("ME Observation Cost")) { VisualizeCost(planner.aveObsCount);  }

        if (GUILayout.Button("ME Path Cost")) { VisualizeCost(planner.pathCost); }

        if (GUILayout.Button("ME Assault Distance Cost")) { VisualizeCost(planner.assaultDistCost);  }

        if (GUILayout.Button("ME Assault Position"))
        {
            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;

            foreach (GraphNode node in planner.sectorNodes)
            {
                planner.positionCost[node.position] = planner.pathWeight * planner.pathCost[node.position]
                    + planner.observationWeight * planner.observerCount[node.position] 
                    + planner.assaultDistWeight * planner.assaultDistCost[node.position];
            }


            // find min
            Int3 minPos = Int3.zero;
            float min = float.MaxValue;
            foreach (GraphNode node in planner.sectorNodes)
            {
                if (planner.positionCost[node.position] < min)
                {
                    min = planner.positionCost[node.position];
                    minPos = node.position;
                }
            }

            planner.meAssaultPos = minPos;
            planner.meAssaultPosSet = true;

            VisualizeCost(planner.positionCost);

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


        if (GUILayout.Button("SE1 Analysis"))
        {

            planner.positionCost = new Dictionary<Int3, float>();
            planner.pathCost = new Dictionary<Int3, float>();
            planner.assaultDistCost = new Dictionary<Int3, float>();
            planner.sectorNodes = new List<GraphNode>();

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

            float acceptanceAngle = (180f / 2f) * (Mathf.PI / 180f); // 180 degrees
            float acceptanceProduct = Mathf.Cos(acceptanceAngle);

            graph.GetNodes(node =>
            {
                Vector3 mePos = (Vector3)planner.meAssaultPos;
                mePos.y = 0f;
                Vector3 nodePos = (Vector3)node.position;
                nodePos.y = 0f;
                float nodeDist = (nodePos - redCM).magnitude;
                if (nodeDist >= planner.minAssaultDist && nodeDist <= planner.maxAssaultDist)
                {
                    Vector3 redToNode = (nodePos - redCM).normalized;
                    if (Vector3.Dot(-mainAxisDir, redToNode) >= acceptanceProduct)
                    {
                        Vector3 meFires = (redCM - mePos).normalized;
                        Vector3 me2se1 = (nodePos - mePos).normalized;
                        Vector3 se1Fires = (redCM - nodePos).normalized;
                        float dangerAngle = (15f) * (Mathf.PI / 180f);
                        float dangerProduct = Mathf.Cos(dangerAngle);
                        if (Vector3.Dot(meFires, me2se1) < dangerProduct && Vector3.Dot(se1Fires, -me2se1) < dangerProduct)
                        {
                            if ((mePos-nodePos).magnitude > 1.5 * planner.unitWidth)
                                planner.sectorNodes.Add(node);
                        }
                    }
                }
            });

            // Compute path length
            foreach (GraphNode node in planner.sectorNodes)
            {
                ABPath path = ABPath.Construct(rawBlueCM, (Vector3)node.position);
                AstarPath.StartPath(path);

                float cost = 0f;
                Vector3 lastPos = rawBlueCM;
                foreach (GraphNode pathNode in path.path)
                {
                    cost += (lastPos - (Vector3)pathNode.position).magnitude;
                    cost += planner.moveObserverPenalty * pathNode.Penalty;
                    lastPos = (Vector3)pathNode.position;
                }
                planner.pathCost[node.position] = cost;
                planner.assaultDistCost[node.position] = (rawRedCM - (Vector3)node.position).magnitude;

                Vector3 mePos = (Vector3)planner.meAssaultPos;
                mePos.y = 0f;
                Vector3 nodePos = (Vector3)node.position;
                nodePos.y = 0f;
                Vector3 meFires = (redCM - mePos).normalized;
                Vector3 se1Fires = (redCM - nodePos).normalized;
                float supportingFireCost = Vector3.Dot(meFires, se1Fires);
                if (supportingFireCost<0)
                    supportingFireCost = -2*Vector3.Dot(meFires, se1Fires);
                planner.supportingFireCost[node.position] = supportingFireCost;
            }
        }

        if (GUILayout.Button("SE1 Observation Cost")) { VisualizeCost(planner.aveObsCount); }

        if (GUILayout.Button("SE1 Path Cost")) { VisualizeCost(planner.pathCost); }

        if (GUILayout.Button("SE1 Assault Distance Cost")) { VisualizeCost(planner.assaultDistCost); }

        if (GUILayout.Button("SE1 Supporting Fire Cost")) { VisualizeCost(planner.supportingFireCost); }

        if (GUILayout.Button("SE1 Assault Position"))
        {
            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;

            foreach (GraphNode node in planner.sectorNodes)
            {
                planner.positionCost[node.position] = planner.pathWeight * planner.pathCost[node.position]
                    + planner.observationWeight * planner.observerCount[node.position]
                    + planner.assaultDistWeight * planner.assaultDistCost[node.position]
                    + planner.supportingFireWeight * planner.supportingFireCost[node.position];
            }


            // find min
            Int3 minPos = Int3.zero;
            float min = float.MaxValue;
            foreach (GraphNode node in planner.sectorNodes)
            {
                if (planner.positionCost[node.position] < min)
                {
                    min = planner.positionCost[node.position];
                    minPos = node.position;
                }
            }

            // Except for this line, same code as a block above
            planner.se1AssaultPos = minPos;
            planner.se1AssaultPosSet = true;

            VisualizeCost(planner.positionCost);

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
            NavGraph graph = AstarPath.active.graphs[0];
            List<GraphNode> remove = new List<GraphNode>();
            foreach ( GraphNode node in planner.sectorNodes)
            {
                Vector3 mePos = (Vector3)planner.meAssaultPos;
                mePos.y = 0f;
                Vector3 se1Pos = (Vector3)planner.se1AssaultPos;
                se1Pos.y = 0f;
                Vector3 nodePos = (Vector3)node.position;
                nodePos.y = 0f;

                Vector3 redCM = CMOfTag("RedForce");
                redCM.y = 0f;
                
                Vector3 se1Fires = (redCM - se1Pos).normalized;
                Vector3 se2tose1 = (se1Pos - nodePos).normalized;
                Vector3 se2Fires = (redCM - nodePos).normalized;
                float distToSE1 = (nodePos - se1Pos).magnitude;
                float dangerAngle = (15f) * (Mathf.PI / 180f);
                float dangerProduct = Mathf.Cos(dangerAngle);
                if (distToSE1 < 1.5 * planner.unitWidth || Vector3.Dot(se1Fires, -se2tose1) >= dangerProduct || Vector3.Dot(se2Fires, se2tose1) >= dangerProduct)
                {
                    remove.Add(node);
                }

            }

            foreach (GraphNode node in remove)
            {
                planner.sectorNodes.Remove(node);
            }
        }

        if (GUILayout.Button("SE2 Observation Cost")) { VisualizeCost(planner.aveObsCount); }

        if (GUILayout.Button("SE2 Path Cost")) { VisualizeCost(planner.pathCost); }

        if (GUILayout.Button("SE2 Assault Distance Cost")) { VisualizeCost(planner.assaultDistCost); }

        if (GUILayout.Button("SE1 Supporting Fire Cost")) { VisualizeCost(planner.supportingFireCost); }

        if (GUILayout.Button("SE2 Assault Position"))
        {
            nmg.ClearMarkers();
            LineRenderer lr = planner.lineVisualizer.GetComponent<LineRenderer>();
            lr.positionCount = 0;

            foreach (GraphNode node in planner.sectorNodes)
            {
                planner.positionCost[node.position] = planner.pathWeight * planner.pathCost[node.position]
                    + planner.observationWeight * planner.observerCount[node.position]
                    + planner.assaultDistWeight * planner.assaultDistCost[node.position]
                    + planner.supportingFireWeight * planner.supportingFireCost[node.position];
            }


            // find min
            Int3 minPos = Int3.zero;
            float min = float.MaxValue;
            foreach (GraphNode node in planner.sectorNodes)
            {
                if (planner.positionCost[node.position] < min)
                {
                    min = planner.positionCost[node.position];
                    minPos = node.position;
                }
            }

            // Except for this line, same code as a block above
            planner.se2AssaultPos = minPos;
            planner.se2AssaultPosSet = true;

            VisualizeCost(planner.positionCost);

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

    void VisualizeCost(Dictionary<Int3, float> cost)
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
        foreach (GraphNode node in planner.sectorNodes)
        {
            float score = cost[node.position];
            if (score < min)
                min = score;
            if (score > max)
                max = score;
        }
        foreach (GraphNode node in planner.sectorNodes)
        { 
            float maxHue = 250f / 360f;
            float frac = (float)(cost[node.position] - min) / (max - min);
            float hue = (1f - frac) * maxHue;

            Color c = Color.HSVToRGB(hue, 1f, 1f);
            Vector3 markerPos = (Vector3)node.position;
            markerPos.y += markerHeight;
            nmg.CreateMarker(markerPos, c, scale);
        }

        if (planner.meAssaultPosSet)
        {
            Vector3 markerPos = (Vector3)planner.meAssaultPos;
            markerPos.y += 1.5f * markerHeight;
            nmg.CreateMarker(markerPos, Color.white, 1.5f*scale);
        }

        if (planner.se1AssaultPosSet)
        {
            Vector3 markerPos = (Vector3)planner.se1AssaultPos;
            markerPos.y += 1.5f * markerHeight;
            nmg.CreateMarker(markerPos, Color.white, 1.5f * scale);
        }

        if (planner.se2AssaultPosSet)
        {
            Vector3 markerPos = (Vector3)planner.se2AssaultPos;
            markerPos.y += 1.5f * markerHeight;
            nmg.CreateMarker(markerPos, Color.white, 1.5f * scale);
        }
    }

    void VisualizeCost2(Dictionary<Int3, float> cost)
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
        foreach (Int3 nodePos in cost.Keys)
        {
            float score = cost[nodePos];
            if (score < min)
                min = score;
            if (score > max)
                max = score;
        }
        foreach (Int3 nodePos in cost.Keys)
        {
            float maxHue = 250f / 360f;
            float frac = (float)(cost[nodePos] - min) / (max - min);
            float hue = (1f - frac) * maxHue;

            Color c = Color.HSVToRGB(hue, 1f, 1f);
            Vector3 markerPos = (Vector3)nodePos;
            markerPos.y += markerHeight;
            nmg.CreateMarker(markerPos, c, scale);
        }
    }
}