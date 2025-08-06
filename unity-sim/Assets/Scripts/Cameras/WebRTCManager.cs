using System;
using System.Collections.Concurrent;
using Cameras.DTO;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Serialization;
using WebSocketSharp;

public delegate void WsMessageHandler(string message);


[RequireComponent(typeof(SignalingServer))]
public class WebRTCManager : MonoBehaviour
{
    public event WsMessageHandler MessageReceived;
    public void SendWebSocketMessage(string message)
    {
        // Debug.Log($"WebRTCManager sending: {message}");
        _ws.Send(message);
    }

    [SerializeField]
    private string serverIp;

    private WebSocket _ws;

    private readonly ConcurrentQueue<string> _receivedMessages = new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<string> _receivedErrors = new ConcurrentQueue<string>();

    [Header("References")]
    public SignalingServer signalingServer;
    
    public static WebRTCManager Instance => _instance;
    private static WebRTCManager _instance;

    private RTCPeerConnection _localConnection;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        
        _instance = this;
        
        // Create WebSocket instance and connect
        Debug.Log("WebRTCManager Creating WebSocket");
        var ip = string.IsNullOrEmpty(serverIp) ? "localhost:8443" : serverIp;
        var url = $"ws://{ip}";
        _ws = new WebSocket(url);

        // Subscribe to events
        _ws.OnOpen += OnOpen;
        _ws.OnError += OnError;
        // _ws.OnClose += OnClose;
        _ws.OnMessage += OnMessage;

        // Connect
        _ws.Connect();
    }

    // Update is called once per frame
    void Update()
    {

        if (!_ws.IsAlive)
        {
            Debug.LogError($"WebRTCManager has not been initialized");
            _ws.Connect();
            
        }
        // Process received errors on the main thread - Unity functions can only be called from the main thread
        while (_receivedErrors.TryDequeue(out var error))
        {
            Debug.LogError("WS error: " + error);
        }

        // Process received messages on the main thread - Unity functions can only be called from the main thread
        while (_receivedMessages.TryDequeue(out var message))
        {
            Debug.Log("WebRTCManager queued WS Message Received: " + message);
            MessageReceived?.Invoke(message);
        }
        
    }

    private void OnDestroy()
    {
        if (_ws == null)
            return;

        // Unsubscribe from events
        _ws.OnOpen -= OnOpen;
        _ws.OnMessage -= OnMessage;
        _ws.OnError -= OnError;
        _ws.OnClose -= OnClose;

        _ws.Close();
        _ws = null;
    }
    
    private void OnMessage(object sender, MessageEventArgs e)
    {
        try
        {
            _receivedMessages.Enqueue(e.Data);
            // Debug.Log($"WebRTCManager received {e.Data}");
            // MessageReceived?.Invoke(e.Data);
        }
        catch (Exception ex)
        {
            Debug.LogError($"OnMessage Exception: {ex}");
        }
    }

    private void OnError(object sender, ErrorEventArgs e) {
        _receivedErrors.Enqueue(e.Message);
        Debug.LogError($"OnError Exception: {e.Exception}, {e.Message}");
    }

    private void OnOpen(object sender, EventArgs e)
    {
        Debug.Log($"WebRTCManager declaring self as producer");
        SendWebSocketMessage(JsonUtility.ToJson(new SetPeerStatusDTO()
        {
            roles = new []{"producer"}
        }));
        
    }

    private void OnClose(object sender, EventArgs e)
    {
        Debug.Log($"WebRTCManager websocket closed");
    }
}
