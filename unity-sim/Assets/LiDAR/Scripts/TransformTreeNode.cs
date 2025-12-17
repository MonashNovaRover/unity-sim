using System.Collections.Generic;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using UnityEngine;
using Unity.Robotics.UrdfImporter;
using RosUtils;

/// <summary>
/// Represents a node in the robot's transform tree hierarchy.
/// Each node corresponds to a robot link (from URDF) and tracks its transform state.
/// 
/// Features:
/// - Hierarchical tree structure matching Unity/URDF hierarchy
/// - Transform caching for optimization (avoid republishing unchanged transforms)
/// - Dirty-flagging system to track which transforms need updating
/// - Conversion utilities for Unity ↔ ROS coordinate systems
/// </summary>
public class TransformTreeNode
{
    public readonly GameObject SceneObject;
    public readonly List<TransformTreeNode> Children;
    public UnityEngine.Transform Transform => SceneObject.transform;
    public string name => SceneObject.name;
    public bool IsALeafNode => Children == null || Children.Count == 0;

    // Caching for optimization - avoids rebuilding unchanged transforms
    public TransformStamped CachedStamp;
    public long LastStampTime;
    
    /// <summary>
    /// Dirty flag - true if transform changed and needs republishing.
    /// Set by Unity's Transform.hasChanged or external modifications.
    /// </summary>
    public bool IsDirty;
    public UnityEngine.Vector3 LastPosition;
    public UnityEngine.Quaternion LastRotation;
    public bool PoseInitialized;
    
    /// <summary>
    /// Constructs a transform tree node and recursively builds its children.
    /// Only includes GameObjects with UrdfLink components in the tree.
    /// </summary>
    public TransformTreeNode(GameObject sceneObject)
    {
        SceneObject = sceneObject;
        Children = new List<TransformTreeNode>();
        PopulateChildNodes(this);
    }

    /// <summary>
    /// Converts this node to a ROS TransformStamped message.
    /// Handles coordinate system conversion from Unity (left-handed Y-up) to ROS (right-handed Z-up).
    /// </summary>
    public static TransformStamped ToTransformStamped(TransformTreeNode node, string parentFrameId, double time)
    {
        var ts = new TransformStamped(
            TimeStamp.GetHeader(time, parentFrameId),
            node.name,
            TransformExtensions.ToFLU(node.Transform) // Convert Unity → ROS FLU coordinates
        );

    	return ts;
    }

    /// <summary>
    /// Recursively populates child nodes by traversing the Unity transform hierarchy.
    /// Only GameObjects with a UrdfLink component are included in the tree.
    /// This ensures only robot links (not arbitrary scene objects) are published to ROS TF.
    /// </summary>
    static void PopulateChildNodes(TransformTreeNode tfNode)
    {
        var parentTransform = tfNode.Transform;
        
        // Iterate through all Unity child transforms
        for (var childIndex = 0; childIndex < parentTransform.childCount; ++childIndex)
        {
            var childTransform = parentTransform.GetChild(childIndex);
            var childGO = childTransform.gameObject;

            // Only include GameObjects with UrdfLink component (robot links)
            if (childGO.TryGetComponent(out UrdfLink _))
            {
                var childNode = new TransformTreeNode(childGO);
                tfNode.Children.Add(childNode);
            }
        }
    }
    
    /// <summary>
    /// Checks if the transform has moved beyond specified thresholds.
    /// Useful for detecting significant movement to trigger updates.
    /// Currently unused but available for optimization.
    /// </summary>
    public bool HasMoved(float posEpsilonSqr, float rotEpsilon)
    {
        if (!PoseInitialized)
        {
            LastPosition = Transform.position;
            LastRotation = Transform.rotation;
            PoseInitialized = true;
            return true;
        }

        var posDeltaSqr = (Transform.position - LastPosition).sqrMagnitude;
        var rotAngle = UnityEngine.Quaternion.Angle(LastRotation, Transform.rotation);

        return posDeltaSqr > posEpsilonSqr || rotAngle > rotEpsilon;
    }

    public void UpdateCachedPose()
    {
        LastPosition = Transform.position;
        LastRotation = Transform.rotation;
    }
}