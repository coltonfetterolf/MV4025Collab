using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class CommandInterface : MonoBehaviour {

    public GameObject cursor;
    GameObject selected = null;

    // Use this for initialization
    void Start () {
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            Camera camera = Camera.main;
            //Debug.Log("camera " + camera + " mouse pos " + Input.mousePosition + " camera==null "+(camera==null));
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);

            Debug.Log("casting ray");

            if (Physics.Raycast(ray, out hit))
            {
                Debug.Log("something was hit");
                GameObject objectHit = hit.transform.gameObject;
                Debug.Log("ray hit object with tag: "+objectHit.tag+" and name is: " + objectHit.name);
                if (objectHit.CompareTag("BlueForce"))
                {
                    selected = objectHit;
                    if (objectHit.GetComponent<Entity>().myLeader)
                        selected = objectHit.GetComponent<Entity>().myLeader;
                    Debug.Log("selected.name = " + selected.name);
                    cursor.transform.SetParent(selected.transform, false);
                }
                else if (objectHit.gameObject.layer == LayerMask.NameToLayer("Ground")) 
                {
                    Debug.Log("Attempting pathfinding");
                    if (selected)
                    {
                        Seeker seeker = selected.GetComponent<Seeker>();
                        seeker.StartPath(selected.transform.position, hit.point, OnPathComplete);
                    }
                }
            }
            Debug.Log("exiting Command Interface Update");

        }
    }


    public void OnPathComplete(Path p)
    {
        Debug.Log("Yay, we got a path back. Did it have an error? " + p.error);
        if (!p.error)
        {
            selected.GetComponent<Entity>().ClearPath();
            for (int i = 0; i < p.path.Count; i++)
            {
                GraphNode gn = p.path[i];
                selected.GetComponent<Entity>().AddToPath((Vector3)gn.position);
            }
        }
    }
}
