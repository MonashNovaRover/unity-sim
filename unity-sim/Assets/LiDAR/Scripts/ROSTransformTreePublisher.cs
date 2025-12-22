using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using RosUtils;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosSharp.RosBridgeClient.MessageTypes.Tf2;
using Unity.Robotics.ROSTCPConnector.ROSGeometry;

/// <summary>
/// Publishes the Unity transform hierarchy as ROS TF (Transform) messages.
/// Essential for ROS navigation, visualization, and coordinate frame transformations.
/// 
/// Manages:
/// - Global reference frames (map, odom, etc.)
/// - Robot transform tree (links from URDF)
/// - Efficient publishing with dirty-flagging to minimize network traffic
/// 
/// Execution order is set to -200 to ensure transforms are published before other components.
/// </summary>
[DefaultExecutionOrder(-200)]
public class ROSTransformTreePublisher : MonoBehaviour
{
    const string TfTopic = "/tf";

    [Header("Publishing Configuration")]
    [Tooltip("Publishing frequency in Hz")]
	[SerializeField] private float _hz = 20f;
    
    [Header("Frame Hierarchy")]
    [Tooltip("Global reference frames in order (e.g., map -> odom). Last frame is parent of root.")]
    [SerializeField] private List<string> _frameIds = new() { "map", "odom" };
    
    [Tooltip("Root GameObject of the robot transform tree (typically the robot base)")]
    [SerializeField] private GameObject _root;

	private double _lastPublishTime;
    private TransformTreeNode _transformRoot;
    private Publisher<TFMessage> _tfPublisher;
    private double _publishPeriod => 1.0 / _hz;
	private TransformStamped[] _cachedTfs;
    private int _maxTfCount;
    private HashSet<TransformTreeNode> _dirtyNodes = new();
    private Dictionary<string, int> _frameIdToIndex = new();

    /// <summary>
    /// Initializes the transform tree and ROS publisher.
    /// Builds the tree structure from Unity hierarchy and pre-allocates memory.
    /// </summary>
	void Start()
    {
        if (_root == null) {
            Debug.LogWarning($"No root specified, using {name}");
            _root = gameObject;
        }
        
        // Create ROS publisher for TF messages
        _tfPublisher = new Publisher<TFMessage>(TfTopic);
        
        // Build transform tree from Unity hierarchy
        _transformRoot = new TransformTreeNode(_root);
        BuildFrameIdLookup();
        
        _maxTfCount = GetTreeSize(_transformRoot) + _frameIds.Count + 10;
        _cachedTfs = new TransformStamped[_maxTfCount];
        
        _lastPublishTime = Clock.time + _publishPeriod;
    }

    // Creates a lookup dictionary for quick frame ID access.
	void BuildFrameIdLookup()
    {
        _frameIdToIndex.Clear();
        for (int i = 0; i < _frameIds.Count; i++)
            _frameIdToIndex[_frameIds[i]] = i;
    }
    
    // Recursively counts all nodes in the transform tree.
    static int GetTreeSize(TransformTreeNode node)
    {
        int size = 1;
        foreach (var child in node.Children)
            size += GetTreeSize(child);
        return size;
    }

	public void MarkDirty(TransformTreeNode node) => _dirtyNodes.Add(node);

    // Checks for transform changes and marks dirty nodes.
	void LateUpdate()
    {
        if (_transformRoot.Transform.hasChanged)
        {
            _dirtyNodes.Add(_transformRoot);
            _transformRoot.Transform.hasChanged = false;
        }
        
        foreach (var node in _dirtyNodes)
            MarkDirtyRecursive(node);
        _dirtyNodes.Clear();
    }
	
    // Recursively marks nodes as dirty when their transform changed.
	static void MarkDirtyRecursive(TransformTreeNode node)
    {
        if (node.Transform.hasChanged)
        {
            node.IsDirty = true;
            node.Transform.hasChanged = false;
        }
        foreach (var child in node.Children)
            MarkDirtyRecursive(child);
    }

    // Builds and publishes the complete TF message.
	public void PublishMessage(double stampTime)
    {  
        // Build global frame chain (map -> odom -> base_link)
        int tfIndex = BuildGlobalChain(stampTime);
        
        // Build robot transform tree (all robot links)
        BuildTreeTransforms(stampTime, ref tfIndex);
       
        // Create message with only the used portion of the array
        TransformStamped[] messageTfs = new TransformStamped[tfIndex];
        Array.Copy(_cachedTfs, messageTfs, tfIndex);
        
        _tfPublisher.Publish(new TFMessage(messageTfs));
    }

    // Builds the global reference frame chain.
	int BuildGlobalChain(double time)
    {
        int tfCount = 0;
        if (_frameIds.Count == 0) return tfCount;

        // Connect root to last global frame (e.g., odom -> base_link)
        var h0 = TimeStamp.GetHeader(time, _frameIds[^1]);
        _cachedTfs[tfCount++] = new TransformStamped
        {
            header = h0,
            child_frame_id = _transformRoot.name,
            transform = _transformRoot.Transform.ToRosSharpTransform()
        };
        
        // Build chain of global frames with identity transforms
        // Example: map -> odom (identity transform, no relative motion)
        for (int i = 1; i < _frameIds.Count; i++)
        {
            var h = TimeStamp.GetHeader(time, _frameIds[i - 1]);
            _cachedTfs[tfCount++] = new TransformStamped
			{
				header = h,
				child_frame_id = _frameIds[i],
				transform = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Transform() // Identity
                {
                    translation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(),
                    rotation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion()
                }
			};
        }
        
        return tfCount;
    }

    // Builds transforms for the entire robot tree.
	void BuildTreeTransforms(double time, ref int tfIndex)
    {
        FastPopulateTF(_cachedTfs, ref tfIndex, _transformRoot, time);
    }

    // Recursively populates the transform array with robot link transforms.
	static void FastPopulateTF(TransformStamped[] tfArray, ref int index, 
                              TransformTreeNode tfNode, double time)
    {
        foreach (var childTf in tfNode.Children)
        {
            // Create fresh transform message
            tfArray[index++] = TransformTreeNode.ToTransformStamped(childTf, tfNode.name, time);
            childTf.CachedStamp = tfArray[index-1];  
            childTf.LastStampTime = (long)time;
            childTf.IsDirty = false;
            
            // Recurse to children
            if (!childTf.IsALeafNode)
                FastPopulateTF(tfArray, ref index, childTf, time);
        }
    }
    
    void Update()
    {
		double stampTime = Clock.FrameStartTimeInSeconds;

        if (stampTime - _lastPublishTime < _publishPeriod)
            return;

        PublishMessage(stampTime);
		_lastPublishTime = stampTime;
    }

	private void OnDestroy()
    {
        _tfPublisher.Dispose();
    }
}