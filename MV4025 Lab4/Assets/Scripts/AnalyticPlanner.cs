
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

    public float minDefenseRange = 75f;
    public float maxDefenseRange = 300f;
    

    
    public float observationWeight = 1f;
    public float pathWeight = 1f;
    public float assaultDistWeight = 1f;
    public float supportingFireWeight = 1f;


    public Dictionary<Int3, float> observerCount = null;
    public Dictionary<Int3, float> aveObsCount = null;
    public Dictionary<Int3, float> positionCost = null;  // Based on observer count
    public Dictionary<Int3, float> pathCost = null;  // Cost to move to assault point
    public Dictionary<Int3, float> assaultDistCost = null;  // Distance from assault point to target
    public Dictionary<Int3, float> supportingFireCost = null;  // Encourages better supporting fire angles
    public List<GraphNode> sectorNodes = null;

    public Dictionary<Int3, float> numPossibleFires = null;
    public float aveFiresPerDefenseNode;

    public Int3 meAssaultPos, se1AssaultPos, se2AssaultPos;
    //public Vector3[] mePath, se1Path, se2Path;

    public bool meAssaultPosSet = false;
    public bool se1AssaultPosSet = false;
    public bool se2AssaultPosSet = false;

}
