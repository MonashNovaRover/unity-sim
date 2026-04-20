using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Setup : MonoBehaviour
{
    public List<NamedObject<GameObject>> robotPrefabs;
    void Start()
    {
        string robotName = LoadScene.GetArg("robot", "default");
        GameObject prefab = robotPrefabs.First(prefab => prefab.name == robotName).obj;
        
        GameObject startPositionObject = GameObject.Find("robot_start_position");
        GameObject rover = Instantiate(prefab, startPositionObject.transform);
        
        Transform chassisTransform = rover.transform.Find("base_link/chassis");
        CinemachineCamera cameraFollower = GetComponentInChildren<CinemachineCamera>();
        cameraFollower.Follow = chassisTransform;
    }

    void Update()
    {

    }
}
