using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Entity : MonoBehaviour
{

    const float closeEnough = 1e-3f;
    Vector3 goal;
    GameObject target = null;
    const float speed = 1f;
    const float pKill = 0.01f;
    float timeToNextShot = float.PositiveInfinity;
    string targetTag = null;
    const float range = 5f;
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

        goal = transform.position;

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

    void Move()
    {
        LookAtYOnly(goal);
        float maxTravelDist = Time.fixedDeltaTime * speed;
        transform.position = Vector3.MoveTowards(transform.position, goal, maxTravelDist);
        GroundClamp();
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
            bool possibleTarget = false;

            // We can always shoot back at someone shooting at us regardless of range or obstacles
            if (targ.GetComponent<Entity>().GetTarget() == gameObject)
                possibleTarget = true;

            Vector3 toPosition = targ.transform.position;
            toPosition.y += height;
            Vector3 direction = toPosition - fromPosition;
            float distToTarget = direction.magnitude;

            if (distToTarget < minTargetDistance && Physics.Raycast(fromPosition, direction, out hit, distToTarget))
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
                Object.Destroy(target);
            }
        }
    }

    void FixedUpdate()
    {
        if (!target)
            Search();
        if (!target)
            Move();
        else
            Shoot();
    }

    public void SetGoal(Vector3 g)
    {
        goal = g;
    }

    public GameObject GetTarget()
    {
        return target;
    }

    void OnDestroy()
    {
        GameObject cursor = transform.Find("cursor").gameObject;
        if (cursor)
        {
            cursor.transform.parent = null;
            cursor.transform.position = new Vector3(0, 3, 0);
        }
    }
}
