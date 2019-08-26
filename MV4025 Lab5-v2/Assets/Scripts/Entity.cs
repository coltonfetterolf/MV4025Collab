using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

using ConvNetSharp.Core;
using UnityEngine.SceneManagement;

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

public class Entity : MonoBehaviour
{
    public RewardFunctionID rewardFnID = RewardFunctionID.RewardKillsMinusLosses;

    const float closeEnough = 0.1f;
    List<Vector3> path = new List<Vector3>();
    GameObject target = null;
    const float speed = 10f;
    const float pKill = 0.1f;
    float timeToNextShot = float.PositiveInfinity;
    string targetTag = null;
    const float range = 10f;
    const float height = 0.0175f; // Units of 100m
    const float eyeDrop = 0.0012f;
    const float eyeHeight = height - eyeDrop;
    const float minShotInterval = 0.2f;
    const float maxShotInterval = 0.3f;
    const float radius = 4f;

    public Transform fireVizPrefab;
    GameObject fireViz;
    bool dead = false;
    float AIUpdatePeriod = 0.1f;

    // AI
    public bool AIControl = false;
    public int nSectors = 8;
    public int nRings = 3;
    float lastRingStartRange;
    float ringWidth;
    float senseRadius;
    Vector3[] sectorCenters;
    Vector3[,] sectorCenterPoints;
    float nextAIUpdate = 0f;
    float reward = 0f;
    float epsilon = 1f; // initial value
    float epsilon_decrement; // per unit time
    float epsilon_min = 0.05f;
    Net<double> net;

    public float kill_factor = 1f;
    public float loss_factor;
    int kills_this_update;
    int losses_this_update;

    Vector3 bumpUp = new Vector3(0, .1f, 0);

    public bool respawnOnDeath = true;
    float respawnAfter;  // Delay before respawning
    float respawnWidth;

    delegate AIState StateCreationFunction();
    StateCreationFunction CreateState;

    public enum RewardFunctionID { RewardDistToTarget, RewardCloserToTarget, RewardKills, RewardKillsMinusLosses };

    class AIState : Brain.IAIState
    {
        public int nSectors;
        public int nRings;
        public float[,] state_friendly, state_hostile;
        public AIState(int nSectors, int nRings=1)
        {
            this.nSectors = nSectors;
            this.nRings = nRings;
            state_friendly = new float[nSectors, nRings];
            state_hostile = new float[nSectors, nRings];
        }
        public double[] toDoubleArray()
        {
            double[] result = new double[2*nSectors*nRings];
            int cnt = 0;
            for (int j = 0; j < nRings; j++)
                for (int i = 0; i < nSectors; i++)
                    result[cnt++] = state_friendly[i, j];
            for (int j = 0; j < nRings; j++)
                for (int i = 0; i < nSectors; i++)
                    result[cnt++] = state_hostile[i, j];
            return result;
        }
        override public string ToString()
        {
            string result = "state ";
            result += " friend ";
            for (int j = 0; j < nRings; j++)
            {
                result += "ring " + j + " : ";
                for (int i = 0; i < nSectors; i++)
                    result += state_friendly[i, j] + " ";
            }
            result += " foe ";
            for (int j = 0; j < nRings; j++)
            {
                result += "ring " + j + " : ";
                for (int i = 0; i < nSectors; i++)
                    result += state_hostile[i, j] + " ";
            }
            return result;
        }
    }

    AIState current_state, last_state;
    int last_action;
    float last_min_target_dist = float.MaxValue;
    delegate float reward_fn();
    reward_fn rewardFn;

    Brain brain = null;

