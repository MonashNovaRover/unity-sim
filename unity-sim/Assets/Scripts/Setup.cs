using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Setup : MonoBehaviour
{
    public List<NamedObject<GameObject>> robotPrefabs;
    public Camera cam0, cam1, cam2, cam3;
    public Vector3 cam0Pos = new Vector3(0.2f, 0.2f, 0.2f), cam1Pos = new Vector3(0.2f, 0.2f, 0.2f), cam2Pos = new Vector3(0.2f, 0.2f, 0.2f), cam3Pos = new Vector3(0.2f, 0.2f, 0.2f);
    public Vector3 cam0Rot = new Vector3(0f, 0f, 0f), cam1Rot = new Vector3(0f, 0f, 0f), cam2Rot = new Vector3(0f, 0f, 0f), cam3Rot = new Vector3(0f, 0f, 0f);

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
        rover.tag = "Rover";
        
        Transform chassisTransform = rover.transform.Find("base_link/chassis");
        CinemachineCamera cameraFollower = GetComponentInChildren<CinemachineCamera>();
        cameraFollower.Follow = chassisTransform;

        // Anchor SuVIS camera
        AddSuVISCamera(cam0, chassisTransform, cam0Pos, cam0Rot);
        AddSuVISCamera(cam1, chassisTransform, cam1Pos, cam1Rot);
        AddSuVISCamera(cam2, chassisTransform, cam2Pos, cam2Rot);
        AddSuVISCamera(cam3, chassisTransform, cam3Pos, cam3Rot);
    }

    void Update()
    {

    }
}
