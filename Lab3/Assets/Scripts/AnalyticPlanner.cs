
using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

[ExecuteInEditMode]

public class AnalyticPlanner : MonoBehaviour
{
    public GameObject pointVisualizer;
    public GameObject lineVisualizer;
    public float moveObserverPenalty = 500;
    public float unitWidth = 42f;
    
    public float minAssaultDist = 100f;
    public float maxAssaultDist = 800f;
    public float sectorWidthDegrees = 10f;
    public string MEsectorAxis = "center";
    public int seed = 1111;
    

    
    public float observationWeight = 1f;
    public float pathWeight = 1f;
    public float assaultDistWeight = 1f;
    

    public Dictionary<Int3, float> observerCount = null;
    public Dictionary<Int3, float> aveObsCount = null;
    public Dictionary<Int3, float> positionCost = null;  // Based on observer count
    public Dictionary<Int3, float> pathCost = null;  // Cost to move to assault point
    public Dictionary<Int3, float> assaultDistCost = null;  // Distance from assault point to target
    public List<GraphNode> sectorNodes = null;

}
