using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class Entity : MonoBehaviour
{

    public GameObject myLeader = null;
    List<GameObject> followers = new List<GameObject>();
    bool checkedIn = false;
    public Vector3 offset;

    const float closeEnough = 0.1f;
    List<Vector3> path = new List<Vector3>();
    GameObject target = null;
    const float speed = 100f;
    const float pKill = 0.01f;
    float timeToNextShot = float.PositiveInfinity;
    string targetTag = null;
    //const float range = 500f;
    const float range = 2f; // using spears
    const float height = 1.75f;
    const float eyeDrop = 0.12f;
    const float eyeHeight = height - eyeDrop;
    const float minShotInterval = 0.2f;
    const float maxShotInterval = 0.3f;
    Color shotColor;


    // Use this for initialization
    void Start()
    {
        if (gameObject.tag == "BlueForce")
        {
            targetTag = "RedForce";
            shotColor = Color.blue;
        }
        else
        {
            targetTag = "BlueForce";
            shotColor = Color.red;
        }

        GroundClamp();
    }

    // Update is called once per frame
    void Update()
    {
        if (target)
        {
            Vector3 fromPosition = transform.position;
            fromPosition.y += eyeHeight;
            Vector3 toPosition = target.transform.position;
            toPosition.y += height;

            Debug.DrawLine(fromPosition, toPosition, shotColor);
        }
    }

    void GroundClamp()
    {
        Vector3 pos = transform.position;
        pos.y = Terrain.activeTerrain.SampleHeight(transform.position);
        transform.position = pos;
    }

    void LookAtYOnly(Vector3 target)
    {
        transform.LookAt(target);
        transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
    }

    public void OnPathComplete(Path p)
    {
        Debug.Log("Yay, we got a path back. Did it have an error? " + p.error);
    }

    float DistanceXZ(Vector3 a, Vector3 b)
    {
        return Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));
    }

    void Move()
    {
        bool imaLeader = (myLeader == null);

        if (imaLeader && path.Count == 0)
            return;

        float effectiveSpeed = speed;
        Vector3 goal;
        if (imaLeader)
        {
            goal = path[0];
            effectiveSpeed = .67f * speed;
            if (DistanceXZ(transform.position, goal) < closeEnough)
            {
                path.RemoveAt(0);
                if (path.Count == 0)
                    return;
                goal = path[0];
            }
        }
        else
        {
            goal = myLeader.transform.TransformVector(offset) + myLeader.transform.position;
        }
            
        LookAtYOnly(goal);
        float maxTravelDist = Time.fixedDeltaTime * effectiveSpeed;
        transform.position = Vector3.MoveTowards(transform.position, goal, maxTravelDist);
        GroundClamp();

    }

    public virtual float GetRange() { return range; }

    void Search()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag(targetTag);
        Vector3 fromPosition = transform.position;
        fromPosition.y += eyeHeight;
        float minTargetDistance = float.PositiveInfinity;
        RaycastHit hit;
        foreach (var targ in targets)
        {
            bool possibleTarget = false;

            Vector3 toPosition = targ.transform.position;
            toPosition.y += height;
            Vector3 direction = toPosition - fromPosition;
            float distToTarget = direction.magnitude;

            // We can always shoot back at someone shooting at us regardless of obstacles or precise range
            if (targ.GetComponent<Entity>().GetTarget() == gameObject && distToTarget < 1.01*GetRange() )
            {
                Debug.Log("Entity " + gameObject.name + " being shot at by " + targ.name);
                possibleTarget = true;
            }

            if (distToTarget < GetRange() && Physics.Raycast(fromPosition, direction, out hit, distToTarget))
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
            Debug.Log(gameObject.name + " shooting " + target.name);
            if (Random.Range(0f, 1f) <= pKill)
            {
                Debug.Log("hit");
                //target.SetActive(false);
                target.GetComponent<Entity>().Die();
                target = null;
            }
        }
    }

    void CheckInSubordinate(GameObject subord)
    {
        followers.Add(subord);
    }

    void CheckIn()
    {
        bool imaLeader = (myLeader == null);
        if (!imaLeader)
        {
            myLeader.GetComponent<Entity>().CheckInSubordinate(gameObject);
        }
        checkedIn = true;
    }

    void Die()
    {
        bool imaLeader = (myLeader == null);
        gameObject.SetActive(false);
        if (imaLeader)
        {
            if (followers.Count > 0)
                followers[0].GetComponent<Entity>().Promote(followers);
        }
    }

    void Promote(List<GameObject> _followers)
    {
        _followers.Remove(gameObject); // Remove self from follower list
        path = myLeader.GetComponent<Entity>().path;
        myLeader = null;
        followers = _followers;
        foreach (GameObject follower in followers)
            follower.GetComponent<Entity>().myLeader = gameObject;
    }

    void FixedUpdate()
    {
        if (!checkedIn)
            CheckIn();
        if (target && !target.activeSelf)
            target = null;
        if (!target)
            Search();
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

    void OnDestroy()
    {
        /*
        GameObject cursor = transform.Find("cursor").gameObject;
        if (cursor)
        {
            cursor.transform.parent = null;
            cursor.transform.position = new Vector3(0, 3, 0);
        }
        */
    }
}
