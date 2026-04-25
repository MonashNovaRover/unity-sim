using System;
using System.Diagnostics.CodeAnalysis;
using RosSharp.RosBridgeClient;
using UnityEngine;
using System.Threading.Tasks;
using UnityEngine.Serialization;

namespace RosUtils
{
    /**
     * Helper class for automatically advertising topics before publishing through RosManager.Instance
     */
    [Serializable]
    public class Subscriber<T> where T : Message
    {
        private readonly object _msgLock = new();

        [Serializable]
        public class Profile
        {
            public int throttleRate = 0;
            public int queueLength = 1;
            public int fragmentSize = 2147483647;
            public string compression = "none";
            public bool ensureThreadSafety = false;
        }

        [SerializeField] private string topic;
        [SerializeField] private Profile profile;

        public delegate void SubscriberCallback(T msg);
        /// <summary>
        /// Event invoked whenever a new message is available from the subscription.
        /// </summary>
        public event SubscriberCallback OnMessage;

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

        [MaybeNull] private T _message = null;

        public Subscriber(string topic, T initialValue = null, Profile profile = null)
        {
            this.topic = topic;
            _message = initialValue;
            this.profile = profile ?? new Profile();
        }

        public void Connect()
        {
            RosManager.Instance.OnConnected += ConnectedCallback;
            if (RosManager.Instance.IsConnected)
                ConnectedCallback();
        }

        private void ConnectedCallback()
        {
            RosManager.Instance.Socket.Advertise<T>(topic);
            RosManager.Instance.Socket.Subscribe<T>(topic: topic, MessageCallback, profile.throttleRate,
                profile.queueLength,
                profile.fragmentSize, profile.compression, profile.ensureThreadSafety);
        }

        private void MessageCallback(T msg)
        {
            lock (_msgLock)
                _message = msg;
            OnMessage?.Invoke(msg);
        }

        ~Subscriber()
        {
            ReleaseUnmanagedResources();
        }

        private void ReleaseUnmanagedResources()
        {
            if (!RosManager.Instance)
                return;

            RosManager.Instance.OnConnected -= ConnectedCallback;
            RosManager.Instance.Socket?.Unadvertise(topic);
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

    }
}