using System;
using System.Diagnostics.CodeAnalysis;
using RosSharp.RosBridgeClient;
using UnityEngine;
using System.Threading.Tasks;

/**
 * Helper class for automatically advertising topics before publishing through RosManager.Instance
 */
public class Subscriber<T> where T : Message
{
    private readonly object _msgLock = new();

    public class Profile
    {
        public int throttleRate = 0;
        public int queueLength = 1;
        public int fragmentSize = 2147483647;
        public string compression = "none";
        public bool ensureThreadSafety = false;
    }

    delegate void SubscriberCallback(T msg);
    
    private readonly string _topic;
    private readonly Profile _profile;

    /// <summary>
    /// The most recent message from the topic
    /// </summary>
    [MaybeNull]
    public T Message
    {
        get
        {
            lock (_msgLock)
                return _message;
        }
    }
    [MaybeNull]
    private T _message = null;
    
    public Subscriber(string topic, T initialValue = null, Profile profile = null)
    {
        _topic = topic;
        _profile = profile ?? new Profile();
        
        RosManager.Instance.OnConnected += OnConnected;
        if (RosManager.Instance.IsConnected)
            OnConnected();
    }

    private void OnConnected()
    {
        RosManager.Instance.Socket.Advertise<T>(_topic);
        RosManager.Instance.Socket.Subscribe<T>(topic: _topic, OnMessage, _profile.throttleRate, _profile.queueLength, 
            _profile.fragmentSize, _profile.compression, _profile.ensureThreadSafety);
    }

    private void OnMessage(T msg)
    {
        lock (_msgLock)
            _message = msg;
    }
    
    ~Subscriber()
    {
        ReleaseUnmanagedResources();
    }

    private void ReleaseUnmanagedResources()
    {
        if (!RosManager.Instance)
            return;
        
        RosManager.Instance.OnConnected -= OnConnected;    
        RosManager.Instance.Socket?.Unadvertise(_topic);
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }
    
}
