using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Setup : MonoBehaviour
{
    public string defaultPrefab;
    public List<NamedObject<GameObject>> robotPrefabs;

    void Start()
    {
        // Check if Default Prefab is valid
        List<string> robotPrefabStrings = robotPrefabs.Select(prefab => prefab.name).ToList();
        if (!robotPrefabStrings.Contains(defaultPrefab)) {
            Debug.LogError("Default Prefab does not exist in Robot Prefabs");
            return;
        }
        
        // Check if Robot Prefab argument is valid
        string robotName = LoadScene.GetArg("robot", defaultPrefab);
        if (!robotPrefabStrings.Contains(robotName)) {
            Debug.LogError("Robot argument does not exist in Robot Prefabs");
            return;
        }

        // Load Robot Prefab
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
