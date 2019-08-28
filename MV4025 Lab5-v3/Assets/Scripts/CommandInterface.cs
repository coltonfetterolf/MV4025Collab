using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class CommandInterface : MonoBehaviour
{

    public GameObject cursor;
    GameObject selected = null;

    // Use this for initialization
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Camera camera = Camera.main;
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);

            Debug.Log("casting ray");

            LayerMask mask = LayerMask.GetMask("BlueLayer","Ground");

            if (Physics.Raycast(ray, out hit, 500f, mask))
            {
                GameObject objectHit = hit.transform.gameObject;
                Debug.Log("ray hit object with tag: " + objectHit.tag);
                if (objectHit.CompareTag("BlueForce"))
                {
                    selected = objectHit;
                    Debug.Log("selected.name = " + selected.name);
                    Vector3 pos = selected.transform.position;
                    pos.y += 4f;
                    cursor.transform.SetParent(selected.transform, false);
                    cursor.transform.position = pos;
                }
                else if (objectHit.name == "Height")
                {
                    if (selected)
                    {
                        Seeker seeker = selected.GetComponent<Seeker>();
                        if (seeker)
                        {
                            seeker.StartPath(selected.transform.position, hit.point, OnPathComplete);
                        }
                        else
                        {
                            selected.GetComponent<Entity>().ClearPath();
                            selected.GetComponent<Entity>().AddToPath(hit.point);
                        }
                    }

                }
            }

        }
    }


    public void OnPathComplete(Path p)
    {
        //Debug.Log("Yay, we got a path back. Did it have an error? " + p.error);
        if (!p.error)
        {
            selected.GetComponent<Entity>().ClearPath();
            for (int i = 0; i < p.vectorPath.Count; i++)
            {
                selected.GetComponent<Entity>().AddToPath((Vector3)p.vectorPath[i]);
            }
            /*
            // Use this version if you want to end on a node of the waypoint graph
            for (int i = 0; i < p.path.Count; i++)
            {
                GraphNode gn = p.path[i];
                selected.GetComponent<Entity>().AddToPath((Vector3)gn.position);
            }
            */
        }
    }
}
