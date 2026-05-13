using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class CameraPose
{
    public Vector3 position;
    public Vector3 rotation;
}

[System.Serializable]
public class CameraPreset
{
    public string presetName;

    public CameraPose cam0;
    public CameraPose cam1;
    public CameraPose cam2;
    public CameraPose cam3;
}

public class Setup : MonoBehaviour
{
    public List<NamedObject<GameObject>> robotPrefabs;
    public Camera cam0, cam1, cam2, cam3;

    public List<CameraPreset> cameraPresets;

    public enum CameraPresetSelection
    {
        Mast,
        Panoramic,
        Vertical,
        Floating
    }

    [Header("Camera Preset")]
    public CameraPresetSelection selectedPreset;

    void AddSuVISCamera(Camera cam, Transform parent, Vector3 pos, Vector3 rot)
    {
        cam.transform.SetParent(parent, worldPositionStays: false);
        cam.transform.localPosition = pos;
        cam.transform.localEulerAngles = rot;
    }

    void Start()
    {
        string robotName = LoadScene.GetArg("robot", "default");
        GameObject prefab = robotPrefabs.First(prefab => prefab.name == robotName).obj;
        
        GameObject startPositionObject = GameObject.Find("robot_start_position");
        GameObject rover = Instantiate(prefab, startPositionObject.transform);
        
        Transform chassisTransform = rover.transform.Find("base_link/chassis");
        CinemachineCamera cameraFollower = GetComponentInChildren<CinemachineCamera>();
        cameraFollower.Follow = chassisTransform;

        CameraPreset preset = cameraPresets[(int)selectedPreset];
        Debug.Log($"Selected Camera Preset: {preset.presetName}");

        // Anchor SuVIS camera
        AddSuVISCamera(cam0, chassisTransform, preset.cam0.position, preset.cam0.rotation);
        AddSuVISCamera(cam1, chassisTransform, preset.cam1.position, preset.cam1.rotation);
        AddSuVISCamera(cam2, chassisTransform, preset.cam2.position, preset.cam2.rotation);
        AddSuVISCamera(cam3, chassisTransform, preset.cam3.position, preset.cam3.rotation);
    }

    void Update()
    {

    }
}
