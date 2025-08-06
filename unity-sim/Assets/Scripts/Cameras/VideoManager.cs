using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.WebRTC;
using UnityEngine;
using Cameras.DTO;
using Newtonsoft.Json;
using RosSharp.RosBridgeClient.MessageTypes.UniqueIdentifier;
using RosUtils;
using UnityEngine.Serialization;
using WebSocketSharp;

namespace Cameras
{
    public class VideoManager : MonoBehaviour
    {
        [SerializeField]
        private WebRTCManager client;
        private RTCPeerConnection _peerConnection;
        private RTCRtpTransceiver _pcTransceiver;
        private MediaStream _videoStream;
        // private RTCDataChannel _dataChannel;
        public int width = 1280;
        public int height = 720;
        /// Whether the WebRTC.Update coroutine has been started yet
        bool videoUpdateStarted = false;

        private string lastSessionId;

        [System.Serializable]
        class ProducerMetaDTO
        {
            public string serial;
        }
        
        [System.Serializable]
        class ProducerDTO
        {
            public ProducerMetaDTO meta;
            public string id;
        }
        
        class TrackHandle
        {
            public ProducerDTO producer; 
        }
        
        private List<TrackHandle> _tracks = new List<TrackHandle>()
        {
            new TrackHandle()
            {
                producer = new ProducerDTO()
                {
                    meta = new ProducerMetaDTO()
                    {
                        serial = "unity1"  
                    },
                    id = "unity"
                }
            }
        };

        private Publisher<RosSharp.RosBridgeClient.MessageTypes.Camera.Cameras> _camerasPublisher;
        
     
        protected void Awake()
        {
            _camerasPublisher =
                new Publisher<RosSharp.RosBridgeClient.MessageTypes.Camera.Cameras>("/camera_directory/cameras");
            
            // FindObjectOfType is used for the demo purpose only. In a real production it's better to avoid it for performance reasons
            client ??= WebRTCManager.Instance ?? FindFirstObjectByType<WebRTCManager>();

            // Triggered when a new message is received from the other peer via WebSocket
            client.MessageReceived += OnWebSocketMessageReceived;
        }
        
        private void OnNegotiationNeeded()
        {
            Debug.Log("SDP Offer <-> Answer exchange requested by the webRTC client.");

            StartCoroutine(PeerNegotiationNeeded());
        }

        private IEnumerator PeerNegotiationNeeded()
        {
            var op = _peerConnection.CreateOffer();
            yield return op;

            if (!op.IsError)
            {
                if (_peerConnection.SignalingState != RTCSignalingState.Stable)
                {
                    Debug.LogError($"Video Manager -- signaling state is not stable in peer negotiation coroutine.");
                    yield break;
                }

                yield return StartCoroutine(OnCreateOfferSuccess(op.Desc));
            }
            else
            {
                Debug.LogError($"Video Manager -- Error in peer negotiation coroutine: {op.Error.message}");
            }
        }
        
        private IEnumerator OnCreateOfferSuccess(RTCSessionDescription desc)
        {
            Debug.Log($"Offer: \n{desc.sdp}");
            Debug.Log($"setLocalDescription start");
            var op = _peerConnection.SetLocalDescription(ref desc);
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
            
            SendSdpToOtherPeer(desc, lastSessionId);
        }

        private void SendIceCandidateToOtherPeer(RTCIceCandidate iceCandidate, string sessionId)
        {
            var iceDto = new PeerDTO.ICECanddidateDTO
            {
                candidate = iceCandidate.Candidate,
                sdpMid = iceCandidate.SdpMid,
                sdpMLineIndex = iceCandidate.SdpMLineIndex
            };
            
            var peerDto = new PeerDTO()
            {
                sessionId = sessionId,
                ice = iceDto
            };
            
            var serializedDto = JsonConvert.SerializeObject(peerDto);
            client.SendWebSocketMessage(serializedDto);
        }

