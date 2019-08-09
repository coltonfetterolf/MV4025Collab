using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class CommandInterface : MonoBehaviour {

    public GameObject cursor;
    GameObject selected = null;

    public GameObject orthographicMovementModel;
    public GameObject RTSMovementModel;
    public GameObject FPSMovementModel;


    // Use this for initialization
    void Start () {
        orthographicMovementModel.SetActive(false);
        RTSMovementModel.SetActive(true);
        FPSMovementModel.SetActive(false);
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
                    //Vector3 pos = selected.transform.position;
                    //pos.y += 3f;
                    //cursor.transform.position = pos;
                    cursor.transform.SetParent(selected.transform, false);
                }
                else if (objectHit.gameObject.layer == LayerMask.NameToLayer("Ground")) 
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        orthographicMovementModel.SetActive(false);
                        RTSMovementModel.SetActive(false);
                        FPSMovementModel.SetActive(true);
                        FPSMovementModel.transform.position = hit.point;
                    }
                    else
                    {
                        Debug.Log("Attempting pathfinding");
                        if (selected)
                        {
                            Seeker seeker = selected.GetComponent<Seeker>();
                            seeker.StartPath(selected.transform.position, hit.point, OnPathComplete);
                        }
                    }  
                }
            }
            Debug.Log("exiting Command Interface Update");

        }
        if (Input.GetKeyDown(KeyCode.Minus))
        {
            orthographicMovementModel.SetActive(true);
            RTSMovementModel.SetActive(false);
            FPSMovementModel.SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.LeftBracket))
        {
            orthographicMovementModel.SetActive(false);
            RTSMovementModel.SetActive(true);
            FPSMovementModel.SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.Quote))
        {
            orthographicMovementModel.SetActive(false);
            RTSMovementModel.SetActive(false);
            FPSMovementModel.SetActive(true);

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
