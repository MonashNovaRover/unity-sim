using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CameraRosConfig
{
    public Camera camera;
    public string topic;
    public string frameId;
    
    // Capture settings are low so the sim doesn't tank
    public int width = 128;
    public int height = 72;
    public float framerate = 5f;
}

public class MultiRosCameraManager : MonoBehaviour
{
    [Header("Camera Configurations")] public List<CameraRosConfig> cameraConfigs;

    void Start()
    {
        int lostConfigs = cameraConfigs.Count;
        foreach (var config in cameraConfigs)
        {
            if (config.camera == null)
            {
                Debug.LogWarning("Camera config missing Camera reference.");
                lostConfigs--;
                continue;
            }

            var rosCam = config.camera.GetComponent<RosCamera>();
            var gpuCap = config.camera.GetComponent<GpuReadbackCameraCapture>();

            if (rosCam == null || gpuCap == null)
            {
                Debug.LogWarning($"Camera '{config.camera.name}' missing required components.");
                lostConfigs--;
                continue;
            }

            typeof(RosCamera)
                .GetField("topic", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(rosCam, config.topic);
            typeof(RosCamera)
                .GetField("frameId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(rosCam, config.frameId);

            gpuCap.width = config.width;
            gpuCap.height = config.height;
            gpuCap.framerate = config.framerate;

            Debug.Log(
                $"Configured camera '{config.camera.name}' for topic '{config.topic}' at {config.width}x{config.height}@{config.framerate}fps.");
        }

        if (lostConfigs == 0)
        {
            Debug.LogError("There are no camera configs setup for this manager");
            this.enabled = false;
        }
    }
}