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
using UnityEditorInternal;
using UnityEngine.Serialization;
using WebSocketSharp;

namespace Cameras
{
    public class VideoManager : MonoBehaviour
    {
        public WebRTCManager client;
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
        
        class CamHandle
        {
            public WebRTCCamera cam;
            
            public ProducerDTO Producer => new ProducerDTO()
            {
                meta = new ProducerMetaDTO()
                {
                    serial = cam.serial,
                },
                id = cam.PeerId
            };

            public CamHandle(WebRTCCamera cam)
            {
                this.cam = cam;
            }
        }

        private List<CamHandle> _handles = new List<CamHandle>() { };
        private Dictionary<string, CamHandle> _peerIdToHandle = new Dictionary<string, CamHandle>();
        private Dictionary<string, CamHandle> _sessionIdToHandle = new Dictionary<string, CamHandle>();

        private Publisher<RosSharp.RosBridgeClient.MessageTypes.Camera.Cameras> _camerasPublisher;
        
        public static VideoManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<VideoManager>();
                return _instance;
            }
        }
        private static VideoManager _instance = null;
        
        protected void Awake()
        {
            _instance = this;
            _camerasPublisher =
                new Publisher<RosSharp.RosBridgeClient.MessageTypes.Camera.Cameras>("/camera_directory/cameras");
            
            // FindObjectOfType is used for the demo purpose only. In a real production it's better to avoid it for performance reasons
            client ??= WebRTCManager.Instance ?? FindFirstObjectByType<WebRTCManager>();

            // Triggered when a new message is received from the other peer via WebSocket
            client.MessageReceived += OnWebSocketMessageReceived;
        }
        

        

        public void SendIceCandidateToOtherPeer(RTCIceCandidate iceCandidate, string sessionId)
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

        public void SendSdpToOtherPeer(RTCSessionDescription sdp, string sessionId)
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
            cams.cameras = _handles.Select(track =>
            {
                var camera = new RosSharp.RosBridgeClient.MessageTypes.Camera.Camera();
                camera.serial = track.Producer.meta.serial;
                return camera;
            }).ToArray();
            _camerasPublisher.Publish(cams);
        }
        

        private void OnWebSocketMessageReceived(string message)
        {
            var dtoWrapper = JsonUtility.FromJson<DTOWrapper>(message) ?? new DTOWrapper();
            var type = dtoWrapper.type;
            
            CamHandle handle;
            
            switch (type)
            {
                case "list":
                    Debug.Log("Video Manager -- list command");
                    PublishCameras(); 
                    
                    var comma = false;
                    var producers = String.Empty;
                    foreach (var trackHandle in _handles)
                    {
                        if (comma) 
                            producers += ", ";
                        
                        producers += JsonUtility.ToJson(trackHandle.Producer);
                        comma = true;
                    }
                    
                    client.SendWebSocketMessage($"{{\"type\":\"list\",\"producers\":[{producers}]}}");
                    break;
                case "startSession":
                    var startSession = JsonUtility.FromJson<StartSessionDTO>(message);
                    Debug.Log($"Video Manager -- startSession command for peerId {startSession.peerId}");

                    if (!_peerIdToHandle.TryGetValue(startSession.peerId, out handle))
                    {
                        Debug.LogError($"Video Manager -- PeerId {startSession.peerId} not found!");
                        return;
                    }

                    var sessionId = Guid.NewGuid().ToString();
                    _sessionIdToHandle[sessionId] = handle;
                    handle.cam.StartSession(sessionId);
                    
                    // TODO: Remove
                    lastSessionId = sessionId;
                    break;
                case "peer":
                    var peerDto = JsonUtility.FromJson<PeerDTO>(message);

                    if (!_sessionIdToHandle.TryGetValue(peerDto.sessionId, out handle))
                    {
                        Debug.LogError($"Video Manager -- SessionId {peerDto.sessionId} not found!");
                        return;
                    }
                    
                    if (peerDto.sdp != null && !peerDto.sdp.sdp.IsNullOrEmpty())
                    {
                        switch (peerDto.sdp.type)
                        {
                            case "offer":
                                StartCoroutine(handle.cam.OnRemoteSdpOfferReceived(new RTCSessionDescription()
                                {
                                    sdp = peerDto.sdp.sdp,
                                    type = RTCSdpType.Offer
                                }, peerDto.sessionId));
                                break;
                            case "answer":
                                StartCoroutine(handle.cam.OnRemoteSdpAnswerReceived(new RTCSessionDescription()
                                {
                                    sdp = peerDto.sdp.sdp,
                                    type = RTCSdpType.Answer
                                }, peerDto.sessionId));
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

                        handle.cam.AddIceCandidate(ice, peerDto.sessionId);
                        Debug.Log($"Received ICE Candidate: {ice.Candidate}");
                    }
                    break;
                case "endSession":
                    var endSession = JsonUtility.FromJson<EndSessionDTO>(message);
                    if (!_sessionIdToHandle.TryGetValue(endSession.sessionId, out handle))
                    {
                        Debug.LogError($"Video Manager -- SessionId {endSession.sessionId} not found!");
                        return;
                    }
                    
                    handle.cam.EndSession(endSession.sessionId);
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


        

        

        // For if we ever want to receive video, rather than send:
        /*
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
        */

        
        // Public methods to interact with the video manager:
        

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

        public void EnsureVideoUpdateStarted()
        {
            if (videoUpdateStarted)
                return;
            
            StartCoroutine(WebRTC.Update());
            videoUpdateStarted = true;
        }

        public void RegisterCam(WebRTCCamera cam)
        {
            var handle = new CamHandle(cam);
            
            _handles.Add(handle);
            _peerIdToHandle[cam.PeerId] = handle;
        }

        public void UnregisterCam(WebRTCCamera cam)
        {
            _handles.RemoveAll(handle => handle.cam == cam);
            _peerIdToHandle.Remove(cam.PeerId);
        }
    }
}