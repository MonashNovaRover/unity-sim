namespace Cameras.DTO
{
    [System.Serializable]
    public class EndSessionDTO
    {
        public string type = "endSession";
        public string peerId;
        public string sessionId;

    }
}