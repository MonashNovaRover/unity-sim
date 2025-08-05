using UnityEngine;
using RosSharp.RosBridgeClient;
using RosSharp.RosBridgeClient.Protocols;
using UnityEngine.SceneManagement;
using static UnityEngine.SceneManagement.SceneManager;

public class RosManager : MonoBehaviour
{
    public static RosManager Instance
    {
        get => _instance;
    }
    private static RosManager _instance;

    // The connection to RosBridge
    public string IP 
    {
        get => _ip;
        set
        {
            _ip = value;
            CreateSocket();
        }
    }
    [SerializeField] private static string _ip = "localhost";

    public string Port
    {
        get => _port;
        set
        {
            _port = value;
            CreateSocket();
        }
    }
    [SerializeField] private static string _port = "9090";
    
    /// <summary>
    /// Used to make connections to rosbridge. Safe to assume is always initialized.
    /// </summary>
    public RosSocket Socket => _socket ?? CreateSocket();
    private RosSocket _socket;
    
    // Singleton logic
    
    private static RosManager CreateInstance()
    {
        var go = new GameObject("ROS Manager");
        
        _instance = go.AddComponent<RosManager>();
        
        return _instance;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
         
    }

    private RosSocket CreateSocket() => CreateSocket(_ip, _port);
    private RosSocket CreateSocket(string ip, string port)
    {
        var uri = $"ws://{ip}:{port}";
        _socket = new RosSocket(new WebSocketNetProtocol(uri));
        return _socket;
    }
}
