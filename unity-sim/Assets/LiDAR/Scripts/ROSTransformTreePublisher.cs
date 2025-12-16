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

[DefaultExecutionOrder(-200)]
public class ROSTransformTreePublisher : MonoBehaviour
{
    const string TfTopic = "/tf";

	[SerializeField] private float _hz = 20f;
    [SerializeField] private List<string> _frameIds = new() { "map", "odom" };
    [SerializeField] private GameObject _root;

	private double _lastPublishTime;
    private TransformTreeNode _transformRoot;
    private Publisher<TFMessage> _tfPublisher;
    private double _publishPeriod => 1.0 / _hz;

	private TransformStamped[] _cachedTfs;
    private int _maxTfCount;
    private HashSet<TransformTreeNode> _dirtyNodes = new();
    private Dictionary<string, int> _frameIdToIndex = new();

	void Start()
    {
        if (_root == null) {
            Debug.LogWarning($"No root specified, using {name}");
            _root = gameObject;
        }
        
        _tfPublisher = new Publisher<TFMessage>(TfTopic);
        _transformRoot = new TransformTreeNode(_root);
        BuildFrameIdLookup();
        
        // Calculate the maximum number of transforms we'll ever need
        // Tree size + frame IDs + small buffer for safety
        _maxTfCount = GetTreeSize(_transformRoot) + _frameIds.Count + 10;
        _cachedTfs = new TransformStamped[_maxTfCount];
        
        _lastPublishTime = Clock.time + _publishPeriod;
    }

	void BuildFrameIdLookup()
    {
        _frameIdToIndex.Clear();
        for (int i = 0; i < _frameIds.Count; i++)
            _frameIdToIndex[_frameIds[i]] = i;
    }
    
    static int GetTreeSize(TransformTreeNode node)
    {
        int size = 1;
        foreach (var child in node.Children)
            size += GetTreeSize(child);
        return size;
    }

	public void MarkDirty(TransformTreeNode node) => _dirtyNodes.Add(node);

	void LateUpdate()
    {
        if (_transformRoot.Transform.hasChanged)
            _dirtyNodes.Add(_transformRoot);
            
        foreach (var node in _dirtyNodes)
            MarkDirtyRecursive(node);
        _dirtyNodes.Clear();
    }
	
	static void MarkDirtyRecursive(TransformTreeNode node)
    {
        if (node.Transform.hasChanged)
            node.IsDirty = true;
        foreach (var child in node.Children)
            MarkDirtyRecursive(child);
    }

	public void PublishMessage(double stampTime)
    {  
        int tfIndex = BuildGlobalChain(stampTime);
        BuildTreeTransforms(stampTime, ref tfIndex);
       
        // Create a span/slice of the array up to the actual count used
        // Only copy the transforms we actually populated
        TransformStamped[] messageTfs = new TransformStamped[tfIndex];
        Array.Copy(_cachedTfs, messageTfs, tfIndex);
        
        _tfPublisher.Publish(new TFMessage(messageTfs));
    }

	int BuildGlobalChain(double time)
    {
        int tfCount = 0;
        if (_frameIds.Count == 0) return tfCount;

        var h0 = TimeStamp.GetHeader(time, _frameIds[^1]);
        _cachedTfs[tfCount++] = new TransformStamped
        {
            header = h0,
            child_frame_id = _transformRoot.name,
            transform = _transformRoot.Transform.ToRosSharpTransform()
        };
        
        for (int i = 1; i < _frameIds.Count; i++)
        {
            var h = TimeStamp.GetHeader(time, _frameIds[i - 1]);
            _cachedTfs[tfCount++] = new TransformStamped
			{
				header = h,
				child_frame_id = _frameIds[i],
				transform = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Transform() // Identity transform
                {
                    translation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Vector3(),
                    rotation = new RosSharp.RosBridgeClient.MessageTypes.Geometry.Quaternion()
                }
			};
        }
        
        return tfCount;
    }

	void BuildTreeTransforms(double time, ref int tfIndex)
    {
        FastPopulateTF(_cachedTfs, ref tfIndex, _transformRoot, time);
    }

	static void FastPopulateTF(TransformStamped[] tfArray, ref int index, 
                              TransformTreeNode tfNode, double time)
    {
        foreach (var childTf in tfNode.Children)
        {
            if (!childTf.IsDirty && childTf.CachedStamp != null && 
                childTf.LastStampTime == time)
            {
                tfArray[index++] = childTf.CachedStamp;
                continue;
            }
            
            tfArray[index++] = TransformTreeNode.ToTransformStamped(childTf, tfNode.name, time);
            childTf.CachedStamp = tfArray[index-1];  
            childTf.LastStampTime = (long)time;
            childTf.IsDirty = false;
            
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