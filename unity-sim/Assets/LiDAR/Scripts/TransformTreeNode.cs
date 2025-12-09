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
    public bool IsALeafNode => Children.Count == 0;

    public TransformStamped CachedStamp;
    public long LastStampTime;
    public bool IsDirty;
    
    public TransformTreeNode(GameObject sceneObject)
    {
        SceneObject = sceneObject;
        Children = new List<TransformTreeNode>();
        PopulateChildNodes(this);
    }

    public static TransformStamped ToTransformStamped(TransformTreeNode node, string parentFrameId, double time)
    {
        return new TransformStamped(
            TimeStamp.GetHeader(time, parentFrameId),  
            node.name,                                
            TransformExtensions.ToFLU(node.Transform)                 
        );
    }

    // Overload for backward compatibility
    public static TransformStamped ToTransformStamped(TransformTreeNode node, double time)
    {
        return ToTransformStamped(node, "world", time);
    }

    static void PopulateChildNodes(TransformTreeNode tfNode)
    {
        var parentTransform = tfNode.Transform;
        for (var childIndex = 0; childIndex < parentTransform.childCount; ++childIndex)
        {
            var childTransform = parentTransform.GetChild(childIndex);
            var childGO = childTransform.gameObject;

            // Only URDF links (maintains tree structure)
            if (childGO.TryGetComponent(out UrdfLink _))
            {
                var childNode = new TransformTreeNode(childGO);
                tfNode.Children.Add(childNode);
            }
        }
    }
}