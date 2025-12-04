using RosSharp.RosBridgeClient.MessageTypes.Sensor;
using RosSharp.RosBridgeClient.MessageTypes.Std;
using RosUtils;
using UnityEngine;
using Time = RosSharp.RosBridgeClient.MessageTypes.BuiltinInterfaces.Time;

[RequireComponent(typeof(GpuReadbackCameraCapture))]
public class RosCamera : MonoBehaviour
{
    [Header("ROS2")]
    /// The name of the ROS2 topic to publish to.
    [SerializeField]
    private string topic;
    [SerializeField] 
    private string frameId = "";

    [Header("References")]
    /// The component used to get camera frames.
    private GpuReadbackCameraCapture cameraCapture;

    // Private member variables
    
    private Publisher<Image> _pub;

    /// <summary>
    ///     Callback for when a new camera frame is available
    /// </summary>
    /// <param name="data">a RGBA8 byte array (width * height * 4)</param>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    private void HandleFrameCaptured(byte[] data, int width, int height)
    {
        var timeInSeconds = UnityEngine.Time.timeAsDouble;
        var secs = (int)timeInSeconds;
        var nsecs = (uint)((timeInSeconds - secs) * 1e9);
        var stamp = new Time(secs, nsecs);

        var header = new Header(stamp, frameId);
        var image = new Image
        {
            header = header,
            width = (uint)width,
            height = (uint)height,
            encoding = "rgba8",
            is_bigendian = 0,
            step = (uint)(width * 4),
            data = data
        };

        _pub.Publish(image);
    }

    private void Start()
    {
        cameraCapture = GetComponent<GpuReadbackCameraCapture>();
        _pub = new Publisher<Image>(topic);
        
        if (!cameraCapture && !TryGetComponent(out cameraCapture))
        {
            Debug.LogError("RosCamera doesn't have a cameraCapture reference assigned, or a GpuReadbackCameraCapture on the component.");
            enabled = false; 
            return;
        }

        RosManager.Instance.Socket.Advertise<Image>(topic);
        
        cameraCapture.enabled = true;
        cameraCapture.OnFrameCaptured += HandleFrameCaptured;
    }

    private void OnDestroy()
    {
        if (!cameraCapture)
            return;

        cameraCapture.OnFrameCaptured -= HandleFrameCaptured;
        _pub.Dispose();
    }
}