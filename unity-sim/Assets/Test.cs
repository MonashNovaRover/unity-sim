using UnityEngine;
using RosSharp.RosBridgeClient;

public class Test : MonoBehaviour
{
    public static Test Instance
    {
        get => _instance;
    }
    private static Test _instance;

    private static Test CreateInstance()
    {
        return _instance;
    }
    
    // The connection to RosBridge
    public string IP 
    {
        get => _ip;
        set
        {
            _ip = value;
            CreateRosSocket(_ip, _port);
        }
    }
    [SerializeField] private static string _ip = "localhost";

    public string Port
    {
        get => _port;
        set
        {
            _port = value;
            CreateRosSocket(_ip, _port);
        }
    }
    [SerializeField] private static string _port = "9090";
    
    private RosSocket rosSocket;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        
         
    }

    void OnDestroy()
    {
        rosSocket.Close();
    }

    void SetURI(string ip, string port)
    {
        _ip = ip;
        _port = port;
        CreateRosSocket(ip, port);
    }

    private void CreateRosSocket(string ip, string port)
    {
        string uri = $"ws://{ip}:{port}";
        // rosSocket = new RosSocket(new RosBridgeClient.Protocols.WebSocketNetProtocol(uri));
    }
}
