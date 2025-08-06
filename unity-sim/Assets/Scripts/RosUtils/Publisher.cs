using System;
using RosSharp.RosBridgeClient;
using UnityEngine;

namespace RosUtils
{
    /**
     * Helper class for automatically advertising topics before publishing through RosManager.Instance
     */
    public class Publisher<T> : IDisposable where T : Message
    {
        private readonly string _topic;
        private string _publicationId = "";
    
        public Publisher(string topic)
        {
            _topic = topic;
            if (RosManager.Instance.IsConnected)
                OnConnected();
            RosManager.Instance.OnConnected += OnConnected;
        }

        private void OnConnected()
        {
            _publicationId = RosManager.Instance.Socket.Advertise<T>(_topic);
            Debug.Log($"Advertised topic {_topic} received publicationId {_publicationId} from ROS Manager ({RosManager.Instance.ID})");
        }

        public void Publish(T message)
        {
            if (RosManager.Instance.IsConnected)
                RosManager.Instance.Socket?.Publish(_publicationId, message);
            else
            {
                Debug.LogError($"Can't publish message {_topic}");
            }
        }

        ~Publisher()
        {
            ReleaseUnmanagedResources();
        }

        private void ReleaseUnmanagedResources()
        {
            if (!RosManager.Instance)
                return;
        
            RosManager.Instance.OnConnected -= OnConnected;    
            Debug.Log($"Unadvertising topic {_topic} from ROS Manager ({RosManager.Instance.ID})");
            RosManager.Instance.Socket?.Unadvertise(_publicationId);
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }
    }
}
