using RosSharp.RosBridgeClient;
using UnityEngine;
using StringMsg = RosSharp.RosBridgeClient.MessageTypes.Std.String;

public class TestPublisher : MonoBehaviour
{
    private Publisher<StringMsg> _publisher;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        _publisher = new Publisher<StringMsg>("/joe");
    }

    // Update is called once per frame
    private void Update()
    {
        _publisher.Publish(new("joe"));
    }
}