        private void SendSdpToOtherPeer(RTCSessionDescription sdp, string sessionId)
        {
            var sdpDto = new PeerDTO.PeerSdpDTO()
            {
                type = sdp.type.ToString().ToLower(),
                sdp = sdp.sdp
            };

            var peerDto = new PeerDTO()
            {
                sdp = sdpDto,
                ice = null,
                sessionId = sessionId 
            };
            
            
            client.SendWebSocketMessage(JsonConvert.SerializeObject(peerDto));
        }

        private void PublishCameras()
        {
            var cams = new RosSharp.RosBridgeClient.MessageTypes.Camera.Cameras();
            cams.cameras = _tracks.Select(track =>
            {
                var camera = new RosSharp.RosBridgeClient.MessageTypes.Camera.Camera();
                camera.serial = track.producer.meta.serial;
                return camera;
            }).ToArray();
            _camerasPublisher.Publish(cams);
        }
        
        private void OnIceConnectionChange(RTCIceConnectionState state)
        {
            Debug.Log($"IceConnectionState: {state}");
        }

        private void OnWebSocketMessageReceived(string message)
        {
            var dtoWrapper = JsonUtility.FromJson<DTOWrapper>(message) ?? new DTOWrapper();
            var type = dtoWrapper.type;
            
            switch (type)
            {
                case "list":
                    Debug.Log("Video Manager -- list command");
                    PublishCameras(); 
                    
                    var comma = false;
                    var producers = String.Empty;
                    foreach (var trackHandle in _tracks)
                    {
                        if (comma) 
                            producers += ", ";
                        
                        producers += JsonUtility.ToJson(trackHandle.producer);
                        comma = true;
                    }
                    
                    client.SendWebSocketMessage($"{{\"type\":\"list\",\"producers\":[{producers}]}}");
                    break;
                case "startSession":
                    Debug.Log("Video Manager -- startSession command");

                    lastSessionId = Guid.NewGuid().ToString();
                    var started = new SessionStartedDTO()
                    {
                        sessionId = lastSessionId,
                        peerId = "unity"
                    };
                    
                    if (_videoStream == null)
                    {
                        _videoStream = Camera.main.CaptureStream(width, height);
                    }
                    
                    
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
                        // iceCandidatePoolSize = 3,
                        // iceTransportPolicy = RTCIceTransportPolicy.All,
                    };
                    // _pc1 = new RTCPeerConnection(ref configuration);
                    // _pc1.OnIceCandidate = pc1OnIceCandidate;
                    // _pc1.OnIceConnectionChange = pc1OnIceConnectionChange;
                    // _pc1.OnNegotiationNeeded = pc1OnNegotiationNeeded;
                    
                    _peerConnection = new RTCPeerConnection(ref config);

                    // Triggered when a new network endpoint is found that could potentially be used to establish the connection
                    _peerConnection.OnIceCandidate += OnIceCandidate;
                    
                    // "Negotiation" is the exchange of SDP Offer/Answer. Peers describe what media they want to send and agree on, for example, what codecs to use
                    // In this tutorial we exchange the SDP Offer/Answer only once when connecting.
                    // But in a real production you'd have to repeat the exchange every time the OnNegotiationNeeded event is triggered
                    _peerConnection.OnNegotiationNeeded += OnNegotiationNeeded;
                    _peerConnection.OnIceConnectionChange += OnIceConnectionChange;
                    _peerConnection.OnIceGatheringStateChange += state => Debug.Log($"IceGatheringStateChange: {state}"); 

                    // Triggered when a new track is received
                    _peerConnection.OnTrack += OnTrack;
                    
                    AddTracks();
                    
                    // client.SendWebSocketMessage($"{{\"type\":\"startSession\",\"sessionId\":\"session\"}}");
                    client.SendWebSocketMessage(JsonUtility.ToJson(started));
                    // StartCoroutine(CreateAndSendLocalSdpOffer(lastSessionId));
                    
                    break;
                case "peer":
                    var peerDto = JsonUtility.FromJson<PeerDTO>(message);

                    if (peerDto.sdp != null && !peerDto.sdp.sdp.IsNullOrEmpty())
                    {
                        switch (peerDto.sdp.type)
                        {
                            case "offer":
                                StartCoroutine(OnRemoteSdpOfferReceived(new RTCSessionDescription()
                                {
                                    sdp = peerDto.sdp.sdp,
                                    type = RTCSdpType.Offer
                                }, peerDto.sessionId));
                                break;
                            case "answer":
                                StartCoroutine(OnRemoteSdpAnswerReceived(new RTCSessionDescription()
                                {
                                    sdp = peerDto.sdp.sdp,
                                    type = RTCSdpType.Answer
                                }));
                                break;
                            default:
                                Debug.LogError($"Video Manager -- Unknown SDP type \"{peerDto.sdp.type}\"");
                                throw new ArgumentOutOfRangeException("Unhandled type of SDP message: " + peerDto.sdp.type);
                        }
                    }

                    if (peerDto.ice != null && !peerDto.ice.candidate.IsNullOrEmpty())
                    {
                        var ice = new RTCIceCandidate(new RTCIceCandidateInit
                        {
                            candidate = peerDto.ice.candidate,
                            sdpMid = peerDto.ice.sdpMid,
                            sdpMLineIndex = peerDto.ice.sdpMLineIndex 
                        });

                        _peerConnection.AddIceCandidate(ice);
                        Debug.Log($"Received ICE Candidate: {ice.Candidate}");
                    }
                    break;
                case "endSession":
                    
                    break;
                default:
                    Debug.LogError($"Video Manager -- Unknown command type \"{dtoWrapper.type}\"");
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private IEnumerator CreateAndSendLocalSdpOffer(string sessionId)
        {
            // 1. Create local SDP offer
            var createOfferOperation = _peerConnection.CreateOffer();
            yield return createOfferOperation;

            if (createOfferOperation.IsError)
            {
                Debug.LogError("Failed to create offer");
                yield break;
            }

            var sdpOffer = createOfferOperation.Desc;

            // 2. Set the offer as a local SDP 
            var setLocalSdpOperation = _peerConnection.SetLocalDescription(ref sdpOffer);
            yield return setLocalSdpOperation;

            if (setLocalSdpOperation.IsError)
            {
                Debug.LogError("Failed to set local description");
                yield break;
            }

            // 3. Send the SDP Offer to the other Peer
            SendSdpToOtherPeer(sdpOffer, sessionId);
            Debug.Log("Sent Sdp Offer");
        }

        private IEnumerator OnRemoteSdpOfferReceived(RTCSessionDescription remoteSdpOffer, string sessionId)
        {
            Debug.Log("Remote SDP Offer received. Set as local offer and send back the generated answer");

            // 1. Set the received offer as remote description
            var setRemoteSdpOperation = _peerConnection.SetRemoteDescription(ref remoteSdpOffer);
            yield return setRemoteSdpOperation;
            
            if (setRemoteSdpOperation.IsError)
            {
                Debug.LogError("Failed to set remote description");
                yield break;
            }

            // 2. Generate Answer
            var createAnswerOperation = _peerConnection.CreateAnswer();
            yield return createAnswerOperation;

            if (createAnswerOperation.IsError)
            {
                Debug.LogError("Failed to create answer");
                yield break;
            }

            var sdpAnswer = createAnswerOperation.Desc;

            // 3. Set the generated answer as local description
            var setLocalDspOperation = _peerConnection.SetLocalDescription(ref sdpAnswer);
            yield return setLocalDspOperation;

            if (setLocalDspOperation.IsError)
            {
                Debug.LogError("Failed to set local description");
                yield break;
            }

            // 4. Send the answer to the other Peer
            SendSdpToOtherPeer(sdpAnswer, sessionId);
            Debug.Log("Sent Sdp Answer");
        }

        private IEnumerator OnRemoteSdpAnswerReceived(RTCSessionDescription remoteSdpAnswer)
        {
            Debug.Log($"Video Manager -- Received SDP Answer {remoteSdpAnswer.sdp}");
            
            // 1. Set the received answer as remote description
            var setRemoteSdpOperation = _peerConnection.SetRemoteDescription(ref remoteSdpAnswer);
            yield return setRemoteSdpOperation;

            if (setRemoteSdpOperation.IsError)
            {
                Debug.LogError("Failed to set remote description");
            }
            
            Debug.Log($"Video Manager -- Received SDP Answer success? {_peerConnection.RemoteDescription.sdp}\nCurrent: {_peerConnection.CurrentRemoteDescription.type} {_peerConnection.CurrentRemoteDescription.sdp}, Pending: ");
            Debug.Log($"Video Manager -- Local: {_peerConnection.LocalDescription.sdp}\nCurrent: {_peerConnection.CurrentLocalDescription.type} {_peerConnection.CurrentLocalDescription.sdp}, Pending:");
        }
        
        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            if (candidate.Candidate.Contains("127.0.0.1"))
                return; // Skip loopback candidates
            SendIceCandidateToOtherPeer(candidate, lastSessionId);
            SendIceCandidateToOtherPeer(candidate, lastSessionId);
            Debug.Log("Sent Ice Candidate to the other peer THREAD  " + Thread.CurrentThread.ManagedThreadId);
        }

