using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Cameras;
using Cameras.DTO;
using Unity.WebRTC;
using UnityEngine;

public class WebRTCCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera cam;
    [SerializeField] private VideoManager videoManager;
    
    [Header("Configuration")]
    public string serial = "";
    
    /// The 'peerId' for this camera
    public string PeerId => _peerId.Value;
    private readonly Lazy<string> _peerId = new Lazy<string>(() => Guid.NewGuid().ToString());
    
    public int width = 1280;
    public int height = 720;
    
    // Private members    

    public class ConnectionHandle
    {
        [MaybeNull]
        public RTCPeerConnection PeerConnection = null;
        [MaybeNull]
        public RTCRtpTransceiver Transceiver = null;
        
        public bool CanConnect
            => PeerConnection?.ConnectionState == RTCPeerConnectionState.New ||
               PeerConnection?.ConnectionState == RTCPeerConnectionState.Disconnected;
    }
    
    private MediaStream _videoStream;
    /// Map from sessionId to handle
    private Dictionary<string, ConnectionHandle> _connections = new Dictionary<string, ConnectionHandle>();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (videoManager == null)
            videoManager = VideoManager.Instance;
    }

    void OnEnable()
    {
        videoManager?.RegisterCam(this);
    }

    void OnDestroy()
    {
        videoManager?.UnregisterCam(this);
    }

    /// <summary>
    /// Called when receiving a "startMessage" signaling server command for this peerId.
    /// </summary>
    public void StartSession(string sessionId)
    {
        _videoStream ??= cam?.CaptureStream(width, height);
        
        var config = new RTCConfiguration
        {
            iceServers = new RTCIceServer[]
            {
                new RTCIceServer
                {
                    urls = new string[]
                    {
                        // Google Stun server
                        "stun:stun.l.google.com:19302",
                    },
                },
                new RTCIceServer()
                {
                    urls = new  string[] {"stun:stun1.l.google.com:19302"},
                },
                new RTCIceServer()
                {
                    urls = new  string[] {"stun:stun.iptel.org"}
                }
            },
            bundlePolicy = RTCBundlePolicy.BundlePolicyBalanced,
        };
        
        ConnectionHandle handle = new ConnectionHandle()
        {
            PeerConnection = new RTCPeerConnection(ref config)
        };
        _connections.Add(sessionId, handle);

        // Triggered when a new network endpoint is found that could potentially be used to establish the connection
        handle.PeerConnection.OnIceCandidate += candidate => OnIceCandidate(candidate, sessionId);
        
        // "Negotiation" is the exchange of SDP Offer/Answer. Peers describe what media they want to send and agree on, for example, what codecs to use
        // In this tutorial we exchange the SDP Offer/Answer only once when connecting.
        // But in a real production you'd have to repeat the exchange every time the OnNegotiationNeeded event is triggered
        handle.PeerConnection.OnNegotiationNeeded += () => OnNegotiationNeeded(sessionId);
        handle.PeerConnection.OnIceConnectionChange += OnIceConnectionChange;
        handle.PeerConnection.OnIceGatheringStateChange += state => Debug.Log($"IceGatheringStateChange: {state}"); 

        // Triggered when a new track is received
        // handle.PeerConnection.OnTrack += OnTrack;
        
        AddTracks(ref handle);
        
        var started = new SessionStartedDTO()
        {
            sessionId = sessionId,
            peerId = PeerId
        };
        videoManager?.client.SendWebSocketMessage(JsonUtility.ToJson(started));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    
    public IEnumerator OnRemoteSdpOfferReceived(RTCSessionDescription remoteSdpOffer, string sessionId)
    {
        Debug.Log("Remote SDP Offer received. Set as local offer and send back the generated answer");

        ConnectionHandle handle;
        if (!_connections.TryGetValue(sessionId, out handle))
        {
            Debug.LogError($"WebRTCCamera {serial} -- SessionId \"{sessionId}\" does not exist!");
            yield break;
        }
        
        // 1. Set the received offer as remote description
        var setRemoteSdpOperation = handle.PeerConnection.SetRemoteDescription(ref remoteSdpOffer);
        yield return setRemoteSdpOperation;
            
        if (setRemoteSdpOperation.IsError)
        {
            Debug.LogError("Failed to set remote description");
            yield break;
        }

        // 2. Generate Answer
        var createAnswerOperation = handle.PeerConnection.CreateAnswer();
        yield return createAnswerOperation;

        if (createAnswerOperation.IsError)
        {
            Debug.LogError("Failed to create answer");
            yield break;
        }

        var sdpAnswer = createAnswerOperation.Desc;

        // 3. Set the generated answer as local description
        var setLocalDspOperation = handle.PeerConnection.SetLocalDescription(ref sdpAnswer);
        yield return setLocalDspOperation;

        if (setLocalDspOperation.IsError)
        {
            Debug.LogError("Failed to set local description");
            yield break;
        }

        // 4. Send the answer to the other Peer
        videoManager?.SendSdpToOtherPeer(sdpAnswer, sessionId);
    }
    
    public IEnumerator OnRemoteSdpAnswerReceived(RTCSessionDescription remoteSdpAnswer, string sessionId)
    {
        Debug.Log($"WebRTCCamera {serial} -- Received SDP Answer {remoteSdpAnswer.sdp}");
            
        ConnectionHandle handle;
        if (!_connections.TryGetValue(sessionId, out handle))
        {
            Debug.LogError($"WebRTCCamera {serial} -- SessionId \"{sessionId}\" does not exist!");
            yield break;
        }
        
        // 1. Set the received answer as remote description
        var setRemoteSdpOperation = handle.PeerConnection.SetRemoteDescription(ref remoteSdpAnswer);
        yield return setRemoteSdpOperation;

        if (setRemoteSdpOperation.IsError)
        {
            Debug.LogError("Failed to set remote description");
        }
            
        Debug.Log($"WebRTCCamera {serial} -- Received SDP Answer successfully: {handle.PeerConnection.RemoteDescription.sdp}\nCurrent: {handle.PeerConnection.CurrentRemoteDescription.type} {handle.PeerConnection.CurrentRemoteDescription.sdp}, Pending: ");
        Debug.Log($"Video Manager -- Local: {handle.PeerConnection.LocalDescription.sdp}\nCurrent: {handle.PeerConnection.CurrentLocalDescription.type} {handle.PeerConnection.CurrentLocalDescription.sdp}, Pending:");
    }

    public void AddIceCandidate(RTCIceCandidate iceCandidate, string sessionId)
    {
        ConnectionHandle handle;
        if (!_connections.TryGetValue(sessionId, out handle))
        {
            Debug.LogError($"WebRTCCamera {serial} -- SessionId \"{sessionId}\" does not exist!");
            return;
        }
        
        handle.PeerConnection.AddIceCandidate(iceCandidate);
    }

    public void EndSession(string sessionId)
    {
        ConnectionHandle handle;
        if (!_connections.TryGetValue(sessionId, out handle))
        {
            Debug.LogError($"WebRTCCamera {serial} -- SessionId \"{sessionId}\" does not exist!");
            return;
        }
        
        handle.PeerConnection.Close();
    }
    
    // RTC Callbacks
    
    private void OnIceCandidate(RTCIceCandidate candidate, string sessionId)
    {
        if (candidate.Candidate.Contains("127.0.0.1"))
            return; // Skip loopback candidates
        videoManager?.SendIceCandidateToOtherPeer(candidate, sessionId);
    }
    
    private void OnNegotiationNeeded(string sessionId)
    {
        Debug.Log("SDP Offer <-> Answer exchange requested by the webRTC client.");
        StartCoroutine(PeerNegotiationNeeded(sessionId));
    }
    
    private void OnIceConnectionChange(RTCIceConnectionState state)
    {
        // we could also include the sessionId here, but im lazy
        Debug.Log($"WebRTCCamera {{serial}} -- IceConnectionState: {state}");
    }
    
    // RTC Callback Helpers
    
    private IEnumerator PeerNegotiationNeeded(string sessionId)
    {
        ConnectionHandle handle;
        if (!_connections.TryGetValue(sessionId, out handle))
        {
            Debug.LogError($"WebRTCCamera {serial} -- SessionId \"{sessionId}\" does not exist!");
            yield break;
        }
        
        var op = handle.PeerConnection.CreateOffer();
        yield return op;

        if (!op.IsError)
        {
            if (handle.PeerConnection.SignalingState != RTCSignalingState.Stable)
            {
                Debug.LogError($"Video Manager -- signaling state is not stable in peer negotiation coroutine.");
                yield break;
            }

            yield return StartCoroutine(OnCreateOfferSuccess(op.Desc, sessionId, handle));
        }
        else
        {
            Debug.LogError($"Video Manager -- Error in peer negotiation coroutine: {op.Error.message}");
        }
    }
    
    private IEnumerator OnCreateOfferSuccess(RTCSessionDescription desc, string sessionId, ConnectionHandle handle)
    {
        Debug.Log($"Offer: \n{desc.sdp}");
        Debug.Log($"setLocalDescription start");
        var op = handle.PeerConnection.SetLocalDescription(ref desc);
        yield return op;

        if (!op.IsError)
        {
            Debug.Log($"SetLocalDescription complete");
        }
        else
        {
            var error = op.Error;
            Debug.LogError($"Error Detail Type: {error.message}");
            // HangUp();
            yield break;
        }
            
        videoManager?.SendSdpToOtherPeer(desc, sessionId);
    }
    
    // Video data setup
    
    private void AddTracks(ref ConnectionHandle handle)
    {
        List<RTCRtpEncodingParameters> parameters = new List<RTCRtpEncodingParameters>();
        RTCRtpEncodingParameters encoder = new RTCRtpEncodingParameters();

        encoder = new RTCRtpEncodingParameters();
        encoder.rid = "l";
        encoder.active = true;
        encoder.maxFramerate = 30;
        encoder.maxBitrate = 300 * 1024;
        encoder.minBitrate = 100 * 1024;
        encoder.scaleResolutionDownBy = 0.5;
        parameters.Add(encoder);

        encoder = new RTCRtpEncodingParameters();
        encoder.rid = "m";
        encoder.active = true;
        encoder.maxFramerate = 30;
        encoder.maxBitrate = 250 * 1024;
        encoder.minBitrate = 500 * 1024;
        encoder.scaleResolutionDownBy = 0.8;
        parameters.Add(encoder);

        encoder = new RTCRtpEncodingParameters();
        encoder.rid = "h";
        encoder.active = true;
        encoder.maxFramerate = 30;
        encoder.maxBitrate = 400 * 1024;
        encoder.minBitrate = 1400 * 1024;
        encoder.scaleResolutionDownBy = 1.0;
        parameters.Add(encoder);

        RTCRtpTransceiverInit init = new RTCRtpTransceiverInit();
        init.direction = RTCRtpTransceiverDirection.SendOnly;
        init.sendEncodings = parameters.ToArray();

        var track = _videoStream.GetTracks().First();
        var transceiver = handle.PeerConnection.AddTransceiver(track, init);
        handle.PeerConnection.AddTrack(track, _videoStream);
        // _dataChannel = _peerConnection.CreateDataChannel("dummy");

        handle.Transceiver = transceiver;

        videoManager?.EnsureVideoUpdateStarted();
    }
}
