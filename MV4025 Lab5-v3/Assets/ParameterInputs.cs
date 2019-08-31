using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ParameterInputs : MonoBehaviour
{

    
    private float trainingDuration = 2400f;
    public static float testDuration = 60f;
    public static float rewardTimeout = 10f;
    public static bool respawnOnDeath = true;
    public static float respawnWidth = 80f;
    public static bool loadBrain = false;
    public static bool rangedState = true;
    public static float learningRate = 0.1f;
    public static int numHiddenUnits = 0;
    public static string typeHiddenUnits = "tanh";
    public static float discountFactor = 0.9f;
    public static float lossFactor = 0.6f;
    public static int seed = System.Environment.TickCount;

    

}
