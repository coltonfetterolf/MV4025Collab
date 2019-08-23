using UnityEngine;

public class CallbackRelay : MonoBehaviour
{
    void Start()
    {
        ExperimentControl.Start();
    }

    void Update()
    {
        ExperimentControl.Update();
    }

    void FixedUpdate()
    {
        ExperimentControl.FixedUpdate();
    }
}