        public event Action<Texture> RemoteVideoReceived;

        private void OnTrack(RTCTrackEvent trackEvent)
        {
            Debug.Log("OnTrack received, type: " + trackEvent.Track.Kind);

            if (trackEvent.Track is VideoStreamTrack videoStreamTrack)
            {
                videoStreamTrack.OnVideoReceived += OnVideoReceived;
            }
            else
            {
                Debug.LogError(
                    $"Unhandled track of type: {trackEvent.Track.GetType()}. In this tutorial, we're handling only video tracks.");
            }
        }

        private void OnVideoReceived(Texture texture)
        {
            Debug.Log($"Video received, resolution: {texture.width}x{texture.height}");
            RemoteVideoReceived?.Invoke(texture);
        }
        
        // Public methods to interact with the video manager:
        
        public bool CanConnect
            => _peerConnection?.ConnectionState == RTCPeerConnectionState.New ||
               _peerConnection?.ConnectionState == RTCPeerConnectionState.Disconnected;
        
        public bool IsConnected => _peerConnection?.ConnectionState == RTCPeerConnectionState.Connecting;

        public void SetActiveCamera(WebCamTexture activeWebCamTexture)
        {
            // Remove previous track
            var senders = _peerConnection.GetSenders();
            foreach (var sender in senders)
            {
                _peerConnection.RemoveTrack(sender);
            }

            var videoTrack = new VideoStreamTrack(activeWebCamTexture);
            _peerConnection.AddTrack(videoTrack);
            
            Debug.Log("Sender video track was set");
        }

