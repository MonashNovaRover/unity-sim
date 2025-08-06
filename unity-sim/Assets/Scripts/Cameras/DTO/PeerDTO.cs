using JetBrains.Annotations;
using Newtonsoft.Json;

namespace Cameras.DTO
{
    [System.Serializable]
    public class PeerDTO
    {
        [System.Serializable]
        public class PeerSdpDTO
        {
            public string type = "offer";     //  "answer" | "offer" | "pranswer" | "rollback"
            public string sdp;
        }
        
        /// <summary>
        /// DTO (Data Transfer Object) to send/receive ICE Candidate through the network. This DTO maps to <see cref="RTCIceCandidate"/>
        /// </summary>
        [System.Serializable]
        public class ICECanddidateDTO
        {
            public string candidate;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            [CanBeNull] public string sdpMid;
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? sdpMLineIndex = 0;
        }
        
        public string type = "peer";
        public string sessionId;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [CanBeNull] public PeerSdpDTO sdp;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [CanBeNull] public ICECanddidateDTO ice;

    }
}