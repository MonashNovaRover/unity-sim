using RosMessageTypes.Geometry;
using RosMessageTypes.Nav;
using RosMessageTypes.Std;
using RosMessageTypes.Tf2;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;
using UnityEngine;

public class GroundTruthPublisher : MonoBehaviour
{
    [Header("Frames")]
    public string mapFrameId = "map";
    public string odomFrameId = "odom";
    public string baseFrameId = "base_link";

    [Header("Publishing")]
    public float publishRateHz = 30f;

    public string tfTopic = "/tf";

    private bool initialised;
    private ROSConnection ros;
    private Transform chassis;
    private Transform mapOrigin;
    // Fixed pose of base_link expressed in the chassis frame (from the URDF base_link_to_chassis joint)
    private Vector3 baseInChassisPosition;
    private Quaternion baseInChassisRotation;
    private float timeSinceLastPublish;

    private void OnEnable()
    {
        Setup.RobotSpawned += Initialise;

        // The robot was spawned before this component was enabled. Physics may already have moved the chassis,
        // in which case the base_link -> chassis offset recorded now will be wrong.
        if (!initialised && Setup.Robot != null)
        {
            Debug.LogWarning($"{nameof(GroundTruthPublisher)}: robot was spawned before this was enabled; " +
                             "ground truth may be offset if the rover had already moved.");
            Initialise(Setup.Robot);
        }
    }

    private void OnDisable()
    {
        Setup.RobotSpawned -= Initialise;
    }

    // Called by Setup as soon as the robot spawns, before physics has run, so the chassis is still in its
    // URDF pose relative to base_link.
    private void Initialise(GameObject robot)
    {
        mapOrigin = GameObject.Find("robot_start_position")?.transform;

        if (mapOrigin == null)
        {
            Debug.LogError($"{nameof(GroundTruthPublisher)}: could not find robot_start_position. Disabling.");
            enabled = false;
            return;
        }

        // base_link is a plain Transform that never moves; the ArticulationBody root is its child "chassis".
        // Track the chassis and apply the fixed base_link -> chassis offset to get where base_link really is.
        Transform baseLink = FindChildRecursive(robot.transform, baseFrameId);
        chassis = baseLink != null ? FindChildRecursive(baseLink, "chassis") : null;

        if (chassis == null)
        {
            Debug.LogError($"{nameof(GroundTruthPublisher)}: could not find '{baseFrameId}/chassis' in {robot.name}. Disabling.");
            enabled = false;
            return;
        }

        Quaternion inverseChassisRotation = Quaternion.Inverse(chassis.rotation);
        baseInChassisPosition = inverseChassisRotation * (baseLink.position - chassis.position);
        baseInChassisRotation = inverseChassisRotation * baseLink.rotation;

        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<TFMessageMsg>(tfTopic);

        Debug.Log($"Ground truth {mapFrameId} -> {odomFrameId} -> {baseFrameId} publishing on {tfTopic}. " +
                  $"base_link in chassis: position {baseInChassisPosition:F4}, rotation {baseInChassisRotation.eulerAngles:F2}");
        initialised = true;
    }

    private void Update()
    {
        if (!initialised)
            return;

        timeSinceLastPublish += Time.deltaTime;
        if (publishRateHz > 0f && timeSinceLastPublish < 1f / publishRateHz)
            return;
        timeSinceLastPublish = 0f;

        Publish();
    }

    private void Publish()
    {
        // Pose of base_link relative to the map origin, in Unity (RUF) coordinates.
        Vector3 basePosition = chassis.position + chassis.rotation * baseInChassisPosition;
        Quaternion baseRotation = chassis.rotation * baseInChassisRotation;

        Quaternion inverseOriginRotation = Quaternion.Inverse(mapOrigin.rotation);
        Vector3 position = inverseOriginRotation * (basePosition - mapOrigin.position);
        Quaternion rotation = inverseOriginRotation * baseRotation;

        // Publish map->odom (identity transform)
        var tf = new TransformStampedMsg
        {
            header = new HeaderMsg { stamp = TimePublisher.GetCurrentTime(), frame_id = mapFrameId },
            child_frame_id = odomFrameId,
            transform = new TransformMsg { rotation = new QuaternionMsg(0, 0, 0, 1) }
        };
        ros.Publish(tfTopic, new TFMessageMsg(new[] { tf }));

        // Publish odom->base_link
        tf = new TransformStampedMsg
        {
            header = new HeaderMsg { stamp = TimePublisher.GetCurrentTime(), frame_id = odomFrameId },
            child_frame_id = baseFrameId,
            transform = new TransformMsg
            {
                translation = position.To<FLU>(),
                rotation = rotation.To<FLU>()
            }
        };
        ros.Publish(tfTopic, new TFMessageMsg(new[] { tf }));
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
                return found;
        }
        return null;
    }
}
