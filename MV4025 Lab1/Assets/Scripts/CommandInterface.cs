using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
            Ray ray = camera.ScreenPointToRay(Input.mousePosition);

            Debug.Log("casting ray");

            if (Physics.Raycast(ray, out hit))
            {
                GameObject objectHit = hit.transform.gameObject;
                Debug.Log("ray hit object with tag: "+objectHit.tag);
                if (objectHit.CompareTag("BlueForce"))
                {
                    selected = objectHit;
                    Debug.Log("selected.name = " + selected.name);
                    cursor.transform.SetParent(selected.transform, false);
                }
                else if (objectHit.name == "Terrain") 
                {
                    if (selected)
                        selected.GetComponent<Entity>().SetGoal(hit.point);
                        selected.GetComponent<Entity>().createPath(hit.point);
                }
            }

        }
    }
}
