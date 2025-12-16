using System.Collections.Generic;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using UnityEngine;
using Unity.Robotics.UrdfImporter;
using RosUtils;

public class TransformTreeNode
{
    public readonly GameObject SceneObject;
    public readonly List<TransformTreeNode> Children;
    public UnityEngine.Transform Transform => SceneObject.transform;
    public string name => SceneObject.name;
    public bool IsALeafNode => Children == null || Children.Count == 0;

    public TransformStamped CachedStamp;
    public long LastStampTime;
    public bool IsDirty;
    
    public UnityEngine.Vector3 LastPosition;
    public UnityEngine.Quaternion LastRotation;
    public bool PoseInitialized;
    
    public TransformTreeNode(GameObject sceneObject)
    {
        SceneObject = sceneObject;
        Children = new List<TransformTreeNode>();
        PopulateChildNodes(this);
    }

    public static TransformStamped ToTransformStamped(TransformTreeNode node, string parentFrameId, double time)
    {
        var ts = new TransformStamped(
        TimeStamp.GetHeader(time, parentFrameId),
        node.name,
        TransformExtensions.ToFLU(node.Transform));

    	return ts;
    }

    static void PopulateChildNodes(TransformTreeNode tfNode)
    {
        var parentTransform = tfNode.Transform;
        for (var childIndex = 0; childIndex < parentTransform.childCount; ++childIndex)
        {
            var childTransform = parentTransform.GetChild(childIndex);
            var childGO = childTransform.gameObject;

            if (childGO.TryGetComponent(out UrdfLink _))
            {
                var childNode = new TransformTreeNode(childGO);
                tfNode.Children.Add(childNode);
            }
        }
    }
    
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