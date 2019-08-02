
using UnityEngine;
using System.Collections.Generic;
using Pathfinding;

[ExecuteInEditMode]

public class AnalyticPlanner : MonoBehaviour
{
    public GameObject pointVisualizer;
    public GameObject lineVisualizer;
    public GameObject assaultPosMarker;
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
    public Dictionary<Vector3, float> nodePentalty = null;
    public GraphNode MEassaltLoc; //Allows storage of ME Assault Pos Node
    public GraphNode SE1assaultLoc; //Allows storage of SE1 Assault Pos Node
    public GraphNode SE2assaultLoc; //Allows storage of SE2 Assault Pos Node
    public Dictionary<Int3, float> MEpositionCost = null;  // Based on observer count in reference to ME
    public Dictionary<Int3, float> MEpathCost = null;  // Cost to move to assault point in reference to ME
    public Dictionary<Int3, float> MEassaultDistCost = null;  // Distance from assault point to target in reference to ME
    public List<GraphNode> MEsectorNodes = null; // Nodes in consideration for ME sector choices
    public Dictionary<Int3, float> SE1positionCost = null;  // Based on observer count in reference to SE1
    public Dictionary<Int3, float> SE1pathCost = null;  // Cost to move to assault point in reference to SE1
    public Dictionary<Int3, float> SE1assaultDistCost = null;  // Distance from assault point to target in reference to SE1
    public List<GraphNode> SE1sectorNodes = null; // Nodes in consideration for SE1 sector choices

}