    // Use this for initialization
    void Start()
    {
        Util.CLog("Forcing ExperimentControl's constructor to run");
        ExperimentControl.ForceConstructorToRun();

        if (gameObject.tag == "BlueForce")
            targetTag = "RedForce";
        else
            targetTag = "BlueForce";

        fireViz = Instantiate(fireVizPrefab).gameObject;
        fireViz.SetActive(false);

        // AI
        // Sectors


        float sectorWidth = 2 * Mathf.PI / nSectors;
        lastRingStartRange = (nRings - 1) * range;
        if (nRings > 1)
            ringWidth = lastRingStartRange / (nRings - 1);
        else
            ringWidth = float.MaxValue;
        senseRadius = ringWidth;  // This is a guess

        sectorCenters = new Vector3[nSectors];
        float centerAngle = 0f;
        for (int i = 0; i < nSectors; ++i)
        {
            sectorCenters[i].z = Mathf.Cos(centerAngle);
            sectorCenters[i].x = Mathf.Sin(centerAngle);
            centerAngle += sectorWidth;
            //Debug.Log(i+" "+ sectorCenters[i].x+" "+ sectorCenters[i].z);
        }

        sectorCenterPoints = new Vector3[nSectors, nRings];
        for (int j = 0; j < nRings; ++j)
        {
            for (int i = 0; i < nSectors; ++i)
            {
                float theta = i * sectorWidth;
                float rho = (j + 0.5f) * ringWidth;
                sectorCenterPoints[i, j].x = rho * Mathf.Cos(theta);
                sectorCenterPoints[i, j].y = 0;
                sectorCenterPoints[i, j].z = rho * Mathf.Sin(theta);
            }
        }


        if (rewardFnID == RewardFunctionID.RewardDistToTarget)
            rewardFn = RewardDistToTarget;
        else if (rewardFnID == RewardFunctionID.RewardCloserToTarget)
            rewardFn = RewardCloserToTarget;
        else if (rewardFnID == RewardFunctionID.RewardKills)
            rewardFn = RewardKills;
        else if (rewardFnID == RewardFunctionID.RewardKillsMinusLosses)
            rewardFn = RewardKillsMinusLosses;

        epsilon_decrement = 1f / ExperimentControl.Parameters.train_duration;

        respawnWidth = ExperimentControl.Parameters.respawnWidth;

        bool load_brain = ExperimentControl.Parameters.load_brain;

        bool ranged_state = ExperimentControl.Parameters.ranged_state;
        
        if (ranged_state)
            CreateState = CreateRangedState;
        else
            CreateState = CreateNonrangedState;

        loss_factor = ExperimentControl.Parameters.loss_factor;



        // Get from BrainRegistry, if present
        brain = (Brain)BrainRegistry.Get("my brain");

        // If not, load brain from file if present
        if (brain == null && load_brain)
        {
            if (System.IO.File.Exists("brain.bin"))
            {
                Stream stream = new FileStream("brain.bin", FileMode.Open, FileAccess.Read);
                IFormatter formatter = new BinaryFormatter();
                brain = (Brain)formatter.Deserialize(stream);
                BrainRegistry.Add("my brain", brain);
                stream.Close();
                return;
            }
        }

        // If not found, create new brain
        if (brain == null) {
            brain = new Brain(
                erb_size: 500, epsilon_init: epsilon, epsilon_decrement: epsilon_decrement, epsilon_min: epsilon_min,
                n_states: 2 * nSectors * nRings, n_actions: nSectors + 1, batch_size: 8, gamma: 0.9f
                );
            BrainRegistry.Add("my brain", brain);
        }

        brain.OnStart();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (ExperimentControl.run_complete)
            return;
        fireViz.SetActive(false);
        if (target && target.GetComponent<Entity>().IsDead())
            target = null;

        if (target)
        {
            Vector3 fromPosition = transform.position;
            fromPosition.y += eyeHeight;
            Vector3 toPosition = target.transform.position;
            toPosition.y += height;

            fireViz.SetActive(true);
            Vector3[] positions = new Vector3[2];
            positions[0] = fromPosition + bumpUp;
            positions[1] = toPosition + bumpUp;
            LineRenderer lr = fireViz.GetComponent<LineRenderer>();
            //lr.widthMultiplier = 5f;
            lr.positionCount = 2;
            lr.SetPositions(positions);
        }

        AlsoUpdate();
    }