        public void Connect()
        {
            StartCoroutine(CreateAndSendLocalSdpOffer(Guid.NewGuid().ToString()));
        }

        public void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }

            _peerConnection.Close();
            _peerConnection.Dispose();
        }

        private void AddTracks()
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
            encoder.maxBitrate = 200 * 1024;
            encoder.minBitrate = 400 * 1024;
            encoder.scaleResolutionDownBy = 0.8;
            parameters.Add(encoder);

            encoder = new RTCRtpEncodingParameters();
            encoder.rid = "h";
            encoder.active = true;
            encoder.maxFramerate = 30;
            encoder.maxBitrate = 300 * 1024;
            encoder.minBitrate = 800 * 1024;
            encoder.scaleResolutionDownBy = 1.0;
            parameters.Add(encoder);

            RTCRtpTransceiverInit init = new RTCRtpTransceiverInit();
            init.direction = RTCRtpTransceiverDirection.SendOnly;
            init.sendEncodings = parameters.ToArray();

            var track = _videoStream.GetTracks().First();
            var transceiver = _peerConnection.AddTransceiver(track, init);
            _peerConnection.AddTrack(track, _videoStream);
            // _dataChannel = _peerConnection.CreateDataChannel("dummy");

            _pcTransceiver = transceiver;

            if (!videoUpdateStarted)
            {
                StartCoroutine(WebRTC.Update());
                videoUpdateStarted = true;
            }
        }

        
    }
}