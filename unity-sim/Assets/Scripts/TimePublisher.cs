using RosMessageTypes.BuiltinInterfaces;
using RosSharp.RosBridgeClient;
using RosUtils;
using UnityEngine;
using RosMessageTypes.Rosgraph;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using Unity.Robotics.ROSTCPConnector;
using Time = RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time;

public class TimePublisher : MonoBehaviour
{
    private ROSConnection ros;
    public string topicName = "/clock";
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<ClockMsg>(topicName);
        Debug.Log("Clock published on " + topicName);
    }

    // Update is called once per frame
    private void Update()
    {
        var timeInSeconds = UnityEngine.Time.timeAsDouble;
        var secs = (int)timeInSeconds;
        var nsecs = (uint)((timeInSeconds - secs) * 1e9);
        ros.Publish(topicName, new ClockMsg(new TimeMsg(secs, nsecs)));
    }

    public static TimeMsg GetCurrentTime()
    {
        var timeInSeconds = UnityEngine.Time.timeAsDouble;
        var secs = (int)timeInSeconds;
        var nsecs = (uint)((timeInSeconds - secs) * 1e9);
        var stamp = new TimeMsg(secs, nsecs);
        return stamp;
    }
}