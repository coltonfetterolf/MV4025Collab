using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrthoCameraControl : MonoBehaviour
{
    public float base_translation_speed;
    public float zoom_speed;
   Camera ortho_camera;

    // Start is called before the first frame update
    void Start()
    {
        ortho_camera = gameObject.GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {

        float translation_speed = base_translation_speed * ortho_camera.orthographicSize;

        if (Input.GetKey(KeyCode.W))
        {
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x, pos.y, pos.z + translation_speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.A))
        {
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x - translation_speed * Time.deltaTime, pos.y, pos.z);
        }
        if (Input.GetKey(KeyCode.S))
        {
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x, pos.y, pos.z - translation_speed * Time.deltaTime);
        }
        if (Input.GetKey(KeyCode.D))
        {
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x + translation_speed * Time.deltaTime, pos.y, pos.z);
        }

        float wheel = Input.GetAxis("Mouse ScrollWheel");
        ortho_camera.orthographicSize += zoom_speed * wheel;
    }
}
