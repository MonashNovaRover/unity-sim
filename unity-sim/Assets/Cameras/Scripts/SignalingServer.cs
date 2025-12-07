using System.Net;
using UnityEditor.VersionControl;
using UnityEngine;
using WebSocketSharp.Server;

public class SignalingServer : MonoBehaviour 
{
    public WebSocketSharp.Server.WebSocketServer Server;

    public string uri = "ws://localhost:8443";
    
    private void Start()
    {
        // Server = new WebSocketSharp.Server.WebSocketServer(uri);
        Server = new WebSocketServer(IPAddress.Any, 8443, false);  
        Server.AddWebSocketService<SignalingServerService>("/");
        
        if (Server.IsSecure)
            Debug.Log("Signaling Server is secure");

        Server.Start();
        
        Debug.Log($"Signaling Server is running on {Server.Address} (port {Server.Port}, with security {Server.IsSecure})");
    }

    private void OnDestroy()
    {
        Server.Stop();
    }
}