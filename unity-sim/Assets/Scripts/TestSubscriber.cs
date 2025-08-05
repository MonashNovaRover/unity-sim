using RosSharp.RosBridgeClient;
using UnityEngine;
using StringMsg = RosSharp.RosBridgeClient.MessageTypes.Std.String;

public class TestSubscriber : MonoBehaviour
{
    private Subscriber<StringMsg> _subscriber;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        _subscriber = new Subscriber<StringMsg>("/joe");
    }

    // Update is called once per frame
    private void Update()
    {
        if (_subscriber.Message != null)
        {
            Debug.Log("/joe: " + _subscriber.Message.data);
        }
    }
}
