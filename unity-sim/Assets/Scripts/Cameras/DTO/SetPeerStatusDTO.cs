namespace Cameras.DTO
{
    [System.Serializable]
    public class SetPeerStatusDTO
    {
        public string type = "setPeerStatus";
        public string[] roles;
    }
}