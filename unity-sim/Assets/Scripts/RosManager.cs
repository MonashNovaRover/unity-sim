using System;
using System.Threading;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.Protocols;
using static UnityEngine.SceneManagement.SceneManager;

public class RosManager : MonoBehaviour
{
    public delegate void ConnectionEvent();
    public ConnectionEvent OnConnected;
    public ConnectionEvent OnDisconnected;
    
    public static RosManager Instance
    {
        // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
        get => _instance ?? CreateInstance();
    }
    private static RosManager _instance;

    public string SocketURI = $"ws://localhost:9090";
    
    public RosSocket.SerializerEnum serializer;
    public Protocol protocol;
    public int secondsTimeout = 10;
    
    public ManualResetEvent IsConnectedEvent { get; private set; }
    public ManualResetEvent IsConnectingEvent { get; private set; }
    
    public bool IsConnected { get; private set; }
    public bool IsConnecting { get; private set; }
    
    /// <summary>
    /// Used to make connections to rosbridge. Safe to assume is always initialized.
    /// </summary>
    public RosSocket Socket => _socket ?? CreateSocket();
    private RosSocket _socket;
    
    // Singleton logic
    
    private static RosManager CreateInstance()
    {
        var go = new GameObject("ROS Manager");
        
        // ReSharper disable once Unity.PerformanceCriticalCodeInvocation
        _instance = go.AddComponent<RosManager>();
        
        var scene = GetActiveScene();
        MoveGameObjectToScene(go, scene);
        
        return _instance;
    }
    
    public virtual void Awake()
    {
        Debug.Log("Starting RosManager");
        IsConnectedEvent = new ManualResetEvent(false);
        IsConnectingEvent = new ManualResetEvent(false);
        new Thread(ConnectAndWait).Start();
    }

    /// <summary>
    /// Run on its own thread, attempts to connect to ROS
    /// </summary>
    private void ConnectAndWait()
    {
        IsConnectingEvent.Set();
        IsConnecting = true;
        _socket = CreateSocket(); 

        if (!IsConnectedEvent.WaitOne(secondsTimeout * 1000))
            Debug.LogWarning("Failed to connect to RosBridge at: " + SocketURI);
        
        IsConnecting = false;
        IsConnectedEvent.Reset();
    }

    private RosSocket CreateSocket()
    {
        var protocolInstance = ProtocolInitializer.GetProtocol(protocol, SocketURI);
        
        protocolInstance.OnConnected += Connected;
        protocolInstance.OnClosed += Closed;
        
        _socket = new RosSocket(protocolInstance, serializer);
        return _socket;
    }
    
    private void Connected(object sender, EventArgs e)
    {
        IsConnectedEvent.Set();
        IsConnected = true;
        Debug.Log("Connected to RosBridge: " + SocketURI);
        
        OnConnected?.Invoke();
    }

    private void Closed(object sender, EventArgs e)
    {
        IsConnected = false;
        IsConnectedEvent.Reset();
        Debug.Log("Disconnected from RosBridge: " + SocketURI);
        
        OnDisconnected?.Invoke();
    }
}
