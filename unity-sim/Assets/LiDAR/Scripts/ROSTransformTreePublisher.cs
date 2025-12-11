using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using RosUtils;
using RosSharp.RosBridgeClient.MessageTypes.Geometry;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosSharp.RosBridgeClient.MessageTypes.Tf2;

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
    private int _cachedTfCount;
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
        
        _cachedTfs = new TransformStamped[GetTreeSize(_transformRoot) + _frameIds.Count + 10];
        _cachedTfCount = 0;
        
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
        // Auto-detect changes via Transform.hasChanged
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

	void PublishMessage()
    {   
        var time = UnityEngine.Time.timeAsDouble;
        
        // 1. Global frame chain (static, cache once)
        int tfIndex = BuildGlobalChain(time);
        
        // 2. Tree transforms (dirty-only rebuild)
        BuildTreeTransforms(time, ref tfIndex);
        
        // 3. Publish cached array
        Array.Resize(ref _cachedTfs, tfIndex);
        var tfMessage = new TFMessage(_cachedTfs);
        _tfPublisher.Publish(tfMessage);
        
        _lastPublishTime = Clock.FrameStartTimeInSeconds;
    }

	int BuildGlobalChain(double time)
    {
        _cachedTfCount = 0;
        
        // Root to first global frame
        if (_frameIds.Count > 0)
        {
            _cachedTfs[_cachedTfCount++] = new TransformStamped(
                TimeStamp.GetHeader(time, _frameIds[^1]),  // Last frame as child frame
                _transformRoot.name,
                TransformExtensions.ToFLU(_transformRoot.Transform));
        }
        
        // Global frame chain (identity transforms)
        for (int i = 1; i < _frameIds.Count; i++)
        {
            _cachedTfs[_cachedTfCount++] = new TransformStamped(
                TimeStamp.GetHeader(time, _frameIds[i-1]),
                _frameIds[i],
                new RosSharp.RosBridgeClient.MessageTypes.Geometry.Transform());  // Identity
        }
        
        return _cachedTfCount;
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
        if (Clock.NowTimeInSeconds - _lastPublishTime < _publishPeriod)
            return;
        PublishMessage();
    }

	private void OnDestroy()
    {
        _tfPublisher.Dispose();
    }
}