    /*
    void GroundClamp()
    {
        Vector3 pos = transform.position;
        pos.y = Terrain.activeTerrain.SampleHeight(transform.position);
        transform.position = pos;
    }
    */

    void LookAtYOnly(Vector3 target)
    {
        transform.LookAt(target);
        transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
    }

    public void OnPathComplete(Pathfinding.Path p)
    {
        Debug.Log("Yay, we got a path back. Did it have an error? " + p.error);
    }

    float DistanceXZ(Vector3 a, Vector3 b)
    {
        return Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
    }

    bool IsClear(Vector3 position)
    {
        GameObject[] nearby = GameObject.FindGameObjectsWithTag(gameObject.tag);
        foreach (GameObject friendly in nearby)
        {
            if (friendly == gameObject) continue;
            float dist = Vector3.Distance(friendly.transform.position, position);
            if (dist < radius)
                return false;
        }
        return true;
    }

    float RewardDistToTarget()
    {
        GameObject[] nearby = GameObject.FindGameObjectsWithTag(targetTag);
        float max_val = 1.0f / range;
        float reward = 0.0f;
        foreach (GameObject target in nearby)
        {
            float dist = Vector3.Distance(target.transform.position, gameObject.transform.position);
            if (1.0 / dist > reward)
                reward = Mathf.Min(1.0f / dist, max_val);
        }
        return reward;
    }

    float RewardCloserToTarget()
    {
        GameObject[] nearby = GameObject.FindGameObjectsWithTag(targetTag);
        float min_dist = float.MaxValue;
        foreach (GameObject target in nearby)
        {
            float dist = Vector3.Distance(target.transform.position, gameObject.transform.position);
            if (dist < min_dist)
                min_dist = dist;
        }
        float reward = 0f;
        if (min_dist < last_min_target_dist)
            reward = 1f;
        last_min_target_dist = min_dist;
        return reward;
    }

    float RewardKills()
    {
        float reward = (float)kills_this_update;
        return reward;
    }

    float RewardKillsMinusLosses()
    {
        float reward = kill_factor * kills_this_update - loss_factor * losses_this_update;
        return reward;
    }

    void Move()
    {
        if (path.Count == 0)
            return;
        Vector3 goal = path[0];

        if (DistanceXZ(transform.position, goal) < closeEnough)
        {
            path.RemoveAt(0);
            if (path.Count == 0)
                return;
            goal = path[0];
        }
        LookAtYOnly(goal);

        // Compute speed
        Vector3 direction = (goal - transform.position).normalized;
        float h1 = Terrain.activeTerrain.SampleHeight(transform.position);
        float h2 = Terrain.activeTerrain.SampleHeight(transform.position + direction);
        float grade_modifier = SpeedOnGrade(h2 - h1);
        
        float maxTravelDist = Time.fixedDeltaTime * grade_modifier * speed;
        Vector3 new_position = Vector3.MoveTowards(transform.position, goal, maxTravelDist);
        if (IsClear(new_position) )
            transform.position = new_position;
    }

