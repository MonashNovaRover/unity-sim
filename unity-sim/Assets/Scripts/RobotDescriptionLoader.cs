using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RosMessageTypes.RclInterfaces;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.UrdfImporter;
using UnityEngine;

// Constructs the rover at runtime from the URDF published by robot_state_publisher on /robot_description,
// as an alternative to the URDF that was imported into a robot prefab.
//
// The URDF does not contain the Unity sensors, cameras or rover scripts, so a robot prefab is used as a
// template:
//  1. The prefab is instantiated and the URDF is imported.
//  2. All objects attached to the prefab's links (sensors, cameras, lights, etc.) are moved onto the
//     imported links of the same name.
//  3. The prefab's links are discarded and replaced with the imported links.
// The prefab's root object and its components (RoverPhysics, RoverDrive, etc.) are retained.
public static class RobotDescriptionLoader
{
    // ROS-TCP-Endpoint does not reply when it is unable to call a service (for example, before
    // robot_state_publisher has started). The request is therefore resent at this interval until a
    // reply is received.
    private const float RetryIntervalSeconds = 2f;

    // Interval between log messages stating that the reply is still being waited for
    private const float WaitingMessageIntervalSeconds = 10f;

    // Retrieves the URDF by reading the robot_description parameter of the given node (normally
    // robot_state_publisher) through ROS-TCP-Endpoint. This is the same URDF that the node publishes on
    // /robot_description.
    //
    // The topic itself cannot be used, because it is published only once and ROS-TCP-Endpoint subscribes
    // with a QoS profile that does not receive messages published before the subscription was made.
    //
    // The request is retried until a reply is received, however long that takes.
    // onResult is called with the URDF, or with null if the node has no robot_description parameter.
    public static IEnumerator FetchRobotDescription(string nodeName, Action<string> onResult)
    {
        string serviceName = nodeName + "/get_parameters";

        ROSConnection ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterRosService<GetParametersRequest, GetParametersResponse>(serviceName);
        Debug.Log("Requesting robot_description from " + serviceName);

        GetParametersRequest request = new GetParametersRequest(new string[] { "robot_description" });
        List<Task<GetParametersResponse>> sentRequests = new List<Task<GetParametersResponse>>();
        float startTime = Time.realtimeSinceStartup;
        float nextSendTime = startTime;
        float nextWaitingMessageTime = startTime + WaitingMessageIntervalSeconds;

        while (true)
        {
            // Send the request, or resend it if the retry interval has elapsed
            if (Time.realtimeSinceStartup >= nextSendTime)
            {
                sentRequests.Add(ros.SendServiceMessage<GetParametersResponse>(serviceName, request));
                nextSendTime = Time.realtimeSinceStartup + RetryIntervalSeconds;
            }

            if (Time.realtimeSinceStartup >= nextWaitingMessageTime)
            {
                Debug.LogWarning("Still waiting for a reply from " + serviceName + " ("
                                 + (int)(Time.realtimeSinceStartup - startTime) + "s). "
                                 + "Are ROS-TCP-Endpoint and " + nodeName + " running?");
                nextWaitingMessageTime = Time.realtimeSinceStartup + WaitingMessageIntervalSeconds;
            }

            // Use the first reply received to any of the requests sent so far
            foreach (Task<GetParametersResponse> sent in sentRequests)
            {
                if (!sent.IsCompleted)
                    continue;

                ParameterValueMsg value = sent.Result.values[0];
                if (value.type != ParameterValueMsg.PARAMETER_STRING)
                {
                    Debug.LogError(nodeName + " has no robot_description parameter");
                    onResult(null);
                    yield break;
                }

                Debug.Log("Received robot description (" + value.string_value.Length + " characters) from " + nodeName);
                onResult(value.string_value);
                yield break;
            }

            yield return null;
        }
    }

    // Constructs the robot from the URDF, taking everything the URDF does not define from templatePrefab.
    // The robot is returned inactive and without a parent; the caller is responsible for parenting it and
    // activating it. Returns null if the URDF is incompatible with the prefab. The cause is logged.
    public static GameObject Build(string urdf, GameObject templatePrefab)
    {
        // The URDF importer only reads from files
        string urdfPath = Path.Combine(Application.temporaryCachePath, "robot_description.urdf");
        File.WriteAllText(urdfPath, urdf);

        ImportSettings settings = new ImportSettings();
        settings.convexMethod = ImportSettings.convexDecomposer.unity;
        settings.chosenAxis = templatePrefab.GetComponent<UrdfRobot>().chosenAxis;

        // The robot is assembled under this inactive object so that no components start running before
        // assembly is complete. The object and anything remaining in it are destroyed afterwards.
        GameObject workArea = new GameObject("robot_description work area");
        workArea.SetActive(false);

        GameObject robot = UnityEngine.Object.Instantiate(templatePrefab, workArea.transform);
        robot.name = templatePrefab.name;

        GameObject imported = UrdfRobotExtensions.CreateRuntime(urdfPath, settings);
        imported.transform.SetParent(workArea.transform, false);

        if (!HasAllMovableJoints(robot, imported))
        {
            UnityEngine.Object.Destroy(workArea);
            return null;
        }

        MoveAttachments(robot, imported);
        ReplaceLinks(robot, imported, workArea);

        robot.SetActive(false);
        robot.transform.SetParent(null, false);
        UnityEngine.Object.Destroy(workArea);
        return robot;
    }

