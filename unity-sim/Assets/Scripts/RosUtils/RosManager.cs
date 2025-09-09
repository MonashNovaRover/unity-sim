using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.Protocols;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using static UnityEngine.SceneManagement.SceneManager;
using Random = UnityEngine.Random;

public class RosManager : MonoBehaviour
{
    [Header("rosbridge")]
    [InspectorName("Websocket URI")] 
    public string socketUri = $"ws://localhost:9090";
    public RosSocket.SerializerEnum serializer;
    public Protocol protocol;
    [InspectorName("Connection timeout (in seconds)")] 
    public int secondsTimeout = 10;
    
    public delegate void ConnectionEvent();
    /// <summary>
    /// Event that is invoked whenever a connection to rosbridge is made.
    /// </summary>
    public ConnectionEvent OnConnected;
    /// <summary>
    /// Event that is invoked whenever the connection to rosbridge is lost. 
    /// </summary>
    public ConnectionEvent OnDisconnected;
    
    /// <summary>
    /// Used to make connections to rosbridge. You can assume this is never null.
    /// </summary>
    public RosSocket Socket => _socket ?? CreateSocket();
    private RosSocket _socket;  /// The private instance of the Socket
    /// The instance of the protocol used to connect the Socket
    private IProtocol _protocolInstance;
    /// Used to make sure two threads aren't trying to open RosBridge at the same time
    private readonly object _socketCreationLock = new object();
    
    /// Singleton accessor to the current RosManager instance. If there is no current RosManager, one will be createed.
    public static RosManager Instance => _instance ?? CreateInstance();
    private static RosManager _instance;

    public int ID => _id;
    private int _id = 0;

    /// Used to wait until the socket is connected.
    public ManualResetEvent IsConnectedEvent { get; private set; }
    /// Used to wait until the socket is finished the attempt at connecting (might connect or timeout).
    public ManualResetEvent IsConnectingEvent { get; private set; }
    
    /// True when the Socket is connected to rosbridge
    public bool IsConnected { get; private set; }
    /// True when the Socket is trying to connect to rosbridge
    public bool IsConnecting { get; private set; }
    
    /// <summary>
    /// Call this whenever you want to recreate the Socket connection to rosbridge. Deletes any existing socket,
    /// then creates a new one.
    ///
    /// If for example you have a UI to change the uri used to connect to rosbridge, you'd want to call this after
    /// applying any changes.
    /// </summary>
    public void RecreateSocket()
    {
        lock (_socketCreationLock)
        {
            ClearSocket();
            CreateSocket();
        }
    }
    
    private static RosManager CreateInstance()
    {
        if (_instance != null)
            return _instance;
        
        _instance = FindFirstObjectByType<RosManager>();
        
        if (_instance != null)
            return _instance;
        
        Debug.Log("Creating RosManager Instance");
        var go = new GameObject($"ROS Manager");
        _instance = go.AddComponent<RosManager>();
        
        var scene = GetActiveScene();

        if (!scene.isLoaded)
        {
            Debug.LogError($"Tried to access RosManager before the current scene ({scene.name}) was loaded.");
        }
        
        MoveGameObjectToScene(go, scene);
        
        return _instance;
    }

    private void Awake()
    {
        _id = Random.Range(10000, 99999); 
        gameObject.name += $" ({_id})";
        
        if (_instance != null && _instance != this)
        {
            Debug.LogError($"{nameof(RosManager)} instance already created. Destroying new instance.");
            Destroy(gameObject);
            return;
        }
        
        _instance = this; 
        DontDestroyOnLoad(gameObject);
        
        Debug.Log($"Starting ROS Manager ({_id})");
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
            Debug.LogWarning("Failed to connect to RosBridge at: " + socketUri);
        
        IsConnecting = false;
        IsConnectedEvent.Reset();
    }

    /// <summary>
    /// Called to create the Socket instance. Does nothing if there is already a socket created.
    /// </summary>
    /// <returns>The created or existing socket</returns>
    private RosSocket CreateSocket()
    {
        lock (_socketCreationLock)
        {
            if (_socket != null)
                return _socket;
        
            
            // _protocolInstance = ProtocolInitializer.GetProtocol(protocol, socketUri);
            // _protocolInstance.OnConnected += Connected;
            // _protocolInstance.OnClosed += Closed;
            //
            // _socket = new RosSocket(_protocolInstance, serializer)
            // {
            //     protocol = _protocolInstance
            // };
            
            Debug.Log("Creating Socket the old way");
            _socket = new RosSocket(new WebSocketNetProtocol(socketUri));
            _socket.protocol.OnConnected += Connected;
            _socket.protocol.OnClosed += Closed;
            _protocolInstance = _socket.protocol;

            return _socket;
        }
    }
    
    private void Connected(object sender, EventArgs e)
    {
        IsConnectedEvent.Set();
        IsConnected = true;
        Debug.Log($"ROS Manager ({_id}) Connected to rosbridge: {socketUri}");
        
        OnConnected?.Invoke();
    }

    private void Closed(object sender, EventArgs e)
    {
        IsConnected = false;
        IsConnectedEvent.Reset();
        Debug.Log($"ROS Manager ({_id}) Disconnected from rosbridge: {socketUri}");
        
        OnDisconnected?.Invoke();
    }

    private void OnDestroy()
    {
        ReleaseUnmanagedResources();
    }
    
    ~RosManager()
    {
        ReleaseUnmanagedResources();
    }
    
    private void ReleaseUnmanagedResources()
    {
        Debug.Log($"ROS Manager ({_id}) destroyed.");
        ClearSocket();
    }

    /// <summary>
    /// Release all resources relating to the socket
    /// </summary>
    private void ClearSocket()
    {
        lock (_socketCreationLock)
        {
            if (_socket != null)
            {
                Debug.Log($"ROS Manager ({_id}) closing Socket.");
                _socket.Close();
                _socket = null;
            }
            
            if (_protocolInstance != null)
            {
                Debug.Log($"ROS Manager ({_id}) removing Socket protocol.");
                _protocolInstance.OnConnected -= Connected;
                _protocolInstance.OnClosed -= Closed;
                _protocolInstance = null;
            }
        }
    }
    
    
}