    float DistToClosestTarget()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        GameObject nearest_target = null;
        float nearest_target_dist = float.PositiveInfinity;
        foreach (var targ in targets)
        {
            if (targ.GetComponent<Entity>().IsDead())
                continue;

            float distToTarget = (targ.transform.position - transform.position).magnitude;

            if (distToTarget < nearest_target_dist)
            {
                nearest_target = targ;
                nearest_target_dist = distToTarget;
            }
        }
        return nearest_target_dist;
    }

    void Search()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        Vector3 fromPosition = transform.position;
        fromPosition.y += eyeHeight;
        float minTargetDistance = float.PositiveInfinity;
        RaycastHit hit;
        foreach (var targ in targets)
        {
            if (targ.GetComponent<Entity>().IsDead())
                continue;

            bool possibleTarget = false;

            // We can always shoot back at someone shooting at us regardless of range or obstacles
            if (targ.GetComponent<Entity>().GetTarget() == gameObject)
                possibleTarget = true;

            Vector3 toPosition = targ.transform.position;
            toPosition.y += height;
            Vector3 direction = toPosition - fromPosition;
            float distToTarget = direction.magnitude;

            if (distToTarget < range && Physics.Raycast(fromPosition, direction, out hit, distToTarget+1f))
            {
                if (hit.transform.gameObject == targ)
                {
                    possibleTarget = true;
                }
            }

            if (possibleTarget)
            {
                if (distToTarget < minTargetDistance)
                {
                    minTargetDistance = distToTarget;
                    target = targ;
                }
            }
        }
        if (target)
        {
            LookAtYOnly(target.transform.position);
            timeToNextShot = Random.Range(minShotInterval, maxShotInterval);
        }
    }

    void Shoot()
    {
        timeToNextShot -= Time.fixedDeltaTime;
        if (timeToNextShot <= 0)
        {
            timeToNextShot += Random.Range(minShotInterval, maxShotInterval);
            Util.CLog(gameObject.name + " shooting " + target.name);
            if (Random.Range(0f, 1f) <= pKill)
            {
                ++kills_this_update;
                Util.CLog(gameObject.name + " hit " + target.name);
                target.GetComponent<Entity>().Die();
                target = null;
            }
        }
    }

    void Die()
    {
        dead = true;
        fireViz.SetActive(false);
        respawnAfter = 1f;
        target = null;
        ++losses_this_update;

        // Store experience including death penalty
        current_state = CreateState();
        reward = rewardFn();
        Brain.Experience exp = new Brain.Experience(last_state, last_action, reward, current_state);
        brain.AddExperience(exp);
        }

    bool IsDead() { return dead; }

    void AlsoUpdate()
    {
        if (dead)
        {
            respawnAfter -= Time.fixedDeltaTime;
            if (respawnOnDeath && respawnAfter < 0f)
            {
                dead = false;
                do
                {
                    transform.position = new Vector3(Random.Range(-respawnWidth, respawnWidth), 0, Random.Range(-respawnWidth, respawnWidth));
                }
                while (DistToClosestTarget() <= range || !IsClear(transform.position) );
            }
            else
            {
                // "Respawn" to an invisible location under the map
                transform.position = new Vector3(0, -10f, 0);
            }
            return;
        }

        // Moving and shooting mechanics, Part I
        if (!target)
            Search();


        // AI
        nextAIUpdate -= Time.fixedDeltaTime;
        if (!target && AIControl && nextAIUpdate <= 0f) // Only act when not shooting. Action choice rate controlled by update timer.
        {
            nextAIUpdate = AIUpdatePeriod;
            current_state = CreateState();
            reward = rewardFn();
            // Store experience, if we have one
            if (last_state != null)
            {
                Brain.Experience exp = new Brain.Experience(last_state, last_action, reward, current_state);
                //if (reward>0 && last_state.toDoubleArray()[last_action] ==0.0) Debug.Log(exp.ToString());
                brain.AddExperience(exp);
                brain.Learn();
            }
            int action = brain.SelectAction(current_state);
            TakeAction(action);
            last_action = action;
            last_state = current_state;
        }
        kills_this_update = 0;
        losses_this_update = 0;


        // Moving and shooting mechanics, Part II
        if (!target)
            Move();
        else
            Shoot();
    }

    public void ClearPath()
    {
        path.Clear();
    }

    public void AddToPath(Vector3 g)
    {
        path.Add(g);
    }

    public GameObject GetTarget()
    {
        return target;
    }

    void FillNonrangedState(string forceTag, float[,] state)
    {
        GameObject[] units = GameObject.FindGameObjectsWithTag(forceTag);
        foreach (var unit in units)
        {
            if (unit.GetComponent<Entity>().IsDead() || unit == gameObject)
            {
                continue;
            }

            for (int i = 0; i < nSectors; i++)
            {
                Vector3 center = transform.TransformDirection(sectorCenters[i]);
                float dot = Vector3.Dot(center, (unit.transform.position - transform.position).normalized);
                if (dot > 0)
                    state[i,0] += dot;
            }
        }
    }

    AIState CreateNonrangedState()
    {
        AIState ai_state = new AIState(nSectors);

        FillNonrangedState(tag, ai_state.state_friendly);
        FillNonrangedState(targetTag, ai_state.state_hostile);

        var debug_str = ai_state.ToString();

        //Debug.Log(debug_str);
        return ai_state;
    }

    float SenseKernel(float dist)
    {
        if (dist > senseRadius)
            return 0;
        return 1f - dist / senseRadius;
    }

    
    void FillRangedState(string forceTag, float[,] state)
    {
        GameObject[] units = GameObject.FindGameObjectsWithTag(forceTag);
        float[,] stateDelta = new float[nSectors, nRings];
        float totalDelta;
        for (int j = 0; j < nRings; j++)
            for (int i = 0; i < nSectors; i++)
                state[i, j] = 0;
        foreach (var unit in units)
        {
            if (unit.GetComponent<Entity>().IsDead() || unit == gameObject)
                continue;

            totalDelta = 0;
            for (int j = 0; j < nRings; j++)
                for (int i = 0; i < nSectors; i++)
                    stateDelta[i, j] = 0;
            
            for (int j = 0; j < nRings; j++)
                for (int i = 0; i < nSectors; i++)
                {
                    float dist = (unit.transform.position - transform.TransformPoint(sectorCenterPoints[i, j])).magnitude;
                    float delta = SenseKernel(dist);
                    stateDelta[i, j] = delta;
                    totalDelta += delta;
                }

            // Unit is far from all sensing cell centers. Add to outermost ring of containing sector.
            if (totalDelta == 0)
            {
                int closestSector = 0;
                float minDist = float.MaxValue;
                for (int i = 0; i < nSectors; i++)
                {
                    float dd = (unit.transform.position - transform.TransformPoint(sectorCenterPoints[i, nRings - 1])).magnitude;
                    if (dd < minDist)
                    {
                        minDist = dd;
                        closestSector = i;
                    }
                }
                state[closestSector, nRings - 1] += 1f;
                continue;
            }

            for (int j = 0; j < nRings; j++)
                for (int i = 0; i < nSectors; i++)
                    stateDelta[i, j] /= totalDelta;

            for (int j = 0; j < nRings; j++)
                for (int i = 0; i < nSectors; i++)
                    state[i, j] += stateDelta[i, j];
        }
    }

    AIState CreateRangedState()
    {
        AIState ai_state = new AIState(nSectors, nRings);

        FillRangedState(tag, ai_state.state_friendly);
        FillRangedState(targetTag, ai_state.state_hostile);

        var debug_str = ai_state.ToString();

        //Debug.Log(debug_str);
        return ai_state;
    }

    void TakeAction(int action)
    {
        float lngth = 5f;
        Vector3 direction;
        if (action < nSectors)
            direction = sectorCenters[action];
        else
            direction = Vector3.zero; // Wait
        Vector3 goal = transform.position + transform.TransformDirection(lngth*direction);
        path.Clear();
        path.Add( goal );
    }

    public void OnApplicationQuit()
    {
        IFormatter formatter = new BinaryFormatter();
        Stream stream = new FileStream("brain-"+ExperimentControl.Parameters.seed+".bin", FileMode.Create, FileAccess.Write);

        formatter.Serialize(stream, brain);
        stream.Close();
    }

    float SpeedOnGrade(float grade)
    {
        float max_speed = 1f;
        float speed;
        if (ExperimentControl.Parameters.mobility_model == ExperimentControl.MobilityModel.NoGradePenalty)
            speed = max_speed;
        else if (ExperimentControl.Parameters.mobility_model == ExperimentControl.MobilityModel.SlightGradePenalty)
        {
            float scale = 0.1f;
            speed = max_speed / (1f + scale * Mathf.Abs(grade));
        }
        else
            throw new System.Exception("Unknown mobility model");
        return speed;
    }
}

