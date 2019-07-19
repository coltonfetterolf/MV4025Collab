using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NodeMarkerGenerator : MonoBehaviour {

    public GameObject markerTemplate;

    public void ClearMarkers()
    {
        for (int i = gameObject.transform.childCount - 1; i >= 0; i--) 
        {
            GameObject.DestroyImmediate(gameObject.transform.GetChild(i).gameObject);
        }
    }

    public void CreateMarker(Vector3 pos, Color col, float scale)
    {
        GameObject marker = Object.Instantiate(markerTemplate, gameObject.transform);
        marker.transform.position = pos;
        marker.transform.localScale = scale * Vector3.one;

        Renderer rend = marker.GetComponent<Renderer>();

        Material tempMaterial = new Material(rend.sharedMaterial);

        //Set the main Color of the Material to green
        tempMaterial.shader = Shader.Find("_Color");
        tempMaterial.SetColor("_Color", col);

        //Find the Specular shader and change its Color to red
        tempMaterial.shader = Shader.Find("Specular");
        tempMaterial.SetColor("_SpecColor", col);

        rend.sharedMaterial = tempMaterial;

    }

    public void CreateRandomMarker()
    {
        GameObject marker = Object.Instantiate(markerTemplate);
        float x = Random.Range(-10f, 10f);
        float z = Random.Range(-10f, 10f);
        marker.transform.position = new Vector3(x, 0, z);
        Renderer rend = marker.GetComponent<Renderer>();

        float r = Random.Range(0f, 1f);
        float g = Random.Range(0f, 1f);
        float b = Random.Range(0f, 1f);

        Debug.Log(r + " " + g + " " + b);

        Color col = new Color(r, g, b);

        //Set the main Color of the Material to green
        rend.material.shader = Shader.Find("_Color");
        rend.material.SetColor("_Color", col);

        //Find the Specular shader and change its Color to red
        rend.material.shader = Shader.Find("Specular");
        rend.material.SetColor("_SpecColor", col);
    }

	// Use this for initialization
	void Start () {
    }
	
	// Update is called once per frame
	void Update () {
        //CreateRandomMarker();
	}
}