    // RoverPhysics, RoverDrive and RoverCANBus look up joints by name and fail if a joint is absent.
    // This verifies that the URDF contains every movable joint present in the prefab.
    private static bool HasAllMovableJoints(GameObject template, GameObject imported)
    {
        HashSet<string> importedJoints = new HashSet<string>();
        foreach (UrdfJoint joint in imported.GetComponentsInChildren<UrdfJoint>(true))
            importedJoints.Add(joint.jointName);

        List<string> missing = new List<string>();
        foreach (UrdfJoint joint in template.GetComponentsInChildren<UrdfJoint>(true))
        {
            if (joint.JointType != UrdfJoint.JointTypes.Fixed && !importedJoints.Contains(joint.jointName))
                missing.Add(joint.jointName);
        }

        if (missing.Count > 0)
        {
            Debug.LogError("Robot description is missing these joints from prefab '" + template.name + "': "
                           + string.Join(", ", missing));
            return false;
        }
        return true;
    }

    // Moves all objects attached to the prefab's links (sensors, cameras, lights, etc.) onto the imported
    // links of the same name. Where the URDF does not contain a given link, the object is attached to the
    // nearest ancestor link that the URDF does contain, preserving its pose relative to that link.
    private static void MoveAttachments(GameObject template, GameObject imported)
    {
        Dictionary<string, Transform> importedLinks = new Dictionary<string, Transform>();
        foreach (UrdfLink link in imported.GetComponentsInChildren<UrdfLink>(true))
            importedLinks[link.name] = link.transform;

        // The attachments are collected before any are moved, as reparenting children during iteration
        // would cause some to be skipped
        List<Transform> attachments = new List<Transform>();
        foreach (UrdfLink link in template.GetComponentsInChildren<UrdfLink>(true))
        {
            foreach (Transform child in link.transform)
            {
                bool madeByUrdfImporter = child.GetComponent<UrdfLink>() != null
                                          || child.GetComponent<UrdfVisuals>() != null
                                          || child.GetComponent<UrdfCollisions>() != null;
                if (!madeByUrdfImporter)
                    attachments.Add(child);
            }
        }

        foreach (Transform attachment in attachments)
        {
            // Find the nearest link, starting with the attachment's own link, that also exists in the URDF
            Transform oldLink = attachment.parent;
            while (oldLink != null && !importedLinks.ContainsKey(oldLink.name))
                oldLink = oldLink.parent;

            if (oldLink == null)
            {
                Debug.LogWarning("Robot description has no link to attach '" + attachment.name + "' to, so it was left out");
                continue;
            }

            if (oldLink != attachment.parent)
            {
                Debug.LogWarning("Robot description has no link '" + attachment.parent.name + "', so '" + attachment.name
                                 + "' was attached to '" + oldLink.name + "' instead (in the same place)");
            }

            // Preserve the attachment's position and rotation relative to the link
            Vector3 localPosition = oldLink.InverseTransformPoint(attachment.position);
            Quaternion localRotation = Quaternion.Inverse(oldLink.rotation) * attachment.rotation;

            attachment.SetParent(importedLinks[oldLink.name], false);
            attachment.localPosition = localPosition;
            attachment.localRotation = localRotation;
        }
    }

    // Replaces the prefab's links with the imported links. The prefab's original links are moved into
    // workArea, which is subsequently destroyed.
    private static void ReplaceLinks(GameObject template, GameObject imported, GameObject workArea)
    {
        List<Transform> oldLinks = new List<Transform>();
        foreach (Transform child in template.transform)
        {
            if (child.GetComponent<UrdfLink>() != null)
                oldLinks.Add(child);
        }

        List<Transform> newLinks = new List<Transform>();
        foreach (Transform child in imported.transform)
        {
            if (child.GetComponent<UrdfLink>() != null)
                newLinks.Add(child);
        }

        foreach (Transform oldLink in oldLinks)
            oldLink.SetParent(workArea.transform, false);

        foreach (Transform newLink in newLinks)
            newLink.SetParent(template.transform, false);
    }
}
