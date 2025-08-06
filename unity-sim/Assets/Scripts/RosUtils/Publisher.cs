using System;
using RosSharp.RosBridgeClient;
using UnityEngine;

/**
 * Helper class for automatically advertising topics before publishing through RosManager.Instance
 */
public class Publisher<T> where T : Message
{
    private readonly string _topic;
    
    public Publisher(string topic)
    {
        _topic = topic;
        RosManager.Instance.OnConnected += OnConnected;
        if (RosManager.Instance.IsConnected)
            OnConnected();
    }

    private void OnConnected()
    {
        RosManager.Instance.Socket.Advertise<T>(_topic);
    }

    public void Publish(T message)
    {
        if (RosManager.Instance.IsConnected)
            RosManager.Instance.Socket?.Publish(_topic, message);
    }

    ~Publisher()
    {
        RosManager.Instance.OnConnected -= OnConnected;    
        RosManager.Instance.Socket?.Unadvertise(_topic);
    }
}
