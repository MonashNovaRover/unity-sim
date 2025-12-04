using System.Collections;
using UnityEngine;
using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosUtils;

public class RosMultiCameraManager : MonoBehaviour
{
    public bool autoStart = true;
    [SerializeField] 
	private RosCamera[] cameras;

    void Start()
    {
        cameras = GetComponentsInChildren<RosCamera>();

        if (cameras.Length == 0)
        {
            Debug.LogWarning("No CameraObject components found.");
            this.enabled = false;
            return;
        }

        if (autoStart)
            StartCoroutine(PublishLoop());
    }

    IEnumerator PublishLoop()
    {
        while (true)
        {
            foreach (var cam in cameras)
            {
                //cam.CaptureAndSendFrame();
            }

            yield return null; // Change to fps
        }
    }
}