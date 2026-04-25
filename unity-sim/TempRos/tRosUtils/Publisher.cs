using System;
using RosSharp.RosBridgeClient;
using UnityEngine;
using UnityEngine.Serialization;
using WebSocketSharp;

namespace RosUtils
{
    
    /**
     * Helper class for automatically advertising topics before publishing through RosManager.Instance
     */
    [Serializable]
    public class Publisher<T> : IDisposable where T : Message
    {
        [FormerlySerializedAs("_topic")] [SerializeField]
        private string topic;
        private string _publicationId = "";
    
        public Publisher(string topic)
        {
            this.topic = topic;
            if (RosManager.Instance.IsConnected)
                OnConnected();
            RosManager.Instance.OnConnected += OnConnected;
        }

        private void OnConnected()
        {
            _publicationId = RosManager.Instance.Socket.Advertise<T>(topic);
            Debug.Log($"Advertised topic {topic} received publicationId {_publicationId} from ROS Manager ({RosManager.Instance.ID})");
        }

        public void Publish(T message)
        {
            if (_publicationId.IsNullOrEmpty())
                _publicationId = RosManager.Instance.Socket.Advertise<T>(topic); 
            if (RosManager.Instance.IsConnected)
                RosManager.Instance.Socket?.Publish(_publicationId, message);
            else
            {
                Debug.LogError($"Can't publish message {topic}");
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
            Debug.Log($"Unadvertising topic {topic} from ROS Manager ({RosManager.Instance.ID})");
            RosManager.Instance.Socket?.Unadvertise(_publicationId);
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }
    }
}
