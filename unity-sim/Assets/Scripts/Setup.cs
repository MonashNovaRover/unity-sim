using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnitySensors.Sensor.GNSS;

public class Setup : MonoBehaviour
{
    public enum RobotSource
    {
        Prefab, // Spawns the robot prefab unmodified
        RobotDescription // Constructs the robot from the URDF in robot_state_publisher's robot_description parameter
    } 

    public string defaultPrefab;
    public List<NamedObject<GameObject>> robotPrefabs;

    [Header("Robot source (override with robot_source=prefab or robot_source=robot_description)")]
    public RobotSource defaultRobotSource = RobotSource.Prefab;
    public string robotDescriptionNode = "/robot_state_publisher";

    // The spawned robot, or null if it has not yet been spawned
    public static GameObject Robot { get; private set; }

    // Invoked as soon as the robot is spawned, before physics has simulated it, so its links are still in the poses defined by the prefab/URDF
    public static event System.Action<GameObject> RobotSpawned;

    private IEnumerator Start()
    {
        Robot = null;

        // Check if Default Prefab is valid
        List<string> robotPrefabStrings = robotPrefabs.Select(prefab => prefab.name).ToList();
        if (!robotPrefabStrings.Contains(defaultPrefab)) {
            Debug.LogError("Default Prefab does not exist in Robot Prefabs");
            yield break;
        }

        // Check if Robot Prefab argument is valid
        string robotName = Init.GetArg("robot", defaultPrefab);
        if (!robotPrefabStrings.Contains(robotName)) {
            Debug.LogError("Robot argument does not exist in Robot Prefabs");
            yield break;
        }

        GameObject prefab = robotPrefabs.First(prefab => prefab.name == robotName).obj;

        GameObject startPositionObject = GameObject.Find("robot_start_position");
        GameObject rover = null;

        if (GetRobotSource() == RobotSource.RobotDescription)
        {
            string node = Init.GetArg("robot_description_node", robotDescriptionNode);

            string urdf = null;
            yield return RobotDescriptionLoader.FetchRobotDescription(node, result => urdf = result);

            if (urdf != null)
                rover = RobotDescriptionLoader.Build(urdf, prefab);

            if (rover == null)
            {
                Debug.LogError("Couldn't load the robot description, so no robot was spawned");
                yield break;
            }

            rover.transform.SetParent(startPositionObject.transform, false);
            rover.SetActive(true);
            Debug.Log("Spawned robot from " + node + "'s robot_description, using prefab '" + robotName + "' for sensors and scripts");
        }
        else
        {
            rover = Instantiate(prefab, startPositionObject.transform);
        }

        Transform chassisTransform = rover.transform.Find("base_link/chassis");
        CinemachineCamera cameraFollower = GetComponentInChildren<CinemachineCamera>();
        cameraFollower.Follow = chassisTransform;
        // Move the camera directly behind the rover rather than damping towards it from its initial position.
        // This is necessary when the rover is loaded from the robot description, as the camera has already
        // been active for some time before the target is assigned.
        cameraFollower.PreviousStateIsValid = false;

        // Init GPS
        Transform gnssSensorTransform = rover.transform.Find("base_link/chassis/GNSS_ros");
        GameObject coordinateSystemObject = GameObject.Find("GeoCoordinateSystem");
        if (gnssSensorTransform != null && coordinateSystemObject != null)
        {
            GNSSSensor gnssSensorScript = gnssSensorTransform.GetComponent<GNSSSensor>();
            GeoCoordinateSystem coordinateSystemScript = coordinateSystemObject.GetComponent<GeoCoordinateSystem>();
            FieldInfo field = typeof(GNSSSensor).GetField(
                "_coordinateSystem", BindingFlags.NonPublic | BindingFlags.Instance
            );
            field.SetValue(gnssSensorScript, coordinateSystemScript);
        }

        Robot = rover;
        RobotSpawned?.Invoke(rover);
    }

    private RobotSource GetRobotSource()
    {
        string defaultValue = defaultRobotSource == RobotSource.RobotDescription ? "robot_description" : "prefab";
        string source = Init.GetArg("robot_source", defaultValue);

        if (source == "prefab")
            return RobotSource.Prefab;
        if (source == "robot_description")
            return RobotSource.RobotDescription;

        Debug.LogError("Unknown robot_source '" + source + "' (should be prefab or robot_description), using " + defaultValue);
        return defaultRobotSource;
    }
}
