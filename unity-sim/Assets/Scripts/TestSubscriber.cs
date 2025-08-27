using RosSharp.RosBridgeClient;
using UnityEngine;
using StringMsg = RosSharp.RosBridgeClient.MessageTypes.Std.String;
using RosUtils;
using UnityEngine.Serialization;

public class TestSubscriber : MonoBehaviour
{
    [SerializeField]
    private Subscriber<StringMsg> subscriber = new("/joe");
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        subscriber.Connect();
    }

    // Update is called once per frame
    private void Update()
    {
        if (subscriber.Message != null)
        {
            Debug.Log("/joe: " + subscriber.Message.data);
        }
    }
}
