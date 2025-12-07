using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cameras.DTO;
using UnityEngine;
using UnityEngine.Assertions.Must;
using WebSocketSharp;
using WebSocketSharp.Server;

public class SignalingServerService : WebSocketBehavior
{
    public static SignalingServerService Instance { get; private set; }
    
    private static string _producerID = "";
    private static List<string> _listenerIDs = new List<string>();

    private string[] _listenerMessageTypes = new [] { "welcome", "list", "peerStatusChanged" };
    
    protected override void OnOpen()
    {
        Instance = this; 
        
        // Log that a new connection was opened
        var session = Sessions.Sessions.First(s => s.ID == ID);
        Debug.Log($"Signaling Server ({session.ID}) -- opened");
    }

    void SendToProducer(string data)
    {
        if (_producerID.IsNullOrEmpty())
        {
            Debug.LogWarning($"Signaling Server ({ID}) send to producer -- no producer ID");
            return;
        }
        
        Debug.LogWarning($"Signaling Server ({ID}) send to producer -- {data}");
        Sessions.SendTo(data, _producerID);
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        var name = ID == _producerID ? "Producer" : ID;
        var wrapper = JsonUtility.FromJson<DTOWrapper>(e.Data) ?? new DTOWrapper();
        
        
        
        Debug.Log($"Signaling Server ({name}) -- message: {e.Data}\ntype: {wrapper.type}");
        
        if (wrapper.type == "setPeerStatus")
        {
            var dto = JsonUtility.FromJson<SetPeerStatusDTO>(e.Data) ?? new SetPeerStatusDTO();

            if (dto.roles.Contains("producer"))
            {
                _producerID = ID;
                Debug.Log($"Signaling Server ({name}) -- found producer: {ID}");
            }

            if (dto.roles.Contains("listener") && !_listenerIDs.Contains(ID))
            {
                _listenerIDs.Add(ID);
                SendToProducer(e.Data);
                // Send welcome message
                Sessions.SendTo("{\"type\":\"welcome\"}", ID);
                Debug.Log($"Signaling Server ({name}) -- found listener: {ID}");
            }
            
            return;
        }

        // Include the sender's ID with the outgoing message
        var trimmedMessage = e.Data.TrimEnd();
        var modifiedMessage = !trimmedMessage.EndsWith("}") ? trimmedMessage 
            : !wrapper.unitysimSender.IsNullOrEmpty() ? trimmedMessage 
            : trimmedMessage
                  .Substring(0, trimmedMessage.Length - 1)
              + ",\"unitysimSender\":\"" + ID + "\"}";
        
        if (!wrapper.unitysimDestination.IsNullOrEmpty())
        {
            Sessions.SendTo(modifiedMessage, wrapper.unitysimDestination);
            return;
        }
        
        if (ID != _producerID)
        {
            SendToProducer(modifiedMessage);
            return;
        }
        
        foreach (var id in Sessions.ActiveIDs)
        {
            if (id == _producerID) // ID is the identifier of the current session
                continue; 
            
            // Ban listeners from consuming types not in _listenerMessageTypes
            if (_listenerIDs.Contains(id) && !_listenerMessageTypes.Contains(wrapper.type))
            {
                Debug.Log($"Skipping listener {ID}");
                continue;
            }
            
            Sessions.SendTo(modifiedMessage, id);
        }
    }
}