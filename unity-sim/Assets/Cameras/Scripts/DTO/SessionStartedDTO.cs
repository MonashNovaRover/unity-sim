namespace Cameras.DTO
{
    [System.Serializable]
    public class SessionStartedDTO
    {
        public string type = "sessionStarted";
        public string peerId;
        public string sessionId;
    }
}